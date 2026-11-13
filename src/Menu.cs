// ============================================================================
//  Menu.cs — окно пресетов: открытие, закрытие и действия над пресетами.
//  Само рисование — в MenuDraw.cs, внешний вид (цвета, шрифты) — в Theme.cs.
//  Любая правка сразу пишется в presets.json.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;

namespace Encore
{
    internal sealed partial class Menu
    {
        const float DeleteConfirmSec = 3f;
        const float FadeSec = 0.12f;          // за сколько окно проявляется при открытии
        const int NameMaxLength = 40;
        const string NameField = "encore-name";

        internal bool Open;

        List<string> catalog = new List<string>();
        Vector2 scrollPresets, scrollEntries, scrollCatalog;
        string nameEdit = "";
        Preset nameEditFor;
        Preset deleteArmed;
        float deleteArmedUntil;
        string status = "";
        float openedAt;

        bool cursorWasVisible;
        CursorLockMode cursorWasLocked;
        HeroController blockedHero;

        // ---- открыть / закрыть -------------------------------------------------

        internal void Toggle()
        {
            if (Open) Close(); else Show();
        }

        void Show()
        {
            Open = true;
            status = "";
            openedAt = Time.unscaledTime;
            catalog = Runner.Catalog();
            cursorWasVisible = Cursor.visible;
            cursorWasLocked = Cursor.lockState;

            // Пока окно открыто, Хорнет не реагирует на клавиши.
            blockedHero = HeroController.SilentInstance;
            if (blockedHero != null) blockedHero.AddInputBlocker(this);
        }

        internal void Close()
        {
            if (!Open) return;
            Open = false;
            if (blockedHero != null) blockedHero.RemoveInputBlocker(this);
            blockedHero = null;
            Cursor.visible = cursorWasVisible;
            Cursor.lockState = cursorWasLocked;
        }

        internal void Update()
        {
            // Окно открыли до загрузки сохранения, а Хорнет появилась позже — блокируем её ввод сейчас.
            if (Open && blockedHero == null && HeroController.SilentInstance != null)
            {
                blockedHero = HeroController.SilentInstance;
                blockedHero.AddInputBlocker(this);
            }
        }

        /// Игра прячет курсор каждый кадр — возвращаем его, пока окно открыто.
        internal void KeepCursor()
        {
            if (!Open) return;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        // ---- действия ----------------------------------------------------------

        void ApplyName(Preset preset)
        {
            if (PresetStore.Rename(preset, nameEdit)) status = "";
            else status = L.T("Имя пустое или уже занято", "Name is empty or already used");
            nameEdit = preset.Name;
        }

        void DeleteClicked(Preset preset, bool armed)
        {
            if (armed)
            {
                PresetStore.Delete(preset);
                deleteArmed = null;
            }
            else
            {
                deleteArmed = preset;
                deleteArmedUntil = Time.unscaledTime + DeleteConfirmSec;
            }
        }

        void AddBoss(Preset preset, string boss)
        {
            // Новый босс получает сложность предыдущего босса в пресете.
            string difficulty = Tokens.Attuned;
            foreach (Entry e in preset.Entries) if (!e.IsRest) difficulty = e.Difficulty;
            preset.Entries.Add(new Entry { Boss = boss, Difficulty = difficulty });
            PresetStore.Save();
            scrollEntries.y = 1000000f;   // прокрутить список к новой строке
        }

        static void Swap(Preset preset, int a, int b)
        {
            Entry tmp = preset.Entries[a];
            preset.Entries[a] = preset.Entries[b];
            preset.Entries[b] = tmp;
            PresetStore.Save();
        }

        static void SetAll(Preset preset, string difficulty)
        {
            foreach (Entry e in preset.Entries) e.Difficulty = difficulty;
            PresetStore.Save();
        }

        /// «5 боссов · 1 скамейка» для выбранного пресета.
        static string Summary(Preset preset)
        {
            int bosses = preset.BossCount;
            int benches = preset.Entries.Count - bosses;
            string bossWord = L.T(Plural(bosses, "босс", "босса", "боссов"), bosses == 1 ? "boss" : "bosses");
            string benchWord = L.T(Plural(benches, "скамейка", "скамейки", "скамеек"), benches == 1 ? "bench" : "benches");
            string text = bosses + " " + bossWord;
            if (benches > 0) text += "  ·  " + benches + " " + benchWord;
            return text;
        }

        /// Русская форма слова после числа: 1 босс, 2 босса, 5 боссов.
        static string Plural(int n, string one, string few, string many)
        {
            int last = n % 10, lastTwo = n % 100;
            if (lastTwo >= 11 && lastTwo <= 14) return many;
            if (last == 1) return one;
            if (last >= 2 && last <= 4) return few;
            return many;
        }
    }
}
