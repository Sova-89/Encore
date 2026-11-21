// ============================================================================
//  Phase.cs — «босс весь бой в одной фазе»: список боссов и чистая арифметика, без Unity и GoP.
//  В GoP у босса одно общее здоровье, а фазы отделены порогами: как только HP упало ниже порога,
//  босс уходит в следующую фазу. Пороги GoP считает сам из таблицы EnemyHp (здоровье каждой фазы):
//  порог k = всё здоровье − сумма первых k фаз. Поэтому у большинства боссов фазу можно зафиксировать,
//  подменив таблицу ДО боя: тогда GoP сам расставит и здоровье, и пороги (BossFx.ApplyTable).
//  Для «только фаза N» (нумерация с 1):
//    • фазы до N получают здоровье −1: их пороги оказываются выше здоровья, и босс проходит их сразу;
//    • фаза N получает здоровье «всё здоровье + (N−1)», чтобы сумма была ровно нужной;
//    • фазы после N получают 0: их пороги равны 0, и босс до них не дойдёт.
//  «Тренировка» — здоровье только этой фазы (бой кончается вместе с ней), «челлендж» — полное здоровье
//  босса (сумма всех фаз), но он весь бой остаётся в этой фазе.
//  У двух боссов порог в FSM равен не «остатку», а здоровью фазы напрямую (Palestag — первой,
//  Great Conchflies — второй): правила DirectFirst / DirectSecond.
//  У Bell Eater (правило BellEater) и Father of the Flame (правило Lanterns) строки таблицы GoP — не фазы (голова, зад, порог…; руки, порог, ядро),
//  арифметика у них своя (BellEaterPlan, LanternsPlan), а таблица длиннее числа фаз (BossDef.TableLen). Конец боя после фазы — PhaseEnd (оба),
//  руки Отца пламени ломаем заранее и снимаем потолок ударов — PhaseFather.
//  Троббио (правило Trobbio) — первый босс, он сделан отдельно и проверен в игре (BossFx.TryApply):
//  там ещё пропускается рёв перед второй фазой.
//  Своё здоровье (0.19.0, только тренировка): число вместо ванильного. С выбранной фазой — здоровье этой фазы (вместо её запаса),
//  без неё — здоровье всего боя: таблица масштабируется (ScalePlan), фазы сохраняют доли. Есть и у боссов без фаз (SingleDefs).
//  Значение хранится в presets.json (Entry.Phase / Entry.Full / Entry.Hp): 0 — как в игре, N — фаза N.
//  Что менять: новый босс — строка в Defs (имя как в Names.cs, правило, число фаз, имена таблиц GoP).
// ============================================================================
using System;
using System.Collections.Generic;

namespace Encore
{
    /// Как у босса устроены пороги фаз.
    internal enum PhaseRule
    {
        Trobbio,       // отдельный путь (BossFx.TryApply)
        Cumulative,    // порог = здоровье − сумма предыдущих фаз (у большинства)
        DirectFirst,   // порог = здоровье первой фазы (двухфазный босс)
        DirectSecond,  // порог = здоровье второй фазы (двухфазный босс)
        Vanilla,       // в GoP таблицы нет, здоровье и порог (FSM «Control», «P2 HP») ванильные: путь BossFx.TryApplyVanilla
        Pool,          // у каждой фазы свой запас здоровья (FSM «Phase Control» ставит его по очереди): таблица как у Cumulative + конец боя после фазы N (PhaseEnd)
        Dancers,       // два танцора с отдельным запасом здоровья на фазу (Cogwork / Clover Dancers): фазы пропускаем «роняя» танцора, бой кончаем через их же смерть (PhaseDancers.cs)
        BellEater,     // Bell Eater: в таблице GoP не фазы, а [здоровье головы, здоровье зада, урон до второй фазы, урон до третьей]; конец боя после фазы — как у Pool
        Lanterns,      // Father of the Flame: в таблице четыре руки, порог второй фазы и ядро; руки «ломаем» заранее (PhaseFather)
        Single         // босс без фаз (одна строка в таблице GoP): у него можно менять только здоровье (SingleDefs)
    }

