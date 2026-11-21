// ============================================================================
//  BossFx.cs — настройки боссов в бою: «босс весь бой в одной фазе» (список — Phase.cs, PhaseLock.Defs).
//  Два пути.
//  1) Таблица (все боссы, кроме Троббио). GoP считает здоровье и пороги фаз босса из своей таблицы
//     EnemyHp (здоровье каждой фазы) в момент, когда у босса запускается FSM. Мы подменяем таблицу
//     сразу после загрузки комнаты, до этого момента (ApplyTable), а когда бой кончился — возвращаем
//     (Restore). GoP сам расставляет и здоровье, и пороги. Босс остаётся настоящим: те же атаки, тот же FSM.
//     Фазы до выбранной босс проходит сразу, в начале боя, как при обычном переходе (с рёвом и т. п.).
//  2) Троббио (отдельно, первый босс, проверен в игре). GoP при входе в комнату сам ставит боссу здоровье
//     (HealthManager.hp) и порог второй фазы (переменная FSM «P2 HP»). Когда GoP закончил, мы подменяем
//     эти два числа (арифметика — Phase.cs). Пока GoP не закончил, ждём: смотрим раз в 0,15 с, не дольше минуты.
//     «Только фаза 2» пропускает рёв и уход со сцены: сразу выставляются флаги, которые в ванили
//     ставят «Phase Roar» и «Phase Roar End» (Phase 2, Doing First Burst Column, Will Burst Column;
//     Doing First Attack снимается). Если таких переменных в FSM нет — остаётся обычный переход с рёвом.
//  3) Ванильные двухфазные (Broodmother): в GoP таблицы здоровья фаз у них нет, здоровье и порог
//     второй фазы (FSM «Control», переменная «P2 HP», ванильно 0,55 от здоровья) ставит сама игра.
//     Ждём, пока FSM выйдет из Init, берём здоровье и порог как есть, делим на две фазы и ставим то же,
//     что у Троббио (TryApplyVanilla). Рёв второй фазы не пропускаем.
//  Своё здоровье (0.19.0, Entry.Hp): та же подмена таблицы (или Троббио/Broodmother — их путь), только вместо ванильного числа — своё
//  (PhaseLock.TablePlan / Plan с параметром custom). Без выбранной фазы таблица масштабируется (доли фаз те же). Работает и у боссов без фаз
//  (PhaseLock.SingleDefs: одна строка таблицы). Такой бой — всегда тренировка.
//  Тренировка (здоровье одной фазы): Runner.MarkPractice убирает бой из статистики.
//  Челлендж (полное здоровье, босс весь бой в одной фазе): считается как обычный бой, но у босса
//  отдельная строка статистики (Stats.BossKey с меткой, PhaseLock.Variant).
//  Что менять: новый босс — строка в PhaseLock.Defs (Phase.cs); у Троббио — TryApply.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Reflection;
using HutongGames.PlayMaker;
using UnityEngine;

namespace Encore
{
    internal static class BossFx
    {
        const float PollEvery = 0.15f, GiveUpAfter = 60f, GiveUpAfterVanilla = 300f;

        static string boss;      // кого ждём; null — ничего не делаем
        static BossDef pollDef;  // его запись в списке (правило Trobbio или Vanilla)
        static string scene;     // в какой комнате
        static int mode;
        static bool full;        // челлендж: полное здоровье
        static int custom;       // своё здоровье (0 — не задано)
        static float nextTry, giveUpAt;
        static FieldInfo initHpField;

        /// Что ставят «Phase Roar» / «Phase Roar End» у Троббио: флаги второй фазы (включить) и первой атаки (снять).
        static readonly string[] SecondPhaseOn = { "Phase 2", "Doing First Burst Column", "Will Burst Column" };
        static readonly string[] SecondPhaseOff = { "Doing First Attack" };

