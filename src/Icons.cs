// ============================================================================
//  Icons.cs — картинки для вкладки «Привязки» (0.25.1).
//  Привязки: иконки берутся у самого Gods of Pharloom (его меню привязок: GG_UI_pieces_nail / soul / charm / shell и версия «_off»
//  из его набора ресурсов Preload.bundleResources). Навыки: значки из инвентаря игры (дети «Radial Layout» и «Cloak States» —
//  тот же путь, по которому GoP находит эти навыки), а у «Ударной иглы» — значок, который ей ставит сам GoP (needle_charge_prompt).
//  Спрайт нельзя рисовать в IMGUI напрямую (он лежит в атласе), поэтому он один раз копируется в свою маленькую текстуру.
//  Копирование делается в Update, а не в OnGUI (там нельзя менять активную RenderTexture). Если значок найти не удалось —
//  меню рисует вместо него короткое название, ничего не ломается; повторная попытка раз в несколько секунд
//  (инвентарь и ресурсы GoP могут появиться позже начала игры). Что нашлось — пишется в журнал один раз.
//  0.27.0: ещё значки снаряжения (игла по уровням, маски, шёлк, катушка — из инвентаря; сумка, набор, плащи, «без креста», «проклятая» — из
//  набора ресурсов GoP, как у него), а также кресты и инструменты (картинки берутся у самой игры: ToolCrest.CrestSprite, ToolItem.GetInventorySprite).
//  0.28.0: «art:имя» — любая картинка интерфейса игры по имени спрайта (завиток в углу окна, «гнёзда» инструментов, подложки масок и шёлка):
//  ищется среди загруженных спрайтов, вырезается из атласа как остальные, но без чистки обрывков. Не нашлась — меню рисует свою запасную.
//  0.28.1: «tk2d:имя» — кусок рамки из коллекции спрайтов tk2d самой игры (углы шипастой рамки инвентаря: Border_Fleur_*); они лежат в атласе повёрнутыми,
//  поэтому после вырезания поворачиваются. Плюс значки орба, кольца, центра катушки и жемчужин сердец шёлка (берутся прямо у предмета, без чистки).
//  Что менять: имена — в Find, Gear и Inventory ниже.
// ============================================================================
using System.Collections.Generic;
using Gods_Of_Pharloom;
using UnityEngine;

namespace Encore
{
    internal static class Icons
    {
        static readonly Dictionary<string, Texture2D> baked = new Dictionary<string, Texture2D>();
        static readonly Dictionary<string, float> nextTry = new Dictionary<string, float>();
        static readonly List<string> wanted = new List<string>();
        static readonly HashSet<string> reported = new HashSet<string>();
        static Transform[] scan;
        static Sprite[] spriteScan;
        static int emptyDiag;

        const float RetrySec = 3f, ArtRetrySec = 8f;
        const int PerPump = 6;

        static readonly string[] BindingArt = { "nail", "soul", "charm", "shell" };

        /// Ключ значка привязки i (включена: обычный, выключена: «_off»).
        internal static string BindingKey(int i, bool on)
        {
            return "bind" + i + (on ? "" : "off");
        }

        /// Ключ значка иглы: level 0..4 (у игры предметы Nail1..Nail5).
        internal static string NeedleKey(int level)
        {
            return "needle:" + (Mathf.Clamp(level, 0, 4) + 1);
        }

        /// Ключ значка снаряжения: heart, silk, regen, pouch, kit, crestless, cursed.
        internal static string GearKey(string name)
        {
            return "gear:" + name;
        }

        /// Ключ значка плаща-состояния: 0 нет, 1 странник, 2 Фейдаун, 3 оба (как у GoP).
        internal static string CloakKey(int state)
        {
            return "gear:cloak" + Mathf.Clamp(state, 0, 3);
        }

        /// Ключ значка креста по его внутреннему имени.
        internal static string CrestKey(string name)
        {
            return "crest:" + name;
        }

        /// Ключ значка инструмента по его внутреннему имени.
        internal static string ToolKey(string name)
        {
            return "tool:" + name;
        }

