// ============================================================================
//  SummonData.cs — лимиты призывов и помощников босса: какие виды бывают у какого босса и как лимит лежит в записи пресета.
//  Без Unity (проверяется тестами вне игры). Движок (правка FSM в бою) — Summons.cs.
//  Лимит вида призыва: «как в игре» (SummonData.Vanilla) или число от 0 до Top (0 — отключено). Top — наибольший лимит, который
//  можно выбрать: у мух и Матери Мхов это Max−1 (только ниже ванили, Max — сколько их в ванили), у Зверомухи и Отца раков — 5
//  (выше ванили, недостающих врагов добавляют копии). В записи пресета лежит как «число+1» (0 — как в игре), см. Entry.Sum1 / Entry.Sum2.
//  Здоровье призывов (0.20.0): отдельный список видов (HpKinds) — те, чьё здоровье GoP берёт из своей таблицы EnemyHp (вороны Отца раков,
//  враги Зверомух, подкрепление Старших братьев, Лучины). По одной строке таблицы на вид; число 1…HpMax или 0 («как в игре») лежит в записи
//  пресета (Entry.Sh1…Sh3). Саму подмену таблицы делает SummonHp.cs.
//  Шансы призывов (0.26.0): у боссов, у которых есть случайный выбор, — проценты видов (сумма 100: вороны Отца раков, ползуны и камни Матери Мхов)
//  и шанс самого призыва (зов Отца раков, рёв Зверомух). В записи пресета лежат как «процент+1» (0 — как в игре): Entry.Sc1…Sc3 и Entry.Sp.
//  Саму подмену весов в FSM делает Summons.cs.
//  Что менять: новый босс с призывами — запись в Build() и своя правка FSM в Summons.cs; новое здоровье призыва — запись в BuildHp();
//  новый шанс — запись в BuildChances().
// ============================================================================
using System.Collections.Generic;

namespace Encore
{
    /// Один вид призыва босса.
    internal sealed class SummonKind
    {
        internal string Ru, En;
        /// Сколько таких призывов в ванили (одновременно). Без Above допустимые лимиты — от 0 до Max−1.
        internal int Max;
        /// Наибольший лимит, если его можно поднять выше ванили (0 — нельзя, тогда Top = Max−1).
        internal int Above;
        /// Наибольший лимит, который можно выбрать (лимиты — от 0 до Top).
        internal int Top { get { return Above > 0 ? Above : Max - 1; } }
        internal string Name { get { return L.IsRu ? Ru : En; } }
    }

    /// Вид призыва, чьё здоровье можно задать: строка таблицы GoP, из которой босс берёт здоровье призыва.
    internal sealed class SummonHpKind
    {
        internal string Ru, En;
        /// Ключ строки в EnemyHp.enemies (у призывов одна фаза, здоровье от сложности не зависит).
        internal string Key;
        internal string Name { get { return L.IsRu ? Ru : En; } }
    }

    /// Какие шансы можно задать у босса: виды призыва (проценты, сумма 100) и/или шанс самого призыва.
    internal sealed class ChanceSpec
    {
        internal string[] Ru, En;
        /// Как в игре, в процентах (сумма 100). Пусто — у босса нет выбора вида.
        internal int[] Vanilla;
        /// Шанс самого призыва в игре, % (−1 — у босса такого шанса нет); наибольший шанс, который можно выбрать.
        internal int CallVanilla, CallMax;
        internal string CallRu, CallEn;
        internal int Types { get { return Vanilla.Length; } }
        internal string TypeName(int i) { return L.IsRu ? Ru[i] : En[i]; }
        internal string CallName { get { return L.IsRu ? CallRu : CallEn; } }
    }

    internal static class SummonData
    {
        /// Лимит «как в игре».
        internal const int Vanilla = -1;
        /// Сколько видов призыва у одного босса бывает не больше (поля Entry.Sum1 и Entry.Sum2).
        internal const int Slots = 2;

        /// Сколько видов призыва со здоровьем у одного босса бывает не больше (поля Entry.Sh1…Sh3).
        internal const int HpSlots = 3;

        /// Сколько видов с процентами у одного босса бывает не больше (поля Entry.Sc1…Sc3); в снимке для боя после них идёт шанс призыва (ChanceSlots).
        internal const int TypeSlots = 3;
        internal const int ChanceSlots = TypeSlots + 1;

