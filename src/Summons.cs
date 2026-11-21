// ============================================================================
//  Summons.cs — лимиты и шансы призывов и помощников босса (список видов и шансов — SummonData.cs). Не проверено в игре.
//  Босс не заменяется: правим действия его FSM, как только они появились в комнате (раз в 0,2 с ищем нужные FSM по имени объекта
//  и по тому, что в них есть нужные состояния; каждый FSM правится один раз). Бой с лимитом — тренировка, в статистику не идёт.
//
//  Broodmother (объект «Slab Fly Broodmother», FSM «Control»). Мух в пуле четыре: дети объекта «Spawner Flies» (Slab Fly Small Fresh,
//  (1), (2), (3); остальные «Small Fresh» — яйца и мухи волн, они в лимит не входят; до 0.26.5 я считал пул за три и лимит N давал N+1).
//  Вылупить муху она может, только если свободных больше нуля (состояния выбора атаки считают детей в «Free Flies» и сравнивают
//  с нулём в IntTestToBool, результат — «Can Hatch»). Живая муха пул покидает и возвращается, когда погибнет. Лимит N — «живых
//  одновременно не больше N»: в этих сравнениях ноль меняем на (пул−N), тогда вылупление возможно, лишь пока живых меньше N.
//  Двойное вылупление («Double Hatch»: RandomBool в «Hatch Antic») при лимите меньше пула выключаем — иначе вторая муха превысила бы лимит.
//
//  Матерь Мхов (объекты «Mossbone Mother…», FSM «Control»; в двойном бою их две). Камни: состояния «Rock» и «Rock - DB» шлют
//  сигнал «FALL» объекту «Stals» — выключаем эти SendEventByName (босс делает бросок, но камни не падают). Ползуны: «Crawler» шлёт
//  «SPAWN» первому призывателю и ставит «Spawned» (пока оно истинно, босс висит и ждёт гибели ползуна), «2nd Crawler» шлёт «SPAWN»
//  второму. Лимит 1 — выключаем сигнал второму; лимит 0 — оба сигнала и «Spawned = True».
//
//  Зверомуха (объект «Bone Flyer Giant», FSM «Control»; Часовня Зверя и Дальние поля). Враги ждут в пуле — детях объекта «Summon Enemies»
//  (в Часовне 5, в Дальних полях 2); призванный враг пул покидает и возвращается, когда погибнет. Рёв с призывом («Roar Pos») возможен,
//  если свободных в пуле не меньше «Enemy Remaining Max»: лимит N — «живых не больше N», то есть свободных должно быть пул−N+1
//  (в «Rematch Ready» босс сам ставит это число в 1 — меняем и там). Если N больше пула, пул дополняем копиями. Два способа призыва:
//  «Summon Type → Single / Double» (в Часовне при первом бое — из «Summon Cages») и «Summon Type R → Single R / Double R» (из пула).
//  Берём всегда пул: «Summon Cages» в «Single» / «Double» заменяем на «Summon Enemies». В ванили призыв — «один» («Single») или «все свободные
//  сразу» («Double»); при лимите (0.26.6) «Double» уводим в «Single», а в «Single» / «Single R» ставим своё действие BeastCall: один или два
//  врага (но не больше свободных мест под лимит и свободных в пуле); с 0.26.7 вместо «от 1 до всех».
//
//  Отец раков (объект «Crawfather», FSM «Control»). Четыре ворона в пуле («Pin Wielder» 1, «Dagger» 1, «Tinies» 2; детей этих папок ищем
//  по состоянию «Summon Ready»), «Summons Active» — сколько их живо. В ванили «Call?» зовёт, только когда живых нет, и «Call Type» шлёт
//  «SUMMON» папке выбранного типа (счётчик +1 / +1 / +2). Лимит N: в «Call?» сравниваем «Summons Active» с N−1 вместо нуля (звать можно,
//  пока живых меньше N), в «Call Type» вместо трёх состояний одно действие: тип выбирается случайно, количество как в ванили (1 / 1 / 2),
//  но не больше N минус живые; зовёт свободных воронов этого типа, не хватает — любых свободных. Воронов не хватает на N = 5 — добавляем
//  копию (в первом «Call?»). Перед проверкой «Summons Active» пересчитывается по воронам вне «Summon Ready» — чтобы копии не сбивали счёт.
//
//  Шансы (0.26.0). Подменяем веса у случайного выбора самого FSM (SendRandomEventV2 / V3ActiveBool / V4: weights) и выключаем его ограничения
//  «не больше N подряд» и «не реже чем раз в N» (eventMax / missedMax = 99), иначе шанс был бы не тем, что задали.
//  Отец раков: шанс зова — веса CALL / FINISHED в «Call?» (в ванили 0,75 / 0,25);
//  проценты видов — при лимите в «Call Type» одно наше действие CrowCall (тип по процентам, 1 / 1 / 2 ворона за вызов). Матерь Мхов: веса ROCK / CRAWLER в «Drop Type» и «Drop Type 2» (в ванили поровну и строго
//  по очереди: eventMax = 1 у обоих; а ещё в «Drop Type» стоит проверка «здоровья больше HP Half → всегда ROCK», её при заданных шансах выключаем).
//  У Отца раков без лимита в «Call Type» меняем только веса PIN WIELDER / DAGGER / TINIES. Зверомуха: шанс рёва (призыва) — вес ROAR среди CHARGE / STOMP / ROAR в «Choice», остальные
//  сохраняют своё соотношение. Выше 95 % не даём: если рёв отказывается («Roar Pos» → CANCEL), «Choice» выбирает снова сразу, без паузы.
// ============================================================================
using System.Collections.Generic;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;

