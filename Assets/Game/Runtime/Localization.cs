using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Nubik
{
    /// <summary>Russian source strings remain stable save/config identifiers; only display text is translated.</summary>
    public static class Localization
    {
        [Serializable] private sealed class Entry { public string ru, en; }
        [Serializable] private sealed class Catalog { public Entry[] entries; }
        private static readonly Dictionary<string, string> English = new Dictionary<string, string>(StringComparer.Ordinal);
        private static readonly Dictionary<string, string> Cache = new Dictionary<string, string>(StringComparer.Ordinal);
        private static Regex phrases;
        private static readonly Regex Counts = new Regex(@"\b(\d+) (coin|coins|hit|hits|slot|slots|find|finds)\b");
        public static event Action Changed;
        public static bool Chosen => GameSettings.Current.language == "ru" || GameSettings.Current.language == "en";
        public static bool IsEnglish => GameSettings.Current.language != "ru";
        public static string Language => IsEnglish ? "en" : "ru";
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void NubikLanguage(string language);
        [DllImport("__Internal")] private static extern int NubikPlatformLanguage();
#endif
        /// <summary>
        /// The platform's language for a first launch: null when it has none (the player chooses), "" while it is still
        /// answering, otherwise "ru" or "en". Yandex Games requires the language of its SDK (requirement 2.14). Tests replace it.
        /// </summary>
        public static Func<string> PlatformLanguage = () =>
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            switch (NubikPlatformLanguage()) { case 1: return ""; case 2: return "ru"; case 3: return "en"; }
#endif
            return null;
        };
        public static void Select(string language)
        {
            if (language != "ru" && language != "en") return;
            GameSettings.Set(s => s.language = language);
            Changed?.Invoke();
            SyncPage();
        }
        public static void SyncPage()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            NubikLanguage(Language);
#endif
        }
        private static void Load()
        {
            if (phrases != null) return;
            var asset = Resources.Load<TextAsset>("Localization/English");
            if (asset == null) throw new InvalidOperationException("English localization catalog is missing");
            foreach (var entry in JsonUtility.FromJson<Catalog>(asset.text).entries) English[entry.ru] = entry.en;
            // Biome/ore names also appear in uppercase HUD labels.
            foreach (var entry in English.ToArray())
                if (!English.ContainsKey(entry.Key.ToUpperInvariant())) English[entry.Key.ToUpperInvariant()] = entry.Value.ToUpperInvariant();
            const string letters = @"[\p{L}]";
            phrases = new Regex(string.Join("|", English.Keys.OrderByDescending(s => s.Length).Select(s =>
                (char.IsLetter(s[0]) ? "(?<!" + letters + ")" : "") + Regex.Escape(s) +
                (char.IsLetter(s[s.Length - 1]) ? "(?!" + letters + ")" : ""))), RegexOptions.CultureInvariant);
        }
        public static string Translate(string source)
        {
            if (!IsEnglish || string.IsNullOrEmpty(source)) return source ?? "";
            Load();
            if (Cache.TryGetValue(source, out var cached)) return cached;
            string result = English.TryGetValue(source, out var exact) ? exact : phrases.Replace(source, m => English[m.Value]);
            result = Counts.Replace(result, m => {
                string word = m.Groups[2].Value.TrimEnd('s');
                return m.Groups[1].Value + " " + word + (m.Groups[1].Value == "1" ? "" : "s");
            });
            // Dynamic coin/depth labels are unbounded; keep the cache bounded for long sessions.
            if (Cache.Count >= 2048) Cache.Clear();
            Cache[source] = result;
            return result;
        }
        public static string Source(UnityEngine.UI.Text text) => text is LocalizedText label ? label.SourceText : text.text;
    }
}
