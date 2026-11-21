// ============================================================================
//  Attacks.cs — выключение атак босса (0.29.0). Список атак и как они лежат в пресете — AttackData.cs. В игре проверено: атаки Bell Beast, Last Judge, Widow, Broodmother, Lost Lace.
//  0.29.1: FSM правим, только когда игра его уже запустила (Fsm.Started), а не раньше: в первом бою Bell Beast после запуска игры (2 раза из 2 с выключенными атаками,
//  без них — нормально) босс так и не вышел из «Dormant» и бой не стартовал. Плюс журнал: в каком состоянии был FSM при правке и когда вышел из него.
//  Босс не заменяется: правим случайный выбор в FSM босса, как только он появился в комнате (раз в 0,2 с ищем объект босса по имени, каждый
//  FSM правится один раз). Бой с выключенными атаками — тренировка, в статистику не идёт.
//  Что именно правим. В каждом действии случайного выбора («SendRandomEvent», V2, V3, V3ActiveBool, V4) во всех состояниях FSM босса: если среди
//  событий есть событие выключенной атаки — её вес становится 0, а «выбрать обязательно, если не выпадала N раз» (missedMax) уходит далеко за
//  предел (иначе игра всё равно выберет атаку принудительно, минуя веса). Если после этого в выборе остаётся одна запись с весом, ей снимаем
//  «не больше N подряд» (eventMax): иначе игра сто раз перебросит, упрётся в это ограничение и выберет самую первую запись из списка — возможно, выключенную.
//  Выбор, где после выключения не осталось бы ни одной записи с весом, не трогаем (так босс не повиснет). Побочный эффект: у «продолжений»
//  («Followup?» — продолжить ли серию той же атакой) вес выключенной атаки тоже 0, и серия не продолжается — это нужно.
// ============================================================================
using System.Collections.Generic;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;

namespace Encore
{
    internal static class Attacks
    {
        const float FindEvery = 0.2f, SlowFindEvery = 1.5f, WindowSec = 300f;

        static bool armed;
        static string boss, scene;
        static AttackSpec spec;
        static HashSet<string> off;
        static int mask;
        static int found, changed;
        static float nextFind, until;
        static readonly HashSet<int> patched = new HashSet<int>();
        // 0.29.1: FSM правим только когда игра его уже запустила; а после правки следим, вышел ли босс из своего первого состояния.
        const float WatchWarnSec = 10f;
        static int waiting;
        static Fsm watchFsm;
        static string watchState;
        static float watchSince;
        static bool watchWarned;

        /// Комната боя загрузилась: если у босса выключена хоть одна атака (снимок взят при старте попытки), начинаем искать его FSM.
        internal static void Arm(string bossName, string sceneName, int disabledMask)
        {
            Disarm();
            AttackSpec s = AttackData.Spec(bossName);
            int clean = AttackData.Clean(bossName, disabledMask);
            if (s == null || clean == 0) return;
            armed = true;
            boss = bossName;
            scene = sceneName;
            spec = s;
            mask = clean;
            off = AttackData.OffEvents(bossName, clean);
            found = 0;
            changed = 0;
            waiting = 0;
            watchFsm = null;
            watchState = null;
            watchWarned = false;
            nextFind = 0f;
            until = Time.unscaledTime + WindowSec;
            Runner.MarkPractice();
            Plugin.Log.LogInfo("Босс " + bossName + ": выключены атаки — " + AttackData.DescribeOff(bossName, clean) + " (бой не идёт в статистику).");
        }

        internal static void Disarm()
        {
            armed = false;
            boss = null;
            scene = null;
            spec = null;
            off = null;
            mask = 0;
            found = 0;
            changed = 0;
            waiting = 0;
            watchFsm = null;
            watchState = null;
            watchWarned = false;
            patched.Clear();
        }

        /// Комната другая — искать больше нечего.
        internal static void LeaveScene(string newScene)
        {
            if (armed && !Rooms.Same(newScene, scene)) Disarm();
        }

        /// Раз в кадр. Без выключенных атак — одна проверка на false.
        internal static void Tick()
        {
            if (!armed) return;
            if (!Runner.Active) { Disarm(); return; }
            float now = Time.unscaledTime;
            if (now < nextFind) return;
            nextFind = now + (found >= 1 ? SlowFindEvery : FindEvery);
            if (now > until)
            {
                if (found == 0 && waiting > 0) Plugin.Log.LogWarning("Босс " + boss + ": выключенные атаки не применены — FSM «" + spec.Fsm + "» объекта «" + spec.Object + "» нашёлся, но игра так и не запустила его.");
                else if (found == 0) Plugin.Log.LogWarning("Босс " + boss + ": выключенные атаки не применены — FSM «" + spec.Fsm + "» объекта «" + spec.Object + "» не нашёлся.");
                Disarm();
                return;
            }
            try
            {
                Watch(now);
                PlayMakerFSM[] all = Object.FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (PlayMakerFSM f in all)
                {
                    // Сначала дешёвые проверки по имени, и только у босса — остальное: в тяжёлой комнате тысячи чужих FSM трогать незачем.
                    if (f == null || f.gameObject.name != spec.Object) continue;
                    Fsm fsm = f.Fsm;
                    if (fsm == null || f.FsmName != spec.Fsm) continue;
                    if (!Rooms.Same(f.gameObject.scene.name, scene)) continue;
                    int id = f.GetInstanceID();
                    if (patched.Contains(id)) continue;
                    // Игра ещё не запустила этот FSM (объект выключен или комната грузится): его действия она ещё не подготовила,
                    // читать их сейчас значит заставить загрузиться раньше неё. Ждём следующего опроса (0.29.1: первый бой после запуска игры не стартовал).
                    if (!fsm.Started) { waiting++; continue; }
                    patched.Add(id);
                    found++;
                    string state = fsm.ActiveStateName;
                    int n = Patch(f);
                    changed += n;
                    Plugin.Log.LogInfo("Босс " + boss + ": выбор атаки изменён в " + n + " действиях FSM «" + f.FsmName + "» (FSM был в состоянии «" + state + "»).");
                    if (n == 0) Plugin.Log.LogWarning("Босс " + boss + ": в FSM нет случайного выбора с выключенными атаками — ничего не изменилось.");
                    if (watchFsm == null) { watchFsm = fsm; watchState = state; watchSince = now; watchWarned = false; }
                }
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogWarning("Босс " + boss + ": выключенные атаки не применены: " + ex.Message);
                Disarm();
            }
        }

