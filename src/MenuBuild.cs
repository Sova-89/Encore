// ============================================================================
//  MenuBuild.cs — вкладка «Привязки»: копия экрана привязок Gods of Pharloom (0.27.0; 0.28.0: в стиле игры; 0.28.1: раскладка и рамка как у самой игры).
//  Места взяты со скриншота настоящего экрана GoP (1917 x 1198) и пересчитаны в единицы окна: слева игла-посох, красный орб, значок Ударной иглы,
//  маска (число) и катушка (число), сумка и набор (числа), красная шапка (плащи), под ними катушка в кольце из шести навыков, под катушкой
//  жемчужины сердец шёлка; справа от тающей линии — четыре привязки, «без креста», «проклятая», кресты, слоты креста и выбор инструмента;
//  справа от второй линии — название и описание того, над чем мышь. Рамка — четыре шипастых угла из коллекции спрайтов tk2d самой игры
//  (Border_Fleur_*); не нашлись — запасные рисованные завитки. Подписей на самих картинках нет: число — рядом, состояние — яркостью.
//  Левая кнопка: включить / дальше по кругу / надеть. Правая: назад по кругу (числа и плащи) или очистить слот (слот креста).
//  Всё действует на всю игру, не на пресет, и меняется только вне сегмента (так же, как в самом GoP). Привязки и навыки идут в
//  статистику отдельной строкой («NS -dash»), числа и кресты — нет (так же и у GoP). Сами переключения — Build.cs, картинки — Icons.cs.
//  Что менять: места и размеры — константы и числа в DrawGear / DrawSpool / DrawBindings (числа в них — координаты на скриншоте игры).
// ============================================================================
using System.Collections.Generic;
using UnityEngine;

namespace Encore
{
    internal sealed partial class Menu
    {
        bool showBuild;

        /// Нужны ли сейчас картинки (Update делает их из спрайтов; в OnGUI это нельзя). Нужны всему окну: завиток в углу виден на любой вкладке.
        internal bool WantsIcons { get { return true; } }

        // рамка и места (единицы окна 1700 x 968). Числа «rs» ниже — пиксели скриншота экрана привязок игры; RX / RM / RY переводят их в окно.
        const float FrameX0 = 40f, FrameY0 = 142f, FrameX1 = 1660f, FrameY1 = 888f;
        const float RefK = 0.862f;               // во сколько раз картинки на нашем экране меньше, чем на скриншоте
        const float FrameK = 0.829f;             // и куски рамки (у игры они чуть мельче своих пикселей)
        const float LeftAt = 70f, MidAt = 956f;  // где в окне стоит левый край скриншота (x = 110) и середина средней колонки (x = 1050)
        const float DivX1 = 650f, DivX2 = 1262f; // тающие линии между колонками
        const float InfoX = 1286f, InfoW = 352f; // правая колонка: название, описание, кнопки

        static float RX(float rs) { return LeftAt + (rs - 110f) * RefK; }     // левая колонка
        static float RM(float rs) { return MidAt + (rs - 1050f) * RefK; }     // средняя колонка
        static float RY(float rs) { return FrameY0 + (rs - 160f) * RefK; }

        const float BindD = 92f;
        const float CrestD = 66f, SlotD = 70f, ToolD = 52f, TileGap = 8f;
        const float BuildBtnGap = 10f;

        // что под мышью в этом кадре
        const int KindBinding = 1, KindSkill = 2, KindGear = 3, KindCloak = 4, KindSpecial = 5, KindCrest = 6, KindSlot = 7, KindTool = 8;
        int hoverKind, hoverIndex;
        int selKind = KindBinding, selIndex;     // что выбрано (последнее, над чем была мышь): как курсор в игре, остаётся, когда мышь ушла

        // как рисуется картинка: в кольце, на подложке из игры или без ничего (знак слота рисуется отдельно)
        const int LookRing = 0, LookBoard = 1, LookBare = 2;

        // выбор инструмента в слот: какой слот открыт (-1 — никакой), на каком кресте, список подходящих инструментов
        int pickSlot = -1;
        string pickCrest = "";
        List<ToolItem> pickList = new List<ToolItem>();
        ToolItemType pickType;
        float pickAt = -10f;
        bool pickHas;

        /// Цвет слота по его типу (как цвета инструментов в игре).
        static Color SlotColor(ToolItemType type)
        {
            switch (type)
            {
                case ToolItemType.Red: return new Color(0.93f, 0.36f, 0.30f);
                case ToolItemType.Blue: return new Color(0.40f, 0.62f, 1f);
                case ToolItemType.Yellow: return new Color(0.96f, 0.80f, 0.30f);
                default: return new Color(0.84f, 0.78f, 0.96f);   // навыковый слот
            }
        }

