// ============================================================================
//  ModLogic.cs — чистая логика перенесённых модов, бессмертия, режимов перезапуска и скорости игры (без Unity и игры), её проверяют тесты.
//  Hit Counter 1.1.4 и Always Double Incoming Damage 1.7.0 — прежние отдельные моды
//  Abdulbasit, встроенные в Encore как есть. Здесь только арифметика и правила счёта.
// ============================================================================
using System;
using System.Collections.Generic;

namespace Encore
{
    /// Решает, считать ли очередной вызов «Хорнет получила урон» новым ударом.
    /// Одну и ту же атаку игра иногда засчитывает несколькими вызовами подряд (особые атаки «тикают»,
    /// обычная после них даёт хвост); такие повторы в пределах MultiTickGap секунд склеиваются в один удар.
    internal sealed class HitFilter
    {
        internal const float MultiTickGap = 0.75f;

        float lastAccepted = -100f;
        bool specialOpen;

        internal void Reset()
        {
            lastAccepted = -100f;
            specialOpen = false;
        }

        /// now — время игры в секундах; special — урон пришёл из особой атаки (DoSpecialDamage).
        internal bool ShouldCount(float now, bool special)
        {
            float delta = now - lastAccepted;
            if (special)
            {
                if (specialOpen && delta < MultiTickGap)
                {
                    lastAccepted = now;
                    return false;
                }
                lastAccepted = now;
                specialOpen = true;
                return true;
            }
            if (specialOpen && delta < MultiTickGap)
            {
                lastAccepted = now;
                specialOpen = false;
                return false;
            }
            lastAccepted = now;
            specialOpen = false;
            return true;
        }
    }

    internal static class DamageMath
    {
        /// Урон × множитель; множитель меньше 1 считается как 1, переполнение даёт int.MaxValue.
        internal static int Multiply(int amount, int multiplier)
        {
            int m = Math.Max(1, multiplier);
            return amount > int.MaxValue / m ? int.MaxValue : amount * m;
        }

        /// Текст плашки при переключении («DOUBLE DAMAGE: ON (x2)» / «DOUBLE DAMAGE: OFF»).
        internal static string StatusText(bool on, int multiplier)
        {
            return "DOUBLE DAMAGE: " + (on ? "ON (x" + Math.Max(1, multiplier).ToString() + ")" : "OFF");
        }
    }

    /// Правила бессмертия босса (Immortal.cs): кого считать боссом и сколько урона можно нанести, чтобы здоровье не упало ниже 1.
    internal static class ImmortalMath
    {
        /// С какого здоровья существо в бою считается боссом, а не его призывом (у призывов здоровье меньше).
        internal const int BossFloor = 50;

        /// Порог «это босс»: BossFloor, а если у босса задано своё здоровье меньше, то оно (иначе такой босс остался бы смертным).
        internal static int Floor(int customHp)
        {
            return customHp > 0 && customHp < BossFloor ? customHp : BossFloor;
        }

        /// Босс ли существо с таким здоровьем в первый миг боя. Призыв со своим здоровьем (Sh1…Sh3) боссом не считается,
        /// даже если оно выше порога.
        internal static bool IsBoss(int hp, int floor, int[] summonHp)
        {
            if (hp < floor) return false;
            if (summonHp != null)
                for (int i = 0; i < summonHp.Length; i++)
                    if (summonHp[i] > 0 && summonHp[i] == hp) return false;
            return true;
        }

        /// Сколько урона из damage пройдёт, чтобы у существа с здоровьем hp осталось не меньше 1 (при hp ≤ 1 — ничего).
        internal static int Allowed(int hp, int damage)
        {
            if (damage <= 0 || hp <= 1) return 0;
            int room = hp - 1;
            return damage < room ? damage : room;
        }
    }

    /// Режим перезапуска сегмента (0.23.0): обычный (ничего не перезапускается), рестарт при смерти (с уроном: удары не мешают,
    /// умерла — сегмент заново), без урона (удар — сразу заново, смерть тоже). Две настройки (Mode/Hitless, Mode/RestartOnDeath) сводятся к одному числу.
    internal static class RestartMode
    {
        internal const int Normal = 0, OnDeath = 1, Hitless = 2;

        /// Режим по двум настройкам: «без урона» сильнее.
        internal static int From(bool hitless, bool restartOnDeath)
        {
            return hitless ? Hitless : restartOnDeath ? OnDeath : Normal;
        }

        /// Следующий режим по кругу: обычный → рестарт при смерти → без урона → обычный.
        internal static int Next(int mode)
        {
            return mode == Normal ? OnDeath : mode == OnDeath ? Hitless : Normal;
        }

        /// Настройки для режима (любое неизвестное число — обычный).
        internal static void Split(int mode, out bool hitless, out bool restartOnDeath)
        {
            hitless = mode == Hitless;
            restartOnDeath = mode == OnDeath;
        }

