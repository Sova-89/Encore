// ============================================================================
//  Model.cs — данные пресетов и их хранение в файле.
//  Пресет = имя + список записей. Запись = босс (или скамейка) + сложность.
//  Файл:   BepInEx\config\Encore\presets.json  (можно править руками).
//  Что менять: стандартные пресеты (сегменты Пантеона 5) — в Defaults ниже.
// ============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using Newtonsoft.Json;

namespace Encore
{
    /// Служебные имена, которые встречаются в пресетах.
    internal static class Tokens
    {
        /// Скамейка между боссами (так же называется в GoP: BossScene.bosses["RestScene"]).
        internal const string Rest = "RestScene";
        /// Моховая мать в арене Weave_03 (так начинается настоящий Пантеон 5 в GoP).
        internal const string MossAsc = "Moss Mother (Weave_03)";

        internal const string Attuned = "Attuned";
        internal const string Ascended = "Ascended";
        internal const string Radiant = "Radiant";
        internal static readonly string[] Difficulties = { Attuned, Ascended, Radiant };

        internal static string NextDifficulty(string d)
        {
            int i = Array.IndexOf(Difficulties, d);
            return Difficulties[(i + 1 + Difficulties.Length) % Difficulties.Length];
        }

        internal static string NormalizeDifficulty(string d)
        {
            return Array.IndexOf(Difficulties, d) >= 0 ? d : Attuned;
        }
    }

    /// Одна строка пресета: босс (или скамейка) и сложность этого босса.
    [Serializable]
    public sealed class Entry
    {
        public string Boss;
        public string Difficulty = Tokens.Attuned;

        internal bool IsRest { get { return Boss == Tokens.Rest; } }
    }

    [Serializable]
    public sealed class Preset
    {
        /// Не пустой у стандартных пресетов (нужен кнопке «вернуть стандартные»).
        public string Id;
        public string Name;
        public List<Entry> Entries = new List<Entry>();

        internal int BossCount
        {
            get { int n = 0; foreach (var e in Entries) if (!e.IsRest) n++; return n; }
        }

        internal Preset Clone(string newName)
        {
            var c = new Preset { Name = newName };
            foreach (var e in Entries) c.Entries.Add(new Entry { Boss = e.Boss, Difficulty = e.Difficulty });
            return c;
        }
    }

    [Serializable]
    public sealed class Store
    {
        public int Version = 1;
        /// Имя выбранного пресета (его же запускает быстрая клавиша).
        public string Selected;
        public List<Preset> Presets = new List<Preset>();
    }

    internal static class PresetStore
    {
        internal static Store Data = new Store();

        static string Dir { get { return Path.Combine(Paths.ConfigPath, "Encore"); } }
        internal static string FilePath { get { return Path.Combine(Dir, "presets.json"); } }

        // ---- выбранный пресет --------------------------------------------------

        internal static int SelectedIndex
        {
            get
            {
                for (int i = 0; i < Data.Presets.Count; i++)
                    if (Data.Presets[i].Name == Data.Selected) return i;
                return Data.Presets.Count > 0 ? 0 : -1;
            }
        }

        internal static Preset Selected
        {
            get { int i = SelectedIndex; return i < 0 ? null : Data.Presets[i]; }
        }

        internal static void Select(int index)
        {
            if (Data.Presets.Count == 0) return;
            index = (index % Data.Presets.Count + Data.Presets.Count) % Data.Presets.Count;
            Data.Selected = Data.Presets[index].Name;
            Save();
        }

        // ---- операции над пресетами -------------------------------------------

        internal static string UniqueName(string wanted)
        {
            string name = wanted; int n = 2;
            while (Data.Presets.Exists(p => p.Name == name)) name = wanted + " (" + (n++) + ")";
            return name;
        }

        internal static Preset AddEmpty()
        {
            var p = new Preset { Name = UniqueName(L.T("Мой пресет", "My preset")) };
            Data.Presets.Add(p);
            Data.Selected = p.Name;
            Save();
            return p;
        }

        internal static Preset Duplicate(Preset src)
        {
            var p = src.Clone(UniqueName(src.Name + " " + L.T("(копия)", "(copy)")));
            Data.Presets.Add(p);
            Data.Selected = p.Name;
            Save();
            return p;
        }

        internal static void Delete(Preset p)
        {
            int i = Data.Presets.IndexOf(p);
            if (i < 0) return;
            Data.Presets.RemoveAt(i);
            if (Data.Presets.Count == 0) Data.Presets.Add(new Preset { Name = L.T("Мой пресет", "My preset") });
            Data.Selected = Data.Presets[Math.Min(i, Data.Presets.Count - 1)].Name;
            Save();
        }