namespace Encore
{
    internal static class Summons
    {
        const float FindEvery = 0.2f, SlowFindEvery = 1.5f, WindowSec = 300f;
        const int BeastPerCall = 2;   // Зверомуха при лимите зовёт за рёв одного или двух (0.26.7)
        const int FlyPool = 4;   // мух-вылупленцев в пуле (минимум; в игре берём число детей «Spawner Flies», если оно больше)
        const string BroodObject = "Slab Fly Broodmother", MossPrefix = "Mossbone Mother", ControlFsm = "Control";
        const string BeastObject = "Bone Flyer Giant", PoolObject = "Summon Enemies", CagesObject = "Summon Cages", CrowObject = "Crawfather";

        static bool armed;
        static string boss, scene;
        static int[] limits;
        static int[] chance;
        static readonly int[] NoLimits = { SummonData.Vanilla, SummonData.Vanilla };
        static int found, expected;
        static float nextFind, until;
        static readonly HashSet<int> patched = new HashSet<int>();

        /// Комната боя загрузилась: если у босса стоит лимит или шанс призыва (снимок взят при старте попытки), начинаем искать его FSM.
        internal static void Arm(string bossName, string sceneName, int[] limit, int[] chanceValues)
        {
            Disarm();
            bool lim = SummonData.Any(limit) && SummonData.Count(bossName) > 0;
            bool ch = SummonData.AnyChance(chanceValues) && SummonData.HasChance(bossName);
            if (!lim && !ch) return;
            armed = true;
            boss = bossName;
            scene = sceneName;
            limits = lim ? limit : NoLimits;
            chance = ch ? chanceValues : null;
            if (chance == null)
            {
                chance = new int[SummonData.ChanceSlots];
                for (int i = 0; i < chance.Length; i++) chance[i] = SummonData.Vanilla;
            }
            found = 0;
            expected = bossName == "Broodmother" ? 1 : (bossName == Tokens.MossAsc ? 2 : 1);
            nextFind = 0f;
            until = Time.unscaledTime + WindowSec;
            Runner.MarkPractice();
            string what = (lim ? "лимиты призывов — " + SummonData.Describe(bossName, limit) : "") + (lim && ch ? "; " : "")
                + (ch ? "шансы призывов — " + SummonData.DescribeChance(bossName, chanceValues) : "");
            Plugin.Log.LogInfo("Босс " + bossName + ": " + what + " (бой не идёт в статистику).");
        }

        internal static void Disarm()
        {
            armed = false;
            boss = null;
            scene = null;
            limits = null;
            chance = null;
            found = 0;
            patched.Clear();
        }

        /// Комната другая — искать больше нечего.
        internal static void LeaveScene(string newScene)
        {
            if (armed && !Rooms.Same(newScene, scene)) Disarm();
        }

        /// Раз в кадр. Без лимита — одна проверка на false.
        internal static void Tick()
        {
            if (!armed) return;
            if (!Runner.Active) { Disarm(); return; }
            float now = Time.unscaledTime;
            if (now < nextFind) return;
            // найдены все ожидаемые FSM — дальше смотрим редко (в двойном бою вторая Матерь может прийти позже)
            nextFind = now + (found >= expected ? SlowFindEvery : FindEvery);
            if (now > until)
            {
                if (found == 0) Plugin.Log.LogWarning("Босс " + boss + ": лимит или шанс призыва не применён — нужный FSM не нашёлся.");
                Disarm();
                return;
            }
            try
            {
                PlayMakerFSM[] all = Object.FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (PlayMakerFSM f in all)
                {
                    if (f == null || f.Fsm == null || f.FsmStates == null || f.FsmName != ControlFsm) continue;
                    if (!Rooms.Same(f.gameObject.scene.name, scene)) continue;
                    int id = f.GetInstanceID();
                    if (patched.Contains(id)) continue;
                    bool ok = false;
                    if (boss == "Broodmother") ok = f.gameObject.name == BroodObject && PatchBrood(f);
                    else if (IsBeast(boss)) ok = f.gameObject.name == BeastObject && PatchBeast(f);
                    else if (boss == "Crawfather") ok = f.gameObject.name == CrowObject && PatchCrow(f);
                    else if (f.gameObject.name.StartsWith(MossPrefix)) ok = PatchMoss(f);
                    if (!ok) continue;
                    patched.Add(id);
                    found++;
                }
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogWarning("Босс " + boss + ": лимит или шанс призыва не применён: " + ex.Message);
                Disarm();
            }
        }

        // ---- Broodmother ------------------------------------------------------------

