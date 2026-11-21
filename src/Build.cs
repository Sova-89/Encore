// ============================================================================
//  Build.cs — сборка Хорнет: привязки Gods of Pharloom и отключение навыков (0.25.0).
//  Привязки (Needle, Silk, Tools, Mask) GoP хранит флагами в PlayerDataMod.bindings, а их действие создают его же проверки и циклы-опросы
//  (урон иглы, шёлк, инструменты, маски), поэтому Encore только переключает флаг и сохраняет данные GoP — как кнопка в меню GoP.
//  Навыки (рывок, гарпун, суперпрыжок, стенопрыжок, нидолин, ударная игла, плащи, лечение Евы) GoP отключает прямо в данных игрока
//  (PlayerData.hasDash и другие) — так же делает и Encore. Какие навыки отключил именно он, запоминается в настройке Build/SkillsOff:
//  это и есть «сборка» для статистики (BuildMath.Variant); перед каждой попыткой сегмента эти навыки отключаются заново.
//  Всё действует на всю игру (не по боссам) и меняется только вне сегмента (как и в самом GoP).
//  0.27.0: снаряжение, как в меню привязок GoP, — игла, маски, шёлк, сердца шёлка, сумка и набор (числа по кругу), плащи (4 состояния),
//  Хорнет без креста и проклятая Хорнет, крест и инструменты в его слотах. Всё это данные игрока и инвентаря, а не привязки: Encore
//  меняет их теми же вызовами, что и GoP, и тоже только вне сегмента. В метку сборки для статистики числа НЕ входят (у GoP их тоже нет);
//  плащи идут через тот же список отключённых навыков, что и «Плащ странника» / «Плащ Фейдаун» в ряду навыков.
//  Что менять: новый навык — запись в Skills здесь и код в BuildMath.SkillCodes (тот же порядок).
// ============================================================================
using System;
using System.Collections.Generic;
using System.Reflection;
using Gods_Of_Pharloom;
using GlobalSettings;
using UnityEngine;

namespace Encore
{
    internal static class Build
    {
        /// Один навык: код для метки и настройки, названия, чтение и запись флага в данных игрока.
        internal sealed class Skill
        {
            internal string Code, Ru, En;
            internal Func<PlayerData, bool> Get;
            internal Action<PlayerData, bool> Set;
        }

        /// В том же порядке, что BuildMath.SkillCodes (проверяет Check).
        internal static readonly Skill[] Skills =
        {
            new Skill { Code = "dash", Ru = "Рывок (Swift Step)", En = "Swift Step", Get = p => p.hasDash, Set = (p, v) => p.hasDash = v },
            new Skill { Code = "claw", Ru = "Гарпун (Clawline)", En = "Clawline", Get = p => p.hasHarpoonDash, Set = (p, v) => p.hasHarpoonDash = v },
            new Skill { Code = "eva", Ru = "Лечение Евы (Eva Heal)", En = "Eva Heal", Get = p => p.HasBoundCrestUpgrader, Set = (p, v) => p.HasBoundCrestUpgrader = v },
            new Skill { Code = "soar", Ru = "Суперпрыжок (Silk Soar)", En = "Silk Soar", Get = p => p.hasSuperJump, Set = (p, v) => p.hasSuperJump = v },
            new Skill { Code = "grip", Ru = "Стенопрыжок (Cling Grip)", En = "Cling Grip", Get = p => p.hasWalljump, Set = (p, v) => p.hasWalljump = v },
            new Skill { Code = "needolin", Ru = "Нидолин (Needolin)", En = "Needolin", Get = p => p.hasNeedolin, Set = (p, v) => p.hasNeedolin = v },
            new Skill { Code = "strike", Ru = "Ударная игла (Needle Strike)", En = "Needle Strike", Get = p => p.hasChargeSlash, Set = (p, v) => p.hasChargeSlash = v },
            new Skill { Code = "drift", Ru = "Плащ странника (Drifter's Cloak)", En = "Drifter's Cloak", Get = p => p.hasBrolly, Set = (p, v) => p.hasBrolly = v },
            new Skill { Code = "fay", Ru = "Плащ Фейдаун (Faydown Cloak)", En = "Faydown Cloak", Get = p => p.hasDoubleJump, Set = (p, v) => p.hasDoubleJump = v },
        };

