// ============================================================================
//  Theme.cs — внешний вид окна: цвета, шрифты, размеры букв, стили.
//  Шрифты берутся у самой игры (Trajan — заголовки, Perpetua — текст).
//  Если их нет в памяти, остаётся обычный шрифт, окно работает как раньше.
//  Что менять: цвета и размеры букв — константы в начале файла.
//  Картинки рамок и значков рисует PaintCore.cs, здесь они только раздаются.
//  0.28.0: окно в стиле игры — тонкая рамка цвета кости, завитки в углах, тающие линии, круглые «гнёзда» (RingOf).
// ============================================================================
using System.Collections.Generic;
using UnityEngine;

namespace Encore
{
    internal static class Theme
    {
        // ---- цвета -------------------------------------------------------------
        static readonly Color TextMain = Rgb(236, 236, 232);
        static readonly Color TextSecond = Rgb(178, 182, 190);
        static readonly Color TextDim = Rgb(122, 126, 136);
        internal static readonly Color Bone = Rgb(214, 205, 188);   // заголовки колонок и узор
        static readonly Color Crimson = Rgb(190, 44, 54);       // кнопка «Старт», выбранный пресет
        static readonly Color CrimsonHover = Rgb(214, 60, 70);
        static readonly Color CrimsonDown = Rgb(160, 34, 44);
        static readonly Color ToastGold = Rgb(240, 205, 120);
        static readonly Color WindowFill = new Color(10 / 255f, 11 / 255f, 14 / 255f, 0.94f);
        internal static readonly Color Attuned = Rgb(214, 224, 238);
        internal static readonly Color Ascended = Rgb(240, 190, 80);
        internal static readonly Color Radiant = Rgb(238, 96, 88);

        // ---- размеры букв (для экрана высотой 1080, дальше растягиваются) ---------
        const float TitleSize = 42f, SubtitleSize = 22f, HeadingSize = 16f;
        const float PresetSize = 23f, EntrySize = 24f, BossSize = 22f, SmallSize = 20f, NumberSize = 21f;
        const float PaneNameSize = 31f, PaneTitleSize = 34f, PaneTextSize = 26f;   // экран привязок: надпись, название, описание
        const float TileNumberSize = 30f;     // число в углу плитки снаряжения (маски, шёлк, сумка)
        const float ButtonSize = 14.5f, SmallButtonSize = 13f, ChipSize = 13f, StartSize = 19f, CloseSize = 16f, HudSize = 22f;
        const float PlainFontScale = 0.84f;   // обычный шрифт на вид крупнее игрового — уменьшаем
        const float CapsHeight = 0.75f;       // высота заглавной буквы Trajan в долях размера шрифта
        const float CapsNudgeMax = 0.25f;     // больше этой доли размера подписи не сдвигаем
        const float FontRetrySec = 2f;        // через сколько искать шрифты игры снова, если не нашлись
        const float FontRetryMaxSec = 30f;    // пауза между поисками растёт до этого предела

        // ---- размеры, общие с Menu ------------------------------------------------
        internal const float RefHeight = 1080f;
        internal const float RefWidth = 1760f;   // окно 1700 + поля: на узком экране всё сжимается
        internal const float ChipH = 28f, IconSize = 30f, ScrollW = 8f, PillH = 36f;
        internal const float CornerSize = 76f;   // угловой завиток окна

        // ---- готовое ---------------------------------------------------------------
        internal static float Scale = 1f;
        internal static int Version;          // растёт при каждой пересборке стилей (по нему сбрасываются запомненные ширины)
        internal static GUISkin Skin;
        internal static bool UseGameFont = true;

