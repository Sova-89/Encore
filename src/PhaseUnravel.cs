// ============================================================================
//  PhaseUnravel.cs — Распутыватель (The Unravelled): порог третьей фазы.
//  Босс — объект «Conductor Boss», FSM «Control» (сцена Ward_02_boss). Порог фазы 2 («P2 HP») и фазы 3 («P3 HP») лежат в переменных FSM;
//  состояние «Check P2» / «Check P3» сравнивает с ними здоровье босса и шлёт «TO P2» / «TO P3» сцене боя (она проводит волну «шелух»
//  и отвечает «CONTINUE»). Init босса по порядку: GetHP → {HP}; {P2 HP} = {HP} × 0,8; действие GoP (ставит здоровье босса и пороги из
//  таблицы EnemyHp: P2 = всё − фаза 1, P3 = P2 − фаза 2); {P3 HP} = {HP} × 0,4. Последнее действие — ванильное, оно идёт ПОСЛЕ GoP и
//  затирает порог из таблицы (в дампе боя: P2 HP 600, P3 HP 400, хотя таблица даёт 600 и 200). Для боя целиком это просто странная длина
//  второй фазы, а для режимов Encore («только фаза N» с подменённой таблицей) порог третьей фазы оказывается на 0,4 от случайного числа:
//  «фаза 2» превращается в «фаза 2 + 3», «фаза 3» наступает не сразу, у «фазы 1» порог третьей может сработать раньше второй — сцена
//  боя не ждёт «TO P3» в первой фазе, и босс остаётся невидимым без «CONTINUE».
//  Что делаем: последним действием Init добавляем своё — оно ставит «P3 HP» = «P2 HP» − здоровье фазы 2 из таблицы (то, что хотел GoP).
//  Если FSM уже прошёл Init к моменту, когда мы его нашли, считаем то же самое сразу.
//  0.18.3 (со слов тестера: «нужно несколько ударов и есть все переходы»). «Check P2»/«Check P3» в «Control» ни из одного состояния не достижимы
//  (по дампу: входящих переходов нет). Смену фазы запускает ДРУГОЙ FSM того же объекта — «Custom Stun Control» (состояния «P1»/«P2»/«P3»: на
//  «TOOK DAMAGE» сверяет здоровье с «P2 HP»/«P3 HP» и шлёт «Control» оглушение STUN; ванильный Init считает пороги как 0,75 и 0,4 здоровья,
//  которое прочитал при старте — GoP их не трогает). Поэтому у «фазы 3» (здоровье 200) первое оглушение шло после ≈50 урона, второе после ≈120,
//  и каждое сопровождалось волной шелухи в сцене боя. Теперь:
//   1) пороги «Custom Stun Control» берём из «Control» (там уже стоят значения таблицы GoP, у «Control» исправленные выше) — для пропускаемых фаз
//      порог выше здоровья, и PhaseKick (ворота «P1»/«P2», см. SkipData.IdleGate) запускает проверку без удара;
//   2) оглушение у пропускаемых фаз пролетает PhaseSkip (правило «Stun Hit>Stun Tele Out»);
//   3) сцена боя «Boss Scene/Control»: на «TO P2» (при выбранной фазе ≥ 2) переходит не в «Twin Slammers» (волна шелухи), а в «Pos Suck Spears Pause»
//      → «Boss Phase 2» (шлёт боссу CONTINUE), на «TO P3» (при фазе 3) — в «Enemy Phase End Pause» → «Boss Phase 3».
//  Не проверено в игре.
// ============================================================================
using HutongGames.PlayMaker;
using UnityEngine;

namespace Encore
{
    internal static class PhaseUnravel
    {
        const float FindEvery = 0.2f, WindowSec = 300f;
        const string BossKey = "The Unravelled", ObjectName = "Conductor Boss", FsmName = "Control", InitState = "Init", StunName = "Custom Stun Control";
        const string SceneObject = "Boss Scene";

        static bool armed, patched, stunDone, sceneDone;
        static string boss, scene;
        static int phase;
        static float nextFind, until;