        /// Ключи привязок в данных GoP, в порядке BuildMath.Letters (N, S, T, M).
        static readonly string[] BindingKeys = { "Needle Binding", "Silk Binding", "Tools Binding", "Mask Binding" };

        internal static string BindingName(int i)
        {
            return i == 0 ? "Needle" : i == 1 ? "Silk" : i == 2 ? "Tools" : "Mask";
        }

        /// Что делает привязка (по тому, как её применяет GoP).
        internal static string BindingInfo(int i)
        {
            switch (i)
            {
                case 0: return L.T("Игла слабее: урон иглы урезан.", "Weaker needle: needle damage is capped.");
                case 1: return L.T("Шёлка меньше: катушка урезана.", "Less silk: the spool is cut down.");
                case 2: return L.T("Инструменты отключены, остаются только шёлковые навыки.", "Tools are disabled, only silk skills stay.");
                default: return L.T("Масок меньше: здоровье урезано.", "Fewer masks: health is cut down.");
            }
        }

        /// Названия навыка на текущем языке.
        internal static string SkillName(Skill s)
        {
            return L.IsRu ? s.Ru : s.En;
        }

        /// Таблица навыков и коды в BuildMath совпадают (нужно, чтобы метка статистики и кнопки говорили об одном и том же).
        internal static bool Check()
        {
            if (Skills.Length != BuildMath.SkillCodes.Length) return false;
            for (int i = 0; i < Skills.Length; i++)
                if (Skills[i].Code != BuildMath.SkillCodes[i]) return false;
            return true;
        }

        // ---- привязки ------------------------------------------------------------

        /// Включена ли привязка i (0 Needle, 1 Silk, 2 Tools, 3 Mask). Нет данных GoP — выключена.
        internal static bool Binding(int i)
        {
            PlayerDataMod mod = PlayerDataMod.instance;
            if (mod == null || mod.bindings == null || i < 0 || i >= BindingKeys.Length) return false;
            bool on;
            return mod.bindings.TryGetValue(BindingKeys[i], out on) && on;
        }

        /// Можно ли сейчас менять привязки и навыки. Нет — reason говорит почему.
        internal static bool CanChange(out string reason)
        {
            reason = "";
            if (PlayerDataMod.instance == null || PlayerData.instance == null)
            {
                reason = L.T("Сначала загрузи сохранение.", "Load a save first.");
                return false;
            }
            if (Runner.Active || Gods_Of_Pharloom.BossSequence.isInSequence)
            {
                reason = L.T("Во время сегмента менять нельзя.", "Cannot be changed during a segment.");
                return false;
            }
            return true;
        }

        /// Переключает привязку i. message — что сказать игроку (и когда не вышло).
        internal static bool ToggleBinding(int i, out string message)
        {
            return SetBinding(i, !Binding(i), out message);
        }

        /// Ставит привязку i. Данные GoP сохраняются, значки в его меню обновляются.
        internal static bool SetBinding(int i, bool on, out string message)
        {
            message = "";
            if (i < 0 || i >= BindingKeys.Length) return false;
            if (!CanChange(out message)) return false;
            if (Binding(i) != on)
            {
                PlayerDataMod.instance.bindings[BindingKeys[i]] = on;
                Persist();
            }
            message = BindingName(i) + ": " + (on ? L.T("включена", "on") : L.T("выключена", "off"));
            return true;
        }

        /// Все четыре привязки сразу.
        internal static bool SetAllBindings(bool on, out string message)
        {
            message = "";
            if (!CanChange(out message)) return false;
            bool changed = false;
            for (int i = 0; i < BindingKeys.Length; i++)
                if (Binding(i) != on)
                {
                    PlayerDataMod.instance.bindings[BindingKeys[i]] = on;
                    changed = true;
                }
            if (changed) Persist();
            message = on ? L.T("Все привязки включены.", "All bindings on.") : L.T("Все привязки сняты.", "All bindings off.");
            return true;
        }

