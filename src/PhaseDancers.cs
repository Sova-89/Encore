// ============================================================================
//  PhaseDancers.cs — «только фаза N» для двух боссов-танцоров: Cogwork Dancers (4 фазы) и Clover Dancers (2 фазы в GoP).
//  Здесь нет общего здоровья с порогами: главный FSM «Dancer Control» при входе в каждую фазу сам ставит обоим танцорам
//  своё здоровье (SetHP «Phase k HP» — его GoP берёт из таблицы, её подменяет BossFx.ApplyTable). Фаза кончается, когда
//  здоровье одного из танцоров упало до нуля: у танцора срабатывает «ZERO HP» → «Stun Stagger» → «DANCER STUNNED»
//  главному FSM (у того это глобальный переход) → «Dancers Stunned» → «Return Dancers» → «Phase Check» → «Set Phase k+1» →
//  ожидание удара такта → «Start Phase k+1». Две фазы смерти (в Cogwork после третьей и четвёртой, в Clover после второй)
//  идут через «Dancer Death» → «Check Killed»: первый убитый — «ONE KILLED» (дальше «Set Final Phase»), второй — «BOTH KILLED».
//  Что делаем:
//   • Пропуск фаз до выбранной. В «Start Phase k» (k < N) вставляем свою метку; по метке следующим кадром шлём танцору
//     «Dancer A» то же «ZERO HP», что шлёт нулевое здоровье: дальше всё идёт обычным путём игры (стан, смерть первого танцора
//     после третьей фазы Cogwork). Музыку пропускаемых фаз («START Pk» главному «Music Control») отключаем.
//   • Конец боя после выбранной фазы N (если она не последняя): у «Dancers Stunned» порог смерти ставим равным N, счётчик
//     убитых заранее ставим в 1 — тогда стан в фазе N ведёт «Dancer Death» → «BOTH KILLED» → «Final Kill» → конец боя.
//  Выключателя нет: без этого режим «только фаза N» не имеет смысла. Если нужных состояний нет — пишем в журнал и ничего
//  не трогаем. Не проверено в игре.
// ============================================================================
using System.Collections.Generic;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;

namespace Encore
{
    internal static class PhaseDancers
    {
        const float FindEvery = 0.25f, WindowSec = 240f;

        /// Метка в начале «Start Phase k»: просто сообщает сюда, что фаза k началась.
        sealed class Mark : FsmStateAction
        {
            internal int No;
            public override void OnEnter()
            {
                PhaseDancers.Entered(No);
                Finish();
            }
        }

        static bool armed, patched, reachedEnd;
        static string boss;
        static int phase, kicks, deathAt;   // deathAt: с какой фазы стан танцоров ведёт к смерти (порог IntCompare в «Dancers Stunned»), 0 — не знаем
        static DancersPlan plan;
        static float nextFind, until;
        static PlayMakerFSM control, dancer;
        static readonly HashSet<int> pending = new HashSet<int>();
        static readonly HashSet<int> kicked = new HashSet<int>();

        /// Комната боя загрузилась, босс заперт в фазе phaseNo.
        internal static void Arm(string bossName, int phaseNo)
        {
            Disarm();
            DancersPlan p = PhaseLock.DancersFor(bossName, phaseNo);
            if (p == null) return;
            armed = true;
            plan = p;
            boss = bossName;
            phase = phaseNo;
            nextFind = 0f;
            until = Time.unscaledTime + WindowSec;
        }

        internal static void Disarm()
        {
            if (armed && kicks > 0) Plugin.Log.LogInfo("Босс " + boss + ": фаз пропущено: " + kicks + ".");
            armed = false;
            patched = false;
            reachedEnd = false;
            kicks = 0;
            deathAt = 0;
            plan = null;
            control = null;
            dancer = null;
            pending.Clear();
            kicked.Clear();
        }

        /// Из метки в состоянии «Start Phase k». Только запоминаем: событие уйдёт на следующем кадре из Tick.
        static void Entered(int no)
        {
            if (!armed) return;
            if (no >= phase) reachedEnd = true;
            else if (!kicked.Contains(no)) pending.Add(no);
        }

        /// Раз в кадр. Без настройки — одна проверка на false.
        internal static void Tick()
        {
            if (!armed) return;
            float now = Time.unscaledTime;
            if (!Runner.Active) { Disarm(); return; }
            if (now > until)
            {
                Plugin.Log.LogWarning("Босс " + boss + ": " + (patched ? "бой не дошёл до фазы " + phase + " вовремя" : "FSM «" + plan.Object + "» не нашёлся вовремя")
                    + " — режим «" + PhaseLock.Describe(phase, false) + "» мог не сработать.");
                Disarm();
                return;
            }
            if (!patched)
            {
                if (now < nextFind) return;
                nextFind = now + FindEvery;
                try
                {
                    if (!Find()) return;
                    Setup();
                }
                catch (System.Exception ex)
                {
                    Plugin.Log.LogWarning("Босс " + boss + ": режим «" + PhaseLock.Describe(phase, false) + "» не настроен: " + ex.Message);
                    Disarm();
                }
                return;
            }
            if (pending.Count > 0) Kick();
            if (reachedEnd && pending.Count == 0) Disarm();
        }

