// ============================================================================
//  MenuDraw.cs — рисование окна пресетов (управление мышью).
//  Три колонки: пресеты | порядок боссов и сложность | список боссов GoP.
//  Все числа — в единицах макета: 1 единица = 1 точка на экране высотой 1080,
//  на другом экране всё растягивается (Theme.Px). Макет: окно 1700 x 900.
//  Что менять: размеры и отступы — константы ниже; цвета и шрифты — Theme.cs.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;

namespace Encore
{
    internal sealed partial class Menu
    {
        // окно
        const float WinW = 1700f, WinH = 900f, Pad = 28f;
        const float Top = 132f;           // где начинаются колонки (выше — заголовок и узор)
        const float Foot = 46f;           // высота нижних кнопок
        // колонки
        const float Col1W = 400f, Col2W = 760f, Gap = 20f;
        const float Col1X = Pad, Col2X = Col1X + Col1W + Gap, Col3X = Col2X + Col2W + Gap;
        const float Col3W = WinW - Pad - Col3X;
        const float ColH = WinH - Top - Pad - Foot - 20f;
        const float Inner = 16f;          // отступ содержимого от края колонки
        const float ListTop = Top + 52f;  // где начинается список в колонке
        // строки и кнопки
        const float PresetRowH = 38f, EntryRowH = 40f, BossRowH = 34f, ButtonH = 38f;
        const float ChipW = 128f, IconGap = 6f;
        const float StartW = 290f, CloseW = 230f;

        float originX, originY, fade;

        // ---- вход --------------------------------------------------------------

        internal void Draw()
        {
            Theme.Ensure();
            KeepCursor();

            GUISkin oldSkin = GUI.skin;
            Color oldColor = GUI.color;
            GUI.skin = Theme.Skin;
            try
            {
                fade = Mathf.Clamp01((Time.unscaledTime - openedAt) / FadeSec);
                Tint(1f);
                originX = Mathf.Round((Screen.width - Theme.Px(WinW)) / 2f);
                originY = Mathf.Round((Screen.height - Theme.Px(WinH)) / 2f);

                DrawFrame();
                Preset preset = PresetStore.Selected;
                DrawPresets(preset);
                DrawEntries(preset);
                DrawCatalog(preset);
                DrawFooter();
            }
            finally
            {
                GUI.skin = oldSkin;
                GUI.color = oldColor;
            }
        }

        void DrawFrame()
        {
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Theme.Shade);
            GUI.Box(R(0f, 0f, WinW, WinH), GUIContent.none, Theme.Window);

            GUI.Label(R(0f, 14f, WinW, 64f), "ENCORE", Theme.Title);
            string subtitle = L.T("свои сегменты боссов для Gods of Pharloom", "your own boss segments for Gods of Pharloom");
            GUI.Label(R(0f, 70f, WinW, 26f), subtitle, Theme.Subtitle);

            // узор: ромб и две тающие линии от него
            const float OrnamentY = 110f, LineLength = 420f, LineGap = 22f;
            float middle = WinW / 2f;
            int thickness = Mathf.Max(1, Theme.Px(1f));
            Rect right = R(middle + LineGap, OrnamentY, LineLength, 0f);
            right.height = thickness;
            Rect left = R(middle - LineGap - LineLength, OrnamentY, LineLength, 0f);
            left.height = thickness;
            GUI.DrawTexture(right, Theme.OrnamentLine);
            GUI.DrawTextureWithTexCoords(left, Theme.OrnamentLine, new Rect(1f, 0f, -1f, 1f), true);
            GUI.DrawTexture(R(middle - 9f, OrnamentY - 9f, 18f, 18f), Theme.OrnamentDiamond);
        }

        // ---- колонка «Пресеты» -------------------------------------------------

