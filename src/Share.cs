// ============================================================================
//  Share.cs — пресет одной строкой: скопировал, отправил, друг вставил.
//  Без Unity (проверяется тестами вне игры); буфер обмена трогает только Menu.cs.
//  Вид строки:  ENC1.<контрольная сумма из 4 знаков>.<данные в base64url>   (или ENC2, см. ниже)
//  Данные — текст в UTF-8: имя, «H» (порядок спрятан) или «-», затем по строке на запись:
//  цифра сложности (0 Attuned, 1 Ascended, 2 Radiant) и имя босса (или RestScene для скамейки).
//  ENC2 — то же, но перед именем ещё два знака: цифра зафиксированной фазы босса (0 — как в игре)
//  и 1/0 — полное здоровье (челлендж). Мод пишет ENC2, только если у какого-то босса есть такая настройка,
//  иначе ENC1: такие строки читает и 0.14 и старше.
//  ENC3 — то же, что ENC2, плюс ещё два знака: лимиты первого и второго вида призыва (0 — как в игре, иначе «лимит+1»,
//  см. SummonData.cs). Пишется, только если лимит стоит хоть у одного босса.
//  ENC4 — то же, что ENC3, плюс ещё четыре цифры: своё здоровье босса (0000 — как в игре, иначе число 1…9999). Пишется, только если своё здоровье
//  стоит хоть у одного босса.
//  ENC5 — то же, что ENC4, плюс ещё три раза по четыре цифры: здоровье первого, второго и третьего вида призыва босса (0000 — как в игре,
//  см. SummonData.HpKinds). Пишется, только если здоровье призывов стоит хоть у одного босса.
//  ENC6 — то же, что ENC5, плюс ещё один знак: 1 — босс бессмертен, 0 — как в игре (Immortal.cs). Пишется, только если бессмертие стоит хоть у одного босса.
//  ENC7 — то же, что ENC6, плюс ещё четыре раза по три цифры: проценты первого, второго и третьего вида призыва и шанс самого призыва
//  (000 — как в игре, иначе «процент+1», см. SummonData.Chance). Пишется, только если шансы стоят хоть у одного босса.
//  ENC8 — то же, что ENC7, плюс ещё семь цифр: маска выключенных атак босса (0000000 — все включены, см. AttackData.cs). Пишется, только если
//  выключена хоть одна атака хоть у одного босса.
//  Мод читает все восемь версий.
//  Что менять: поменяешь формат — подними номер (ENC9), старые моды будут отвергать её понятной ошибкой.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Text;

namespace Encore
{
    internal static class Share
    {
        const string Magic = "ENC";
        const int PlainVersion = 1;    // имя босса и сложность
        const int PhaseVersion = 2;    // + фаза и полное здоровье у каждой записи
        const int SummonVersion = 3;   // + лимиты призывов у каждой записи
        const int HpVersion = 4;       // + своё здоровье босса у каждой записи (четыре цифры)
        const int SummonHpVersion = 5; // + здоровье призывов у каждой записи (три раза по четыре цифры)
        const int ImmortalVersion = 6; // + бессмертие у каждой записи (один знак 1/0)
        const int ChanceVersion = 7;   // + шансы призывов у каждой записи (четыре раза по три цифры)
        const int AttackVersion = 8;   // + маска выключенных атак у каждой записи (семь цифр)
        const int NameMax = 40;        // как в окне
        const int EntriesMax = 300;    // защита от мусора в буфере
        const int CodeMax = 30000;     // самая длинная строка, которую вообще разбираем
        const int BossMax = 80;        // самое длинное имя босса

