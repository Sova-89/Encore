// ============================================================================
//  Plugin.cs — вход мода: настройки, клавиши, подсказка на экране.
//  Encore: тренировка своих сегментов боссов в Gods of Pharloom.
//  Что менять: клавиши и язык — в конфиге BepInEx\config\sova.encore.cfg
//  (или в ConfigurationManager, F1). Значения по умолчанию — в Awake ниже.
//  Клавиши привязок GoP (Keys/BindNeedle … BindAll) по умолчанию не заданы — назначь их там (см. Build.cs).
// ============================================================================
using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace Encore
{
    [BepInPlugin(Guid, "Encore", Version)]
    [BepInDependency("bepinex.plugin.test")] // Gods of Pharloom (такой у него идентификатор)
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = "sova.encore";
        public const string Version = "1.0.2";

        internal static ManualLogSource Log;

        static ConfigEntry<KeyboardShortcut> menuKey;
        static ConfigEntry<KeyboardShortcut> startKey;
        static ConfigEntry<string> language;
        static ConfigEntry<bool> showHud;
        static ConfigEntry<bool> gameFont;
        static ConfigEntry<bool> logFrameLoad;
        static ConfigEntry<int> randomBenchEvery;
        static ConfigEntry<bool> randomHideOrder;
        static ConfigEntry<bool> hitless;
        static ConfigEntry<bool> restartOnDeath;
        static ConfigEntry<int> gameSpeed;
        static ConfigEntry<bool> skipAnim;
        static ConfigEntry<bool> kickPhase;
        static ConfigEntry<bool> modHitCounter;
        static ConfigEntry<bool> modDoubleDamage;
        static ConfigEntry<string> skillsOff;
        static readonly ConfigEntry<KeyboardShortcut>[] bindKeys = new ConfigEntry<KeyboardShortcut>[BindKeyCount];
        const int BindKeyCount = 5;           // Needle, Silk, Tools, Mask и «все четыре сразу»
        static readonly string[] bindKeyNames = { "", "", "", "", "" };
        static string skillsOffNow = "";

        static string toastText;
        static float toastUntil;

        // что рисовать в этом кадре — решается один раз в Update
        static string hudNow;
        static bool toastNow;
        static string menuKeyName, startKeyName;
        static readonly TextWidth hudWidth = new TextWidth(), toastWidth = new TextWidth();

        Menu menu;
        Overlay overlay;
        bool overlayOn;

        void Awake()
        {
            Log = Logger;
            Load.StartBegin();
            Config.SaveOnConfigSet = false; // BepInEx переписывает файл настроек при каждом Bind; запишем один раз ниже

            // F1–F12 не трогаем: они заняты другими модами.
            menuKey = Config.Bind("Keys", "Menu", new KeyboardShortcut(KeyCode.End),
                "Открыть / закрыть меню пресетов. Open / close the preset menu.");
            startKey = Config.Bind("Keys", "StartSelected", new KeyboardShortcut(KeyCode.PageDown),
                "Сразу запустить выбранный пресет. Start the selected preset right away.");
            Load.Lap("клавиши");
            language = Config.Bind("General", "Language", "auto",
                new ConfigDescription("Язык / language", new AcceptableValueList<string>("auto", "ru", "en")));
            showHud = Config.Bind("General", "ShowHud", true,
                "Строка во время сегмента: босс, сложность, удары. Status line during a segment.");

            gameFont = Config.Bind("General", "GameFont", true,
                "Шрифты игры в окне мода (false — обычный шрифт). Use the game's fonts in the mod window.");

            L.Mode = language.Value;
            language.SettingChanged += (s, e) => L.Mode = language.Value;
            Load.Lap("общие");
            randomBenchEvery = Config.Bind("Random", "BenchEvery", 5, new ConfigDescription(
                "Скамейка через каждые столько боссов в случайном пресете (0 — без скамеек). Bench after every N bosses, 0 = none.",
                new AcceptableValueRange<int>(0, 20)));
            randomHideOrder = Config.Bind("Random", "HideOrder", true,
                "Прятать порядок боссов в случайном пресете (в окне «???»). Hide the boss order of a random preset.");
            Load.Lap("случайный");
            hitless = Config.Bind("Mode", "Hitless", false,
                "Режим «без урона»: получил удар — сегмент начинается заново. Hitless mode: any hit restarts the segment.");
            restartOnDeath = Config.Bind("Mode", "RestartOnDeath", false,
                "Перезапускать сегмент после смерти Хорнет и без режима «без урона» (удары тогда не мешают). Restart the segment after Hornet dies, even without hitless mode (hits do not restart).");
            gameSpeed = Config.Bind("Mode", "GameSpeed", SpeedMath.Normal, new ConfigDescription(
                "Скорость игры в процентах, во всей игре (100 — как в игре), действует сразу. Выше 100 — челлендж (бой в Encore идёт в статистику со своей строкой), ниже 100 — тренировка (в статистику не идёт). Если поменять посреди попытки сегмента, остаток этой попытки в статистику не идёт. Game speed in percent, whole game (100 = vanilla), applies at once. Above 100 is a challenge (counted in the Encore stats, own row), below 100 is practice (not counted). Changed in the middle of a segment attempt, the rest of that attempt is not counted.",
                new AcceptableValueRange<int>(SpeedMath.Min, SpeedMath.Max)));
            skipAnim = Config.Bind("Mode", "SkipPhaseAnimations", true,
                "Пропускать рёвы и уходы босса при смене фаз до выбранной (вкладка «Боссы»). Skip the roars and exits of the phase changes up to the chosen phase.");
            kickPhase = Config.Bind("Mode", "KickPhaseChange", true,
                "Когда босс заперт в фазе, самим запускать проверку смены фазы, не дожидаясь первого удара (Seth и др.). Start the phase check without waiting for the first hit.");
            logFrameLoad = Config.Bind("General", "LogFrameLoad", false,
                "Раз в 30 с писать в лог, сколько времени мод тратит за кадр. Log the mod's per-frame cost every 30 s.");
            modHitCounter = Config.Bind("Modules", "HitCounter", true,
                "Встроенный Hit Counter: счётчик полученных ударов «HITS» в углу экрана, во всей игре. Built-in Hit Counter (whole game).");
            modDoubleDamage = Config.Bind("Modules", "AlwaysDoubleDamage", true,
                "Встроенный Always Double Incoming Damage: клавиша включает множитель входящего урона, во всей игре. Built-in Always Double Incoming Damage (whole game).");
            Load.Lap("режим");

            // Привязки GoP и навыки (вкладка «Привязки» в окне). Клавиши по умолчанию не заданы: F1–F12 и другие занятые клавиши не трогаем.
            string[] bindKeyIds = { "BindNeedle", "BindSilk", "BindTools", "BindMask", "BindAll" };
            string[] bindKeyInfo =
            {
                "Включить / выключить привязку Needle (во всей игре; не во время сегмента). Toggle the Needle binding (whole game; not during a segment).",
                "Включить / выключить привязку Silk. Toggle the Silk binding.",
                "Включить / выключить привязку Tools. Toggle the Tools binding.",
                "Включить / выключить привязку Mask. Toggle the Mask binding.",
                "Включить все четыре привязки или снять все (если хоть одна включена — снимает). Turn all four bindings on, or off if any is on.",
            };
            for (int i = 0; i < BindKeyCount; i++)
            {
                bindKeys[i] = Config.Bind("Keys", bindKeyIds[i], KeyboardShortcut.Empty, bindKeyInfo[i]);
                bindKeyNames[i] = BindKeyText(bindKeys[i].Value);
                int index = i;
                bindKeys[i].SettingChanged += (s, e) => bindKeyNames[index] = BindKeyText(bindKeys[index].Value);
            }
            skillsOff = Config.Bind("Build", "SkillsOff", "",
                "Навыки, которые Encore отключил (кнопки во вкладке «Привязки»; можно и руками, через запятую): dash, claw, eva, soar, grip, needolin, strike, drift, fay. Перед каждой попыткой сегмента они отключаются в игре заново, бой идёт в статистику отдельной строкой. Skills that Encore turned off (buttons of the Bindings tab; you can edit it by hand, comma separated). They are turned off again before every segment attempt; the fight has its own row in the statistics.");
            skillsOffNow = BuildMath.NormalizeSkills(skillsOff.Value);
            skillsOff.SettingChanged += (s, e) => skillsOffNow = BuildMath.NormalizeSkills(skillsOff.Value);
            Load.Lap("привязки");
            Config.SaveOnConfigSet = true;
            SaveConfigIfNeeded();
            Load.Lap("файл настроек");

            Theme.UseGameFont = gameFont.Value;
            gameFont.SettingChanged += (s, e) => { Theme.UseGameFont = gameFont.Value; Theme.Invalidate(); };
            Load.On = logFrameLoad.Value;
            logFrameLoad.SettingChanged += (s, e) => Load.On = logFrameLoad.Value;
            Load.Lap("тема");

            // названия клавиш для подписей на кнопках готовятся один раз и при смене клавиши
            menuKeyName = KeyName(menuKey.Value);
            startKeyName = KeyName(startKey.Value);
            menuKey.SettingChanged += (s, e) => menuKeyName = KeyName(menuKey.Value);
            startKey.SettingChanged += (s, e) => startKeyName = KeyName(startKey.Value);
            Load.Lap("подписи клавиш");

            PresetStore.Load();
            Load.Lap("пресеты");
            Harmony.CreateAndPatchAll(typeof(Patches), Guid);
            Mods.Patch(typeof(ImmortalPatches), "Immortal"); // бессмертие босса: свой try, сбой тут не роняет Encore
            Mods.Patch(typeof(GameSpeedPatches), "GameSpeed"); // скорость игры: перехват пересчёта Time.timeScale
            Mods.Patch(typeof(GameSpeedActionPatches), "GameSpeed (ScaleTime)"); // и PlayMaker-действия ScaleTime, которое пишет время мимо TimeManager
            GameSpeed.Set(SpeedPercent);
            gameSpeed.SettingChanged += (s, e) => OnSpeedSetting();
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += (scene, mode) => Runner.OnSceneLoaded(scene.name);
            // Сцена сменилась — окно закрываем: иначе забытое открытое окно держит Хорнет без управления.
            UnityEngine.SceneManagement.SceneManager.activeSceneChanged += (prev, next) =>
            {
                if (menu != null) menu.Close("смена сцены");
            };
            Load.Lap("перехваты");

            menu = new Menu();
            overlay = gameObject.AddComponent<Overlay>();
            overlay.Owner = this;
            Load.Lap("окно");

            Log.LogInfo("Encore " + Version + ": пресетов " + PresetStore.Data.Presets.Count + ", файл " + PresetStore.FilePath);
            Log.LogInfo(Load.StartReport());
        }

        /// Первый кадр: все плагины уже загружены, видно, не стоят ли отдельные старые Hit Counter и Always Double Incoming Damage.
        void Start()
        {
            try { Mods.Start(gameObject, modHitCounter.Value, modDoubleDamage.Value); }
            catch (System.Exception ex) { Log.LogError("Встроенные моды: " + ex); }
        }

        /// Пишет файл настроек, только если в нём не хватает записей (первый запуск или новая версия с новыми настройками).
        /// Остальные запуски файл не трогают: запись на диск при старте игры бывает медленной.
        void SaveConfigIfNeeded()
        {
            string text = null;
            try
            {
                if (System.IO.File.Exists(Config.ConfigFilePath)) text = System.IO.File.ReadAllText(Config.ConfigFilePath);
            }
            catch (System.Exception) { }

            bool missing = text == null;
            if (!missing)
                foreach (ConfigDefinition key in Config.Keys)
                    if (text.IndexOf("\n" + key.Key + " = ", System.StringComparison.Ordinal) < 0) { missing = true; break; }
            if (missing) Config.Save();
        }

        internal static int RandomBenchEvery { get { return randomBenchEvery.Value; } }
        internal static bool RandomHideOrder { get { return randomHideOrder.Value; } }
        /// Режим «без урона» (кнопка в окне и настройка Mode/Hitless).
        internal static bool Hitless
        {
            get { return hitless.Value; }
            set { hitless.Value = value; }
        }
        /// Режим перезапуска одним числом (RestartMode): обычный / рестарт при смерти / без урона. Кнопка в окне переключает его по кругу.
        internal static int RestartModeNow
        {
            get { return RestartMode.From(hitless.Value, restartOnDeath.Value); }
            set
            {
                bool h, d;
                RestartMode.Split(value, out h, out d);
                hitless.Value = h;
                restartOnDeath.Value = d;
            }
        }
        /// Скорость, с которой игра идёт на самом деле: если перехват сломался, это 100% (по ней считается статистика).
        internal static int EffectiveSpeed { get { return GameSpeed.Broken ? SpeedMath.Normal : SpeedPercent; } }

        /// Скорость игры в процентах (кнопки в окне и настройка Mode/GameSpeed). Допустимые значения — SpeedMath.
        internal static int SpeedPercent
        {
            get { return SpeedMath.Clamp(gameSpeed.Value); }
            set { gameSpeed.Value = SpeedMath.Clamp(value); }
        }

        /// Настройку скорости поменяли (кнопкой или в файле): игра идёт с новой скоростью сразу. Посреди попытки сегмента Encore
        /// остаток попытки в статистику не идёт (скорость в ней смешанная, см. Runner.OnSpeedChanged).
        static void OnSpeedSetting()
        {
            int p = SpeedPercent;
            if (p != gameSpeed.Value) { gameSpeed.Value = p; return; } // число подогнано под шаг — событие придёт ещё раз
            GameSpeed.Set(p);
            Runner.OnSpeedChanged(p);
        }

        /// Навыки, которые отключил Encore, одной строкой «dash,grip» (BuildMath.NormalizeSkills); пусто — все на месте.
        internal static string SkillsOff
        {
            get { return skillsOffNow; }
            set
            {
                string text = BuildMath.NormalizeSkills(value);
                skillsOffNow = text;
                skillsOff.Value = text;
            }
        }

        /// Название клавиши привязки i (0 Needle … 3 Mask, 4 — все) для подписи в окне; пусто, если клавиша не задана.
        internal static string BindKeyName(int i)
        {
            return i >= 0 && i < BindKeyCount ? bindKeyNames[i] : "";
        }

        static string BindKeyText(KeyboardShortcut shortcut)
        {
            return shortcut.MainKey == KeyCode.None ? "" : KeyName(shortcut);
        }

        /// Нажата клавиша привязки: переключает её и говорит об этом сообщением на экране (клавиши по умолчанию не заданы).
        static void CheckBindKeys()
        {
            for (int i = 0; i < BindKeyCount; i++)
            {
                if (bindKeyNames[i].Length == 0 || !bindKeys[i].Value.IsDown()) continue;
                string message;
                bool changed;
                if (i < 4) changed = Build.ToggleBinding(i, out message);
                else
                {
                    bool anyOn = false;
                    for (int k = 0; k < 4; k++) anyOn |= Build.Binding(k);
                    changed = Build.SetAllBindings(!anyOn, out message);
                }
                Toast(message, changed ? 2.5f : 4f);
            }
        }

        /// Перезапускать ли сегмент после смерти Хорнет («без урона» или «рестарт при смерти»).
        internal static bool RestartsOnDeath { get { return RestartMode.RestartsOnDeath(RestartModeNow); } }
        /// Пропускать рёвы смены фаз до выбранной (настройка Mode/SkipPhaseAnimations).
        internal static bool KickPhaseChange { get { return kickPhase.Value; } }
        internal static bool SkipAnimations { get { return skipAnim.Value; } }
        /// Открыто ли окно мода (пока открыто, перезапуск после смерти ждёт; см. Retry).
        internal static bool MenuOpen;
        internal static string MenuKeyName { get { return menuKeyName; } }
        internal static string StartKeyName { get { return startKeyName; } }
        /// «PageDown» -> «Page Down»: название клавиши для подписи на кнопке.
        static string KeyName(KeyboardShortcut shortcut)
        {
            string raw = shortcut.ToString();
            var text = new System.Text.StringBuilder();
            for (int i = 0; i < raw.Length; i++)
            {
                if (i > 0 && char.IsUpper(raw[i]) && char.IsLower(raw[i - 1])) text.Append(' ');
                text.Append(raw[i]);
            }
            return text.ToString();
        }

        static readonly Dictionary<string, int> caughtCount = new Dictionary<string, int>();

        /// Ошибка в нашем обработчике: пишем в журнал первые три раза для каждого места (со стеком), дальше молчим, чтобы не засорять журнал каждый кадр.
        /// Игру ошибка не роняет.
        internal static void Caught(string where, Exception ex)
        {
            int n;
            caughtCount.TryGetValue(where, out n);
            caughtCount[where] = ++n;
            if (n <= 3) Log.LogError(where + " (ошибка " + n + "): " + ex);
        }

        /// Короткое сообщение вверху экрана.
        internal static void Toast(string text, float seconds)
        {
            toastText = text;
            toastUntil = Time.unscaledTime + seconds;
        }

        internal static void StartSelected()
        {
            string error = Runner.Launch(PresetStore.Selected);
            if (error != null) Toast(error, 4f);
        }

        void Update()
        {
            Load.NextFrame();
            long started = Load.Begin();

            if (menuKey.Value.IsDown()) menu.Toggle();
            if (startKey.Value.IsDown())
            {
                menu.Close("запуск пресета");
                StartSelected();
            }
            MenuOpen = menu.Open;
            // Каждая часть в своём try: ошибка в одной (например, в Runner.Tick) не отключает остальные (скорость, окно) и не сыплет в журнал каждый кадр.
            try { Mods.Tick(menu.Open); } catch (Exception ex) { Caught("Update/Mods.Tick", ex); }
            try { Runner.Tick(); } catch (Exception ex) { Caught("Update/Runner.Tick", ex); }
            try { GameSpeed.Tick(); } catch (Exception ex) { Caught("Update/GameSpeed.Tick", ex); }
            try { menu.Update(); } catch (Exception ex) { Caught("Update/menu.Update", ex); }
            if (menu.Open && menu.WantsIcons)
                try { Icons.Pump(); } catch (Exception ex) { Caught("Update/Icons", ex); }
            try { CheckBindKeys(); } catch (Exception ex) { Caught("Update/CheckBindKeys", ex); }

            // Рисование включается, только когда есть что рисовать: окно, строка сегмента или сообщение.
            hudNow = showHud.Value ? Runner.HudLine() : null;
            toastNow = toastText != null && Time.unscaledTime < toastUntil;
            bool needed = menu.Open || hudNow != null || toastNow;
            if (needed != overlayOn)
            {
                overlayOn = needed;
                overlay.enabled = needed;
            }

            Load.End(started);
        }

        /// Зовёт Overlay, пока он включён: окно либо плашки вверху по центру.
        internal void DrawGui()
        {
            if (menu.Open) { menu.Draw(); return; }
            if (hudNow == null && !toastNow) return;

            Theme.Ensure();
            float y = Theme.Px(12f);
            if (hudNow != null) y = Plate(hudNow, Theme.Hud, hudWidth, y);
            if (toastNow) Plate(toastText, Theme.Toast, toastWidth, y);
        }

        /// Зовёт Overlay в конце кадра, пока он включён.
        internal void AfterFrame()
        {
            menu.KeepCursor();
        }

        /// Рисует плашку с текстом; возвращает, где начинать следующую.
        static float Plate(string text, GUIStyle style, TextWidth measured, float y)
        {
            float width = measured.Of(text, style) + Theme.Px(48f);
            float height = Theme.Px(Theme.PillH);
            Rect rect = new Rect(Mathf.Round((Screen.width - width) / 2f), y, width, height);
            GUI.Box(rect, GUIContent.none, Theme.Pill);
            GUI.Label(rect, text, style);
            return y + height + Theme.Px(8f);
        }
    }
}
