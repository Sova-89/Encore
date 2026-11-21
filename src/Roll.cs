// ============================================================================
//  Roll.cs — случайный пресет: сид, из каких боссов выбирать, сам выбор.
//  Без Unity (проверяется тестами вне игры).
//  Сид — любой текст. Из него считается число, из числа — ряд случайных чисел.
//  Одинаковый сид + пул + число боссов дают один и тот же порядок на любом ПК,
//  поэтому им можно меняться: «сид K7M2QX, 10 боссов, пул P5».
//  Что менять: размер пула «P5» — в Defaults (Model.cs); длина сида — Seed.Length.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Text;

namespace Encore
{
    /// Свой генератор (SplitMix64). System.Random не годится: у него ряд зависит от версии .NET / Mono.
    internal sealed class Rng
    {
        ulong state;

        internal Rng(ulong seed) { state = seed; }

        internal ulong NextU64()
        {
            unchecked
            {
                state += 0x9E3779B97F4A7C15UL;
                ulong z = state;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }

        /// Число от 0 до n-1. Хвост, который не делится на n нацело, отбрасывается — иначе малые числа выпадали бы чаще.
        internal int Next(int n)
        {
            if (n <= 1) return 0;
            ulong bound = ulong.MaxValue - ulong.MaxValue % (ulong)n;
            ulong v;
            do { v = NextU64(); } while (v >= bound);
            return (int)(v % (ulong)n);
        }
    }

    internal static class Seed
    {
        /// Знаки для сида, который придумывает мод: без похожих (0/O, 1/I), чтобы его было легко продиктовать.
        const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        internal const int Length = 6;
        internal const int MaxLength = 24;

        /// Новый случайный сид, например K7M2QX.
        internal static string Make()
        {
            byte[] bytes = Guid.NewGuid().ToByteArray();
            var text = new StringBuilder();
            for (int i = 0; i < Length; i++) text.Append(Alphabet[bytes[i] & 31]);
            return text.ToString();
        }

        /// К какому виду сводится ввод: без пробелов по краям и без различия больших и малых букв («k7m2qx» = «K7M2QX»).
        internal static string Normalize(string text)
        {
            return (text ?? "").Trim().ToUpperInvariant();
        }

        /// Текст -> число (FNV-1a, 64 бита, по UTF-8).
        internal static ulong Hash(string text)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(Normalize(text));
            unchecked
            {
                ulong h = 14695981039346656037UL;
                for (int i = 0; i < bytes.Length; i++)
                {
                    h ^= bytes[i];
                    h *= 1099511628211UL;
                }
                return h;
            }
        }
    }

    /// Из каких боссов собирается случайный пресет.
    internal static class Pool
    {
        internal const string P5 = "p5", All = "all";

        internal static string Normalize(string pool) { return pool == All ? All : P5; }

        internal static string Toggle(string pool) { return Normalize(pool) == P5 ? All : P5; }

        /// Список боссов пула. Порядок задан сортировкой по имени, а не порядком в GoP, поэтому один сид
        /// даёт один результат у всех. Пул «P5» — боссы стандартных сегментов, которые есть в GoP.
        internal static List<string> Build(string pool, IList<string> catalog)
        {
            var have = new HashSet<string>(catalog);
            var result = new List<string>();
            if (Normalize(pool) == All)
                result.AddRange(have);
            else
                foreach (string boss in Defaults.P5Bosses())
                    if (have.Contains(boss)) result.Add(boss);
            result.Sort(string.CompareOrdinal);
            return result;
        }
    }

    internal static class Randomizer
    {
        /// Собирает случайный порядок: count боссов из пула по сиду. Пока боссов хватает — без повторов.
        internal static List<string> Roll(string pool, IList<string> catalog, int count, string seed)
        {
            return Pick(Pool.Build(pool, catalog), count, new Rng(Seed.Hash(seed)));
        }

        /// Берёт count боссов из списка в случайном порядке. Если просят больше, чем есть, список идёт
        /// по второму кругу (тот же босс дважды подряд не выпадает). Первые N боссов не зависят от count:
        /// при том же сиде «5 боссов» — начало «10 боссов».
        internal static List<string> Pick(IList<string> pool, int count, Rng rng)
        {
            var result = new List<string>();
            if (pool.Count == 0) return result;

            var round = new List<string>(pool);
            while (result.Count < count)
            {
                Shuffle(round, rng);
                bool repeatsAtSeam = result.Count > 0 && round.Count > 1 && round[0] == result[result.Count - 1];
                if (repeatsAtSeam)
                {
                    string first = round[0];
                    round[0] = round[1];
                    round[1] = first;
                }
                for (int i = 0; i < round.Count && result.Count < count; i++) result.Add(round[i]);
            }
            return result;
        }

        static void Shuffle(List<string> list, Rng rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                string tmp = list[i];
                list[i] = list[j];
                list[j] = tmp;
            }
        }
    }
}