        internal static GUIStyle Window, Panel, Title, Subtitle, Heading;
        internal static GUIStyle PresetText, PresetTextSecond, EntryText, EntryTextDim, BossText, BossTextSecond;
        internal static GUIStyle Small, SmallRight, SmallCenter, Number, TileNumber, InfoText;
        internal static GUIStyle PaneName, PaneTitle, PaneText;   // 0.28.1: надпись «Bindings», название и описание справа, как на экране привязок игры
        internal static GUIStyle Ghost, GhostSmall, GhostBig, Danger, Primary, Row, RowSelected, IconButton, Field, Tab, TabActive;
        internal static GUIStyle ChipAttuned, ChipAscended, ChipRadiant;
        internal static GUIStyle StartText, CloseText, KeyHint, KeyHintDim, Hud, Toast, Pill;
        internal static Texture2D Shade, Hairline, Zebra, Accent, Plain, OrnamentLine, OrnamentDiamond;
        internal static Texture2D Taper, TaperV, Corner, SoftDisc;   // 0.28.0: линии, тающие к концам, запасной угловой завиток, мягкая подложка
        static readonly Dictionary<int, Texture2D> rings = new Dictionary<int, Texture2D>();
        internal static Texture2D IconUp, IconDown, IconCross, IconPlus;

        static readonly List<Texture2D> textures = new List<Texture2D>();
        static Font bodyFont, capsFont;
        static bool builtRu, dirty = true, fontsReported;
        static float nextFontLookup, fontRetry = FontRetrySec;
        static readonly Dictionary<string, string> capsCache = new Dictionary<string, string>();
        static readonly string[] digits = new string[100];

        // ---- вход ------------------------------------------------------------------

        /// Точки экрана из единиц макета (1 единица = 1 точка при высоте экрана 1080).
        internal static int Px(float units)
        {
            return Mathf.RoundToInt(units * Scale);
        }

        /// Текст заглавными буквами. Одни и те же подписи спрашиваются каждый кадр — ответы запоминаются.
        internal static string Caps(string text)
        {
            string caps;
            if (capsCache.TryGetValue(text, out caps)) return caps;
            if (capsCache.Count > 256) capsCache.Clear();
            caps = text.ToUpperInvariant();
            capsCache[text] = caps;
            return caps;
        }

        /// Число строкой: для чисел до 99 строка готовится один раз, а не каждый кадр.
        internal static string Digits(int n)
        {
            if (n < 0 || n >= digits.Length) return n.ToString();
            if (digits[n] == null) digits[n] = n.ToString();
            return digits[n];
        }

        internal static GUIStyle Chip(string difficulty)
        {
            if (difficulty == Tokens.Ascended) return ChipAscended;
            if (difficulty == Tokens.Radiant) return ChipRadiant;
            return ChipAttuned;
        }

        /// Тонкое белое кольцо диаметром px точек экрана (красится через GUI.color). Кольца разных размеров готовятся по мере надобности.
        internal static Texture2D RingOf(int px)
        {
            px = Mathf.Max(16, px);
            Texture2D t;
            if (rings.TryGetValue(px, out t) && t != null) return t;
            float thickness = Mathf.Max(1.4f, px * 0.026f);
            t = Make(px, px, PaintCore.Ring(px, thickness, ToRgba(Color.white)));
            rings[px] = t;
            return t;
        }

        internal static void Invalidate()
        {
            dirty = true;
            nextFontLookup = 0f;
            fontRetry = FontRetrySec;
        }

        /// Звать в начале OnGUI: собирает стили заново, если сменились размер экрана, язык или шрифты.
        internal static void Ensure()
        {
            float scale = Mathf.Max(0.5f, Mathf.Min(Screen.height / RefHeight, Screen.width / RefWidth));
            bool ru = L.IsRu;
            if (ru != builtRu) { bodyFont = null; capsFont = null; nextFontLookup = 0f; fontRetry = FontRetrySec; }
            FindFonts(ru);
            if (!dirty && Skin != null && Shade != null && Mathf.Abs(scale - Scale) < 0.001f && ru == builtRu) return;

            Scale = scale;
            builtRu = ru;
            dirty = false;
            long buildStarted = System.Diagnostics.Stopwatch.GetTimestamp();
            try
            {
                Build();
                Plugin.Log.LogInfo("Окно: стили собраны за " + Load.Ms(System.Diagnostics.Stopwatch.GetTimestamp() - buildStarted) + " мс");
            }
            catch (System.Exception problem)
            {
                // Со шрифтами игры не собралось — один раз пробуем с обычным шрифтом.
                if (bodyFont == null && capsFont == null) throw;
                Plugin.Log.LogWarning("Окно со шрифтами игры не собралось (" + problem.Message + ") — беру обычный шрифт.");
                UseGameFont = false;
                bodyFont = null;
                capsFont = null;
                Build();
            }
        }