        /// Ключ картинки интерфейса игры по имени спрайта (например overscan_corner_fleur009).
        internal static string ArtKey(string spriteName)
        {
            return "art:" + spriteName;
        }

        /// Ключ куска рамки из коллекции спрайтов tk2d игры (например Border_Fleur_top_corner0007).
        internal static string TkKey(string spriteName)
        {
            return "tk2d:" + spriteName;
        }

        /// Ключ значка навыка по его коду.
        internal static string SkillKey(string code)
        {
            return "skill:" + code;
        }

        /// Готовая текстура или null (тогда её запрашивают, а меню рисует название). Вызывать можно каждый кадр.
        internal static Texture2D Get(string key)
        {
            Texture2D t;
            if (baked.TryGetValue(key, out t)) return t;
            if (!wanted.Contains(key)) wanted.Add(key);
            return null;
        }

        /// Из Update, пока открыта вкладка: делает значки из запрошенных (не больше нескольких за кадр).
        internal static void Pump()
        {
            if (wanted.Count == 0) return;
            float now = Time.unscaledTime;
            int done = 0;
            bool missed = false;
            scan = null;
            spriteScan = null;
            for (int i = wanted.Count - 1; i >= 0 && done < PerPump; i--)
            {
                string key = wanted[i];
                float at;
                if (nextTry.TryGetValue(key, out at) && now < at) continue;
                done++;
                Texture2D tex = null;
                try { tex = Make(key); }
                catch (System.Exception ex) { Plugin.Caught("Icons/" + key, ex); }
                if (tex != null) { baked[key] = tex; wanted.RemoveAt(i); }
                else
                {
                    nextTry[key] = now + (key.StartsWith("art:") || key.StartsWith("tk2d:") ? ArtRetrySec : RetrySec);
                    if (key.StartsWith("skill:") && key != SkillKey("strike")) missed = true;
                    if (reported.Add("miss " + key)) Plugin.Log.LogInfo("Encore: значок «" + key + "» пока не найден (будет повтор)");
                }
            }
            ReleaseBake();
            spriteScan = null;
            if (missed && scan != null && !reported.Contains("diag")) Diagnose();
            scan = null;
        }

        /// Один раз в журнал: что лежит там, где ищем значки навыков (чтобы поправить имена, если что-то не нашлось).
        static void Diagnose()
        {
            int n = 0;
            for (int i = 0; i < scan.Length && n < 60; i++)
            {
                Transform t = scan[i];
                if (t == null || t.parent == null) continue;
                string parent = t.parent.name;
                if (parent != "Radial Layout" && parent != "Cloak States") continue;
                SpriteRenderer[] rs = t.GetComponentsInChildren<SpriteRenderer>(true);
                int withSprite = 0;
                for (int k = 0; k < rs.Length; k++) if (rs[k].sprite != null) withSprite++;
                Plugin.Log.LogInfo("Encore: значки навыков — " + Path(t) + ": SpriteRenderer " + rs.Length + ", со спрайтом " + withSprite);
                n++;
            }
            if (n > 0) reported.Add("diag");
            else if (reported.Add("diag-empty" + (++emptyDiag)) && emptyDiag <= 2)
                Plugin.Log.LogInfo("Encore: значки навыков — в сцене нет предметов инвентаря (Radial Layout / Cloak States); откройте инвентарь в игре");
        }

        /// Спрайт по ключу -> своя текстура.
        static Texture2D Make(string key)
        {
            string note;
            if (key.StartsWith("tk2d:"))
            {
                Texture2D piece = MakeTk(key.Substring(5), out note);
                if (piece != null && reported.Add("found " + key)) Plugin.Log.LogInfo("Encore: значок «" + key + "» ← " + note);
                return piece;
            }
            Sprite sprite = Find(key, out note);
            if (sprite == null) return null;
            Texture2D tex = Bake(sprite, !(key.StartsWith("art:") || NoStrip(key)));
            if (tex != null && reported.Add("found " + key)) Plugin.Log.LogInfo("Encore: значок «" + key + "» ← " + note);
            return tex;
        }