        static readonly SummonKind[] None = new SummonKind[0];
        static readonly Dictionary<string, SummonKind[]> kinds = Build();
        static readonly SummonHpKind[] NoHp = new SummonHpKind[0];
        static readonly Dictionary<string, ChanceSpec> chances = BuildChances();
        static readonly Dictionary<string, SummonHpKind[]> hpKinds = BuildHp();

        static Dictionary<string, SummonKind[]> Build()
        {
            // Broodmother: четыре мухи в пуле (FSM «Control» считает свободных детей «Spawner Flies»; до 0.26.5 тут было три — ошибка на единицу); лимит — сколько живых одновременно.
            SummonKind[] flies = { new SummonKind { Ru = "Мухи", En = "Flies", Max = 4 } };
            // Матерь Мхов: ползуны (одновременно 1–2) и камни (падают по её сигналу «FALL»; их можно только выключить).
            SummonKind[] moss =
            {
                new SummonKind { Ru = "Ползуны", En = "Crawlers", Max = 2 },
                new SummonKind { Ru = "Камни", En = "Rocks", Max = 1 }
            };
            // Зверомуха: враги из пула «Summon Enemies» (в Часовне 5, в Дальних полях 2); лимит — сколько живых одновременно, до 5 (недостающих копируем).
            SummonKind[] beastChapel = { new SummonKind { Ru = "Враги", En = "Enemies", Max = 5, Above = 5 } };
            SummonKind[] beastFields = { new SummonKind { Ru = "Враги", En = "Enemies", Max = 2, Above = 5 } };
            // Отец раков: четыре ворона в пуле (Pin Wielder 1, Dagger 1, Tinies 2); лимит — сколько живых одновременно, до 5 (пятого копируем).
            SummonKind[] crows = { new SummonKind { Ru = "Вороны", En = "Crows", Max = 4, Above = 5 } };
            var map = new Dictionary<string, SummonKind[]>();
            map["Broodmother"] = flies;
            map["Savage Beastfly in Chapel of The Beast"] = beastChapel;
            map["Savage Beastfly in Far Fields"] = beastFields;
            map["Crawfather"] = crows;
            map["Moss Mother"] = moss;
            map[Tokens.MossAsc] = moss;
            return map;
        }

        static SummonHpKind H(string ru, string en, string key)
        {
            return new SummonHpKind { Ru = ru, En = en, Key = key };
        }

        static Dictionary<string, SummonHpKind[]> BuildHp()
        {
            var map = new Dictionary<string, SummonHpKind[]>();
            // Зверомуха (Часовня Зверя): призывает Килика, муху и злого каранида из пула «Summon Enemies»; здоровье каждого GoP берёт из таблицы.
            map["Savage Beastfly in Chapel of The Beast"] = new[]
            {
                H("Килик", "Kilik", "Kilik"),
                H("Муха", "Beastfly", "Beastfly"),
                H("Злой каранид", "Vicious Caranid", "Vicious Caranid")
            };
            map["Savage Beastfly in Far Fields"] = new[] { H("Тармит", "Tarmite", "Tarmite") };
            map["Sister Splinter"] = new[] { H("Щепка", "Splinterbark", "Splinterbark") };
            map["Forebrothers Signis & Gron"] = new[]
            {
                H("Кремнелёт", "Flintstone Flyer", "Flintstone Flyer"),
                H("Просеиватель", "Smokerock Sifter", "Smokerock Sifter")
            };
            map["Crawfather"] = new[]
            {
                H("Булавочник", "Pin Wielder", "Pin Wielder Craw"),
                H("Кинжальщик", "Dagger", "Dagger Craw"),
                H("Малыш", "Tinie", "Tinie Craw")
            };
            return map;
        }

        /// Виды призыва у босса (пусто — призывов с настройкой нет).
        internal static SummonKind[] Kinds(string boss)
        {
            SummonKind[] k;
            return boss != null && kinds.TryGetValue(boss, out k) ? k : None;
        }

        internal static int Count(string boss)
        {
            return Kinds(boss).Length;
        }

        /// Лимит из записи в допустимых границах для вида kind этого босса (Vanilla — как в игре или значение не подходит).
        internal static int FromStored(string boss, int kind, int stored)
        {
            SummonKind[] k = Kinds(boss);
            if (kind < 0 || kind >= k.Length) return Vanilla;
            int limit = stored - 1;
            return limit >= 0 && limit <= k[kind].Top ? limit : Vanilla;
        }

        internal static int ToStored(int limit)
        {
            return limit < 0 ? 0 : limit + 1;
        }

        internal static int Stored(Entry e, int kind)
        {
            return kind == 0 ? e.Sum1 : kind == 1 ? e.Sum2 : 0;
        }