        /// Комната босса загрузилась: если у него есть настройка (её снимок взят при старте попытки, см. Runner),
        /// подменяем таблицу фаз GoP (все боссы) или начинаем ждать, пока GoP поставит здоровье (Троббио).
        internal static void Arm(string bossName, string sceneName, int phase, bool challenge, int ownHp)
        {
            boss = null;
            BossDef def = PhaseLock.Find(bossName);
            bool phased = def != null && phase >= 1 && phase <= def.Phases;
            BossDef hpDef = def != null ? def : PhaseLock.FindHp(bossName);
            int hp = hpDef != null ? PhaseLock.NormalizeHp(bossName, ownHp) : 0;
            if (!phased)
            {
                // фаза не выбрана: смены фаз проходят как в игре; остаётся только своё здоровье (если задано)
                PhaseSkip.Disarm();
                PhaseKick.Disarm();
                PhaseEnd.Disarm();
                PhaseDancers.Disarm();
                PhaseFather.Disarm();
                PhaseUnravel.Disarm();
                if (hp <= 0)
                {
                    LeaveScene(sceneName);
                    return;
                }
                // Распутыватель со своим здоровьем и без фазы: его пороги фаз (P3 HP и оглушения) тоже надо привести к таблице, иначе третья фаза наступит не там
                PhaseUnravel.Arm(bossName, sceneName, PhaseLock.All);
            }
            else
            {
                PhaseFather.Disarm();                        // Отец пламени: ломаем руки заранее, ниже ApplyTable вооружит заново
                PhaseKick.Arm(bossName, sceneName, phase);
                PhaseEnd.Arm(bossName, sceneName, phase);    // босс с отдельным запасом здоровья на фазу: бой кончается вместе с выбранной фазой
                PhaseDancers.Arm(bossName, phase);           // танцоры (Cogwork, Clover): пропуск фаз до выбранной и конец боя после неё
                PhaseUnravel.Arm(bossName, sceneName, phase); // Распутыватель: порог третьей фазы (ванильный Init затирает его после GoP)
                PhaseSkip.Arm(bossName, sceneName, phase);   // рёвы смен фаз до выбранной (Trobbio ставит свои флаги, у него правила нет)
            }
            if (hp > 0) challenge = false;                   // своё здоровье — всегда тренировка
            if (hpDef.Rule == PhaseRule.Trobbio || hpDef.Rule == PhaseRule.Vanilla)
            {
                LeaveScene(sceneName);
                if (!phased && hp <= 0) return;
                boss = bossName;
                pollDef = hpDef;
                scene = sceneName;
                mode = phased ? phase : PhaseLock.All;
                full = challenge;
                custom = hp;
                nextTry = 0f;
                giveUpAt = Time.unscaledTime + (hpDef.Rule == PhaseRule.Vanilla ? GiveUpAfterVanilla : GiveUpAfter);
                return;
            }
            ApplyTable(hpDef, sceneName, phased ? phase : PhaseLock.All, challenge, hp);
        }

        // ---- путь 1: таблица фаз GoP ------------------------------------------------

        /// Что мы подменили в таблице GoP: старые и новые значения для каждой сложности.
        sealed class Saved
        {
            internal EnemyHp info;
            internal string[] diffs;
            internal int[][] oldPhases;   // [сложность][фаза]
            internal int[] oldFull;
            internal int[][] newPhases;
            internal int[] newFull;
        }

        static readonly List<Saved> saved = new List<Saved>();
        static string tableScene;

        /// Вернуть таблицу GoP, если босс уже другой комнаты (или сегмент кончился).
        internal static void LeaveScene(string newScene)
        {
            if (saved.Count > 0 && !Rooms.Same(newScene, tableScene)) Restore();
        }

        /// Вернуть таблицу фаз GoP как была.
        internal static void Restore()
        {
            if (saved.Count == 0) return;
            try
            {
                foreach (Saved s in saved)
                    for (int d = 0; d < s.diffs.Length; d++)
                    {
                        for (int i = 0; i < s.oldPhases[d].Length; i++) s.info.phases[i].hpDict[s.diffs[d]] = s.oldPhases[d][i];
                        s.info.hpFullDict[s.diffs[d]] = s.oldFull[d];
                    }
                Plugin.Log.LogInfo("Таблица здоровья фаз GoP возвращена.");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning("Таблица здоровья фаз GoP не возвращена: " + ex.Message);
            }
            saved.Clear();
            tableScene = null;
        }

        static readonly Dictionary<string, int[]> vanillaHp = new Dictionary<string, int[]>();