        // ---- шрифты игры -------------------------------------------------------------

        static void FindFonts(bool ru)
        {
            if (!UseGameFont)
            {
                if (bodyFont != null || capsFont != null) { bodyFont = null; capsFont = null; dirty = true; }
                return;
            }
            if (bodyFont != null && capsFont != null) return;
            if (Time.unscaledTime < nextFontLookup) return;
            // Не нашлись (например, игра на английском, а окно на русском) — ищем всё реже.
            nextFontLookup = Time.unscaledTime + fontRetry;
            fontRetry = Mathf.Min(fontRetry * 2f, FontRetryMaxSec);

            // В русских шрифтах игры есть и латиница; в английских кириллицы нет — для русского их не берём.
            long lookStarted = System.Diagnostics.Stopwatch.GetTimestamp();
            Font[] loaded = Resources.FindObjectsOfTypeAll<Font>();
            long lookTicks = System.Diagnostics.Stopwatch.GetTimestamp() - lookStarted;
            Font body = ru ? Pick(loaded, "Perpetua_ru_edited") : Pick(loaded, "Perpetua", "Perpetua_ru_edited");
            Font caps = ru ? Pick(loaded, "TrajanPro-Bold_ru")
                           : Pick(loaded, "TrajanPro-Bold", "TrajanPro-Bold_ru", "TrajanPro-Regular");
            if (body != bodyFont || caps != capsFont) { bodyFont = body; capsFont = caps; dirty = true; }

            if (!fontsReported && (body != null || Time.unscaledTime > 60f))
            {
                fontsReported = true;
                Plugin.Log.LogInfo("Шрифты окна: текст — " + (body != null ? body.name : "обычный")
                    + ", заголовки — " + (caps != null ? caps.name : "обычный") + " (поиск " + Load.Ms(lookTicks) + " мс)");
            }
        }

        static Font Pick(Font[] loaded, params string[] names)
        {
            foreach (string name in names)
                foreach (Font font in loaded)
                    if (font != null && font.dynamic && font.name == name) return font;
            return null;
        }

        // ---- сборка ------------------------------------------------------------------

