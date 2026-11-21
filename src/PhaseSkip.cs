// ============================================================================
//  PhaseSkip.cs — «без рёвов»: пока босс проходит смены фаз до выбранной, его цепочки состояний
//  (рёв, уход, приземление) пролетают за кадр-другой. Что пропускать — PhaseSkipData.cs.
//  Работает так: после загрузки комнаты (BossFx.Arm) ищем FSM босса, затем каждый кадр смотрим на его
//  активное состояние. Вошло в первое состояние цепочки — шлём FINISHED (или указанное событие), пока
//  босс не выйдет из цепочки. Первые N−1 смен фазы (N — выбранная фаза) пропускаем, остальные идут как в игре.
//  Если состояние не реагирует (ждёт чего-то снаружи), через 2,5 с цепочку отпускаем: босс доиграет её сам.
//  Чтобы пролетающие состояния не взрывались разом звуком, тряской камеры, вспышкой и выбросом рёва, у пропускаемых
//  цепочек заранее отключаются «косметические» действия (SkipData.IsMuted); после пропуска они включаются обратно.
//  Выключатель — настройка Mode/SkipPhaseAnimations. В статистику это не влияет: бой с выбранной фазой
//  и так отдельная строка (челлендж) или тренировка.
// ============================================================================
using System.Collections.Generic;
using HutongGames.PlayMaker;
using UnityEngine;

namespace Encore
{
    internal static class PhaseSkip
    {
        const float FindEvery = 0.25f, WindowSec = 90f, StuckSec = 2.5f;
        const int MaxStepsPerFrame = 16;

        sealed class Watch
        {
            internal SkipRule Rule;
            internal PlayMakerFSM Fsm;
            internal Dictionary<string, string> Events = new Dictionary<string, string>();   // состояние цепочки -> событие
            internal HashSet<string> Starts = new HashSet<string>();                        // первые состояния цепочек
            internal List<List<SkipStep>> Chains = new List<List<SkipStep>>();              // разобранные цепочки
            internal Dictionary<string, int> StartChain = new Dictionary<string, int>();    // первое состояние -> номер цепочки
            internal List<int> MinPhase = new List<int>();                                  // из префикса «N|»: пропускать при выбранной фазе ≥ N; 0 — по счёту входов
            internal List<FsmStateAction> Muted = new List<FsmStateAction>();               // отключённые действия
            internal int MutedTotal;
            internal int Entries;        // сколько цепочек уже началось
            internal bool InChain;
            internal string Last = "";
            internal float Moved;
            internal bool Done;
        }

        static List<SkipRule> rules;     // null — ничего не делаем
        static string boss, scene;
        static int wanted;               // сколько смен фазы пропускаем (выбранная фаза − 1)
        static float nextFind, until;
        static bool[] matched;
        static readonly List<Watch> watches = new List<Watch>();

        /// Комната босса загрузилась, босс заперт в фазе phase.
        internal static void Arm(string bossName, string sceneName, int phase)
        {
            Disarm();
            if (phase < 2 || !Plugin.SkipAnimations) return;
            List<SkipRule> found = SkipData.For(bossName);
            if (found.Count == 0) return;
            rules = found;
            matched = new bool[found.Count];
            boss = bossName;
            scene = sceneName;
            wanted = phase - 1;
            nextFind = 0f;
            until = Time.unscaledTime + WindowSec;
        }

        internal static void Disarm()
        {
            rules = null;
            matched = null;
            watches.Clear();
        }

        /// Раз в кадр. Без настройки — одна проверка на null.
        internal static void Tick()
        {
            if (rules == null) return;
            float now = Time.unscaledTime;
            if (now > until || !Runner.Active) { Finish(); return; }

            if (now >= nextFind && !AllMatched())
            {
                nextFind = now + FindEvery;
                Find();
            }

            bool allDone = AllMatched();
            for (int i = 0; i < watches.Count; i++)
            {
                Watch w = watches[i];
                if (!w.Done) Step(w, now);
                if (!w.Done) allDone = false;
            }
            if (allDone) Finish();
        }

        static bool AllMatched()
        {
            for (int i = 0; i < matched.Length; i++) if (!matched[i]) return false;
            return true;
        }