        /// Всё здоровье босса в таблице GoP на этой сложности — подсказка «в игре: N» в меню. 0 — не знаем (двойные бои, Broodmother, таблица сейчас подменена).
        internal static int VanillaHp(string bossName, string difficulty)
        {
            int di = Array.IndexOf(Tokens.Difficulties, difficulty);
            if (di < 0 || bossName == null) return 0;
            int[] byDifficulty;
            if (!vanillaHp.TryGetValue(bossName, out byDifficulty))
            {
                if (saved.Count > 0) return 0;   // таблица подменена нами: настоящих чисел сейчас не видно
                byDifficulty = new int[Tokens.Difficulties.Length];
                try
                {
                    BossDef def = PhaseLock.FindHp(bossName);
                    EnemyHp info;
                    if (def != null && def.Keys.Length == 1 && def.Rule != PhaseRule.Vanilla
                        && EnemyHp.enemies.TryGetValue(def.Keys[0], out info) && info.phases != null && info.phases.Length == TableLen(def))
                    {
                        for (int d = 0; d < byDifficulty.Length; d++)
                        {
                            var orig = new int[info.phases.Length];
                            for (int i = 0; i < orig.Length; i++) orig[i] = info.phases[i].hpDict[Tokens.Difficulties[d]];
                            byDifficulty[d] = PhaseLock.VanillaTotal(def.Rule, orig);
                        }
                    }
                }
                catch (Exception) { byDifficulty = new int[Tokens.Difficulties.Length]; }
                vanillaHp[bossName] = byDifficulty;
            }
            return byDifficulty[di];
        }

        /// Сколько строк у таблицы GoP этого босса (чаще всего столько же, сколько фаз).
        static int TableLen(BossDef def)
        {
            return def.TableLen > 0 ? def.TableLen : def.Phases;
        }

        static void ApplyTable(BossDef def, string sceneName, int phase, bool challenge, int ownHp)
        {
            Restore();
            try
            {
                var plans = new List<Saved>();
                foreach (string key in def.Keys)
                {
                    EnemyHp info;
                    if (!EnemyHp.enemies.TryGetValue(key, out info) || info.phases == null || info.phases.Length != TableLen(def))
                    {
                        Plugin.Log.LogWarning("Босс " + def.Boss + ": GoP не знает здоровья его фаз (" + key + ") — настройку не применяю.");
                        return;
                    }
                    var s = new Saved();
                    s.info = info;
                    s.diffs = new List<string>(info.hpFullDict.Keys).ToArray();
                    s.oldPhases = new int[s.diffs.Length][];
                    s.newPhases = new int[s.diffs.Length][];
                    s.oldFull = new int[s.diffs.Length];
                    s.newFull = new int[s.diffs.Length];
                    for (int d = 0; d < s.diffs.Length; d++)
                    {
                        var orig = new int[TableLen(def)];
                        for (int i = 0; i < orig.Length; i++) orig[i] = info.phases[i].hpDict[s.diffs[d]];
                        int total;
                        int[] plan;
                        if (!PhaseLock.TablePlan(def.Rule, phase, challenge, ownHp, orig, out plan, out total))
                        {
                            Plugin.Log.LogWarning("Босс " + def.Boss + ": здоровье фаз в GoP не подходит для настройки"
                                + (ownHp > 0 ? " (своё здоровье " + ownHp + ", слишком мало для стольких фаз?)" : "") + " — не применяю.");
                            return;
                        }
                        s.oldPhases[d] = orig;
                        s.newPhases[d] = plan;
                        s.oldFull[d] = info.hpFullDict[s.diffs[d]];
                        s.newFull[d] = total;
                    }
                    plans.Add(s);
                }

                foreach (Saved s in plans)
                {
                    for (int d = 0; d < s.diffs.Length; d++)
                    {
                        for (int i = 0; i < s.newPhases[d].Length; i++) s.info.phases[i].hpDict[s.diffs[d]] = s.newPhases[d][i];
                        s.info.hpFullDict[s.diffs[d]] = s.newFull[d];
                    }
                    saved.Add(s);
                }
                tableScene = sceneName;

                int hp = 0;
                string current = Gods_Of_Pharloom.BossSequence.currentDifficultMode;
                Saved first = plans[0];
                for (int d = 0; d < first.diffs.Length; d++) if (first.diffs[d] == current) hp = first.newFull[d];

                if (def.Rule == PhaseRule.Lanterns)
                {
                    int di = Array.IndexOf(first.diffs, current);
                    if (di >= 0) PhaseFather.Arm(def.Boss, sceneName, phase, first.oldPhases[di], first.newPhases[di]);   // без фазы — только снять потолок ударов
                }

                if (!challenge) Runner.MarkPractice(); // тренировка не идёт в статистику; челлендж считается (отдельной строкой)
                string what = PhaseLock.DescribeHp(phase, challenge, ownHp);
                Plugin.Log.LogInfo("Босс " + def.Boss + ": " + what + " (здоровье " + hp + ", таблица фаз GoP подменена) — "
                    + (challenge ? "челлендж, считается в статистике" : "тренировка, статистика не меняется"));
                Plugin.Toast(Runner.Display(def.Boss) + ": " + what
                    + (challenge ? L.T(" · челлендж", " · challenge") : L.T(" · тренировка", " · practice")), 4f);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning("Босс " + def.Boss + ": настройка не применена: " + ex.Message);
                Restore();
            }
        }