        static void Build()
        {
            Version++;
            rings.Clear();
            foreach (Texture2D old in textures) if (old != null) Object.Destroy(old);
            textures.Clear();
            if (Skin != null) Object.Destroy(Skin);

            float bodyK = bodyFont != null ? 1f : PlainFontScale;
            float capsK = capsFont != null ? 1f : PlainFontScale;
            Font caps = capsFont != null ? capsFont : bodyFont;
            bool fakeBold = capsFont == null;
            int line = Mathf.Max(1, Px(1f));
            int r4 = Mathf.Max(2, Px(4f)), r5 = Mathf.Max(2, Px(5f)), r6 = Mathf.Max(2, Px(6f)), r8 = Mathf.Max(3, Px(8f));
            int chipR = Mathf.Max(3, Px(ChipH) / 2), pillR = Mathf.Max(3, Px(PillH) / 2), barR = Mathf.Max(2, Px(ScrollW) / 2);
            Color none = Color.clear;

            // картинки без формы
            Shade = Solid(new Color(0f, 0f, 0f, 0.59f));
            Hairline = Solid(White(0.11f));
            Zebra = Solid(White(0.027f));
            Accent = Solid(Crimson);
            Plain = Solid(Color.white);   // белая заливка: красится через GUI.color (рамки и полоски цвета на плитках снаряжения)
            OrnamentLine = Make(256, 1, PaintCore.FadingLine(256, ToRgba(Alpha(Bone, 0.43f)), 1.5f));
            int diamond = Px(18f);
            OrnamentDiamond = Make(diamond, diamond,
                PaintCore.Diamond(diamond, 7f * Scale, 3f * Scale, Mathf.Max(0.5f, 0.55f * Scale), ToRgba(Bone)));

            Taper = Make(256, 1, PaintCore.Taper(256, ToRgba(Bone), 0.6f));
            TaperV = Make(1, 256, PaintCore.Taper(256, ToRgba(Bone), 0.6f));
            SoftDisc = Make(128, 128, PaintCore.Disc(128, ToRgba(Color.white), 1.1f));
            Corner = Make(Px(CornerSize), Px(CornerSize), PaintCore.Fleur(Px(CornerSize), ToRgba(Bone)));

            int icon = Px(IconSize);
            float arm = 5.5f * Scale, half = Mathf.Max(0.75f, 0.9f * Scale);
            Rgba white = ToRgba(TextMain);
            IconUp = Make(icon, icon, PaintCore.Lines(icon, PaintCore.ArrowUp(arm), half, white));
            IconDown = Make(icon, icon, PaintCore.Lines(icon, PaintCore.ArrowDown(arm), half, white));
            IconCross = Make(icon, icon, PaintCore.Lines(icon, PaintCore.Cross(arm), half, white));
            IconPlus = Make(icon, icon, PaintCore.Lines(icon, PaintCore.Plus(arm), half, white));

            // рамки
            int r3 = Mathf.Max(2, Px(3f));
            Window = Frame(r3, Rounded(r3, WindowFill, Alpha(Bone, 0.34f), line), null, null);
            Panel = Frame(r3, Rounded(r3, new Color(0f, 0f, 0f, 0.24f), White(0.045f), line), null, null);
            Pill = Frame(pillR, Rounded(pillR, new Color(0f, 0f, 0f, 0.6f), White(0.12f), line), null, null);
            Row = Frame(r4, null, Rounded(r4, White(0.043f), none, 0), Rounded(r4, White(0.08f), none, 0));
            RowSelected = Frame(r4, Rounded(r4, White(0.086f), none, 0), Rounded(r4, White(0.11f), none, 0),
                Rounded(r4, White(0.13f), none, 0));
            IconButton = Outlined(r5, line, none, Alpha(Bone, 0.22f), White(0.08f), Alpha(Bone, 0.55f), White(0.14f), Alpha(Bone, 0.75f));
            Primary = Outlined(r6, line, Crimson, White(0.20f), CrimsonHover, White(0.35f), CrimsonDown, White(0.20f));
            GhostBig = Outlined(r6, line, none, Alpha(Bone, 0.32f), White(0.063f), Alpha(Bone, 0.65f), White(0.12f), Alpha(Bone, 0.85f));

            // подписи
            Title = Label(caps, TitleSize * capsK, TextMain, TextAnchor.MiddleCenter, fakeBold);
            Center(Title);
            Subtitle = Label(bodyFont, SubtitleSize * bodyK, TextDim, TextAnchor.MiddleCenter, false);
            Heading = Label(caps, HeadingSize * capsK, Bone, TextAnchor.MiddleLeft, fakeBold);
            Center(Heading);
            PresetText = Label(bodyFont, PresetSize * bodyK, TextMain, TextAnchor.MiddleLeft, false);
            PresetTextSecond = Label(bodyFont, PresetSize * bodyK, TextSecond, TextAnchor.MiddleLeft, false);
            EntryText = Label(bodyFont, EntrySize * bodyK, TextMain, TextAnchor.MiddleLeft, false);
            EntryTextDim = Label(bodyFont, BossSize * bodyK, TextDim, TextAnchor.MiddleLeft, false);
            BossText = Label(bodyFont, BossSize * bodyK, TextMain, TextAnchor.MiddleLeft, false);
            BossTextSecond = Label(bodyFont, BossSize * bodyK, TextSecond, TextAnchor.MiddleLeft, false);
            Small = Label(bodyFont, SmallSize * bodyK, TextDim, TextAnchor.MiddleLeft, false);
            SmallRight = Label(bodyFont, SmallSize * bodyK, TextDim, TextAnchor.MiddleRight, false);
            SmallCenter = Label(bodyFont, SmallSize * bodyK, TextDim, TextAnchor.MiddleCenter, false);
            Number = Label(bodyFont, NumberSize * bodyK, TextDim, TextAnchor.MiddleRight, false);
            TileNumber = Label(bodyFont, TileNumberSize * bodyK, Color.white, TextAnchor.UpperRight, false);
            InfoText = Label(bodyFont, SmallSize * bodyK, TextDim, TextAnchor.UpperLeft, false);   // описание плитки справа во вкладке «Привязки»
            InfoText.wordWrap = true;
            PaneName = Label(bodyFont, PaneNameSize * bodyK, TextMain, TextAnchor.MiddleLeft, false);
            PaneTitle = Label(bodyFont, PaneTitleSize * bodyK, TextMain, TextAnchor.UpperCenter, false);
            PaneText = Label(bodyFont, PaneTextSize * bodyK, TextMain, TextAnchor.UpperLeft, false);
            PaneText.wordWrap = true;
            StartText = Label(caps, StartSize * capsK, Color.white, TextAnchor.MiddleLeft, fakeBold);
            Center(StartText);
            CloseText = Label(caps, CloseSize * capsK, Rgb(216, 216, 212), TextAnchor.MiddleLeft, fakeBold);
            Center(CloseText);
            KeyHint = Label(bodyFont, SmallSize * bodyK, new Color(1f, 1f, 1f, 0.67f), TextAnchor.MiddleLeft, false);
            KeyHintDim = Label(bodyFont, SmallSize * bodyK, TextDim, TextAnchor.MiddleLeft, false);
            Hud = Label(bodyFont, HudSize * bodyK, new Color(1f, 1f, 1f, 0.92f), TextAnchor.MiddleCenter, false);
            Toast = Label(bodyFont, HudSize * bodyK, ToastGold, TextAnchor.MiddleCenter, false);

            // кнопки с текстом
            Color ghostText = Rgb(216, 216, 212);
            Ghost = Button(caps, ButtonSize * capsK, fakeBold, ghostText, TextMain,
                Outlined(r5, line, none, Alpha(Bone, 0.32f), White(0.063f), Alpha(Bone, 0.65f), White(0.12f), Alpha(Bone, 0.85f)));
            GhostSmall = new GUIStyle(Ghost);
            GhostSmall.fontSize = Mathf.Max(8, Px(SmallButtonSize * capsK));
            // вкладки: невыбранная — только рамка и тусклая подпись, выбранная — залита и с яркой подписью
            // как пункты меню игры: без рамок, выбранная светлая и с тающей чертой снизу (её рисует DrawTabs)
            Tab = Button(caps, SmallButtonSize * capsK, fakeBold, TextDim, TextMain,
                Outlined(r5, line, none, none, White(0.035f), none, White(0.06f), none));
            TabActive = Button(caps, SmallButtonSize * capsK, fakeBold, Bone, Bone,
                Outlined(r5, line, none, none, none, none, none, none));
            Danger = Button(caps, ButtonSize * capsK, fakeBold, Radiant, Radiant, Tinted(r5, line, Radiant));
            ChipAttuned = ChipStyle(caps, capsK, fakeBold, Attuned, chipR, line);
            ChipAscended = ChipStyle(caps, capsK, fakeBold, Ascended, chipR, line);
            ChipRadiant = ChipStyle(caps, capsK, fakeBold, Radiant, chipR, line);

            BuildSkin(r5, barR, line, bodyK);
        }