        /// Лимит вида kind в этой записи пресета. У скамейки и босса без таких призывов всегда «как в игре».
        internal static int Get(Entry e, int kind)
        {
            if (e == null || e.IsRest) return Vanilla;
            return FromStored(e.Boss, kind, Stored(e, kind));
        }

        /// Лимиты всех видов (длина Slots): снимок для боя.
        internal static int[] Limits(Entry e)
        {
            var result = new int[Slots];
            for (int i = 0; i < Slots; i++) result[i] = Get(e, i);
            return result;
        }

        internal static void Set(Entry e, int kind, int limit)
        {
            if (e == null || e.IsRest || kind < 0 || kind >= Count(e.Boss)) return;
            int stored = ToStored(FromStored(e.Boss, kind, ToStored(limit)));
            if (kind == 0) e.Sum1 = stored; else e.Sum2 = stored;
        }

        /// Приводит значения записи к допустимым для её босса (чужие и лишние отбрасываются).
        internal static void Normalize(Entry e)
        {
            e.Sum1 = ToStored(Get(e, 0));
            e.Sum2 = ToStored(Get(e, 1));
        }

        internal static bool Any(int[] limits)
        {
            if (limits == null) return false;
            foreach (int x in limits) if (x != Vanilla) return true;
            return false;
        }

        /// «выкл» для нуля, иначе число.
        internal static string LimitText(int limit)
        {
            return limit == 0 ? L.T("выкл", "off") : limit.ToString();
        }

        /// «Ползуны: 1, Камни: выкл» — для сообщений и журнала. Пусто — всё как в игре.
        internal static string Describe(string boss, int[] limits)
        {
            SummonKind[] k = Kinds(boss);
            var parts = new List<string>();
            for (int i = 0; i < k.Length && limits != null && i < limits.Length; i++)
                if (limits[i] != Vanilla) parts.Add(k[i].Name + ": " + LimitText(limits[i]));
            return string.Join(", ", parts.ToArray());
        }

        // ---- здоровье призывов (0.20.0) ----------------------------------------------

        /// Виды призыва босса со здоровьем (пусто — у этого босса задать здоровье призывов нельзя).
        internal static SummonHpKind[] HpKinds(string boss)
        {
            SummonHpKind[] k;
            return boss != null && hpKinds.TryGetValue(boss, out k) ? k : NoHp;
        }

        internal static int HpCount(string boss)
        {
            return HpKinds(boss).Length;
        }

        /// Здоровье в допустимых границах: 0 — как в игре, иначе 1…PhaseLock.HpMax.
        internal static int ClampHp(int hp)
        {
            return hp < 1 ? 0 : hp > PhaseLock.HpMax ? PhaseLock.HpMax : hp;
        }

        static int StoredHp(Entry e, int kind)
        {
            return kind == 0 ? e.Sh1 : kind == 1 ? e.Sh2 : kind == 2 ? e.Sh3 : 0;
        }

        /// Здоровье вида призыва kind в этой записи пресета (0 — как в игре). У скамейки и босса без таких призывов всегда 0.
        internal static int GetHp(Entry e, int kind)
        {
            if (e == null || e.IsRest || kind < 0 || kind >= HpCount(e.Boss)) return 0;
            return ClampHp(StoredHp(e, kind));
        }

        internal static void SetHp(Entry e, int kind, int hp)
        {
            if (e == null || e.IsRest || kind < 0 || kind >= HpCount(e.Boss)) return;
            hp = ClampHp(hp);
            if (kind == 0) e.Sh1 = hp; else if (kind == 1) e.Sh2 = hp; else e.Sh3 = hp;
        }

        /// Здоровье всех видов (длина HpSlots): снимок для боя.
        internal static int[] HpValues(Entry e)
        {
            var result = new int[HpSlots];
            for (int i = 0; i < HpSlots; i++) result[i] = GetHp(e, i);
            return result;
        }

        /// Приводит значения записи к допустимым для её босса (чужие и лишние отбрасываются).
        internal static void NormalizeHp(Entry e)
        {
            e.Sh1 = GetHp(e, 0);
            e.Sh2 = GetHp(e, 1);
            e.Sh3 = GetHp(e, 2);
        }

        internal static bool AnyHp(int[] values)
        {
            if (values == null) return false;
            foreach (int x in values) if (x > 0) return true;
            return false;
        }