    /// Куда перенаправить бой, чтобы он кончился вместе с выбранной фазой (боссы с отдельным запасом здоровья на фазу).
    internal sealed class PoolEnd
    {
        internal string Object;     // имя объекта с FSM (как в дампе)
        internal string Fsm;        // имя FSM
        internal string[] Checks;   // Checks[k-1] — состояние проверки здоровья в конце фазы k; его переход «NEXT» ведёт в следующую фазу
        internal string Death;      // состояние смерти босса
        internal string Event;      // событие перехода в Checks[k-1], который перенаправляем; пусто — «NEXT»
    }

    /// Босс-«танцоры»: FSM «Dancer Control» ставит каждой фазе свой запас здоровья танцорам (SetHP), конец фазы — стан танцора
    /// (событие «ZERO HP» у него самого), после стана «Dancer Control» по глобальному переходу «DANCER STUNNED» идёт к следующей фазе.
    internal sealed class DancersPlan
    {
        internal string Scene;        // сцена, где лежат объекты боя (у Cogwork это «Cog_Dancers_boss», а GoP грузит «Cog_Dancers»)
        internal string Object;       // объект с главным FSM
        internal string Fsm;          // и у него, и у танцора FSM называется так
        internal string Dancer;       // кого «роняем» (дочерний объект главного)
        internal string KickEvent;    // событие, которым танцор уходит в стан (нужно там, где после стана танцор умирает)
        internal string StunEvent;    // событие главному FSM «танцоры оглушены» (глобальный переход): так пропускаем фазы без стана и смерти танцора
        internal string StartPrefix;  // «Start Phase » + номер — состояние главного FSM на входе в фазу
        internal string StunState;    // состояние «танцоры оглушены»: сравнивает номер фазы с порогом смерти
        internal string KilledVar;    // счётчик убитых танцоров (на втором бой кончается)
    }

    /// Один босс с настройкой фазы.
    internal sealed class BossDef
    {
        internal string Boss;     // имя босса, как в пресетах и в GoP
        internal PhaseRule Rule;
        internal int Phases;      // сколько фаз
        internal int TableLen;    // сколько строк в таблице GoP, если их не столько же, сколько фаз (Bell Eater, Father of the Flame); 0 — столько же
        internal string[] Keys;   // ключи таблицы EnemyHp.enemies в GoP (у двойных боёв их два)
        internal string Object;   // имя объекта с HealthManager (только для правила Vanilla)
        internal PoolEnd End;     // только для правила Pool
        internal DancersPlan Dancers; // только для правила Dancers
    }

    internal static class PhaseLock
    {
        /// Режим: 0 — как в игре, N — фаза N (с единицы).
        internal const int All = 0, First = 1, Second = 2;

        internal const string Trobbio = "Trobbio";

        static BossDef D(string boss, PhaseRule rule, int phases, params string[] keys)
        {
            return new BossDef { Boss = boss, Rule = rule, Phases = phases, Keys = keys.Length > 0 ? keys : new[] { boss } };
        }

        static BossDef V(string boss, int phases, string objectName)
        {
            return new BossDef { Boss = boss, Rule = PhaseRule.Vanilla, Phases = phases, Keys = new string[0], Object = objectName };
        }

        /// Босс, у которого каждая фаза — отдельный запас здоровья. checks — состояния проверки здоровья по фазам.
        static BossDef P(string boss, int phases, string objectName, string fsm, string death, params string[] checks)
        {
            return new BossDef
            {
                Boss = boss, Rule = PhaseRule.Pool, Phases = phases, Keys = new[] { boss },
                End = new PoolEnd { Object = objectName, Fsm = fsm, Death = death, Checks = checks }
            };
        }

        /// Босс-танцоры (правило Dancers): phases — сколько фаз в таблице GoP, scene — сцена с объектами боя.
        static BossDef Dn(string boss, int phases, string scene)
        {
            return new BossDef
            {
                Boss = boss, Rule = PhaseRule.Dancers, Phases = phases, Keys = new[] { boss },
                Dancers = new DancersPlan
                {
                    Scene = scene, Object = "Dancer Control", Fsm = "Control", Dancer = "Dancer A", KickEvent = "ZERO HP", StunEvent = "DANCER STUNNED",
                    StartPrefix = "Start Phase ", StunState = "Dancers Stunned", KilledVar = "Dancers Killed"
                }
            };
        }

        /// Bell Eater: три фазы при четырёх строках таблицы GoP.
        static BossDef Be(string boss)
        {
            return new BossDef
            {
                Boss = boss, Rule = PhaseRule.BellEater, Phases = 3, TableLen = 4, Keys = new[] { boss },
                End = new PoolEnd { Object = "Centipede Control", Fsm = "Control", Death = "Can Die", Checks = new[] { "P1", "P2", "Set P3" } }
            };
        }