        /// Сохраняет данные GoP и обновляет значки в его меню привязок (ошибка тут не мешает: само переключение уже сделано).
        static void Persist()
        {
            try { if (GodsOfPharloomMod.instance != null) GodsOfPharloomMod.instance.SaveModData(); }
            catch (Exception ex) { Plugin.Caught("Build/SaveModData", ex); }
            try { if (BindingsMenu.menuBindingsFsm != null) BindingsMenu.UpdateMenuBindingsDisplay(); }
            catch (Exception ex) { Plugin.Caught("Build/UpdateMenuBindingsDisplay", ex); }
        }

        // ---- навыки --------------------------------------------------------------

        /// Навык отключён мной (Encore)?
        internal static bool IsOff(Skill s)
        {
            return BuildMath.HasSkill(Plugin.SkillsOff, s.Code);
        }

        /// Навык сейчас есть у Хорнет (флаг включён).
        internal static bool Has(Skill s)
        {
            PlayerData pd = PlayerData.instance;
            return pd != null && s.Get(pd);
        }

        /// Отключает навык (off = true) или возвращает его (off = false). Возвращает только то, что отключил сам Encore:
        /// навык, которого в сохранении никогда не было, он не выдаёт.
        internal static bool SetSkill(Skill s, bool off, out string message)
        {
            message = "";
            if (!CanChange(out message)) return false;
            PlayerData pd = PlayerData.instance;
            if (off)
            {
                if (!s.Get(pd) && !IsOff(s))
                {
                    message = SkillName(s) + L.T(": такого навыка нет в сохранении.", ": the save does not have this skill.");
                    return false;
                }
                Plugin.SkillsOff = BuildMath.WithSkill(Plugin.SkillsOff, s.Code, true);
                s.Set(pd, false);
            }
            else
            {
                if (!IsOff(s))
                {
                    message = SkillName(s) + L.T(": Encore его не отключал.", ": Encore did not turn it off.");
                    return false;
                }
                Plugin.SkillsOff = BuildMath.WithSkill(Plugin.SkillsOff, s.Code, false);
                s.Set(pd, true);
            }
            message = SkillName(s) + ": " + (off ? L.T("отключён", "off") : L.T("включён", "on"));
            return true;
        }

        /// Возвращает все навыки, которые отключил Encore.
        internal static bool ReturnSkills(out string message)
        {
            message = "";
            if (!CanChange(out message)) return false;
            PlayerData pd = PlayerData.instance;
            int n = 0;
            foreach (Skill s in Skills)
                if (IsOff(s)) { s.Set(pd, true); n++; }
            Plugin.SkillsOff = "";
            message = L.T("Навыки возвращены: ", "Skills returned: ") + n;
            return true;
        }

        /// Перед попыткой сегмента: навыки из настройки отключены в игре (их могли включить в меню GoP). Возвращает, сколько пришлось отключить.
        internal static int ApplySkills()
        {
            PlayerData pd = PlayerData.instance;
            string off = Plugin.SkillsOff;
            if (pd == null || off.Length == 0) return 0;
            int n = 0;
            foreach (Skill s in Skills)
                if (BuildMath.HasSkill(off, s.Code) && s.Get(pd)) { s.Set(pd, false); n++; }
            return n;
        }

        // ---- снаряжение (0.27.0) -------------------------------------------------

        /// Название числового снаряжения (номера — BuildMath.Gear*).
        internal static string GearName(int gear)
        {
            switch (gear)
            {
                case BuildMath.GearNeedle: return L.T("Игла (уровень)", "Needle (level)");
                case BuildMath.GearMasks: return L.T("Маски", "Masks");
                case BuildMath.GearSilk: return L.T("Катушка шёлка", "Silk spool");
                case BuildMath.GearRegen: return L.T("Сердца шёлка", "Silk hearts");
                case BuildMath.GearPouch: return L.T("Сумка для инструментов", "Tool pouch");
                default: return L.T("Набор для ремонта", "Crafting kit");
            }
        }