        /// Ищем FSM боссов этой комнаты (включая выключенные объекты).
        static void Find()
        {
            PlayMakerFSM[] all = Object.FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int r = 0; r < rules.Count; r++)
            {
                if (matched[r]) continue;
                SkipRule rule = rules[r];
                foreach (PlayMakerFSM fsm in all)
                {
                    if (fsm.FsmName != rule.Fsm || fsm.gameObject.name != rule.Object || !Rooms.Same(fsm.gameObject.scene.name, scene)) continue;
                    var w = new Watch();
                    w.Rule = rule;
                    w.Fsm = fsm;
                    foreach (string chain in rule.Chains)
                    {
                        List<SkipStep> steps = SkipData.Parse(chain);
                        w.MinPhase.Add(SkipData.MinPhase(chain));
                        w.Starts.Add(steps[0].State);
                        w.StartChain[steps[0].State] = w.Chains.Count;
                        w.Chains.Add(steps);
                        foreach (SkipStep step in steps) w.Events[step.State] = step.Event;
                    }
                    // цепочки, которые точно пропустим (первые wanted по порядку), глушим заранее — вместе со входом
                    for (int c = 0; c < w.Chains.Count; c++) if (Skips(w, c, c)) Mute(w, c, true);
                    ApplyPresets(w);
                    ApplyDetours(w);
                    watches.Add(w);
                    matched[r] = true;
                }
            }
        }

        /// Пропускаем ли цепочку chainNo, если до неё уже было entries входов: с префиксом «N|» — по выбранной фазе, без — по счёту.
        static bool Skips(Watch w, int chainNo, int entries)
        {
            int min = w.MinPhase[chainNo];
            return min > 0 ? wanted + 1 >= min : entries < wanted;
        }

        /// Флаги «переход уже был» (SkipData.Presets): ярость Колокольного зверя, огонь Судьи. Один раз, когда нашли FSM.
        static void ApplyPresets(Watch w)
        {
            foreach (SkipData.Preset p in SkipData.PresetsFor(w.Rule.Boss, wanted + 1))
            {
                if (p.Object != w.Rule.Object || p.Fsm != w.Rule.Fsm) continue;
                try
                {
                    FsmBool flag = w.Fsm.FsmVariables.FindFsmBool(p.Var);
                    if (flag == null) { Plugin.Log.LogInfo("Босс " + boss + ": в FSM нет флага «" + p.Var + "» — переход пропустится цепочкой."); continue; }
                    flag.Value = true;
                    Plugin.Log.LogInfo("Босс " + boss + ": флаг «" + p.Var + "» поставлен заранее — переход фазы не повторяется.");
                }
                catch (System.Exception e)
                {
                    Plugin.Log.LogWarning("Босс " + boss + ": флаг «" + p.Var + "» не поставился: " + e.Message);
                }
            }
        }

        /// Обходы (SkipData.Detours): все переходы состояния ведём в другое. Один раз, когда нашли FSM (до того, как босс дойдёт до состояния).
        static void ApplyDetours(Watch w)
        {
            foreach (SkipData.Detour d in SkipData.DetoursFor(w.Rule.Boss, wanted + 1))
            {
                if (d.Object != w.Rule.Object || d.Fsm != w.Rule.Fsm) continue;
                try
                {
                    FsmState from = w.Fsm.Fsm.GetState(d.From), to = w.Fsm.Fsm.GetState(d.To);
                    if (from == null || to == null || from.Transitions == null)
                    {
                        Plugin.Log.LogWarning("Босс " + boss + ": нет состояний «" + d.From + "» или «" + d.To + "» — обход первой атаки не настроен.");
                        continue;
                    }
                    int n = 0;
                    foreach (FsmTransition t in from.Transitions)
                    {
                        if (t == null) continue;
                        t.ToState = to.Name;
                        t.ToFsmState = to;
                        n++;
                    }
                    Plugin.Log.LogInfo("Босс " + boss + ": «" + d.From + "» ведёт сразу в «" + d.To + "» (переходов: " + n + ") — смена фазы без первой атаки.");
                }
                catch (System.Exception e)
                {
                    Plugin.Log.LogWarning("Босс " + boss + ": обход «" + d.From + "» → «" + d.To + "» не настроен: " + e.Message);
                }
            }
        }

        static void Step(Watch w, float now)
        {
            if (w.Fsm == null) { w.Done = true; return; }     // объект уничтожен
            string name = w.Fsm.ActiveStateName;
            if (!w.InChain)
            {
                int chainNo;
                if (name != w.Last && w.Starts.Contains(name) && w.StartChain.TryGetValue(name, out chainNo) && Skips(w, chainNo, w.Entries))
                {
                    w.Entries++;
                    w.InChain = true;
                    w.Moved = now;
                    Mute(w, chainNo, false);
                }
                w.Last = name;
            }
            if (w.InChain) Advance(w, now);
            if (w.Fsm != null) w.Last = w.Fsm.ActiveStateName;
            if (!w.InChain && w.Entries >= wanted) { w.Done = true; Unmute(w); }
        }