        /// Своя копия набора стилей игры: в ней шрифт, поле ввода и тонкая полоса прокрутки.
        static void BuildSkin(int fieldR, int barR, int line, float bodyK)
        {
            Skin = Object.Instantiate(GUI.skin);
            Skin.hideFlags = HideFlags.HideAndDontSave;
            Skin.font = bodyFont;
            Skin.settings.cursorColor = TextMain;
            Skin.settings.selectionColor = Alpha(Crimson, 0.6f);

            Field = new GUIStyle();
            Field.font = bodyFont;
            Field.fontSize = Mathf.Max(8, Px(BossSize * bodyK));
            Field.alignment = TextAnchor.MiddleLeft;
            Field.clipping = TextClipping.Clip;
            Field.padding = new RectOffset(Px(12f), Px(12f), 0, 0);
            Field.border = Edge(fieldR);
            Texture2D fieldIdle = Rounded(fieldR, new Color(0f, 0f, 0f, 0.47f), White(0.16f), line);
            Texture2D fieldHover = Rounded(fieldR, new Color(0f, 0f, 0f, 0.47f), White(0.28f), line);
            Texture2D fieldFocus = Rounded(fieldR, new Color(0f, 0f, 0f, 0.60f), White(0.45f), line);
            SetState(Field.normal, fieldIdle, TextMain);
            SetState(Field.hover, fieldHover, TextMain);
            SetState(Field.focused, fieldFocus, TextMain);
            SetState(Field.active, fieldFocus, TextMain);

            GUIStyle bar = Skin.verticalScrollbar;
            Texture2D track = Rounded(barR, White(0.055f), Color.clear, 0);
            AllStates(bar, track);
            bar.fixedWidth = Px(ScrollW);
            bar.fixedHeight = 0f;
            bar.border = Edge(barR);
            bar.margin = new RectOffset(0, 0, 0, 0);
            bar.padding = new RectOffset(0, 0, 0, 0);
            bar.overflow = new RectOffset(0, 0, 0, 0);

            GUIStyle thumb = Skin.verticalScrollbarThumb;
            AllStates(thumb, Rounded(barR, White(0.30f), Color.clear, 0));
            thumb.hover.background = Rounded(barR, White(0.45f), Color.clear, 0);
            thumb.active.background = Rounded(barR, White(0.60f), Color.clear, 0);
            thumb.fixedWidth = Px(ScrollW);
            thumb.fixedHeight = 0f;
            thumb.border = Edge(barR);
            thumb.margin = new RectOffset(0, 0, 0, 0);
            thumb.padding = new RectOffset(0, 0, Px(12f), Px(12f));   // ползунок не короче 24 единиц
            thumb.overflow = new RectOffset(0, 0, 0, 0);

            // стрелки по краям полосы не нужны
            foreach (GUIStyle arrow in new[] { Skin.verticalScrollbarUpButton, Skin.verticalScrollbarDownButton })
            {
                AllStates(arrow, null);
                arrow.fixedHeight = 0f;
                arrow.fixedWidth = 0f;
            }
        }