        /// Father of the Flame: три фазы (руки до порога, руки после порога, ядро) при шести строках таблицы GoP.
        static BossDef Ln(string boss)
        {
            return new BossDef
            {
                Boss = boss, Rule = PhaseRule.Lanterns, Phases = 3, TableLen = 6, Keys = new[] { boss },
                // когда сломана последняя рука, «Broken Pause» ведёт к ядру (Flare Up); в фазах 1 и 2 ведём сразу к смерти босса
                End = new PoolEnd { Object = "Wisp Pyre Effigy", Fsm = "Summon Control", Death = "Final Hit", Event = "FINISHED",
                    Checks = new[] { "Broken Pause", "Broken Pause", "Broken Pause" } }
            };
        }

        /// Боссы, у которых фазу можно зафиксировать, в порядке Пантеона 5.
        internal static readonly BossDef[] Defs =
        {
            D("Moss Mother", PhaseRule.Cumulative, 4, "Moss Mother", "Moss Mother Double 1", "Moss Mother Double 2"),
            D(Tokens.MossAsc, PhaseRule.Cumulative, 4, "Moss Mother Double 1", "Moss Mother Double 2"),
            D("Bell Beast", PhaseRule.Cumulative, 3),
            D("Savage Beastfly in Chapel of The Beast", PhaseRule.Cumulative, 3),
            D("Lace in Deep Docks", PhaseRule.Cumulative, 2),
            D("Fourth Chorus", PhaseRule.Cumulative, 4),
            D("Moorwing", PhaseRule.Cumulative, 2),
            D("Great Conchflies", PhaseRule.DirectSecond, 2),
            D("Sister Splinter", PhaseRule.Cumulative, 3),
            D("Widow", PhaseRule.Cumulative, 3),
            D("The Last Judge", PhaseRule.Cumulative, 3),
            V("Broodmother", 2, "Slab Fly Broodmother"),
            D("Nyleth", PhaseRule.Cumulative, 2),
            D("Voltvyrm", PhaseRule.Cumulative, 3),
            D(Trobbio, PhaseRule.Trobbio, 2),
            Dn("Clover Dancers", 2, "Clover_10"),
            Dn("Cogwork Dancers", 4, "Cog_Dancers_boss"),
            D("Disgraced Chef Lugoli", PhaseRule.Cumulative, 2),
            D("Raging Conchfly", PhaseRule.Cumulative, 2),
            D("Groal the Great", PhaseRule.Cumulative, 2),
            D("Crust King Khann", PhaseRule.Cumulative, 3),
            D("Phantom", PhaseRule.Cumulative, 3),
            D("Crawfather", PhaseRule.Cumulative, 2),
            D("Forebrothers Signis & Gron", PhaseRule.Cumulative, 4, "Forebrothers_Sigins"),
            D("Second Sentiel", PhaseRule.Cumulative, 2),
            D("Lace in the Cradle", PhaseRule.Cumulative, 3),
            D("Plasmified Zango", PhaseRule.Cumulative, 5),
            D("Watcher at the Edge", PhaseRule.Cumulative, 2),
            D("The Unravelled", PhaseRule.Cumulative, 3),
            D("First Sinner", PhaseRule.Cumulative, 3),
            Be("Bell Eater"),
            Ln("Father of the Flame"),
            D("Palestag", PhaseRule.DirectFirst, 2),
            D("Gurr the Outcast", PhaseRule.Cumulative, 3),
            D("Tormented Trobbio", PhaseRule.Cumulative, 3),
            D("Shrine Guardian Seth", PhaseRule.Cumulative, 3),
            D("Savage Beastfly in Far Fields", PhaseRule.Cumulative, 3),
            D("Pinstress", PhaseRule.Cumulative, 2),
            D("Skarrsinger Karmelita", PhaseRule.Cumulative, 3),
            P("Grand Mother Silk", 6, "Silk Boss", "Phase Control", "Death Hit",
                "HP Check 1", "HP Check 2", "HP Check 3", "HP Check 4", "HP Check 5", "HP Check 6"),
            D("Lost Lace", PhaseRule.Cumulative, 4),
        };