        /// Имя знака слота в игре по типу (картинки UI_tool_slot_*).
        static string SlotSigil(ToolItemType type)
        {
            switch (type)
            {
                case ToolItemType.Red: return "UI_tool_slot_attack0000";
                case ToolItemType.Blue: return "UI_tool_slot_defend0000";
                case ToolItemType.Yellow: return "UI_tool_slot_explore0000";
                default: return "UI_tool_slot_weave0000";
            }
        }

        /// Прямоугольник с серединой в (cx, cy).
        Rect Mid(float cx, float cy, float w, float h)
        {
            return R(cx - w / 2f, cy - h / 2f, w, h);
        }

        void DrawBuild()
        {
            hoverKind = 0;
            DrawThornFrame();
            GUI.Label(R(70f, FrameY0 + 4f, 240f, 40f), L.T("Привязки", "Bindings"), Theme.PaneName);

            string reason;
            bool can = Build.CanChange(out reason);
            // сверху между кусками рамки: почему сейчас нельзя менять
            if (!can) GUI.Label(R(730f, FrameY0 + 6f, 400f, 28f), reason, Theme.SmallCenter);

            float dy = RY(262f), dh = (940f - 262f) * RefK;
            Divider(DivX1, dy, dh);
            Divider(DivX2, dy, dh);

            DrawGear(can);
            DrawBindings(can);
            DrawCrests(DivX1 + 24f, 474f, DivX2 - DivX1 - 48f, can);
            if (hoverKind != 0) { selKind = hoverKind; selIndex = hoverIndex; }
            DrawInfo(can);
        }

        /// Шипастая рамка: четыре угла игры (верхний левый — свой, остальные — «Corner»; правый верхний — нижний левый, повёрнутый на пол-оборота).
        void DrawThornFrame()
        {
            Texture2D tl = Icons.Get(Icons.TkKey("Border_Fleur_top_corner0007"));
            Texture2D bl = Icons.Get(Icons.TkKey("Border_Fleur_corner0008"));
            Texture2D br = Icons.Get(Icons.TkKey("Border_Fleur_corner0007"));
            if (tl != null && bl != null && br != null)
            {
                float k = FrameK;
                float bw = bl.width * k, bh = bl.height * k;
                GUI.color = new Color(1f, 1f, 1f, 0.96f * fade);
                GUI.DrawTexture(R(FrameX0, FrameY0, tl.width * k, tl.height * k), tl);
                GUI.DrawTextureWithTexCoords(R(FrameX1 - bw, FrameY0, bw, bh), bl, new Rect(1f, 1f, -1f, -1f), true);
                GUI.DrawTexture(R(FrameX0, FrameY1 - bh, bw, bh), bl);
                GUI.DrawTexture(R(FrameX1 - br.width * k, FrameY1 - br.height * k, br.width * k, br.height * k), br);
                Tint(1f);
                return;
            }
            // запасная рамка: рисованные завитки в углах и тонкая линия между ними
            Texture2D tex = Theme.Corner;
            float s = Theme.CornerSize;
            Color bone = Theme.Bone;
            GUI.color = new Color(bone.r, bone.g, bone.b, 0.92f * fade);
            GUI.DrawTexture(R(FrameX0, FrameY0, s, s), tex);
            GUI.DrawTextureWithTexCoords(R(FrameX1 - s, FrameY0, s, s), tex, new Rect(1f, 0f, -1f, 1f), true);
            GUI.DrawTextureWithTexCoords(R(FrameX0, FrameY1 - s, s, s), tex, new Rect(0f, 1f, 1f, -1f), true);
            GUI.DrawTextureWithTexCoords(R(FrameX1 - s, FrameY1 - s, s, s), tex, new Rect(1f, 1f, -1f, -1f), true);
            Tint(1f);
        }

        /// Тонкая вертикальная линия между колонками, тающая к концам.
        void Divider(float x, float y, float height)
        {
            Color bone = Theme.Bone;
            GUI.color = new Color(bone.r, bone.g, bone.b, 0.8f * fade);
            GUI.DrawTexture(R(x, y, 1f, height), Theme.TaperV);
            Tint(1f);
        }

        /// Число рядом с картинкой (с тенью, чтобы читалось на любом фоне), по центру в (cx, cy).
        void Num(float cx, float cy, int value)
        {
            string text = value.ToString();
            Rect r = R(cx - 40f, cy - 20f, 80f, 44f);
            GUI.color = new Color(0f, 0f, 0f, 0.9f * fade);
            GUI.Label(new Rect(r.x + 1.5f, r.y + 1.5f, r.width, r.height), text, Theme.PaneTitle);
            Tint(1f);
            GUI.Label(r, text, Theme.PaneTitle);
        }

