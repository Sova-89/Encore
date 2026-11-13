// ============================================================================
//  Runner.cs — запуск пресета через Gods of Pharloom (GoP).
//  Как работает: GoP умеет проигрывать список боссов (BossSequence) — так
//  устроены его пантеоны. Мы собираем свой список из пресета и отдаём его GoP,
//  а перед каждым боссом подставляем сложность этого босса
//  (BossSequence.currentDifficultMode: от неё GoP считает HP боссов и урон).
//  Значки и прохождения пантеонов GoP при этом НЕ выдаются.
//  Что менять: правила запуска — в Launch; сообщение о победе — в OnEndSequence.
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

        static GopScene[] seq;        // список, отданный GoP (те же объекты, что в BossScene.bosses)
        static List<Entry> entries;    // строки пресета, по одной на элемент seq
        static string presetName;

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

            Plugin.Log.LogInfo("Старт «" + preset.Name + "»: " + Describe() + (skipped > 0 ? " (пропущено неизвестных: " + skipped + ")" : ""));

            // Тип Pantheon + пустое имя пантеона: GoP проигрывает список, но не выдаёт значки.
            GopSequence.SetSequence(seq, gate.gameObject.name, gate.gameObject.scene.name,
                                     GopSequence.SequenceType.Pantheon, entries[0].Difficulty, "", true);
            return null;
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
            if (!Active) return;
            ApplyDifficulty(GopSequence.currentSequenceSceneIndex, sceneName);
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

        // ---- конец и HUD -------------------------------------------------------

        /// GoP вызывает EndSequence после последнего босса (см. Patches).
        internal static void OnEndSequence()
        {
            if (!Active || GopSequence.isHeroDead) return;
            int hits = GopSequence.hitCounter;
            string text = hits == 0
                ? L.T("Сегмент пройден без урона!", "Segment cleared hitless!")
                : L.T("Сегмент пройден · получено ударов: ", "Segment cleared · hits taken: ") + hits;
            Plugin.Log.LogInfo("«" + presetName + "»: " + text);
            Plugin.Toast("«" + presetName + "» — " + text, 6f);
        }

        /// Раз в кадр: забываем список, когда GoP закончил или сбросил последовательность.
        internal static void Tick()
        {
            if (seq != null && GopSequence.bossSequence != seq) { seq = null; entries = null; }
        }

        /// Строка для HUD во время сегмента, либо null.
        internal static string HudLine()
        {
            if (!Active) return null;
            int i = GopSequence.currentSequenceSceneIndex;
            if (i < 0 || i >= seq.Length) return null;

            int total = 0, number = 0;
            for (int k = 0; k < seq.Length; k++)
            {
                if (seq[k].sceneType == GopScene.SceneType.Rest) continue;
                total++;
                if (k <= i) number++;
            }
            string where = seq[i].sceneType == GopScene.SceneType.Rest
                ? Display(Tokens.Rest)
                : Display(entries[i].Boss) + " · " + entries[i].Difficulty;
            return presetName + " · " + number + "/" + total + " · " + where
                 + " · " + L.T("ударов: ", "hits: ") + GopSequence.hitCounter;
        }
    }

    /// Два перехвата: перед сменой комнаты и в конце последовательности GoP.
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
        [HarmonyPatch(typeof(GopSequence), "EndSequence")]
        static void BeforeEndSequence()
        {
            try { Runner.OnEndSequence(); }
            catch (Exception ex) { Plugin.Log.LogWarning("OnEndSequence: " + ex.Message); }
        }
    }
}