        /// Боссы без фаз: в таблице GoP у них одна строка, настройка — только своё здоровье (0.19.0). Фаз и смен фаз у них нет.
        internal static readonly BossDef[] SingleDefs =
        {
            Sg("Skull Tyrant"),
            Sg("Lost Garmond"),
            Sg("Shakra"),
            Sg("Garmond & Zaza"),
        };

        static BossDef Sg(string boss)
        {
            return new BossDef { Boss = boss, Rule = PhaseRule.Single, Phases = 1, Keys = new[] { boss } };
        }

        static readonly Dictionary<string, BossDef> byName = Index(Defs);
        static readonly Dictionary<string, BossDef> singleByName = Index(SingleDefs);

        static Dictionary<string, BossDef> Index(BossDef[] defs)
        {
            var map = new Dictionary<string, BossDef>();
            foreach (BossDef d in defs) map[d.Boss] = d;
            return map;
        }

        /// Своё здоровье: наименьшее и наибольшее значение, которое можно задать (0 — как в игре).
        internal const int HpMin = 1, HpMax = 9999;

        /// Запись босса для своего здоровья: босс с фазами (Defs) или без них (SingleDefs). null — здоровье этого босса менять нельзя.
        internal static BossDef FindHp(string boss)
        {
            BossDef d = Find(boss);
            if (d != null) return d;
            if (boss == null || !singleByName.TryGetValue(boss, out d)) return null;
            return d;
        }

        /// Можно ли у этого босса задать своё здоровье.
        internal static bool HpCapable(string boss)
        {
            return FindHp(boss) != null;
        }

        /// Можно ли сделать босса бессмертным (0.22.0). Нужен босс с общим запасом здоровья: у Pool, Dancers, BellEater и Lanterns
        /// каждая фаза — отдельный запас, и фазы меняются через «здоровье кончилось», поэтому защита «здоровье не ниже 1» их сломала бы.
        internal static bool ImmortalCapable(string boss)
        {
            BossDef d = FindHp(boss);
            if (d == null) return false;
            switch (d.Rule)
            {
                case PhaseRule.Pool:
                case PhaseRule.Dancers:
                case PhaseRule.BellEater:
                case PhaseRule.Lanterns:
                    return false;
                default:
                    return true;
            }
        }

        /// Своё здоровье в допустимых границах: 0 (как в игре) или от HpMin до HpMax; у босса без такой настройки всегда 0.
        internal static int NormalizeHp(string boss, int hp)
        {
            if (hp < HpMin || !HpCapable(boss)) return 0;
            return Math.Min(hp, HpMax);
        }

        internal static BossDef Find(string boss)
        {
            BossDef d;
            if (boss == null || !byName.TryGetValue(boss, out d)) return null;
            return d;
        }

        internal static bool Supports(string boss)
        {
            return Find(boss) != null;
        }

        /// Сколько фаз у босса (0 — у босса нет настройки).
        internal static int Count(string boss)
        {
            BossDef d = Find(boss);
            return d == null ? 0 : d.Phases;
        }

        /// Режим в допустимых границах для этого босса: 0 (как в игре) или номер фазы.
        internal static int Normalize(string boss, int mode)
        {
            int count = Count(boss);
            return mode >= 1 && mode <= count ? mode : All;
        }

        /// Таблица здоровья фаз для режима «только фаза mode» (с единицы). orig — здоровье каждой фазы в GoP.
        /// phases — новая таблица, total — полное здоровье босса. false — ничего менять не надо или данных не хватает.
        internal static bool TablePlan(PhaseRule rule, int mode, bool full, int[] orig, out int[] phases, out int total)
        {
            return TablePlan(rule, mode, full, 0, orig, out phases, out total);
        }

