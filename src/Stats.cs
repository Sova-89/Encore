// ============================================================================
//  Stats.cs — статистика: попытки и прохождения пресетов, бои с боссами.
//  Файл:   BepInEx\config\Encore\stats.json
//  Читается один раз — когда статистика впервые понадобилась (первый запуск
//  сегмента или кнопка «Статистика»). Пишется только в конце попытки:
//  сегмент пройден, смерть или перезапуск режима «без урона».
//  Что менять: какие числа считаются — в PresetStats и BossStats ниже.
// ============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using Newtonsoft.Json;

namespace Encore
{
    /// Числа одного пресета.
    [Serializable]
    public sealed class PresetStats
    {
        public int Attempts;        // сколько раз сегмент начинали (перезапуск «без урона» — тоже попытка)
        public int Clears;          // сколько раз прошли до конца
        public int HitlessClears;   // из них без единого удара
        public int Deaths;
        public int BestHits = -1;   // меньше всего ударов за прохождение; -1 — ещё не проходили
    }

    /// Числа одного босса на одной сложности (общие для всех пресетов).
    [Serializable]
    public sealed class BossStats
    {
        public int Fights;          // бои, которые чем-то закончились: победа, смерть, перезапуск
        public int Wins;
        public int HitlessWins;
        public int Hits;            // удары, полученные за все эти бои
        public int Deaths;
    }

    [Serializable]
    public sealed class StatsData
    {
        public int Version = 1;
        public Dictionary<string, PresetStats> Presets = new Dictionary<string, PresetStats>();
        public Dictionary<string, BossStats> Bosses = new Dictionary<string, BossStats>();
    }

    internal static class Stats
    {
        static StatsData data;

        /// Растёт при каждом изменении: по нему окно понимает, что строки пора собрать заново.
        internal static int Revision;

        internal static string FilePath
        {
            get { return Path.Combine(Path.Combine(Paths.ConfigPath, "Encore"), "stats.json"); }
        }

        static StatsData Data
        {
            get { if (data == null) Load(); return data; }
        }

        /// Ключ босса в файле: имя и сложность.
        internal static string BossKey(string boss, string difficulty)
        {
            return boss + " | " + difficulty;
        }

        /// Ключ с меткой челленджа («Trobbio | Attuned | phase 2»); без метки — прежний ключ.
        internal static string BossKey(string boss, string difficulty, string variant)
        {
            return string.IsNullOrEmpty(variant) ? BossKey(boss, difficulty) : BossKey(boss, difficulty) + " | " + variant;
        }

        // ---- чтение (ничего не создаёт) ---------------------------------------

        internal static PresetStats FindPreset(string name)
        {
            PresetStats found;
            return Data.Presets.TryGetValue(name, out found) ? found : null;
        }

        internal static BossStats FindBoss(string key)
        {
            BossStats found;
            return Data.Bosses.TryGetValue(key, out found) ? found : null;
        }

        // ---- запись ------------------------------------------------------------

        internal static PresetStats Preset(string name)
        {
            PresetStats found;
            if (!Data.Presets.TryGetValue(name, out found))
            {
                found = new PresetStats();
                Data.Presets[name] = found;
            }
            Revision++;
            return found;
        }

        internal static BossStats Boss(string key)
        {
            BossStats found;
            if (!Data.Bosses.TryGetValue(key, out found))
            {
                found = new BossStats();
                Data.Bosses[key] = found;
            }
            Revision++;
            return found;
        }

        /// Пресет переименовали — его числа (и числа его челленджей: скорости, сборки) переезжают на новое имя.
        internal static void RenamePreset(string oldName, string newName)
        {
            if (oldName == newName || !File.Exists(FilePath) && data == null) return;
            // вместе с обычными числами переезжают и числа челленджей («имя | speed 150», «имя | build NS»)
            var moves = new List<string>();
            foreach (string key in Data.Presets.Keys)
                if (key == oldName || SpeedMath.IsVariantKeyOf(key, oldName)) moves.Add(key);
            if (moves.Count == 0) return;
            foreach (string key in moves)
            {
                PresetStats moved = Data.Presets[key];
                Data.Presets.Remove(key);
                Data.Presets[newName + key.Substring(oldName.Length)] = moved;
            }
            Revision++;
            Save();
        }

        /// Стирает всю статистику.
        internal static void Clear()
        {
            data = new StatsData();
            Revision++;
            Save();
        }

        // ---- файл --------------------------------------------------------------

        static void Load()
        {
            data = new StatsData();
            try
            {
                if (!File.Exists(FilePath)) return;
                var read = JsonConvert.DeserializeObject<StatsData>(File.ReadAllText(FilePath));
                if (read == null) return;
                if (read.Presets == null) read.Presets = new Dictionary<string, PresetStats>();
                if (read.Bosses == null) read.Bosses = new Dictionary<string, BossStats>();
                data = read;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning("stats.json не прочитан, статистика начинается заново: " + ex.Message);
            }
        }