        internal static bool Rename(Preset p, string newName)
        {
            newName = (newName ?? "").Trim();
            if (newName.Length == 0) return false;
            if (Data.Presets.Exists(x => x != p && x.Name == newName)) return false;
            bool wasSelected = Data.Selected == p.Name;
            p.Name = newName;
            if (wasSelected) Data.Selected = newName;
            Save();
            return true;
        }

        /// Добавляет недостающие стандартные пресеты (сегменты Пантеона 5). Возвращает, сколько добавлено.
        internal static int RestoreDefaults()
        {
            int added = 0;
            foreach (var d in Defaults.Build())
            {
                if (Data.Presets.Exists(p => p.Id == d.Id)) continue;
                d.Name = UniqueName(d.Name);
                Data.Presets.Add(d);
                added++;
            }
            if (added > 0) Save();
            return added;
        }

        // ---- файл --------------------------------------------------------------

        internal static void Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    var s = JsonConvert.DeserializeObject<Store>(File.ReadAllText(FilePath));
                    if (s != null && s.Presets != null && s.Presets.Count > 0)
                    {
                        Data = s;
                        Sanitize();
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning("presets.json не прочитан, делаю копию .bad и начинаю заново: " + ex.Message);
                try { File.Copy(FilePath, FilePath + ".bad", true); } catch { }
            }

            Data = new Store { Presets = Defaults.Build() };
            Data.Selected = Data.Presets[0].Name;
            Save();
        }

        static void Sanitize()
        {
            foreach (var p in Data.Presets)
            {
                if (p.Entries == null) p.Entries = new List<Entry>();
                if (string.IsNullOrEmpty(p.Name)) p.Name = UniqueName(L.T("Мой пресет", "My preset"));
                p.Entries.RemoveAll(e => e == null || string.IsNullOrEmpty(e.Boss));
                foreach (var e in p.Entries) e.Difficulty = Tokens.NormalizeDifficulty(e.Difficulty);
            }
        }

        internal static void Save()
        {
            try
            {
                Directory.CreateDirectory(Dir);
                string tmp = FilePath + ".tmp";
                File.WriteAllText(tmp, JsonConvert.SerializeObject(Data, Formatting.Indented));
                File.Copy(tmp, FilePath, true);
                File.Delete(tmp);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning("Не удалось сохранить presets.json: " + ex.Message);
            }
        }
    }

    /// Стандартные пресеты: Пантеон 5 из GoP, разбитый скамейками на сегменты.
    internal static class Defaults
    {
        // Порядок взят из GoP (ModInit.cs, Pantheon 5): 9 сегментов между 8 скамейками.
        static readonly string[][] P5 =
        {
            new[] { Tokens.MossAsc, "Skull Tyrant", "Bell Beast", "Savage Beastfly in Chapel of The Beast", "Lace in Deep Docks" },
            new[] { "Fourth Chorus", "Moorwing", "Great Conchflies", "Sister Splinter", "Widow" },
            new[] { "The Last Judge", "Broodmother", "Nyleth", "Voltvyrm", "Trobbio" },
            new[] { "Lost Garmond", "Clover Dancers", "Disgraced Chef Lugoli", "Raging Conchfly", "Groal the Great" },
            new[] { "Crust King Khann", "Phantom", "Crawfather", "Forebrothers Signis & Gron", "Second Sentiel" },
            new[] { "Lace in the Cradle", "Plasmified Zango", "Watcher at the Edge", "The Unravelled", "First Sinner" },
            new[] { "Bell Eater", "Father of the Flame", "Palestag", "Gurr the Outcast", "Tormented Trobbio" },
            new[] { "Shrine Guardian Seth", "Savage Beastfly in Far Fields", "Shakra", "Pinstress", "Skarrsinger Karmelita" },
            new[] { "Grand Mother Silk", "Lost Lace" },
        };

        internal static List<Preset> Build()
        {
            var list = new List<Preset>();
            for (int i = 0; i < P5.Length; i++)
            {
                var p = new Preset
                {
                    Id = "p5-seg-" + (i + 1),
                    Name = L.T("P5 · сегмент ", "P5 · segment ") + (i + 1),
                };
                foreach (var boss in P5[i]) p.Entries.Add(new Entry { Boss = boss, Difficulty = Tokens.Attuned });
                list.Add(p);
            }
            return list;
        }
    }
}
