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

    /// Одна строка пресета: босс (или скамейка), его сложность и настройка фазы в этом бою.
    [Serializable]
    public sealed class Entry
    {
        public string Boss;
        public string Difficulty = Tokens.Attuned;
        /// Какая фаза босса остаётся в этом бою: 0 — как в игре, N — фаза N (см. Phase.cs). В файл пишется, только если не 0.
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public int Phase;
        /// Челлендж: у босса полное здоровье (без этого — тренировка с здоровьем одной фазы). Действует вместе с Phase.
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public bool Full;
        /// Лимиты призывов босса (см. SummonData.cs): 0 — как в игре, иначе «лимит+1». Первый и второй вид призыва босса.
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public int Sum1;
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public int Sum2;
        /// Своё здоровье босса в этом бою (0.19.0): 0 — как в игре, иначе число от 1 до PhaseLock.HpMax. С выбранной фазой — здоровье этой фазы,
        /// без неё — всего боя. Бой с таким здоровьем — тренировка (в статистику не идёт), «Полное здоровье» с ним не сочетается.
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public int Hp;
        /// Здоровье призывов босса (0.20.0, см. SummonData.HpKinds): 0 — как в игре, иначе число 1…PhaseLock.HpMax. Первый, второй и третий вид призыва.
        /// Бой с таким здоровьем — тренировка (в статистику не идёт).
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public int Sh1;
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public int Sh2;
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public int Sh3;
        /// Бессмертие босса (0.22.0, см. Immortal.cs): босс не умирает, бой не кончается его смертью. Тренировка (в статистику не идёт).
        /// Есть не у всех боссов — см. PhaseLock.ImmortalCapable.
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public bool Imm;
        /// Шансы призывов босса (0.26.0, см. SummonData.Chance): проценты видов (Sc1…Sc3, сумма 100) и шанс самого призыва (Sp). Лежат как «процент+1»,
        /// 0 — как в игре. Бой с такими шансами — тренировка (в статистику не идёт).
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public int Sc1;
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public int Sc2;
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public int Sc3;
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public int Sp;
        /// Выключенные атаки босса (0.29.0, см. AttackData.cs): маска, бит i — атака i из списка босса выключена; 0 — все включены.
        /// Бой с выключенными атаками — тренировка (в статистику не идёт).
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public int At;

        internal bool IsRest { get { return Boss == Tokens.Rest; } }
        internal bool HasSummons { get { return Sum1 != 0 || Sum2 != 0; } }
        internal bool HasHp { get { return Hp > 0; } }
        internal bool HasSummonHp { get { return Sh1 > 0 || Sh2 > 0 || Sh3 > 0; } }
        internal bool HasImmortal { get { return Imm; } }
        internal bool HasChance { get { return Sc1 != 0 || Sc2 != 0 || Sc3 != 0 || Sp != 0; } }
        internal bool HasAttacks { get { return At != 0; } }
        internal bool HasSettings { get { return Phase != 0 || Full || HasSummons || HasHp || HasSummonHp || Imm || HasChance || HasAttacks; } }
    }

    [Serializable]
    public sealed class Preset
    {
        /// Не пустой у стандартных пресетов (нужен кнопке «вернуть стандартные»).
        public string Id;
        public string Name;
        /// Порядок боссов спрятан: в окне вместо имён «???» (так собирается случайный пресет).
        public bool Hidden;
        public List<Entry> Entries = new List<Entry>();

        internal int BossCount
        {
            get { int n = 0; foreach (var e in Entries) if (!e.IsRest) n++; return n; }
        }

        internal Preset Clone(string newName)
        {
            var c = new Preset { Name = newName, Hidden = Hidden };
            foreach (var e in Entries) c.Entries.Add(new Entry { Boss = e.Boss, Difficulty = e.Difficulty, Phase = e.Phase, Full = e.Full, Sum1 = e.Sum1, Sum2 = e.Sum2, Hp = e.Hp, Sh1 = e.Sh1, Sh2 = e.Sh2, Sh3 = e.Sh3, Imm = e.Imm, Sc1 = e.Sc1, Sc2 = e.Sc2, Sc3 = e.Sc3, Sp = e.Sp, At = e.At });
            return c;
        }
    }

    /// Старый вид настройки фазы: одна на босса для всех пресетов (до 0.15.0). Теперь настройка лежит в самой записи
    /// пресета (Entry.Phase / Entry.Full); этот класс нужен только чтобы прочитать старый файл (см. Sanitize).
    [Serializable]
    public sealed class BossSetting
    {
        public string Boss;
        public int Phase;
        /// Челлендж: полное здоровье босса (без этого — тренировка с здоровьем одной фазы).
        public bool Full;
    }

    [Serializable]
    public sealed class Store
    {
        public int Version = 1;
        /// Имя выбранного пресета (его же запускает быстрая клавиша).
        public string Selected;
        /// Сколько боссов собирать в случайный пресет (последнее введённое число).
        public int RandomCount = 10;
        /// Из каких боссов собирать случайный пресет: "p5" (боссы Пантеона 5) или "all" (все боссы GoP).
        public string RandomPool = Pool.P5;
        /// Последний сид случайного пресета и был ли он введён руками (введённый повторяется, придуманный модом — нет).
        public string RandomSeed = "";
        public bool RandomSeedTyped;
        /// Только для чтения старого файла (до 0.15.0): при загрузке переезжает в записи пресетов и больше не пишется.
        public List<BossSetting> Phases = new List<BossSetting>();
        public bool ShouldSerializePhases() { return false; }
        public List<Preset> Presets = new List<Preset>();
    }

    internal static class PresetStore
    {
        /// Метка пресета, в который пишутся случайные наборы.
        internal const string RandomId = "random";
        internal const int RandomMin = 1, RandomMax = 60;

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
            // Случайный пресет, которому дали своё имя, становится обычным: следующий бросок его не затрёт.
            if (p.Id == RandomId && p.Name != newName) p.Id = null;
            string oldName = p.Name;
            p.Name = newName;
            if (wasSelected) Data.Selected = newName;
            Save();
            Stats.RenamePreset(oldName, newName);
            return true;
        }

        /// Добавляет пресет, полученный строкой от другого игрока (см. Share.cs), и выбирает его.
        internal static Preset Import(Preset p)
        {
            p.Id = null;
            if (string.IsNullOrEmpty(p.Name)) p.Name = L.T("Из строки", "Imported");
            p.Name = UniqueName(p.Name);
            Data.Presets.Add(p);
            Data.Selected = p.Name;
            Save();
            return p;
        }

        /// Записывает случайный набор боссов в пресет «Случайный» (создаёт его, если нет) и выбирает его.
        /// benchEvery — скамейка через каждые столько боссов (0 — без скамеек); hide — спрятать порядок в окне.
        /// pool, seed, seedTyped запоминаются в файле, чтобы окно показало их при следующем открытии.
        internal static Preset SetRandom(List<string> bosses, int benchEvery, bool hide, string pool, string seed, bool seedTyped)
        {
            Preset preset = Data.Presets.Find(p => p.Id == RandomId);
            if (preset == null)
            {
                preset = new Preset { Id = RandomId, Name = UniqueName(L.T("Случайный", "Random")) };
                Data.Presets.Add(preset);
            }

            // Сложность остаётся той, что была у пресета до броска (поставил всем Radiant — так и будет).
            string difficulty = Tokens.Attuned;
            foreach (Entry e in preset.Entries)
                if (!e.IsRest) { difficulty = e.Difficulty; break; }

            preset.Entries.Clear();
            for (int i = 0; i < bosses.Count; i++)
            {
                if (benchEvery > 0 && i > 0 && i % benchEvery == 0) preset.Entries.Add(new Entry { Boss = Tokens.Rest });
                preset.Entries.Add(new Entry { Boss = bosses[i], Difficulty = difficulty });
            }

            preset.Hidden = hide;
            Data.RandomCount = bosses.Count;
            Data.RandomPool = Pool.Normalize(pool);
            Data.RandomSeed = seed ?? "";
            Data.RandomSeedTyped = seedTyped;
            Data.Selected = preset.Name;
            Save();
            return preset;
        }

        // ---- фаза босса в бою ---------------------------------------------------

        /// Какая фаза зафиксирована у босса в этой записи пресета (PhaseLock.All — никакая) и челлендж ли это.
        /// У скамейки и босса без настройки всегда «как в игре».
        internal static void GetPhase(Entry e, out int phase, out bool full)
        {
            phase = PhaseLock.All;
            full = false;
            if (e == null || e.IsRest || !PhaseLock.Supports(e.Boss)) return;
            phase = PhaseLock.Normalize(e.Boss, e.Phase);
            full = e.Full && PhaseLock.NormalizeHp(e.Boss, e.Hp) == 0;   // своё здоровье и «полное здоровье» не сочетаются: побеждает своё
        }

        /// Своё здоровье босса в этой записи пресета (0 — как в игре). У скамейки и босса без такой настройки всегда 0.
        internal static int GetHp(Entry e)
        {
            if (e == null || e.IsRest) return 0;
            return PhaseLock.NormalizeHp(e.Boss, e.Hp);
        }

        /// Задаёт своё здоровье босса в записи (0 — как в игре). Своё здоровье выключает «Полное здоровье».
        internal static void SetHp(Entry e, int hp)
        {
            if (e == null || e.IsRest || !PhaseLock.HpCapable(e.Boss)) return;
            int value = PhaseLock.NormalizeHp(e.Boss, hp);
            if (value == e.Hp && (value == 0 || !e.Full)) return;
            PhaseRevision++;
            e.Hp = value;
            if (value > 0) e.Full = false;
            Save();
        }

        /// Бессмертие босса в этой записи пресета. У скамейки и босса, которому его не дают (PhaseLock.ImmortalCapable), всегда false.
        internal static bool GetImmortal(Entry e)
        {
            return e != null && !e.IsRest && e.Imm && PhaseLock.ImmortalCapable(e.Boss);
        }

        /// Включает или выключает бессмертие босса в записи.
        internal static void SetImmortal(Entry e, bool on)
        {
            if (e == null || e.IsRest || !PhaseLock.ImmortalCapable(e.Boss)) return;
            if (e.Imm == on) return;
            PhaseRevision++;
            e.Imm = on;
            Save();
        }

        /// Проценты видов призыва у босса этой записи (длина SummonData.TypeCount). Сумма всегда 100; null — вернуть «как в игре».
        internal static void SetTypeChances(Entry e, int[] pct)
        {
            if (e == null || e.IsRest || SummonData.TypeCount(e.Boss) == 0) return;
            PhaseRevision++;
            SummonData.SetTypes(e, pct);
            Save();
        }

        /// Шанс самого призыва у босса этой записи, % (SummonData.Vanilla — как в игре).
        internal static void SetCallChance(Entry e, int pct)
        {
            if (e == null || e.IsRest || !SummonData.HasCall(e.Boss)) return;
            if (SummonData.GetCall(e) == pct) return;
            PhaseRevision++;
            SummonData.SetCall(e, pct);
            Save();
        }

        /// Включает или выключает атаку index у босса этой записи. false — так нельзя (последнюю включённую атаку выключить не дают).
        internal static bool SetAttack(Entry e, int index, bool off)
        {
            if (e == null || e.IsRest || !AttackData.Has(e.Boss)) return false;
            int before = AttackData.Mask(e);
            if (!AttackData.Set(e, index, off)) return false;
            if (AttackData.Mask(e) != before) { PhaseRevision++; Save(); }
            return true;
        }

        /// Оставляет у босса этой записи только атаку index.
        internal static void SetOnlyAttack(Entry e, int index)
        {
            if (e == null || e.IsRest || !AttackData.Has(e.Boss)) return;
            int before = AttackData.Mask(e);
            if (!AttackData.Only(e, index) || AttackData.Mask(e) == before) return;
            PhaseRevision++;
            Save();
        }

        /// Включает все атаки босса этой записи.
        internal static void ResetAttacks(Entry e)
        {
            if (e == null || e.IsRest || e.At == 0) return;
            PhaseRevision++;
            AttackData.Reset(e);
            Save();
        }

        /// Растёт при каждом изменении настройки боссов (таблица статистики по нему узнаёт, что пора пересобраться).
        internal static int PhaseRevision;

        /// Здоровье вида призыва kind у босса этой записи (0 — как в игре).
        internal static void SetSummonHp(Entry e, int kind, int hp)
        {
            if (e == null || e.IsRest || kind < 0 || kind >= SummonData.HpCount(e.Boss)) return;
            if (SummonData.GetHp(e, kind) == SummonData.ClampHp(hp)) return;
            PhaseRevision++;
            SummonData.SetHp(e, kind, hp);
            Save();
        }

        /// Лимит вида призыва kind у босса этой записи (SummonData.Vanilla — как в игре).
        internal static void SetSummon(Entry e, int kind, int limit)
        {
            if (e == null || e.IsRest || kind < 0 || kind >= SummonData.Count(e.Boss)) return;
            PhaseRevision++;
            SummonData.Set(e, kind, limit);
            Save();
        }

        internal static void SetPhase(Entry e, int mode, bool full)
        {
            if (e == null || e.IsRest || !PhaseLock.Supports(e.Boss)) return;
            PhaseRevision++;
            e.Phase = PhaseLock.Normalize(e.Boss, mode);
            e.Full = full;
            if (full) e.Hp = 0;   // «Полное здоровье» и своё здоровье не сочетаются
            Save();
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
                        if (Sanitize()) Save();
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

        /// Приводит данные из файла в порядок. true — файл надо переписать (переехали старые настройки фаз).
        static bool Sanitize()
        {
            foreach (var p in Data.Presets)
            {
                if (p.Entries == null) p.Entries = new List<Entry>();
                if (string.IsNullOrEmpty(p.Name)) p.Name = UniqueName(L.T("Мой пресет", "My preset"));
                p.Entries.RemoveAll(e => e == null || string.IsNullOrEmpty(e.Boss));
                foreach (var e in p.Entries) e.Difficulty = Tokens.NormalizeDifficulty(e.Difficulty);
            }
            Data.RandomCount = Math.Max(RandomMin, Math.Min(RandomMax, Data.RandomCount));
            Data.RandomPool = Pool.Normalize(Data.RandomPool);
            Data.RandomSeed = Data.RandomSeed ?? "";
            if (Data.RandomSeed.Length > Seed.MaxLength) Data.RandomSeed = Data.RandomSeed.Substring(0, Seed.MaxLength);

            // Настройки фаз: у записей — только допустимые значения для своего босса; у скамейки и босса без фаз их нет.
            foreach (var p in Data.Presets)
                foreach (var e in p.Entries)
                {
                    int phase; bool full;
                    GetPhase(e, out phase, out full);
                    e.Phase = phase;
                    e.Full = full;
                    e.Hp = e.IsRest ? 0 : PhaseLock.NormalizeHp(e.Boss, e.Hp);
                    SummonData.Normalize(e);
                    SummonData.NormalizeHp(e);
                    SummonData.NormalizeChance(e);
                    AttackData.Normalize(e);
                    e.Imm = !e.IsRest && e.Imm && PhaseLock.ImmortalCapable(e.Boss);
                }

            // Файл от версии до 0.15.0: одна настройка на босса для всех пресетов. Переносим её в каждую запись этого
            // босса, у которой своей настройки ещё нет, и больше её не храним (ShouldSerializePhases).
            bool moved = false;
            if (Data.Phases != null)
            {
                var done = new List<string>();
                foreach (BossSetting b in Data.Phases)
                {
                    if (b == null || !PhaseLock.Supports(b.Boss) || done.Contains(b.Boss)) continue;
                    int mode = PhaseLock.Normalize(b.Boss, b.Phase);
                    if (mode == PhaseLock.All && !b.Full) continue;
                    done.Add(b.Boss);
                    foreach (var p in Data.Presets)
                        foreach (var e in p.Entries)
                            if (e.Boss == b.Boss && !e.HasSettings) { e.Phase = mode; e.Full = b.Full; moved = true; }
                }
            }
            if (Data.Phases != null && Data.Phases.Count > 0) moved = true;   // файл перепишем без старого списка
            Data.Phases = new List<BossSetting>();
            return moved;
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

        /// Все боссы стандартных сегментов (42 штуки, без повторов) — пул «P5» случайного пресета.
        internal static List<string> P5Bosses()
        {
            var list = new List<string>();
            foreach (string[] segment in P5)
                foreach (string boss in segment)
                    if (!list.Contains(boss)) list.Add(boss);
            return list;
        }

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