        static int SkillIndex(string code)
        {
            for (int i = 0; i < Build.Skills.Length; i++) if (Build.Skills[i].Code == code) return i;
            return -1;
        }

        // ---- левая часть: игла, снаряжение, катушка с навыками -----------------------------

        void DrawGear(bool can)
        {
            // игла-посох: картинка большая, а нажимается только узкая полоса над ней — чтобы не перекрывать навыки в кольце (было: нажатие на
            // «Суперпрыжок» меняло иглу)
            Texture2D needle = Icons.Get(Icons.NeedleKey(Build.Gear(BuildMath.GearNeedle)));
            Rect pic = R(RX(215f) - 100f, RY(338f), 200f, RY(965f) - RY(338f));
            if (needle != null)
            {
                Tint(can ? 1f : 0.55f);
                GUI.DrawTexture(pic, needle, ScaleMode.ScaleToFit);
                Tint(1f);
                if (selKind == KindGear && selIndex == BuildMath.GearNeedle) SelectCorners(Fit(pic, needle));
            }
            GearTile(R(RX(215f) - 36f, RY(338f), 72f, RY(965f) - RY(338f)), "", BuildMath.GearNeedle, can);

            // первый ряд: значок Ударной иглы, маска, катушка
            int strike = SkillIndex("strike");
            if (strike >= 0) SkillTile(Mid(RX(360f), RY(365f), 172f, 126f), strike, can);
            GearTile(Mid(RX(543f), RY(365f), 138f, 220f), Icons.GearKey("heart"), BuildMath.GearMasks, can);
            Num(RX(607f), RY(277f), Build.Gear(BuildMath.GearMasks));
            GearTile(Mid(RX(690f), RY(368f), 106f, 176f), Icons.GearKey("silk"), BuildMath.GearSilk, can);
            Num(RX(755f), RY(275f), Build.Gear(BuildMath.GearSilk));

            // второй ряд: набор (красная сумка), сумка для инструментов, красная шапка
            GearTile(Mid(RX(410f), RY(565f), 96f, 106f), Icons.GearKey("kit"), BuildMath.GearKit, can);
            Num(RX(455f), RY(517f), Build.Gear(BuildMath.GearKit));
            GearTile(Mid(RX(540f), RY(565f), 92f, 92f), Icons.GearKey("pouch"), BuildMath.GearPouch, can);
            Num(RX(587f), RY(517f), Build.Gear(BuildMath.GearPouch));
            CloakTile(Mid(RX(680f), RY(570f), 98f, 78f), can);

            DrawSpool(RX(440f), RY(820f), can);
        }

        /// Катушка в кольце из шести навыков (как у игры) и сердца шёлка под ней: нажатие на катушку — дальше по кругу 0–3.
        void DrawSpool(float cx, float cy, bool can)
        {
            const float Radius = 110f;
            Texture2D ring = Icons.Get(Icons.GearKey("ring"));
            if (ring != null)
            {
                GUI.color = new Color(1f, 1f, 1f, 0.9f * fade);
                GUI.DrawTexture(Mid(cx, cy + 4f, 262f, 262f), ring, ScaleMode.ScaleToFit);
            }
            else
            {
                GUI.color = new Color(1f, 1f, 1f, 0.3f * fade);
                GUI.DrawTexture(Mid(cx, cy, 2f * Radius + 20f, 2f * Radius + 20f), Theme.RingOf((int)Theme.Px(2f * Radius + 20f)));
            }
            Tint(1f);

            string[] codes = { "needolin", "dash", "claw", "eva", "soar", "grip" };
            float[] dx = { 0f, 0.866f, 0.866f, 0f, -0.866f, -0.866f };
            float[] dy = { -1f, -0.5f, 0.5f, 1f, 0.5f, -0.5f };
            for (int i = 0; i < codes.Length; i++)
            {
                int idx = SkillIndex(codes[i]);
                if (idx >= 0) SkillTile(Mid(cx + dx[i] * Radius, cy + dy[i] * Radius, 82f, 82f), idx, can);
            }

            // центр катушки: широкий — когда есть сердца шёлка, узкий — когда нет (что нашлось)
            int hearts = Build.Gear(BuildMath.GearRegen);
            string main = Icons.GearKey(hearts > 0 ? "wide" : "slim"), other = Icons.GearKey(hearts > 0 ? "slim" : "wide");
            if (Icons.Get(main) == null && Icons.Get(other) != null) main = other;
            GearTile(Mid(cx, cy + 4f, 108f, 124f), main, BuildMath.GearRegen, can);

            Texture2D pearl = Icons.Get(Icons.GearKey("pearl"));
            float[] px = { -27f, 0f, 27f }, py = { 54f, 66f, 54f };
            for (int k = 0; k < 3; k++)
            {
                Rect pb = Mid(cx + px[k], cy + py[k], 26f, 26f);
                GUI.color = new Color(1f, 1f, 1f, (k < hearts ? 1f : 0.22f) * fade);
                if (pearl != null) GUI.DrawTexture(pb, pearl, ScaleMode.ScaleToFit);
                else GUI.DrawTexture(pb, Theme.SoftDisc);
            }
            Tint(1f);
        }