        /// Перезапускать ли сегмент после смерти Хорнет.
        internal static bool RestartsOnDeath(int mode)
        {
            return mode == OnDeath || mode == Hitless;
        }

        /// Перезапускать ли сегмент сразу после удара.
        internal static bool RestartsOnHit(int mode)
        {
            return mode == Hitless;
        }
    }

    /// Скорость игры (0.24.0): общая настройка в процентах на всю игру. Выше 100% — челлендж (бой идёт в статистику со своей строкой),
    /// ниже 100% — тренировка (в статистику не идёт), ровно 100% — как в игре.
    internal static class SpeedMath
    {
        internal const int Normal = 100, Min = 25, Max = 300, Step = 5, BigStep = 25;
        const string Word = "speed ";

        /// Приводит число к допустимому: от 25 до 300 с шагом 5.
        internal static int Clamp(int percent)
        {
            if (percent < Min) return Min;
            if (percent > Max) return Max;
            return ((percent + Step / 2) / Step) * Step;
        }

        /// Во сколько раз менять игровое время.
        internal static float Factor(int percent)
        {
            return Clamp(percent) / 100f;
        }

        /// Меняет ли это число скорость игры (100% — нет).
        internal static bool Changes(int percent)
        {
            return Clamp(percent) != Normal;
        }

        /// Быстрее обычного: челлендж, бой считается в статистике.
        internal static bool IsChallenge(int percent)
        {
            return Clamp(percent) > Normal;
        }

        /// Медленнее обычного: тренировка, бой в статистику не идёт.
        internal static bool IsPractice(int percent)
        {
            return Clamp(percent) < Normal;
        }

        /// Метка в ключе статистики: «speed 150» у челленджа, пусто у обычной скорости и у тренировки.
        internal static string Variant(int percent)
        {
            return IsChallenge(percent) ? Word + Clamp(percent) : "";
        }

        /// Две метки через « | » (пустые пропускаются): «phase 2 | speed 150».
        internal static string Join(string first, string second)
        {
            if (string.IsNullOrEmpty(first)) return second ?? "";
            if (string.IsNullOrEmpty(second)) return first;
            return first + " | " + second;
        }

        /// Подпись к имени в таблице статистики: « · 150%». Пусто — обычный бой.
        internal static string Suffix(int percent)
        {
            return IsChallenge(percent) ? " · " + Clamp(percent) + "%" : "";
        }

        /// Кнопки «−» и «+»: сдвиг на шаг (с Shift — на большой шаг) в допустимых границах.
        internal static int Nudge(int percent, int direction, bool big)
        {
            int step = big ? BigStep : Step;
            return Clamp(Clamp(percent) + (direction < 0 ? -step : step));
        }

        /// Ключ пресета в статистике: у скоростного челленджа свои числа (попытки, прохождения, лучший результат).
        internal static string PresetKey(string preset, string variant)
        {
            return string.IsNullOrEmpty(variant) ? preset : preset + " | " + variant;
        }

        /// Это ключ скоростного челленджа пресета с таким именем («Мой | speed 150»)? Нужно при переименовании пресета.
        internal static bool IsSpeedKeyOf(string key, string preset)
        {
            return key != null && preset != null && key.StartsWith(preset + " | " + Word, StringComparison.Ordinal);
        }

        /// Это ключ любого челленджа пресета с таким именем: скорости («Мой | speed 150») или сборки («Мой | build NS»)? Нужно при переименовании пресета.
        internal static bool IsVariantKeyOf(string key, string preset)
        {
            return IsSpeedKeyOf(key, preset) || BuildMath.IsBuildKeyOf(key, preset);
        }
    }

    /// Сборка Хорнет (0.25.0): привязки Gods of Pharloom (Needle, Silk, Tools, Mask) и отключённые навыки. Они делают бой тяжелее,
    /// поэтому у такого боя в статистике своя строка (метка «build NS -dash»), как у скоростного челленджа. Чистая арифметика меток:
    /// сами привязки и навыки трогает Build.cs.
    internal static class BuildMath
    {
        internal const string Word = "build ";
        /// Привязки по порядку и их буквы в метке: Needle, Silk, Tools, Mask.
        internal const string Letters = "NSTM";
        internal const int Bindings = 4;
        /// Навыки, которые можно отключить, в порядке показа (он же порядок в метке).
        internal static readonly string[] SkillCodes = { "dash", "claw", "eva", "soar", "grip", "needolin", "strike", "drift", "fay" };

        // ---- числовое снаряжение (0.27.0): игла, маски, шёлк, катушка, сумка, набор ----------------
        internal const int GearNeedle = 0, GearMasks = 1, GearSilk = 2, GearRegen = 3, GearPouch = 4, GearKit = 5, GearCount = 6;

