// ============================================================================
//  Immortal.cs — бессмертие босса (0.22.0, Entry.Imm). Не проверено в игре.
//  Идея: смерть в игре — это здоровье (HealthManager.hp) не выше нуля. Пока бессмертие включено, босс не теряет последнюю единицу здоровья:
//    - удар (HealthManager.TakeDamage) помечаем нелетальным — игра сама не даёт ему убить: здоровье остаётся 1, бой идёт дальше;
//    - добавочный урон (ApplyExtraDamage: яд, электричество и прочее) режем так, чтобы осталась единица.
//  Так босс остаётся в своей фазе сколько угодно, и бой не кончается его смертью (кончить бой можно кнопкой «Закончить» или смертью Хорнет).
//  Кто босс: здоровье не меньше порога (ImmortalMath.BossFloor или своё здоровье босса, если оно меньше). Призывы слабее и умирают как обычно.
//  Решение о существе запоминается по его номеру (GetInstanceID); если здоровье у него выросло (GoP ставит его заново на смене фазы), решаем заново.
//  Боссы с отдельным запасом здоровья на фазу (PhaseLock.ImmortalCapable) не охвачены: там фаза сменяется «здоровье кончилось».
//  Бой с бессмертием — тренировка (Runner.MarkPractice), в статистику не идёт.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace Encore
{
    internal static class Immortal
    {
        sealed class Verdict
        {
            internal bool Target;
            internal int LastHp;
        }

        /// Сколько существ записываем в лог при решении (чтобы по логу тестера было видно, кого защитили).
        const int LogLimit = 24;

        static bool armed;
        static string scene;
        static int floor = ImmortalMath.BossFloor;
        static int[] summonHp;
        static int logged;
        static bool failed;
        static readonly Dictionary<int, Verdict> verdicts = new Dictionary<int, Verdict>();
        static MethodInfo scaling;
        static bool scalingLooked;

        internal static bool Armed { get { return armed; } }

        /// Комната боя загрузилась: включаем бессмертие босса, если оно задано (снимок взят при старте попытки, см. Runner).
        internal static void Arm(string bossName, string sceneName, bool on, int customHp, int[] summons)
        {
            Disarm();
            if (!on || failed || !PhaseLock.ImmortalCapable(bossName)) return;
            armed = true;
            scene = sceneName;
            floor = ImmortalMath.Floor(customHp);
            summonHp = summons;
            Plugin.Log.LogInfo("Бессмертие: " + bossName + " (порог босса " + floor + " здоровья)");
            Runner.MarkPractice(); // тренировка не идёт в статистику
        }

        /// Комната другая — бессмертие снимаем (та же комната и её дополнительные сцены — нет).
        internal static void LeaveScene(string newScene)
        {
            if (armed && !Rooms.Same(newScene, scene)) Disarm();
        }

        internal static void Disarm()
        {
            armed = false;
            scene = null;
            floor = ImmortalMath.BossFloor;
            summonHp = null;
            logged = 0;
            verdicts.Clear();
        }

        /// Это существо надо беречь от смерти? Зовётся при каждом ударе, поэтому решение запоминается.
        internal static bool Guards(HealthManager hm)
        {
            if (!armed || hm == null) return false;
            int hp = hm.hp;
            if (hp <= 0) return false; // уже мертво: не мешаем (например, скриптовая смерть)
            int id = hm.GetInstanceID();
            Verdict v;
            if (!verdicts.TryGetValue(id, out v) || hp > v.LastHp)
            {
                v = new Verdict();
                v.Target = ImmortalMath.IsBoss(hp, floor, summonHp);
                verdicts[id] = v;
                if (logged < LogLimit)
                {
                    logged++;
                    Plugin.Log.LogInfo("Бессмертие: «" + hm.gameObject.name + "» здоровье " + hp + " — " + (v.Target ? "босс, защищаю" : "не босс, не трогаю"));
                }
            }
            v.LastHp = hp;
            return v.Target;
        }

        /// Ошибка внутри перехвата: пишем один раз и больше не мешаем игре (бессмертие просто перестаёт работать).
        internal static void Fail(Exception ex)
        {
            if (failed) return;
            failed = true;
            armed = false;
            Plugin.Log.LogError("Бессмертие остановлено: " + ex);
        }

        /// Игровая надбавка урона (HealthManager.ApplyDamageScaling), нужна, чтобы посчитать добавочный урон так же, как игра.
        internal static HitInstance Scaled(HealthManager hm, HitInstance hit)
        {
            if (!scalingLooked)
            {
                scalingLooked = true;
                try
                {
                    scaling = typeof(HealthManager).GetMethod("ApplyDamageScaling", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                                                              null, new Type[] { typeof(HitInstance) }, null);
                }
                catch (Exception) { scaling = null; }
            }
            if (scaling == null) return hit;
            try { return (HitInstance)scaling.Invoke(hm, new object[] { hit }); }
            catch (Exception) { return hit; }
        }
    }

    /// Перехваты игры для бессмертия. Включаются отдельно (Mods.Patch), чтобы сбой тут не ронял остальной Encore.
    internal static class ImmortalPatches
    {
        /// Удар: нелетальный — игра сама оставляет у существа 1 здоровья.
        [HarmonyPrefix]
        [HarmonyPatch(typeof(HealthManager), "TakeDamage")]
        static void BeforeTakeDamage(HealthManager __instance, ref HitInstance hitInstance)
        {
            try { if (Immortal.Guards(__instance)) hitInstance.NonLethal = true; }
            catch (Exception ex) { Immortal.Fail(ex); }
        }

        /// Добавочный урон числом: режем так, чтобы осталась единица здоровья.
        [HarmonyPrefix]
        [HarmonyPatch(typeof(HealthManager), "ApplyExtraDamage", new Type[] { typeof(int) })]
        static void BeforeExtraInt(HealthManager __instance, ref int damageAmount)
        {
            try { if (Immortal.Guards(__instance)) damageAmount = ImmortalMath.Allowed(__instance.hp, damageAmount); }
            catch (Exception ex) { Immortal.Fail(ex); }
        }

        /// Добавочный урон ударом: считаем сами (игра после надбавки сразу убивает) и не даём здоровью упасть ниже 1.
        [HarmonyPrefix]
        [HarmonyPatch(typeof(HealthManager), "ApplyExtraDamage", new Type[] { typeof(HitInstance) })]
        static bool BeforeExtraHit(HealthManager __instance, HitInstance hitInstance)
        {
            try
            {
                if (!Immortal.Guards(__instance)) return true;
                HitInstance scaled = Immortal.Scaled(__instance, hitInstance);
                __instance.hp -= ImmortalMath.Allowed(__instance.hp, scaled.DamageDealt);
                return false;
            }
            catch (Exception ex) { Immortal.Fail(ex); return true; }
        }
    }
}
