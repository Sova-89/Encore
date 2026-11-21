// ============================================================================
//  Runner.cs — запуск пресета через Gods of Pharloom (GoP).
//  Как работает: GoP умеет проигрывать список боссов (BossSequence) — так
//  устроены его пантеоны. Мы собираем свой список из пресета и отдаём его GoP,
//  а перед каждым боссом подставляем сложность этого босса
//  (BossSequence.currentDifficultMode: от неё GoP считает HP боссов и урон).
//  Значки и прохождения пантеонов GoP при этом НЕ выдаются.
//  Режим «без урона»: получил удар — сегмент начинается заново (Restart);
//  смерть тоже считается ударом (см. Retry.cs).
//  Что менять: правила запуска — в Launch; сообщение о победе — в OnEndSequence;
//  пауза перед перезапуском «без урона» — RestartDelay.
// ============================================================================
using System;
using System.Collections.Generic;
using Gods_Of_Pharloom;
// В самой игре тоже есть классы BossScene и BossSequence, поэтому классы GoP берём под своими именами.
using GopScene = Gods_Of_Pharloom.BossScene;
using GopSequence = Gods_Of_Pharloom.BossSequence;
using HarmonyLib;
using UnityEngine;

namespace Encore
{
    internal static class Runner
    {
        /// Имя, с которого в GoP начинаются точки возврата у статуй и дверей пантеонов.
        const string BackGatePrefix = "back_entry";

        /// Сколько секунд после удара ждать перед перезапуском «без урона» (Хорнет успевает отлететь).
        const float RestartDelay = 0.6f;
        /// Сколько боссов перечислять в сообщении «на ком получены удары».
        const int BreakdownLimit = 4;

        static GopScene[] seq;        // список, отданный GoP (те же объекты, что в BossScene.bosses)
        static List<Entry> entries;    // строки пресета, по одной на элемент seq
        static int[] phaseAt;          // настройка фазы каждого босса, снятая при старте попытки (PhaseLock.All — нет)
        static bool[] fullAt;          // и челлендж ли это (полное здоровье)
        static int[][] sumAt;          // лимиты призывов каждого босса, снятые при старте попытки (SummonData.Vanilla — как в игре)
        static int[][] chAt;           // шансы призывов каждого босса, снятые при старте попытки (SummonData.Vanilla — как в игре)
        static int[] hpAt;             // своё здоровье каждого босса, снятое при старте попытки (0 — как в игре)
        static int[][] shpAt;          // здоровье призывов каждого босса, снятое при старте попытки (0 — как в игре)
        static bool[] immAt;           // бессмертие каждого босса, снятое при старте попытки
        static int[] atkAt;            // маска выключенных атак каждого босса, снятая при старте попытки (0 — все включены)
        static int speedAt = SpeedMath.Normal; // скорость игры в процентах, снятая при старте попытки (на весь сегмент одна)
        static string buildAt = "";    // метка сборки (привязки GoP, отключённые навыки), снятая при старте попытки (BuildMath.Variant; пусто — обычная)
        static bool buildWarned;       // про сбитую посреди попытки сборку уже сказали (раз за попытку)
        static string presetName;
        static Preset lastPreset;      // пресет, запущенный последним (нужен, чтобы начать заново после смерти)

        static RunLog run;            // текущая попытка: удары по боссам, чем закончилась
        static int attempt;            // номер попытки с момента запуска (растёт при перезапусках «без урона»)
        static bool heroDied;          // в этой попытке Хорнет погибла
        static bool betweenBosses;     // GoP уже ведёт к следующему боссу — перезапуск подождёт загрузки
        static float restartAt;        // когда перезапустить сегмент; 0 — перезапуск не нужен
        static bool stopRequested;     // нажато «Закончить»: после этого сегмент заново не начинаем
        static bool stopPending;       // «Закончить» нажато, но Хорнет ещё не может уйти из комнаты: ждём безопасного момента
        static bool stopping;          // сейчас сами завершаем сегмент (EndSequence — не победа)

        /// Идёт ли сейчас наш сегмент (а не обычный пантеон или статуя GoP).
        internal static bool Active
        {
            get { return seq != null && GopSequence.bossSequence == seq; }
        }

        // ---- список боссов для меню -------------------------------------------