        internal static void Save()
        {
            if (data == null) return;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                string tmp = FilePath + ".tmp";
                File.WriteAllText(tmp, JsonConvert.SerializeObject(data, Formatting.Indented));
                File.Copy(tmp, FilePath, true);
                File.Delete(tmp);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning("Не удалось сохранить stats.json: " + ex.Message);
            }
        }

        /// Для проверок вне игры: забыть прочитанное, чтобы файл прочитался заново.
        internal static void Forget()
        {
            data = null;
        }
    }

    /// Одна попытка пройти сегмент: кто сколько ударов нанёс и чем всё кончилось.
    /// Runner сообщает сюда события, а отсюда они попадают в Stats.
    internal sealed class RunLog
    {
        readonly string preset;
        readonly string[] bossKeys;   // по одному на запись сегмента; у скамейки null
        readonly int[] hits;          // удары, полученные на каждой записи
        readonly bool[] practice;     // босс на этой записи изменён настройкой (BossFx): бой в статистику не идёт
        bool anyPractice;             // в попытке есть такой бой: клир и смерть сегмента тоже не считаются
        int index;
        bool finished;

        internal int TotalHits;
        internal int Index { get { return index; } }

        internal RunLog(string presetName, string[] keys)
        {
            preset = presetName;
            bossKeys = keys;
            hits = new int[keys.Length];
            practice = new bool[keys.Length];
            Stats.Preset(preset).Attempts++;
        }

        internal bool Practice { get { return anyPractice; } }

        /// Босс на записи i изменён настройкой («только фаза 2»): это тренировка, статистику она не меняет.
        internal void MarkPractice(int i)
        {
            if (finished || i < 0 || i >= practice.Length) return;
            practice[i] = true;
            if (anyPractice) return;
            anyPractice = true;
            PresetStats stats = Stats.Preset(preset);
            if (stats.Attempts > 0) stats.Attempts--; // тренировочная попытка — не попытка пройти сегмент
        }

        /// Скорость игры поменяли посреди попытки: записи с номера from и дальше (и сама попытка) в статистику не идут.
        /// Уже закончившиеся бои до from остаются как записаны.
        internal void MarkFrom(int from)
        {
            if (finished) return;
            for (int i = Math.Max(0, from); i < practice.Length; i++) practice[i] = true;
            if (anyPractice) return;
            anyPractice = true;
            PresetStats stats = Stats.Preset(preset);
            if (stats.Attempts > 0) stats.Attempts--; // такая попытка — не попытка пройти сегмент
        }

        /// Счётчик ударов GoP вырос: разницу записываем на текущего босса.
        internal void Hits(int total)
        {
            if (finished || total <= TotalHits) return;
            hits[index] += total - TotalHits;
            TotalHits = total;
        }

        /// GoP перешёл к следующей записи — значит, босс на прежней побеждён.
        internal void Moved(int newIndex)
        {
            if (finished || newIndex == index || newIndex < 0 || newIndex >= bossKeys.Length) return;
            RecordFight(true, false);
            index = newIndex;
        }

        /// Последний босс побеждён, сегмент пройден.
        internal void Cleared()
        {
            if (finished) return;
            RecordFight(true, false);
            finished = true;
            if (anyPractice) return;

            PresetStats stats = Stats.Preset(preset);
            stats.Clears++;
            if (TotalHits == 0) stats.HitlessClears++;
            if (stats.BestHits < 0 || TotalHits < stats.BestHits) stats.BestHits = TotalHits;
        }

        internal void Died()
        {
            if (finished) return;
            RecordFight(false, true);
            finished = true;
            if (!anyPractice) Stats.Preset(preset).Deaths++;
        }

        /// Попытку прервал режим «без урона»: бой засчитан, но не победа и не смерть.
        internal void Dropped()
        {
            if (finished) return;
            RecordFight(false, false);
            finished = true;
        }

        void RecordFight(bool won, bool died)
        {
            string key = bossKeys[index];
            if (key == null || practice[index]) return; // скамейка или тренировка
            BossStats stats = Stats.Boss(key);
            stats.Fights++;
            stats.Hits += hits[index];
            if (won) stats.Wins++;
            if (won && hits[index] == 0) stats.HitlessWins++;
            if (died) stats.Deaths++;
        }

        /// «Вдова 2, Лейс 1» — на ком получены удары, от большего к меньшему. names — названия записей.
        internal string Breakdown(string[] names, int limit)
        {
            var order = new List<int>();
            for (int i = 0; i < hits.Length; i++) if (hits[i] > 0 && bossKeys[i] != null) order.Add(i);
            order.Sort((a, b) => hits[b] != hits[a] ? hits[b].CompareTo(hits[a]) : a.CompareTo(b));

            var parts = new List<string>();
            for (int i = 0; i < order.Count && i < limit; i++) parts.Add(names[order[i]] + " " + hits[order[i]]);
            if (order.Count > limit) parts.Add("...");
            return string.Join(", ", parts.ToArray());
        }
    }
}