        /// Значки, у которых «обрывки» стирать нельзя: тонкое кольцо, центр катушки, жемчужины, орб, лучи «Ударной иглы».
        static bool NoStrip(string key)
        {
            return key == "skill:strike" || key == "gear:orb" || key == "gear:ring" || key == "gear:wide" || key == "gear:slim" || key == "gear:pearl";
        }

        static Sprite Find(string key, out string note)
        {
            note = "";
            if (key.StartsWith("bind"))
            {
                bool off = key.EndsWith("off");
                int i = key[4] - '0';
                if (i < 0 || i >= BindingArt.Length) return null;
                string name = "GG_UI_pieces_" + BindingArt[i] + (off ? "_off" : "");
                note = "GoP: " + name;
                return Bundle(name);
            }
            if (key.StartsWith("art:")) return Art(key.Substring(4), out note);
            if (key.StartsWith("needle:"))
                return Inventory("Nail" + key.Substring(7), "Needle", out note);
            if (key.StartsWith("gear:")) return Gear(key.Substring(5), out note);
            if (key.StartsWith("crest:"))
            {
                ToolCrest crest = ToolItemManager.GetCrestByName(key.Substring(6));
                if (crest == null) return null;
                note = "крест " + crest.name;
                return crest.CrestSprite;
            }
            if (key.StartsWith("tool:"))
            {
                ToolItem tool = ToolItemManager.GetToolByName(key.Substring(5));
                if (tool == null) return null;
                note = "инструмент " + tool.name;
                return tool.GetInventorySprite(ToolItem.IconVariants.Default);
            }
            string code = key.StartsWith("skill:") ? key.Substring(6) : key;
            switch (code)
            {
                case "dash": return Inventory("Sprint", out note);
                case "claw": return Inventory("Harpoon Dash", out note);
                case "eva": return Inventory("Eva Heal", out note);
                case "soar": return Inventory("Super Jump", out note);
                case "grip": return Inventory("Wall Jump", out note);
                case "needolin": return Inventory("Needolin", out note);
                case "drift": return Inventory("CloakState_1", out note);   // у GoP: 1 — только плащ странника
                case "fay": return Inventory("CloakState_2", out note);     // 2 — только плащ Фейдаун
                case "strike":
                    {
                        note = "GoP: needle_charge_prompt";
                        Sprite s = Bundle("needle_charge_prompt");
                        return s != null ? s : Inventory("Needle Art", out note);
                    }
            }
            return null;
        }

        /// Значки снаряжения: то, что GoP берёт из инвентаря (маски, шёлк, катушка), и то, что у него лежит в наборе ресурсов.
        static Sprite Gear(string name, out string note)
        {
            note = "";
            switch (name)
            {
                case "heart": return Inventory("Pieces 4", "Heart Pieces", out note);   // у GoP включён только «Pieces 4» — целая маска
                case "silk": return Inventory("Full", "Spool Pieces", out note);        // и только «Full» — целая катушка
                case "regen": return Inventory("Spool", "Spool Group", out note);
                case "orb": return Direct("New Item Orb", "Needle", out note);         // красный «орб» над иглой
                case "ring": return Direct("Icon Ring", "Spool Group", out note);      // кольцо вокруг катушки
                case "wide": return Direct("Wide", "Spool", out note);                 // центр катушки: широкий
                case "slim": return Direct("Slim", "Spool", out note);                 // и узкий
                case "pearl": return Direct("Left", "Heart", out note);                // жемчужина сердца шёлка
                case "pouch": note = "GoP: tools_bug_upgrade"; return Bundle("tools_bug_upgrade");
                case "kit": note = "GoP: tools_upgrade_sack"; return Bundle("tools_upgrade_sack");
                case "crestless": note = "GoP: cloakless_hornet_attack"; return Bundle("cloakless_hornet_attack");
                case "cursed": note = "GoP: CurseWitch"; return Bundle("CurseWitch");
                case "cloak0": case "cloak1": case "cloak2": case "cloak3":
                    note = "GoP: CloakState_" + name[5];
                    return Bundle("CloakState_" + name[5]);
            }
            return null;
        }

