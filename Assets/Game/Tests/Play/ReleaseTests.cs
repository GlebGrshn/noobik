using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Nubik.PlayTests
{
    public partial class DepthSnapshots
    {
        [UnityTest] public IEnumerator PortalMuteDoesNotFreezePlayAndSurvivesVisibilityChanges()
        {
            var game = Object.FindAnyObjectByType<MineGame>(); PlayByTouch(game);
            var bridge = game.GetComponent<YandexBridge>();
            bridge.OnPlatformMute("1");
            Assert.IsFalse(YandexBridge.Paused);
            Assert.AreEqual(1, Time.timeScale);
            Assert.IsTrue(AudioListener.pause);
            bridge.OnVisibility("0"); bridge.OnVisibility("1");
            Assert.IsTrue(AudioListener.pause);
            bridge.OnPlatformMute("0");
            Assert.IsFalse(AudioListener.pause);
            yield return null;
        }

        [UnityTest] public IEnumerator InterstitialWaitsFourActiveMinutesAndPausesWithoutReward()
        {
            var game = Object.FindAnyObjectByType<MineGame>(); PlayByTouch(game);
            var bridge = game.GetComponent<YandexBridge>();
            var hud = game.GetComponent<MineHud>();
            int rewards = 0, rewardClosures = 0;
            bridge.AdRewarded += _ => rewards++;
            bridge.AdFinished += _ => rewardClosures++;
            int coins = game.Progress.coins;
            float initialSeconds = bridge.ActivePlaySeconds;
            bridge.AdvancePlayTime(1000, false);
            Assert.AreEqual(initialSeconds, bridge.ActivePlaySeconds);
            bridge.OnVisibility("0"); bridge.AdvancePlayTime(1000, true);
            Assert.AreEqual(initialSeconds, bridge.ActivePlaySeconds);
            bridge.OnVisibility("1");
            bridge.AdvancePlayTime(239 - initialSeconds, true);
            Assert.IsFalse(bridge.TryShowInterstitial());
            bridge.AdvancePlayTime(1, true);
            Assert.IsTrue(bridge.InterstitialDue);
            Assert.IsFalse(bridge.AdShowing, "Time alone never interrupts a swing or flight.");
            game.OpenMenu();
            Assert.IsTrue(bridge.AdShowing); Assert.IsTrue(YandexBridge.Paused);
            Assert.AreEqual(0, Time.timeScale); Assert.IsTrue(AudioListener.pause);
            Assert.IsFalse(bridge.TryShowInterstitial(), "No concurrent ad.");
            bridge.OnAdRewarded("1"); bridge.OnAdClose("");
            Assert.IsTrue(bridge.AdShowing, "Rewarded callbacks cannot close an interstitial.");
            yield return new WaitForSecondsRealtime(1.2f);
            Assert.IsFalse(bridge.AdShowing); Assert.IsFalse(YandexBridge.Paused);
            Assert.AreEqual(1, Time.timeScale); Assert.IsFalse(AudioListener.pause);
            Assert.AreEqual(0, rewards); Assert.AreEqual(0, rewardClosures);
            Assert.AreEqual(coins, game.Progress.coins);
            Assert.IsFalse(bridge.InterstitialDue);
            // The next interval can be consumed on opening the house. An error restores the game.
            bridge.AdvancePlayTime(240, true); hud.ShowHouse(MineHud.SellPage);
            Assert.IsTrue(bridge.AdShowing);
            bridge.OnInterstitialError(""); bridge.OnInterstitialClose("");
            Assert.IsFalse(YandexBridge.Paused); Assert.IsFalse(bridge.InterstitialDue);
            yield return new WaitForSecondsRealtime(1.1f);
            // Voluntary rewarded ads remain separate, pay their ticket, and restart the interval.
            bridge.AdvancePlayTime(220, true);
            bridge.ShowRewarded(1);
            yield return new WaitForSecondsRealtime(1.2f);
            Assert.AreEqual(1, rewards); Assert.AreEqual(1, rewardClosures);
            Assert.AreEqual(0, bridge.ActivePlaySeconds);
        }

        [UnityTest] public IEnumerator ReleaseHasNoFreeUpgradeOrContinuousNoiseBed()
        {
            var game = Object.FindAnyObjectByType<MineGame>(); PlayByTouch(game);
            game.Progress.coins = 0;
            int tool = game.Progress.tool;
            game.BuyFree(Track.Tool);
            Assert.AreEqual(tool, game.Progress.tool);
            var hud = game.GetComponent<MineHud>(); hud.ShowHouse(MineHud.UpgradePage);
            var ui = GameObject.Find("Game UI");
            Assert.IsFalse(ui.GetComponentsInChildren<Text>(true).Any(t => t.text.Contains("Даром")));
            hud.ClosePanel();
            game.DebugDig("40");
            yield return new WaitForSeconds(.3f);
            Assert.IsFalse(Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None)
                .Any(s => s.isPlaying && s.clip != null && s.clip.name.StartsWith("ambient_")));
        }
    }
}
