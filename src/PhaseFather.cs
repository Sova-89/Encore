// ============================================================================
//  PhaseFather.cs — Father of the Flame (правило PhaseRule.Lanterns): «сломать руки заранее» и снять потолок ударов.
//  Устройство боя (дамп FSM комнаты Belltown_08). Четыре руки-жаровни «Brazier Arm BL/TL/TR/BR» (FSM «wisp_brazier_arm», переменная HP),
//  главный FSM «Summon Control» на «Wisp Pyre Effigy» и ядро (FSM «Take Damage» на том же объекте, переменная HP). Руки ломаются
//  по здоровью (HP ≤ 0) или после 11 ударов; каждая сломанная вычитает единицу из «Shell Pieces» главного FSM; когда он дошёл до нуля —
//  «Broken Pause» → «Flare Up» → ядро открыто. Ядро ломается по здоровью или после 29 ударов, тогда «Take Damage» шлёт «FINAL BREAK»
//  (у главного FSM это глобальный переход к «Final Hit» и смерти). «Фаза 2» в игре — только то, что при общем здоровье рук ниже порога
//  («Lanterns Half HP») главный FSM чаще бросает огонь (пауза между волнами 2 с вместо 2,5 с).
//  Здоровье рук, порог и здоровье ядра GoP ставит из таблицы EnemyHp (её подменяет BossFx.ApplyTable, арифметика — PhaseLock.LanternsPlan).
//  Здесь два дела, которых таблицей не сделать:
//   • Руку со здоровьем 0 в таблице («сломана заранее»: фаза 2 — две руки, фаза 3 — все четыре) надо реально сломать: как только рука
//     вошла в состояние «Idle» (зажглась), шлём ей «DAMAGED» — то же событие, что шлёт удар, — и по нулевому здоровью она ломается обычным путём
//     (анимация, «Pop Core», минус единица в «Shell Pieces»).
//   • Если здоровье руки или ядра в таблице больше ванильного (челлендж с полным здоровьем), потолок ударов (11 у руки, 29 у ядра) поднимаем
//     до 9999 — иначе рука сломалась бы раньше времени.
//  Конец боя после фаз 1 и 2 (когда сломана последняя рука) — перенаправление «Broken Pause» → «Final Hit», это делает PhaseEnd.
//  Не проверено в игре.
// ============================================================================
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;

namespace Encore
{
    internal static class PhaseFather
    {
        const float FindEvery = 0.2f, WindowSec = 300f;
        const int Raised = 9999, ArmCap = 11, CoreCap = 29;
        const string EffigyName = "Wisp Pyre Effigy", CoreFsm = "Take Damage", ArmFsm = "wisp_brazier_arm", IdleState = "Idle", HitState = "Hit", KickEvent = "DAMAGED";
        static readonly string[] ArmNames = { "Brazier Arm BL", "Brazier Arm TL", "Brazier Arm TR", "Brazier Arm BR" };

        static bool armed, patched;
        static string boss, scene;
        static int phase, kicks;
        static int[] orig, table;
        static bool[] broken, kicked;
        static PlayMakerFSM[] arms;
        static PlayMakerFSM core;
        static float nextFind, until;

        /// Комната боя загрузилась, босс заперт в фазе phaseNo; origTable и newTable — таблица GoP до и после подмены (6 чисел).
        internal static void Arm(string bossName, string sceneName, int phaseNo, int[] origTable, int[] newTable)
        {
            Disarm();
            if (origTable == null || newTable == null || origTable.Length != 6 || newTable.Length != 6) return;
            bool[] b = PhaseLock.LanternsBroken(newTable);
            bool raise = newTable[5] > origTable[5];
            for (int i = 0; i < 4; i++) if (newTable[i] > origTable[i]) raise = true;
            bool any = false;
            foreach (bool x in b) if (x) any = true;
            if (!any && !raise) return; // руки целы и здоровье не больше ванильного — делать нечего
            armed = true;
            boss = bossName;
            scene = sceneName;
            phase = phaseNo;
            orig = origTable;
            table = newTable;
            broken = b;
            kicked = new bool[4];
            arms = new PlayMakerFSM[4];
            core = null;
            nextFind = 0f;
            until = Time.unscaledTime + WindowSec;
        }

