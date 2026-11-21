// ============================================================================
//  AttackData.cs — атаки боссов, которые можно выключить (0.29.0): какие атаки бывают у какого босса и как выбор лежит в записи пресета.
//  Без Unity (проверяется тестами вне игры). Движок (правка случайного выбора в FSM босса) — Attacks.cs.
//  Как это работает. Босс Silksong выбирает следующую атаку действием PlayMaker «SendRandomEvent…» (состояния «Attack Choice», «Close Range», «Choice P2»
//  и так далее): у каждой атаки там своё событие («JUMP DIVE», «TENDRIL»), вес и две страховки: «не больше N раз подряд» (eventMax) и «обязательно
//  выбрать, если её не было N выборов» (missedMax). Чтобы выключить атаку, ставим её событию вес 0 и снимаем «обязательно» (иначе игра всё равно её
//  выберет) во всех выборах FSM босса, где это событие встречается: так выключается и сама атака, и её продолжения («Followup?»).
//  Хранение: в записи пресета (Entry.At) лежит маска ВЫКЛЮЧЕННЫХ атак: бит i — атака i из списка босса (нумерация по порядку списка ниже, не менять
//  порядок у боссов, которые уже вышли, — иначе чужие пресеты поедут). 0 — все атаки включены. Выключить все нельзя (хоть одна остаётся).
//  Список атак — это события из дампов FSM (разбор в проекте: encore-attacks-plan.md); названия по-русски — мои, описательные, не из игры.
//  Что менять: новый босс — запись в Build() (имя босса как в Names.cs, объект и FSM как в дампе, события — из «events = […]» состояний выбора).
// ============================================================================
using System.Collections.Generic;

namespace Encore
{
    /// Одна атака босса: что показать в меню и какие события FSM её запускают (их все выключаем вместе).
    internal sealed class AttackKind
    {
        internal string Ru, En;
        internal string[] Events;
        internal string Name { get { return L.IsRu ? Ru : En; } }
    }

    /// Атаки одного босса: объект и FSM, где живёт выбор, и список атак.
    internal sealed class AttackSpec
    {
        internal string Boss, Object, Fsm;
        internal AttackKind[] Kinds;
    }

    /// Что делать с одним случайным выбором FSM: какие записи выключить и (если осталась одна) какую беречь от «не больше N подряд».
    internal sealed class ChoicePlan
    {
        internal bool[] Off;
        /// Номер единственной записи с весом больше нуля, оставшейся после выключения (−1 — таких несколько).
        internal int Keep = -1;
    }

    internal static class AttackMath
    {
        /// «Никогда не принуждать»: у выключенной атаки «обязательно выбрать после N выборов» сдвигаем так далеко, что оно не наступит.
        internal const int Never = 99999;
        /// «Сколько раз подряд можно» у единственной оставшейся записи: без этого игра после первого же раза сто раз перебрасывает и берёт нулевую запись.
        internal const int Unlimited = 99;

        /// План для одного выбора: events[i] — имя события (null — «ничего»), weights[i] — вес, off — выключенные события.
        /// null — менять нечего: ни одно событие не выключено или выключение оставило бы выбор без веса (тогда выбор не трогаем, игра работает как была).
        internal static ChoicePlan Plan(string[] events, float[] weights, ICollection<string> off)
        {
            if (events == null || weights == null || off == null || off.Count == 0) return null;
            int n = events.Length < weights.Length ? events.Length : weights.Length;
            var plan = new ChoicePlan { Off = new bool[n] };
            int hits = 0, left = 0, keep = -1;
            for (int i = 0; i < n; i++)
            {
                if (events[i] != null && off.Contains(events[i])) { plan.Off[i] = true; hits++; continue; }
                if (weights[i] > 0f) { left++; keep = i; }
            }
            if (hits == 0 || left == 0) return null;
            plan.Keep = left == 1 ? keep : -1;
            return plan;
        }
    }

    internal static class AttackData
    {
        /// Сколько атак у одного босса бывает не больше (маска — int, 7 цифр в строке обмена: 2^20 − 1 = 1 048 575).
        internal const int Max = 20;

        static readonly AttackKind[] None = new AttackKind[0];
        static readonly Dictionary<string, AttackSpec> specs = Build();