        // ---- заготовки стилей -------------------------------------------------------

        static GUIStyle Label(Font font, float size, Color color, TextAnchor anchor, bool bold)
        {
            GUIStyle style = new GUIStyle();
            style.font = font;
            style.fontSize = Mathf.Max(8, Px(size));
            style.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            style.alignment = anchor;
            style.clipping = TextClipping.Clip;
            style.wordWrap = false;
            style.richText = false;
            style.normal.textColor = color;
            return style;
        }

        /// Подпись заглавными буквами Trajan ставит точно по середине кнопки.
        /// Игра выравнивает строку целиком (с местом под хвосты букв), а у заглавных хвостов нет —
        /// без поправки они сидят выше середины. Поправка считается по размерам самого шрифта.
        static void Center(GUIStyle style)
        {
            Font font = style.font;
            if (font == null || font != capsFont || font.fontSize <= 0 || font.lineHeight <= 0 || font.ascent <= 0) return;

            float size = style.fontSize;
            float k = size / font.fontSize;
            float lettersMiddle = font.ascent * k - CapsHeight * size / 2f;   // середина заглавной буквы от верха строки
            float shift = font.lineHeight * k / 2f - lettersMiddle;
            shift = Mathf.Clamp(shift, -CapsNudgeMax * size, CapsNudgeMax * size);
            style.contentOffset = new Vector2(0f, Mathf.Round(shift));
        }

        static GUIStyle Frame(int radius, Texture2D idle, Texture2D hover, Texture2D down)
        {
            GUIStyle style = new GUIStyle();
            style.border = Edge(radius);
            style.normal.background = idle;
            style.hover.background = hover;
            style.active.background = down;
            return style;
        }