        static bool Find()
        {
            PlayMakerFSM[] all = Object.FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (PlayMakerFSM f in all)
            {
                if (f.FsmName != plan.Fsm || f.gameObject.name != plan.Object || f.gameObject.scene.name != plan.Scene) continue;
                if (f.Fsm == null || f.FsmStates == null) continue;
                Transform t = f.transform.Find(plan.Dancer);
                if (t == null) continue;
                PlayMakerFSM d = null;
                foreach (PlayMakerFSM x in t.GetComponents<PlayMakerFSM>()) if (x.FsmName == plan.Fsm) d = x;
                if (d == null) continue;
                control = f;
                dancer = d;
                return true;
            }
            return false;
        }

        static void Setup()
        {
            patched = true;
            int marks = 0, muted = 0;
            for (int k = 1; k <= phase; k++)
            {
                FsmState st = control.Fsm.GetState(plan.StartPrefix + k);
                if (st == null) continue;
                FsmStateAction[] old = st.Actions;
                if (old == null) continue;
                if (k < phase)
                    foreach (FsmStateAction a in old)
                    {
                        SendEventByName se = a as SendEventByName;
                        if (se != null && se.sendEvent != null && se.sendEvent.Value == "START P" + k) { se.Enabled = false; muted++; }
                    }
                var list = new List<FsmStateAction>();
                var mark = new Mark();
                mark.No = k;
                list.Add(mark);
                list.AddRange(old);
                st.Actions = list.ToArray();
                marks++;
            }
            if (marks < phase)
                Plugin.Log.LogWarning("Босс " + boss + ": у главного FSM нашлись не все состояния «" + plan.StartPrefix + "k» (" + marks + " из " + phase + ").");

            deathAt = ReadDeathAt();
            string endText = "";
            if (PhaseLock.DancersEndEarly(boss, phase)) endText = EndHere() ? ", бой кончится вместе с фазой " + phase : ", конец боя после фазы не настроен";
            Plugin.Log.LogInfo("Босс " + boss + ": режим «" + PhaseLock.Describe(phase, false) + "» настроен (меток входа в фазу: " + marks
                + ", музыка пропускаемых фаз отключена: " + muted + endText + ").");
        }

        /// Порог смерти из «Dancers Stunned» (Cogwork 3, Clover 2) — до того, как EndHere его изменит. 0 — не нашли.
        static int ReadDeathAt()
        {
            FsmState st = control.Fsm.GetState(plan.StunState);
            if (st == null || st.Actions == null) return 0;
            foreach (FsmStateAction a in st.Actions)
            {
                IntCompare c = a as IntCompare;
                if (c != null && c.integer2 != null) return c.integer2.Value;
            }
            return 0;
        }

        /// Порог смерти в «Dancers Stunned» ставим равным N и считаем одного танцора уже убитым.
        static bool EndHere()
        {
            FsmInt killed = control.FsmVariables.FindFsmInt(plan.KilledVar);
            FsmState st = control.Fsm.GetState(plan.StunState);
            IntCompare cmp = null;
            if (st != null && st.Actions != null)
                foreach (FsmStateAction a in st.Actions)
                {
                    IntCompare c = a as IntCompare;
                    if (c != null) { cmp = c; break; }
                }
            if (killed == null || cmp == null || cmp.integer2 == null)
            {
                Plugin.Log.LogWarning("Босс " + boss + ": нет счётчика «" + plan.KilledVar + "» или проверки в «" + plan.StunState + "» — конец боя после фазы " + phase + " не настроен.");
                return false;
            }
            killed.Value = 1;
            cmp.integer2.Value = phase;
            return true;
        }

        static void Kick()
        {
            var now = new List<int>(pending);
            pending.Clear();
            now.Sort();
            foreach (int k in now)
            {
                if (kicked.Contains(k)) continue;
                kicked.Add(k);
                if (dancer == null || dancer.Fsm == null)
                {
                    Plugin.Log.LogWarning("Босс " + boss + ": танцор «" + plan.Dancer + "» пропал — фаза " + k + " не пропущена.");
                    continue;
                }
                // Фаза, после которой танцоры не умирают (k меньше порога смерти): шлём главному FSM «танцоры оглушены» — дальше обычный путь
                // («Return Dancers» → «Set Phase k+1» → заводка), но танцор не падает в стан и не умирает на глазах. Иначе (после неё
                // нужна смерть первого танцора, Cogwork перед последней фазой) — как раньше: танцору «ZERO HP».
                bool direct = deathAt > 0 && k < deathAt && !string.IsNullOrEmpty(plan.StunEvent) && control != null && control.Fsm != null;
                if (direct) control.SendEvent(plan.StunEvent); else dancer.SendEvent(plan.KickEvent);
                kicks++;
                Plugin.Log.LogInfo("Босс " + boss + ": фаза " + k + " пропущена (" + (direct ? "главному FSM послано «" + plan.StunEvent + "», без стана танцора"
                    : "танцору «" + plan.Dancer + "» послано «" + plan.KickEvent + "»") + ").");
            }
        }
    }
}
