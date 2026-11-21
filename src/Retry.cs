// ============================================================================
//  Retry.cs — режимы «без урона» и «рестарт при смерти»: после смерти сегмент начинается заново.
//  Зачем: на сложности Radiant один удар — это смерть. GoP в этот момент сам
//  возвращает Хорнет в зал, и сегмент не запускается снова. Здесь мы ждём,
//  пока Хорнет вернётся и сможет двигаться, и запускаем тот же пресет.
//  Что менять: паузы — Delay, Step и Timeout ниже.
// ============================================================================
using UnityEngine;
using GopSequence = Gods_Of_Pharloom.BossSequence;

namespace Encore
{
    internal static class Retry
    {
        /// Сколько секунд после загрузки зала подождать, чтобы всё встало на место.
        const float Delay = 1.5f;
        /// Как часто проверять, можно ли запускать (поиск точки возврата не бесплатный).
        const float Step = 0.5f;
        /// Сколько секунд ждать возвращения в зал; потом попытку отменяем.
        const float Timeout = 20f;

        static Preset preset;      // что запускать заново; null — ничего не ждём
        static int attempt;        // номер попытки, на которой Хорнет погибла
        static float checkAt;      // когда проверить в следующий раз
        static float giveUpAt;     // когда отказаться от ожидания
        static float sceneLoadedAt; // когда загрузилась последняя комната

        /// Хорнет погибла в режиме «без урона» или «рестарт при смерти»: запомнить пресет и ждать возвращения в зал.
        internal static void Schedule(Preset toRun, int failedAttempt)
        {
            preset = toRun;
            attempt = failedAttempt;
            checkAt = Time.unscaledTime + Delay;
            giveUpAt = Time.unscaledTime + Timeout;
        }

        /// Ждём ли мы сейчас возвращения в зал, чтобы начать сегмент заново.
        internal static bool Pending
        {
            get { return preset != null; }
        }

        /// Отменить ожидание (запущен сегмент вручную, нажато «Закончить» или режим выключен).
        internal static void Cancel()
        {
            preset = null;
        }

        /// Загрузилась комната: до следующей проверки пройдёт ещё Delay секунд.
        internal static void SceneLoaded()
        {
            sceneLoadedAt = Time.unscaledTime;
        }

        /// Раз в кадр, пока нашего сегмента нет.
        internal static void Tick()
        {
            if (preset == null) return;
            if (!Plugin.RestartsOnDeath) { preset = null; return; }

            float now = Time.unscaledTime;
            if (now > giveUpAt)
            {
                Plugin.Log.LogInfo("«" + preset.Name + "»: перезапуск после смерти отменён — Хорнет не вернулась в зал за "
                                   + Timeout + " с");
                preset = null;
                return;
            }
            if (now < checkAt || now < sceneLoadedAt + Delay) return;
            checkAt = now + Step;

            if (!HeroReady()) return;

            Preset toRun = preset;
            int failed = attempt;
            if (Runner.Launch(toRun) != null) return; // ещё не у точки возврата — попробуем позже

            Runner.SetAttempt(failed + 1);
            Plugin.Log.LogInfo("«" + toRun.Name + "»: смерть — сегмент заново, попытка " + (failed + 1));
            Plugin.Toast(L.T("Смерть — сегмент заново · попытка ", "Death — restarting · attempt ") + (failed + 1), 3f);
        }

        /// Хорнет жива, ею можно управлять, игра не на паузе и не грузит комнату, окно мода закрыто.
        static bool HeroReady()
        {
            if (Plugin.MenuOpen) return false; // пока окно открыто, игрок решает сам: «Закончить» или «Старт»
            HeroController hero = HeroController.SilentInstance;
            GameManager game = GameManager.SilentInstance;
            if (hero == null || game == null) return false;
            if (hero.cState.dead || !hero.acceptingInput || hero.controlReqlinquished) return false;
            if (game.IsInSceneTransition || game.IsGamePaused()) return false;
            return !GopSequence.isInSequence;
        }
    }
}