        /// «Килик: 30, Муха: 10» — для сообщений и журнала. Пусто — всё как в игре.
        internal static string DescribeHp(string boss, int[] values)
        {
            SummonHpKind[] k = HpKinds(boss);
            var parts = new List<string>();
            for (int i = 0; i < k.Length && values != null && i < values.Length; i++)
                if (values[i] > 0) parts.Add(k[i].Name + ": " + values[i]);
            return string.Join(", ", parts.ToArray());
        }

        // ---- шансы призывов (0.26.0) --------------------------------------------------

        static Dictionary<string, ChanceSpec> BuildChances()
        {
            var map = new Dictionary<string, ChanceSpec>();
            // Отец раков: тип ворона выбирается в «Call Type» поровну (по 1,0), зов — в «Call?» (CALL 0,75 против 0,25).
            map["Crawfather"] = new ChanceSpec
            {
                Ru = new[] { "Булавочник", "Кинжальщик", "Малыш" },
                En = new[] { "Pin Wielder", "Dagger", "Tinies" },
                Vanilla = new[] { 34, 33, 33 },
                CallVanilla = 75, CallMax = 100, CallRu = "Зов", CallEn = "Call"
            };
            // Матерь Мхов: что бросить — в «Drop Type» (камень или ползун, поровну).
            var moss = new ChanceSpec
            {
                Ru = new[] { "Ползуны", "Камни" },
                En = new[] { "Crawlers", "Rocks" },
                Vanilla = new[] { 50, 50 },
                CallVanilla = -1, CallMax = 0, CallRu = "", CallEn = ""
            };
            map["Moss Mother"] = moss;
            map[Tokens.MossAsc] = moss;
            // Зверомуха: призыв — рёв в «Choice» (одна из трёх атак). Верхнюю границу держим ниже 100: в игре рёв может отказаться (мало свободных врагов,
            // перезарядка), и тогда босс тут же выбирает снова — при 100 % он бы выбирал без конца.
            var beast = new ChanceSpec
            {
                Ru = new string[0], En = new string[0], Vanilla = new int[0],
                CallVanilla = 33, CallMax = 95, CallRu = "Рёв", CallEn = "Roar"
            };
            map["Savage Beastfly in Chapel of The Beast"] = beast;
            map["Savage Beastfly in Far Fields"] = beast;
            return map;
        }

        /// Что у босса можно настроить по шансам (null — ничего).
        internal static ChanceSpec Chance(string boss)
        {
            ChanceSpec c;
            return boss != null && chances.TryGetValue(boss, out c) ? c : null;
        }

        internal static int TypeCount(string boss)
        {
            ChanceSpec c = Chance(boss);
            return c == null ? 0 : c.Types;
        }

        internal static bool HasCall(string boss)
        {
            ChanceSpec c = Chance(boss);
            return c != null && c.CallVanilla >= 0;
        }

        /// Есть ли у босса хоть какой-то шанс для настройки.
        internal static bool HasChance(string boss)
        {
            return TypeCount(boss) > 0 || HasCall(boss);
        }

        static int StoredType(Entry e, int kind)
        {
            return kind == 0 ? e.Sc1 : kind == 1 ? e.Sc2 : kind == 2 ? e.Sc3 : 0;
        }

        /// Проценты видов из записи (длина TypeCount) или null — «как в игре» (не заданы или не подходят: чужие, сумма не 100).
        internal static int[] TypesOrNull(Entry e)
        {
            if (e == null || e.IsRest) return null;
            ChanceSpec c = Chance(e.Boss);
            if (c == null || c.Types == 0) return null;
            var result = new int[c.Types];
            int sum = 0;
            for (int i = 0; i < TypeSlots; i++)
            {
                int stored = StoredType(e, i);
                if (i >= c.Types) { if (stored != 0) return null; continue; }
                int pct = stored - 1;
                if (pct < 0 || pct > 100) return null;
                result[i] = pct;
                sum += pct;
            }
            return sum == 100 ? result : null;
        }

        /// Проценты видов, как они сейчас в игре у этого босса: заданные или «как в игре».
        internal static int[] TypePercents(Entry e)
        {
            int[] set = TypesOrNull(e);
            if (set != null) return set;
            ChanceSpec c = e == null ? null : Chance(e.Boss);
            return c == null ? new int[0] : (int[])c.Vanilla.Clone();
        }

