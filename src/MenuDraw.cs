// ============================================================================
//  MenuDraw.cs — рисование окна пресетов (управление мышью).
//  Под заголовком четыре вкладки: «Пресеты», «Статистика», «Боссы» и «Привязки».
//  «Пресеты»: три колонки — пресеты | порядок боссов и сложность | список боссов GoP.
//  «Статистика»: слева те же пресеты, справа вместо двух колонок таблица (MenuStats.cs).
//  «Боссы»: одна широкая панель с настройками боссов (MenuBosses.cs).
//  «Привязки»: привязки Gods of Pharloom и отключение навыков, на всю игру (MenuBuild.cs).
//  Справа вверху — скорость игры «-» / значение / «+» (одна на всю игру, см. GameSpeed.cs).
//  Новая вкладка — ещё одна запись в Tabs и ветка в Draw.
//  0.28.0: стиль игры. Тонкая двойная рамка цвета кости, угловые завитки (у игры берётся её собственный, иначе рисуется свой), вкладки без рамок,
//  у колонок уголки и тающая линия с ромбом под заголовком. Низ окна и скорость отодвинуты от углов на FootPad.
//  Все числа — в единицах макета: 1 единица = 1 точка на экране высотой 1080,
//  на другом экране всё растягивается (Theme.Px). Макет: окно 1700 x 968.
//  Что менять: размеры и отступы — константы ниже; цвета и шрифты — Theme.cs.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;

namespace Encore
{
    internal sealed partial class Menu
    {
        // окно
        const float WinW = 1700f, WinH = 968f, Pad = 28f;
        const float Top = 140f;           // где начинаются колонки (выше — заголовок и вкладки)
        // вкладки
        const float TabsY = 104f, TabH = 30f, TabW = 200f, TabGap = 8f;
        const int TabCount = 4;
        const float Foot = 46f;           // высота нижних кнопок
        const float FootPad = 84f;        // поле слева и справа у нижних кнопок и у скорости: в углах окна стоят завитки
        const float RuleInset = 10f;      // где проходит внутренняя тонкая рамка
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
        const float StartW = 290f, CloseW = 230f, HitlessW = 370f, StopW = 210f;
        const float PhaseTagW = 170f;        // ширина подписи «только фаза 2» в строке пресета
        const string HiddenName = "? ? ?";   // что написано вместо имени босса, пока порядок спрятан

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
                DrawTabs();
                DrawSpeed();
                if (showBuild) DrawBuild();
                else if (showBosses) DrawBosses();
                else
                {
                    Preset preset = PresetStore.Selected;
                    DrawPresets(preset);
                    if (showStats) DrawStats(preset);
                    else
                    {
                        DrawEntries(preset);
                        DrawCatalog(preset);
                    }
                }
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
            DrawRule();
            DrawCorners();

            GUI.Label(R(0f, 14f, WinW, 64f), "ENCORE", Theme.Title);
            string subtitle = L.T("свои сегменты боссов для Gods of Pharloom", "your own boss segments for Gods of Pharloom");
            GUI.Label(R(0f, 70f, WinW, 26f), subtitle, Theme.Subtitle);
        }

