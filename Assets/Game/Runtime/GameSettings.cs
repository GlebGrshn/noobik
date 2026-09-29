using System;
using UnityEngine;

namespace Nubik
{
    /// <summary>
    /// Device settings: audio, graphics and camera sensitivity. Kept apart from progress, so starting over or a
    /// broken save never resets them.
    /// </summary>
    [Serializable]
    public sealed class GameSettings
    {
        public const int Low = 0, Normal = 1, Ultra = 2;
        public static readonly string[] QualityNames = { "Низко", "Обычно", "Ультра" };
        private const string Key = "nubik.settings.v1";

        public float master = 1, music = .7f, effects = 1, ambience = .7f;
        public const float MinSensitivity = .25f, MaxSensitivity = 2f;
        public float cameraSensitivity = 1f;
        public int quality = Normal;

        public static GameSettings Current { get; private set; } = Load();
        /// <summary>Raised after any change, so audio and graphics follow at once.</summary>
        public static event Action Changed;

        public static GameSettings Load()
        {
            try
            {
                var json = PlayerPrefs.GetString(Key, "");
                var loaded = string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<GameSettings>(json);
                if (loaded != null) { loaded.Clamp(); return loaded; }
            }
            catch (ArgumentException) { }
            return new GameSettings();
        }

        private void Clamp()
        {
            master = Mathf.Clamp01(float.IsNaN(master) ? 1 : master);
            music = Mathf.Clamp01(float.IsNaN(music) ? .7f : music);
            effects = Mathf.Clamp01(float.IsNaN(effects) ? 1 : effects);
            ambience = Mathf.Clamp01(float.IsNaN(ambience) ? .7f : ambience);
            quality = Mathf.Clamp(quality, Low, Ultra);
            cameraSensitivity = float.IsNaN(cameraSensitivity) || float.IsInfinity(cameraSensitivity) || cameraSensitivity <= 0
                ? 1f : Mathf.Clamp(cameraSensitivity, MinSensitivity, MaxSensitivity);
        }

        /// <summary>Applies a change at once; <paramref name="persist"/> false while a slider is dragged, <see cref="Save"/> later.</summary>
        public static void Set(Action<GameSettings> change, bool persist = true)
        {
            change(Current);
            Current.Clamp();
            if (persist) Save();
            Changed?.Invoke();
        }

        public static void Save()
        {
            try { PlayerPrefs.SetString(Key, JsonUtility.ToJson(Current)); PlayerPrefs.Save(); }
            catch (Exception e) { Debug.LogWarning("Settings not saved: " + e.Message); }
        }
    }
}