        /// Спрайт интерфейса по имени среди всех загруженных (у спрайтов из атласа к имени может быть приписано «(Clone)»).
        static Sprite Art(string name, out string note)
        {
            note = "";
            if (spriteScan == null) spriteScan = Resources.FindObjectsOfTypeAll<Sprite>();
            string clone = name + "(Clone)";
            Sprite found = null;
            for (int i = 0; i < spriteScan.Length; i++)
            {
                Sprite s = spriteScan[i];
                if (s == null) continue;
                string n = s.name;
                if (n != name && n != clone) continue;
                if (found == null || s.rect.width * s.rect.height > found.rect.width * found.rect.height) found = s;
            }
            if (found != null) note = "спрайт " + found.name + " " + (int)found.rect.width + "x" + (int)found.rect.height;
            return found;
        }

        /// Спрайт самого предмета с таким именем и родителем (без поиска «значка» среди детей).
        static Sprite Direct(string objectName, string parentName, out string note)
        {
            note = "";
            if (scan == null) scan = Resources.FindObjectsOfTypeAll<Transform>();
            for (int i = 0; i < scan.Length; i++)
            {
                Transform t = scan[i];
                if (t == null || t.name != objectName) continue;
                if (t.parent == null || t.parent.name != parentName) continue;
                SpriteRenderer r = t.GetComponent<SpriteRenderer>();
                if (r == null || r.sprite == null) continue;
                note = "инвентарь: " + Path(t) + " (спрайт " + r.sprite.name + ")";
                return r.sprite;
            }
            return null;
        }

        static System.Type tkDataType;

        /// Кусок из коллекции спрайтов tk2d: ищется среди загруженных коллекций по имени (через отражение: сам tk2d не подключён),
        /// вырезается из атласа; если лежит в атласе повёрнутым (flipped = 1) — поворачивается обратно.
        static Texture2D MakeTk(string name, out string note)
        {
            note = "";
            if (tkDataType == null)
            {
                System.Reflection.Assembly[] all = System.AppDomain.CurrentDomain.GetAssemblies();
                for (int i = 0; i < all.Length && tkDataType == null; i++)
                {
                    try { tkDataType = all[i].GetType("tk2dSpriteCollectionData"); } catch (System.Exception) { }
                }
                if (tkDataType == null) return null;
            }
            Object[] cols = Resources.FindObjectsOfTypeAll(tkDataType);
            System.Reflection.FieldInfo defsField = tkDataType.GetField("spriteDefinitions");
            if (defsField == null) return null;
            for (int c = 0; c < cols.Length; c++)
            {
                if (cols[c] == null) continue;
                System.Array defs = defsField.GetValue(cols[c]) as System.Array;
                if (defs == null) continue;
                for (int i = 0; i < defs.Length; i++)
                {
                    object def = defs.GetValue(i);
                    if (def == null) continue;
                    System.Type dt = def.GetType();
                    string n = dt.GetField("name").GetValue(def) as string;
                    if (n != name) continue;
                    Vector2[] uvs = dt.GetField("uvs").GetValue(def) as Vector2[];
                    Material mat = dt.GetField("material").GetValue(def) as Material;
                    if (uvs == null || uvs.Length < 3 || mat == null || mat.mainTexture == null) continue;
                    Texture src = mat.mainTexture;
                    float x0 = 1f, x1 = 0f, y0 = 1f, y1 = 0f;
                    for (int k = 0; k < uvs.Length; k++)
                    {
                        x0 = Mathf.Min(x0, uvs[k].x); x1 = Mathf.Max(x1, uvs[k].x);
                        y0 = Mathf.Min(y0, uvs[k].y); y1 = Mathf.Max(y1, uvs[k].y);
                    }
                    Rect r = new Rect(Mathf.Round(x0 * src.width), Mathf.Round(y0 * src.height),
                        Mathf.Round((x1 - x0) * src.width), Mathf.Round((y1 - y0) * src.height));
                    Texture2D tex = BakeRegion(src, r);
                    if (tex == null) return null;
                    int flipped = System.Convert.ToInt32(dt.GetField("flipped").GetValue(def));
                    if (flipped == 1) tex = TurnLeft(tex);
                    note = "tk2d " + n + " " + tex.width + "x" + tex.height + (flipped == 1 ? " (повёрнут)" : "");
                    return tex;
                }
            }
            return null;
        }