        /// Раз в кадр из Runner.Tick. Без настройки — одна проверка на null.
        internal static void Tick()
        {
            if (saved.Count > 0 && !Runner.Active) Restore(); // сегмент кончился — таблица GoP снова обычная
            PhaseSkip.Tick();
            PhaseFather.Tick();
            Summons.Tick();
            Attacks.Tick();
            PhaseKick.Tick();
            PhaseEnd.Tick();
            PhaseDancers.Tick();
            PhaseUnravel.Tick();
            if (boss == null) return;
            float now = Time.unscaledTime;
            if (now < nextTry) return;
            nextTry = now + PollEvery;

            if (!Runner.Active) { boss = null; return; }
            if (now > giveUpAt)
            {
                Plugin.Log.LogWarning("Босс " + boss + ": настройка (" + PhaseLock.DescribeHp(mode, full, custom) + ") не применена — босс не появился или не поставил здоровье вовремя.");
                boss = null;
                return;
            }
            try
            {
                bool done = pollDef != null && pollDef.Rule == PhaseRule.Vanilla ? TryApplyVanilla() : TryApply();
                if (done) boss = null;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning("Босс " + boss + ": настройка не применена: " + ex.Message);
                boss = null;
            }
        }

        /// true — готово (или применять нечего); false — ещё ждём.
        static bool TryApply()
        {
            HealthManager hm = FindBoss(boss, scene);
            if (hm == null) return false;

            PlayMakerFSM control = null;
            foreach (PlayMakerFSM f in hm.GetComponents<PlayMakerFSM>())
                if (f.FsmName == "Control") { control = f; break; }
            if (control == null) return false;
            FsmInt second = control.FsmVariables.FindFsmInt("P2 HP");
            if (second == null)
            {
                Plugin.Log.LogWarning("Босс " + boss + ": в FSM нет переменной «P2 HP» — настройку не применяю.");
                return true;
            }

            EnemyHp info;
            if (!EnemyHp.enemies.TryGetValue(boss, out info) || info.phases == null || info.phases.Length < 2)
            {
                Plugin.Log.LogWarning("Босс " + boss + ": GoP не знает здоровья его фаз — настройку не применяю.");
                return true;
            }
            var phaseHp = new int[info.phases.Length];
            for (int i = 0; i < phaseHp.Length; i++) phaseHp[i] = info.phases[i].hp;

            // GoP ещё не поставил боссу здоровье (там ванильное) — подождём следующей проверки.
            int total = info.hpFullDict[Gods_Of_Pharloom.BossSequence.currentDifficultMode];
            if (hm.hp != total || second.Value != total - phaseHp[0]) return false;

            int hp, secondPhaseAt;
            if (!PhaseLock.Plan(mode, full, custom, phaseHp, out hp, out secondPhaseAt)) return true;

            hm.hp = hp;
            if (initHpField == null)
                initHpField = typeof(HealthManager).GetField("initHp", BindingFlags.Instance | BindingFlags.NonPublic);
            if (initHpField != null) initHpField.SetValue(hm, hp);
            second.Value = secondPhaseAt;
            bool skipped = mode == PhaseLock.Second && SkipRoar(control);

            if (!full) Runner.MarkPractice(); // тренировка не идёт в статистику; челлендж считается (отдельной строкой)
            string what = PhaseLock.DescribeHp(mode, full, custom);
            Plugin.Log.LogInfo("Босс " + boss + ": " + what + " (здоровье " + hp + ", порог второй фазы " + secondPhaseAt
                + (mode == PhaseLock.Second ? (skipped ? ", рёв пропущен" : ", рёв остался") : "") + ") — " + (full ? "челлендж, считается в статистике" : "тренировка, статистика не меняется"));
            Plugin.Toast(Runner.Display(boss) + ": " + what
                + (full ? L.T(" · челлендж", " · challenge") : L.T(" · тренировка", " · practice")), 4f);
            return true;
        }