        internal static List<string> Catalog()
        {
            var list = new List<string>();
            try
            {
                foreach (var kv in GopScene.bosses)
                {
                    GopScene b = kv.Value;
                    if (b.sceneType == GopScene.SceneType.Rest) continue;
                    if (!IsGopScene(b.sceneName)) continue; // боссы, добавленные другими модами без своей комнаты
                    list.Add(kv.Key);
                    if (kv.Key == "Moss Mother" && b.ascendedVersion != null) list.Add(Tokens.MossAsc);
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning("Не удалось прочитать список боссов GoP: " + ex.Message);
            }
            return list;
        }

        internal static string Display(string boss)
        {
            return Names.Display(boss); // русские названия — в Names.cs
        }

        static bool IsGopScene(string sceneName)
        {
            return GodsOfPharloomMod.customScenes.Exists(s => s.sceneName == sceneName);
        }

        /// Босс пресета -> объект GoP. null, если такого босса в GoP нет.
        static GopScene Resolve(Entry e)
        {
            GopScene moss;
            if (e.Boss == Tokens.MossAsc)
                return GopScene.bosses.TryGetValue("Moss Mother", out moss) ? moss.ascendedVersion : null;

            GopScene b;
            if (!GopScene.bosses.TryGetValue(e.Boss, out b)) return null;
            if (b.sceneType == GopScene.SceneType.Rest) return b;

            // Как у статуи в Зале Богов: на Ascended/Radiant GoP берёт «старшую» арену босса (есть у Моховой матери).
            if (b.ascendedVersion != null && e.Difficulty != Tokens.Attuned) b = b.ascendedVersion;

            return IsGopScene(b.sceneName) ? b : null;
        }

        // ---- запуск ------------------------------------------------------------

        /// Запускает пресет. Возвращает null, если всё началось, иначе текст причины.
        internal static string Launch(Preset preset)
        {
            if (preset == null) return L.T("Нет выбранного пресета", "No preset selected");

            HeroController hero = HeroController.SilentInstance;
            if (hero == null) return L.T("Сначала загрузи сохранение", "Load a save first");
            if (GopSequence.isInSequence) return L.T("Сейчас уже идёт бой или пантеон", "A fight or pantheon is already running");

            // Собираем список: скамейка не может быть первой, последней или идти два раза подряд.
            var list = new List<Entry>();
            var scenes = new List<GopScene>();
            int skipped = 0;
            foreach (Entry e in preset.Entries)
            {
                GopScene b = Resolve(e);
                if (b == null) { skipped++; continue; }
                bool rest = b.sceneType == GopScene.SceneType.Rest;
                if (rest && (scenes.Count == 0 || scenes[scenes.Count - 1].sceneType == GopScene.SceneType.Rest)) continue;
                list.Add(e);
                scenes.Add(b);
            }
            while (scenes.Count > 0 && scenes[scenes.Count - 1].sceneType == GopScene.SceneType.Rest)
            {
                scenes.RemoveAt(scenes.Count - 1);
                list.RemoveAt(list.Count - 1);
            }
            if (scenes.Count == 0) return L.T("В пресете нет боссов", "The preset has no bosses");

            // Куда вернуться после победы или смерти: ближайшая статуя / дверь пантеона GoP.
            TransitionPoint gate = NearestBackGate(hero.transform.position);
            if (gate == null)
                return L.T("Запускать можно из Атриума или Зала Богов (Gods of Pharloom)",
                           "Start it from the Atrium or the Hall of Gods (Gods of Pharloom)");

            seq = scenes.ToArray();
            entries = list;
            presetName = preset.Name;
            lastPreset = preset;
            Retry.Cancel();
            hudLine = null;
            attempt = 1;
            BeginAttempt();

            // У пресета со спрятанным порядком имена в лог не идут (его видно в окне консоли BepInEx).
            string order = preset.Hidden ? L.T("порядок спрятан, записей ", "order hidden, entries ") + seq.Length : Describe();
            string skippedNote = skipped > 0 ? " (пропущено неизвестных: " + skipped + ")" : "";
            Plugin.Log.LogInfo("Старт «" + preset.Name + "»: " + order + skippedNote);

            // Тип Pantheon + пустое имя пантеона: GoP проигрывает список, но не выдаёт значки.
            GopSequence.SetSequence(seq, gate.gameObject.name, gate.gameObject.scene.name,
                                     GopSequence.SequenceType.Pantheon, entries[0].Difficulty, "", true);
            return null;
        }

        /// Начинает считать новую попытку (первый запуск или перезапуск «без урона»).
        static void BeginAttempt()
        {
            var keys = new string[seq.Length];
            phaseAt = new int[seq.Length];
            fullAt = new bool[seq.Length];
            sumAt = new int[seq.Length][];
            chAt = new int[seq.Length][];
            hpAt = new int[seq.Length];
            shpAt = new int[seq.Length][];
            immAt = new bool[seq.Length];
            atkAt = new int[seq.Length];
            // скорость игры на эту попытку: цифры статистики считаются по ней (если её поменять посреди попытки — см. OnSpeedChanged)
            speedAt = Plugin.EffectiveSpeed;
            GameSpeed.Set(speedAt);
            // сборка Хорнет на эту попытку: навыки из настройки отключаются заново (их могли включить в меню GoP), метка идёт в ключи статистики
            try { Build.ApplySkills(); } catch (Exception ex) { Plugin.Caught("BeginAttempt/ApplySkills", ex); }
            bool buildBroken = false;   // не удалось узнать сборку: что за бой пойдёт, неизвестно, в статистику не идёт (лучше потерять запись, чем испортить её)
            try { buildAt = Build.Variant(); } catch (Exception ex) { Plugin.Caught("BeginAttempt/Variant", ex); buildAt = ""; buildBroken = true; }
            buildWarned = false;
            string speedVariant = SpeedMath.Join(SpeedMath.Variant(speedAt), buildAt);
            for (int i = 0; i < seq.Length; i++)
            {
                if (seq[i].sceneType == GopScene.SceneType.Rest) continue;
                // настройки боссов берём один раз на попытку: меню во время боя их не перепутает
                PresetStore.GetPhase(entries[i], out phaseAt[i], out fullAt[i]);
                sumAt[i] = SummonData.Limits(entries[i]);
                chAt[i] = SummonData.ChanceValues(entries[i]);
                hpAt[i] = PresetStore.GetHp(entries[i]);
                shpAt[i] = SummonData.HpValues(entries[i]);
                immAt[i] = PresetStore.GetImmortal(entries[i]);
                atkAt[i] = AttackData.Mask(entries[i]);
                keys[i] = Stats.BossKey(entries[i].Boss, entries[i].Difficulty, SpeedMath.Join(PhaseLock.Variant(phaseAt[i], fullAt[i]), speedVariant));
            }
            run = new RunLog(SpeedMath.PresetKey(presetName, speedVariant), keys);
            if (buildBroken) run.MarkFrom(0);
            heroDied = false;
            stopRequested = false;
            stopPending = false;
            betweenBosses = false;
            restartAt = 0f;
        }

        static TransitionPoint NearestBackGate(Vector3 from)
        {
            TransitionPoint best = null;
            float bestDist = float.MaxValue;
            foreach (TransitionPoint tp in UnityEngine.Object.FindObjectsByType<TransitionPoint>(FindObjectsSortMode.None))
            {
                if (!tp.gameObject.name.StartsWith(BackGatePrefix)) continue;
                float d = (tp.transform.position - from).sqrMagnitude;
                if (d < bestDist) { bestDist = d; best = tp; }
            }
            return best;
        }

        static string Describe()
        {
            var parts = new List<string>();
            for (int i = 0; i < seq.Length; i++)
                parts.Add(seq[i].sceneType == GopScene.SceneType.Rest ? "[bench]" : entries[i].Boss + " (" + entries[i].Difficulty + ")");
            return string.Join(" > ", parts.ToArray());
        }

        // ---- сложность на каждого босса ---------------------------------------

        /// Вызывается перед каждой сменой комнаты (см. Patches): ставит сложность босса, к которому идём.
        internal static void OnBeginTransition(string targetScene)
        {
            if (!Active) return;
            int i = GopSequence.currentSequenceSceneIndex;
            if (i < 0 || i >= seq.Length) return;

            // Со скамейки GoP переключает номер уже во время загрузки, поэтому смотрим на следующую запись.
            if (seq[i].sceneType == GopScene.SceneType.Rest && seq[i].sceneName != targetScene && i + 1 < seq.Length) i++;

            ApplyDifficulty(i, targetScene);
        }

        /// Подстраховка: то же самое, когда комната уже загрузилась.
        internal static void OnSceneLoaded(string sceneName)
        {
            betweenBosses = false;
            // Каждый шаг в своём try: ошибка в одном не должна пропускать остальные (иначе настройки боя молча не применятся).
            try { Retry.SceneLoaded(); } catch (Exception ex) { Plugin.Caught("OnSceneLoaded/Retry", ex); }
            try { BossFx.LeaveScene(sceneName); } catch (Exception ex) { Plugin.Caught("OnSceneLoaded/BossFx", ex); } // комната другая — таблица фаз GoP снова обычная
            try { Summons.LeaveScene(sceneName); } catch (Exception ex) { Plugin.Caught("OnSceneLoaded/Summons", ex); }
            try { Attacks.LeaveScene(sceneName); } catch (Exception ex) { Plugin.Caught("OnSceneLoaded/Attacks", ex); }
            try { SummonHp.LeaveScene(sceneName); } catch (Exception ex) { Plugin.Caught("OnSceneLoaded/SummonHp", ex); } // комната другая — здоровье призывов в таблице GoP снова обычное
            try { Immortal.LeaveScene(sceneName); } catch (Exception ex) { Plugin.Caught("OnSceneLoaded/Immortal", ex); } // комната другая — бессмертие снимаем
            if (!Active) return;
            int i = GopSequence.currentSequenceSceneIndex;
            // Если не удалось выставить сложность или настройки боя, неизвестно, что за бой пойдёт: не считаем его в статистику (лучше потерять запись, чем испортить её).
            try { ApplyDifficulty(i, sceneName); }
            catch (Exception ex) { Plugin.Caught("OnSceneLoaded/ApplyDifficulty", ex); MarkPractice(); }
            try { ArmBossSettings(i, sceneName); }
            catch (Exception ex) { Plugin.Caught("OnSceneLoaded/ArmBossSettings", ex); MarkPractice(); }
        }

        /// Босс этой комнаты настроен («только фаза 2»)? Тогда BossFx применит это, когда GoP поставит боссу здоровье.
        static void ArmBossSettings(int i, string sceneName)
        {
            if (i < 0 || i >= seq.Length) return;
            if (seq[i].sceneType == GopScene.SceneType.Rest || seq[i].sceneName != sceneName) return;
            if (SpeedMath.IsPractice(speedAt)) MarkPractice(); // медленнее обычного — тренировка, в статистику не идёт
            CheckBuild();
            BossFx.Arm(entries[i].Boss, sceneName, phaseAt[i], fullAt[i], hpAt != null && i < hpAt.Length ? hpAt[i] : 0);
            Summons.Arm(entries[i].Boss, sceneName, sumAt != null && i < sumAt.Length ? sumAt[i] : null, chAt != null && i < chAt.Length ? chAt[i] : null);
            Attacks.Arm(entries[i].Boss, sceneName, atkAt != null && i < atkAt.Length ? atkAt[i] : 0);
            SummonHp.Arm(entries[i].Boss, sceneName, shpAt != null && i < shpAt.Length ? shpAt[i] : null);
            Immortal.Arm(entries[i].Boss, sceneName, ImmortalAt(i), hpAt != null && i < hpAt.Length ? hpAt[i] : 0, shpAt != null && i < shpAt.Length ? shpAt[i] : null);
        }

        /// Включено ли бессмертие у босса номер i (по снимку попытки).
        internal static bool ImmortalAt(int i)
        {
            return immAt != null && i >= 0 && i < immAt.Length && immAt[i];
        }

        /// Скорость игры поменяли (кнопки или файл настроек). Игра уже пошла с новой скоростью; а цифры статистики были бы смешанными
        /// (часть боя на одной скорости, часть на другой), поэтому остаток этой попытки в статистику не идёт, со следующей — идёт.
        internal static void OnSpeedChanged(int percent)
        {
            if (!Active || run == null || percent == speedAt) return;
            speedAt = percent;
            hudLine = null;
            run.MarkFrom(run.Index);
            Plugin.Log.LogInfo("Скорость изменена посреди попытки: остаток попытки в статистику не идёт.");
            Plugin.Toast(L.T("Скорость изменена посреди попытки — остаток попытки не идёт в статистику, со следующей идёт.",
                             "Speed changed in the middle of an attempt — the rest of it is not counted, the next attempt is."), 6f);
        }

        /// Сборка Хорнет (привязки GoP, навыки) в этой комнате та же, что при старте попытки? Нет — кто-то поменял её посреди сегмента
        /// (в сегменте это нельзя и в меню GoP, и в Encore, но проверка дешёвая): бой и остаток попытки в статистику не идут.
        static void CheckBuild()
        {
            if (!Active || run == null) return;
            bool same;
            try { same = Build.Variant() == buildAt && Build.SkillsApplied(); }
            catch (Exception ex) { Plugin.Caught("CheckBuild", ex); same = false; }
            if (same) return;
            run.MarkFrom(run.Index);
            if (buildWarned) return;
            buildWarned = true;
            Plugin.Log.LogInfo("Сборка (привязки, навыки) изменилась посреди попытки: остаток попытки в статистику не идёт.");
            Plugin.Toast(L.T("Привязки или навыки изменились посреди попытки — остаток попытки не идёт в статистику, со следующей идёт.",
                             "Bindings or skills changed in the middle of an attempt — the rest of it is not counted, the next attempt is."), 6f);
        }

        /// BossFx изменил босса: бой считается тренировкой и в статистику не идёт.
        internal static void MarkPractice()
        {
            if (!Active || run == null) return;
            run.MarkPractice(GopSequence.currentSequenceSceneIndex);
        }

        static void ApplyDifficulty(int i, string sceneName)
        {
            if (i < 0 || i >= seq.Length) return;
            if (seq[i].sceneType == GopScene.SceneType.Rest) return;
            if (seq[i].sceneName != sceneName) return; // это переход не к боссу (например, возврат в зал)
            if (GopSequence.currentDifficultMode == entries[i].Difficulty) return;

            GopSequence.currentDifficultMode = entries[i].Difficulty;
            Plugin.Log.LogInfo("Босс " + entries[i].Boss + ": сложность " + entries[i].Difficulty);
        }

        // ---- удары, смерть, перезапуск «без урона» ----------------------------

        /// GoP начал переход к следующему боссу (см. Patches).
        internal static void OnNextBoss()
        {
            if (Active) betweenBosses = true;
        }

        /// Кнопка «Закончить»: отменяет ожидающий перезапуск, а идущий сегмент завершает — Хорнет возвращается в зал.
        /// Уводить её сразу нельзя: если нажато, пока она ещё входит в комнату босса, второй переход ломает состояние игры
        /// (Хорнет не двигается и пауза не работает). Поэтому сегмент заканчивается, как только можно (см. TryStop).
        /// Возвращает короткий текст для строки состояния окна.
        internal static string Stop()
        {
            bool waiting = Retry.Pending;
            Retry.Cancel();
            restartAt = 0f;
            if (!Active)
                return waiting ? L.T("Перезапуск отменён.", "Restart cancelled.")
                               : L.T("Сегмент не идёт.", "No segment is running.");

            stopRequested = true; // и если Хорнет как раз умирает, заново сегмент не начнётся
            stopPending = true;
            TryStop();            // обычно можно сразу
            return stopPending
                ? L.T("Сегмент закончится, как только Хорнет сможет уйти из комнаты.",
                      "The segment will end as soon as Hornet can leave the room.")
                : L.T("Сегмент закончен.", "Segment ended.");
        }

        /// Заканчивает сегмент, если сейчас безопасно: игра в режиме «идёт игра» (не вход в комнату, не пауза, не смена
        /// сцены), Хорнет жива, не в переходе и не на шипах. Иначе ждёт: зовётся каждый кадр, пока стоит stopPending.
        static void TryStop()
        {
            if (!stopPending) return;
            if (!Active) { stopPending = false; return; }              // сегмент уже кончился сам
            HeroController hero = HeroController.SilentInstance;
            GameManager game = GameManager.SilentInstance;
            if (hero == null || game == null) return;
            if (hero.cState.dead) { stopPending = false; return; }     // смерть обработает GoP, заново сегмент не начнём
            if (betweenBosses || game.IsInSceneTransition || game.IsGamePaused()) return;
            if (game.GameState != GlobalEnums.GameState.PLAYING) return;
            if (hero.cState.hazardDeath || hero.cState.hazardRespawning || hero.cState.transitioning) return;

            stopPending = false;
            stopping = true;
            try { GopSequence.EndSequence(); }  // тот же возврат в зал, что после последнего босса, но без победы
            finally { stopping = false; }
            Plugin.Log.LogInfo("«" + presetName + "»: сегмент закончен кнопкой");
            Plugin.Toast(L.T("Сегмент закончен.", "Segment ended."), 3f);
        }

        /// Номер попытки после перезапуска по смерти (см. Retry): счёт продолжается, а не начинается с единицы.
        internal static void SetAttempt(int number)
        {
            attempt = number;
            hudLine = null;
        }

        /// Игра сообщила о смерти Хорнет (см. Patches).
        internal static void OnPlayerDead()
        {
            if (Active) heroDied = true;
        }

        /// Раз в кадр. Вне сегмента — одна проверка; в сегменте — сравнение номера босса и счётчика ударов.
        internal static void Tick()
        {
            BossFx.Tick();
            if (seq == null) { Retry.Tick(); return; }
            if (GopSequence.bossSequence != seq) { Forget(); return; }
            if (stopPending) TryStop();
            if (run == null) return;

            int i = GopSequence.currentSequenceSceneIndex;
            if (i != run.Index) run.Moved(i);

            int hits = GopSequence.hitCounter;
            if (hits != run.TotalHits)
            {
                run.Hits(hits);
                if (Plugin.Hitless && restartAt == 0f) restartAt = Time.unscaledTime + RestartDelay;
            }
            if (restartAt > 0f && Time.unscaledTime >= restartAt) TryRestart();
        }

        /// GoP закончил или сбросил последовательность: записываем смерть (если была) и забываем список.
        static void Forget()
        {
            BossFx.Restore();
            SummonHp.Restore();
            Immortal.Disarm();
            if (run != null && heroDied)
            {
                run.Died();
                Stats.Save();
                // В «Радианте» удар — это смерть, поэтому в режиме «без урона» смерть тоже начинает сегмент заново.
                if (Plugin.RestartsOnDeath && lastPreset != null && !stopRequested) Retry.Schedule(lastPreset, attempt);
            }
            stopPending = false;
            seq = null;
            entries = null;
            phaseAt = null;
            fullAt = null;
            sumAt = null;
            chAt = null;
            hpAt = null;
            shpAt = null;
            immAt = null;
            run = null;
            hudLine = null;
            restartAt = 0f;
            // сегмент кончился: скорость снова такая, какая стоит в настройке (её могли поменять во время сегмента)
            speedAt = SpeedMath.Normal;
            buildAt = "";
            GameSpeed.Set(Plugin.SpeedPercent);
        }

        /// Перезапуск «без урона». Ждёт, пока Хорнет можно безопасно увести из комнаты.
        static void TryRestart()
        {
            if (heroDied || !Plugin.Hitless) { restartAt = 0f; return; } // смерть GoP обработает сам
            HeroController hero = HeroController.SilentInstance;
            GameManager game = GameManager.SilentInstance;
            if (hero == null || game == null) return;
            if (hero.cState.dead) { heroDied = true; restartAt = 0f; return; }
            if (betweenBosses || game.IsInSceneTransition || game.IsGamePaused()) return;
            if (hero.cState.hazardDeath || hero.cState.hazardRespawning || hero.cState.transitioning) return;

            string where = entries[run.Index].IsRest ? Tokens.Rest : entries[run.Index].Boss;
            run.Dropped();
            Stats.Save();
            attempt++;
            Plugin.Log.LogInfo("«" + presetName + "»: удар на " + where + " — сегмент заново, попытка " + attempt);
            Plugin.Toast(L.T("Удар — сегмент заново · попытка ", "Hit — restarting · attempt ") + attempt, 3f);

            // Как при входе в Атриум: полное здоровье и инструменты, затем тот же список с первого босса.
            hero.MaxHealth();
            ToolItemManager.TryReplenishTools(true, ToolItemManager.ReplenishMethod.BenchSilent);
            hudLine = null;
            BeginAttempt();
            GopSequence.SetSequence(seq, GopSequence.backEntry, GopSequence.backScene,
                                    GopSequence.SequenceType.Pantheon, entries[0].Difficulty, "", true);
        }

        // ---- конец и HUD -------------------------------------------------------

        /// GoP вызывает EndSequence после последнего босса (см. Patches).
        internal static void OnEndSequence()
        {
            if (stopping || !Active || GopSequence.isHeroDead || run == null) return;
            run.Hits(GopSequence.hitCounter);
            int hits = run.TotalHits;
            string text = hits == 0
                ? L.T("Сегмент пройден без урона!", "Segment cleared hitless!")
                : L.T("Сегмент пройден · получено ударов: ", "Segment cleared · hits taken: ") + hits
                  + " (" + run.Breakdown(EntryNames(), BreakdownLimit) + ")";
            if (attempt > 1) text += L.T(" · попытка ", " · attempt ") + attempt;
            if (run.Practice) text += L.T(" · тренировка, без статистики", " · practice, not counted");
            run.Cleared();
            Stats.Save();
            Plugin.Log.LogInfo("«" + presetName + "»: " + text);
            Plugin.Toast("«" + presetName + "» — " + text, 8f);
        }

        /// Названия записей сегмента для сообщений.
        static string[] EntryNames()
        {
            var names = new string[entries.Count];
            for (int i = 0; i < names.Length; i++) names[i] = Display(entries[i].Boss);
            return names;
        }

        // Строка сегмента собирается заново, только когда что-то изменилось (босс, удары, язык).
        static string hudLine;
        static int hudIndex, hudHits, hudAttempt;
        static bool hudRu;
        static int hudMode;

        /// Строка для HUD во время сегмента, либо null. Зовётся каждый кадр, поэтому готовая строка запоминается.
        internal static string HudLine()
        {
            if (!Active) return null;
            int i = GopSequence.currentSequenceSceneIndex;
            if (i < 0 || i >= seq.Length) return null;

            int hits = GopSequence.hitCounter;
            bool ru = L.IsRu;
            int mode = Plugin.RestartModeNow;
            bool same = i == hudIndex && hits == hudHits && ru == hudRu && mode == hudMode && attempt == hudAttempt;
            if (hudLine != null && same) return hudLine;
            hudIndex = i;
            hudHits = hits;
            hudRu = ru;
            hudMode = mode;
            hudAttempt = attempt;

            int total = 0, number = 0;
            for (int k = 0; k < seq.Length; k++)
            {
                if (seq[k].sceneType == GopScene.SceneType.Rest) continue;
                total++;
                if (k <= i) number++;
            }
            string where = seq[i].sceneType == GopScene.SceneType.Rest
                ? Display(Tokens.Rest)
                : Display(entries[i].Boss) + " · " + entries[i].Difficulty + (ImmortalAt(i) ? " · " + L.T("бессмертие", "immortal") : "")
                  + (SpeedMath.Changes(speedAt) ? " · " + L.T("скорость ", "speed ") + speedAt + "%" : "")
                  + BuildMath.Suffix(buildAt);
            string tail = mode == RestartMode.Hitless
                ? L.T("без урона · попытка ", "hitless · attempt ") + attempt
                : mode == RestartMode.OnDeath
                    ? L.T("ударов: ", "hits: ") + hits + L.T(" · попытка ", " · attempt ") + attempt
                    : L.T("ударов: ", "hits: ") + hits;
            hudLine = presetName + " · " + number + "/" + total + " · " + where + " · " + tail;
            return hudLine;
        }
    }

    /// Перехваты: смена комнаты, переход к следующему боссу и конец последовательности GoP, смерть Хорнет.
    internal static class Patches
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(GameManager), "BeginSceneTransition")]
        static void BeforeSceneTransition(GameManager.SceneLoadInfo info)
        {
            try { if (info != null) Runner.OnBeginTransition(info.SceneName); }
            catch (Exception ex) { Plugin.Log.LogWarning("OnBeginTransition: " + ex.Message); }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(GopSequence), "NextBoss")]
        static void BeforeNextBoss()
        {
            try { Runner.OnNextBoss(); }
            catch (Exception ex) { Plugin.Caught("NextBoss", ex); }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(GameManager), "PlayerDead")]
        static void BeforePlayerDead()
        {
            try { Runner.OnPlayerDead(); }
            catch (Exception ex) { Plugin.Caught("PlayerDead", ex); }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(GopSequence), "EndSequence")]
        static void BeforeEndSequence()
        {
            try { Runner.OnEndSequence(); }
            catch (Exception ex) { Plugin.Log.LogWarning("OnEndSequence: " + ex.Message); }
        }
    }
}