        /// То же со своим здоровьем custom (0 — не задано). Своё здоровье — всегда тренировка (full игнорируется).
        /// mode 0 («все фазы») имеет смысл только со своим здоровьем: таблица масштабируется (ScalePlan).
        internal static bool TablePlan(PhaseRule rule, int mode, bool full, int custom, int[] orig, out int[] phases, out int total)
        {
            phases = null;
            total = 0;
            if (custom < 0 || custom > HpMax) return false;
            if (custom > 0) full = false;
            if (rule == PhaseRule.Single) return mode < 1 && custom > 0 && ScalePlan(rule, custom, orig, out phases, out total);
            if (mode < 1) return custom > 0 && ScalePlan(rule, custom, orig, out phases, out total);
            if (rule == PhaseRule.BellEater) return BellEaterPlan(mode, full, custom, orig, out phases, out total);
            if (rule == PhaseRule.Lanterns) return LanternsPlan(mode, full, custom, orig, out phases, out total);
            if (orig == null || orig.Length < 2) return false;
            int n = orig.Length;
            if (mode < 1 || mode > n) return false;
            int sum = 0;
            foreach (int h in orig)
            {
                if (h <= 0) return false;
                sum += h;
            }

            int p = mode - 1;
            total = custom > 0 ? custom : (full ? sum : orig[p]);
            var result = new int[n];
            switch (rule)
            {
                case PhaseRule.Cumulative:
                case PhaseRule.Pool:
                    // Pool: FSM ставит здоровье первой фазы (SetHP), к остальным добавляет (AddHP), поэтому цифры те же:
                    // фазы до N дают −1 (в сумме −(N−1)), фаза N добавляет «нужное + N−1» — выходит ровно нужное
                    for (int i = 0; i < p; i++) result[i] = -1;
                    result[p] = total + p;
                    break;
                case PhaseRule.Dancers:
                    // у каждой фазы свой запас (SetHP при входе в фазу), поэтому остальные фазы остаются как в GoP, а у выбранной — нужный запас
                    for (int i = 0; i < n; i++) result[i] = orig[i];
                    result[p] = total;
                    break;
                case PhaseRule.DirectFirst:
                    if (n != 2) return false;
                    result[0] = p == 0 ? 0 : total + 1;
                    result[1] = total;
                    break;
                case PhaseRule.DirectSecond:
                    if (n != 2) return false;
                    result[0] = total;
                    result[1] = p == 0 ? 0 : total + 1;
                    break;
                default:
                    total = 0;
                    return false;
            }
            phases = result;
            return true;
        }

        /// Bell Eater. Таблица GoP: [голова, зад, p1, p2]. Общее здоровье = голова + зад, порог второй фазы = общее − p1,
        /// порог третьей = порог второй − p2; фаза кончается, когда общее здоровье упало НИЖЕ порога (так же и сама смерть: ниже нуля).
        /// Фазы в ванили: первая — p1 (300), вторая — p2 (200), третья — остаток (300).
        /// Режим «только фаза N»: общее здоровье — нужное (у челленджа полное), пороги ставим так, чтобы фазы до N кончались сразу
        /// (порог выше здоровья: p1 = −1 даёт порог «здоровье + 1»), а фаза N кончалась на нуле (порог 0) — этот конец ведёт на смерть (EndPlan).
        internal static bool BellEaterPlan(int mode, bool full, int[] orig, out int[] phases, out int total)
        {
            return BellEaterPlan(mode, full, 0, orig, out phases, out total);
        }

        internal static bool BellEaterPlan(int mode, bool full, int custom, int[] orig, out int[] phases, out int total)
        {
            phases = null;
            total = 0;
            if (orig == null || orig.Length != 4) return false;
            foreach (int h in orig) if (h <= 0) return false;
            int sum = orig[0] + orig[1];
            int third = sum - orig[2] - orig[3];
            if (third <= 0 || mode < 1 || mode > 3) return false;
            int own = mode == 1 ? orig[2] : mode == 2 ? orig[3] : third;
            total = custom > 0 ? custom : (full ? sum : own);
            if (total < 2) { total = 0; return false; }
            int head = (total + 1) / 2, butt = total - head;
            int p1, p2;
            if (mode == 1) { p1 = total; p2 = 0; }                 // порог второй фазы 0, третьей 0
            else if (mode == 2) { p1 = -1; p2 = total + 1; }       // порог второй фазы total+1 (прошли сразу), третьей 0
            else { p1 = -1; p2 = 0; }                              // обе пройдены сразу, остаётся третья
            phases = new[] { head, butt, p1, p2 };
            return true;
        }

        /// Father of the Flame. Таблица GoP: [рука BL, рука TL, рука TR, рука BR, «порог второй фазы», ядро].
        /// Фазы: 1 — руки, пока их общее здоровье выше порога (в ванили 400 → 200: 200 урона), 2 — руки ниже порога (200 урона), 3 — ядро (250).
        /// Фаза 1: четыре руки делят нужное здоровье, порог 0 (весь бой первая фаза). Фаза 2: две руки (BL и TR) делят нужное здоровье,
        /// остальные две «сломаны заранее» (здоровье 0, PhaseFather бьёт по ним), порог равен всему здоровью рук (весь бой вторая фаза).
        /// Фаза 3: все руки сломаны заранее, ядро получает нужное здоровье. Конец боя после фаз 1 и 2 — когда сломана последняя рука (PhaseFather).
        internal static bool LanternsPlan(int mode, bool full, int[] orig, out int[] phases, out int total)
        {
            return LanternsPlan(mode, full, 0, orig, out phases, out total);
        }