        static AttackKind A(string ru, string en, params string[] events)
        {
            return new AttackKind { Ru = ru, En = en, Events = events };
        }

        static void Add(Dictionary<string, AttackSpec> map, string boss, string obj, params AttackKind[] kinds)
        {
            map[boss] = new AttackSpec { Boss = boss, Object = obj, Fsm = "Control", Kinds = kinds };
        }

        static Dictionary<string, AttackSpec> Build()
        {
            var map = new Dictionary<string, AttackSpec>();

            // Акт 1–2 (порядок атак в списке не менять: он записан в чужих пресетах)
            Add(map, "Bell Beast", "Bone Beast",
                A("Прыжок", "Leap", "LEAP"), A("Рывок", "Charge", "CHARGE"), A("Взрыв колокола", "Bell burst", "BURST"));
            Add(map, "Lace in Deep Docks", "Lace Boss1",
                A("Серия", "Combo", "COMBO"), A("Рывок", "Charge", "CHARGE"), A("Удар J", "J slash", "J SLASH"));
            Add(map, "Fourth Chorus", "song_golem",
                A("Прицельный удар", "Aim slam", "AIM SLAM"), A("Взмах", "Swipe", "ANY SWIPE"), A("Случайный удар", "Random slam", "RANDOM SLAM"),
                A("Угольный залп", "Coal barrage", "COAL BARRAGE"));
            Add(map, "Moorwing", "Vampire Gnat",
                A("Серпы", "Flap sickle", "FLAP SICKLE"), A("Взмах", "Flap", "FLAP"), A("Рывок", "Charge", "CHARGE"), A("Удар", "Slash", "SLASH"),
                A("Ярость", "Rage", "RAGE"));
            Add(map, "Sister Splinter", "Splinter Queen",
                A("Удар", "Slam", "SLAM"), A("Рёв", "Roar", "ROAR"), A("Шипы", "Spikes", "SPIKES"));
            Add(map, "Widow", "Spinner Boss",
                A("Удар в погоне", "Chase slam", "CHASE SLAM"), A("Рывок с ударом", "Dash slash", "DASH SLASH"), A("Перебежка", "Scuttle", "SCUTTLE"),
                A("Выстрел нитью", "Strum shot", "STRUM SHOT"), A("Крик", "Scream", "SCREAM"));
            Add(map, "The Last Judge", "Last Judge",
                A("Удар", "Slam", "SLAM"), A("Прыжок", "Jump", "JUMP"), A("Рывок", "Dash", "DASH"), A("Топот", "Stomp", "STOMP"),
                A("Вращение", "Spin", "SPIN"), A("Разбег", "Charge", "CHARGE"));
            Add(map, "Broodmother", "Slab Fly Broodmother",
                A("Прыжок", "Leap", "LEAP"), A("Плевок", "Spit", "SPIT"), A("Вылупление", "Hatch", "HATCH"), A("Удар", "Slam", "SLAM"));
            Add(map, "Nyleth", "Flower Queen Boss",
                A("Прыжок", "Leap", "LEAP"), A("Шипы", "Spikes", "SPIKE SUMMON"), A("Выстрел", "Shoot", "SHOOT"), A("Пыльца", "Pollen blast", "POLLEN BLAST"));
            Add(map, "Trobbio", "Trobbio",
                A("Торнадо", "Tornado", "TORNADO"), A("Бомбы", "Bombs", "BOMB THROW"), A("Прыжок", "Jump", "JUMP"), A("Вспышка", "Flash", "DAZZLE FLASH"),
                A("Столбы", "Columns", "BURST COLUMNS"));
            Add(map, "Raging Conchfly", "Coral Conch Driller Giant Solo",
                A("Горизонтальный", "Horizontal", "ATTACK H"), A("Вертикальный", "Vertical", "ATTACK V"), A("Рёв", "Roar", "ROAR"), A("Выстрел", "Shoot", "SHOOT"),
                A("Рывок миньонов", "Minion charge", "MINION CHARGE"));
            Add(map, "Groal the Great", "Swamp Shaman",
                A("Плевок", "Spit", "SPIT"), A("Засасывание", "Suck", "SUCK"), A("Пике", "Dive", "DIVE"), A("Огненный шар", "Fireball", "FIREBALL"));

            // Акт 3
            Add(map, "Crust King Khann", "Coral King",
                A("Тычок", "Jab", "JAB"), A("Тычок сверху", "Air jab", "AIR JAB"), A("Апперкот", "Uppercut", "UPPERCUT"), A("Рёв", "Roar", "ROAR"),
                A("Крестовый удар", "Cross chop", "CROSS CHOP"), A("Шипы", "Spikes", "SHOOT SPIKES"));
            Add(map, "Phantom", "Phantom",
                A("Серия уколов", "Stab combo", "STAB COMBO"), A("Бросок в воздухе", "Air throw", "A THROW"), A("Бросок с земли", "Ground throw", "G THROW"),
                A("Драгун", "Dragoon", "DRAGOON"));
            Add(map, "Second Sentiel", "Song Knight",
                A("Рывок", "Dash attack", "DASH ATTACK"), A("Пике", "Dive slash", "DIVE SLASH"), A("Ветровой удар", "Wind slash", "WIND SLASH"),
                A("Крестовый удар", "Cross slash", "CROSS SLASH"), A("Удар вверх", "Rising slash", "RISING SLASH"));
            Add(map, "The Unravelled", "Conductor Boss",
                A("Выстрел", "Shoot", "SHOOT"), A("Рывок", "Charge", "CHARGE"), A("Град копий", "Barrage", "BARRAGE"), A("Рёв", "Roar", "ROAR"));
            Add(map, "First Sinner", "First Weaver",
                A("Иглы", "Pins", "PINS"), A("Удар", "Slash", "SLASH"), A("Рывок с разрезом", "Slice charge", "SLICE CHARGE"), A("Бомба", "Bomb", "BOMB"));
            Add(map, "Lace in the Cradle", "Lace Boss2 New",
                A("Серия", "Combo", "COMBO"), A("Рывок", "Charge", "CHARGE"), A("Удар J", "J slash", "J SLASH"));
            Add(map, "Gurr the Outcast", "Bone Hunter Trapper",
                A("Рывок", "Charge", "CHARGE"), A("Зарыться", "Dig in", "DIG IN"), A("Прыжок", "Hop", "HOP"), A("Шипы", "Spikes", "SPIKES"));
            Add(map, "Tormented Trobbio", "Tormented Trobbio",
                A("Торнадо", "Tornado", "TORNADO"), A("Бомбы", "Bombs", "BOMB THROW"), A("Прыжок", "Jump", "JUMP"), A("Вспышка", "Flash", "DAZZLE FLASH"),
                A("Столбы", "Columns", "BURST COLUMNS"));
            Add(map, "Shrine Guardian Seth", "Seth",
                A("Серия", "Slash combo", "SLASH COMBO"), A("Пике", "Jump dive", "JUMP DIVE"), A("Бросок с земли", "Ground throw", "G THROW"),
                A("Бросок в прыжке", "Jump throw", "JUMP THROW"), A("Телепорт", "Tele out", "TELE OUT"), A("Удар из тени", "Fade slash", "FADE SLASH"));
            Add(map, "Pinstress", "Pinstress Boss",
                A("Рывок", "Dash", "DASH"), A("Бросок", "Throw", "THROW"), A("Таран", "Charge", "CHARGE"), A("Бросок вниз", "Down throw", "DOWN THROW"));
            Add(map, "Skarrsinger Karmelita", "Hunter Queen Boss",
                A("Прыжок с вращением", "Jump spin", "JUMP SPIN"), A("Серия", "Slash combo", "SLASH COMBO"), A("Циклон", "Cyclone spin", "CYCLONE SPIN"),
                A("Бросок", "Throw", "THROW"), A("Бросок в воздухе", "Air throw", "AIR THROW"), A("Скользящий рывок", "Dash grind", "DASH GRIND"));
            Add(map, "Lost Lace", "Lost Lace Boss",
                A("Серия", "Combo", "COMBO"), A("Рывок", "Charge", "CHARGE"), A("Удар J", "J slash", "J SLASH"), A("Щупальца", "Tendril", "TENDRIL"),
                A("Рвота", "Vomit", "VOMIT"), A("Пули", "Bullets", "BULLET SUMMON"), A("Призыв щупалец", "Tendril summon", "TENDRIL SUMMON"),
                A("Крестовый удар", "Cross slash", "CROSS SLASH"));
            return map;
        }