        /// Следим за боссом после правки: когда он вышел из состояния, в котором стоял в момент правки, и не застрял ли (нужно для журнала, на бой не влияет).
        static void Watch(float now)
        {
            if (watchFsm == null) return;
            string cur = watchFsm.ActiveStateName;
            if (cur != watchState)
            {
                Plugin.Log.LogInfo("Босс " + boss + ": FSM вышел из состояния «" + watchState + "» в «" + cur + "» через " + (now - watchSince).ToString("0.0") + " с после правки.");
                watchFsm = null;
                return;
            }
            if (!watchWarned && now - watchSince > WatchWarnSec)
            {
                watchWarned = true;
                Plugin.Log.LogWarning("Босс " + boss + ": FSM уже " + WatchWarnSec.ToString("0") + " с стоит в состоянии «" + cur + "» после правки атак. Если бой не начался, пришли этот журнал.");
            }
        }

        // ---- правка выбора ------------------------------------------------------------

        /// Части случайного выбора из действия; false — действие другого рода. V2 и «SendRandomEvent» не знают «обязательно выбрать» (missedMax).
        static bool Parts(FsmStateAction a, out FsmEvent[] events, out FsmFloat[] weights, out FsmInt[] eventMax, out FsmInt[] missedMax)
        {
            events = null; weights = null; eventMax = null; missedMax = null;
            SendRandomEventV4 v4 = a as SendRandomEventV4;
            if (v4 != null) { events = v4.events; weights = v4.weights; eventMax = v4.eventMax; missedMax = v4.missedMax; return true; }
            SendRandomEventV3ActiveBool v3a = a as SendRandomEventV3ActiveBool;
            if (v3a != null) { events = v3a.events; weights = v3a.weights; eventMax = v3a.eventMax; missedMax = v3a.missedMax; return true; }
            SendRandomEventV3 v3 = a as SendRandomEventV3;
            if (v3 != null) { events = v3.events; weights = v3.weights; eventMax = v3.eventMax; missedMax = v3.missedMax; return true; }
            SendRandomEventV2 v2 = a as SendRandomEventV2;
            if (v2 != null) { events = v2.events; weights = v2.weights; eventMax = v2.eventMax; return true; }
            SendRandomEvent v1 = a as SendRandomEvent;
            if (v1 != null) { events = v1.events; weights = v1.weights; return true; }
            return false;
        }

        /// Правит все случайные выборы FSM. Возвращает, сколько действий изменено.
        static int Patch(PlayMakerFSM f)
        {
            int n = 0;
            foreach (FsmState st in f.FsmStates)
            {
                if (st == null || st.Actions == null) continue;
                foreach (FsmStateAction a in st.Actions)
                {
                    if (a == null) continue;
                    FsmEvent[] events; FsmFloat[] weights; FsmInt[] eventMax, missedMax;
                    if (!Parts(a, out events, out weights, out eventMax, out missedMax) || events == null || weights == null) continue;
                    if (Apply(events, weights, eventMax, missedMax)) n++;
                }
            }
            return n;
        }

        static bool Apply(FsmEvent[] events, FsmFloat[] weights, FsmInt[] eventMax, FsmInt[] missedMax)
        {
            int len = events.Length < weights.Length ? events.Length : weights.Length;
            var names = new string[len];
            var w = new float[len];
            for (int i = 0; i < len; i++)
            {
                names[i] = events[i] == null ? null : events[i].Name;
                w[i] = weights[i] == null ? 0f : weights[i].Value;
            }
            ChoicePlan plan = AttackMath.Plan(names, w, off);
            if (plan == null) return false;
            for (int i = 0; i < plan.Off.Length; i++)
            {
                if (!plan.Off[i]) continue;
                if (weights[i] != null) weights[i].Value = 0f;
                if (missedMax != null && i < missedMax.Length && missedMax[i] != null) missedMax[i].Value = AttackMath.Never;
            }
            if (plan.Keep >= 0 && eventMax != null && plan.Keep < eventMax.Length && eventMax[plan.Keep] != null)
                eventMax[plan.Keep].Value = AttackMath.Unlimited;
            return true;
        }
    }
}
