using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Nubik.PlayTests
{
    /// <summary>A first launch on Yandex Games: the platform's language is taken while the HUD is being built.</summary>
    public class PlatformLanguageStartTests
    {
        private const string SettingsKey = "nubik.settings.v1", ProgressKey = "nubik.progress.v1";
        private string savedSettings, savedProgress, savedBackup, savedLanguage;
        private Func<string> platform;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            savedSettings = PlayerPrefs.GetString(SettingsKey, null);
            savedProgress = PlayerPrefs.GetString(ProgressKey, null);
            savedBackup = PlayerPrefs.GetString(ProgressKey + ".backup", null);
            savedLanguage = GameSettings.Current.language;
            platform = Localization.PlatformLanguage;
            PlayerPrefs.DeleteKey(ProgressKey);
            PlayerPrefs.DeleteKey(ProgressKey + ".backup");
            GameSettings.Set(s => s.language = "", false);
            Localization.PlatformLanguage = () => "ru";
            yield return SceneManager.LoadSceneAsync("Mine");
        }

        [UnityTearDown]
        public IEnumerator Restore()
        {
            Localization.PlatformLanguage = platform;
            var scene = SceneManager.GetSceneByName("Mine");
            if (scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            Put(SettingsKey, savedSettings);
            Put(ProgressKey, savedProgress);
            Put(ProgressKey + ".backup", savedBackup);
            PlayerPrefs.Save();
            GameSettings.Set(s => s.language = savedLanguage, false);
        }

        private static void Put(string key, string value)
        {
            if (value == null) PlayerPrefs.DeleteKey(key); else PlayerPrefs.SetString(key, value);
        }

        [UnityTest]
        public IEnumerator ThePlatformLanguageIsTakenWithoutAQuestion()
        {
            for (int i = 0; i < 5; i++) yield return null;
            var hud = UnityEngine.Object.FindAnyObjectByType<MineHud>();
            Assert.AreEqual("ru", GameSettings.Current.language);
            Assert.IsFalse(hud.LanguageChoiceOpen || hud.WaitingForLanguage);
            var wallet = GameObject.Find("Wallet").GetComponentsInChildren<Text>();
            Assert.AreEqual("0", wallet[wallet.Length - 1].text);
        }

        [UnityTest]
        public IEnumerator EveryTruncatingLabelHasRoomForOneLine()
        {
            // Truncate hides a line that does not fit at all: a label only just tall enough vanishes at some screen scales.
            yield return null;
            int checkedLabels = 0;
            foreach (var label in UnityEngine.Object.FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (label.verticalOverflow != VerticalWrapMode.Truncate || label.resizeTextForBestFit || label.font == null) continue;
                // NotoSans hhea: ascender 1069 + descender 293 per 1000 units (the editor's dynamic font reports no real line height).
                float line = 1.362f * label.fontSize;
                Assert.GreaterOrEqual(label.rectTransform.rect.height, line * 1.08f,
                    label.transform.parent.name + "/" + label.name + " '" + label.text + "' size " + label.fontSize);
                checkedLabels++;
            }
            Assert.Greater(checkedLabels, 50);
        }
    }
}