        static bool PatchBrood(PlayMakerFSM f)
        {
            int cap = limits[0];
            if (cap == SummonData.Vanilla) return true;
            if (f.Fsm.GetState("Hatch Antic") == null) return false;
            int pool = BroodPool(f);
            bool noDouble = cap < pool;
            int tests = 0, doubles = 0;
            foreach (FsmState st in f.FsmStates)
            {
                if (st == null || st.Actions == null) continue;
                foreach (FsmStateAction a in st.Actions)
                {
                    IntTestToBool t = a as IntTestToBool;
                    if (t != null && t.int1 != null && t.int2 != null && t.int1.Name == "Free Flies")
                    {
                        t.int2.Value = pool - cap;   // вылупление возможно, лишь когда свободных больше этого числа, то есть живых меньше cap
                        tests++;
                    }
                    RandomBool rb = a as RandomBool;
                    if (noDouble && rb != null && rb.storeResult != null && rb.storeResult.Name == "Double Hatch")
                    {
                        rb.Enabled = false;   // без двойного вылупления вторая муха не обойдёт лимит
                        doubles++;
                    }
                }
            }
            // журнал каждого вылупления: сколько мух свободно (из пула) в момент выбора
            FsmState hatch = f.Fsm.GetState("Hatch Antic");
            var log = new BroodLog();
            log.Pm = f;
            log.Pool = pool;
            log.Cap = cap;
            var list = new List<FsmStateAction>();
            list.Add(log);
            list.AddRange(hatch.Actions);
            hatch.Actions = list.ToArray();
            Plugin.Log.LogInfo("Босс " + boss + ": мух одновременно не больше " + SummonData.LimitText(cap) + " (пул " + pool + ", проверок «Free Flies»: " + tests
                + ", двойное вылупление выключено: " + doubles + ").");
            if (tests == 0) Plugin.Log.LogWarning("Босс " + boss + ": в FSM не нашлось проверки «Free Flies» — лимит мух не сработает.");
            return true;
        }

        /// Сколько мух в пуле Матери рода: дети «Spawner Flies» на момент правки (в начале боя все дома), но не меньше четырёх (по дампу).
        static int BroodPool(PlayMakerFSM f)
        {
            int n = 0;
            try
            {
                FsmGameObject v = f.FsmVariables.FindFsmGameObject("Spawner Flies");
                if (v != null && v.Value != null) n = v.Value.transform.childCount;
            }
            catch (System.Exception ex) { Plugin.Log.LogWarning("Матерь рода: пул мух не посчитан: " + ex.Message); }
            return Mathf.Max(n, FlyPool);
        }

        /// Начало «Hatch Antic»: пишет в журнал, что мать вылупляет муху (свободных мух в пуле на выборе, пул, лимит).
        sealed class BroodLog : FsmStateAction
        {
            internal PlayMakerFSM Pm;
            internal int Pool, Cap;

            public override void OnEnter()
            {
                try
                {
                    FsmInt free = Pm.FsmVariables.FindFsmInt("Free Flies");
                    int fr = free != null ? free.Value : -1;
                    Plugin.Log.LogInfo("Матерь рода вылупляет муху: свободных в пуле " + fr + " из " + Pool + " (живых " + (fr >= 0 ? Pool - fr : -1) + "), лимит " + SummonData.LimitText(Cap) + ".");
                }
                catch (System.Exception ex) { Plugin.Log.LogWarning("Матерь рода: журнал вылупления: " + ex.Message); }
                Finish();
            }
        }

        // ---- Зверомуха ---------------------------------------------------------------------

        static bool IsBeast(string name)
        {
            return name != null && name.StartsWith("Savage Beastfly");
        }

        static bool PatchBeast(PlayMakerFSM f)
        {
            int cap = limits[0];
            FsmState roar = f.Fsm.GetState("Roar Pos");
            if (roar == null || f.Fsm.GetState("Summon Type") == null) return false;   // не главный FSM босса

            // шанс рёва (призыва): вес ROAR среди атак в «Choice»
            if (chance[0] != SummonData.Vanilla)
            {
                int changed = SetShare(f, "Choice", "ROAR", chance[0]);
                Plugin.Log.LogInfo("Босс " + boss + ": шанс рёва " + chance[0] + "% (выборов атаки изменено: " + changed + ").");
                if (changed == 0) Plugin.Log.LogWarning("Босс " + boss + ": в «Choice» не нашёлся выбор с событием ROAR — шанс рёва может не сработать.");
            }
            if (cap == SummonData.Vanilla) return true;

            GameObject pool = BeastPool(f, roar);
            if (pool == null)
            {
                Plugin.Log.LogWarning("Босс " + boss + ": пул «" + PoolObject + "» не найден — лимит врагов не применён.");
                return true;
            }

            // враги в пуле; не хватает до лимита — копии (по кругу из тех, что есть)
            int size = pool.transform.childCount, cloned = 0;
            if (size > 0 && cap > size)
            {
                var originals = new List<GameObject>();
                for (int i = 0; i < size; i++) originals.Add(pool.transform.GetChild(i).gameObject);
                for (int i = 0; i < cap - size; i++)
                {
                    try
                    {
                        GameObject src = originals[i % originals.Count];
                        GameObject copy = (GameObject)Object.Instantiate(src, pool.transform);
                        copy.name = src.name + " (extra " + (i + 1) + ")";
                        cloned++;
                    }
                    catch (System.Exception ex)
                    {
                        Plugin.Log.LogWarning("Босс " + boss + ": копия врага не создалась: " + ex.Message);
                    }
                }
                size = pool.transform.childCount;
            }

            // рёв возможен, если свободных не меньше X: живых < N ⇔ свободных ≥ пул − N + 1; при N = 0 — недостижимо
            int need = cap == 0 ? size + 1 : size - cap + 1;
            if (need < 1) need = 1;
            int sets = 0;
            FsmInt max = f.FsmVariables.FindFsmInt("Enemy Remaining Max");
            if (max != null) { max.Value = need; sets++; }
            foreach (FsmState st in f.FsmStates)
            {
                if (st == null || st.Actions == null) continue;
                foreach (FsmStateAction a in st.Actions)
                {
                    SetIntValue si = a as SetIntValue;
                    if (si != null && si.intVariable != null && si.intVariable.Name == "Enemy Remaining Max" && si.intValue != null)
                    {
                        si.intValue.Value = need;   // «Rematch Ready» ставит единицу — не даём сбить
                        sets++;
                    }
                }
            }

            // в Часовне при первом бое враги шли из «Summon Cages» — берём пул
            int moved = RedirectCages(f, "Single", pool) + RedirectCages(f, "Double", pool);

            // «Single» и «Double» ванили — «один» или «все свободные сразу». При лимите оба заменяем одним действием: один или два врага
            // случайно, но не больше свободных мест под лимит (0.26.7), враги берутся из пула. «Double» уводим в «Single», чтобы второго пути не было.
            int forced = 0;
            if (cap > 0)
            {
                forced += Reroute(f, "Summon Type", "DOUBLE", "Single");
                forced += Reroute(f, "Summon Type R", "DOUBLE", "Single R");
                forced += SetBeastCall(f, "Single", pool, size, cap);
                forced += SetBeastCall(f, "Single R", pool, size, cap);
            }

            Plugin.Log.LogInfo("Босс " + boss + ": врагов одновременно не больше " + SummonData.LimitText(cap) + " (пул " + size + (cloned > 0 ? ", копий " + cloned : "")
                + ", «Enemy Remaining Max» = " + need + " (" + sets + "), «Summon Cages» заменено: " + moved + ", призыв «один или два» поставлен: " + forced + ").");
            if (max == null) Plugin.Log.LogWarning("Босс " + boss + ": нет переменной «Enemy Remaining Max» — лимит врагов может не сработать.");
            return true;
        }

