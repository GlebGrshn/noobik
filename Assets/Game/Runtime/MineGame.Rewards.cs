using System;
using UnityEngine;

namespace Nubik
{
    /// <summary>
    /// Voluntary rewarded ads. The reward is paid only when the platform confirms it, once per ticket,
    /// and saved at once; closing the ad early, an error or a missing ad network pays nothing and costs nothing.
    /// </summary>
    public sealed partial class MineGame
    {
        private int adPaid;
        private bool adHooked;

        public static long Now => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        public bool AdBusy => platform != null && platform.AdShowing;
        public bool AdAvailable => !AdBusy && Progress.AdReady(Now);
        public int AdWait => (int)Math.Max(0, Progress.adReadyAt - Now);
        public int AdCoins => Progress.AdReward(config);

        public void WatchAd()
        {
            if (!AdAvailable) return;
            if (!adHooked)
            {
                adHooked = true;
                platform.AdRewarded += PayForAd;
                platform.AdFinished += AdClosed;
            }
            adPaid = 0;
            hud.ClearInput();
            platform.ShowRewarded(Progress.adRewards + 1);
        }

        private void PayForAd(int ticket)
        {
            int paid = Progress.GrantAd(ticket, Now, config);
            if (paid <= 0) return;
            adPaid += paid;
            SaveNow();
        }

        private void AdClosed(bool failed)
        {
            if (adPaid > 0)
            {
                sound.Play("sell", 1, 1.05f, 0);
                hud.Notify("Спасибо за просмотр: +" + adPaid + " монет");
                adPaid = 0;
            }
            else hud.Notify(failed ? "Реклама сейчас недоступна · попробуй позже" : "Реклама закрыта раньше времени · монеты не начислены");
        }
    }
}