        internal static bool LanternsPlan(int mode, bool full, int custom, int[] orig, out int[] phases, out int total)
        {
            phases = null;
            total = 0;
            if (orig == null || orig.Length != 6) return false;
            foreach (int h in orig) if (h <= 0) return false;
            int arms = orig[0] + orig[1] + orig[2] + orig[3], half = orig[4], core = orig[5];
            if (mode < 1 || mode > 3 || half >= arms) return false;
            int own = mode == 1 ? arms - half : mode == 2 ? half : core;
            total = custom > 0 ? custom : (full ? arms + core : own);
            var r = new int[6];
            r[4] = 0;
            r[5] = core;
            if (mode == 1)
            {
                if (total < 4) { total = 0; return false; }
                Spread(total, r, 0, 1, 2, 3);
            }
            else if (mode == 2)
            {
                if (total < 2) { total = 0; return false; }
                Spread(total, r, 0, 2);
                r[4] = total;
            }
            else r[5] = total;
            phases = r;
            return true;
        }

        /// Своё здоровье без выбранной фазы («все фазы»): таблица GoP масштабируется так, чтобы всё здоровье стало custom, а фазы сохранили свои доли.
        /// Cumulative / Pool / Dancers / Direct — каждая строка по доле (сумма ровно custom, у каждой не меньше 1); Bell Eater — доли трёх фаз
        /// (голова и зад делят всё здоровье); Father of the Flame — доли рук и ядра, порог второй фазы пропорционально; Single — одна строка.
        /// false — слишком маленькое число для стольких фаз или таблица не подходит.
        internal static bool ScalePlan(PhaseRule rule, int custom, int[] orig, out int[] phases, out int total)
        {
            phases = null;
            total = 0;
            if (custom < HpMin || custom > HpMax || orig == null || orig.Length == 0) return false;
            foreach (int h in orig) if (h <= 0) return false;
            int[] parts;
            switch (rule)
            {
                case PhaseRule.Single:
                    if (orig.Length != 1) return false;
                    phases = new[] { custom };
                    break;
                case PhaseRule.Cumulative:
                case PhaseRule.Pool:
                case PhaseRule.Dancers:
                case PhaseRule.DirectFirst:
                case PhaseRule.DirectSecond:
                    if (orig.Length < 2 || ((rule == PhaseRule.DirectFirst || rule == PhaseRule.DirectSecond) && orig.Length != 2)) return false;
                    phases = Distribute(custom, orig);
                    if (phases == null) return false;
                    break;
                case PhaseRule.BellEater:
                {
                    // [голова, зад, урон до второй фазы, урон до третьей]: доли трёх фаз — p1, p2 и остаток
                    if (orig.Length != 4) return false;
                    int sum = orig[0] + orig[1], third = sum - orig[2] - orig[3];
                    if (third <= 0) return false;
                    parts = Distribute(custom, new[] { orig[2], orig[3], third });
                    if (parts == null) return false;
                    int head = (custom + 1) / 2;
                    phases = new[] { head, custom - head, parts[0], parts[1] };
                    break;
                }
                case PhaseRule.Lanterns:
                {
                    // [рука BL, TL, TR, BR, порог второй фазы, ядро]: руки и ядро делят custom по долям, порог — доля здоровья рук
                    if (orig.Length != 6) return false;
                    int arms = orig[0] + orig[1] + orig[2] + orig[3];
                    if (orig[4] >= arms) return false;
                    parts = Distribute(custom, new[] { arms, orig[5] });
                    if (parts == null || parts[0] < 4) return false;
                    int half = (int)(((long)orig[4] * parts[0] + arms / 2) / arms);
                    half = Math.Max(1, Math.Min(parts[0] - 1, half));
                    var r = new int[6];
                    Spread(parts[0], r, 0, 1, 2, 3);
                    r[4] = half;
                    r[5] = parts[1];
                    phases = r;
                    break;
                }
                default:
                    return false;
            }
            total = custom;
            return true;
        }