        internal static AttackSpec Spec(string boss)
        {
            AttackSpec s;
            return boss != null && specs.TryGetValue(boss, out s) ? s : null;
        }

        internal static bool Has(string boss)
        {
            return Spec(boss) != null;
        }

        /// Все боссы, у которых есть список атак (в порядке добавления в Build нельзя положиться — словарь; нужен для тестов).
        internal static IEnumerable<AttackSpec> All { get { return specs.Values; } }

        internal static AttackKind[] Kinds(string boss)
        {
            AttackSpec s = Spec(boss);
            return s == null ? None : s.Kinds;
        }

        internal static int Count(string boss)
        {
            return Kinds(boss).Length;
        }

        static int Full(int count)
        {
            return count >= 31 ? int.MaxValue : (1 << count) - 1;
        }

        /// Маска выключенных атак в допустимых границах для босса: лишние биты отбрасываются, «выключены все» — как «включены все».
        internal static int Clean(string boss, int mask)
        {
            int n = Count(boss);
            if (n == 0 || mask < 0) return 0;
            int full = Full(n);
            mask &= full;
            return mask == full ? 0 : mask;
        }

        /// Маска выключенных атак у этой записи (0 — всё включено). У скамейки и босса без списка всегда 0.
        internal static int Mask(Entry e)
        {
            if (e == null || e.IsRest) return 0;
            return Clean(e.Boss, e.At);
        }