        /// Поворот картинки на 90 градусов против часовой стрелки.
        static Texture2D TurnLeft(Texture2D tex)
        {
            int w = tex.width, h = tex.height;
            Color32[] src = tex.GetPixels32();
            Color32[] dst = new Color32[src.Length];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    dst[x * h + (h - 1 - y)] = src[y * w + x];
            Texture2D turned = new Texture2D(h, w, TextureFormat.RGBA32, false);
            turned.SetPixels32(dst);
            turned.Apply(false);
            turned.filterMode = FilterMode.Bilinear;
            turned.wrapMode = TextureWrapMode.Clamp;
            Object.Destroy(tex);
            return turned;
        }

        /// Спрайт из набора ресурсов Gods of Pharloom.
        static Sprite Bundle(string name)
        {
            if (Preload.bundleResources == null) return null;
            object o;
            if (!Preload.bundleResources.TryGetValue(name, out o)) return null;
            return o as Sprite;
        }

        /// Значок предмета инвентаря с таким именем. Сначала там, где его ищет GoP (Radial Layout / Cloak States), потом где угодно.
        static Sprite Inventory(string objectName, out string note)
        {
            return Inventory(objectName, null, out note);
        }

        /// То же, но с жёстким родителем: предмет с таким именем прямо под объектом parentName (null — как выше).
        static Sprite Inventory(string objectName, string parentName, out string note)
        {
            note = "";
            if (scan == null) scan = Resources.FindObjectsOfTypeAll<Transform>();
            int passes = parentName == null ? 2 : 1;
            for (int pass = 0; pass < passes; pass++)
            {
                for (int i = 0; i < scan.Length; i++)
                {
                    Transform t = scan[i];
                    if (t == null || t.name != objectName) continue;
                    if (parentName != null)
                    {
                        if (t.parent == null || t.parent.name != parentName) continue;
                    }
                    else if (pass == 0)
                    {
                        string parent = t.parent != null ? t.parent.name : "";
                        if (parent != "Radial Layout" && parent != "Cloak States") continue;
                    }
                    Sprite s = IconOf(t);
                    if (s == null) continue;
                    note = "инвентарь: " + Path(t) + " (спрайт " + s.name + ")";
                    return s;
                }
            }
            return null;
        }

        /// Самый «значковый» спрайт среди самого объекта и его детей: «Icon», иначе самый большой, без подсветок и рамок.
        static Sprite IconOf(Transform t)
        {
            SpriteRenderer[] all = t.GetComponentsInChildren<SpriteRenderer>(true);
            Sprite best = null;
            float bestScore = -1f;
            for (int i = 0; i < all.Length; i++)
            {
                SpriteRenderer r = all[i];
                Sprite s = r.sprite;
                if (s == null) continue;
                string n = r.name.ToLowerInvariant();
                if (n.Contains("orb") || n.Contains("flash") || n.Contains("glow") || n.Contains("frame") || n.Contains("back")
                    || n.Contains("shadow") || n.Contains("select") || n.Contains("arrow") || n.Contains("amount")) continue;
                float score = s.rect.width * s.rect.height + (n == "icon" ? 1e9f : 0f);
                if (score > bestScore) { bestScore = score; best = s; }
            }
            return best;
        }

        static string Path(Transform t)
        {
            string p = t.name;
            int depth = 0;
            while (t.parent != null && depth < 6) { t = t.parent; p = t.name + "/" + p; depth++; }
            return p;
        }