        /// Состояние «Single»/«Single R» при лимите: вместо «один случайный» или «все» — своё действие (BeastCall).
        static int SetBeastCall(PlayMakerFSM f, string state, GameObject pool, int size, int cap)
        {
            FsmState st = f.Fsm.GetState(state);
            if (st == null) return 0;
            var act = new BeastCall();
            act.Pool = pool;
            act.Size = size;
            act.Cap = cap;
            st.Actions = new FsmStateAction[] { act };
            return 1;
        }

        /// Призыв Зверомухи при лимите: один или два врага случайно (но не больше свободных мест под лимит и свободных в пуле).
        sealed class BeastCall : FsmStateAction
        {
            internal GameObject Pool;
            internal int Size, Cap;

            public override void OnEnter()
            {
                try { Run(); }
                catch (System.Exception ex) { Plugin.Log.LogWarning("Зверомуха: призыв врагов: " + ex.Message); }
                Finish();
            }

            void Run()
            {
                if (Pool == null) return;
                int free = Pool.transform.childCount;
                int alive = Size - free;
                int room = Cap - alive;
                int max = Mathf.Min(Mathf.Min(free, room), BeastPerCall);   // за рёв — один или два
                if (max <= 0) return;
                int want = Random.Range(1, max + 1);
                var kids = new List<GameObject>();
                for (int i = 0; i < free; i++) kids.Add(Pool.transform.GetChild(i).gameObject);
                var names = new List<string>();
                while (names.Count < want && kids.Count > 0)
                {
                    int k = Random.Range(0, kids.Count);
                    GameObject go = kids[k];
                    kids.RemoveAt(k);
                    foreach (PlayMakerFSM fsm in go.GetComponents<PlayMakerFSM>()) fsm.SendEvent("SUMMON");
                    names.Add(go.name);
                }
                Plugin.Log.LogInfo("Зверомуха: призвано " + names.Count + " из " + max + " возможных (свободных в пуле " + free + ", живых " + alive + ", лимит " + SummonData.LimitText(Cap) + ") [" + string.Join(", ", names.ToArray()) + "].");
            }
        }

        /// Пул врагов: объект из GetChildCount в «Roar Pos» (его считает сам босс); запасной путь — поиск по имени в той же комнате.
        static GameObject BeastPool(PlayMakerFSM f, FsmState roar)
        {
            foreach (FsmStateAction a in roar.Actions)
            {
                GetChildCount gc = a as GetChildCount;
                if (gc == null || gc.gameObject == null || gc.gameObject.GameObject == null) continue;
                GameObject go = gc.gameObject.GameObject.Value;
                if (go != null) return go;
            }
            foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t != null && t.name == PoolObject && t.gameObject.scene == f.gameObject.scene) return t.gameObject;
            return null;
        }

        /// В состоянии state действия, нацеленные на «Summon Cages», нацеливаем на пул.
        static int RedirectCages(PlayMakerFSM f, string state, GameObject pool)
        {
            FsmState st = f.Fsm.GetState(state);
            if (st == null || st.Actions == null) return 0;
            int n = 0;
            foreach (FsmStateAction a in st.Actions)
            {
                FsmOwnerDefault target = null;
                GetRandomChild gr = a as GetRandomChild;
                if (gr != null) target = gr.gameObject;
                SendEventByName se = a as SendEventByName;
                if (se != null && se.eventTarget != null) target = se.eventTarget.gameObject;
                if (target == null || target.GameObject == null) continue;
                GameObject go = target.GameObject.Value;
                if (go == null || go.name != CagesObject) continue;
                target.GameObject.Value = pool;
                n++;
            }
            return n;
        }