        /// Числовое снаряжение: левая кнопка — дальше по кругу, правая — назад. Число рисуется отдельно (Num).
        void GearTile(Rect box, string iconKey, int gear, bool can)
        {
            bool shown = iconKey.Length > 0;    // пустое имя: картинку рисует вызывающий, здесь только нажатие
            int click = Tile(box, shown ? Icons.Get(iconKey) : null, shown ? Build.GearName(gear) : "", can ? 1f : 0.55f, can, false, KindGear, gear, "",
                LookBare, null, 1f, 1f, false);
            if (click != 0)
            {
                string message;
                Build.StepGear(gear, click == 1 ? 1 : -1, out message);
                status = message;
            }
        }

        /// «Хорнет без креста» (cursed = false) и «проклятая Хорнет» (cursed = true): надета — яркая, нет — тусклая.
        void SpecialTile(Rect box, bool cursed, bool can)
        {
            bool on = Build.SpecialCrestOn(cursed);
            int click = Tile(box, Icons.Get(Icons.GearKey(cursed ? "cursed" : "crestless")),
                cursed ? "Cursed" : "Crestless", on ? 1f : 0.5f, can, false, KindSpecial, cursed ? 1 : 0, "", LookBare, null, 1f, 1f, false);
            if (click != 0)
            {
                string message;
                Build.ToggleSpecialCrest(cursed, out message);
                status = message;
            }
        }

        /// Красная шапка: плащи по кругу (нет, странник, Фейдаун, оба); картинка показывает текущее состояние.
        void CloakTile(Rect box, bool can)
        {
            int click = Tile(box, Icons.Get(Icons.CloakKey(Build.Cloak())), "Cloaks", can ? 1f : 0.55f, can, false, KindCloak, 0, "",
                LookBare, null, 1f, 1f, false);
            if (click != 0)
            {
                string message;
                Build.StepCloak(click == 1 ? 1 : -1, out message);
                status = message;
            }
        }

        /// Навык: нажал — отключил (красный крест), нажал ещё — вернул. Навыка нет в сохранении — бледный, не нажимается.
        void SkillTile(Rect box, int index, bool can)
        {
            Build.Skill skill = Build.Skills[index];
            bool off = Build.IsOff(skill);
            bool has = Build.Has(skill);
            float alpha = off ? 0.45f : has ? 1f : 0.42f;
            if (Tile(box, Icons.Get(Icons.SkillKey(skill.Code)), skill.En, alpha, can && (off || has), off, KindSkill, index, "",
                LookBare, null, 1f, 1f, false) != 0)
            {
                string message;
                Build.SetSkill(skill, !off, out message);
                status = message;
            }
        }

        // ---- средняя часть: привязки, «без креста», «проклятая»; правая: описание и кнопки -----------------

        /// Ряд из четырёх привязок (включена — обычная иконка GoP, выключена — его же «_off») и под ним «без креста» и «проклятая».
        void DrawBindings(bool can)
        {
            float[] xs = { 850f, 972f, 1102f, 1232f };
            for (int i = 0; i < BuildMath.Bindings; i++)
            {
                bool on = Build.Binding(i);
                Texture2D icon = Icons.Get(Icons.BindingKey(i, on));
                float alpha = 1f;
                if (icon == null && !on)
                {
                    // нет «_off»: берём обычную иконку, но бледно
                    icon = Icons.Get(Icons.BindingKey(i, true));
                    alpha = 0.3f;
                }
                Rect box = Mid(RM(xs[Mathf.Min(i, xs.Length - 1)]), RY(337f), BindD, BindD);
                if (Tile(box, icon, Build.BindingName(i), can ? alpha : alpha * 0.55f, can, false, KindBinding, i, "", LookBare, null, 1f, 1f, false) != 0)
                {
                    string message;
                    Build.ToggleBinding(i, out message);
                    status = message;
                }
            }
            SpecialTile(Mid(RM(865f), RY(450f), 132f, 100f), false, can);
            SpecialTile(Mid(RM(1040f), RY(465f), 98f, 100f), true, can);
        }

