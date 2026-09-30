using System;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Nubik
{
    public sealed class YandexBridge : MonoBehaviour
    {
        public static bool Paused { get; private set; }
        /// <summary>An ad is pending/on screen: game time and sound stay paused.</summary>
        public bool AdShowing { get; private set; }
        /// <summary>Called with the ticket once the platform confirms the reward.</summary>
        public event Action<int> AdRewarded;
        /// <summary>Called when the ad closes or fails; true when it could not be shown at all.</summary>
        public event Action<bool> AdFinished;
        private bool inMine = true;
        private bool focused = true;
        private bool visible = true;
        private bool platformPaused, upright;
        private bool platformMuted;
        public bool RewardedSupported => (PlatformCapabilities & 1) != 0;
        public bool MenuAdsAllowed => (PlatformCapabilities & 2) != 0;
        private int PlatformCapabilities
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                return NubikPlatformCapabilities();
#else
                return 3;
#endif
            }
        }
        public const float InterstitialInterval = 240f;
        public float ActivePlaySeconds { get; private set; }
        public bool InterstitialDue => ActivePlaySeconds >= InterstitialInterval;
        private bool rewardedAd;
        private int rewardTicket;
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void NubikReady();
        [DllImport("__Internal")] private static extern int NubikPlatformCapabilities();
        [DllImport("__Internal")] private static extern void NubikGameplay(int active);
        [DllImport("__Internal")] private static extern void NubikShowRewarded(int ticket);
        [DllImport("__Internal")] private static extern void NubikShowInterstitial();
#endif
        public void Ready()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            NubikReady();
#endif
            Apply();
        }

        /// <summary>Asks the platform for a rewarded video; the reward arrives through <see cref="AdRewarded"/>.</summary>
        public void ShowRewarded(int ticket)
        {
            if (AdShowing || Paused || !RewardedSupported) return;
            rewardedAd = true;
            rewardTicket = ticket;
            AdShowing = true;
            Apply();
#if UNITY_WEBGL && !UNITY_EDITOR
            NubikShowRewarded(ticket);
#else
            // The editor and desktop builds have no ad network: a short pause stands in for the video.
            StartCoroutine(FakeAd(ticket));
#endif
        }

        private IEnumerator FakeAd(int ticket)
        {
            yield return new WaitForSecondsRealtime(1f);
            if (ticket > 0) { OnAdRewarded(ticket.ToString()); OnAdClose(""); }
            else OnInterstitialClose("");
        }

        public void AdvancePlayTime(float seconds, bool active)
        {
            if (active && !Paused && !AdShowing && seconds > 0 && !float.IsInfinity(seconds))
                ActivePlaySeconds = Mathf.Min(InterstitialInterval, ActivePlaySeconds + seconds);
        }

        /// <summary>Call only at a natural break after four minutes of active play.</summary>
        public bool TryShowInterstitial()
        {
            if (!InterstitialDue || AdShowing || Paused) return false;
            rewardedAd = false;
            AdShowing = true;
            Apply();
#if UNITY_WEBGL && !UNITY_EDITOR
            NubikShowInterstitial();
#else
            StartCoroutine(FakeAd(0));
#endif
            return true;
        }

        // Messages from the page (Yandex.jslib / index.html).
        public void OnAdOpen(string _) { if (AdShowing && rewardedAd) Apply(); }
        public void OnAdRewarded(string ticket)
        { if (AdShowing && rewardedAd && int.TryParse(ticket, out int value) && value == rewardTicket) AdRewarded?.Invoke(value); }
        public void OnAdClose(string _) { if (rewardedAd) EndAd(false); }
        public void OnAdError(string _) { if (rewardedAd) EndAd(true); }
        public void OnInterstitialOpen(string _) { if (AdShowing && !rewardedAd) Apply(); }
        public void OnInterstitialClose(string _) { if (!rewardedAd) EndAd(false); }
        public void OnInterstitialError(string _) { if (!rewardedAd) EndAd(true); }

        private void EndAd(bool failed)
        {
            if (!AdShowing) return;
            AdShowing = false;
            // Rewarded views also start a fresh interval; failed requests cannot retry every frame.
            ActivePlaySeconds = 0;
            Apply();
            if (rewardedAd) AdFinished?.Invoke(failed);
        }

        public void SetInMine(bool value) { inMine = value; Apply(); }
        public void OnVisibility(string value) { visible = value == "1"; Apply(); }
        /// <summary>game_api_pause / game_api_resume from the Yandex SDK.</summary>
        public void OnPlatformPause(string value) { platformPaused = value == "1"; Apply(); }
        public void OnPlatformMute(string value) { platformMuted = value == "1"; Apply(); }
        /// <summary>The phone is held upright: the page shows "turn the phone" and the game waits.</summary>
        public void OnOrientation(string value) { upright = value == "1"; Apply(); }
        private void OnApplicationFocus(bool value) { focused = value; Apply(); }
        private void Apply()
        {
            Paused = !focused || !visible || AdShowing || platformPaused || upright;
            Time.timeScale = Paused ? 0 : 1;
            AudioListener.pause = Paused || platformMuted;
#if UNITY_WEBGL && !UNITY_EDITOR
            // Gameplay markup follows the game's own states; the platform marks its own pauses itself.
            NubikGameplay(inMine && focused && visible && !AdShowing && !upright ? 1 : 0);
#endif
        }
        private void OnDestroy() { Paused = false; Time.timeScale = 1; AudioListener.pause = false; }
    }
}
