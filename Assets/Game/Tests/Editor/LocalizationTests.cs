using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace Nubik.Tests
{
    public class LocalizationTests
    {
        private const string SettingsKey = "nubik.settings.v1";
        private string savedSettings, previousLanguage;
        private bool existed;
        [SetUp] public void Setup()
        {
            existed = PlayerPrefs.HasKey(SettingsKey); savedSettings = PlayerPrefs.GetString(SettingsKey);
            previousLanguage = GameSettings.Current.language; Localization.Select("en");
        }
        [TearDown] public void Restore()
        {
            GameSettings.Set(s => s.language = previousLanguage, false);
            if (existed) PlayerPrefs.SetString(SettingsKey, savedSettings); else PlayerPrefs.DeleteKey(SettingsKey);
            PlayerPrefs.Save();
        }
        [Test] public void OldSettingsAskForLanguageWithoutResettingOtherSettings()
        {
            PlayerPrefs.SetString(SettingsKey, "{\"master\":0.4,\"quality\":2,\"cameraSensitivity\":1.5}");
            var settings = GameSettings.Load();
            Assert.IsTrue(string.IsNullOrEmpty(settings.language));
            Assert.AreEqual(.4f, settings.master); Assert.AreEqual(2, settings.quality);
            Assert.AreEqual(1.5f, settings.cameraSensitivity);
        }
        [Test] public void LanguageIsPersistentAndIndependentOfProgress()
        {
            string progress = PlayerPrefs.GetString("nubik.progress.v1", "");
            Localization.Select("ru"); Assert.AreEqual("ru", GameSettings.Load().language);
            Localization.Select("en"); Assert.AreEqual("en", GameSettings.Load().language);
            Assert.AreEqual(progress, PlayerPrefs.GetString("nubik.progress.v1", ""));
        }
        [TestCase("21 монета", "21 coins")]
        [TestCase("1 монета", "1 coin")]
        [TestCase("Рюкзак: 3 / 8 слотов", "Backpack: 3 / 8 slots")]
        [TestCase("ЗАКАЗ · ЗВЁЗДНЫЙ МЕТАЛЛ", "ORDER · STAR METAL")]
        [TestCase("Ключи 3/5 · дверь на 120 м", "Keys 3/5 · door at 120 m")]
        [TestCase("Здоровье с 100 до 125: падения менее опасны", "Health: 100 → 125: safer falls")]
        public void DynamicLabelsTranslate(string source, string expected) => Assert.AreEqual(expected, Localization.Translate(source));
        [Test] public void HiddenLabelsSwitchBothWaysWithoutLosingSource()
        {
            var obj = new GameObject("label", typeof(RectTransform), typeof(LocalizedText));
            var label = obj.GetComponent<LocalizedText>(); label.text = "Начать вылазку";
            Assert.AreEqual("Start exploring", label.text);
            obj.SetActive(false); Localization.Select("ru"); obj.SetActive(true);
            Assert.AreEqual("Начать вылазку", label.text);
            Localization.Select("en"); Assert.AreEqual("Start exploring", label.text);
            Object.DestroyImmediate(obj);
        }
        [Test] public void CatalogAndSerializedBalanceContainNoUntranslatedText()
        {
            var catalog = JsonUtility.FromJson<TestCatalog>(Resources.Load<TextAsset>("Localization/English").text);
            foreach (var entry in catalog.entries)
                Assert.IsFalse(Regex.IsMatch(Localization.Translate(entry.ru), "[А-Яа-яЁё]"), entry.ru);
            var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<MineConfig>("Assets/Game/Config/MineBalance.asset");
            var property = new UnityEditor.SerializedObject(asset).GetIterator();
            while (property.Next(true))
                if (property.propertyType == UnityEditor.SerializedPropertyType.String)
                    Assert.IsFalse(Regex.IsMatch(Localization.Translate(property.stringValue), "[А-Яа-яЁё]"), property.propertyPath);
        }
        [System.Serializable] private class TestEntry { public string ru, en; }
        [System.Serializable] private class TestCatalog { public TestEntry[] entries; }
    }
}