        /// Правая колонка: название и описание того, над чем мышь (как у игры справа), внизу три кнопки:
        /// все привязки включить / снять и вернуть отключённые навыки.
        void DrawInfo(bool can)
        {
            string title, text;
            Describe(out title, out text);
            if (title.Length > 0) GUI.Label(R(InfoX, RY(285f) - 20f, InfoW, 44f), title, Theme.PaneTitle);
            GUI.Label(R(InfoX + 6f, RY(342f) - 17f, InfoW - 12f, 300f), text, Theme.PaneText);

            string message;
            float x = InfoX + 14f, width = InfoW - 28f;
            float by = FrameY1 - 44f - (3f * ButtonH + 2f * BuildBtnGap);
            GUI.enabled = can;
            if (GUI.Button(R(x, by, width, ButtonH), Theme.Caps(L.T("Включить все", "All on")), Theme.Ghost))
            {
                Build.SetAllBindings(true, out message);
                status = message;
            }
            if (GUI.Button(R(x, by + ButtonH + BuildBtnGap, width, ButtonH), Theme.Caps(L.T("Снять все", "All off")), Theme.Ghost))
            {
                Build.SetAllBindings(false, out message);
                status = message;
            }
            GUI.enabled = can && Plugin.SkillsOff.Length > 0;
            if (GUI.Button(R(x, by + 2f * (ButtonH + BuildBtnGap), width, ButtonH), Theme.Caps(L.T("Вернуть навыки", "Return skills")), Theme.Ghost))
            {
                Build.ReturnSkills(out message);
                status = message;
            }
            GUI.enabled = true;
        }

        /// Где окажется картинка в рамке при ScaleToFit.
        static Rect Fit(Rect box, Texture2D tex)
        {
            float k = Mathf.Min(box.width / tex.width, box.height / tex.height);
            float w = tex.width * k, h = tex.height * k;
            return new Rect(box.x + (box.width - w) / 2f, box.y + (box.height - h) / 2f, w, h);
        }

        /// Курсор выбора, как у игры: два уголка — вверху слева и внизу справа от картинки.
        void SelectCorners(Rect box)
        {
            float pad = Theme.Px(6f), arm = Theme.Px(16f), t = Mathf.Max(2f, Theme.Px(2f));
            Rect r = new Rect(box.x - pad, box.y - pad, box.width + 2f * pad, box.height + 2f * pad);
            GUI.color = new Color(1f, 1f, 1f, 0.9f * fade);
            GUI.DrawTexture(new Rect(r.x, r.y, arm, t), Theme.Plain);
            GUI.DrawTexture(new Rect(r.x, r.y, t, arm), Theme.Plain);
            GUI.DrawTexture(new Rect(r.xMax - arm, r.yMax - t, arm, t), Theme.Plain);
            GUI.DrawTexture(new Rect(r.xMax - t, r.yMax - arm, t, arm), Theme.Plain);
            Tint(1f);
        }

