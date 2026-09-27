using System;
using UnityEngine;

namespace Nubik
{
    public static class ProgressStore
    {
        private const string Key = "nubik.progress.v1";
        public static string Status { get; private set; } = "Прогресс сохраняется на этом устройстве";
        // Keep the previous valid snapshot. A broken/unknown save is never silently overwritten.
        private static bool writable = true;

        public static GameProgress Load(MineConfig config)
        {
            writable = true;
            if (!PlayerPrefs.HasKey(Key)) return new GameProgress { seed = config.seed };
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
            return new GameProgress { seed = config.seed };
        }

        public static GameProgress Decode(string json, MineConfig config)
        {
            if (string.IsNullOrWhiteSpace(json) || !json.Contains("\"version\"") || !json.Contains("\"seed\"")) return null;
            try
            {
                var data = JsonUtility.FromJson<GameProgress>(json);
                return data != null && data.IsValid(config) ? data : null;
            }
            catch (ArgumentException) { return null; }
        }

        public static void Save(GameProgress data, MineConfig config)
        {
            if (!writable) return;
            try
            {
                string old = PlayerPrefs.GetString(Key, "");
                if (Decode(old, config) != null) PlayerPrefs.SetString(Key + ".backup", old);
                PlayerPrefs.SetString(Key, JsonUtility.ToJson(data));
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