        /// Внутренняя тонкая рамка окна (вторая линия за внешней).
        void DrawRule()
        {
            float t = Mathf.Max(1f, Theme.Px(1f));
            Rect r = R(RuleInset, RuleInset, WinW - 2f * RuleInset, WinH - 2f * RuleInset);
            Color bone = Theme.Bone;
            GUI.color = new Color(bone.r, bone.g, bone.b, 0.15f * fade);
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, t), Theme.Plain);
            GUI.DrawTexture(new Rect(r.x, r.yMax - t, r.width, t), Theme.Plain);
            GUI.DrawTexture(new Rect(r.x, r.y, t, r.height), Theme.Plain);
            GUI.DrawTexture(new Rect(r.xMax - t, r.y, t, r.height), Theme.Plain);
            Tint(1f);
        }

        /// Завитки в четырёх углах. Берётся завиток самой игры (спрайт overscan_corner_fleur009), пока он не нашёлся — рисованный.
        void DrawCorners()
        {
            Texture2D art = Icons.Get(Icons.ArtKey("overscan_corner_fleur009"));
            Texture2D tex = art != null ? art : Theme.Corner;
            // линии завитка у игры лежат на 0.7 единицы от края картинки, у запасного — на 3.8
            float lineOff = art != null ? 0.7f : 3.8f;
            float w = Theme.CornerSize, h = art != null ? w * 223f / 227f : w;
            float x0 = RuleInset - lineOff, y0 = RuleInset - lineOff;
            Color bone = Theme.Bone;
            GUI.color = new Color(bone.r, bone.g, bone.b, 0.92f * fade);
            Rect tl = R(x0, y0, w, h);
            GUI.DrawTexture(tl, tex);
            GUI.DrawTextureWithTexCoords(R(WinW - x0 - w, y0, w, h), tex, new Rect(1f, 0f, -1f, 1f), true);
            GUI.DrawTextureWithTexCoords(R(x0, WinH - y0 - h, w, h), tex, new Rect(0f, 1f, 1f, -1f), true);
            GUI.DrawTextureWithTexCoords(R(WinW - x0 - w, WinH - y0 - h, w, h), tex, new Rect(1f, 1f, -1f, -1f), true);
            Tint(1f);
        }

        /// Уголки вокруг прямоугольника (длина в единицах окна).
        void Brackets(Rect r, float len)
        {
            float t = Mathf.Max(1f, Theme.Px(1f)), l = Theme.Px(len);
            Color bone = Theme.Bone;
            GUI.color = new Color(bone.r, bone.g, bone.b, 0.55f * fade);
            GUI.DrawTexture(new Rect(r.x, r.y, l, t), Theme.Plain);
            GUI.DrawTexture(new Rect(r.x, r.y, t, l), Theme.Plain);
            GUI.DrawTexture(new Rect(r.xMax - l, r.y, l, t), Theme.Plain);
            GUI.DrawTexture(new Rect(r.xMax - t, r.y, t, l), Theme.Plain);
            GUI.DrawTexture(new Rect(r.x, r.yMax - t, l, t), Theme.Plain);
            GUI.DrawTexture(new Rect(r.x, r.yMax - l, t, l), Theme.Plain);
            GUI.DrawTexture(new Rect(r.xMax - l, r.yMax - t, l, t), Theme.Plain);
            GUI.DrawTexture(new Rect(r.xMax - t, r.yMax - l, t, l), Theme.Plain);
            Tint(1f);
        }

        /// Скорость игры справа вверху: «-», значение (клик — вернуть 100%), «+». С Shift шаг большой. Действует на всю игру (см. GameSpeed).
        void DrawSpeed()
        {
            const float ButtonW = 54f, ValueW = 250f, SpeedGap = 6f, SpeedY = 22f;
            float x = WinW - FootPad - (ButtonW * 2f + ValueW + SpeedGap * 2f);
            int speed = Plugin.SpeedPercent;
            Rect minus = R(x, SpeedY, ButtonW, ButtonH);
            Rect value = R(x + ButtonW + SpeedGap, SpeedY, ValueW, ButtonH);
            Rect plus = R(x + ButtonW + SpeedGap + ValueW + SpeedGap, SpeedY, ButtonW, ButtonH);
            bool big = Event.current != null && Event.current.shift;

            if (GUI.Button(minus, "-", Theme.Ghost)) ChangeSpeed(SpeedMath.Nudge(speed, -1, big));
            string valueText = L.T("Скорость: ", "Speed: ") + speed + "%" + (GameSpeed.Broken ? L.T(" (не работает)", " (not working)") : "");
            if (GUI.Button(value, Theme.Caps(valueText), SpeedMath.IsChallenge(speed) && !GameSpeed.Broken ? Theme.Danger : Theme.Ghost))
                ChangeSpeed(SpeedMath.Normal);
            if (GUI.Button(plus, "+", Theme.Ghost)) ChangeSpeed(SpeedMath.Nudge(speed, 1, big));
        }

        /// Четыре вкладки по центру под заголовком; по бокам от них — прежний узор (ромб и тающая линия).
        void DrawTabs()
        {
            const float LineLength = 420f, DiamondSize = 18f, DiamondGap = 10f, LineGap = 6f;
            float total = TabCount * TabW + (TabCount - 1) * TabGap;
            float left = Mathf.Round((WinW - total) / 2f);
            float right = left + total;
            float mid = TabsY + TabH / 2f;

            // узор
            int thickness = Mathf.Max(1, Theme.Px(1f));
            Rect rightLine = R(right + DiamondGap + DiamondSize + LineGap, mid, LineLength, 0f);
            rightLine.height = thickness;
            Rect leftLine = R(left - DiamondGap - DiamondSize - LineGap - LineLength, mid, LineLength, 0f);
            leftLine.height = thickness;
            GUI.DrawTexture(rightLine, Theme.OrnamentLine);
            GUI.DrawTextureWithTexCoords(leftLine, Theme.OrnamentLine, new Rect(1f, 0f, -1f, 1f), true);
            GUI.DrawTexture(R(left - DiamondGap - DiamondSize, mid - DiamondSize / 2f, DiamondSize, DiamondSize), Theme.OrnamentDiamond);
            GUI.DrawTexture(R(right + DiamondGap, mid - DiamondSize / 2f, DiamondSize, DiamondSize), Theme.OrnamentDiamond);

            // вкладки
            for (int i = 0; i < TabCount; i++)
            {
                bool active = i == 1 ? showStats : i == 2 ? showBosses : i == 3 ? showBuild : !showStats && !showBosses && !showBuild;
                float x = left + i * (TabW + TabGap);
                string text = i == 1 ? L.T("Статистика", "Statistics") : i == 2 ? L.T("Боссы", "Bosses")
                            : i == 3 ? L.T("Привязки", "Bindings") : L.T("Пресеты", "Presets");
                if (GUI.Button(R(x, TabsY, TabW, TabH), Theme.Caps(text), active ? Theme.TabActive : Theme.Tab))
                {
                    showStats = i == 1;
                    showBosses = i == 2;
                    showBuild = i == 3;
                }
                if (active)
                {
                    Color bone = Theme.Bone;
                    GUI.color = new Color(bone.r, bone.g, bone.b, 0.95f * fade);
                    GUI.DrawTexture(R(x + 10f, TabsY + TabH - 2f, TabW - 20f, 2f), Theme.Taper);
                    Tint(1f);
                }
            }
        }

        // ---- колонка «Пресеты» -------------------------------------------------

        void DrawPresets(Preset selected)
        {
            Panel(Col1X, Col1W, L.T("Пресеты", "Presets"));
            List<Preset> presets = PresetStore.Data.Presets;

            // снизу вверх: «вернуть сегменты», сид и пул, случайные боссы, поле имени, подпись, обмен строкой, три кнопки
            float restoreY = Top + ColH - Inner - ButtonH;
            float seedY = restoreY - 12f - ButtonH;
            float randomY = seedY - 12f - ButtonH;
            float fieldY = randomY - 12f - ButtonH;
            float labelY = fieldY - 26f;
            float shareY = labelY - 10f - ButtonH;
            float buttonsY = shareY - 12f - ButtonH;

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
                GUI.Label(countBox, Theme.Digits(presets[i].BossCount), Theme.Number);
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

            DrawShareRow(shareY, PresetStore.Selected);

            // имя выбранного пресета
            selected = PresetStore.Selected;
            if (selected != null)
            {
                if (nameEditFor != selected) { nameEditFor = selected; nameEdit = selected.Name; }
                GUI.Label(R(Col1X + Inner, labelY, Col1W - 2f * Inner, 24f), L.T("Имя пресета", "Preset name"), Theme.Small);

                bool enter = EnterIn(NameField);
                const float OkW = 62f;
                GUI.SetNextControlName(NameField);
                Rect fieldBox = R(Col1X + Inner, fieldY, Col1W - 2f * Inner - OkW - 8f, ButtonH);
                nameEdit = GUI.TextField(fieldBox, nameEdit, NameMaxLength, Theme.Field);
                Rect okBox = R(Col1X + Col1W - Inner - OkW, fieldY, OkW, ButtonH);
                if (GUI.Button(okBox, "OK", Theme.Ghost) || enter) ApplyName(selected);
            }

            DrawRandomRow(randomY);
            DrawSeedRow(seedY);

            string restoreText = Theme.Caps(L.T("Вернуть сегменты Пантеона 5", "Restore Pantheon 5 segments"));
            if (GUI.Button(R(Col1X + Inner, restoreY, Col1W - 2f * Inner, ButtonH), restoreText, Theme.GhostSmall))
                status = L.T("Добавлено пресетов: ", "Presets added: ") + PresetStore.RestoreDefaults();
        }

        /// Строка «Случайных боссов: [10] [Собрать]»: собирает пресет «Случайный» из стольких боссов.
        void DrawRandomRow(float y)
        {
            const float CountW = 60f, RollW = 124f;
            float labelW = Col1W - 2f * Inner - CountW - RollW - 16f;
            GUI.Label(R(Col1X + Inner, y, labelW, ButtonH), L.T("Случайных боссов:", "Random bosses:"), Theme.Small);

            bool enter = EnterIn(RandomField);
            GUI.SetNextControlName(RandomField);
            Rect countBox = R(Col1X + Inner + labelW + 8f, y, CountW, ButtonH);
            string typed = GUI.TextField(countBox, randomEdit, 2, Theme.Field);
            if (typed != randomEdit) randomEdit = DigitsOnly(typed);

            Rect rollBox = R(Col1X + Col1W - Inner - RollW, y, RollW, ButtonH);
            if (GUI.Button(rollBox, Theme.Caps(L.T("Собрать", "Roll")), Theme.Ghost) || enter) RollRandom();
        }

        /// Строка «Сид: [K7M2QX] [Пул: P5]»: сид повторяет бросок, пул — из каких боссов собирать.
        void DrawSeedRow(float y)
        {
            const float LabelW = 50f, PoolW = 132f;
            GUI.Label(R(Col1X + Inner, y, LabelW, ButtonH), L.T("Сид:", "Seed:"), Theme.Small);

            bool enter = EnterIn(SeedField);
            GUI.SetNextControlName(SeedField);
            float fieldW = Col1W - 2f * Inner - LabelW - PoolW - 16f;
            Rect seedBox = R(Col1X + Inner + LabelW + 8f, y, fieldW, ButtonH);
            string typed = GUI.TextField(seedBox, seedEdit, Seed.MaxLength, Theme.Field);
            if (typed != seedEdit) { seedEdit = typed; seedTyped = true; }
            // Подпись в пустом поле, пока ни в одно поле не встал курсор. Без GetNameOfFocusedControl: он каждый кадр создаёт строку.
            if (seedEdit.Length == 0 && GUIUtility.keyboardControl == 0)
                GUI.Label(seedBox, L.T("случайный", "random"), Theme.SmallCenter);
            if (enter) RollRandom();

            bool p5 = Pool.Normalize(PresetStore.Data.RandomPool) == Pool.P5;
            // готовые строки, а не склейка каждый кадр
            string poolText = p5 ? L.T("Пул: P5", "Pool: P5") : L.T("Пул: все", "Pool: all");
            Rect poolBox = R(Col1X + Col1W - Inner - PoolW, y, PoolW, ButtonH);
            if (GUI.Button(poolBox, Theme.Caps(poolText), Theme.GhostSmall)) TogglePool();
        }

        /// Строка «Обмен: [Копировать] [Вставить]»: пресет одной строкой через буфер обмена.
        void DrawShareRow(float y, Preset selected)
        {
            const float LabelW = 70f;
            GUI.Label(R(Col1X + Inner, y, LabelW, ButtonH), L.T("Обмен:", "Share:"), Theme.Small);

            float half = (Col1W - 2f * Inner - LabelW - 16f) / 2f;
            Rect copyBox = R(Col1X + Inner + LabelW + 8f, y, half, ButtonH);
            Rect pasteBox = R(Col1X + Inner + LabelW + 16f + half, y, half, ButtonH);
            GUI.enabled = selected != null && selected.BossCount > 0;
            if (GUI.Button(copyBox, Theme.Caps(L.T("Копировать", "Copy")), Theme.GhostSmall)) CopyPreset(selected);
            GUI.enabled = true;
            if (GUI.Button(pasteBox, Theme.Caps(L.T("Вставить", "Paste")), Theme.GhostSmall)) PastePreset();
        }

        // ---- колонка «Порядок боссов и сложность» ------------------------------

        void DrawEntries(Preset preset)
        {
            Panel(Col2X, Col2W, L.T("Порядок боссов и сложность", "Boss order and difficulty"));
            if (preset == null) return;

            // справа в шапке: сколько боссов, а у случайного пресета ещё кнопка «Показать / Скрыть» порядок
            float summaryRight = Col2X + Col2W - Inner;
            if (preset.Hidden || preset.Id == PresetStore.RandomId)
            {
                const float RevealW = 120f;
                string revealText = preset.Hidden ? L.T("Показать", "Reveal") : L.T("Скрыть", "Hide");
                if (GUI.Button(R(summaryRight - RevealW, Top + 9f, RevealW, 28f), Theme.Caps(revealText), Theme.GhostSmall))
                {
                    preset.Hidden = !preset.Hidden;
                    PresetStore.Save();
                }
                summaryRight -= RevealW + 14f;
            }
            GUI.Label(R(Col2X + Inner, Top + 10f, summaryRight - Col2X - Inner, 28f), Summary(preset), Theme.SmallRight);

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
                    GUI.Label(new Rect(0f, y, Theme.Px(34f), rowH), Theme.Digits(++number), Theme.Number);
                    string name = preset.Hidden ? HiddenName : Runner.Display(e.Boss);
                    float nameW = chipX - nameX - Theme.Px(10f);
                    // настройка фазы (вкладка «Боссы») видна и тут: «только фаза 2». У спрятанного порядка её не показываем
                    int phase; bool full;
                    PresetStore.GetPhase(e, out phase, out full);
                    bool sums = SummonData.Any(SummonData.Limits(e)) || SummonData.AnyHp(SummonData.HpValues(e)) || SummonData.AnyChance(SummonData.ChanceValues(e));
                    int ownHp = PresetStore.GetHp(e);
                    bool immortal = PresetStore.GetImmortal(e);
                    if (!preset.Hidden && (phase > 0 || sums || ownHp > 0 || immortal))
                    {
                        // «только фаза 2», «здоровье 300», «призывы» или несколько подписей через « · »
                        string tag = phase > 0 ? PhaseLock.Describe(phase, full) : "";
                        if (ownHp > 0) tag += (tag.Length > 0 ? " · " : "") + L.T("здоровье ", "health ") + ownHp;
                        if (sums) tag += (tag.Length > 0 ? " · " : "") + L.T("призывы", "summons");
                        if (immortal) tag += (tag.Length > 0 ? " · " : "") + L.T("бессмертие", "immortal");
                        int parts = (phase > 0 ? 1 : 0) + (sums ? 1 : 0) + (ownHp > 0 ? 1 : 0) + (immortal ? 1 : 0);
                        float tagW = Theme.Px(PhaseTagW + (parts - 1) * 90f);
                        GUI.Label(new Rect(chipX - Theme.Px(10f) - tagW, y, tagW, rowH), tag, Theme.SmallRight);
                        nameW -= tagW;
                    }
                    GUI.Label(new Rect(nameX, y, nameW, rowH), name, Theme.EntryText);

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

            // В списке полсотни боссов, а видно меньше двадцати — рисуем только те строки, что на экране.
            int first = Mathf.Max(0, Mathf.FloorToInt(scrollCatalog.y / rowH));
            int last = Mathf.Min(catalog.Count - 1, Mathf.CeilToInt((scrollCatalog.y + view.height) / rowH));
            for (int i = first; i <= last; i++)
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
            float gap = Theme.Px(14f), room = Theme.Px(56f);
            float startTextWidth = startTextW.Of(startText, Theme.StartText);
            float startKeyWidth = startKeyW.Of(Plugin.StartKeyName, Theme.KeyHint);
            float closeTextWidth = closeTextW.Of(closeText, Theme.CloseText);
            float closeKeyWidth = closeKeyW.Of(Plugin.MenuKeyName, Theme.KeyHintDim);
            float startW = Mathf.Max(Theme.Px(StartW), startTextWidth + gap + startKeyWidth + room);
            float closeW = Mathf.Max(Theme.Px(CloseW), closeTextWidth + gap + closeKeyWidth + room);
            Rect start = R(FootPad, y, 0f, Foot);
            start.width = startW;
            Rect close = new Rect(start.xMax + Theme.Px(16f), start.y, closeW, start.height);

            bool startClicked = GUI.Button(start, GUIContent.none, Theme.Primary);
            Pair(start, startText, Theme.StartText, startTextWidth, Plugin.StartKeyName, Theme.KeyHint, startKeyWidth);
            bool closeClicked = GUI.Button(close, GUIContent.none, Theme.GhostBig);
            Pair(close, closeText, Theme.CloseText, closeTextWidth, Plugin.MenuKeyName, Theme.KeyHintDim, closeKeyWidth);

            // справа: режим перезапуска (обычный / рестарт при смерти / без урона)
            Rect hitlessBox = R(WinW - FootPad - HitlessW, y, HitlessW, Foot);
            int mode = Plugin.RestartModeNow;
            string hitlessText = mode == RestartMode.Hitless ? L.T("Режим: без урона", "Mode: hitless")
                               : mode == RestartMode.OnDeath ? L.T("Режим: рестарт при смерти", "Mode: restart on death")
                               : L.T("Режим: обычный", "Mode: normal");
            if (GUI.Button(hitlessBox, Theme.Caps(hitlessText), mode == RestartMode.Hitless ? Theme.Danger : Theme.Ghost)) CycleMode();

            float textX = close.xMax + Theme.Px(24f);

            // «Закончить» — только когда есть что заканчивать: идёт сегмент или ждёт перезапуск после смерти
            if (Runner.Active || Retry.Pending)
            {
                Rect stopBox = new Rect(textX, start.y, Theme.Px(StopW), start.height);
                if (GUI.Button(stopBox, Theme.Caps(L.T("Закончить", "Stop")), Theme.Ghost)) StopAttempts();
                textX = stopBox.xMax + Theme.Px(16f);
            }
            string hint = L.T("Запуск — из Атриума или Зала Богов.", "Start it from the Atrium or the Hall of Gods.");
            Rect hintBox = new Rect(textX, start.y, hitlessBox.x - Theme.Px(16f) - textX, start.height);
            GUI.Label(hintBox, status.Length > 0 ? status : hint, Theme.Small);

            if (startClicked)
            {
                Close("запуск пресета");
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
            Rect box = R(x, Top, w, ColH);
            GUI.Box(box, GUIContent.none, Theme.Panel);
            Brackets(box, 14f);
            GUI.Label(R(x + Inner, Top + 10f, w - 2f * Inner, 28f), Theme.Caps(title), Theme.Heading);

            // под заголовком: линия, тающая к краям, и ромб посередине
            const float Dia = 14f;
            Rect line = R(x + Inner, Top + 42f, w - 2f * Inner, 0f);
            line.height = Mathf.Max(1, Theme.Px(1f));
            Color bone = Theme.Bone;
            GUI.color = new Color(bone.r, bone.g, bone.b, 0.9f * fade);
            GUI.DrawTexture(line, Theme.Taper);
            GUI.DrawTexture(R(x + w / 2f - Dia / 2f, Top + 42f - Dia / 2f + 0.5f, Dia, Dia), Theme.OrnamentDiamond);
            Tint(1f);
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

        /// Две подписи рядом по центру кнопки: название и клавиша (ширины уже измерены).
        void Pair(Rect box, string main, GUIStyle mainStyle, float mainW, string hint, GUIStyle hintStyle, float hintW)
        {
            float gap = Theme.Px(14f);
            float x = box.x + Mathf.Round((box.width - mainW - gap - hintW) / 2f);
            GUI.Label(new Rect(x, box.y, mainW + 2f, box.height), main, mainStyle);
            GUI.Label(new Rect(x + mainW + gap, box.y, hintW + 2f, box.height), hint, hintStyle);
        }

        /// Прозрачность всего, что рисуется дальше (с учётом проявления окна).
        void Tint(float alpha)
        {
            GUI.color = new Color(1f, 1f, 1f, alpha * fade);
        }
    }
}
