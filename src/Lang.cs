// ============================================================================
//  Lang.cs — выбор языка интерфейса (ru / en).
//  Зачем: все надписи мода двуязычные. Пишем L.T("по-русски", "in English").
//  Что менять: добавить язык — расширить Detect/метод T. Сам выбор языка лежит
//  в конфиге (Language = auto / ru / en), см. Plugin.cs.
// ============================================================================
using UnityEngine;

namespace Encore
{
    internal static class L
    {
        static string mode = "auto";
        static bool known, ru;

        /// "auto" (по языку системы), "ru" или "en". Выставляется из конфига.
        internal static string Mode
        {
            get { return mode; }
            set { mode = value; known = false; }
        }

        /// Спрашивается десятки раз за кадр, пока открыто окно, поэтому ответ запоминается.
        internal static bool IsRu
        {
            get
            {
                if (!known) { ru = Detect(); known = true; }
                return ru;
            }
        }

        internal static string T(string ru, string en)
        {
            return IsRu ? ru : en;
        }

        static bool Detect()
        {
            if (mode == "ru") return true;
            if (mode == "en") return false;
            SystemLanguage lang = Application.systemLanguage;
            return lang == SystemLanguage.Russian || lang == SystemLanguage.Ukrainian || lang == SystemLanguage.Belarusian;
        }
    }
}
