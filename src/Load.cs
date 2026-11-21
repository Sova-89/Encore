// ============================================================================
//  Load.cs — замеры: запуск мода по шагам и время, которое мод тратит за кадр.
//  Запуск (Lap, StartReport) пишется в лог всегда: сколько миллисекунд заняла
//  каждая часть Awake. Нагрузка за кадр работает только при
//  General/LogFrameLoad = true (по умолчанию выключен):
//  раз в 30 секунд пишет в лог BepInEx среднее и худшее время за кадр.
//  Считается только код самого мода; время, которое игра тратит на вызов
//  рисования, сюда не входит.
// ============================================================================
using System.Diagnostics;
using System.Text;
using UnityEngine;

namespace Encore
{
    internal static class Load
    {
        const float ReportEverySec = 30f;

        internal static bool On;

        static long frameTicks, sumTicks, worstTicks;
        static int frames, draws;
        static float windowStart = -1f;

        // ---- запуск мода по шагам ------------------------------------------------

        static long startTotal, startLast;
        static readonly StringBuilder laps = new StringBuilder();

        /// Начало замера запуска (первая строка Awake).
        internal static void StartBegin()
        {
            startTotal = Stopwatch.GetTimestamp();
            startLast = startTotal;
            laps.Length = 0;
        }

        /// Записывает, сколько прошло с прошлой отметки (или с начала запуска).
        internal static void Lap(string name)
        {
            long now = Stopwatch.GetTimestamp();
            if (laps.Length > 0) laps.Append(", ");
            laps.Append(name).Append(' ').Append(Ms(now - startLast));
            startLast = now;
        }

        /// Строка для лога: общее время запуска и по шагам.
        internal static string StartReport()
        {
            return "Запуск мода занял " + Ms(Stopwatch.GetTimestamp() - startTotal) + " мс (" + laps + ")";
        }

        /// Тики таймера -> миллисекунды с десятыми долями, для строк лога.
        internal static string Ms(long ticks)
        {
            return (ticks * 1000.0 / Stopwatch.Frequency).ToString("0.0");
        }

        // ---- нагрузка за кадр ----------------------------------------------------

        /// Начало замеряемого куска. Выключен замер — возвращает 0 и ничего не стоит.
        internal static long Begin()
        {
            return On ? Stopwatch.GetTimestamp() : 0L;
        }

        internal static void End(long started)
        {
            if (On && started != 0L) frameTicks += Stopwatch.GetTimestamp() - started;
        }

        /// Отмечает, что окно или плашки рисовались ещё раз.
        internal static void CountDraw()
        {
            if (On) draws++;
        }

        /// Раз в кадр: закрывает прошлый кадр и, когда пора, пишет строку в лог.
        internal static void NextFrame()
        {
            if (!On) { windowStart = -1f; return; }
            if (windowStart < 0f) { Reset(); return; }

            sumTicks += frameTicks;
            if (frameTicks > worstTicks) worstTicks = frameTicks;
            frameTicks = 0L;
            frames++;

            float elapsed = Time.unscaledTime - windowStart;
            if (elapsed < ReportEverySec) return;

            double toMs = 1000.0 / Stopwatch.Frequency;
            string perFrame = (sumTicks * toMs / frames).ToString("0.0000");
            string worst = (worstTicks * toMs).ToString("0.000");
            Plugin.Log.LogInfo("Нагрузка за " + elapsed.ToString("0") + " с (" + (frames / elapsed).ToString("0") + " кадров/с): в среднем "
                + perFrame + " мс на кадр, худший кадр " + worst + " мс; окно и плашки рисовались " + draws + " раз");
            Reset();
        }

        static void Reset()
        {
            frameTicks = 0L;
            sumTicks = 0L;
            worstTicks = 0L;
            frames = 0;
            draws = 0;
            windowStart = Time.unscaledTime;
        }
    }
}