        internal static string Encode(Preset preset)
        {
            // Выключенные атаки есть хоть у одного босса — ENC8; шансы призывов — ENC7; бессмертие — ENC6; здоровье призывов — ENC5; своё здоровье — ENC4; лимиты призывов — ENC3; настройки фаз — ENC2; иначе прежний ENC1 (его прочитает и старый мод).
            int version = PlainVersion;
            foreach (Entry e in preset.Entries)
            {
                if (e.IsRest) continue;
                int need = e.HasAttacks ? AttackVersion : e.HasChance ? ChanceVersion : e.HasImmortal ? ImmortalVersion : e.HasSummonHp ? SummonHpVersion : e.HasHp ? HpVersion : e.HasSummons ? SummonVersion : (e.Phase != 0 || e.Full) ? PhaseVersion : PlainVersion;
                if (need > version) version = need;
            }

            var text = new StringBuilder();
            text.Append(Clean(preset.Name)).Append('\n');
            text.Append(preset.Hidden ? "H" : "-");
            foreach (Entry e in preset.Entries)
            {
                int d = Array.IndexOf(Tokens.Difficulties, e.Difficulty);
                text.Append('\n').Append(d < 0 ? 0 : d);
                if (version >= PhaseVersion)
                {
                    int phase = e.IsRest ? 0 : Math.Max(0, Math.Min(9, e.Phase));
                    text.Append(phase).Append(!e.IsRest && e.Full ? '1' : '0');
                }
                if (version >= SummonVersion)
                {
                    text.Append(e.IsRest ? 0 : Math.Max(0, Math.Min(9, e.Sum1)));
                    text.Append(e.IsRest ? 0 : Math.Max(0, Math.Min(9, e.Sum2)));
                }
                if (version >= HpVersion)
                    text.Append((e.IsRest ? 0 : Math.Max(0, Math.Min(PhaseLock.HpMax, e.Hp))).ToString("0000"));
                if (version >= SummonHpVersion)
                {
                    text.Append((e.IsRest ? 0 : Math.Max(0, Math.Min(PhaseLock.HpMax, e.Sh1))).ToString("0000"));
                    text.Append((e.IsRest ? 0 : Math.Max(0, Math.Min(PhaseLock.HpMax, e.Sh2))).ToString("0000"));
                    text.Append((e.IsRest ? 0 : Math.Max(0, Math.Min(PhaseLock.HpMax, e.Sh3))).ToString("0000"));
                }
                if (version >= ImmortalVersion)
                    text.Append(!e.IsRest && e.Imm ? '1' : '0');
                if (version >= ChanceVersion)
                {
                    text.Append(Three(e, e.Sc1)).Append(Three(e, e.Sc2)).Append(Three(e, e.Sc3)).Append(Three(e, e.Sp));
                }
                if (version >= AttackVersion)
                    text.Append((e.IsRest ? 0 : Math.Max(0, Math.Min(MaskMax, e.At))).ToString("0000000"));
                text.Append(Clean(e.Boss));
            }
            byte[] raw = Encoding.UTF8.GetBytes(text.ToString());
            return Magic + version + "." + Checksum(raw) + "." + ToBase64Url(raw);
        }

        /// Наибольшая маска атак, которая влезает в семь цифр (и в AttackData.Max атак).
        const int MaskMax = (1 << AttackData.Max) - 1;

        /// Три цифры: 000 — как в игре, иначе «процент+1» (не больше 101).
        static string Three(Entry e, int stored)
        {
            return (e.IsRest ? 0 : Math.Max(0, Math.Min(101, stored))).ToString("000");
        }