        void DrawPresets(Preset selected)
        {
            Panel(Col1X, Col1W, L.T("Пресеты", "Presets"));
            List<Preset> presets = PresetStore.Data.Presets;

            // снизу вверх: «вернуть сегменты», поле имени, подпись, три кнопки
            float restoreY = Top + ColH - Inner - ButtonH;
            float fieldY = restoreY - 12f - ButtonH;
            float labelY = fieldY - 26f;
            float buttonsY = labelY - 10f - ButtonH;

            Rect view = R(Col1X + 10f, ListTop, Col1W - 20f, buttonsY - 12f - ListTop);
            float rowH = Theme.Px(PresetRowH);
            float width = BeginList(view, ref scrollPresets, presets.Count * rowH);
            int clicked = -1;
            for (int i = 0; i < presets.Count; i++)
            {
                bool isSelected = presets[i] == selected;
                float y = i * rowH;
                Rect row = new Rect(0f, y + Theme.Px(2f), width, rowH - Theme.Px(4f));
                if (GUI.Button(row, GUIContent.none, isSelected ? Theme.RowSelected : Theme.Row)) clicked = i;
                if (isSelected) GUI.DrawTexture(new Rect(0f, row.y, Theme.Px(4f), row.height), Theme.Accent);

                float count = Theme.Px(44f), left = Theme.Px(18f);
                GUI.Label(new Rect(left, y, width - left - count, rowH), presets[i].Name,
                    isSelected ? Theme.PresetText : Theme.PresetTextSecond);
                Rect countBox = new Rect(width - count, y, count - Theme.Px(12f), rowH);
                GUI.Label(countBox, presets[i].BossCount.ToString(), Theme.Number);
            }
            GUI.EndScrollView();
            if (clicked >= 0) PresetStore.Select(clicked);

            // Новый / Копия / Удалить
            float third = (Col1W - 2f * Inner - 16f) / 3f;
            Rect newBox = R(Col1X + Inner, buttonsY, third, ButtonH);
            Rect copyBox = R(Col1X + Inner + third + 8f, buttonsY, third, ButtonH);
            Rect deleteBox = R(Col1X + Inner + 2f * (third + 8f), buttonsY, third, ButtonH);
            bool armed = deleteArmed == selected && Time.unscaledTime < deleteArmedUntil;
            string deleteText = armed ? L.T("Точно?", "Sure?") : L.T("Удалить", "Delete");

            if (GUI.Button(newBox, Theme.Caps(L.T("Новый", "New")), Theme.Ghost)) PresetStore.AddEmpty();
            GUI.enabled = selected != null;
            if (GUI.Button(copyBox, Theme.Caps(L.T("Копия", "Copy")), Theme.Ghost)) PresetStore.Duplicate(selected);
            if (GUI.Button(deleteBox, Theme.Caps(deleteText), armed ? Theme.Danger : Theme.Ghost)) DeleteClicked(selected, armed);
            GUI.enabled = true;

            // имя выбранного пресета
            selected = PresetStore.Selected;
            if (selected != null)
            {
                if (nameEditFor != selected) { nameEditFor = selected; nameEdit = selected.Name; }
                GUI.Label(R(Col1X + Inner, labelY, Col1W - 2f * Inner, 24f), L.T("Имя пресета", "Preset name"), Theme.Small);

                Event e = Event.current;
                bool enter = e.type == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
                             && GUI.GetNameOfFocusedControl() == NameField;
                if (enter) e.Use();

                const float OkW = 62f;
                GUI.SetNextControlName(NameField);
                Rect fieldBox = R(Col1X + Inner, fieldY, Col1W - 2f * Inner - OkW - 8f, ButtonH);
                nameEdit = GUI.TextField(fieldBox, nameEdit, NameMaxLength, Theme.Field);
                Rect okBox = R(Col1X + Col1W - Inner - OkW, fieldY, OkW, ButtonH);
                if (GUI.Button(okBox, "OK", Theme.Ghost) || enter) ApplyName(selected);
            }

            string restoreText = Theme.Caps(L.T("Вернуть сегменты Пантеона 5", "Restore Pantheon 5 segments"));
            if (GUI.Button(R(Col1X + Inner, restoreY, Col1W - 2f * Inner, ButtonH), restoreText, Theme.GhostSmall))
                status = L.T("Добавлено пресетов: ", "Presets added: ") + PresetStore.RestoreDefaults();
        }

