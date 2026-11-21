// ============================================================================
//  GameSpeed.cs — скорость игры (0.24.0, настройка Mode/GameSpeed, 25–300%; 0.24.2 — перехват ScaleTime). Не проверено в игре.
//  Идея: игра сама держит Time.timeScale в TimeManager.UpdateTimeScale: перемножает все свои множители
//  (замедление, тряска камеры, паузы боя) и, если вышло больше 1, срезает до 1 (читов нет). Замедление работает само,
//  ускорение срезается. Поэтому после этого метода (Postfix) мы умножаем получившееся время на свой коэффициент.
//  Пауза (0) остаётся нулём; замедления и заморозки кадра игры сохраняются — просто всё идёт быстрее или медленнее.
//  Включено всегда, во всей игре (не только в сегментах Encore): так же, как обычные челленджи Зала Богов.
//  Любое изменение действует сразу (с 0.24.1; в 0.24.0 в сегменте оно ждало следующей попытки, и тестер решил, что скорость «не работает»).
//  Если число поменяли посреди попытки сегмента, остаток этой попытки в статистику не идёт (Runner.OnSpeedChanged).
//  Выше 100% — челлендж (бой идёт в статистику с меткой «speed N»), ниже 100% — тренировка (в статистику не идёт; см. Runner).
//  Игра в двух местах ставит Time.timeScale мимо TimeManager: TimeManager.Reset (выход в меню) и PlayMaker-действие ScaleTime
//  (проверено по коду игры; так делают скрипты сцен и боёв — пауза, возобновление, замедления). До 0.24.2 это сбрасывало нашу скорость на 1,
//  пока что-нибудь (например, урон по герою) не заставляло TimeManager пересчитать время. С 0.24.2 на ScaleTime.DoTimeScale стоит свой
//  Postfix: значение, которое поставило действие, тоже умножается на коэффициент (замедление боя остаётся замедлением, 1 становится нужной скоростью).
//  Страховка (Tick): для всего остального (Reset, чужие моды) каждые 0,1 с видно, что время не совпадает с тем, что записал наш перехват,
//  и игру просят пересчитать его ещё раз.
//  Все таймеры Encore считаются по Time.unscaledTime, поэтому скорость им не мешает.
//  Что менять: границы и шаг — SpeedMath (ModLogic.cs); значение — настройка Mode/GameSpeed или кнопки в окне.
// ============================================================================
using System;
using HarmonyLib;
using UnityEngine;

namespace Encore
{
    internal static class GameSpeed
    {
        /// Как часто (секунд) страховка проверяет, не сбито ли время.
        const float CheckEvery = 0.1f;
        /// Сколько раз писать в журнал о пересчёте по страховке (чтобы не засорять его, если мешает другой мод).
        const int LogLimit = 6;

        static int percent = SpeedMath.Normal;
        static float factor = 1f;
        static bool failed;
        static int calls;            // сколько раз сработал перехват TimeManager.UpdateTimeScale (для журнала)
        static int direct;           // сколько раз сработал перехват действия ScaleTime (для журнала)
        static float written;        // что записал наш перехват в Time.timeScale в последний раз (0 — ничего или пауза)
        static float nextCheck;
        static int reapplied;

        /// Скорость, с которой игра идёт сейчас (в процентах).
        internal static int Percent { get { return percent; } }

        /// Перехват сломался и скорость выключена: игра идёт как обычно (статистика тогда тоже считает 100%, см. Plugin.EffectiveSpeed).
        internal static bool Broken { get { return failed; } }

        /// Ставит скорость и сразу пересчитывает игровое время (не ждёт следующего изменения времени в игре).
        internal static void Set(int newPercent)
        {
            int p = SpeedMath.Clamp(newPercent);
            if (p == percent) return;
            percent = p;
            if (failed) { factor = 1f; return; } // перехват сломался: число запоминаем, время не трогаем
            factor = SpeedMath.Factor(p);
            int before = calls;
            Refresh();
            Plugin.Log.LogInfo("Скорость игры: " + p + "% (Time.timeScale сейчас " + Time.timeScale + ", перехват сработал " + (calls - before) + " раз, ScaleTime до этого " + direct + " раз)");
        }