        /// Стирает с картинки обрывки, которые не относятся к самому значку: у навыков в инвентаре к каждому спрайту прилипает кусочек
        /// кольца вокруг катушки, у масок — клочок тени. Стирается всякая отдельная часть картинки, которая касается края и меньше
        /// 15 % самой большой части. Цельный значок (игла, маска, шапка) не страдает; у «Ударной иглы» могут пропасть крайние искры.
        static void DropStrays(Texture2D tex)
        {
            int w = tex.width, h = tex.height;
            if (w < 8 || h < 8 || w * h > 1048576) return;
            Color32[] px = tex.GetPixels32();
            int total = px.Length;
            int[] label = new int[total];
            int[] stack = new int[total];
            List<int> areas = new List<int>();
            List<bool> edge = new List<bool>();
            for (int start = 0; start < total; start++)
            {
                if (px[start].a < 24 || label[start] != 0) continue;
                int id = areas.Count + 1;
                int sp = 0, area = 0;
                bool touches = false;
                stack[sp++] = start;
                label[start] = id;
                while (sp > 0)
                {
                    int p = stack[--sp];
                    area++;
                    int x = p % w, y = p / w;
                    if (x == 0 || y == 0 || x == w - 1 || y == h - 1) touches = true;
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        int ny = y + dy;
                        if (ny < 0 || ny >= h) continue;
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx = x + dx;
                            if (nx < 0 || nx >= w || (dx == 0 && dy == 0)) continue;
                            int q = ny * w + nx;
                            if (label[q] != 0 || px[q].a < 24) continue;
                            label[q] = id;
                            stack[sp++] = q;
                        }
                    }
                }
                areas.Add(area);
                edge.Add(touches);
            }
            if (areas.Count < 2) return;
            int biggest = 0;
            for (int i = 0; i < areas.Count; i++) if (areas[i] > biggest) biggest = areas[i];
            bool changed = false;
            for (int i = 0; i < total; i++)
            {
                int l = label[i];
                if (l == 0) continue;
                if (edge[l - 1] && areas[l - 1] < biggest * 0.15f)
                {
                    px[i].a = 0;
                    changed = true;
                }
            }
            if (!changed) return;
            tex.SetPixels32(px);
            tex.Apply(false);
        }

        static Texture bakeSrc;
        static RenderTexture bakeRt;

        /// Отпускает временную копию атласа (она одна на все значки из одного атласа за один проход Pump).
        static void ReleaseBake()
        {
            if (bakeRt != null) RenderTexture.ReleaseTemporary(bakeRt);
            bakeRt = null;
            bakeSrc = null;
        }

        /// Вырезает спрайт из его текстуры (атласа) в отдельную читаемую текстуру.
        /// Атлас копируется в RenderTexture один раз на проход: инструментов много, и почти все лежат в одном атласе.
        static Texture2D Bake(Sprite sprite, bool strip)
        {
            Texture2D src = sprite.texture;
            if (src == null) return null;
            Texture2D tex = BakeRegion(src, sprite.textureRect);
            if (tex != null && strip) DropStrays(tex);
            return tex;
        }

        /// Вырезает прямоугольник (в пикселях, начало внизу слева) из текстуры.
        static Texture2D BakeRegion(Texture src, Rect r)
        {
            int w = Mathf.Max(1, (int)r.width), h = Mathf.Max(1, (int)r.height);
            RenderTexture previous = RenderTexture.active;
            try
            {
                if (bakeRt == null || bakeSrc != src)
                {
                    ReleaseBake();
                    bakeRt = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
                    Graphics.Blit(src, bakeRt);
                    bakeSrc = src;
                }
                RenderTexture.active = bakeRt;
                Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(r.x, r.y, w, h), 0, 0);
                tex.Apply(false);
                tex.filterMode = FilterMode.Bilinear;
                tex.wrapMode = TextureWrapMode.Clamp;
                return tex;
            }
            finally
            {
                RenderTexture.active = previous;
            }
        }
    }
}
