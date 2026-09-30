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
        [TestCase("Здоровье с 100 до 125: падения менее опасны", "Health: 100 to 125: safer falls")]
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
        [Test] public void FractionsFollowTheGameLanguageNotTheBrowser()
        {
            Assert.AreEqual("2.3", Localization.Decimal(2.3f));
            Assert.AreEqual("1.25", Localization.Decimal(1.25f));
            Localization.Select("ru");
            Assert.AreEqual("2,3", Localization.Decimal(2.3f));
            Assert.AreEqual("14", Localization.Decimal(14f));
        }
        [Test] public void EveryTranslatedCharacterIsInTheGameFont()
        {
            // A glyph missing from the bundled font draws as an empty gap in the browser (the upgrade arrow did);
            // the editor would quietly fall back to system fonts, so read the font file's own character map.
            var ranges = FontRanges(System.IO.File.ReadAllBytes("Assets/Game/Resources/Fonts/NotoSans.ttf"));
            bool Has(char c) => char.IsControl(c) || ranges.Exists(r => r.start <= c && c <= r.end);
            Assert.IsTrue(Has('Ж') && Has('·') && !Has('\u2192'), "The character map is read correctly.");
            var catalog = JsonUtility.FromJson<TestCatalog>(Resources.Load<TextAsset>("Localization/English").text);
            foreach (var entry in catalog.entries)
                foreach (char c in entry.en)
                    Assert.IsTrue(Has(c), "No glyph U+" + ((int)c).ToString("X4") + " in \"" + entry.en + "\"");
        }
        /// <summary>Character ranges of the font's format 4 cmap subtables (the Basic Multilingual Plane).</summary>
        private static System.Collections.Generic.List<(int start, int end)> FontRanges(byte[] d)
        {
            int U16(int o) => d[o] << 8 | d[o + 1];
            int U32(int o) => d[o] << 24 | d[o + 1] << 16 | d[o + 2] << 8 | d[o + 3];
            int cmap = -1;
            for (int i = 0; i < U16(4); i++)
                if (System.Text.Encoding.ASCII.GetString(d, 12 + 16 * i, 4) == "cmap") cmap = U32(12 + 16 * i + 8);
            var ranges = new System.Collections.Generic.List<(int start, int end)>();
            for (int i = 0; i < U16(cmap + 2); i++)
            {
                int table = cmap + U32(cmap + 4 + 8 * i + 4);
                if (U16(table) != 4) continue;
                int segments = U16(table + 6) / 2;
                for (int k = 0; k < segments; k++) ranges.Add((U16(table + 16 + 2 * segments + 2 * k), U16(table + 14 + 2 * k)));
            }
            return ranges;
        }
        [System.Serializable] private class TestEntry { public string ru, en; }
        [System.Serializable] private class TestCatalog { public TestEntry[] entries; }
    }
}