        // ---- колонка «Порядок боссов и сложность» ------------------------------

        void DrawEntries(Preset preset)
        {
            Panel(Col2X, Col2W, L.T("Порядок боссов и сложность", "Boss order and difficulty"));
            if (preset == null) return;
            GUI.Label(R(Col2X + Inner, Top + 10f, Col2W - 2f * Inner, 28f), Summary(preset), Theme.SmallRight);

            float barY = Top + ColH - Inner - ButtonH;
            Rect view = R(Col2X + 10f, ListTop, Col2W - 20f, barY - 12f - ListTop);
            if (preset.Entries.Count == 0)
            {
                string empty = L.T("Пусто. Добавь боссов из списка справа.", "Empty. Add bosses from the list on the right.");
                GUI.Label(view, empty, Theme.SmallCenter);
            }

            float rowH = Theme.Px(EntryRowH);
            float width = BeginList(view, ref scrollEntries, preset.Entries.Count * rowH);
            float icon = Theme.Px(Theme.IconSize), step = icon + Theme.Px(IconGap);
            float iconTop = Mathf.Round((rowH - icon) / 2f);
            float crossX = width - Theme.Px(6f) - icon, downX = crossX - step, upX = downX - step;
            float chipW = Theme.Px(ChipW), chipH = Theme.Px(Theme.ChipH);
            float chipX = upX - Theme.Px(14f) - chipW, nameX = Theme.Px(48f);

            int moveUp = -1, moveDown = -1, remove = -1, number = 0;
            for (int i = 0; i < preset.Entries.Count; i++)
            {
                Entry e = preset.Entries[i];
                float y = i * rowH;
                if (i % 2 == 1) GUI.DrawTexture(new Rect(0f, y, width, rowH), Theme.Zebra);

                if (e.IsRest)
                    GUI.Label(new Rect(nameX, y, chipX - nameX, rowH), L.T("скамейка", "bench"), Theme.EntryTextDim);
                else
                {
                    GUI.Label(new Rect(0f, y, Theme.Px(34f), rowH), (++number).ToString(), Theme.Number);
                    GUI.Label(new Rect(nameX, y, chipX - nameX - Theme.Px(10f), rowH), Runner.Display(e.Boss), Theme.EntryText);

                    // Нажатие меняет сложность по кругу: Attuned -> Ascended -> Radiant.
                    Rect chip = new Rect(chipX, y + Mathf.Round((rowH - chipH) / 2f), chipW, chipH);
                    if (GUI.Button(chip, Theme.Caps(e.Difficulty), Theme.Chip(e.Difficulty)))
                    {
                        e.Difficulty = Tokens.NextDifficulty(e.Difficulty);
                        PresetStore.Save();
                    }
                }

                bool isLast = i == preset.Entries.Count - 1;
                if (IconButton(new Rect(upX, y + iconTop, icon, icon), Theme.IconUp, i > 0)) moveUp = i;
                if (IconButton(new Rect(downX, y + iconTop, icon, icon), Theme.IconDown, !isLast)) moveDown = i;
                if (IconButton(new Rect(crossX, y + iconTop, icon, icon), Theme.IconCross, true)) remove = i;
            }
            GUI.EndScrollView();

            // Список меняем после цикла, а не посреди него.
            if (moveUp > 0) Swap(preset, moveUp, moveUp - 1);
            if (moveDown >= 0) Swap(preset, moveDown, moveDown + 1);
            if (remove >= 0) { preset.Entries.RemoveAt(remove); PresetStore.Save(); }

            // под списком: скамейка и одна сложность всем сразу
            if (GUI.Button(R(Col2X + Inner, barY, 170f, ButtonH), Theme.Caps(L.T("+ Скамейка", "+ Bench")), Theme.Ghost))
            {
                preset.Entries.Add(new Entry { Boss = Tokens.Rest });
                PresetStore.Save();
                scrollEntries.y = 1000000f;
            }
            float x = Col2X + Col2W - Inner;
            float chipY = barY + (ButtonH - Theme.ChipH) / 2f;
            for (int i = Tokens.Difficulties.Length - 1; i >= 0; i--)
            {
                string difficulty = Tokens.Difficulties[i];
                x -= ChipW;
                Rect chip = R(x, chipY, ChipW, Theme.ChipH);
                if (GUI.Button(chip, Theme.Caps(difficulty), Theme.Chip(difficulty))) SetAll(preset, difficulty);
                x -= 8f;
            }
            GUI.Label(R(x - 220f, barY, 216f, ButtonH), L.T("Всем сразу:", "Set all:"), Theme.SmallRight);
        }