        /// Один ряд-поток: «Хорнет без креста», «проклятая Хорнет» и кресты (нажал — надел); слоты надетого креста (нажал — выбрать
        /// инструмент, правая — очистить); выбор инструмента. Всё кольцами, по центру колонки.
        void DrawCrests(float x, float y, float width, bool can)
        {
            string message;
            int perRow = Mathf.Max(1, (int)((width + TileGap) / (CrestD + TileGap)));

            List<ToolCrest> crests = Build.Crests();
            int total = crests.Count;
            int rows = (total + perRow - 1) / perRow;
            perRow = CrestMath.PerRow(total, perRow); // ряды ровные: 9 крестов — 5 и 4, а не 7 и 2
            for (int i = 0; i < total; i++)
            {
                int col = i % perRow, row = i / perRow;
                int inRow = Mathf.Min(perRow, total - row * perRow);
                float rowX = x + (width - (inRow * CrestD + (inRow - 1) * TileGap)) / 2f;
                Rect box = R(rowX + col * (CrestD + TileGap), y + row * (CrestD + TileGap), CrestD, CrestD);
                ToolCrest c = crests[i];
                bool on = Build.CrestIsOn(c);
                int levels;
                int level = Build.CrestLevel(c, out levels);
                int click = Tile(box, Icons.Get(Icons.CrestKey(c.name)), c.name, on ? 1f : (can ? 0.5f : 0.3f), can, false, KindCrest, i,
                    level > 0 ? level.ToString() : "", LookRing, null, 0.7f, 0.7f, on);
                if (click != 0)
                {
                    // надетый крест с несколькими уровнями: левая кнопка — следующий уровень, правая — прежний (как плащи); иначе просто надеть
                    if (on && level > 0) { Build.StepCrest(c, click == 1 ? 1 : -1, out message); status = message; }
                    else if (!on) { Build.EquipCrest(c, out message); status = message; }
                }
            }

            // слоты креста, который надет сейчас
            ToolCrest cur = Build.CurrentCrest();
            string curName = cur != null ? cur.name : "";
            if (curName != pickCrest) { pickSlot = -1; pickCrest = curName; }
            List<Build.SlotView> slots = Build.Slots(cur);
            if (pickSlot >= slots.Count) pickSlot = -1;

            float sy = y + rows * (CrestD + TileGap) + 22f;
            int slotPerRow = Mathf.Max(1, (int)((width + TileGap) / (SlotD + TileGap + 6f)));
            int slotRows = 0;
            for (int i = 0; i < slots.Count; i++)
            {
                Build.SlotView v = slots[i];
                int col = i % slotPerRow, row = i / slotPerRow;
                slotRows = row + 1;
                int inRow = Mathf.Min(slotPerRow, slots.Count - row * slotPerRow);
                float rowX = x + (width - (inRow * SlotD + (inRow - 1) * (TileGap + 6f))) / 2f;
                Rect box = R(rowX + col * (SlotD + TileGap + 6f), sy + row * (SlotD + TileGap), SlotD, SlotD);
                float fade2 = v.Allowed ? 1f : 0.3f;

                // знак слота из игры; не нашёлся — кольцо цвета слота
                Texture2D sigil = Icons.Get(Icons.ArtKey(SlotSigil(v.Type)));
                Color sc = SlotColor(v.Type);
                GUI.color = new Color(sc.r, sc.g, sc.b, 0.95f * fade2 * fade);
                if (sigil != null) GUI.DrawTexture(box, sigil, ScaleMode.ScaleToFit);
                else GUI.DrawTexture(box, Theme.RingOf((int)box.width));
                Tint(1f);

                Texture2D icon = v.Tool.Length > 0 ? Icons.Get(Icons.ToolKey(v.Tool)) : null;
                int click = Tile(box, icon, v.Tool.Length > 0 ? Build.ToolLabel(v.Tool) : "", v.Allowed ? 1f : 0.35f, can && v.Allowed, false, KindSlot, i, "",
                    LookBare, null, 0.56f, 0.56f, pickSlot == i);
                if (click == 1) pickSlot = pickSlot == i ? -1 : i;
                else if (click == 2)
                {
                    Build.SetSlot(cur, i, "", out message);
                    status = message;
                }
            }

            // выбор инструмента для открытого слота: первое кольцо — «пусто», дальше открытые инструменты этого цвета
            if (pickSlot >= 0 && pickSlot < slots.Count && cur != null && can)
            {
                Build.SlotView v = slots[pickSlot];
                if (!pickHas || pickType != v.Type || Time.unscaledTime - pickAt > 1f)
                {
                    pickList = Build.ToolsOf(v.Type);
                    pickType = v.Type;
                    pickAt = Time.unscaledTime;
                    pickHas = true;
                }
                int perPick = Mathf.Max(1, (int)((width + TileGap) / (ToolD + TileGap)));
                float py = sy + slotRows * (SlotD + TileGap) + 20f;
                int count = pickList.Count + 1;
                for (int k = 0; k < count; k++)
                {
                    int col = k % perPick, row = k / perPick;
                    int inRow = Mathf.Min(perPick, count - row * perPick);
                    float rowX = x + (width - (inRow * ToolD + (inRow - 1) * TileGap)) / 2f;
                    Rect box = R(rowX + col * (ToolD + TileGap), py + row * (ToolD + TileGap), ToolD, ToolD);
                    string name = k == 0 ? "" : pickList[k - 1].name;
                    bool chosen = name == v.Tool;
                    Texture2D icon = k == 0 ? Theme.IconCross : Icons.Get(Icons.ToolKey(name));
                    int click = Tile(box, icon, k == 0 ? "" : Build.ToolLabel(name), k == 0 ? 0.7f : 1f, true, false, KindTool, k, "",
                        LookRing, null, k == 0 ? 0.5f : 0.7f, k == 0 ? 0.5f : 0.7f, chosen);
                    if (click != 0)
                    {
                        Build.SetSlot(cur, pickSlot, name, out message);
                        status = message;
                        pickSlot = -1;
                        pickHas = false;
                    }
                }
            }
            else pickHas = false;
        }

        // ---- картинка в кольце ---------------------------------------------------------