        /// В состоянии state переход по сигналу ev ведёт в состояние to.
        static int Reroute(PlayMakerFSM f, string state, string ev, string to)
        {
            FsmState st = f.Fsm.GetState(state), dest = f.Fsm.GetState(to);
            if (st == null || dest == null || st.Transitions == null) return 0;
            int n = 0;
            foreach (FsmTransition t in st.Transitions)
            {
                if (t.EventName != ev) continue;
                t.ToState = dest.Name;
                t.ToFsmState = dest;
                n++;
            }
            return n;
        }

        // ---- шансы: подмена весов случайного выбора ------------------------------------

        /// Части случайного выбора из действия (V2, V3ActiveBool или V4); false — действие другого рода.
        static bool RandomParts(FsmStateAction a, out FsmEvent[] events, out FsmFloat[] weights, out FsmInt[] eventMax, out FsmInt[] missedMax)
        {
            events = null; weights = null; eventMax = null; missedMax = null;
            SendRandomEventV4 v4 = a as SendRandomEventV4;
            if (v4 != null) { events = v4.events; weights = v4.weights; eventMax = v4.eventMax; missedMax = v4.missedMax; return true; }
            SendRandomEventV3ActiveBool v3 = a as SendRandomEventV3ActiveBool;
            if (v3 != null) { events = v3.events; weights = v3.weights; eventMax = v3.eventMax; missedMax = v3.missedMax; return true; }
            SendRandomEventV2 v2 = a as SendRandomEventV2;
            if (v2 != null) { events = v2.events; weights = v2.weights; eventMax = v2.eventMax; return true; }
            return false;
        }

        /// Снимает ограничения «подряд не больше» и «не реже чем» (иначе игра подправляет выбор и заданный шанс был бы не тем).
        static void Unlock(FsmInt[] limits99)
        {
            if (limits99 == null) return;
            foreach (FsmInt x in limits99) if (x != null) x.Value = 99;
        }

        static string EventName(FsmEvent[] events, int i)
        {
            return events != null && i >= 0 && i < events.Length && events[i] != null ? events[i].Name : null;
        }

        /// В случайных выборах состояния state ставит событию ev вес pct, остальным — (100 − pct) в прежнем соотношении. Возвращает, сколько выборов изменено.
        static int SetShare(PlayMakerFSM f, string state, string ev, int pct)
        {
            FsmState st = f.Fsm.GetState(state);
            if (st == null || st.Actions == null) return 0;
            int n = 0;
            foreach (FsmStateAction a in st.Actions)
            {
                FsmEvent[] events; FsmFloat[] weights; FsmInt[] eventMax, missedMax;
                if (!RandomParts(a, out events, out weights, out eventMax, out missedMax) || events == null || weights == null) continue;
                int at = -1;
                for (int i = 0; i < events.Length; i++) if (EventName(events, i) == ev) at = i;
                if (at < 0 || at >= weights.Length) continue;
                float others = 0f;
                for (int i = 0; i < weights.Length; i++) if (i != at && weights[i] != null) others += weights[i].Value;
                int count = 0;
                for (int i = 0; i < weights.Length; i++) if (i != at && weights[i] != null) count++;
                for (int i = 0; i < weights.Length; i++)
                {
                    if (weights[i] == null) continue;
                    if (i == at) weights[i].Value = pct;
                    else weights[i].Value = others > 0f ? (100f - pct) * weights[i].Value / others : (count > 0 ? (100f - pct) / count : 0f);
                }
                Unlock(eventMax);
                Unlock(missedMax);
                n++;
            }
            return n;
        }

        /// В случайных выборах состояния state ставит событиям names[i] веса pct[i] (остальные события не трогаем). Возвращает, сколько выборов изменено.
        static int SetWeights(PlayMakerFSM f, string state, string[] names, int[] pct)
        {
            FsmState st = f.Fsm.GetState(state);
            if (st == null || st.Actions == null) return 0;
            int n = 0;
            foreach (FsmStateAction a in st.Actions)
            {
                FsmEvent[] events; FsmFloat[] weights; FsmInt[] eventMax, missedMax;
                if (!RandomParts(a, out events, out weights, out eventMax, out missedMax) || events == null || weights == null) continue;
                int hits = 0;
                for (int i = 0; i < events.Length && i < weights.Length; i++)
                {
                    string name = EventName(events, i);
                    for (int k = 0; k < names.Length; k++)
                        if (name == names[k] && weights[i] != null) { weights[i].Value = pct[k]; hits++; }
                }
                if (hits == 0) continue;
                Unlock(eventMax);
                Unlock(missedMax);
                n++;
            }
            return n;
        }

        // ---- Отец раков ----------------------------------------------------------------

        static readonly string[] CrowKinds = { "Pin Wielder", "Dagger", "Tinies" };
        static readonly string[] CrowCloneOrder = { "Dagger", "Pin Wielder", "Tinies" };
        static readonly int[] CrowPerCall = { 1, 1, 2 };
        const string CrowReady = "Summon Ready";
        const int CrowVanillaPool = 4;

        /// Первое действие «Call?»: один раз достраивает пул воронов копиями, затем пересчитывает «Summons Active».
        /// Копии делаются из допустимых видов (с процентом > 0): сколько воронов нужно, чтобы жило N сразу (при лимите) и чтобы единственный вид мог прийти по два.
        sealed class CrowPrepare : FsmStateAction
        {
            internal PlayMakerFSM Pm;
            internal int Cap;      // Vanilla — лимита нет
            internal int[] Pct;    // проценты видов или null
            internal bool Pair;    // единственный вид зовётся по два — воронов этого вида в пуле нужно не меньше двух
            bool built;
            int tries;
            readonly List<GameObject> clones = new List<GameObject>();