        /// Делит total между частями по весам: у каждой не меньше 1, сумма ровно total (остаток — по наибольшим дробным долям).
        /// null — total меньше числа частей или веса не годятся.
        internal static int[] Distribute(int total, int[] weights)
        {
            if (weights == null || weights.Length == 0 || total < weights.Length) return null;
            int n = weights.Length;
            long sum = 0;
            foreach (int w in weights)
            {
                if (w <= 0) return null;
                sum += w;
            }
            long rest = total - n;
            var result = new int[n];
            var frac = new long[n];
            long given = 0;
            for (int i = 0; i < n; i++)
            {
                long num = rest * weights[i];
                result[i] = 1 + (int)(num / sum);
                frac[i] = num % sum;
                given += num / sum;
            }
            for (long left = rest - given; left > 0; left--)
            {
                int best = 0;
                for (int i = 1; i < n; i++) if (frac[i] > frac[best]) best = i;
                result[best]++;
                frac[best] = -1;
            }
            return result;
        }

        /// Всё здоровье босса в таблице GoP (для подсказки «в игре: N»): 0 — не знаем (двойные бои, ванильные боссы).
        internal static int VanillaTotal(PhaseRule rule, int[] orig)
        {
            if (orig == null || orig.Length == 0) return 0;
            int sum = 0;
            switch (rule)
            {
                case PhaseRule.BellEater:
                    return orig.Length == 4 ? orig[0] + orig[1] : 0;
                case PhaseRule.Lanterns:
                    return orig.Length == 6 ? orig[0] + orig[1] + orig[2] + orig[3] + orig[5] : 0;
                case PhaseRule.Vanilla:
                    return 0;
                default:
                    foreach (int h in orig) sum += h;
                    return sum;
            }
        }

        /// Делит total поровну между руками idx; остаток — первым по списку.
        static void Spread(int total, int[] result, params int[] idx)
        {
            int each = total / idx.Length, extra = total % idx.Length;
            for (int i = 0; i < idx.Length; i++) result[idx[i]] = each + (i < extra ? 1 : 0);
        }

        /// Какие руки Отца пламени надо сломать заранее (здоровье 0 в таблице): 4 флага в порядке таблицы (BL, TL, TR, BR).
        internal static bool[] LanternsBroken(int[] table)
        {
            var broken = new bool[4];
            if (table == null || table.Length != 6) return broken;
            for (int i = 0; i < 4; i++) broken[i] = table[i] <= 0;
            return broken;
        }

        /// Нужна ли у Отца пламени остановка боя на последней сломанной руке (фазы 1 и 2: до ядра дело не доходит).
        internal static bool LanternsEndAtArms(string boss, int mode)
        {
            BossDef d = Find(boss);
            return d != null && d.Rule == PhaseRule.Lanterns && (mode == 1 || mode == 2);
        }

        /// Распутыватель: порог третьей фазы («P3 HP») = порог второй («P2 HP») − здоровье второй фазы. Так его считает GoP в начале боя,
        /// но FSM босса сразу после этого пересчитывает «P3 HP» как «здоровье × 0,4» (последнее действие Init) — см. PhaseUnravel.
        internal static int UnravelP3(int p2Hp, int secondPhaseHp)
        {
            return p2Hp - secondPhaseHp;
        }

        /// Куда перенаправить конец боя для режима «только фаза mode» (босс с отдельным запасом здоровья на фазу).
        /// null — перенаправлять нечего: у босса нет такого правила или выбрана последняя фаза (её конец и есть смерть).
        internal static PoolEnd EndPlan(string boss, int mode)
        {
            BossDef d = Find(boss);
            if (d == null || d.End == null || (d.Rule != PhaseRule.Pool && d.Rule != PhaseRule.BellEater && d.Rule != PhaseRule.Lanterns)) return null;
            if (mode < 1 || mode >= d.Phases) return null;
            if (d.End.Checks == null || d.End.Checks.Length != d.Phases) return null;
            return d.End;
        }

        /// План для босса-танцоров в режиме «фаза mode» (с единицы). null — у босса нет такого правила или режим вне границ.
        internal static DancersPlan DancersFor(string boss, int mode)
        {
            BossDef d = Find(boss);
            if (d == null || d.Dancers == null || d.Rule != PhaseRule.Dancers) return null;
            if (mode < 1 || mode > d.Phases) return null;
            return d.Dancers;
        }