        /// Рамка в трёх видах: обычная, под мышью, нажатая. Для каждого вида — заливка и цвет рамки.
        static GUIStyle Outlined(int radius, int line, Color fill, Color edge, Color fillHover, Color edgeHover,
                                 Color fillDown, Color edgeDown)
        {
            return Frame(radius, Rounded(radius, fill, edge, line), Rounded(radius, fillHover, edgeHover, line),
                Rounded(radius, fillDown, edgeDown, line));
        }

        /// То же, но одного цвета: бледная, ярче под мышью, ещё ярче нажатая (метки сложности, «Точно?»).
        static GUIStyle Tinted(int radius, int line, Color color)
        {
            return Outlined(radius, line, Alpha(color, 0.10f), Alpha(color, 0.35f), Alpha(color, 0.20f), Alpha(color, 0.70f),
                Alpha(color, 0.30f), Alpha(color, 0.90f));
        }

        /// Кнопка с подписью: к готовой рамке добавляет шрифт и цвета букв.
        static GUIStyle Button(Font font, float size, bool bold, Color text, Color textHover, GUIStyle frame)
        {
            GUIStyle style = Label(font, size, text, TextAnchor.MiddleCenter, bold);
            style.border = frame.border;
            Center(style);
            SetState(style.normal, frame.normal.background, text);
            SetState(style.hover, frame.hover.background, textHover);
            SetState(style.active, frame.active.background, textHover);
            return style;
        }

        static GUIStyle ChipStyle(Font font, float fontK, bool bold, Color color, int radius, int line)
        {
            return Button(font, ChipSize * fontK, bold, color, color, Tinted(radius, line, color));
        }

        static void SetState(GUIStyleState state, Texture2D background, Color text)
        {
            state.background = background;
            state.textColor = text;
        }

        static void AllStates(GUIStyle style, Texture2D background)
        {
            style.normal.background = background;
            style.hover.background = background;
            style.active.background = background;
            style.focused.background = background;
            style.onNormal.background = background;
            style.onHover.background = background;
            style.onActive.background = background;
            style.onFocused.background = background;
        }

        /// Сколько точек по краям картинки не растягивается (углы).
        static RectOffset Edge(int radius)
        {
            return new RectOffset(radius + 1, radius + 1, radius + 1, radius + 1);
        }

        // ---- картинки ---------------------------------------------------------------

        static Texture2D Rounded(int radius, Color fill, Color border, int borderWidth)
        {
            int size = PaintCore.RoundedSize(radius);
            return Make(size, size, PaintCore.Rounded(radius, ToRgba(fill), ToRgba(border), borderWidth));
        }

        static Texture2D Solid(Color color)
        {
            Rgba c = ToRgba(color);
            return Make(1, 1, new[] { (byte)(c.R * 255f), (byte)(c.G * 255f), (byte)(c.B * 255f), (byte)(c.A * 255f) });
        }

        /// Точки приходят строками сверху вниз, а Unity хранит картинку снизу вверх — переворачиваем.
        static Texture2D Make(int width, int height, byte[] topDown)
        {
            Color32[] pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                int from = (height - 1 - y) * width * 4;
                for (int x = 0; x < width; x++)
                {
                    int at = from + x * 4;
                    pixels[y * width + x] = new Color32(topDown[at], topDown[at + 1], topDown[at + 2], topDown[at + 3]);
                }
            }
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            texture.hideFlags = HideFlags.HideAndDontSave;
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            textures.Add(texture);
            return texture;
        }

        // ---- цвета: мелочи -----------------------------------------------------------

        static Color Rgb(int r, int g, int b) { return new Color(r / 255f, g / 255f, b / 255f, 1f); }
        static Color White(float alpha) { return new Color(1f, 1f, 1f, alpha); }
        static Color Alpha(Color color, float alpha) { return new Color(color.r, color.g, color.b, alpha); }
        static Rgba ToRgba(Color color) { return new Rgba(color.r, color.g, color.b, color.a); }
    }
}