        // ---- колонка «Добавить босса» ------------------------------------------

        void DrawCatalog(Preset preset)
        {
            Panel(Col3X, Col3W, L.T("Добавить босса", "Add a boss"));
            Rect view = R(Col3X + 10f, ListTop, Col3W - 20f, Top + ColH - 12f - ListTop);
            if (catalog.Count == 0)
            {
                string empty = L.T("Список боссов GoP пуст — см. лог BepInEx.", "GoP boss list is empty — see the BepInEx log.");
                GUI.Label(view, empty, Theme.SmallCenter);
                return;
            }

            float rowH = Theme.Px(BossRowH);
            float width = BeginList(view, ref scrollCatalog, catalog.Count * rowH);
            Rect visible = new Rect(scrollCatalog.x, scrollCatalog.y, view.width, view.height);
            Vector2 mouse = Event.current.mousePosition;
            float icon = Theme.Px(Theme.IconSize);
            string add = null;
            for (int i = 0; i < catalog.Count; i++)
            {
                float y = i * rowH;
                Rect row = new Rect(0f, y + Theme.Px(1f), width, rowH - Theme.Px(2f));
                if (GUI.Button(row, GUIContent.none, Theme.Row)) add = catalog[i];

                bool hover = row.Contains(mouse) && visible.Contains(mouse);
                float left = Theme.Px(14f);
                GUI.Label(new Rect(left, y, width - left - icon - Theme.Px(8f), rowH), Runner.Display(catalog[i]),
                    hover ? Theme.BossText : Theme.BossTextSecond);
                Rect plus = new Rect(width - icon - Theme.Px(4f), y + Mathf.Round((rowH - icon) / 2f), icon, icon);
                if (hover) GUI.DrawTexture(plus, Theme.IconPlus);
            }
            GUI.EndScrollView();

            if (add != null && preset != null) AddBoss(preset, add);
        }

        // ---- низ окна ----------------------------------------------------------

        void DrawFooter()
        {
            float y = WinH - Pad - Foot;
            string startText = Theme.Caps(L.T("Старт", "Start")), closeText = Theme.Caps(L.T("Закрыть", "Close"));

            // кнопки шире, если название клавиши длинное
            float room = Theme.Px(56f);
            float startNeeds = PairWidth(startText, Theme.StartText, Plugin.StartKeyName, Theme.KeyHint) + room;
            float closeNeeds = PairWidth(closeText, Theme.CloseText, Plugin.MenuKeyName, Theme.KeyHintDim) + room;
            float startW = Mathf.Max(Theme.Px(StartW), startNeeds);
            float closeW = Mathf.Max(Theme.Px(CloseW), closeNeeds);
            Rect start = R(Pad, y, 0f, Foot);
            start.width = startW;
            Rect close = new Rect(start.xMax + Theme.Px(16f), start.y, closeW, start.height);

            bool startClicked = GUI.Button(start, GUIContent.none, Theme.Primary);
            Pair(start, startText, Theme.StartText, Plugin.StartKeyName, Theme.KeyHint);
            bool closeClicked = GUI.Button(close, GUIContent.none, Theme.GhostBig);
            Pair(close, closeText, Theme.CloseText, Plugin.MenuKeyName, Theme.KeyHintDim);

            float textX = close.xMax + Theme.Px(24f);
            string hint = L.T("Запуск — из Атриума или Зала Богов.", "Start it from the Atrium or the Hall of Gods.");
            Rect hintBox = new Rect(textX, start.y, originX + Theme.Px(WinW - Pad) - textX, start.height);
            GUI.Label(hintBox, status.Length > 0 ? status : hint, Theme.Small);

            if (startClicked)
            {
                Close();
                Plugin.StartSelected();
            }
            else if (closeClicked) Close();
        }