            public override void OnEnter()
            {
                try { Run(); }
                catch (System.Exception ex) { Plugin.Log.LogWarning("Отец раков: подготовка воронов: " + ex.Message); }
                Finish();
            }

            bool Allowed(int kind)
            {
                return Pct == null || Pct[kind] > 0;
            }

            void Run()
            {
                if (!built)
                {
                    if (++tries > 20) built = true;                    // не вышло за 20 заходов — живём с четырьмя
                    else if (Jurors(Pm, null).Count >= CrowVanillaPool)   // Init уже заполнил папки
                    {
                        built = true;
                        // сколько воронов допустимых видов уже есть и сколько нужно
                        int haveAllowed = 0;
                        for (int k = 0; k < CrowKinds.Length; k++) if (Allowed(k)) haveAllowed += Jurors(Pm, CrowKinds[k]).Count;
                        int need = Cap != SummonData.Vanilla ? Cap : 0;
                        if (Pair && need < 2) need = 2;
                        int want = need - haveAllowed;
                        var kinds = new List<string>();
                        foreach (string kind in CrowCloneOrder)
                            if (Allowed(System.Array.IndexOf(CrowKinds, kind))) kinds.Add(kind);
                        for (int i = 0; i < want && kinds.Count > 0; i++)
                        {
                            string kind = kinds[i % kinds.Count];
                            try
                            {
                                List<PlayMakerFSM> src = Jurors(Pm, kind);
                                if (src.Count == 0) continue;
                                GameObject go = src[0].gameObject;
                                GameObject copy = (GameObject)Object.Instantiate(go, go.transform.parent);
                                copy.name = go.name + " (extra " + (i + 1) + ")";
                                clones.Add(copy);
                            }
                            catch (System.Exception ex)
                            {
                                Plugin.Log.LogWarning("Отец раков: копия ворона (" + kind + ") не создалась: " + ex.Message);
                            }
                        }
                        Plugin.Log.LogInfo("Отец раков: воронов в пуле " + Jurors(Pm, null).Count + " (допустимых видов было " + haveAllowed + "), копий добавлено " + clones.Count
                            + (Cap != SummonData.Vanilla ? ", живых одновременно не больше " + SummonData.LimitText(Cap) : "") + (Pair ? ", единственный вид зовётся по два" : "") + ".");
                    }
                }
                // «Summons Active» = вороны вне «Summon Ready»: счёт не расходится со смертями, даже если копии учли себя не так, как оригиналы
                FsmInt active = Pm.FsmVariables.FindFsmInt("Summons Active");
                List<PlayMakerFSM> all = Jurors(Pm, null);
                if (active != null && all.Count >= CrowVanillaPool)
                {
                    int busy = 0;
                    foreach (PlayMakerFSM j in all) if (j.Fsm.ActiveStateName != CrowReady) busy++;
                    active.Value = busy;
                }
            }
        }

        /// «Call Type»: зовёт воронов — тип по заданным процентам (иначе поровну), число как в ванили (1 / 1 / 2), при лимите — не больше N минус живые.
        sealed class CrowCall : FsmStateAction
        {
            internal PlayMakerFSM Pm;
            internal int Cap;      // Vanilla — лимита нет (считаем «Summons Active» как ваниль)
            internal int[] Pct;    // проценты Булавочник / Кинжальщик / Малыш или null
            internal bool Pair;    // единственный вид зовётся по два (вместо одного)

            public override void OnEnter()
            {
                try { Run(); }
                catch (System.Exception ex) { Plugin.Log.LogWarning("Отец раков: призыв воронов: " + ex.Message); }
                Finish();
            }

            bool Allowed(int kind)
            {
                return Pct == null || Pct[kind] > 0;
            }

            int PickKind()
            {
                if (Pct == null) return Random.Range(0, CrowKinds.Length);
                int total = 0;
                for (int i = 0; i < CrowKinds.Length; i++) total += Pct[i];
                if (total <= 0) return Random.Range(0, CrowKinds.Length);
                int r = Random.Range(0, total);
                for (int i = 0; i < CrowKinds.Length; i++)
                {
                    if (r < Pct[i]) return i;
                    r -= Pct[i];
                }
                return CrowKinds.Length - 1;
            }

            void Run()
            {
                FsmInt active = Pm.FsmVariables.FindFsmInt("Summons Active");
                if (active == null) return;
                bool capped = Cap != SummonData.Vanilla;
                int room = capped ? Cap - active.Value : int.MaxValue;
                int kind = PickKind();
                int perCall = CrowPerCall[kind];
                if (Pair && perCall < 2) perCall = 2;
                int want = Mathf.Min(perCall, room);
                if (want <= 0) return;

                // сначала свободные вороны выбранного типа, затем (при лимите) любые свободные из допустимых типов
                var order = new List<PlayMakerFSM>();
                List<PlayMakerFSM> own = Ready(Pm, CrowKinds[kind]), rest = new List<PlayMakerFSM>();
                if (capped)
                    for (int k = 0; k < CrowKinds.Length; k++)
                    {
                        if (k == kind || !Allowed(k)) continue;
                        foreach (PlayMakerFSM j in Ready(Pm, CrowKinds[k])) rest.Add(j);
                    }
                Shuffle(own);
                Shuffle(rest);
                order.AddRange(own);
                order.AddRange(rest);

                var names = new List<string>();
                foreach (PlayMakerFSM j in order)
                {
                    if (names.Count >= want) break;
                    j.SendEvent("SUMMON");
                    names.Add(j.gameObject.name);
                }
                active.Value += names.Count;
                Plugin.Log.LogInfo("Отец раков: тип «" + CrowKinds[kind] + "», позвано " + names.Count + " из " + want + " [" + string.Join(", ", names.ToArray()) + "], живых "
                    + active.Value + (capped ? " из " + SummonData.LimitText(Cap) : "") + ".");
            }
        }

