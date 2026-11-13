// ============================================================================
//  Plugin.cs — вход мода: настройки, клавиши, подсказка на экране.
//  Encore: тренировка своих сегментов боссов в Gods of Pharloom.
//  Что менять: клавиши и язык — в конфиге BepInEx\config\sova.encore.cfg
//  (или в ConfigurationManager, F1). Значения по умолчанию — в Awake ниже.
// ============================================================================
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
        public const string Version = "0.3.0";

        internal static ManualLogSource Log;

        static ConfigEntry<KeyboardShortcut> menuKey;
        static ConfigEntry<KeyboardShortcut> startKey;
        static ConfigEntry<string> language;
        static ConfigEntry<bool> showHud;
        static ConfigEntry<bool> gameFont;

        static string toastText;
        static float toastUntil;

        Menu menu;

        void Awake()
        {
            Log = Logger;

            // F1–F12 не трогаем: они заняты другими модами.
            menuKey = Config.Bind("Keys", "Menu", new KeyboardShortcut(KeyCode.End),
                "Открыть / закрыть меню пресетов. Open / close the preset menu.");
            startKey = Config.Bind("Keys", "StartSelected", new KeyboardShortcut(KeyCode.PageDown),
                "Сразу запустить выбранный пресет. Start the selected preset right away.");
            language = Config.Bind("General", "Language", "auto",
                new ConfigDescription("Язык / language", new AcceptableValueList<string>("auto", "ru", "en")));
            showHud = Config.Bind("General", "ShowHud", true,
                "Строка во время сегмента: босс, сложность, удары. Status line during a segment.");

            gameFont = Config.Bind("General", "GameFont", true,
                "Шрифты игры в окне мода (false — обычный шрифт). Use the game's fonts in the mod window.");

            L.Mode = language.Value;
            language.SettingChanged += (s, e) => L.Mode = language.Value;
            Theme.UseGameFont = gameFont.Value;
            gameFont.SettingChanged += (s, e) => { Theme.UseGameFont = gameFont.Value; Theme.Invalidate(); };

            PresetStore.Load();
            Harmony.CreateAndPatchAll(typeof(Patches), Guid);
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += (scene, mode) => Runner.OnSceneLoaded(scene.name);

            menu = new Menu();
            Log.LogInfo("Encore " + Version + ": пресетов " + PresetStore.Data.Presets.Count + ", файл " + PresetStore.FilePath);
        }

        internal static string MenuKeyName { get { return KeyName(menuKey.Value); } }
        internal static string StartKeyName { get { return KeyName(startKey.Value); } }

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
            if (menuKey.Value.IsDown()) menu.Toggle();
            if (startKey.Value.IsDown())
            {
                menu.Close();
                StartSelected();
            }
            Runner.Tick();
            menu.Update();
        }

        void LateUpdate()
        {
            menu.KeepCursor();
        }

        void OnGUI()
        {
            if (menu.Open) { menu.Draw(); return; }

            string hud = showHud.Value ? Runner.HudLine() : null;
            bool toast = toastText != null && Time.unscaledTime < toastUntil;
            if (hud == null && !toast) return;

            // строка сегмента и сообщение — тёмные плашки вверху по центру
            Theme.Ensure();
            float y = Theme.Px(12f);
            if (hud != null) y = Plate(hud, Theme.Hud, y);
            if (toast) Plate(toastText, Theme.Toast, y);
        }

        /// Рисует плашку с текстом; возвращает, где начинать следующую.
        static float Plate(string text, GUIStyle style, float y)
        {
            float width = style.CalcSize(new GUIContent(text)).x + Theme.Px(48f);
            float height = Theme.Px(Theme.PillH);
            Rect rect = new Rect(Mathf.Round((Screen.width - width) / 2f), y, width, height);
            GUI.Box(rect, GUIContent.none, Theme.Pill);
            GUI.Label(rect, text, style);
            return y + height + Theme.Px(8f);
        }
    }
}
