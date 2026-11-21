// ============================================================================
//  PhaseKick.cs — «смена фазы без первого удара». У части боссов (Seth, Grand Mother Silk, Fourth Chorus) проверку
//  здоровья делает отдельный FSM «Phase Control»: он стоит в состоянии «P1» и ждёт ровно одного события —
//  «TOOK DAMAGE» (босс получил урон), только после этого сверяет здоровье с порогом. Когда босс заперт в фазе N,
//  пороги ниже здоровья уже с первой секунды, но FSM про это не знает, пока игрок не ударит. Отсюда «надо ударить
//  босса, чтобы он перешёл в следующую фазу».
//  Что делаем: находим такие FSM (состояние с единственным переходом «TOOK DAMAGE → …Check…») у объектов с
//  HealthManager и, когда босс уже готов к бою, один раз на состояние шлём им то же событие, что шлёт удар.
//  Остальным FSM босса (Stun Control, Control) событие не шлём: удар не засчитывается как стан или урон.
//  Выключатель — Mode/KickPhaseChange.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;

namespace Encore
{
    internal static class PhaseKick
    {
        const float FindEvery = 0.5f, WindowSec = 90f, NoControlDelay = 3f;
        const int MaxKicks = 8;

        sealed class Gate
        {
            internal PlayMakerFSM Fsm, Control;
            internal Dictionary<string, bool> IsGate = new Dictionary<string, bool>();
            internal string Last = "";
            internal bool Pending;
            internal int Kicks;
            internal float Seen;
            internal float ReadyAt = -1f;   // с какого момента босс «готов» (для SkipData.KickDelay)
        }

        static bool armed;
        static int phaseNo;
        static string scene, boss;
        static readonly List<KeyValuePair<HutongGames.PlayMaker.FsmString, string>> quieted = new List<KeyValuePair<HutongGames.PlayMaker.FsmString, string>>();
        static float until, nextFind, armedAt;
        static readonly List<Gate> gates = new List<Gate>();
        static readonly HashSet<PlayMakerFSM> known = new HashSet<PlayMakerFSM>();

        internal static void Arm(string bossName, string sceneName, int phase)
        {
            Disarm();
            if (phase < 2 || !Plugin.KickPhaseChange) return;
            armed = true;
            phaseNo = phase;
            boss = bossName;
            scene = sceneName;
            armedAt = Time.unscaledTime;
            until = armedAt + WindowSec;
            nextFind = 0f;
        }

        internal static void Disarm()
        {
            if (armed)
            {
                int total = 0;
                foreach (Gate g in gates) total += g.Kicks;
                if (total > 0) Plugin.Log.LogInfo("Босс " + boss + ": проверка фазы запущена без удара: " + total + " раз.");
            }
            armed = false;
            gates.Clear();
            known.Clear();
            for (int i = 0; i < quieted.Count; i++)
            {
                try { quieted[i].Key.Value = quieted[i].Value; } catch (System.Exception) { }
            }
            quieted.Clear();
        }

        internal static void Tick()
        {
            if (!armed) return;
            float now = Time.unscaledTime;
            if (now > until || !Runner.Active) { Disarm(); return; }
            if (now >= nextFind && now < armedAt + 45f)
            {
                nextFind = now + FindEvery;
                Find(now);
            }
            for (int i = 0; i < gates.Count; i++) Step(gates[i], now);
        }

        /// Ищет FSM-«ворота» у боссов этой комнаты.
        static void Find(float now)
        {
            HealthManager[] all = Object.FindObjectsByType<HealthManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (HealthManager hm in all)
            {
                if (hm == null || !Rooms.Same(hm.gameObject.scene.name, scene)) continue;
                PlayMakerFSM[] fsms = hm.GetComponents<PlayMakerFSM>();
                PlayMakerFSM control = null;
                foreach (PlayMakerFSM f in fsms) if (f.FsmName == "Control") control = f;
                foreach (PlayMakerFSM f in fsms)
                {
                    if (known.Contains(f) || !(SkipData.KickableFsm(f.FsmName) || SkipData.KickFakeDeath(boss, f.FsmName, phaseNo))) continue;
                    known.Add(f);
                    if (!HasGate(f)) continue;
                    QuietStuns(f);
                    var g = new Gate();
                    g.Fsm = f;
                    g.Control = control != f ? control : null;
                    g.Seen = now;
                    gates.Add(g);
                }
            }
        }

