// ============================================================================
//  PhaseSkipData.cs — какие цепочки состояний FSM босса пропускать («без рёвов»). Без Unity: проверяется тестами
//  по дампам FSM (/tmp/fsm1/boss_fsm), см. тест «цепочки рёвов совпадают с дампами».
//
//  Зачем. Когда босс заперт в фазе N (PhaseLock), бой начинается с полного здоровья или с запасом, и босс
//  проходит смены фаз 2…N подряд: рёв, уход, приземление. Для тренировки это пустая трата времени.
//  Как. У каждой смены фазы в FSM босса есть цепочка состояний (Antic → Roar → Recover …), которая
//  идёт по событию FINISHED (или по другому, если указано «Состояние/СОБЫТИЕ»). Пока босс в такой цепочке,
//  мы сами шлём ей это событие каждый кадр, и она пролетает за кадр-другой. Состояния входят как обычно,
//  поэтому всё, что они ставят (флаги, неуязвимость, звук), успевает выполниться; пропускается только ожидание.
//  Первое состояние цепочки — «вход»: по нему считаем смены (пропускаем первые N−1).
//  Формат цепочки: "A>B>C" или "A/EVENT>B", «/EVENT» — какое событие слать вместо FINISHED.
//  Что менять: новый босс — строка в Rules; имя объекта и FSM — из BossFsmDump (bosses.txt).
// ============================================================================
using System.Collections.Generic;

namespace Encore
{
    internal sealed class SkipRule
    {
        internal string Boss;       // как в PhaseLock.Defs
        internal string Object;     // имя объекта с HealthManager и FSM (как в дампе)
        internal string Fsm;        // имя FSM
        internal string[] Chains;
    }

    internal struct SkipStep
    {
        internal string State, Event;
    }

    internal static class SkipData
    {
        static SkipRule R(string boss, string obj, string fsm, params string[] chains)
        {
            return new SkipRule { Boss = boss, Object = obj, Fsm = fsm, Chains = chains };
        }

        // Косметические действия FSM: звук, тряска камеры, выброс рёва, частицы, вибрация геймпада. Ничего не решают
        // в логике боя, поэтому в пропускаемых цепочках их отключаем (иначе рёв «играет» разом за один кадр).
        static readonly string[] MuteTypes = new string[]
        {
            "AudioPlayerOneShotSingle", "AudioPlayerOneShot", "AudioPlayRandomVoiceFromTable", "AudioPlayRandomVoiceFromTableV2",
            "AudioPlayRandomVoice", "AudioPlaySimple", "AudioPlayInState", "PlayAudioEvent", "PlayAudioEventRandom",
            "StartRoarEmitter", "DoCameraShake", "PlayParticleEmitter", "PlayParticleEmitterInState", "PlayParticleEmitterChildren",
            "PlayVibrationV3"
        };

        // Что ещё у конкретного босса чисто косметика: Судья включает вспышки «Roar Glow Antic»/«Roar Glow» (дети объектов-эффектов).
        static readonly string[][] MuteExtra = new string[][]
        {
            new string[] { "The Last Judge", "ActivateAllChildren" },
            // Лучина: «Spike Spree» шлёт шипам событие «шипы посыпались» — это первая атака после рёва третьей фазы, а не часть рёва
            new string[] { "Sister Splinter", "SendEventToRegister" },
            // Raging Conchfly: «ROOF SLAM» и «DRILL INTRO ROAR» — события на звук и обвал потолка, без рёва они играли одиночным странным звуком
            new string[] { "Raging Conchfly", "SendEventToRegister" }
        };

        /// События-«вызов подкрепления»: в пропускаемых цепочках их шлёт только последняя из пропущенных (фаза, с которой начинается бой), остальные
        /// глушим. Forebrothers: «P2/P3 Roar End» шлют сцене «CALL ENEMIES», «P4 Roar End» — «CALL THROWER» (приходит второй брат). Без этого при
        /// выбранной фазе 4 в самом начале боя разом приходили подкрепления трёх фаз.
        internal static bool IsCallEvent(string boss, string eventName)
        {
            return boss == "Forebrothers Signis & Gron" && eventName != null && eventName.StartsWith("CALL ", System.StringComparison.Ordinal);
        }