        /// Достаёт пресет из текста (в нём может быть что угодно вокруг строки). error — что сказать игроку.
        internal static bool TryDecode(string text, out Preset preset, out string error)
        {
            preset = null;
            string code = Find(text, out error);
            if (code == null) return false;

            string[] parts = code.Split('.');
            int version = parts[0].Length == Magic.Length + 1 ? parts[0][Magic.Length] - '0' : 0;
            int prefix = version == AttackVersion ? 41 : version == ChanceVersion ? 34 : version == ImmortalVersion ? 22 : version == SummonHpVersion ? 21 : version == HpVersion ? 9 : version == SummonVersion ? 5 : version == PhaseVersion ? 3 : 1;   // знаков перед именем босса: сложность [, фаза, полное здоровье [, лимиты призывов x2 [, здоровье x4 [, здоровье призывов 3 x 4 [, бессмертие [, шансы 4 x 3 [, маска атак 7]]]]]]]
            if (code.Length > CodeMax || parts.Length != 3 || parts[1].Length != 4 || parts[2].Length == 0)
            {
                error = Broken();
                return false;
            }

            byte[] raw;
            try { raw = FromBase64Url(parts[2]); }
            catch (FormatException) { error = Broken(); return false; }
            if (Checksum(raw) != parts[1]) { error = Broken(); return false; }

            string[] lines = Encoding.UTF8.GetString(raw).Split('\n');
            if (lines.Length < 2) { error = Broken(); return false; }

            var result = new Preset { Name = lines[0].Trim(), Hidden = lines[1] == "H" };
            if (result.Name.Length > NameMax) result.Name = result.Name.Substring(0, NameMax);

            for (int i = 2; i < lines.Length; i++)
            {
                string line = lines[i];
                if (line.Length <= prefix) continue;
                int d = line[0] - '0';
                if (d < 0 || d >= Tokens.Difficulties.Length || line.Length > BossMax + prefix) { error = Broken(); return false; }
                int phase = 0, sum1 = 0, sum2 = 0, hp = 0, attacks = 0; bool full = false, immortal = false;
                var chance = new int[4];
                var summonHp = new int[SummonData.HpSlots];
                if (prefix >= 3)
                {
                    phase = line[1] - '0';
                    if (phase < 0 || phase > 9 || (line[2] != '0' && line[2] != '1')) { error = Broken(); return false; }
                    full = line[2] == '1';
                }
                if (prefix >= 5)
                {
                    sum1 = line[3] - '0';
                    sum2 = line[4] - '0';
                    if (sum1 < 0 || sum1 > 9 || sum2 < 0 || sum2 > 9) { error = Broken(); return false; }
                }
                if (prefix >= 9)
                {
                    for (int k = 5; k < 9; k++)
                    {
                        int digit = line[k] - '0';
                        if (digit < 0 || digit > 9) { error = Broken(); return false; }
                        hp = hp * 10 + digit;
                    }
                }
                if (prefix >= 21)
                {
                    for (int slot = 0; slot < SummonData.HpSlots; slot++)
                        for (int k = 9 + slot * 4; k < 13 + slot * 4; k++)
                        {
                            int digit = line[k] - '0';
                            if (digit < 0 || digit > 9) { error = Broken(); return false; }
                            summonHp[slot] = summonHp[slot] * 10 + digit;
                        }
                }
                if (prefix >= 22)
                {
                    if (line[21] != '0' && line[21] != '1') { error = Broken(); return false; }
                    immortal = line[21] == '1';
                }
                if (prefix >= 34)
                {
                    for (int slot = 0; slot < 4; slot++)
                        for (int k = 22 + slot * 3; k < 25 + slot * 3; k++)
                        {
                            int digit = line[k] - '0';
                            if (digit < 0 || digit > 9) { error = Broken(); return false; }
                            chance[slot] = chance[slot] * 10 + digit;
                        }
                }
                if (prefix >= 41)
                {
                    for (int k = 34; k < 41; k++)
                    {
                        int digit = line[k] - '0';
                        if (digit < 0 || digit > 9) { error = Broken(); return false; }
                        attacks = attacks * 10 + digit;
                    }
                }
                if (result.Entries.Count >= EntriesMax) break;
                var entry = new Entry { Boss = line.Substring(prefix), Difficulty = Tokens.Difficulties[d] };
                // фазу берём только если у такого босса она бывает; чужие значения из строки отбрасываются
                if (PhaseLock.Supports(entry.Boss))
                {
                    entry.Phase = PhaseLock.Normalize(entry.Boss, phase);
                    entry.Full = full;
                }
                // своё здоровье — только допустимое для этого босса; с ним «полное здоровье» не бывает
                entry.Hp = PhaseLock.NormalizeHp(entry.Boss, hp);
                if (entry.Hp > 0) entry.Full = false;
                // лимиты призывов — тоже только допустимые для этого босса
                entry.Sum1 = sum1;
                entry.Sum2 = sum2;
                SummonData.Normalize(entry);
                // здоровье призывов — тоже только для видов этого босса
                entry.Sh1 = summonHp[0];
                entry.Sh2 = summonHp[1];
                entry.Sh3 = summonHp[2];
                SummonData.NormalizeHp(entry);
                // шансы призывов — только допустимые для этого босса (проценты видов должны давать в сумме 100)
                entry.Sc1 = chance[0];
                entry.Sc2 = chance[1];
                entry.Sc3 = chance[2];
                entry.Sp = chance[3];
                SummonData.NormalizeChance(entry);
                // выключенные атаки — только из списка этого босса (хоть одна остаётся включённой)
                entry.At = attacks;
                AttackData.Normalize(entry);
                // бессмертие — только у боссов, которым его дают
                entry.Imm = immortal && PhaseLock.ImmortalCapable(entry.Boss);
                result.Entries.Add(entry);
            }
            if (result.BossCount == 0)
            {
                error = L.T("В строке нет ни одного босса", "The string has no bosses");
                return false;
            }
            preset = result;
            return true;
        }

