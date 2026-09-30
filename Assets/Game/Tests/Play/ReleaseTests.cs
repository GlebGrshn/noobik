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
        [UnityTest] public IEnumerator PlatformLanguageIsTakenWithoutAskingAndOtherwiseThePlayerChooses()
        {
            var hud = Object.FindAnyObjectByType<MineGame>().GetComponent<MineHud>();
            var platform = Localization.PlatformLanguage;
            string settings = PlayerPrefs.GetString("nubik.settings.v1", null);
            try
            {
                // Yandex Games: while its SDK answers nothing is asked, then its language is taken.
                string answer = "";
                Localization.PlatformLanguage = () => answer;
                GameSettings.Set(s => s.language = "", false);
                hud.AwaitLanguage();
                yield return null;
                Assert.IsFalse(hud.LanguageChoiceOpen);
                Assert.IsTrue(hud.WaitingForLanguage);
                answer = "en";
                yield return null;
                Assert.AreEqual("en", GameSettings.Current.language);
                Assert.IsFalse(hud.LanguageChoiceOpen || hud.WaitingForLanguage);

                // A language the player picked later stays on the next launch.
                Localization.Select("ru");
                hud.AwaitLanguage();
                yield return null;
                Assert.AreEqual("ru", GameSettings.Current.language);

                // The SDK failed to load: the player is asked instead.
                GameSettings.Set(s => s.language = "", false);
                answer = "";
                hud.AwaitLanguage();
                yield return null;
                answer = null;
                yield return null;
                Assert.IsTrue(hud.LanguageChoiceOpen);
                GameObject.Find("Choose Russian").GetComponent<Button>().onClick.Invoke();
                Assert.AreEqual("ru", GameSettings.Current.language);

                // Other portals have no platform language: the question comes at once.
                GameSettings.Set(s => s.language = "", false);
                hud.AwaitLanguage();
                Assert.IsTrue(hud.LanguageChoiceOpen);
                GameObject.Find("Choose Russian").GetComponent<Button>().onClick.Invoke();
            }
            finally
            {
                Localization.PlatformLanguage = platform;
                if (settings == null) PlayerPrefs.DeleteKey("nubik.settings.v1"); else PlayerPrefs.SetString("nubik.settings.v1", settings);
                PlayerPrefs.Save();
            }
        }

        [UnityTest] public IEnumerator LanguageChoiceAndLiveSwitchTranslateTheWholeInterface()
        {
            var game = Object.FindAnyObjectByType<MineGame>(); PlayByTouch(game);
            var hud = game.GetComponent<MineHud>();
            string settings = PlayerPrefs.GetString("nubik.settings.v1", null);
            GameSettings.Set(s => s.language = "", false);
            hud.ShowLanguageChoice();
            Assert.IsTrue(hud.PanelOpen); Assert.IsFalse(game.Active);
            hud.ClosePanel(); Assert.IsTrue(hud.LanguageChoiceOpen);
            GameObject.Find("Choose English").GetComponent<Button>().onClick.Invoke();
            Assert.IsFalse(hud.LanguageChoiceOpen);
            Assert.AreEqual("en", GameSettings.Load().language);
            for (int page = 0; page < 5; page++)
            {
                hud.ShowHouse(page); yield return null;
                foreach (var label in Object.FindObjectsByType<Text>(FindObjectsSortMode.None))
                    Assert.IsFalse(System.Text.RegularExpressions.Regex.IsMatch(label.text, "[А-Яа-яЁё]"), label.text);
            }
            hud.OpenSettings(); yield return null;
            Assert.IsTrue(Object.FindObjectsByType<Text>(FindObjectsSortMode.None).Any(t => t.text == "SETTINGS"));
            Localization.Select("ru"); yield return null;
            Assert.IsTrue(Object.FindObjectsByType<Text>(FindObjectsSortMode.None).Any(t => t.text == "НАСТРОЙКИ"));
            Localization.Select("en"); yield return null;
            Assert.IsTrue(Object.FindObjectsByType<Text>(FindObjectsSortMode.None).Any(t => t.text == "SETTINGS"));
            hud.CloseSettings(); hud.ClosePanel();
            if (settings == null) PlayerPrefs.DeleteKey("nubik.settings.v1"); else PlayerPrefs.SetString("nubik.settings.v1", settings);
            PlayerPrefs.Save();
        }

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