        /// Записывает проценты видов; null (или не подходящие — сумма не 100) — «как в игре».
        internal static void SetTypes(Entry e, int[] pct)
        {
            if (e == null || e.IsRest) return;
            ChanceSpec c = Chance(e.Boss);
            e.Sc1 = e.Sc2 = e.Sc3 = 0;
            if (c == null || c.Types == 0 || pct == null || pct.Length != c.Types) return;
            int sum = 0;
            foreach (int x in pct)
            {
                if (x < 0 || x > 100) return;
                sum += x;
            }
            if (sum != 100) return;
            e.Sc1 = pct[0] + 1;
            if (pct.Length > 1) e.Sc2 = pct[1] + 1;
            if (pct.Length > 2) e.Sc3 = pct[2] + 1;
        }

        /// Двигает процент вида index на value (0…100); остальные подстраиваются так, чтобы сумма осталась 100 и их соотношение сохранилось
        /// (если у остальных поровну нуль — делятся поровну). Возвращает новый массив.
        internal static int[] Rebalance(int[] pct, int index, int value)
        {
            int n = pct.Length;
            var result = new int[n];
            if (n == 0) return result;
            if (n == 1) { result[0] = 100; return result; }
            value = value < 0 ? 0 : value > 100 ? 100 : value;
            result[index] = value;
            int rest = 100 - value;
            int others = 0;
            for (int i = 0; i < n; i++) if (i != index) others += pct[i];

            // доли остальных: по старым процентам, а если там нули — поровну
            var share = new double[n];
            for (int i = 0; i < n; i++)
                if (i != index) share[i] = others > 0 ? (double)pct[i] / others : 1.0 / (n - 1);

            // целые части + остаток самым большим дробным частям (сумма ровно rest)
            var frac = new double[n];
            int given = 0;
            for (int i = 0; i < n; i++)
            {
                if (i == index) continue;
                double exact = share[i] * rest;
                result[i] = (int)exact;
                frac[i] = exact - result[i];
                given += result[i];
            }
            for (int left = rest - given; left > 0; left--)
            {
                int best = -1;
                for (int i = 0; i < n; i++)
                    if (i != index && (best < 0 || frac[i] > frac[best])) best = i;
                result[best]++;
                frac[best] = -1.0;
            }
            return result;
        }

        /// Шанс самого призыва из записи, % (−1 — как в игре или не подходит).
        internal static int GetCall(Entry e)
        {
            if (e == null || e.IsRest || !HasCall(e.Boss)) return Vanilla;
            int pct = e.Sp - 1;
            return pct >= 0 && pct <= Chance(e.Boss).CallMax ? pct : Vanilla;
        }

        internal static void SetCall(Entry e, int pct)
        {
            if (e == null || e.IsRest || !HasCall(e.Boss)) return;
            e.Sp = pct < 0 || pct > Chance(e.Boss).CallMax ? 0 : pct + 1;
        }

        /// Приводит значения записи к допустимым для её босса (чужие и лишние отбрасываются).
        internal static void NormalizeChance(Entry e)
        {
            if (e == null) return;
            SetTypes(e, TypesOrNull(e));
            SetCall(e, GetCall(e));
            if (e.IsRest || !HasCall(e.Boss)) e.Sp = 0;   // у босса без зова (и у скамейки) ячейка шанса зова пуста
            if (e.IsRest) e.Sc1 = e.Sc2 = e.Sc3 = 0;
        }

        /// Снимок шансов для боя (длина ChanceSlots): [0] — шанс призыва, [1…] — проценты видов; Vanilla (−1) — как в игре.
        internal static int[] ChanceValues(Entry e)
        {
            var result = new int[ChanceSlots];
            for (int i = 0; i < ChanceSlots; i++) result[i] = Vanilla;
            if (e == null || e.IsRest) return result;
            result[0] = GetCall(e);
            int[] types = TypesOrNull(e);
            if (types != null) for (int i = 0; i < types.Length && i < TypeSlots; i++) result[1 + i] = types[i];
            return result;
        }

        internal static bool AnyChance(int[] values)
        {
            if (values == null) return false;
            foreach (int x in values) if (x != Vanilla) return true;
            return false;
        }

        /// «Зов: 50%, Булавочник: 10% …» — для сообщений и журнала. Пусто — всё как в игре.
        internal static string DescribeChance(string boss, int[] values)
        {
            ChanceSpec c = Chance(boss);
            if (c == null || values == null || values.Length < ChanceSlots) return "";
            var parts = new List<string>();
            if (values[0] != Vanilla && c.CallVanilla >= 0) parts.Add(c.CallName + ": " + values[0] + "%");
            if (values[1] != Vanilla)
                for (int i = 0; i < c.Types && 1 + i < values.Length; i++) parts.Add(c.TypeName(i) + ": " + values[1 + i] + "%");
            return string.Join(", ", parts.ToArray());
        }
    }
}