        // ---- поиск строки в тексте --------------------------------------------

        /// Находит в тексте кусок «ENC<число>.…» и проверяет номер версии.
        static string Find(string text, out string error)
        {
            error = L.T("В буфере нет строки пресета Encore (она начинается с ENC)",
                        "No Encore preset string on the clipboard (starts with ENC)");
            if (string.IsNullOrEmpty(text)) return null;

            int from = 0;
            while (true)
            {
                int at = text.IndexOf(Magic, from, StringComparison.Ordinal);
                if (at < 0) return null;
                from = at + Magic.Length;

                int digits = from;
                while (digits < text.Length && text[digits] >= '0' && text[digits] <= '9') digits++;
                if (digits == from || digits >= text.Length || text[digits] != '.') continue;   // «ENCORE» и подобное

                int end = digits;
                while (end < text.Length && IsCodeChar(text[end])) end++;
                string code = text.Substring(at, end - at).TrimEnd('.');   // точка в конце — скорее всего конец предложения

                int version;
                int.TryParse(text.Substring(from, digits - from), out version);
                if (version != PlainVersion && version != PhaseVersion && version != SummonVersion && version != HpVersion && version != SummonHpVersion && version != ImmortalVersion && version != ChanceVersion && version != AttackVersion)
                {
                    error = L.T("Строка другой версии Encore (ENC" + version + ") — обнови мод",
                                "String from another Encore version (ENC" + version + ") — update the mod");
                    return null;
                }
                error = null;
                return code;
            }
        }

        static bool IsCodeChar(char c)
        {
            return (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '-' || c == '_' || c == '.';
        }

        static string Broken()
        {
            return L.T("Строка повреждена или скопирована не целиком", "The string is damaged or was not copied in full");
        }

        // ---- мелочи ------------------------------------------------------------

        /// Перенос строки в имени сломал бы формат.
        static string Clean(string s)
        {
            return (s ?? "").Replace('\r', ' ').Replace('\n', ' ');
        }

        /// Контрольная сумма (FNV-1a 32 бита, свёрнутая до 4 знаков): ловит обрезанную или правленую строку.
        static string Checksum(byte[] data)
        {
            unchecked
            {
                uint h = 2166136261;
                for (int i = 0; i < data.Length; i++)
                {
                    h ^= data[i];
                    h *= 16777619;
                }
                return ((h ^ (h >> 16)) & 0xFFFF).ToString("x4");
            }
        }

        static string ToBase64Url(byte[] data)
        {
            return Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        static byte[] FromBase64Url(string text)
        {
            string s = text.Replace('-', '+').Replace('_', '/');
            switch (s.Length % 4)
            {
                case 2: s += "=="; break;
                case 3: s += "="; break;
                case 1: throw new FormatException();
            }
            return Convert.FromBase64String(s);
        }
    }
}
