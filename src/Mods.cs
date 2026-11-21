// ============================================================================
//  Mods.cs — перенесённые в Encore отдельные моды Abdulbasit:
//    • Hit Counter 1.1.4 (abdulbasit.silksong.hitcounter) — счётчик полученных ударов «HITS: N»;
//    • Always Double Incoming Damage 1.7.0 (abdulbasit.silksong.alwaysdoubleincoming) — множитель входящего урона.
//  Перенесены как есть: те же клавиши по умолчанию, те же надписи на экране, те же настройки
//  в тех же файлах BepInEx\config\abdulbasit.silksong.hitcounter.cfg / ...alwaysdoubleincoming.cfg,
//  поэтому прежние значения подхватываются сами. В отличие от Encore, работают во всей игре,
//  а не только в сегментах.
//  Если старые отдельные моды ещё стоят в plugins, встроенная копия молча отключается
//  (иначе урон считался бы дважды) и на экране появляется подсказка, что убрать.
//  Что менять: включить/выключить модуль — настройки Modules/* в sova.encore.cfg.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace Encore
{
    /// Запуск, опрос клавиш и рисование перенесённых модов.
    internal static class Mods
    {
        static ModsOverlay overlay;
        static bool drawing;

        /// Зовёт Plugin.Start — к этому времени BepInEx загрузил все плагины, и видно, стоят ли старые моды.
        internal static void Start(GameObject host, bool wantHitCounter, bool wantDoubleDamage)
        {
            var leftovers = new List<string>();
            if (wantHitCounter) Load("Hit Counter", HitCounterMod.OldGuid, "HitCounter.dll", HitCounterMod.Init, leftovers);
            if (wantDoubleDamage) Load("Always Double Incoming Damage", DoubleDamageMod.OldGuid, "AlwaysDoubleDamage.dll", DoubleDamageMod.Init, leftovers);

            overlay = host.AddComponent<ModsOverlay>();
            if (leftovers.Count > 0)
            {
                string names = string.Join(", ", leftovers.ToArray());
                Plugin.Toast(L.T(
                    "Encore: отдельный мод уже стоит (" + names + ") — встроенная копия выключена. Уберите его из plugins, чтобы пользоваться встроенной.",
                    "Encore: the standalone mod is still installed (" + names + ") — the built-in copy is off. Remove it from plugins to use the built-in one."), 12f);
            }
        }

        static void Load(string title, string oldGuid, string oldFile, Action init, List<string> leftovers)
        {
            try
            {
                if (OldPluginLoaded(oldGuid))
                {
                    Plugin.Log.LogWarning(title + ": отдельный мод " + oldGuid + " уже загружен — встроенная копия выключена (уберите " + oldFile + ").");
                    leftovers.Add(oldFile);
                    return;
                }
                init();
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError(title + ": не запустился: " + ex);
            }
        }

        static bool OldPluginLoaded(string guid)
        {
            try
            {
                PluginInfo info;
                return Chainloader.PluginInfos != null && Chainloader.PluginInfos.TryGetValue(guid, out info) && info != null;
            }
            catch (Exception) { return false; }
        }

        /// Файл настроек старого мода под его прежним именем: значения подхватываются как были.
        internal static ConfigFile OpenConfig(string guid, string name)
        {
            string path = System.IO.Path.Combine(Paths.ConfigPath, guid + ".cfg");
            return new ConfigFile(path, true, new BepInPlugin(guid, name, Plugin.Version));
        }

        /// Один класс перехватов в своём try: упавший перехват не должен ронять остальные и сам Encore.
        internal static void Patch(Type patches, string title)
        {
            try { Harmony.CreateAndPatchAll(patches, Plugin.Guid + "." + title); }
            catch (Exception ex) { Plugin.Log.LogError(title + ": перехват " + patches.Name + " не встал: " + ex.Message); }
        }

        /// Каждый кадр из Plugin.Update. Пока открыто окно Encore, клавиши модулей молчат (в окне можно печатать).
        internal static void Tick(bool menuOpen)
        {
            if (!menuOpen)
            {
                HitCounterMod.Tick();
                DoubleDamageMod.Tick();
            }
            bool want = HitCounterMod.WantsDraw || DoubleDamageMod.WantsDraw;
            if (overlay != null && want != drawing)
            {
                drawing = want;
                overlay.enabled = want;
            }
        }
    }

    /// Рисует плашки модулей. Включена, только пока есть что показывать.
    internal sealed class ModsOverlay : MonoBehaviour
    {
        void Awake()
        {
            useGUILayout = false; // раскладка не нужна: один вызов OnGUI на кадр вместо двух
            enabled = false;
        }

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;
            HitCounterMod.Draw();
            DoubleDamageMod.Draw();
        }
    }

    // ------------------------------------------------------------------ Hit Counter

    internal static class HitCounterMod
    {
        internal const string OldGuid = "abdulbasit.silksong.hitcounter";

        internal static bool Active;
        internal static int HitCount;
        // контекст атаки: «обычный» урон приходит из HeroController.TakeDamage, особый — из DoSpecialDamage
        internal static int NormalDepth, SpecialDepth;
        internal static string NormalSource;

        static ConfigEntry<KeyboardShortcut> resetKey;
        static ConfigEntry<bool> showCounter;
        static readonly HitFilter filter = new HitFilter();
        static GUIStyle style;
        static int shownCount = -1;
        static string shownText;

        internal static bool WantsDraw { get { return Active && showCounter.Value; } }

        internal static void Init()
        {
            ConfigFile cfg = Mods.OpenConfig(OldGuid, "Hit Counter");
            resetKey = cfg.Bind("Controls", "ResetKey", new KeyboardShortcut(KeyCode.P), "Reset the hit counter.");
            showCounter = cfg.Bind("Display", "ShowCounter", true, "Display the hit counter.");
            HitCount = 0;
            Reset();
            Mods.Patch(typeof(HitCounterNormalContext), "hitcounter");
            Mods.Patch(typeof(HitCounterSpecialContext), "hitcounter");
            Mods.Patch(typeof(HitCounterTakeHealth), "hitcounter");
            Active = true;
            Plugin.Log.LogInfo("Hit Counter 1.1.4 (встроен в Encore): сброс — " + resetKey.Value + ", счётчик " + (showCounter.Value ? "виден" : "скрыт"));
        }

        internal static void Reset()
        {
            NormalDepth = 0;
            SpecialDepth = 0;
            NormalSource = null;
            filter.Reset();
        }

        internal static void Tick()
        {
            if (!Active) return;
            if (resetKey.Value.IsDown())
            {
                HitCount = 0;
                Reset();
                Plugin.Log.LogInfo("Hit counter reset.");
            }
        }

        /// Вызов «Хорнет получила урон»: считаем или склеиваем с предыдущим ударом.
        internal static void OnHit(float now)
        {
            bool special = SpecialDepth > 0;
            if (!filter.ShouldCount(now, special))
            {
                Plugin.Log.LogInfo(special
                    ? "[HC] merged special tick"
                    : "[HC] merged cross-path normal tail; source=" + (NormalSource ?? "<direct>"));
                return;
            }
            HitCount++;
            Plugin.Log.LogInfo(special
                ? "[HC] +1 special attack; total=" + HitCount.ToString()
                : "[HC] +1 normal attack; source=" + (NormalSource ?? "<direct>") + "; total=" + HitCount.ToString());
        }

        internal static void Draw()
        {
            if (!Active || !showCounter.Value) return;
            if (style == null)
            {
                style = new GUIStyle(GUI.skin.box);
                style.fontSize = 20;
                style.fontStyle = FontStyle.Bold;
                style.alignment = TextAnchor.MiddleCenter;
                style.normal.textColor = Color.white;
            }
            if (shownCount != HitCount)
            {
                shownCount = HitCount;
                shownText = "HITS: " + HitCount.ToString();
            }
            GUI.Box(new Rect(Screen.width - 202, 12f, 190f, 44f), shownText, style);
        }
    }

    /// Какой обычной атакой нанесён урон (для журнала): HeroController.TakeDamage(GameObject, ...) с пятью параметрами.
    [HarmonyPatch]
    internal static class HitCounterNormalContext
    {
        static MethodBase TargetMethod()
        {
            MethodInfo[] all = typeof(HeroController).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].Name != "TakeDamage") continue;
                ParameterInfo[] parameters = all[i].GetParameters();
                if (parameters.Length == 5 && parameters[0].ParameterType == typeof(GameObject)) return all[i];
            }
            throw new MissingMethodException("HeroController.TakeDamage(GameObject, ..., ..., ..., ...) not found");
        }

        [HarmonyPrefix]
        static void Prefix(object[] __args)
        {
            HitCounterMod.NormalDepth++;
            GameObject source = null;
            if (__args != null && __args.Length > 0) source = __args[0] as GameObject;
            HitCounterMod.NormalSource = source != null ? source.name : "<null>";
        }

        [HarmonyPostfix]
        static void Postfix()
        {
            Leave();
        }

        [HarmonyFinalizer]
        static Exception Finalizer(Exception __exception)
        {
            if (__exception != null) Leave();
            return __exception;
        }

        static void Leave()
        {
            if (HitCounterMod.NormalDepth > 0) HitCounterMod.NormalDepth--;
            if (HitCounterMod.NormalDepth == 0) HitCounterMod.NormalSource = null;
        }
    }

    /// Урон внутри особой атаки (DoSpecialDamage): её «тики» не должны считаться отдельными ударами.
    [HarmonyPatch(typeof(HeroController), "DoSpecialDamage")]
    internal static class HitCounterSpecialContext
    {
        [HarmonyPrefix]
        static void Prefix()
        {
            HitCounterMod.SpecialDepth++;
        }

        [HarmonyPostfix]
        static void Postfix()
        {
            if (HitCounterMod.SpecialDepth > 0) HitCounterMod.SpecialDepth--;
        }

        [HarmonyFinalizer]
        static Exception Finalizer(Exception __exception)
        {
            if (__exception != null && HitCounterMod.SpecialDepth > 0) HitCounterMod.SpecialDepth--;
            return __exception;
        }
    }

    [HarmonyPatch(typeof(PlayerData), "TakeHealth", new Type[] { typeof(int), typeof(bool), typeof(bool) })]
    internal static class HitCounterTakeHealth
    {
        static bool inside;

        [HarmonyPrefix]
        static void Prefix(int amount, out bool __state)
        {
            __state = false;
            if (inside || amount <= 0) return;
            inside = true;
            __state = true;
            HitCounterMod.OnHit(Time.realtimeSinceStartup);
        }

        [HarmonyPostfix]
        static void Postfix(bool __state)
        {
            if (__state) inside = false;
        }

        [HarmonyFinalizer]
        static Exception Finalizer(Exception __exception, bool __state)
        {
            if (__exception != null && __state) inside = false;
            return __exception;
        }
    }

    // ------------------------------------------------------------------ Always Double Incoming Damage

    internal static class DoubleDamageMod
    {
        internal const string OldGuid = "abdulbasit.silksong.alwaysdoubleincoming";

        internal static bool Active;
        internal static bool IsEnabled;

        static ConfigFile cfg;
        static ConfigEntry<int> multiplier;
        static ConfigEntry<bool> enabledEntry;
        static ConfigEntry<KeyboardShortcut> toggleKey;
        static ConfigEntry<bool> showStatus;
        static float notificationUntil;

        internal static int Multiplier { get { return multiplier.Value; } }
        internal static bool WantsDraw { get { return Active && showStatus.Value && Time.realtimeSinceStartup <= notificationUntil; } }

        internal static void Init()
        {
            cfg = Mods.OpenConfig(OldGuid, "Always Double Incoming Damage");
            multiplier = cfg.Bind("Damage", "Multiplier", 2, "Incoming damage multiplier when enabled.");
            // У отдельного мода по умолчанию было true; во встроенной копии на чистой установке урон не меняется,
            // пока его не включили (у кого файл настроек уже есть, значение остаётся прежним).
            enabledEntry = cfg.Bind("Damage", "Enabled", false, "Whether extra incoming damage is enabled.");
            toggleKey = cfg.Bind("Controls", "ToggleKey", new KeyboardShortcut(KeyCode.Quote), "Key to toggle extra incoming damage.");
            showStatus = cfg.Bind("Display", "ShowStatus", true, "Show ON/OFF notification.");
            IsEnabled = enabledEntry.Value;
            notificationUntil = Time.realtimeSinceStartup + 3f;
            Mods.Patch(typeof(DoubleDamageTakeHealth), "doubledamage");
            Active = true;
            Plugin.Log.LogInfo("Always Double Incoming Damage 1.7.0 (встроен в Encore): включён=" + IsEnabled + ", клавиша " + toggleKey.Value);
        }

        internal static void Tick()
        {
            if (!Active || !toggleKey.Value.IsDown()) return;
            IsEnabled = !IsEnabled;
            enabledEntry.Value = IsEnabled;
            cfg.Save();
            notificationUntil = Time.realtimeSinceStartup + 3f;
            Plugin.Log.LogInfo("Always Double Incoming Damage: " + (IsEnabled ? "ON" : "OFF"));
        }

        internal static void Draw()
        {
            if (!Active || !showStatus.Value || Time.realtimeSinceStartup > notificationUntil) return;
            GUI.Box(new Rect(12f, 12f, 245f, 34f), DamageMath.StatusText(IsEnabled, multiplier.Value));
        }
    }

    [HarmonyPatch(typeof(PlayerData), "TakeHealth", new Type[] { typeof(int), typeof(bool), typeof(bool) })]
    internal static class DoubleDamageTakeHealth
    {
        static bool inside;

        [HarmonyPrefix]
        static void Prefix(ref int amount, out bool __state)
        {
            __state = false;
            if (!DoubleDamageMod.IsEnabled || inside || amount <= 0) return;
            inside = true;
            __state = true;
            amount = DamageMath.Multiply(amount, DoubleDamageMod.Multiplier);
        }

        [HarmonyPostfix]
        static void Postfix(bool __state)
        {
            if (__state) inside = false;
        }

        [HarmonyFinalizer]
        static Exception Finalizer(Exception __exception, bool __state)
        {
            if (__state) inside = false;
            return __exception;
        }
    }
}
