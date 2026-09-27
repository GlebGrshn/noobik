using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nubik
{
    public static class ProgressStore
    {
        // The key name predates schema v2; the version lives inside the JSON.
        private const string Key = "nubik.progress.v1";
        public static string Status { get; private set; } = "Прогресс сохраняется на этом устройстве";
        // Keep the previous valid snapshot. A broken/unknown save is never silently overwritten.
        private static bool writable = true;

        [Serializable]
        private sealed class VersionProbe { public int version; }

        /// <summary>Top-down grid prototype save. Terrain is not carried over: the world changed.</summary>
        [Serializable]
        private sealed class LegacyV1
        {
            public int version, seed, coins, backpack, pickTier, maxDepth, expeditions;
            public bool foundHelmet;
            public List<int> destroyed = new List<int>();
        }

        public static GameProgress Load(MineConfig config)
        {
            writable = true;
            if (!PlayerPrefs.HasKey(Key)) return GameProgress.New(config);
            var result = Decode(PlayerPrefs.GetString(Key), config);
            if (result != null) return result;
            result = Decode(PlayerPrefs.GetString(Key + ".backup", ""), config);
            if (result != null)
            {
                Status = "Восстановлена резервная копия прогресса";
                return result;
            }
            writable = false;
            Status = "Сохранение не прочитано. Исходные данные сохранены; новая сессия временная";
            return GameProgress.New(config);
        }

        public static GameProgress Decode(string json, MineConfig config)
        {
            if (string.IsNullOrWhiteSpace(json) || !json.Contains("\"version\"") || !json.Contains("\"seed\"")) return null;
            try
            {
                int version = JsonUtility.FromJson<VersionProbe>(json).version;
                if (version == 1) return Migrate(JsonUtility.FromJson<LegacyV1>(json), config);
                if (version != GameProgress.CurrentVersion) return null;
                var data = JsonUtility.FromJson<GameProgress>(json);
                return data != null && data.IsValid(config) ? data : null;
            }
            catch (ArgumentException) { return null; }
        }

        private static GameProgress Migrate(LegacyV1 old, MineConfig config)
        {
            if (old == null || old.seed != config.seed || old.coins < 0 || old.backpack < 0 || old.pickTier < 0 || old.expeditions < 0) return null;
            var data = GameProgress.New(config);
            data.coins = old.coins;
            data.backpack = old.backpack;
            data.tool = Mathf.Min(old.pickTier, config.tools.Length - 1);
            data.expeditions = old.expeditions;
            // The helmet bonus was already paid in v1.
            if (old.foundHelmet && config.collection.Length > 0) data.collection = 1;
            return data.IsValid(config) ? data : null;
        }

        public static string Encode(GameProgress data) => JsonUtility.ToJson(data);

        public static void Save(GameProgress data, MineConfig config)
        {
            if (!writable) return;
            try
            {
                string old = PlayerPrefs.GetString(Key, "");
                if (Decode(old, config) != null) PlayerPrefs.SetString(Key + ".backup", old);
                PlayerPrefs.SetString(Key, Encode(data));
                PlayerPrefs.Save();
            }
            catch (Exception e)
            {
                Status = "Не удалось сохранить прогресс на устройстве";
                Debug.LogWarning("Local save failed: " + e.Message);
            }
        }
    }
}
