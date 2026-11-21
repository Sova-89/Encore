// ============================================================================
//  MenuStats.cs — таблица статистики в окне (кнопка «Статистика» внизу).
//  Занимает место двух правых колонок. Строки собираются один раз и
//  пересобираются, только когда изменились пресет, статистика или язык.
//  Сами числа считает Stats.cs.
//  Что менять: столбцы — в ColumnTitle и BuildStats; ширины — константы ниже.
// ============================================================================
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Encore
{
    internal sealed partial class Menu
    {
        const float StatsW = Col2W + Gap + Col3W;   // таблица шириной в две колонки
        const float TotalsH = 36f, StatHeadH = 28f;
        const float ResetW = 340f;
        const int StatColumns = 5;
        /// Ширины столбцов с числами: бои, победы, без урона, ударов за бой, смерти.
        static readonly float[] StatColW = { 96f, 104f, 124f, 164f, 104f };
        readonly float[] statColX = new float[StatColumns + 1];   // левые края столбцов в точках экрана

        /// Одна строка таблицы: всё уже превращено в текст.
        sealed class StatRow
        {
            internal string Number, Name, Difficulty;
            internal string[] Cells;
        }

        bool showStats;
        Preset statsFor;              // для какого пресета собраны строки
        int statsRevision = -1, statsSettings = -1, statsSpeed = -1;
        string statsBuild = "";
        bool statsRu, statsHidden;
        string statsTotals = "", statsWorst = "";
        readonly List<StatRow> statRows = new List<StatRow>();
        Vector2 scrollStats;
        float resetArmedUntil;

        /// «Закончить»: перезапуски отменены, идущий сегмент завершён (см. Runner.Stop).
        void StopAttempts()
        {
            status = Runner.Stop();
        }

        /// Кнопка режима: обычный → рестарт при смерти → без урона → обычный.
        void CycleMode()
        {
            Plugin.RestartModeNow = RestartMode.Next(Plugin.RestartModeNow);
            int mode = Plugin.RestartModeNow;
            status = mode == RestartMode.Hitless
                ? L.T("Без урона: получил удар — сегмент начнётся заново.", "Hitless: any hit restarts the segment.")
                : mode == RestartMode.OnDeath
                    ? L.T("Рестарт при смерти: удары не мешают, а после смерти сегмент начнётся заново.", "Restart on death: hits do not matter, the segment restarts after Hornet dies.")
                    : "";
        }

        /// Кнопки скорости игры: ставит значение и объясняет в строке состояния, что оно значит для статистики.
        void ChangeSpeed(int percent)
        {
            Plugin.SpeedPercent = percent;
            int speed = Plugin.SpeedPercent;
            if (GameSpeed.Broken) status = L.T("Скорость игры не работает (сломался перехват, см. журнал): игра идёт как обычно, статистика считается как при 100%.", "Game speed is not working (the hook broke, see the log): the game runs as usual and the stats count it as 100%.");
            else if (!SpeedMath.Changes(speed)) status = L.T("Скорость как в игре.", "Game speed is vanilla.");
            else if (SpeedMath.IsChallenge(speed))
                status = L.T("Скорость " + speed + "%: челлендж — бои идут в статистику отдельной строкой.", "Speed " + speed + "%: a challenge — fights are counted in the stats on their own row.");
            else
                status = L.T("Скорость " + speed + "%: тренировка — бои в статистику не идут.", "Speed " + speed + "%: practice — fights are not counted in the stats.");
        }

        // ---- рисование ---------------------------------------------------------

        void DrawStats(Preset preset)
        {
            Panel(Col2X, StatsW, L.T("Статистика пресета", "Preset statistics"));
            if (preset == null) return;
            EnsureStats(preset);

            float left = Col2X + Inner, innerW = StatsW - 2f * Inner;
            GUI.Label(R(left, Top + 10f, innerW, 28f), preset.Name, Theme.SmallRight);
            GUI.Label(R(left, ListTop, innerW, TotalsH), statsTotals, Theme.EntryText);

            float barY = Top + ColH - Inner - ButtonH;
            DrawStatsBar(left, barY, innerW);

            float headY = ListTop + TotalsH + 4f;
            float listY = headY + StatHeadH;
            Rect view = R(Col2X + 10f, listY, StatsW - 20f, barY - 12f - listY);
            if (preset.Hidden)
            {
                string hidden = L.T("Порядок спрятан — боссы появятся здесь, когда откроешь его («Показать» в порядке боссов).",
                                    "The order is hidden — bosses appear here once you reveal it.");
                GUI.Label(view, hidden, Theme.SmallCenter);
                return;
            }
            if (statRows.Count == 0)
            {
                GUI.Label(view, L.T("В пресете нет боссов.", "The preset has no bosses."), Theme.SmallCenter);
                return;
            }

            float rowH = Theme.Px(EntryRowH);
            float width = BeginList(view, ref scrollStats, statRows.Count * rowH);
            float pad = Theme.Px(12f);
            statColX[StatColumns] = width - Theme.Px(10f);
            for (int c = StatColumns - 1; c >= 0; c--) statColX[c] = statColX[c + 1] - Theme.Px(StatColW[c]);
            float colsX = statColX[0];
            float chipW = Theme.Px(ChipW), chipH = Theme.Px(Theme.ChipH);
            float chipX = colsX - Theme.Px(10f) - chipW, nameX = Theme.Px(48f);

            // рисуем только строки, которые видно
            int first = Mathf.Max(0, Mathf.FloorToInt(scrollStats.y / rowH));
            int last = Mathf.Min(statRows.Count - 1, Mathf.CeilToInt((scrollStats.y + view.height) / rowH));
            for (int i = first; i <= last; i++)
            {
                StatRow row = statRows[i];
                float y = i * rowH;
                if (i % 2 == 1) GUI.DrawTexture(new Rect(0f, y, width, rowH), Theme.Zebra);

                GUI.Label(new Rect(0f, y, Theme.Px(34f), rowH), row.Number, Theme.Number);
                GUI.Label(new Rect(nameX, y, chipX - nameX - Theme.Px(10f), rowH), row.Name, Theme.EntryText);
                Rect chip = new Rect(chipX, y + Mathf.Round((rowH - chipH) / 2f), chipW, chipH);
                GUI.Label(chip, Theme.Caps(row.Difficulty), Theme.Chip(row.Difficulty));
                for (int c = 0; c < StatColumns; c++)
                    GUI.Label(new Rect(statColX[c], y, statColX[c + 1] - statColX[c] - pad, rowH), row.Cells[c], Theme.Number);
            }
            GUI.EndScrollView();

            // заголовки столбцов — над списком, на тех же местах
            float headTop = originY + Theme.Px(headY), headH = Theme.Px(StatHeadH);
            for (int c = 0; c < StatColumns; c++)
            {
                Rect box = new Rect(view.x + statColX[c], headTop, statColX[c + 1] - statColX[c] - pad, headH);
                GUI.Label(box, ColumnTitle(c), Theme.SmallRight);
            }
        }

        /// Низ таблицы: сброс статистики (со вторым нажатием для подтверждения) и «самые трудные боссы».
        void DrawStatsBar(float left, float barY, float innerW)
        {
            bool armed = Time.unscaledTime < resetArmedUntil;
            string resetText = armed ? L.T("Точно стереть всё?", "Erase everything?") : L.T("Сбросить всю статистику", "Reset all statistics");
            if (GUI.Button(R(left, barY, ResetW, ButtonH), Theme.Caps(resetText), armed ? Theme.Danger : Theme.Ghost))
            {
                if (armed)
                {
                    Stats.Clear();
                    resetArmedUntil = 0f;
                    status = L.T("Статистика стёрта.", "Statistics erased.");
                }
                else resetArmedUntil = Time.unscaledTime + DeleteConfirmSec;
            }
            GUI.Label(R(left + ResetW + 16f, barY, innerW - ResetW - 16f, ButtonH), statsWorst, Theme.SmallRight);
        }

        static string ColumnTitle(int column)
        {
            switch (column)
            {
                case 0: return L.T("Бои", "Fights");
                case 1: return L.T("Победы", "Wins");
                case 2: return L.T("Без урона", "Hitless");
                case 3: return L.T("Ударов за бой", "Hits per fight");
                default: return L.T("Смерти", "Deaths");
            }
        }

        // ---- сборка строк ------------------------------------------------------

        void EnsureStats(Preset preset)
        {
            bool ru = L.IsRu;
            bool same = statsFor == preset && statsRevision == Stats.Revision && statsRu == ru && statsHidden == preset.Hidden
                        && statsSettings == PresetStore.PhaseRevision && statsSpeed == Plugin.EffectiveSpeed
                        && string.Equals(statsBuild, Build.Variant(), System.StringComparison.Ordinal);
            if (same) return;
            statsFor = preset;
            statsRevision = Stats.Revision;
            statsSettings = PresetStore.PhaseRevision;
            statsSpeed = Plugin.EffectiveSpeed;
            statsBuild = Build.Variant();
            statsRu = ru;
            statsHidden = preset.Hidden;
            BuildStats(preset);
        }

        void BuildStats(Preset preset)
        {
            statRows.Clear();
            // скорость выше 100% и сборка (привязки, отключённые навыки) — свои числа: другая метка у пресета и у каждого босса
            int speed = Plugin.EffectiveSpeed;
            string build = Build.Variant();
            string speedVariant = SpeedMath.Join(SpeedMath.Variant(speed), build);
            statsTotals = Totals(Stats.FindPreset(SpeedMath.PresetKey(preset.Name, speedVariant)));
            statsWorst = "";
            if (preset.Hidden) return;

            string deadliest = null, roughest = null;
            int mostDeaths = 0;
            float mostHits = 0f;
            int number = 0;
            foreach (Entry e in preset.Entries)
            {
                if (e.IsRest) continue;
                // у челленджа («фаза 2 весь бой») свои цифры: ключ и подпись зависят от настройки босса
                int phase = PhaseLock.All; bool full = false;
                PresetStore.GetPhase(e, out phase, out full);
                BossStats boss = Stats.FindBoss(Stats.BossKey(e.Boss, e.Difficulty, SpeedMath.Join(PhaseLock.Variant(phase, full), speedVariant)));
                string name = Runner.Display(e.Boss) + PhaseLock.Suffix(phase, full) + SpeedMath.Suffix(speed) + BuildMath.Suffix(build);
                var row = new StatRow { Number = Theme.Digits(++number), Name = name, Difficulty = e.Difficulty };
                row.Cells = Cells(boss);
                statRows.Add(row);

                if (boss == null || boss.Fights == 0) continue;
                float average = (float)boss.Hits / boss.Fights;
                if (boss.Deaths > mostDeaths) { mostDeaths = boss.Deaths; deadliest = name; }
                if (average > mostHits) { mostHits = average; roughest = name; }
            }

            if (deadliest != null)
                statsWorst = L.T("Чаще гибнешь: ", "Most deaths: ") + deadliest + " (" + mostDeaths + ")";
            if (roughest != null)
            {
                if (statsWorst.Length > 0) statsWorst += "   ·   ";
                statsWorst += L.T("больше ударов за бой: ", "most hits per fight: ") + roughest + " (" + Decimal(mostHits) + ")";
            }
        }

        static string[] Cells(BossStats boss)
        {
            const string None = "—";
            if (boss == null || boss.Fights == 0) return new[] { "0", None, None, None, None };
            return new[]
            {
                Theme.Digits(boss.Fights),
                Theme.Digits(boss.Wins),
                Theme.Digits(boss.HitlessWins),
                Decimal((float)boss.Hits / boss.Fights),
                Theme.Digits(boss.Deaths),
            };
        }

        static string Totals(PresetStats stats)
        {
            if (stats == null || stats.Attempts == 0)
                return L.T("Этот пресет ещё не запускали.", "This preset has not been started yet.");

            string text = L.T("Попыток: ", "Attempts: ") + stats.Attempts
                        + L.T("   ·   пройдено: ", "   ·   cleared: ") + stats.Clears
                        + L.T("   ·   без урона: ", "   ·   hitless: ") + stats.HitlessClears
                        + L.T("   ·   смертей: ", "   ·   deaths: ") + stats.Deaths;
            if (stats.BestHits >= 0) text += L.T("   ·   лучший результат — ударов: ", "   ·   best run — hits: ") + stats.BestHits;
            return text;
        }

        /// Число с одним знаком после запятой: «1,5» по-русски, «1.5» по-английски.
        static string Decimal(float value)
        {
            string text = value.ToString("0.0", CultureInfo.InvariantCulture);
            return L.IsRu ? text.Replace('.', ',') : text;
        }
    }
}