        /// Одна картинка: кольцо (или подложка игры, или ничего) и значок внутри. Под мышью светлеет. Нет значка (не нашёлся) — короткое название.
        /// corner — число в правом нижнем углу. selected — второе кольцо снаружи (надетый крест, открытый слот, выбранный инструмент).
        /// Запоминает, что мышь над ней (kind/index), чтобы справа показать подпись. Возвращает 1 — левая кнопка, 2 — правая, 0 — не нажата.
        int Tile(Rect box, Texture2D icon, string fallback, float alpha, bool enabled, bool crossed, int kind, int index, string corner,
                 int look, Texture2D board, float fillW, float fillH, bool selected)
        {
            bool chosen = look == LookBare && kind == selKind && index == selIndex;
            Event e = Event.current;
            bool hover = box.Contains(e.mousePosition);
            if (hover) { hoverKind = kind; hoverIndex = index; }

            int result = 0;
            if (enabled && hover && e.type == EventType.MouseDown && (e.button == 0 || e.button == 1))
            {
                result = e.button == 1 ? 2 : 1;
                e.Use();
            }

            bool lit = hover && enabled;
            float a = alpha * (lit ? 1f : 0.88f);
            Color bone = Theme.Bone;
            float d = Mathf.Min(box.width, box.height);
            Rect round = new Rect(box.x + (box.width - d) / 2f, box.y + (box.height - d) / 2f, d, d);

            if (look == LookBoard && board != null)
            {
                Tint(a * (lit ? 1f : 0.9f));
                GUI.DrawTexture(box, board, ScaleMode.ScaleToFit);
            }
            else if (look != LookBare)
            {
                // мягкая подложка (видно, куда нажимать) и тонкое кольцо цвета кости
                GUI.color = new Color(1f, 1f, 1f, (lit ? 0.16f : 0.06f) * Mathf.Min(1f, alpha * 1.6f) * fade);
                GUI.DrawTexture(round, Theme.SoftDisc);
                GUI.color = new Color(bone.r, bone.g, bone.b, a * fade);
                GUI.DrawTexture(round, Theme.RingOf((int)round.width));
            }
            else if (lit && box.height < 2f * box.width)
            {
                GUI.color = new Color(1f, 1f, 1f, 0.12f * fade);
                GUI.DrawTexture(round, Theme.SoftDisc);
            }

            if (selected)
            {
                float grow = Mathf.Max(4f, Theme.Px(6f));
                Rect outer = new Rect(round.x - grow, round.y - grow, round.width + 2f * grow, round.height + 2f * grow);
                GUI.color = new Color(1f, 1f, 1f, 0.8f * fade);
                GUI.DrawTexture(outer, Theme.RingOf((int)outer.width));
            }

            if (chosen && icon != null) SelectCorners(Fit(box, icon));
            Rect inner = new Rect(box.x + box.width * (1f - fillW) / 2f, box.y + box.height * (1f - fillH) / 2f, box.width * fillW, box.height * fillH);
            Tint(a);
            if (icon != null) GUI.DrawTexture(inner, icon, ScaleMode.ScaleToFit);
            else if (fallback.Length > 0) GUI.Label(box, fallback, Theme.SmallCenter);
            if (crossed)
            {
                GUI.color = new Color(1f, 0.38f, 0.32f, 0.95f * fade);
                GUI.DrawTexture(inner, Theme.IconCross, ScaleMode.ScaleToFit);
            }
            if (corner.Length > 0) Corner(box, corner);
            Tint(1f);
            return result;
        }

        /// Число в правом нижнем углу картинки (с тенью, чтобы читалось на любой подложке).
        void Corner(Rect box, string text)
        {
            float w = Theme.Px(56f), h = Theme.Px(38f);
            Rect r = new Rect(box.xMax - w + Theme.Px(6f), box.yMax - h + Theme.Px(4f), w, h);
            GUI.color = new Color(0f, 0f, 0f, 0.9f * fade);
            GUI.Label(new Rect(r.x + 1.5f, r.y + 1.5f, r.width, r.height), text, Theme.TileNumber);
            Tint(1f);
            GUI.Label(r, text, Theme.TileNumber);
        }

        // ---- описание плитки под мышью ---------------------------------------------------