        /// Каждый кадр из Plugin.Update: если кто-то поставил Time.timeScale мимо TimeManager, просим игру пересчитать его заново,
        /// и наш перехват снова применит скорость. Паузу (0) и обычную скорость не трогаем.
        internal static void Tick()
        {
            if (factor == 1f || failed) return;
            float now = Time.unscaledTime;
            if (now < nextCheck) return;
            nextCheck = now + CheckEvery;

            float scale = Time.timeScale;
            if (scale <= 0f || Mathf.Approximately(scale, written)) return;

            int before = calls;
            Refresh();
            reapplied++;
            if (reapplied <= LogLimit)
                Plugin.Log.LogInfo("Скорость игры: время было сбито (" + scale + " вместо " + written + "), пересчитано заново: теперь " + Time.timeScale + ", перехват сработал " + (calls - before) + " раз, ScaleTime всего " + direct + " раз.");
            if (calls == before)
                Fail(new InvalidOperationException("пересчёт времени не дошёл до перехвата (патч TimeManager.UpdateTimeScale не применился)"));
        }

        /// Заставляет игру заново посчитать Time.timeScale: наш Postfix применит коэффициент (или перестанет его применять).
        static void Refresh()
        {
            if (failed) return;
            try { TimeManager.TimeScale = TimeManager.TimeScale; }
            catch (Exception ex) { Fail(ex); }
        }

        /// Сбой в перехвате: пишем в лог один раз и больше время не трогаем (игра идёт как обычно).
        internal static void Fail(Exception ex)
        {
            if (failed) return;
            failed = true;
            factor = 1f;
            Plugin.Log.LogError("Скорость игры: перехват сломался, скорость выключена: " + ex);
            Plugin.Toast(L.T("Скорость игры сломалась и выключена (подробности в журнале)", "Game speed broke and is switched off (see the log)"), 8f);
            // Время уже умножено нашим коэффициентом: просим игру пересчитать его заново (без нас), иначе оно осталось бы ускоренным до следующего пересчёта.
            try { TimeManager.TimeScale = TimeManager.TimeScale; } catch (Exception) { }
        }

        /// Зовётся после каждого пересчёта времени в игре (TimeManager.UpdateTimeScale).
        internal static void Scale()
        {
            calls++;
            Apply();
        }

        /// Зовётся после действия PlayMaker ScaleTime: оно пишет Time.timeScale само, мимо TimeManager.
        internal static void ScaleByAction()
        {
            direct++;
            Apply();
        }

        /// Умножает то, что сейчас стоит в Time.timeScale, на коэффициент (пауза и 0 остаются нулём).
        static void Apply()
        {
            if (failed || factor == 1f) { written = 0f; return; }
            float now = Time.timeScale;
            if (now <= 0f) { written = 0f; return; } // пауза и заморозка кадра остаются паузой
            Time.timeScale = now * factor;
            written = Time.timeScale;
        }
    }

    /// Перехват пересчёта игрового времени. Ставится отдельным try (см. Mods.Patch): сбой тут не роняет Encore.
    internal static class GameSpeedPatches
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(TimeManager), "UpdateTimeScale")]
        static void AfterUpdateTimeScale()
        {
            try { GameSpeed.Scale(); }
            catch (Exception ex) { GameSpeed.Fail(ex); }
        }
    }

    /// Перехват PlayMaker-действия ScaleTime (оно ставит Time.timeScale мимо TimeManager). Отдельный класс и отдельный Mods.Patch:
    /// если метод не найдётся, остальная скорость (UpdateTimeScale + страховка Tick) всё равно работает.
    internal static class GameSpeedActionPatches
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(HutongGames.PlayMaker.Actions.ScaleTime), "DoTimeScale")]
        static void AfterScaleTime()
        {
            try { GameSpeed.ScaleByAction(); }
            catch (Exception ex) { GameSpeed.Fail(ex); }
        }
    }
}