        /// Четвёртый Хор: у «Stun 1…» кроме последнего подменяем событие STUN на пустое имя — голем получит одно оглушение, а не три
        /// (подробнее — SkipData.QuietStunStates). Отправка не отключается, а перенацеливается: состояние завершается как обычно.
        static void QuietStuns(PlayMakerFSM f)
        {
            List<string> names = SkipData.QuietStunStates(boss, phaseNo);
            if (names.Count == 0 || f.Fsm == null) return;
            int n = 0;
            foreach (string name in names)
            {
                try
                {
                    HutongGames.PlayMaker.FsmState st = f.Fsm.GetState(name);
                    if (st == null || st.Actions == null) continue;
                    foreach (HutongGames.PlayMaker.FsmStateAction act in st.Actions)
                    {
                        if (act == null || act.GetType().Name != "SendEventByName") continue;
                        System.Reflection.FieldInfo field = act.GetType().GetField("sendEvent");
                        HutongGames.PlayMaker.FsmString ev = field == null ? null : field.GetValue(act) as HutongGames.PlayMaker.FsmString;
                        if (ev == null || ev.Value != "STUN") continue;
                        quieted.Add(new KeyValuePair<HutongGames.PlayMaker.FsmString, string>(ev, ev.Value));
                        ev.Value = "ENCORE NO STUN";
                        n++;
                    }
                }
                catch (System.Exception e)
                {
                    Plugin.Log.LogWarning("Босс " + boss + ": оглушение «" + name + "» не приглушилось: " + e.Message);
                }
            }
            if (n > 0) Plugin.Log.LogInfo("Босс " + boss + ": лишних оглушений убрано: " + n + " (останется одно, от последней смены фазы).");
        }

        static bool HasGate(PlayMakerFSM f)
        {
            if (f.Fsm == null || f.FsmStates == null) return false;
            foreach (HutongGames.PlayMaker.FsmState st in f.FsmStates)
                if (IsGateState(f.FsmName, st)) return true;
            return false;
        }

        static bool IsGateState(string fsmName, HutongGames.PlayMaker.FsmState st)
        {
            if (st == null || st.Transitions == null) return false;
            if (st.Transitions.Length == 1 && SkipData.IsGate(st.Transitions[0].EventName, st.Transitions[0].ToState)) return true;
            foreach (HutongGames.PlayMaker.FsmTransition t in st.Transitions)
                if (t != null && SkipData.IdleGate(boss, fsmName, st.Name, t.EventName, t.ToState)) return true;
            return false;
        }

        static bool Ready(Gate g, float now)
        {
            bool ready = g.Control == null || g.Control.Fsm == null ? now - armedAt >= NoControlDelay : SkipData.BossReady(g.Control.ActiveStateName);
            if (!ready) { g.ReadyAt = -1f; return false; }
            if (g.ReadyAt < 0f) g.ReadyAt = now;
            return now - g.ReadyAt >= SkipData.KickDelay(boss);
        }

        static void Step(Gate g, float now)
        {
            if (g.Fsm == null) return;
            string name = g.Fsm.ActiveStateName;
            if (name != g.Last)
            {
                g.Last = name;
                bool gate;
                if (!g.IsGate.TryGetValue(name, out gate))
                {
                    gate = IsGateState(g.Fsm.FsmName, g.Fsm.Fsm.GetState(name));
                    g.IsGate[name] = gate;
                }
                g.Pending = gate;
            }
            if (g.Pending && g.Kicks < MaxKicks && Ready(g, now))
            {
                g.Pending = false;
                g.Kicks++;
                g.Fsm.SendEvent("TOOK DAMAGE");
            }
        }
    }
}