        /// Наименьшее значение: масок не бывает меньше одной, остальное начинается с нуля.
        internal static int GearMin(int gear)
        {
            return gear == GearMasks ? 1 : 0;
        }

        /// Наибольшее значение. Привязка Mask урезает маски до maskCap (у GoP это 5), привязка Silk — шёлк до 9.
        internal static int GearMax(int gear, bool maskBinding, int maskCap, bool silkBinding)
        {
            switch (gear)
            {
                case GearMasks: return maskBinding ? Math.Max(1, Math.Min(10, maskCap)) : 10;
                case GearSilk: return silkBinding ? 9 : 18;
                case GearRegen: return 3;
                default: return 4;   // игла, сумка и набор: 0..4
            }
        }

        /// Шаг по кругу, как нажатие в меню GoP: за верхним значением идёт нижнее. dir > 0 — вперёд (левая кнопка), иначе назад (правая).
        /// Значение вне диапазона (например, 10 масок при привязке) уходит на край.
        internal static int Step(int current, int dir, int min, int max)
        {
            if (max < min) return min;
            if (current < min || current > max) return dir >= 0 ? min : max;
            int next = current + (dir >= 0 ? 1 : -1);
            if (next > max) return min;
            if (next < min) return max;
            return next;
        }

        /// Состояние плащей как у GoP: 0 нет, 1 только странника, 2 только Фейдаун, 3 оба (биты: 1 — странник, 2 — Фейдаун).
        internal static int CloakState(bool drift, bool fay)
        {
            return (drift ? 1 : 0) | (fay ? 2 : 0);
        }

        /// Известный код навыка?
        internal static bool KnownSkill(string code)
        {
            return Array.IndexOf(SkillCodes, code) >= 0;
        }

        /// Список отключённых навыков в виде «dash,grip»: только известные коды, без повторов, в порядке SkillCodes. Всё лишнее отбрасывается.
        internal static string NormalizeSkills(string csv)
        {
            if (string.IsNullOrEmpty(csv)) return "";
            string text = "";
            for (int i = 0; i < SkillCodes.Length; i++)
                if (HasSkill(csv, SkillCodes[i])) text += (text.Length > 0 ? "," : "") + SkillCodes[i];
            return text;
        }

        /// Есть ли код в списке.
        internal static bool HasSkill(string csv, string code)
        {
            if (string.IsNullOrEmpty(csv) || string.IsNullOrEmpty(code)) return false;
            string[] parts = csv.Split(',');
            for (int i = 0; i < parts.Length; i++)
                if (parts[i].Trim() == code) return true;
            return false;
        }

        /// Список после отключения навыка (off = true: код добавляется) или возвращения (off = false: код убирается).
        internal static string WithSkill(string csv, string code, bool off)
        {
            if (!KnownSkill(code)) return NormalizeSkills(csv);
            string text = "";
            for (int i = 0; i < SkillCodes.Length; i++)
            {
                string c = SkillCodes[i];
                bool has = c == code ? off : HasSkill(csv, c);
                if (has) text += (text.Length > 0 ? "," : "") + c;
            }
            return text;
        }

        /// Метка сборки для ключа статистики: «build NS -dash -grip». Пусто, если привязок нет и все навыки на месте.
        internal static string Variant(bool[] bindings, string skillsOff)
        {
            string text = "";
            if (bindings != null)
                for (int i = 0; i < Letters.Length && i < bindings.Length; i++)
                    if (bindings[i]) text += Letters[i];
            string off = NormalizeSkills(skillsOff);
            if (off.Length > 0)
            {
                string[] codes = off.Split(',');
                for (int i = 0; i < codes.Length; i++) text += (text.Length > 0 ? " " : "") + "-" + codes[i];
            }
            return text.Length == 0 ? "" : Word + text;
        }

        /// Подпись к имени в таблице статистики: « · NS -dash». Пусто — обычная сборка.
        internal static string Suffix(string variant)
        {
            return string.IsNullOrEmpty(variant) || variant.Length <= Word.Length || !variant.StartsWith(Word, StringComparison.Ordinal)
                ? "" : " · " + variant.Substring(Word.Length);
        }

        /// Это ключ пресета с меткой сборки сразу после имени («Мой | build NS»)? Ключ вида «Мой | speed 150 | build NS» узнаёт SpeedMath.IsSpeedKeyOf.
        /// Нужно при переименовании пресета.
        internal static bool IsBuildKeyOf(string key, string preset)
        {
            return key != null && preset != null && key.StartsWith(preset + " | " + Word, StringComparison.Ordinal);
        }
    }

