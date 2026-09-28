using System;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Nubik
{
    public sealed class YandexBridge : MonoBehaviour
    {
        public static bool Paused { get; private set; }
        /// <summary>A rewarded ad is on screen: the game, its timers and its sound stay paused.</summary>
        public bool AdShowing { get; private set; }
        /// <summary>Called with the ticket once the platform confirms the reward.</summary>
        public event Action<int> AdRewarded;
        /// <summary>Called when the ad closes or fails; true when it could not be shown at all.</summary>
        public event Action<bool> AdFinished;
        private bool inMine = true;
        private bool focused = true;
        private bool visible = true;
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void NubikReady();
        [DllImport("__Internal")] private static extern void NubikGameplay(int active);
        [DllImport("__Internal")] private static extern void NubikShowRewarded(int ticket);
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
            OnAdOpen("");
            yield return new WaitForSecondsRealtime(1f);
            OnAdRewarded(ticket.ToString());
            OnAdClose("");
        }

        // Messages from the page (Yandex.jslib / index.html).
        public void OnAdOpen(string _) { AdShowing = true; Apply(); }
        public void OnAdRewarded(string ticket) { if (int.TryParse(ticket, out int value)) AdRewarded?.Invoke(value); }
        public void OnAdClose(string _) => EndAd(false);
        public void OnAdError(string _) => EndAd(true);

        private void EndAd(bool failed)
        {
            if (!AdShowing) return;
            AdShowing = false;
            Apply();
            AdFinished?.Invoke(failed);
        }

        public void SetInMine(bool value) { inMine = value; Apply(); }
        public void OnVisibility(string value) { visible = value == "1"; Apply(); }
        private void OnApplicationFocus(bool value) { focused = value; Apply(); }
        private void Apply()
        {
            Paused = !focused || !visible || AdShowing;
            Time.timeScale = Paused ? 0 : 1;
            AudioListener.pause = Paused;
#if UNITY_WEBGL && !UNITY_EDITOR
            NubikGameplay(inMine && !Paused ? 1 : 0);
#endif
        }
        private void OnDestroy() { Paused = false; Time.timeScale = 1; AudioListener.pause = false; }
    }
}
