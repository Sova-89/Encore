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
        const string RandomField = "encore-random";
        const string SeedField = "encore-seed";

        internal bool Open;

        List<string> catalog = new List<string>();
        Vector2 scrollPresets, scrollEntries, scrollCatalog;
        string nameEdit = "";
        string randomEdit = "";
        string seedEdit = "";
        bool seedTyped;        // в поле сид, который ввёл игрок (а не придумал мод): он повторяется
        Preset nameEditFor;
        Preset deleteArmed;
        float deleteArmedUntil;
        string status = "";
        float openedAt;
        string summaryText;
        int summaryBosses, summaryBenches;
        bool summaryRu;
        readonly TextWidth startTextW = new TextWidth(), startKeyW = new TextWidth();
        readonly TextWidth closeTextW = new TextWidth(), closeKeyW = new TextWidth();

        bool cursorWasVisible;
        CursorLockMode cursorWasLocked;
        HeroController blockedHero;

        // ---- открыть / закрыть -------------------------------------------------

        internal void Toggle()
        {
            if (Open) Close("клавиша"); else Show();
        }

        bool catalogReported;   // время сбора списка боссов пишем в лог один раз

        void Show()
        {
            Open = true;
            status = "";
            openedAt = Time.unscaledTime;
            long catalogStarted = System.Diagnostics.Stopwatch.GetTimestamp();
            catalog = Runner.Catalog();
            if (!catalogReported)
            {
                catalogReported = true;
                Plugin.Log.LogInfo("Окно: список боссов (" + catalog.Count + ") собран за "
                    + Load.Ms(System.Diagnostics.Stopwatch.GetTimestamp() - catalogStarted) + " мс");
            }
            randomEdit = Theme.Digits(PresetStore.Data.RandomCount);
            hpEdit.Clear();
            shpEdit.Clear();   // поля своего здоровья (вкладка «Боссы») показывают то, что в пресете
            seedEdit = PresetStore.Data.RandomSeed;
            seedTyped = PresetStore.Data.RandomSeedTyped;
            cursorWasVisible = Cursor.visible;
            cursorWasLocked = Cursor.lockState;

            // Пока окно открыто, Хорнет не реагирует на клавиши.
            SyncBlocker();
            Plugin.Log.LogInfo("Окно открыто (" + Plugin.MenuKeyName + ", Esc или кнопка «Закрыть» закроют его)"
                + (blockedHero != null ? ", управление Хорнет заблокировано" : ", Хорнет ещё нет"));
        }

        /// Закрыть окно кнопкой.
        internal void Close()
        {
            Close("кнопка");
        }

        internal void Close(string why)
        {
            if (!Open)
            {
                SyncBlocker();   // на всякий случай: окно закрыто — блокировки быть не должно
                return;
            }
            Open = false;
            SyncBlocker();
            Cursor.visible = cursorWasVisible;
            Cursor.lockState = cursorWasLocked;
            Plugin.Log.LogInfo("Окно закрыто (" + why + "), управление Хорнет возвращено");
        }

        /// Держит блокировку ввода в точном соответствии с состоянием окна: открыто — Хорнет заблокирована
        /// (в том числе если она появилась позже или её объект пересоздали), закрыто — блокировки нет совсем.
        void SyncBlocker()
        {
            HeroController wanted = Open ? HeroController.SilentInstance : null;
            if (blockedHero == null) blockedHero = null;       // объект уничтожен — забываем ссылку
            if (blockedHero == wanted) return;
            if (blockedHero != null) blockedHero.RemoveInputBlocker(this);
            blockedHero = wanted;
            if (blockedHero != null) blockedHero.AddInputBlocker(this);
        }

        internal void Update()
        {
            if (Open && Input.GetKeyDown(KeyCode.Escape)) Close("Esc");
            SyncBlocker();
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

        /// Собирает пресет «Случайный»: столько боссов, сколько введено в поле, из выбранного пула, по сиду.
        void RollRandom()
        {
            int count;
            if (!int.TryParse(randomEdit, out count)) count = PresetStore.Data.RandomCount;
            count = Mathf.Clamp(count, PresetStore.RandomMin, PresetStore.RandomMax);
            randomEdit = Theme.Digits(count);

            string pool = PresetStore.Data.RandomPool;
            List<string> source = Pool.Build(pool, catalog);
            if (source.Count == 0)
            {
                status = catalog.Count == 0
                    ? L.T("Список боссов GoP пуст — собирать не из чего", "The GoP boss list is empty, nothing to pick from")
                    : L.T("В пуле нет боссов — собирать не из чего", "The pool has no bosses to pick from");
                return;
            }

            // Сид, который ввёл игрок, повторяется. Пустое поле или сид, который придумал мод, заменяется новым.
            string seed = (seedEdit ?? "").Trim();
            bool typed = seed.Length > 0 && seedTyped;
            if (!typed) seed = Seed.Make();
            seedEdit = seed;
            seedTyped = typed;

            List<string> bosses = Randomizer.Pick(source, count, new Rng(Seed.Hash(seed)));
            Preset preset = PresetStore.SetRandom(bosses, Plugin.RandomBenchEvery, Plugin.RandomHideOrder, pool, seed, typed);
            scrollEntries = Vector2.zero;
            statsFor = null;              // строки статистики соберутся заново под новый набор
            ScrollToSelectedPreset();
            status = L.T("Собран пресет «", "Rolled preset “") + preset.Name + L.T("»: боссов ", "”: bosses ") + count
                     + L.T(" · сид ", " · seed ") + seed;

            // Спрятанный порядок не пишем и в лог: его видно в окне консоли BepInEx. Сид — пишем всегда.
            string rolled = preset.Hidden ? L.T("порядок спрятан", "order hidden") : string.Join(" > ", bosses.ToArray());
            Plugin.Log.LogInfo("Случайный пресет (" + count + ", пул " + Pool.Normalize(pool) + ", сид " + seed
                               + (typed ? ", введён вручную" : "") + "): " + rolled);
        }

        /// Список пресетов прокручивается так, чтобы выбранный оказался вверху видимой части.
        void ScrollToSelectedPreset()
        {
            scrollPresets.y = Mathf.Max(0f, PresetStore.SelectedIndex * Theme.Px(PresetRowH));
        }

        /// Кнопка «Пул»: боссы только Пантеона 5 или все боссы GoP.
        void TogglePool()
        {
            PresetStore.Data.RandomPool = Pool.Toggle(PresetStore.Data.RandomPool);
            PresetStore.Save();
        }

        /// Кладёт пресет строкой в буфер обмена.
        void CopyPreset(Preset preset)
        {
            string code = Share.Encode(preset);
            try
            {
                GUIUtility.systemCopyBuffer = code;
                status = L.T("Строка скопирована — отправь её, друг нажмёт «Вставить»", "String copied — send it, your friend presses Paste");
            }
            catch (System.Exception ex)
            {
                status = L.T("Не удалось скопировать: ", "Could not copy: ") + ex.Message;
            }
        }

        /// Достаёт пресет из строки в буфере обмена и добавляет его в список.
        void PastePreset()
        {
            string clip = null;
            try { clip = GUIUtility.systemCopyBuffer; }
            catch (System.Exception) { }

            Preset incoming;
            string error;
            if (!Share.TryDecode(clip, out incoming, out error)) { status = error; return; }

            int unknown = 0;
            foreach (Entry e in incoming.Entries)
                if (!e.IsRest && !catalog.Contains(e.Boss)) unknown++;

            Preset added = PresetStore.Import(incoming);
            ScrollToSelectedPreset();
            scrollEntries = Vector2.zero;
            status = L.T("Добавлен «", "Added “") + added.Name + L.T("»: боссов ", "”: bosses ") + added.BossCount;
            if (unknown > 0)
                status += L.T(" · нет в GoP: ", " · not in GoP: ") + unknown;
        }

        /// Оставляет в строке только цифры (для поля с числом боссов).
        static string DigitsOnly(string text)
        {
            var digits = new System.Text.StringBuilder();
            foreach (char c in text) if (c >= '0' && c <= '9') digits.Append(c);
            return digits.ToString();
        }

        /// Нажат ли Enter, пока курсор стоит в поле с этим именем. Событие при этом гасится.
        static bool EnterIn(string field)
        {
            Event e = Event.current;
            bool pressed = e.type == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
                           && GUI.GetNameOfFocusedControl() == field;
            if (pressed) e.Use();
            return pressed;
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

        /// «5 боссов · 1 скамейка» для выбранного пресета. Собирается заново, только когда числа изменились.
        string Summary(Preset preset)
        {
            int bosses = preset.BossCount;
            int benches = preset.Entries.Count - bosses;
            bool ru = L.IsRu;
            if (summaryText != null && bosses == summaryBosses && benches == summaryBenches && ru == summaryRu) return summaryText;
            summaryBosses = bosses;
            summaryBenches = benches;
            summaryRu = ru;

            string bossWord = L.T(Plural(bosses, "босс", "босса", "боссов"), bosses == 1 ? "boss" : "bosses");
            string benchWord = L.T(Plural(benches, "скамейка", "скамейки", "скамеек"), benches == 1 ? "bench" : "benches");
            summaryText = bosses + " " + bossWord;
            if (benches > 0) summaryText += "  ·  " + benches + " " + benchWord;
            return summaryText;
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