        internal static bool IsOff(Entry e, int index)
        {
            return index >= 0 && index < Count(e == null ? null : e.Boss) && (Mask(e) & (1 << index)) != 0;
        }

        /// Сколько атак остаётся включено.
        internal static int Enabled(string boss, int mask)
        {
            int n = Count(boss), left = 0;
            for (int i = 0; i < n; i++) if ((mask & (1 << i)) == 0) left++;
            return left;
        }

        /// Выключает (off = true) или включает атаку index. false — так нельзя (последнюю включённую выключить не дают) или номер не тот.
        internal static bool Set(Entry e, int index, bool off)
        {
            if (e == null || e.IsRest || index < 0 || index >= Count(e.Boss)) return false;
            int mask = Mask(e);
            int bit = 1 << index;
            int next = off ? mask | bit : mask & ~bit;
            if (off && Enabled(e.Boss, next) == 0) return false;
            e.At = next;
            return true;
        }

        /// Оставляет только атаку index (остальные выключает).
        internal static bool Only(Entry e, int index)
        {
            if (e == null || e.IsRest || index < 0 || index >= Count(e.Boss)) return false;
            e.At = Full(Count(e.Boss)) & ~(1 << index);
            if (Count(e.Boss) == 1) e.At = 0;
            return true;
        }

        internal static void Reset(Entry e)
        {
            if (e != null) e.At = 0;
        }

        /// Приводит значение записи к допустимому для её босса.
        internal static void Normalize(Entry e)
        {
            if (e == null) return;
            e.At = e.IsRest ? 0 : Clean(e.Boss, e.At);
        }

        /// События всех выключенных атак (для правки FSM). Пусто — менять нечего.
        internal static HashSet<string> OffEvents(string boss, int mask)
        {
            var set = new HashSet<string>();
            AttackKind[] k = Kinds(boss);
            mask = Clean(boss, mask);
            for (int i = 0; i < k.Length; i++)
                if ((mask & (1 << i)) != 0) foreach (string ev in k[i].Events) set.Add(ev);
            return set;
        }

        /// «Рывок, Пике» — названия выключенных атак для сообщений и журнала. Пусто — всё включено.
        internal static string DescribeOff(string boss, int mask)
        {
            AttackKind[] k = Kinds(boss);
            mask = Clean(boss, mask);
            var parts = new List<string>();
            for (int i = 0; i < k.Length; i++) if ((mask & (1 << i)) != 0) parts.Add(k[i].Name);
            return string.Join(", ", parts.ToArray());
        }

        /// Если осталась одна атака — её название, иначе null.
        internal static string OnlyName(string boss, int mask)
        {
            AttackKind[] k = Kinds(boss);
            mask = Clean(boss, mask);
            if (mask == 0 || Enabled(boss, mask) != 1) return null;
            for (int i = 0; i < k.Length; i++) if ((mask & (1 << i)) == 0) return k[i].Name;
            return null;
        }
    }
}