        /// Комната боя загрузилась, босс заперт в фазе phaseNo (таблица GoP уже подменена).
        internal static void Arm(string bossName, string sceneName, int phaseNo)
        {
            Disarm();
            if (bossName != BossKey) return;
            armed = true;
            patched = stunDone = sceneDone = false;
            boss = bossName;
            scene = sceneName;
            phase = phaseNo;
            nextFind = 0f;
            until = Time.unscaledTime + WindowSec;
        }

        internal static void Disarm()
        {
            armed = false;
        }

        /// Раз в кадр. Без настройки — одна проверка на false.
        internal static void Tick()
        {
            if (!armed) return;
            float now = Time.unscaledTime;
            if (!Runner.Active) { Disarm(); return; }
            if (now > until)
            {
                Plugin.Log.LogWarning("Босс " + boss + ": порог третьей фазы не поправлен — FSM «" + FsmName + "» объекта «" + ObjectName + "» не нашёлся вовремя.");
                Disarm();
                return;
            }
            if (now < nextFind) return;
            nextFind = now + FindEvery;
            try
            {
                if (!patched && Patch()) patched = true;
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogWarning("Босс " + boss + ": порог третьей фазы не поправлен: " + ex.Message);
                patched = true;
            }
            try
            {
                if (patched && !stunDone && SyncStun()) stunDone = true;
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogWarning("Босс " + boss + ": пороги оглушения не поправлены: " + ex.Message);
                stunDone = true;
            }
            try
            {
                if (!sceneDone && RedirectScene()) sceneDone = true;
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogWarning("Босс " + boss + ": волны шелухи между фазами не пропущены: " + ex.Message);
                sceneDone = true;
            }
            if (patched && stunDone && sceneDone) Disarm();
        }

        /// Пороги «Custom Stun Control» («P2 HP», «P3 HP») = пороги «Control» (таблица GoP). Ждём, пока оба FSM выйдут из Init.
        static bool SyncStun()
        {
            PlayMakerFSM control = null, stun = null;
            PlayMakerFSM[] all = Object.FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (PlayMakerFSM f in all)
            {
                if (f.gameObject.name != ObjectName || !Rooms.Same(f.gameObject.scene.name, scene)) continue;
                if (f.FsmName == FsmName) control = f;
                else if (f.FsmName == StunName) stun = f;
            }
            if (control == null || stun == null || control.Fsm == null || stun.Fsm == null) return false;
            string cs = control.Fsm.ActiveStateName, ss = stun.Fsm.ActiveStateName;
            if (string.IsNullOrEmpty(cs) || cs == InitState || string.IsNullOrEmpty(ss) || ss == InitState) return false;
            FsmInt cp2 = control.FsmVariables.FindFsmInt("P2 HP"), cp3 = control.FsmVariables.FindFsmInt("P3 HP");
            FsmInt sp2 = stun.FsmVariables.FindFsmInt("P2 HP"), sp3 = stun.FsmVariables.FindFsmInt("P3 HP");
            if (cp2 == null || cp3 == null || sp2 == null || sp3 == null)
            {
                Plugin.Log.LogWarning("Босс " + boss + ": в «" + FsmName + "» / «" + StunName + "» нет переменных «P2 HP»/«P3 HP» — пороги оглушения не поправлены.");
                return true;
            }
            // порог 0 и ниже — «этой смены фазы не будет» (бой кончается смертью): ставим −1, чтобы смертельный удар (здоровье 0) не посылал оглушение
            int want2 = cp2.Value > 0 ? cp2.Value : -1, want3 = cp3.Value > 0 ? cp3.Value : -1;
            Plugin.Log.LogInfo("Босс " + boss + ": пороги оглушения «" + StunName + "»: вторая фаза " + want2 + ", третья " + want3
                + " (были " + sp2.Value + " и " + sp3.Value + ").");
            sp2.Value = want2;
            sp3.Value = want3;
            return true;
        }

