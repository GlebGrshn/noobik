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

        /// <summary>v2 kept the backpack as a coin total; v3 keeps ore pieces.</summary>
        [Serializable]
        private sealed class LegacyV2 { public int backpack, tool; }

        // Shovel indices of v1/v2 (basic, copper, steel, crystal) in the eight-level list of v3.
        private static readonly int[] OldTools = { 0, 1, 3, 6 };

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
                if (version != 2 && version != 3 && version != GameProgress.CurrentVersion) return null;
                var data = JsonUtility.FromJson<GameProgress>(json);
                if (data != null && version == 2) Migrate(data, JsonUtility.FromJson<LegacyV2>(json), config);
                if (data != null && data.version == 3 && !ExpandWorld(data, config)) return null;
                return data != null && data.IsValid(config) ? data : null;
            }
            catch (ArgumentException) { return null; }
        }

        private static GameProgress Migrate(LegacyV1 old, MineConfig config)
        {
            if (old == null || old.seed != config.seed || old.coins < 0 || old.backpack < 0 || old.pickTier < 0 || old.expeditions < 0) return null;
            var data = GameProgress.New(config);
            // Loot of the old backpack is paid out: v3 bags hold ore pieces, not coin totals.
            data.coins = old.coins + old.backpack;
            data.tool = ToolFromOld(old.pickTier, config);
            data.expeditions = old.expeditions;
            // The helmet bonus was already paid in v1.
            if (old.foundHelmet && config.collection.Length > 0) data.collection = 1;
            return data.IsValid(config) ? data : null;
        }

        private static void Migrate(GameProgress data, LegacyV2 old, MineConfig config)
        {
            if (old == null || old.backpack < 0) { data.version = -1; return; }
            data.version = 3;
            data.coins += old.backpack;
            data.tool = ToolFromOld(old.tool, config);
        }

        private static int ToolFromOld(int tier, MineConfig config) =>
            tier < 0 ? -1 : Mathf.Min(tier < OldTools.Length ? OldTools[tier] : OldTools[OldTools.Length - 1], config.tools.Length - 1);

        private static bool ExpandWorld(GameProgress data, MineConfig config)
        {
            var oldConfig = UnityEngine.Object.Instantiate(config);
            try
            {
                oldConfig.width = 12;
                data.version = GameProgress.CurrentVersion;
                data.worldWidth = 12;
                // The prototype's depth milestone is not the new campaign victory.
                data.finished = false;
                data.fuel = config.fuelTank[0].value;
                if (!data.IsValid(oldConfig) || data.found.Length > oldConfig.ChunkCount) return false;
                var old = new VoxelTerrain(oldConfig);
                foreach (var entry in data.terrain) if (!old.Decode(entry.chunk, entry.data)) return false;
                var expanded = new VoxelTerrain(config);
                expanded.ImportExcavation(old);
                data.terrain.Clear();
                foreach (int chunk in expanded.EditedChunks()) data.terrain.Add(new ChunkSave { chunk = chunk, data = expanded.Encode(chunk) });
                var found = new long[config.LootChunkCount];
                for (int i = 0; i < data.found.Length; i++)
                {
                    var c = old.ChunkCoords(i);
                    found[(c.y * 4 + c.z + 1) * 4 + c.x + 1] = data.found[i];
                }
                data.found = found;
                data.worldWidth = config.width;
                return true;
            }
            finally { UnityEngine.Object.DestroyImmediate(oldConfig); }
        }

        public static string Encode(GameProgress data) => JsonUtility.ToJson(data);

        /// <summary>
        /// Starts over on purpose: the old save and its backup are replaced by fresh progress, so a later
        /// failed read cannot bring the old game back.
        /// </summary>
        public static GameProgress Reset(MineConfig config, bool muted)
        {
            var fresh = GameProgress.New(config);
            fresh.muted = muted;
            writable = true;
            Status = "Новая игра";
            try
            {
                PlayerPrefs.DeleteKey(Key + ".backup");
                PlayerPrefs.SetString(Key, Encode(fresh));
                PlayerPrefs.Save();
            }
            catch (Exception e)
            {
                Status = "Не удалось сохранить прогресс на устройстве";
                Debug.LogWarning("Local save failed: " + e.Message);
            }
            return fresh;
        }

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
