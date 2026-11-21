// ============================================================================
//  SummonHp.cs — здоровье призывов босса (виды — SummonData.HpKinds, значения лежат в Entry.Sh1…Sh3). Не проверено в игре.
//  GoP ставит здоровье призывов из своей таблицы EnemyHp (вороны Отца раков, враги Зверомух, подкрепление Старших братьев, Щепка Сестры Лучины):
//  в тот момент, когда у босса запускается FSM, он обходит своих призывов и берёт для каждого здоровье из строки таблицы (hpFullDict).
//  Мы подменяем эти строки сразу после загрузки комнаты (Arm), до того момента, а когда комната кончилась или сегмент закончился —
//  возвращаем (Restore). Так же GoP читает и таблицу самого босса (BossFx.cs). Копии врагов, которых лимит призывов (Summons.cs) делает
//  из существующих, получают то же здоровье: Unity копирует здоровье вместе с объектом. Бой с таким здоровьем — тренировка.
//  Что менять: новый призыв — запись в SummonData.BuildHp (ключ — имя строки в EnemyHp.enemies).
// ============================================================================
using System;
using System.Collections.Generic;

namespace Encore
{
    internal static class SummonHp
    {
        /// Что мы подменили в таблице GoP: старые значения каждой сложности.
        sealed class Saved
        {
            internal string Key;
            internal EnemyHp Info;
            internal string[] Diffs;
            internal int[] OldFull;
            internal int[] OldPhase;
        }

        static readonly List<Saved> saved = new List<Saved>();
        static string tableScene;

        /// Комната боя загрузилась: если у босса задано здоровье призывов (снимок взят при старте попытки, см. Runner), подменяем строки таблицы GoP.
        internal static void Arm(string bossName, string sceneName, int[] values)
        {
            Restore();
            if (!SummonData.AnyHp(values)) return;
            SummonHpKind[] kinds = SummonData.HpKinds(bossName);
            if (kinds.Length == 0) return;
            try
            {
                int applied = 0;
                for (int k = 0; k < kinds.Length && k < values.Length; k++)
                {
                    if (values[k] <= 0) continue;
                    EnemyHp info;
                    if (!EnemyHp.enemies.TryGetValue(kinds[k].Key, out info) || info.phases == null || info.phases.Length < 1 || info.hpFullDict == null)
                    {
                        Plugin.Log.LogWarning("Босс " + bossName + ": GoP не знает здоровья призыва «" + kinds[k].Key + "» — не применяю.");
                        continue;
                    }
                    var s = new Saved();
                    s.Key = kinds[k].Key;
                    s.Info = info;
                    s.Diffs = new List<string>(info.hpFullDict.Keys).ToArray();
                    s.OldFull = new int[s.Diffs.Length];
                    s.OldPhase = new int[s.Diffs.Length];
                    for (int d = 0; d < s.Diffs.Length; d++)
                    {
                        int oldPhase;
                        s.OldFull[d] = info.hpFullDict[s.Diffs[d]];
                        s.OldPhase[d] = info.phases[0].hpDict.TryGetValue(s.Diffs[d], out oldPhase) ? oldPhase : -1;
                    }
                    for (int d = 0; d < s.Diffs.Length; d++)
                    {
                        info.hpFullDict[s.Diffs[d]] = values[k];
                        if (s.OldPhase[d] >= 0) info.phases[0].hpDict[s.Diffs[d]] = values[k];
                    }
                    saved.Add(s);
                    applied++;
                }
                if (applied == 0) return;
                tableScene = sceneName;
                Runner.MarkPractice();
                string what = SummonData.DescribeHp(bossName, values);
                Plugin.Log.LogInfo("Босс " + bossName + ": здоровье призывов — " + what + " (таблица GoP подменена, бой не идёт в статистику).");
                Plugin.Toast(Runner.Display(bossName) + ": " + L.T("здоровье призывов — ", "summon health - ") + what + L.T(" · тренировка", " · practice"), 4f);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning("Босс " + bossName + ": здоровье призывов не применено: " + ex.Message);
                Restore();
            }
        }

        /// Комната другая — таблица снова обычная.
        internal static void LeaveScene(string newScene)
        {
            if (saved.Count > 0 && !Rooms.Same(newScene, tableScene)) Restore();
        }

        /// Вернуть строки таблицы GoP как были.
        internal static void Restore()
        {
            if (saved.Count == 0) return;
            try
            {
                foreach (Saved s in saved)
                    for (int d = 0; d < s.Diffs.Length; d++)
                    {
                        s.Info.hpFullDict[s.Diffs[d]] = s.OldFull[d];
                        if (s.OldPhase[d] >= 0) s.Info.phases[0].hpDict[s.Diffs[d]] = s.OldPhase[d];
                    }
                Plugin.Log.LogInfo("Здоровье призывов в таблице GoP возвращено.");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning("Здоровье призывов в таблице GoP не возвращено: " + ex.Message);
            }
            saved.Clear();
            tableScene = null;
        }

        /// Здоровье призыва в игре (подсказка в меню). Пока таблица подменена нами — берём сохранённое старое. 0 — не знаем.
        internal static int VanillaHp(string key, string difficulty)
        {
            try
            {
                foreach (Saved s in saved)
                    if (s.Key == key)
                        for (int d = 0; d < s.Diffs.Length; d++) if (s.Diffs[d] == difficulty) return s.OldFull[d];
                EnemyHp info;
                int hp;
                if (key != null && difficulty != null && EnemyHp.enemies.TryGetValue(key, out info) && info.hpFullDict != null && info.hpFullDict.TryGetValue(difficulty, out hp)) return hp;
            }
            catch (Exception) { }
            return 0;
        }
    }
}