        /// Сцена боя: при выбранной фазе ≥ 2 «TO P2» ведёт мимо волны (Twin Slammers), при фазе 3 «TO P3» — мимо второй волны.
        static bool RedirectScene()
        {
            if (phase < 2) return true;
            PlayMakerFSM scenePm = null;
            PlayMakerFSM[] all = Object.FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (PlayMakerFSM f in all)
                if (f.FsmName == FsmName && f.gameObject.name == SceneObject && Rooms.Same(f.gameObject.scene.name, scene)) scenePm = f;
            if (scenePm == null || scenePm.Fsm == null || scenePm.FsmStates == null) return false;
            Redirect(scenePm, "Boss Phase 1", "TO P2", "Pos Suck Spears Pause");
            if (phase >= 3) Redirect(scenePm, "Boss Phase 2", "TO P3", "Enemy Phase End Pause");
            return true;
        }

        static void Redirect(PlayMakerFSM f, string from, string ev, string to)
        {
            FsmState a = f.Fsm.GetState(from), b = f.Fsm.GetState(to);
            if (a == null || b == null || a.Transitions == null)
            {
                Plugin.Log.LogWarning("Босс " + boss + ": в сцене боя нет состояний «" + from + "» или «" + to + "» — волна шелухи на «" + ev + "» не пропущена.");
                return;
            }
            foreach (FsmTransition t in a.Transitions)
            {
                if (t == null || t.EventName != ev) continue;
                string was = t.ToState;
                t.ToState = b.Name;
                t.ToFsmState = b;
                Plugin.Log.LogInfo("Босс " + boss + ": сцена боя — «" + from + "» на «" + ev + "» → «" + b.Name + "» (было «" + was + "»), волна шелухи пропущена.");
                return;
            }
            Plugin.Log.LogWarning("Босс " + boss + ": у состояния «" + from + "» нет перехода «" + ev + "» — волна шелухи не пропущена.");
        }

        /// Последнее действие Init: «P3 HP» = «P2 HP» − здоровье второй фазы из таблицы GoP.
        sealed class SetP3 : FsmStateAction
        {
            internal PlayMakerFSM Pm;

            public override void OnEnter()
            {
                try { Apply(Pm); }
                catch (System.Exception ex) { Plugin.Log.LogWarning("Босс " + BossKey + ": порог третьей фазы: " + ex.Message); }
                Finish();
            }
        }

        static void Apply(PlayMakerFSM f)
        {
            FsmInt p2 = f.FsmVariables.FindFsmInt("P2 HP"), p3 = f.FsmVariables.FindFsmInt("P3 HP");
            EnemyHp info;
            if (p2 == null || p3 == null || !EnemyHp.enemies.TryGetValue(BossKey, out info) || info.phases == null || info.phases.Length < 2) return;
            int was = p3.Value, now = PhaseLock.UnravelP3(p2.Value, info.phases[1].hp);
            p3.Value = now;
            Plugin.Log.LogInfo("Босс " + BossKey + ": порог третьей фазы " + now + " (по умолчанию было бы " + was + "), порог второй " + p2.Value + ".");
        }

        /// true — готово; false — FSM ещё не появился.
        static bool Patch()
        {
            PlayMakerFSM[] all = Object.FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (PlayMakerFSM f in all)
            {
                if (f.FsmName != FsmName || f.gameObject.name != ObjectName || !Rooms.Same(f.gameObject.scene.name, scene)) continue;
                if (f.Fsm == null || f.FsmStates == null) return false;
                FsmState init = f.Fsm.GetState(InitState);
                if (init == null || init.Actions == null || f.Fsm.GetState("Check P3") == null)
                {
                    Plugin.Log.LogWarning("Босс " + boss + ": у FSM «" + FsmName + "» объекта «" + ObjectName + "» не те состояния — порог третьей фазы не поправлен.");
                    return true;
                }
                bool hasOurs = false;
                foreach (FsmStateAction a in init.Actions) if (a is SetP3) hasOurs = true;
                if (!hasOurs)
                {
                    var act = new SetP3();
                    act.Pm = f;
                    var list = new System.Collections.Generic.List<FsmStateAction>(init.Actions);
                    list.Add(act);
                    init.Actions = list.ToArray();
                }
                if (f.Fsm.ActiveStateName != InitState && !string.IsNullOrEmpty(f.Fsm.ActiveStateName)) Apply(f);   // Init уже прошёл — считаем сразу
                Plugin.Log.LogInfo("Босс " + boss + ": режим «" + PhaseLock.Describe(phase, false) + "» — порог третьей фазы поправлен.");
                return true;
            }
            return false;
        }
    }
}