        /// Нужно ли перенаправлять конец боя: после последней фазы таблицы бой и так кончается, после остальных — нет.
        internal static bool DancersEndEarly(string boss, int mode)
        {
            BossDef d = Find(boss);
            return d != null && d.Dancers != null && mode >= 1 && mode < d.Phases;
        }

        /// Номер фазы из имени состояния «Start Phase 3» (0 — это другое состояние).
        internal static int StartNo(string prefix, string state)
        {
            if (string.IsNullOrEmpty(prefix) || string.IsNullOrEmpty(state)) return 0;
            if (!state.StartsWith(prefix, StringComparison.Ordinal)) return 0;
            int n;
            if (!int.TryParse(state.Substring(prefix.Length), out n)) return 0;
            return n > 0 ? n : 0;
        }

        /// Здоровье и порог второй фазы Троббио (отдельный путь). phaseHp — здоровье каждой фазы по порядку.
        /// full — челлендж: здоровье остаётся полным (сумма фаз), босс только не выходит из выбранной фазы.
        /// false — ничего менять не надо (обе фазы или данных не хватает).
        internal static bool Plan(int mode, bool full, int[] phaseHp, out int hp, out int secondPhaseAt)
        {
            return Plan(mode, full, 0, phaseHp, out hp, out secondPhaseAt);
        }

        /// То же со своим здоровьем custom (0 — не задано): здоровье босса — custom; без выбранной фазы порог второй — её доля от custom.
        internal static bool Plan(int mode, bool full, int custom, int[] phaseHp, out int hp, out int secondPhaseAt)
        {
            hp = 0;
            secondPhaseAt = 0;
            if (phaseHp == null || phaseHp.Length < 2) return false;
            if (custom < 0 || custom > HpMax) return false;
            if (mode != First && mode != Second && !(mode == All && custom > 0)) return false;
            if (phaseHp[0] <= 0 || phaseHp[1] <= 0) return false;
            if (custom > 0)
            {
                int[] parts = mode == All ? Distribute(custom, new[] { phaseHp[0], phaseHp[1] }) : null;
                if (mode == All && parts == null) return false;
                hp = custom;
                secondPhaseAt = mode == First ? 0 : mode == Second ? hp + 1 : parts[1];
                return true;
            }

            if (full)
            {
                int total = 0;
                foreach (int h in phaseHp) total += h;
                hp = total;
            }
            else hp = phaseHp[mode == First ? 0 : 1];

            // фаза 1: порог 0 — вторая не наступит; фаза 2: порог выше здоровья — запасной путь (переход с рёвом)
            secondPhaseAt = mode == First ? 0 : hp + 1;
            return true;
        }

        /// Метка в ключе статистики: у челленджа свои цифры, чтобы не смешивать их с обычным боем. Пусто — обычный бой.
        internal static string Variant(int mode, bool full)
        {
            if (!full || mode < 1) return "";
            return "phase " + mode;
        }

        /// Подпись к имени босса в таблице статистики: « · фаза 2». Пусто — обычный бой.
        internal static string Suffix(int mode, bool full)
        {
            if (!full || mode < 1) return "";
            return L.T(" · фаза ", " · phase ") + mode;
        }

        /// «только фаза 2» (тренировка) или «фаза 2 весь бой» (челлендж) — для сообщений и журнала.
        internal static string Describe(int mode, bool full)
        {
            if (mode < 1) return L.T("все фазы", "all phases");
            return full ? L.T("фаза ", "phase ") + mode + L.T(" весь бой", " all fight")
                        : L.T("только фаза ", "phase ") + mode + L.T("", " only");
        }

        /// То же со своим здоровьем: «здоровье 500» (без фазы) или «только фаза 2 · здоровье 500». hp 0 — как Describe.
        internal static string DescribeHp(int mode, bool full, int hp)
        {
            if (hp <= 0) return Describe(mode, full);
            string own = L.T("здоровье ", "health ") + hp;
            return mode < 1 ? own : Describe(mode, false) + " · " + own;
        }

        /// «2 фазы», «3 фазы», «5 фаз» — подпись у босса в списке.
        internal static string CountText(int n)
        {
            if (!L.IsRu) return n + " phases";
            int last = n % 10, tens = n % 100;
            string word = (tens >= 11 && tens <= 14) || last == 0 || last >= 5 ? "фаз" : last == 1 ? "фаза" : "фазы";
            return n + " " + word;
        }
    }
}