        /// Отключает косметические действия (звук, тряска, вспышки, выброс рёва) в состояниях цепочки.
        /// Вход цепочки при includeEntry=false не трогаем: его действия уже выполнены.
        static void Mute(Watch w, int chainNo, bool includeEntry)
        {
            if (w.Fsm == null || w.Fsm.Fsm == null) return;
            List<SkipStep> steps = w.Chains[chainNo];
            string active = w.Fsm.ActiveStateName;
            for (int i = includeEntry ? 0 : 1; i < steps.Count; i++)
            {
                if (steps[i].State == active) continue;
                FsmState st = w.Fsm.Fsm.GetState(steps[i].State);
                if (st == null) continue;
                FsmStateAction[] acts = st.Actions;
                if (acts == null) continue;
                for (int a = 0; a < acts.Length; a++)
                {
                    FsmStateAction act = acts[a];
                    if (act == null || !act.Enabled) continue;
                    bool mute = SkipData.IsMuted(w.Rule.Boss, act.GetType().Name);
                    // вызов подкрепления: оставляем только у последней из пропущенных цепочек (с неё начинается выбранная фаза)
                    if (!mute && chainNo != wanted - 1 && SkipData.IsCallEvent(w.Rule.Boss, EventOf(act))) mute = true;
                    if (!mute) continue;
                    act.Enabled = false;
                    w.Muted.Add(act);
                    w.MutedTotal++;
                }
            }
        }

        /// Имя события у SendEventByName (по типу действия, без ссылки на сборку PlayMaker-действий); у остальных действий null.
        static string EventOf(FsmStateAction act)
        {
            if (act.GetType().Name != "SendEventByName") return null;
            System.Reflection.FieldInfo field = act.GetType().GetField("sendEvent");
            FsmString ev = field == null ? null : field.GetValue(act) as FsmString;
            return ev == null ? null : ev.Value;
        }

        static void Unmute(Watch w)
        {
            for (int i = 0; i < w.Muted.Count; i++)
                if (w.Muted[i] != null) w.Muted[i].Enabled = true;
            w.Muted.Clear();
        }

        /// Шлёт событие, пока босс в состоянии цепочки; вышел из цепочки — всё.
        static void Advance(Watch w, float now)
        {
            for (int i = 0; i < MaxStepsPerFrame; i++)
            {
                string name = w.Fsm.ActiveStateName;
                string ev;
                if (!w.Events.TryGetValue(name, out ev)) { w.InChain = false; return; }
                w.Fsm.SendEvent(ev);
                if (w.Fsm.ActiveStateName == name)
                {
                    if (now - w.Moved > StuckSec)
                    {
                        w.InChain = false;
                        Unmute(w);
                        Plugin.Log.LogInfo("Босс " + boss + ": состояние «" + name + "» не отозвалось на " + ev + " — дальше идёт как в игре.");
                    }
                    return;
                }
                w.Moved = now;
            }
        }

        static void Finish()
        {
            int skipped = 0, found = 0, muted = 0, entered = 0;
            string perFsm = "";
            foreach (Watch w in watches)
            {
                found++;
                if (w.Entries > skipped) skipped = w.Entries;
                entered += w.Entries;
                muted += w.MutedTotal;
                perFsm += (perFsm.Length > 0 ? ", " : "") + w.Rule.Fsm + " " + w.Entries;
                Unmute(w);
            }
            if (found == 0)
                Plugin.Log.LogInfo("Босс " + boss + ": рёвы смены фазы не пропускались (объект босса для этого не нашёлся).");
            else if (found > 1)   // рёвы босса разбросаны по нескольким FSM (Grand Mother Silk): считаем по каждому
                Plugin.Log.LogInfo("Босс " + boss + ": пропущено рёвов смены фазы: " + entered + " (" + perFsm + "), заглушено эффектов: " + muted + ".");
            else
                Plugin.Log.LogInfo("Босс " + boss + ": пропущено смен фазы (рёв, уход): " + skipped + " из " + wanted + ", заглушено эффектов: " + muted + ".");
            Disarm();
        }
    }
}
