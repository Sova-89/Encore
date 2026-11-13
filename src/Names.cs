// ============================================================================
//  Names.cs — русские названия боссов.
//  Откуда: официальная русская локализация Silksong (лист Titles: надпись над
//  именем + имя + подпись, как на титре боя; места — из Titles и Map Zones).
//  В пресетах и в логе остаются английские ключи GoP — меняется только то,
//  что видно на экране. При английском интерфейсе названия как в GoP.
//  Что менять: поправить или добавить название — строка в таблице ниже.
// ============================================================================
using System.Collections.Generic;

namespace Encore
{
    internal static class Names
    {
        // Ключ = имя босса в Gods of Pharloom, значение = название в русской версии игры.
        static readonly Dictionary<string, string> Ru = new Dictionary<string, string>
        {
            // Акты 1–2
            { "Moss Mother", "Матерь Мхов" },
            { Tokens.MossAsc, "Матерь Мхов (две)" },
            { "Bell Beast", "Колокольный зверь" },
            { "Fourth Chorus", "Четвёртый Хор" },
            { "Great Conchflies", "Большие ракушечные мухи" },
            { "Lace in Deep Docks", "Лейс (Глубокие доки)" },
            { "The Last Judge", "Последняя судья" },
            { "Moorwing", "Топекрыл" },
            { "Phantom", "Фантом" },
            { "Savage Beastfly in Chapel of The Beast", "Дикая зверомуха (Капелла Зверя)" },
            { "Sister Splinter", "Сестра Лучина" },
            { "Skull Tyrant", "Тиран Череп" },
            { "Widow", "Вдова" },
            { "Broodmother", "Матерь рода" },
            { "Cogwork Dancers", "Заводные танцоры" },
            { "Disgraced Chef Lugoli", "Опальная повариха Луголи" },
            { "Father of the Flame", "Отец пламени" },
            { "First Sinner", "Первая грешница" },
            { "Forebrothers Signis & Gron", "Старшие братья Сигнис и Грон" },
            { "Garmond & Zaza", "Гармонд и Заза" },
            { "Grand Mother Silk", "Верховная матерь Шёлка" },
            { "Groal the Great", "Гроал Великий" },
            { "Lace in the Cradle", "Лейс (Колыбель)" },
            { "Raging Conchfly", "Разъярённая ракушечная муха" },
            { "Savage Beastfly in Far Fields", "Дикая зверомуха (Дальние поля)" },
            { "Second Sentiel", "Второй Страж" },
            { "Shakra", "Шакра" },
            { "The Unravelled", "Распутыватель" },
            { "Trobbio", "Троббио" },
            { "Voltvyrm", "Червевольт" },

            // Акт 3
            { "Bell Eater", "Пожиратель колоколов" },
            { "Clover Dancers", "Клеверные танцоры" },
            { "Crawfather", "Отец каров" },
            { "Crust King Khann", "Коралловый король Хан" },
            { "Gurr the Outcast", "Изгой Гурр" },
            { "Lost Garmond", "Пропащий Гармонд" },
            { "Lost Lace", "Пропащая Лейс" },
            { "Nyleth", "Найлет" },
            { "Palestag", "Бледнорожка" },
            { "Pinstress", "Булавочница" },
            { "Plasmified Zango", "Плазмифицированный Занго" },
            { "Shrine Guardian Seth", "Страж алтаря Сет" },
            { "Skarrsinger Karmelita", "Поющая Скарру Кармелита" },
            { "Tormented Trobbio", "Истерзанный Троббио" },
            { "Watcher at the Edge", "Пограничный Наблюдатель" },

            // Арены с волнами (в игре у них нет титра — взято название места)
            { "Forum Battle", "Форум (битва)" },
            { "Coral Tower Battle", "Коралловая башня (битва)" },
        };

        /// Название для экрана на текущем языке интерфейса.
        internal static string Display(string boss)
        {
            if (boss == Tokens.Rest) return L.T("— скамейка —", "— bench —");

            string ru;
            if (L.IsRu && Ru.TryGetValue(boss, out ru)) return ru;

            if (boss == "Second Sentiel") return "Second Sentinel"; // опечатка в ключе GoP, показываем правильно
            return boss;
        }
    }
}