        static void Shuffle(List<PlayMakerFSM> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                PlayMakerFSM t = list[i]; list[i] = list[j]; list[j] = t;
            }
        }

        /// Вороны типа kind (null — всех типов): FSM под папкой типа, у которых есть состояние «Summon Ready».
        static List<PlayMakerFSM> Jurors(PlayMakerFSM owner, string kind)
        {
            var res = new List<PlayMakerFSM>();
            foreach (string k in CrowKinds)
            {
                if (kind != null && k != kind) continue;
                FsmGameObject v = owner.FsmVariables.FindFsmGameObject(k);
                if (v == null || v.Value == null) continue;
                foreach (PlayMakerFSM f in v.Value.GetComponentsInChildren<PlayMakerFSM>(true))
                {
                    if (f == null || f.Fsm == null || f.Fsm.GetState(CrowReady) == null) continue;
                    res.Add(f);
                }
            }
            return res;
        }

        /// Свободные вороны — те, что стоят в «Summon Ready».
        static List<PlayMakerFSM> Ready(PlayMakerFSM owner, string kind)
        {
            List<PlayMakerFSM> all = Jurors(owner, kind), res = new List<PlayMakerFSM>();
            foreach (PlayMakerFSM f in all) if (f.Fsm.ActiveStateName == CrowReady) res.Add(f);
            return res;
        }

        static bool PatchCrow(PlayMakerFSM f)
        {
            int cap = limits[0];
            FsmState call = f.Fsm.GetState("Call?"), type = f.Fsm.GetState("Call Type"), end = f.Fsm.GetState("Call End");
            if (call == null || type == null || end == null || call.Actions == null) return false;   // не главный FSM босса

            // шанс зова: вес CALL в «Call?» (остальное — «не звать»)
            if (chance[0] != SummonData.Vanilla)
            {
                int changed = SetShare(f, "Call?", "CALL", chance[0]);
                Plugin.Log.LogInfo("Босс " + boss + ": шанс зова " + chance[0] + "% (выборов изменено: " + changed + ").");
                if (changed == 0) Plugin.Log.LogWarning("Босс " + boss + ": в «Call?» не нашёлся выбор с событием CALL — шанс зова может не сработать.");
            }

            int[] pct = null;
            if (chance[1] != SummonData.Vanilla) pct = new[] { chance[1], chance[2], chance[3] };
            // остался один вид (100 %), которого в ванили зовут по одному (Булавочник, Кинжальщик), — зовём по два
            bool pair = false;
            if (pct != null)
            {
                int only = -1, nonZero = 0;
                for (int k = 0; k < pct.Length; k++) if (pct[k] > 0) { nonZero++; only = k; }
                pair = nonZero == 1 && CrowPerCall[only] < 2;
            }
            if (cap == SummonData.Vanilla && pct == null) return true;
            if (cap == SummonData.Vanilla && !pair)
            {
                // лимита нет — оставляем ванильные состояния «Pin Wielder» / «Dagger» / «Tinies» (они сами считают «Summons Active»), меняем только веса выбора в «Call Type»
                int changed = SetWeights(f, "Call Type", new[] { "PIN WIELDER", "DAGGER", "TINIES" }, pct);
                Plugin.Log.LogInfo("Босс " + boss + ": типы воронов — Булавочник " + pct[0] + "%, Кинжальщик " + pct[1] + "%, Малыш " + pct[2] + "% (выборов изменено: " + changed + ").");
                if (changed == 0) Plugin.Log.LogWarning("Босс " + boss + ": в «Call Type» не нашёлся выбор типа — проценты видов могут не сработать.");
                return true;
            }

            if (cap != SummonData.Vanilla)
            {
                // «Call?»: звать можно, пока живых меньше N (в ванили — пока их нет)
                int gates = 0;
                foreach (FsmStateAction a in call.Actions)
                {
                    IntCompare c = a as IntCompare;
                    if (c != null && c.integer1 != null && c.integer1.Name == "Summons Active" && c.integer2 != null)
                    {
                        c.integer2.Value = cap - 1;   // «живых больше N−1» — не звать; при N = 0 всегда не звать
                        gates++;
                    }
                }
                Plugin.Log.LogInfo("Босс " + boss + ": воронов одновременно не больше " + SummonData.LimitText(cap) + " (проверок «Summons Active»: " + gates + ").");
                if (gates == 0) Plugin.Log.LogWarning("Босс " + boss + ": в «Call?» не нашлось проверки «Summons Active» — лимит воронов может не сработать.");
            }
            // первым действием «Call?» готовим воронов (копии, пересчёт «Summons Active»)
            var prep = new CrowPrepare();
            prep.Pm = f;
            prep.Cap = cap;
            prep.Pct = pct;
            prep.Pair = pair;
            var list = new List<FsmStateAction>();
            list.Add(prep);
            list.AddRange(call.Actions);
            call.Actions = list.ToArray();

            // «Call Type»: одно наше действие вместо выбора из трёх состояний; дальше — «Call End»
            var callAct = new CrowCall();
            callAct.Pm = f;
            callAct.Cap = cap;
            callAct.Pct = pct;
            callAct.Pair = pair;
            type.Actions = new FsmStateAction[] { callAct };
            bool hasFinished = false;
            foreach (FsmTransition t in type.Transitions)
                if (t.EventName == "FINISHED") { t.ToState = end.Name; t.ToFsmState = end; hasFinished = true; }
            if (!hasFinished)
            {
                var tr = new FsmTransition();
                tr.FsmEvent = FsmEvent.GetFsmEvent("FINISHED");
                tr.ToState = end.Name;
                tr.ToFsmState = end;
                var ts = new List<FsmTransition>(type.Transitions);
                ts.Add(tr);
                type.Transitions = ts.ToArray();
            }
            if (pct != null)
                Plugin.Log.LogInfo("Босс " + boss + ": типы воронов — Булавочник " + pct[0] + "%, Кинжальщик " + pct[1] + "%, Малыш " + pct[2] + "%" + (pair ? "; единственный вид зовётся по два" : "") + ".");
            return true;
        }

        // ---- Матерь Мхов ---------------------------------------------------------------

        static bool PatchMoss(PlayMakerFSM f)
        {
            if (f.Fsm.GetState("Rock") == null || f.Fsm.GetState("Crawler") == null) return false;   // не её главный FSM
            int crawlers = limits[0], rocks = limits[1];
            int muted = 0;
            bool lim = SummonData.Any(limits);
            if (rocks == 0)
            {
                muted += Mute(f, "Rock", "FALL");
                muted += Mute(f, "Rock - DB", "FALL");
            }
            if (crawlers == 0 || crawlers == 1) muted += Mute(f, "2nd Crawler", "SPAWN");
            if (crawlers == 0)
            {
                muted += Mute(f, "Crawler", "SPAWN");
                muted += MuteBool(f, "Crawler", "Spawned");
            }
            if (lim)
            {
                Plugin.Log.LogInfo("Босс " + boss + " («" + f.gameObject.name + "»): " + SummonData.Describe(boss, limits) + " (действий выключено: " + muted + ").");
                if (muted == 0) Plugin.Log.LogWarning("Босс " + boss + ": у «" + f.gameObject.name + "» не нашлось нужных действий — лимит может не сработать.");
            }

            // шансы: что бросить — ROCK / CRAWLER в «Drop Type» и «Drop Type 2» (порядок в панели: ползуны, камни)
            if (chance[1] != SummonData.Vanilla)
            {
                var names = new[] { "CRAWLER", "ROCK" };
                var pct = new[] { chance[1], chance[2] };
                // в ванили пока у матери больше «HP Half» здоровья, в «Drop Type» всегда выпадает камень (случайный выбор — только во второй половине боя): снимаем это правило
                int forced = DisableCompare(f, "Drop Type", "HP Half");
                int changed = SetWeights(f, "Drop Type", names, pct) + SetWeights(f, "Drop Type 2", names, pct);
                if (forced > 0) Plugin.Log.LogInfo("Босс " + boss + ": правило «пока здоровья много — всегда камень» выключено (проверок: " + forced + ").");
                Plugin.Log.LogInfo("Босс " + boss + " («" + f.gameObject.name + "»): ползуны " + chance[1] + "%, камни " + chance[2] + "% (выборов изменено: " + changed + ").");
                if (changed == 0) Plugin.Log.LogWarning("Босс " + boss + ": у «" + f.gameObject.name + "» не нашлись выборы «Drop Type» — шансы могут не сработать.");
            }
            return true;
        }

        /// Выключает в состоянии state проверки IntCompare, где вторым числом стоит переменная variable. Возвращает, сколько выключено.
        static int DisableCompare(PlayMakerFSM f, string state, string variable)
        {
            FsmState st = f.Fsm.GetState(state);
            if (st == null || st.Actions == null) return 0;
            int n = 0;
            foreach (FsmStateAction a in st.Actions)
            {
                IntCompare c = a as IntCompare;
                if (c == null || !c.Enabled || c.integer2 == null || c.integer2.Name != variable) continue;
                c.Enabled = false;
                n++;
            }
            return n;
        }

        /// Выключает в состоянии state все SendEventByName с сигналом ev. Возвращает, сколько выключено.
        static int Mute(PlayMakerFSM f, string state, string ev)
        {
            FsmState st = f.Fsm.GetState(state);
            if (st == null || st.Actions == null) return 0;
            int n = 0;
            foreach (FsmStateAction a in st.Actions)
            {
                SendEventByName se = a as SendEventByName;
                if (se == null || !se.Enabled || se.sendEvent == null || se.sendEvent.Value != ev) continue;
                se.Enabled = false;
                n++;
            }
            return n;
        }

        /// Выключает в состоянии state SetBoolValue, которое ставит переменную variable в «истина».
        static int MuteBool(PlayMakerFSM f, string state, string variable)
        {
            FsmState st = f.Fsm.GetState(state);
            if (st == null || st.Actions == null) return 0;
            int n = 0;
            foreach (FsmStateAction a in st.Actions)
            {
                SetBoolValue sb = a as SetBoolValue;
                if (sb == null || !sb.Enabled || sb.boolVariable == null || sb.boolVariable.Name != variable) continue;
                if (sb.boolValue == null || !sb.boolValue.Value) continue;
                sb.Enabled = false;
                n++;
            }
            return n;
        }
    }
}