    /// Порядок и уровни крестов (0.28.4): у Охотницы три уровня, и во вкладке «Привязки» их можно переключать, а не только надевать высший.
    /// Кресты одной семьи идут подряд, по уровням (низший первым); семьи — в порядке первого появления в списке игры.
    internal static class CrestMath
    {
        internal sealed class Entry
        {
            internal string Name = "", Previous = "";
            internal bool Unlocked;
            internal Entry(string name, string previous, bool unlocked) { Name = name ?? ""; Previous = previous ?? ""; Unlocked = unlocked; }
        }

        /// Открытые кресты по семьям: версии одного креста подряд, низший уровень первым; семьи — в порядке первого появления в списке игры.
        /// level[имя] — номер уровня (1, 2, 3…), levels[имя] — сколько уровней открыто; обе записи есть только у крестов, у которых открыто
        /// больше одной версии (одному кресту номер не нужен). level и levels можно не передавать (null).
        internal static List<List<string>> Families(List<Entry> all, Dictionary<string, int> level, Dictionary<string, int> levels)
        {
            List<List<string>> result = new List<List<string>>();
            if (all == null) return result;
            Dictionary<string, Entry> byName = new Dictionary<string, Entry>();
            for (int i = 0; i < all.Count; i++)
                if (all[i] != null && all[i].Name.Length > 0 && !byName.ContainsKey(all[i].Name)) byName[all[i].Name] = all[i];

            List<string> rootOrder = new List<string>();
            Dictionary<string, List<string>> family = new Dictionary<string, List<string>>();
            Dictionary<string, int> depthOf = new Dictionary<string, int>();
            for (int i = 0; i < all.Count; i++)
            {
                Entry e = all[i];
                if (e == null || !e.Unlocked || e.Name.Length == 0 || depthOf.ContainsKey(e.Name)) continue;
                string root = e.Name;
                int depth = 0;
                for (int guard = 0; guard < 8; guard++)
                {
                    Entry cur;
                    if (!byName.TryGetValue(root, out cur) || cur.Previous.Length == 0 || !byName.ContainsKey(cur.Previous)) break;
                    root = cur.Previous;
                    depth++;
                }
                List<string> members;
                if (!family.TryGetValue(root, out members))
                {
                    members = new List<string>();
                    family[root] = members;
                    rootOrder.Add(root);
                }
                members.Add(e.Name);
                depthOf[e.Name] = depth;
            }
            for (int r = 0; r < rootOrder.Count; r++)
            {
                List<string> members = family[rootOrder[r]];
                // сортировка вставками: устойчивая и без делегатов (семья — до трёх-четырёх крестов)
                for (int a = 1; a < members.Count; a++)
                {
                    string x = members[a];
                    int b = a - 1;
                    while (b >= 0 && depthOf[members[b]] > depthOf[x]) { members[b + 1] = members[b]; b--; }
                    members[b + 1] = x;
                }
                if (members.Count > 1 && level != null && levels != null)
                    for (int m = 0; m < members.Count; m++)
                    {
                        level[members[m]] = depthOf[members[m]] + 1;
                        levels[members[m]] = members.Count;
                    }
                result.Add(members);
            }
            return result;
        }

        /// Имена открытых крестов в порядке показа (все семьи подряд, без разбивки).
        internal static List<string> Order(List<Entry> all, Dictionary<string, int> level, Dictionary<string, int> levels)
        {
            List<string> result = new List<string>();
            List<List<string>> families = Families(all, level, levels);
            for (int i = 0; i < families.Count; i++) result.AddRange(families[i]);
            return result;
        }

        /// Какую версию креста показывать плиткой семьи: надета одна из них — она; иначе выбранная в прошлый раз (picked), если она ещё
        /// открыта; иначе высшая.
        internal static string Representative(List<string> members, string current, string picked)
        {
            if (members == null || members.Count == 0) return "";
            if (!string.IsNullOrEmpty(current) && members.Contains(current)) return current;
            if (!string.IsNullOrEmpty(picked) && members.Contains(picked)) return picked;
            return members[members.Count - 1];
        }

        /// Следующая (dir = 1) или предыдущая (dir = −1) версия по кругу, как у плаща. Одна версия или имени нет в семье — остаётся та же.
        internal static string Step(List<string> members, string from, int dir)
        {
            if (members == null || members.Count == 0) return from ?? "";
            int at = members.IndexOf(from ?? "");
            if (at < 0 || members.Count == 1) return from ?? "";
            int n = members.Count;
            return members[(((at + (dir >= 0 ? 1 : -1)) % n) + n) % n];
        }

        /// Сколько крестов в ряду, когда рядов несколько: ровно, без «7 и 2» (9 крестов — 5 и 4).
        internal static int PerRow(int total, int most)
        {
            if (total <= 0 || most <= 0) return Math.Max(1, most);
            int rows = (total + most - 1) / most;
            return rows > 1 ? (total + rows - 1) / rows : most;
        }
    }
}