        /// Текущее значение. Нет сохранения — 0.
        internal static int Gear(int gear)
        {
            PlayerData pd = PlayerData.instance;
            if (pd == null) return 0;
            switch (gear)
            {
                case BuildMath.GearNeedle: return pd.nailUpgrades;
                case BuildMath.GearMasks: return pd.maxHealth;
                case BuildMath.GearSilk: return pd.silkMax;
                case BuildMath.GearRegen: return pd.silkRegenMax;
                case BuildMath.GearPouch: return pd.ToolPouchUpgrades;
                default: return pd.ToolKitUpgrades;
            }
        }

        /// Наибольшее значение сейчас (привязки Mask и Silk его урезают, как у GoP).
        internal static int GearTop(int gear)
        {
            return BuildMath.GearMax(gear, Binding(3), BindingsMenu.maskBindingCount, Binding(1));
        }

        /// Шаг снаряжения по кругу: dir > 0 — вперёд (левая кнопка), иначе назад (правая). Правила и границы — как у кнопок в меню GoP.
        internal static bool StepGear(int gear, int dir, out string message)
        {
            message = "";
            if (gear < 0 || gear >= BuildMath.GearCount) return false;
            if (!CanChange(out message)) return false;
            PlayerData pd = PlayerData.instance;
            int cur = Gear(gear);
            int next = BuildMath.Step(cur, dir, BuildMath.GearMin(gear), GearTop(gear));
            if (next != cur)
            {
                switch (gear)
                {
                    case BuildMath.GearNeedle: pd.nailUpgrades = next; break;
                    case BuildMath.GearMasks:
                        if (BindingsMenu.isHealthIncreasing)
                        {
                            message = L.T("Маски ещё меняются — подожди секунду.", "The masks are still changing — wait a second.");
                            return false;
                        }
                        SetMasks(next);
                        break;
                    case BuildMath.GearSilk: pd.silkMax = next; break;
                    case BuildMath.GearRegen: pd.silkRegenMax = next; break;
                    case BuildMath.GearPouch: pd.ToolPouchUpgrades = next; break;
                    default: pd.ToolKitUpgrades = next; break;
                }
                Persist();
            }
            message = GearName(gear) + ": " + next;
            return true;
        }

        /// Маски как у GoP: игровая корутина перерисовывает сердца на экране. Нет его меню или экрана игры — просто числа в данных.
        static void SetMasks(int n)
        {
            PlayerData pd = PlayerData.instance;
            PlayerDataMod pdm = PlayerDataMod.instance;
            if (Binding(3) && pdm != null) pdm.previousHealthCount = n;
            bool hud = false;
            try
            {
                GameCameras gc = GameCameras.instance;
                hud = gc != null && gc.hudCanvasSlideOut != null && GodsOfPharloomMod.instance != null && BindingsMenu.menuBindingsFsm != null;
            }
            catch (Exception ex) { Plugin.Caught("Build/MasksHud", ex); }
            if (hud)
            {
                try { GodsOfPharloomMod.instance.StartCoroutine(BindingsMenu.TrySetHeroHealth(n)); return; }
                catch (Exception ex) { Plugin.Caught("Build/TrySetHeroHealth", ex); }
            }
            pd.maxHealth = n;
            pd.maxHealthBase = n;
            pd.health = n;
        }

        /// Состояние плащей 0..3 (BuildMath.CloakState).
        internal static int Cloak()
        {
            PlayerData pd = PlayerData.instance;
            return pd == null ? 0 : BuildMath.CloakState(pd.hasBrolly, pd.hasDoubleJump);
        }

        /// Следующее (dir > 0) или предыдущее состояние плащей, как красная шапка у GoP. Плащ, который был и пропал, попадает в список
        /// отключённых навыков (как в ряду навыков), плащ, который появился, — выходит из него.
        internal static bool StepCloak(int dir, out string message)
        {
            message = "";
            if (!CanChange(out message)) return false;
            int next = BuildMath.Step(Cloak(), dir, 0, 3);
            SetCloakFlag(Skills[7], (next & 1) != 0);
            SetCloakFlag(Skills[8], (next & 2) != 0);
            Persist();
            message = L.T("Плащи: ", "Cloaks: ") + (next == 0 ? L.T("нет", "none") : next == 1 ? SkillName(Skills[7]) : next == 2 ? SkillName(Skills[8]) : L.T("оба", "both"));
            return true;
        }