        /// Название и описание плитки, над которой мышь (третья колонка). Мышь ни над одной — короткая подсказка.
        void Describe(out string title, out string text)
        {
            title = "";
            string both = L.T("Левая кнопка — дальше по кругу, правая — назад.", "Left click: next, right click: back.");
            text = L.T("Наведи мышь на картинку, чтобы увидеть, что это. Нажми на картинку, чтобы изменить.",
                       "Hover a picture to see what it is. Click it to change it.");
            switch (selKind)
            {
                case KindBinding:
                    if (selIndex >= 0 && selIndex < BuildMath.Bindings)
                    {
                        int i = selIndex;
                        string key = Plugin.BindKeyName(i);
                        title = Build.BindingName(i);
                        text = (Build.Binding(i) ? L.T("Включена. ", "On. ") : L.T("Выключена. ", "Off. ")) + Build.BindingInfo(i) + "\n\n"
                            + (key.Length > 0 ? L.T("Клавиша: ", "Key: ") + key : L.T("Клавиша не назначена.", "No key set."));
                    }
                    break;
                case KindSkill:
                    if (selIndex >= 0 && selIndex < Build.Skills.Length)
                    {
                        Build.Skill skill = Build.Skills[selIndex];
                        title = Build.SkillName(skill);
                        text = Build.IsOff(skill) ? L.T("Отключён. Нажми, чтобы вернуть.", "Off. Click to get it back.")
                             : Build.Has(skill) ? L.T("Включён. Нажми, чтобы отключить.", "On. Click to turn it off.")
                             : L.T("Этого навыка нет в сохранении.", "This skill is not in the save.");
                    }
                    break;
                case KindGear:
                    if (selIndex >= 0 && selIndex < BuildMath.GearCount)
                    {
                        title = Build.GearName(selIndex);
                        text = L.T("Сейчас: ", "Now: ") + Build.Gear(selIndex) + " (" + BuildMath.GearMin(selIndex) + "–" + Build.GearTop(selIndex) + ").\n\n" + both;
                    }
                    break;
                case KindCloak:
                    {
                        int c = Build.Cloak();
                        title = L.T("Плащи", "Cloaks");
                        text = L.T("Сейчас: ", "Now: ") + (c == 0 ? L.T("нет", "none") : c == 1 ? Build.SkillName(Build.Skills[7]) : c == 2 ? Build.SkillName(Build.Skills[8]) : L.T("оба", "both")) + ".\n\n" + both;
                        break;
                    }
                case KindSpecial:
                    {
                        bool cursed = selIndex == 1;
                        title = cursed ? L.T("Проклятая Хорнет", "Cursed Hornet") : L.T("Хорнет без креста", "Crestless Hornet");
                        text = Build.SpecialCrestOn(cursed) ? L.T("Надета. Нажми, чтобы снять и вернуть прежний крест.", "On. Click to take it off and get the old crest back.")
                                                            : L.T("Не надета. Нажми, чтобы надеть.", "Off. Click to put it on.");
                        break;
                    }
                case KindCrest:
                    {
                        List<ToolCrest> crests = Build.Crests();
                        if (selIndex >= 0 && selIndex < crests.Count)
                        {
                            int levels;
                            int level = Build.CrestLevel(crests[selIndex], out levels);
                            title = Build.Label(crests[selIndex]);
                            bool worn = Build.CrestIsOn(crests[selIndex]);
                            text = worn ? L.T("Надет.", "On.") : L.T("Нажми, чтобы надеть.", "Click to put it on.");
                            if (level > 0)
                                text = L.T("Уровень ", "Level ") + level + L.T(" из ", " of ") + levels + ". " + text
                                     + "\n\n" + (worn ? L.T("Левая кнопка — следующий уровень, правая — прежний (как плащи). ", "Left click: next level, right click: previous (like the cloaks). ")
                                                        : L.T("Потом нажимай на него же, чтобы менять уровень: левая кнопка — следующий, правая — прежний. ", "Then click it again to change the level: left for next, right for previous. "))
                                     + L.T("У каждого уровня свои слоты и свои инструменты в них.", "Every level has its own slots and tools in them.");
                        }
                        break;
                    }
                case KindSlot:
                    {
                        List<Build.SlotView> slots = Build.Slots(Build.CurrentCrest());
                        if (selIndex >= 0 && selIndex < slots.Count)
                        {
                            Build.SlotView v = slots[selIndex];
                            title = L.T("Слот ", "Slot ") + (selIndex + 1);
                            string what = v.Tool.Length > 0 ? Build.ToolLabel(v.Tool) : L.T("пусто", "empty");
                            string tail = !v.Open ? L.T("Слот закрыт.", "The slot is locked.")
                                        : !v.Allowed ? L.T("Привязка Tools отключила инструменты.", "The Tools binding disabled tools.")
                                        : L.T("Левая кнопка — выбрать инструмент, правая — очистить.", "Left click: choose a tool, right click: clear.");
                            text = L.T("Надето: ", "Equipped: ") + what + ".\n\n" + tail;
                        }
                        break;
                    }
                case KindTool:
                    if (selIndex == 0)
                    {
                        title = L.T("Пусто", "Empty");
                        text = L.T("Очистить слот.", "Clear the slot.");
                    }
                    else if (selIndex - 1 < pickList.Count)
                    {
                        title = Build.ToolLabel(pickList[selIndex - 1].name);
                        text = L.T("Нажми, чтобы положить в слот.", "Click to put it in the slot.");
                    }
                    break;
            }
        }
    }
}
