// ============================================================================
//  PhaseEnd.cs — «бой кончается вместе с выбранной фазой» для боссов с отдельным запасом здоровья на фазу
//  (правило PhaseRule.Pool — Grand Mother Silk; тот же приём у Bell Eater и Father of the Flame; список — Phase.cs, PhaseLock.Defs).
//  У обычных боссов порог фазы стоит на общем здоровье, поэтому «после фазы N ничего нет» достаточно задать таблицей.
//  У Матери Шёлка FSM «Phase Control» сам ставит здоровье каждой фазы (первой — SetHP, остальным — AddHP), а в конце
//  фазы проверяет «здоровье ≤ 0?» (состояние «HP Check k») и по событию NEXT уходит в следующую фазу
//  (после третьей — длинный переход с обвалом арены). Нам нужно, чтобы после выбранной фазы N босс умирал:
//  у «HP Check N» переход NEXT перенаправляем в «Death Hit» — то самое состояние, в которое после шестой фазы
//  попадает сам бой. Дальше всё как при обычной смерти босса.
//  Здоровье фаз до выбранной и самой выбранной задаёт таблица GoP (BossFx.ApplyTable), «ворота» между фазами
//  открывает PhaseKick. Выключателя нет: без этого перехода режим «только фаза N» не имеет смысла.
// ============================================================================
using HutongGames.PlayMaker;
using UnityEngine;

namespace Encore
{
    internal static class PhaseEnd
    {
        const float FindEvery = 0.25f, WindowSec = 120f;
        const string DefaultEvent = "NEXT";

        static bool armed;
        static string boss, scene;
        static int phase;
        static PoolEnd plan;
        static float nextFind, until;

        /// Комната босса загрузилась, босс заперт в фазе phase.
        internal static void Arm(string bossName, string sceneName, int phaseNo)
        {
            Disarm();
            PoolEnd p = PhaseLock.EndPlan(bossName, phaseNo);
            if (p == null) return;
            armed = true;
            plan = p;
            boss = bossName;
            scene = sceneName;
            phase = phaseNo;
            nextFind = 0f;
            until = Time.unscaledTime + WindowSec;
        }

        internal static void Disarm()
        {
            armed = false;
            plan = null;
        }

        /// Раз в кадр. Без настройки — одна проверка на false.
        internal static void Tick()
        {
            if (!armed) return;
            float now = Time.unscaledTime;
            if (!Runner.Active) { Disarm(); return; }
            if (now > until)
            {
                Plugin.Log.LogWarning("Босс " + boss + ": конец боя после фазы " + phase + " не настроен — FSM «" + plan.Fsm + "» не нашёлся вовремя.");
                Disarm();
                return;
            }
            if (now < nextFind) return;
            nextFind = now + FindEvery;
            try
            {
                if (Redirect()) Disarm();
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogWarning("Босс " + boss + ": конец боя после фазы " + phase + " не настроен: " + ex.Message);
                Disarm();
            }
        }

        /// true — готово (или настраивать нечего); false — FSM ещё не появился.
        static bool Redirect()
        {
            PlayMakerFSM[] all = Object.FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (PlayMakerFSM f in all)
            {
                if (f.FsmName != plan.Fsm || f.gameObject.name != plan.Object || !Rooms.Same(f.gameObject.scene.name, scene)) continue;
                if (f.Fsm == null || f.FsmStates == null) return false;

                string checkName = plan.Checks[phase - 1];
                string ev = string.IsNullOrEmpty(plan.Event) ? DefaultEvent : plan.Event;
                FsmState check = f.Fsm.GetState(checkName);
                FsmState death = f.Fsm.GetState(plan.Death);
                if (check == null || death == null)
                {
                    Plugin.Log.LogWarning("Босс " + boss + ": в FSM «" + plan.Fsm + "» нет состояний «" + checkName + "» или «" + plan.Death + "» — конец боя после фазы " + phase + " не настроен.");
                    return true;
                }
                FsmTransition[] transitions = check.Transitions;
                if (transitions != null)
                    for (int i = 0; i < transitions.Length; i++)
                    {
                        if (transitions[i].EventName != ev) continue;
                        string was = transitions[i].ToState;
                        transitions[i].ToState = death.Name;
                        transitions[i].ToFsmState = death;
                        Plugin.Log.LogInfo("Босс " + boss + ": бой кончится вместе с фазой " + phase + " (переход «" + checkName + "» → «" + death.Name + "», было «" + was + "»).");
                        return true;
                    }
                Plugin.Log.LogWarning("Босс " + boss + ": у состояния «" + checkName + "» нет перехода " + ev + " — конец боя после фазы " + phase + " не настроен.");
                return true;
            }
            return false;
        }
    }
}