        static void SetCloakFlag(Skill s, bool want)
        {
            PlayerData pd = PlayerData.instance;
            bool has = s.Get(pd);
            if (want)
            {
                if (!has) s.Set(pd, true);
                Plugin.SkillsOff = BuildMath.WithSkill(Plugin.SkillsOff, s.Code, false);
            }
            else if (has)
            {
                s.Set(pd, false);
                Plugin.SkillsOff = BuildMath.WithSkill(Plugin.SkillsOff, s.Code, true);
            }
        }

        // ---- кресты и инструменты ------------------------------------------------

        static string crestBefore = "";

        /// Надет ли «крестовый» режим: Хорнет без креста (cursed = false) или проклятая Хорнет (cursed = true).
        internal static bool SpecialCrestOn(bool cursed)
        {
            try
            {
                ToolCrest c = cursed ? Gameplay.CursedCrest : Gameplay.CloaklessCrest;
                return c != null && c.IsEquipped;
            }
            catch (Exception ex) { Plugin.Caught("Build/SpecialCrestOn", ex); return false; }
        }

        static bool IsSpecialCrest(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            try { return name == Gameplay.CloaklessCrest.name || name == Gameplay.CursedCrest.name; }
            catch (Exception) { return false; }
        }

        /// Включает или выключает «Хорнет без креста» / «Проклятая Хорнет» (так же, как GoP: AutoEquip). Выключая, возвращает тот крест,
        /// что был до этого (если не помним — лучший охотничий, как у GoP).
        internal static bool ToggleSpecialCrest(bool cursed, out string message)
        {
            message = "";
            if (!CanChange(out message)) return false;
            try
            {
                ToolCrest target = cursed ? Gameplay.CursedCrest : Gameplay.CloaklessCrest;
                if (target == null)
                {
                    message = L.T("Такого креста в игре нет.", "The game has no such crest.");
                    return false;
                }
                if (target.IsEquipped)
                {
                    ToolCrest back = null;
                    if (crestBefore.Length > 0)
                    {
                        back = ToolItemManager.GetCrestByName(crestBefore);
                        if (back != null && !back.IsUnlocked) back = null;
                    }
                    if (back == null)
                    {
                        back = Gameplay.HunterCrest3;
                        if (!back.IsUnlocked) back = Gameplay.HunterCrest2;
                        if (!back.IsUnlocked) back = Gameplay.HunterCrest;
                    }
                    ToolItemManager.AutoEquip(back, false, false);
                }
                else
                {
                    string now = PlayerData.instance.CurrentCrestID;
                    if (!string.IsNullOrEmpty(now) && !IsSpecialCrest(now)) crestBefore = now;
                    ToolItemManager.AutoEquip(target, false, false);
                }
                Persist();
                message = (cursed ? L.T("Проклятая Хорнет: ", "Cursed Hornet: ") : L.T("Хорнет без креста: ", "Crestless Hornet: "))
                    + (target.IsEquipped ? L.T("надета", "on") : L.T("снята", "off"));
                return true;
            }
            catch (Exception ex)
            {
                Plugin.Caught("Build/ToggleSpecialCrest", ex);
                message = L.T("Не вышло: ", "Failed: ") + ex.Message;
                return false;
            }
        }

        // Плитки крестов: по одной на семью (у Охотницы три уровня — одна плитка, уровни переключаются нажатием, как плащи).
        static List<ToolCrest> crestCache = new List<ToolCrest>();
        static float crestCacheAt = -10f;
        static List<List<ToolCrest>> crestFamilies = new List<List<ToolCrest>>();
        // уровень (1, 2, 3…) и число открытых уровней креста по его имени; пусто — у креста один уровень
        static Dictionary<string, int> crestLevel = new Dictionary<string, int>();
        static Dictionary<string, int> crestLevels = new Dictionary<string, int>();
        // какой уровень семьи показывать, пока она не надета: имя первой версии семьи → имя выбранной
        static Dictionary<string, string> crestPicked = new Dictionary<string, string>();
        static FieldInfo previousField;
        static bool previousTried;