        /// Ванильный двухфазный босс (Broodmother): true — готово (или применять нечего); false — ещё ждём.
        static bool TryApplyVanilla()
        {
            HealthManager hm = FindBoss(pollDef.Object, scene);
            if (hm == null) return false;

            PlayMakerFSM control = null;
            foreach (PlayMakerFSM f in hm.GetComponents<PlayMakerFSM>())
                if (f.FsmName == "Control") { control = f; break; }
            if (control == null) return false;
            FsmInt second = control.FsmVariables.FindFsmInt("P2 HP");
            if (second == null)
            {
                Plugin.Log.LogWarning("Босс " + boss + ": в FSM нет переменной «P2 HP» — настройку не применяю.");
                return true;
            }

            // Пока FSM не стартовал или стоит в Init, порог ещё не посчитан.
            string state = control.Fsm.ActiveStateName;
            if (string.IsNullOrEmpty(state) || state == "Init") return false;

            int total = hm.hp, secondAt = second.Value;
            if (secondAt <= 0 || total <= secondAt)
            {
                Plugin.Log.LogWarning("Босс " + boss + ": здоровье " + total + " и порог второй фазы " + secondAt + " не подходят — настройку не применяю.");
                return true;
            }

            // Здоровье фазы 1 — выше порога, фазы 2 — от порога до нуля.
            var phaseHp = new[] { total - secondAt, secondAt };
            int hp, secondPhaseAt;
            if (!PhaseLock.Plan(mode, full, custom, phaseHp, out hp, out secondPhaseAt)) return true;

            hm.hp = hp;
            if (initHpField == null)
                initHpField = typeof(HealthManager).GetField("initHp", BindingFlags.Instance | BindingFlags.NonPublic);
            if (initHpField != null) initHpField.SetValue(hm, hp);
            second.Value = secondPhaseAt;

            if (!full) Runner.MarkPractice();
            string what = PhaseLock.DescribeHp(mode, full, custom);
            Plugin.Log.LogInfo("Босс " + boss + ": " + what + " (здоровье " + hp + " из " + total + ", порог второй фазы " + secondPhaseAt
                + ") — " + (full ? "челлендж, считается в статистике" : "тренировка, статистика не меняется"));
            Plugin.Toast(Runner.Display(boss) + ": " + what
                + (full ? L.T(" · челлендж", " · challenge") : L.T(" · тренировка", " · practice")), 4f);
            return true;
        }

        /// Сразу ставит флаги второй фазы, чтобы рёв и уход со сцены не проигрывались. false — переменных не нашли, ничего не тронуто.
        static bool SkipRoar(PlayMakerFSM control)
        {
            var on = new FsmBool[SecondPhaseOn.Length];
            var off = new FsmBool[SecondPhaseOff.Length];
            for (int i = 0; i < on.Length; i++)
            {
                on[i] = control.FsmVariables.FindFsmBool(SecondPhaseOn[i]);
                if (on[i] == null) return false;
            }
            for (int i = 0; i < off.Length; i++)
            {
                off[i] = control.FsmVariables.FindFsmBool(SecondPhaseOff[i]);
                if (off[i] == null) return false;
            }
            foreach (FsmBool b in on) b.Value = true;
            foreach (FsmBool b in off) b.Value = false;
            return true;
        }

        /// Босс с таким именем и здоровьем в нужной комнате (включая выключенные объекты).
        static HealthManager FindBoss(string name, string sceneName)
        {
            foreach (HealthManager hm in UnityEngine.Object.FindObjectsByType<HealthManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (hm.gameObject.name != name) continue;
                if (!Rooms.Same(hm.gameObject.scene.name, sceneName)) continue;
                return hm;
            }
            return null;
        }
    }
}