        // ---- «смена фазы без удара» (PhaseKick) ----

        /// Состояние-«ворота»: единственный переход — «TOOK DAMAGE» в состояние проверки («… Check …»).
        internal static bool IsGate(string eventName, string toState)
        {
            return eventName == "TOOK DAMAGE" && toState != null && toState.IndexOf("check", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// Ворота, которых не видно по общему правилу (IsGate): Лейс — фазы сверяются в самом «Control» и только после удара или атаки:
        /// «Idle» → по «TOOK DAMAGE» в «P3 Check» (Колыбель) или «Attack Choice» (Потерянная Лейс), переходов у состояния много.
        /// Распутыватель — «Custom Stun Control»: «P1» → «P1 Stun?», «P2» → «P2 Stun?» (там порог здоровья и шлётся оглушение-смена фазы).
        internal static bool IdleGate(string boss, string fsmName, string stateName, string eventName, string toState)
        {
            if (eventName != "TOOK DAMAGE" || toState == null) return false;
            if (fsmName == "Control" && stateName == "Idle")
                return (boss == "Lace in the Cradle" && toState == "P3 Check") || (boss == "Lost Lace" && toState == "Attack Choice");
            return boss == "The Unravelled" && fsmName == "Custom Stun Control" && (stateName == "P1" || stateName == "P2") && toState == stateName + " Stun?";
        }

        /// Какие FSM можно будить: смерть и «притворная смерть» не трогаем (там «Check» — не про фазы).
        internal static bool KickableFsm(string fsmName)
        {
            if (fsmName == null) return false;
            return fsmName.IndexOf("death", System.StringComparison.OrdinalIgnoreCase) < 0 && fsmName != "Stun Control";
        }

        /// У Вдовы последняя фаза начинается «притворной смертью»: FSM «Fake Death» ждёт удара и сверяет здоровье с порогом
        /// третьей фазы. В последней фазе будим и его (у других боссов «притворная смерть» — не про фазы, их не трогаем).
        internal static bool KickFakeDeath(string boss, string fsmName, int phase)
        {
            return boss == "Widow" && fsmName == "Fake Death" && phase >= 3;
        }

        /// Четвёртый Хор: «Phase Control» при каждой смене фазы шлёт голему STUN (оглушение). Когда фаз пропускают несколько,
        /// «удары» идут подряд за пару кадров, и голем получает 3 оглушения разом. Оставляем STUN только у последней смены:
        /// у предыдущих «Stun K» глушим отправку (Phase всё равно ставится каждым состоянием). Имена состояний — «Stun 1», «Stun 2», …
        internal static List<string> QuietStunStates(string boss, int phase)
        {
            var list = new List<string>();
            if (boss != "Fourth Chorus") return list;
            for (int k = 1; k <= phase - 2; k++) list.Add("Stun " + k);
            return list;
        }

        // ---- «уже было» (PhaseSkip): флаг FSM, который в игре ставит рёв/ярость ----
        // Часть переходов нельзя пролететь цепочкой: за ними идёт настоящая атака (дождь колоколов после ярости, вращение с огнём
        // после рёва Судьи). Тогда в выбранной (последней) фазе просто ставим флаг, будто переход уже случился, и босс идёт дальше.
        internal sealed class Preset
        {
            internal string Boss, Object, Fsm, Var;
            internal int MinPhase;
        }

        static Preset P(string boss, string obj, string fsm, string var, int minPhase)
        {
            return new Preset { Boss = boss, Object = obj, Fsm = fsm, Var = var, MinPhase = minPhase };
        }

        internal static readonly Preset[] Presets = new Preset[]
        {
            // Колокольный зверь: «Raged» → «Rage?» сразу даёт FINISHED, ярость (рёв + дождь колоколов 10 с) не начинается
            P("Bell Beast", "Bone Beast", "Control", "Raged", 3),
            // Последняя судья: «Flamed Up» ставит «Flame Roar 2»; без него в 3-й фазе сначала шёл рёв огня и вращение кадила
            // с огнём (Flame Roar 5 → Spin Antic), и только потом ярость с длинным вращением. Флаг стоит — сразу ярость.
            P("The Last Judge", "Last Judge", "Control", "Flamed Up", 3)
        };

        // ---- «обход»: переход состояния ведёт сразу в проверку фазы (PhaseSkip.ApplyDetours) ----
        // У части боссов проверка фазы стоит только после первой атаки («Away» → «Phase Check»), поэтому смена фазы начиналась после неё.
        internal sealed class Detour
        {
            internal string Boss, Object, Fsm, From, To;
            internal int MinPhase;
        }

        static Detour Dt(string boss, string obj, string fsm, string from, string to, int minPhase)
        {
            return new Detour { Boss = boss, Object = obj, Fsm = fsm, From = from, To = to, MinPhase = minPhase };
        }

        internal static readonly Detour[] Detours = new Detour[]
        {
            // Raging Conchfly: после вступления «First Exit» (EXIT U/EXIT D) шлёт её в первое пикирование, и только «Away» → «Phase Check» запускает рёв
            // второй фазы. Ведём «First Exit» прямо в «Phase Check»: здоровье второй фазы → «P2 ROAR» → «P2 Roar Pos» (позиция и так задана числами).
            Dt("Raging Conchfly", "Coral Conch Driller Giant Solo", "Control", "First Exit", "Phase Check", 2)
        };

        internal static List<Detour> DetoursFor(string boss, int phase)
        {
            var list = new List<Detour>();
            foreach (Detour d in Detours)
                if (d.Boss == boss && phase >= d.MinPhase) list.Add(d);
            return list;
        }

        internal static List<Preset> PresetsFor(string boss, int phase)
        {
            var list = new List<Preset>();
            foreach (Preset p in Presets)
                if (p.Boss == boss && phase >= p.MinPhase) list.Add(p);
            return list;
        }

        /// Босс уже вышел из ожидания и вступления (по состоянию его «Control»): до этого событие может потеряться.
        internal static bool BossReady(string controlState)
        {
            if (string.IsNullOrEmpty(controlState)) return false;
            string[] early = { "Pause", "Init", "Dormant", "Intro", "Set Intro", "Entry", "Wake", "Start", "Door", "Appear", "Enter" };
            foreach (string e in early) if (controlState.StartsWith(e, System.StringComparison.Ordinal)) return false;
            // у Матери Шёлка вступление кончается тремя состояниями: ждёт начала («Ready»), заголовок («Title Up»), «Begin» (запускает атаки)
            // «Scream Start» — рёв вступления Вдовы; «Roar Antic» / «Roar» / «Roar End» и «Rest Idle» — вступление Сета (прыжок с места отдыха
            // на землю): толчок «P3 STUN» в это время подбрасывает его с неверной точки, он вылетает за карту и «Stun Air 2» не дожидается LAND
            // «Tele Antic Intro» — Распутыватель телепортируется на арену после рёва вступления: оглушение в этот миг сломает вход
            string[] exact = { "Ready", "Title Up", "Begin", "Scream Start", "Roar Antic", "Roar", "Roar End", "Rest Idle", "Tele Antic Intro" };
            foreach (string e in exact) if (controlState == e) return false;
            return true;
        }

        /// Сколько секунд после «готов» ещё ждать перед ударом (PhaseKick). Сет: после вступления ему нужно встать на землю и начать ходы.
        internal static float KickDelay(string boss)
        {
            return boss == "Shrine Guardian Seth" ? 1.0f : boss == "The Unravelled" ? 1.5f : 0f;
        }

        internal static bool IsMuted(string boss, string actionType)
        {
            if (System.Array.IndexOf(MuteTypes, actionType) >= 0) return true;
            for (int i = 0; i < MuteExtra.Length; i++)
                if (MuteExtra[i][0] == boss && System.Array.IndexOf(MuteExtra[i], actionType, 1) >= 0) return true;
            return false;
        }

        internal static readonly SkipRule[] Rules = new SkipRule[]
        {
            // «3|» — цепочка пропускается только при выбранной фазе ≥ 3 (в бою со 2-й фазы ярость идёт как в игре).
            // Дождь колоколов после ярости (Choose 1st Pattern → Ptn L/R) цепочкой не пролетишь: его «DROP» шлются с задержкой
            // и сыплются, даже когда состояние уже покинуто. Поэтому в 3-й фазе ярость вообще «уже была» (см. Presets).
            // Bell Eater: «P1» → «Wait For Clear» ждёт, пока обе головы доиграют атаку и встанут («Waiting»), поэтому смена фазы шла только после
            // первой атаки. Вход в цепочку («NEXT» от «P1») шлёт «Центипед Контрол», «Wait For Clear» пролетаем сразу событием NEXT: головы «Wait»
            // сбросит «Set Wide» (оно же шлёт ACT), смена арены идёт как обычно.
            R("Bell Eater", "Centipede Control", "Control",
                "Wait For Clear/NEXT>To P2 Pause>To P2>Set Wide"),
            R("Bell Beast", "Bone Beast", "Control",
                "3|Rage Pos>Rage Antic>Rage Burst>Rage Roar>Roar End>Rage Submerge>Rage Rumble"),
            // Savage Beastfly (обе комнаты) сознательно без правил: «рёв» у него — обычная атака (Choice → ROAR → призыв мух),
            // а цепочка «Start Rage…» за 112 записанных боёв ни разу не включалась (проверено по BossLiveLog).
            R("Forebrothers Signis & Gron", "Dock Guard Slasher", "Control",
                "P2 Roar Antic>P2 Roar>P2 Roar End",
                "P3 Roar Antic>P3 Roar>P3 Roar End",
                "P4 Roar Antic>P4 Roar>P4 Roar End"),
            R("Moorwing", "Vampire Gnat", "Control",
                "Rage Roar Antic>Rage Roar>Rage End"),
            R("Sister Splinter", "Splinter Queen", "Control",
                "P2 Antic>Roar Pos 2>Roar Antic 2>Roar 2>Roar End 2",
                "P3 Antic>Roar Pos 3>Roar Antic 3>Roar 3>Roar End 3>Spikes Leave 7>Spikes Leave 8>Spikes Leave 9>Shake Start>Spike Spree"),
            // Вдова: последняя фаза начинается «притворной смертью» (FSM «Fake Death» ждёт удара, см. PhaseKick) и яростью
            R("Widow", "Spinner Boss", "Control",
                "3|Death Stagger F>Fake Death End",
                "3|Rage Start 1>Rage Scream 2>Rage Scream End 2"),
            R("The Last Judge", "Last Judge", "Control",
                "Flame Roar 1>Flame Roar 2>Flame Roar 3>Flame Roar 4>Flame Roar 5",
                "Rage Roar 1>Rage Roar 2"),
            // Nyleth: пролетаем рёв («Shift Antic», «Shift Sing» 2,25 с). «Shift Break» (пол ломается, 0,5 с) и «Shift Recover» (анимация подъёма)
            // оставляем: если пролетать и их, она вставала в середине анимации и «Recover» играла один кадр — выглядело кривой анимацией.
            R("Nyleth", "Flower Queen Boss", "Control",
                "Shift Antic>Shift Sing"),
            R("Raging Conchfly", "Coral Conch Driller Giant Solo", "Control",
                "P2 Roar Pos>Roar EmergeAntic 2>Roar Poke 2>Roar Emerge 2/END>P2 Roar Antic>P2 Roar/END>P2 Roar End"),
            // Распутыватель: оглушение между фазами («Stun Hit» → «Stun Tele Out» → «Cancel Lag/Tag» → «Phase Check» → «Notify Scene»). «Cancel Lag/Tag»
            // в запись цепочки не поместить (косая черта — разделитель события), а он мгновенный. Волны шелухи между фазами пропускает PhaseUnravel
            // (переход сцены боя), здесь — само оглушение.
            R("The Unravelled", "Conductor Boss", "Control",
                "Stun Hit>Stun Tele Out"),
            R("Groal the Great", "Swamp Shaman", "Control",
                "Fly To Soul Roar/ATTACK>Soul Roar Antic>Soul Roar>Soul Roar End"),
            R("Crust King Khann", "Coral King", "Control",
                "P2 Roar>Roar Antic>Roar>Roar Wave End>Roar Recover",
                "P3 Roar Antic>P3 Roar"),
            R("Phantom", "Phantom", "Control",
                "Dragoon Rage>Dragoon Roar>Roar End"),
            R("Lace in the Cradle", "Lace Boss2 New", "Control",
                "P2 Shift 1>P2 Shift 2>P2 Restore Recoil",
                "P3 RoarAntic>P3 Roar>P3 Restore Recoil"),
            R("Skarrsinger Karmelita", "Hunter Queen Boss", "Control",
                "P2 Roar Antic>P2 Roar",
                "P3 Roar Antic>P3 Roar"),
            R("Lost Lace", "Lost Lace Boss", "Control",
                "P2 Shift 1>P2 Shift 2",
                "P4 Shift 1>P4 Shift 2"),
            R("Gurr the Outcast", "Bone Hunter Trapper", "Control",
                "Rage Roar Antic>Rage Roar"),
            R("First Sinner", "First Weaver", "Control",
                "Music Stop>P2 Roar>P2 Start>Set Bomb"),
            R("Tormented Trobbio", "Tormented Trobbio", "Control",
                "Phase Roar Antic>Phase Roar>Phase Roar End"),
            // Grand Mother Silk: два FSM. Рёв фаз 3 и 6 — в «Attack Control» (флаг Do Phase Roar → Roar Prepare → …); «Roar Prepare» не трогаем:
            // он ждёт ATTACK PREPARED от «Control», и если пролететь его раньше, «Control» зависнет в «Attack Prepare». Рёв после обвала арены
            // (фаза 4) — в «Control»: «Rerise Up» (подъём на 18 единиц за 1,2 с) оставляем, пропускаем только рёв и возврат в бой.
            // Рёв вступления («Intro Roar») не трогаем. Входов 2 и 1, а пропускается «фаза − 1» первых: лишние просто не наступят.
            R("Grand Mother Silk", "Silk Boss", "Attack Control",
                "Roar Antic>Roar>Roar End"),
            R("Grand Mother Silk", "Silk Boss", "Control",
                "Rerise Roar Antic>Bound 2>Rerise Roar>Rerise Roar End>Rerise End"),
        };

        /// Правила босса (может быть несколько объектов); пусто — для босса нет цепочек.
        internal static List<SkipRule> For(string boss)
        {
            var found = new List<SkipRule>();
            foreach (SkipRule rule in Rules)
                if (rule.Boss == boss) found.Add(rule);
            return found;
        }

        internal static bool Has(string boss)
        {
            foreach (SkipRule rule in Rules)
                if (rule.Boss == boss) return true;
            return false;
        }

        /// Префикс «N|» у цепочки: пропускать только при выбранной фазе ≥ N. Без префикса — 0 (по счёту входов, как раньше).
        internal static int MinPhase(string chain)
        {
            int bar = chain.IndexOf('|');
            int n;
            return bar > 0 && int.TryParse(chain.Substring(0, bar), out n) ? n : 0;
        }

        /// "A>B/END>C" -> [(A, FINISHED), (B, END), (C, FINISHED)]; префикс «N|» отбрасывается.
        internal static List<SkipStep> Parse(string chain)
        {
            var steps = new List<SkipStep>();
            int bar = chain.IndexOf('|');
            if (bar > 0) chain = chain.Substring(bar + 1);
            foreach (string part in chain.Split('>'))
            {
                var step = new SkipStep();
                int slash = part.IndexOf('/');
                step.State = slash < 0 ? part : part.Substring(0, slash);
                step.Event = slash < 0 ? "FINISHED" : part.Substring(slash + 1);
                steps.Add(step);
            }
            return steps;
        }
    }
}