        /// Предыдущая версия креста (у Охотницы 3 → 2 → 1). Поле закрытое, поэтому через отражение; нет поля — null.
        static ToolCrest PreviousOf(ToolCrest c)
        {
            if (c == null) return null;
            try
            {
                if (!previousTried)
                {
                    previousTried = true;
                    previousField = typeof(ToolCrest).GetField("previousVersion", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                }
                if (previousField == null) return null;
                return previousField.GetValue(c) as ToolCrest;
            }
            catch (Exception) { return null; }
        }

        /// Кресты, которые можно надеть, по одной плитке на крест: у Охотницы одна плитка на три уровня — показан надетый уровень, а пока она
        /// не надета, выбранный в прошлый раз (сначала высший). Без «без креста» и «проклятой» (у них свои плитки). Список пересобирается
        /// раз в полсекунды и сразу после нажатия: вызывается каждый кадр. Не вышло узнать версии креста — как раньше, только высшая.
        internal static List<ToolCrest> Crests()
        {
            float now = Time.unscaledTime;
            if (now - crestCacheAt < 0.5f && crestCache.Count > 0) return crestCache;
            crestCacheAt = now;
            List<ToolCrest> list = new List<ToolCrest>();
            List<List<ToolCrest>> families = new List<List<ToolCrest>>();
            Dictionary<string, int> level = new Dictionary<string, int>();
            Dictionary<string, int> levels = new Dictionary<string, int>();
            try
            {
                List<ToolCrest> all = ToolItemManager.GetAllCrests();
                bool known = false;
                for (int i = 0; all != null && i < all.Count && !known; i++) known = PreviousOf(all[i]) != null;
                Dictionary<string, ToolCrest> byName = new Dictionary<string, ToolCrest>();
                List<CrestMath.Entry> entries = new List<CrestMath.Entry>();
                for (int i = 0; all != null && i < all.Count; i++)
                {
                    ToolCrest c = all[i];
                    if (c == null || IsSpecialCrest(c.name) || byName.ContainsKey(c.name)) continue;
                    bool open = c.IsUnlocked && (known || !c.IsUpgradedVersionUnlocked);
                    ToolCrest prev = known ? PreviousOf(c) : null;
                    byName[c.name] = c;
                    entries.Add(new CrestMath.Entry(c.name, prev != null ? prev.name : "", open));
                }
                PlayerData pd = PlayerData.instance;
                string current = pd != null ? pd.CurrentCrestID : "";
                List<List<string>> names = CrestMath.Families(entries, level, levels);
                for (int f = 0; f < names.Count; f++)
                {
                    List<ToolCrest> members = new List<ToolCrest>();
                    for (int m = 0; m < names[f].Count; m++) members.Add(byName[names[f][m]]);
                    string picked;
                    crestPicked.TryGetValue(names[f][0], out picked);
                    string rep = CrestMath.Representative(names[f], current, picked);
                    families.Add(members);
                    list.Add(byName[rep]);
                }
            }
            catch (Exception ex) { Plugin.Caught("Build/Crests", ex); }
            crestCache = list;
            crestFamilies = families;
            crestLevel = level;
            crestLevels = levels;
            return list;
        }

        /// Уровень креста (1, 2, 3) и сколько уровней открыто; 0 — у креста один уровень, номер не нужен.
        internal static int CrestLevel(ToolCrest c, out int of)
        {
            of = 0;
            if (c == null) return 0;
            int n;
            if (!crestLevel.TryGetValue(c.name, out n)) return 0;
            crestLevels.TryGetValue(c.name, out of);
            return n;
        }

        /// Семья креста: все его открытые уровни (низший первым). Одному кресту — список из него одного; не нашёлся — null.
        static List<ToolCrest> FamilyOf(ToolCrest c)
        {
            if (c == null) return null;
            for (int f = 0; f < crestFamilies.Count; f++)
                for (int m = 0; m < crestFamilies[f].Count; m++)
                    if (crestFamilies[f][m].name == c.name) return crestFamilies[f];
            return null;
        }

        /// Запоминает выбранный уровень семьи (для плитки, пока крест не надет) и просит пересобрать список.
        static void RememberCrest(ToolCrest c)
        {
            List<ToolCrest> family = FamilyOf(c);
            if (family != null && family.Count > 0) crestPicked[family[0].name] = c.name;
            crestCacheAt = -10f;
        }

        /// Переключает уровень креста по кругу, как плащи: dir = 1 — следующий, −1 — предыдущий; сразу надевает его. У креста с одним уровнем
        /// ничего не делает.
        internal static bool StepCrest(ToolCrest c, int dir, out string message)
        {
            message = "";
            List<ToolCrest> family = FamilyOf(c);
            if (family == null || family.Count < 2) return false;
            List<string> names = new List<string>();
            for (int i = 0; i < family.Count; i++) names.Add(family[i].name);
            string next = CrestMath.Step(names, c.name, dir);
            for (int i = 0; i < family.Count; i++)
                if (family[i].name == next) return EquipCrest(family[i], out message);
            return false;
        }

        /// Крест, надетый сейчас (в том числе «без креста» и «проклятый»), или null.
        internal static ToolCrest CurrentCrest()
        {
            PlayerData pd = PlayerData.instance;
            if (pd == null || string.IsNullOrEmpty(pd.CurrentCrestID)) return null;
            try { return ToolItemManager.GetCrestByName(pd.CurrentCrestID); }
            catch (Exception) { return null; }
        }

        /// Это тот крест, что надет сейчас?
        internal static bool CrestIsOn(ToolCrest c)
        {
            PlayerData pd = PlayerData.instance;
            return c != null && pd != null && c.name == pd.CurrentCrestID;
        }

        /// Надевает крест (как GoP для «без креста»: инструменты на кресте остаются как были).
        internal static bool EquipCrest(ToolCrest c, out string message)
        {
            message = "";
            if (!CanChange(out message)) return false;
            if (c == null) return false;
            try
            {
                ToolItemManager.AutoEquip(c, false, false);
                Persist();
                RememberCrest(c);
                int of;
                int level = CrestLevel(c, out of);
                message = L.T("Крест надет: ", "Crest on: ") + Label(c) + (level > 0 ? L.T(" (уровень ", " (level ") + level + ")" : "");
                return true;
            }
            catch (Exception ex)
            {
                Plugin.Caught("Build/EquipCrest", ex);
                message = L.T("Не вышло: ", "Failed: ") + ex.Message;
                return false;
            }
        }

        /// Один слот креста: номер, цвет (тип), что в нём надето, можно ли в него класть сейчас.
        internal sealed class SlotView
        {
            internal int Index;
            internal ToolItemType Type;
            internal string Tool = "";
            internal bool Open;      // слот открыт (не заперт в сохранении)
            internal bool Allowed;   // и привязка Tools его не закрывает
        }

        /// Слоты креста с тем, что в них надето (данные сохранения). Нет данных — пустой список.
        internal static List<SlotView> Slots(ToolCrest crest)
        {
            List<SlotView> list = new List<SlotView>();
            PlayerData pd = PlayerData.instance;
            if (crest == null || pd == null) return list;
            try
            {
                ToolCrest.SlotInfo[] info = crest.Slots;
                List<ToolCrestsData.SlotData> saved = pd.ToolEquips.GetData(crest.name).Slots;
                if (info == null || saved == null) return list;
                bool toolsOff = Binding(2);
                for (int i = 0; i < info.Length && i < saved.Count; i++)
                {
                    SlotView v = new SlotView();
                    v.Index = i;
                    v.Type = info[i].Type;
                    v.Tool = saved[i].EquippedTool ?? "";
                    v.Open = !info[i].IsLocked || saved[i].IsUnlocked;
                    v.Allowed = v.Open && (!toolsOff || v.Type == ToolItemType.Skill);
                    list.Add(v);
                }
            }
            catch (Exception ex) { Plugin.Caught("Build/Slots", ex); }
            return list;
        }

        /// Открытые у игрока инструменты данного цвета (для выбора в слот).
        internal static List<ToolItem> ToolsOf(ToolItemType type)
        {
            List<ToolItem> list = new List<ToolItem>();
            try
            {
                foreach (ToolItem t in ToolItemManager.GetUnlockedTools())
                    if (t != null && t.Type == type) list.Add(t);
            }
            catch (Exception ex) { Plugin.Caught("Build/ToolsOf", ex); }
            return list;
        }

        /// Кладёт инструмент в слот креста (toolName пустое — очищает слот). Один и тот же инструмент не стоит в двух слотах одного креста.
        internal static bool SetSlot(ToolCrest crest, int slot, string toolName, out string message)
        {
            message = "";
            if (!CanChange(out message)) return false;
            if (crest == null) return false;
            try
            {
                List<SlotView> slots = Slots(crest);
                if (slot < 0 || slot >= slots.Count) return false;
                if (!slots[slot].Allowed)
                {
                    message = slots[slot].Open ? L.T("Привязка Tools: инструменты отключены.", "Tools binding: tools are disabled.")
                                               : L.T("Этот слот ещё закрыт.", "This slot is still locked.");
                    return false;
                }
                List<string> names = new List<string>();
                for (int i = 0; i < slots.Count; i++) names.Add(slots[i].Tool);
                if (!string.IsNullOrEmpty(toolName))
                    for (int i = 0; i < names.Count; i++)
                        if (i != slot && names[i] == toolName) names[i] = "";
                names[slot] = toolName ?? "";
                ToolItemManager.SetEquippedTools(crest.name, names);
                ToolItemManager.SendEquippedChangedEvent(false);
                message = string.IsNullOrEmpty(toolName) ? L.T("Слот очищен.", "Slot cleared.") : L.T("Инструмент надет: ", "Tool on: ") + ToolLabel(toolName);
                return true;
            }
            catch (Exception ex)
            {
                Plugin.Caught("Build/SetSlot", ex);
                message = L.T("Не вышло: ", "Failed: ") + ex.Message;
                return false;
            }
        }

        /// Имя креста на языке игры (нет — внутреннее имя).
        internal static string Label(ToolCrest c)
        {
            if (c == null) return "";
            try { string t = c.DisplayName.ToString(); if (!string.IsNullOrEmpty(t)) return t; }
            catch (Exception) { }
            return c.name;
        }

        /// Имя инструмента на языке игры по внутреннему имени (нет — само имя).
        internal static string ToolLabel(string toolName)
        {
            if (string.IsNullOrEmpty(toolName)) return "";
            try
            {
                ToolItem t = ToolItemManager.GetToolByName(toolName);
                if (t != null) { string text = t.DisplayName.ToString(); if (!string.IsNullOrEmpty(text)) return text; }
            }
            catch (Exception) { }
            return toolName;
        }

        // ---- сборка для статистики -----------------------------------------------

        static int cachedMask = -1;
        static string cachedOff;
        static string cachedVariant = "";

        /// Метка сборки сейчас (BuildMath.Variant): привязки GoP и навыки, отключённые Encore. Готовая строка запоминается:
        /// зовётся каждый кадр, пока открыта вкладка статистики.
        internal static string Variant()
        {
            int mask = 0;
            for (int i = 0; i < BindingKeys.Length; i++)
                if (Binding(i)) mask |= 1 << i;
            string off = Plugin.SkillsOff;
            if (mask == cachedMask && string.Equals(off, cachedOff, StringComparison.Ordinal)) return cachedVariant;
            var bindings = new bool[BindingKeys.Length];
            for (int i = 0; i < bindings.Length; i++) bindings[i] = (mask & (1 << i)) != 0;
            cachedMask = mask;
            cachedOff = off;
            cachedVariant = BuildMath.Variant(bindings, off);
            return cachedVariant;
        }

        /// Навыки из настройки действительно отключены в игре? Нет — сборка не та, что в метке, и бой в статистику не идёт.
        internal static bool SkillsApplied()
        {
            PlayerData pd = PlayerData.instance;
            string off = Plugin.SkillsOff;
            if (off.Length == 0) return true;
            if (pd == null) return false;
            foreach (Skill s in Skills)
                if (BuildMath.HasSkill(off, s.Code) && s.Get(pd)) return false;
            return true;
        }
    }
}