        internal static void Disarm()
        {
            if (armed && kicks > 0) Plugin.Log.LogInfo("Босс " + boss + ": рук сломано заранее: " + kicks + ".");
            armed = false;
            patched = false;
            kicks = 0;
            orig = null;
            table = null;
            broken = null;
            kicked = null;
            arms = null;
            core = null;
        }

        /// Раз в кадр. Без настройки — одна проверка на false.
        internal static void Tick()
        {
            if (!armed) return;
            float now = Time.unscaledTime;
            if (!Runner.Active) { Disarm(); return; }
            if (now > until)
            {
                Plugin.Log.LogWarning("Босс " + boss + ": руки не удалось подготовить вовремя — режим «" + PhaseLock.Describe(phase, false) + "» мог не сработать.");
                Disarm();
                return;
            }
            if (now < nextFind) return;
            nextFind = now + FindEvery;
            try
            {
                if (!patched)
                {
                    if (!Find()) return;
                    Setup();
                }
                if (Kick()) Disarm();
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogWarning("Босс " + boss + ": режим «" + PhaseLock.Describe(phase, false) + "» не настроен: " + ex.Message);
                Disarm();
            }
        }

        static bool Find()
        {
            PlayMakerFSM[] all = Object.FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int found = 0;
            PlayMakerFSM c = null;
            var a = new PlayMakerFSM[4];
            foreach (PlayMakerFSM f in all)
            {
                if (!Rooms.Same(f.gameObject.scene.name, scene) || f.Fsm == null || f.FsmStates == null) continue;
                string go = f.gameObject.name;
                if (f.FsmName == CoreFsm && go == EffigyName) { c = f; continue; }
                if (f.FsmName != ArmFsm) continue;
                for (int i = 0; i < 4; i++)
                    if (go == ArmNames[i] && a[i] == null) { a[i] = f; found++; }
            }
            if (c == null || found < 4) return false;
            core = c;
            arms = a;
            return true;
        }

        static void Setup()
        {
            patched = true;
            int caps = 0;
            for (int i = 0; i < 4; i++)
                if (table[i] > orig[i] && RaiseCap(arms[i], ArmCap)) caps++;
            if (table[5] > orig[5] && RaiseCap(core, CoreCap)) caps++;
            int pre = 0;
            foreach (bool b in broken) if (b) pre++;
            Plugin.Log.LogInfo("Босс " + boss + ": режим «" + PhaseLock.Describe(phase, false) + "» настроен (рук сломать заранее: " + pre
                + ", потолок ударов снят: " + caps + ").");
        }

        /// В состоянии «Hit» ищем сравнение числа ударов с потолком (11 или 29) и поднимаем потолок.
        static bool RaiseCap(PlayMakerFSM f, int cap)
        {
            FsmState st = f.Fsm.GetState(HitState);
            if (st == null || st.Actions == null) return false;
            foreach (FsmStateAction a in st.Actions)
            {
                IntCompare c = a as IntCompare;
                if (c == null || c.integer2 == null || c.integer2.Value != cap) continue;
                c.integer2.Value = Raised;
                return true;
            }
            Plugin.Log.LogWarning("Босс " + boss + ": у «" + f.gameObject.name + "» не нашлось потолка ударов " + cap + " — здоровье может кончиться по ударам.");
            return false;
        }

        /// Шлём «DAMAGED» рукам, которым положено быть сломанными, как только они зажглись. true — все руки обработаны.
        static bool Kick()
        {
            bool done = true;
            for (int i = 0; i < 4; i++)
            {
                if (!broken[i] || kicked[i]) continue;
                PlayMakerFSM f = arms[i];
                if (f == null || f.Fsm == null) { kicked[i] = true; continue; }
                if (f.ActiveStateName != IdleState) { done = false; continue; }
                f.SendEvent(KickEvent);
                kicked[i] = true;
                kicks++;
                Plugin.Log.LogInfo("Босс " + boss + ": рука «" + ArmNames[i] + "» сломана заранее (послано «" + KickEvent + "»).");
            }
            return done;
        }
    }
}