        // ---- части, из которых собрано окно ------------------------------------

        /// Прямоугольник на экране из единиц макета (от левого верхнего угла окна).
        Rect R(float x, float y, float w, float h)
        {
            return new Rect(originX + Theme.Px(x), originY + Theme.Px(y), Theme.Px(w), Theme.Px(h));
        }

        void Panel(float x, float w, string title)
        {
            GUI.Box(R(x, Top, w, ColH), GUIContent.none, Theme.Panel);
            GUI.Label(R(x + Inner, Top + 10f, w - 2f * Inner, 28f), Theme.Caps(title), Theme.Heading);
            Rect line = R(x + Inner, Top + 42f, w - 2f * Inner, 0f);
            line.height = Mathf.Max(1, Theme.Px(1f));
            GUI.DrawTexture(line, Theme.Hairline);
        }

        /// Начинает прокручиваемый список; возвращает ширину строки (уже, если справа есть полоса прокрутки).
        float BeginList(Rect view, ref Vector2 scroll, float contentHeight)
        {
            bool needsBar = contentHeight > view.height;
            float width = view.width - (needsBar ? Theme.Px(Theme.ScrollW + 6f) : 0f);
            scroll = GUI.BeginScrollView(view, scroll, new Rect(0f, 0f, width, contentHeight), false, false,
                GUIStyle.none, Theme.Skin.verticalScrollbar);
            return width;
        }

        /// Квадратная кнопка со значком. Выключенная видна бледно и не нажимается.
        bool IconButton(Rect rect, Texture2D icon, bool enabled)
        {
            bool clicked = false;
            if (enabled) clicked = GUI.Button(rect, GUIContent.none, Theme.IconButton);
            bool hover = enabled && rect.Contains(Event.current.mousePosition);
            Tint(!enabled ? 0.22f : (hover ? 1f : 0.62f));
            GUI.DrawTexture(rect, icon);
            Tint(1f);
            return clicked;
        }

        /// Две подписи рядом по центру кнопки: название и клавиша.
        void Pair(Rect box, string main, GUIStyle mainStyle, string hint, GUIStyle hintStyle)
        {
            float mainW = mainStyle.CalcSize(new GUIContent(main)).x, hintW = hintStyle.CalcSize(new GUIContent(hint)).x;
            float x = box.x + Mathf.Round((box.width - mainW - Theme.Px(14f) - hintW) / 2f);
            GUI.Label(new Rect(x, box.y, mainW + 2f, box.height), main, mainStyle);
            GUI.Label(new Rect(x + mainW + Theme.Px(14f), box.y, hintW + 2f, box.height), hint, hintStyle);
        }

        float PairWidth(string main, GUIStyle mainStyle, string hint, GUIStyle hintStyle)
        {
            return mainStyle.CalcSize(new GUIContent(main)).x + Theme.Px(14f) + hintStyle.CalcSize(new GUIContent(hint)).x;
        }

        /// Прозрачность всего, что рисуется дальше (с учётом проявления окна).
        void Tint(float alpha)
        {
            GUI.color = new Color(1f, 1f, 1f, alpha * fade);
        }
    }
}
