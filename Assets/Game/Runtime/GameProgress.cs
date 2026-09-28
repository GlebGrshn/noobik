using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nubik
{
    [Serializable]
    public sealed class ChunkSave
    {
        public int chunk;
        public string data;
    }

    public enum Track { Tool, Backpack, Jetpack, Health }

    public enum Pickup { Collected, BagFull, Gone }

    /// <summary>
    /// Save schema v3. Every payout is guarded by a bit or a counter, so repeating a call
    /// (double tap, reload, returning from an ad) never pays twice. New fields need defaults.
    /// Ore rides in the backpack until it is sold in the house; items take backpack slots too.
    /// </summary>
    [Serializable]
    public sealed class GameProgress
    {
        public const int CurrentVersion = 3;
        public int version = CurrentVersion;
        public int seed;
        public int coins;
        public int tool;
        public int maxDepth;
        public int expeditions;
        /// <summary>Hundredths of a coin earned from dirt that has not become a whole coin yet.</summary>
        public int digCredit;
        public int collection;
        public int specials;
        public bool finished;
        public bool muted;
        /// <summary>Where the next session starts.</summary>
        public bool hasResume;
        public Vector3 resume;
        public float resumeYaw;
        /// <summary>Last underground spot (kept from v2 saves; nothing teleports there any more).</summary>
        public bool hasDive;
        public Vector3 dive;
        public float diveYaw;
        public long[] found = new long[0];
        public List<ChunkSave> terrain = new List<ChunkSave>();
        /// <summary>Pieces of each ore entry in the backpack.</summary>
        public int[] ores = new int[0];
        public int bagLevel, jetLevel, healthLevel;
        /// <summary>Current health; negative means full (older saves).</summary>
        public float health = -1;
        public bool scanner;
        public int medkits;

        public static GameProgress New(MineConfig config) => new GameProgress { seed = config.seed };

        public bool IsValid(MineConfig config)
        {
            if (version != CurrentVersion || seed != config.seed || coins < 0 || expeditions < 0) return false;
            if (tool < 0 || tool >= config.tools.Length || maxDepth < 0 || maxDepth > config.depth) return false;
            if (bagLevel < 0 || bagLevel >= config.backpack.Length || jetLevel < 0 || jetLevel >= config.jetpack.Length) return false;
            if (healthLevel < 0 || healthLevel >= config.health.Length || float.IsNaN(health) || health > MaxHealth(config) || medkits < 0) return false;
            if (ores == null || ores.Length > config.ores.Length) return false;
            foreach (int count in ores) if (count < 0) return false;
            if (digCredit < 0 || digCredit >= 100 || found == null || found.Length > config.ChunkCount || terrain == null) return false;
            if (collection < 0 || collection >= 1 << config.collection.Length || specials < 0) return false;
            if (hasResume && !(IsFinite(resume) && resume.y > config.FloorY - 1)) return false;
            if (hasDive && !(IsFinite(dive) && dive.y > config.FloorY - 1)) return false;
            var chunks = new HashSet<int>();
            foreach (var entry in terrain)
                if (entry == null || entry.chunk < 0 || entry.chunk >= config.ChunkCount || !chunks.Add(entry.chunk)) return false;
            return true;
        }

        private static bool IsFinite(Vector3 v) => !float.IsNaN(v.x + v.y + v.z) && !float.IsInfinity(v.x + v.y + v.z);

        public bool IsFound(int chunk, int slot) => chunk >= 0 && chunk < found.Length && (found[chunk] & 1L << slot) != 0;
        public bool HasSpecial(int index) => (specials & 1 << index) != 0;
        public bool HasCollectible(int index) => (collection & 1 << index) != 0;
        public int CollectionCount { get { int n = 0; for (int bits = collection; bits != 0; bits &= bits - 1) n++; return n; } }

        // ---------- Backpack ----------

        public int Capacity(MineConfig config) => Mathf.RoundToInt(config.backpack[bagLevel].value);
        public int OreCount(int ore) => ore < ores.Length ? ores[ore] : 0;

        public int UsedSlots(MineConfig config)
        {
            int used = (scanner ? config.scanner.slots : 0) + medkits * config.medkit.slots;
            for (int i = 0; i < ores.Length; i++) used += ores[i] * Mathf.Max(1, config.ores[i].slots);
            return used;
        }

        public int FreeSlots(MineConfig config) => Capacity(config) - UsedSlots(config);

        public int BagValue(MineConfig config)
        {
            int value = 0;
            for (int i = 0; i < ores.Length; i++) value += ores[i] * config.ores[i].value;
            return value;
        }

        public int OrePieces { get { int n = 0; foreach (int count in ores) n += count; return n; } }

        /// <summary>Takes a find into the backpack, or records a collectible. Nothing changes if the bag is full.</summary>
        public Pickup Collect(LootItem item, MineConfig config)
        {
            if (item.Kind == LootKind.Collectible)
            {
                if (item.Collectible < 0 || item.Collectible >= config.collection.Length || HasCollectible(item.Collectible)) return Pickup.Gone;
                collection |= 1 << item.Collectible;
                coins += config.firstDiscoveryCoins;
                return Pickup.Collected;
            }
            if (item.Ore < 0 || item.Ore >= config.ores.Length) return Pickup.Gone;
            if (item.Special >= 0 ? item.Special > 30 || HasSpecial(item.Special)
                : item.Chunk < 0 || item.Chunk >= config.ChunkCount || item.Slot < 0 || item.Slot >= LootField.SlotsPerChunk || IsFound(item.Chunk, item.Slot))
                return Pickup.Gone;
            if (FreeSlots(config) < Mathf.Max(1, config.ores[item.Ore].slots)) return Pickup.BagFull;
            if (item.Special >= 0) specials |= 1 << item.Special;
            else
            {
                if (found.Length <= item.Chunk) Array.Resize(ref found, item.Chunk + 1);
                found[item.Chunk] |= 1L << item.Slot;
            }
            if (ores.Length < config.ores.Length) Array.Resize(ref ores, config.ores.Length);
            ores[item.Ore]++;
            return Pickup.Collected;
        }

        /// <summary>Sells every ore piece; items stay. Returns the coins earned.</summary>
        public int Sell(MineConfig config)
        {
            int sold = BagValue(config);
            coins += sold;
            Array.Clear(ores, 0, ores.Length);
            return sold;
        }

        /// <summary>Fainting or calling the rescuers leaves the ore behind; items stay in the bag.</summary>
        public int DropOre()
        {
            int lost = OrePieces;
            Array.Clear(ores, 0, ores.Length);
            return lost;
        }

        // ---------- Upgrades and items ----------

        public int Level(Track track) => track == Track.Tool ? tool : track == Track.Backpack ? bagLevel : track == Track.Jetpack ? jetLevel : healthLevel;

        public int Levels(Track track, MineConfig config) =>
            track == Track.Tool ? config.tools.Length : track == Track.Backpack ? config.backpack.Length : track == Track.Jetpack ? config.jetpack.Length : config.health.Length;

        public int NextPrice(Track track, MineConfig config)
        {
            int next = Level(track) + 1;
            if (next >= Levels(track, config)) return -1;
            return track == Track.Tool ? config.tools[next].price : track == Track.Backpack ? config.backpack[next].price
                : track == Track.Jetpack ? config.jetpack[next].price : config.health[next].price;
        }

        public bool Upgrade(Track track, MineConfig config)
        {
            int price = NextPrice(track, config);
            if (price < 0 || coins < price) return false;
            coins -= price;
            switch (track)
            {
                case Track.Tool: tool++; break;
                case Track.Backpack: bagLevel++; break;
                case Track.Jetpack: jetLevel++; break;
                default:
                    float before = MaxHealth(config);
                    healthLevel++;
                    // The extra health arrives filled.
                    if (health >= 0) health += MaxHealth(config) - before;
                    break;
            }
            return true;
        }

        public bool BuyScanner(MineConfig config)
        {
            if (scanner || coins < config.scanner.price || FreeSlots(config) < config.scanner.slots) return false;
            coins -= config.scanner.price;
            scanner = true;
            return true;
        }

        public bool BuyMedkit(MineConfig config)
        {
            if (coins < config.medkit.price || FreeSlots(config) < config.medkit.slots) return false;
            coins -= config.medkit.price;
            medkits++;
            return true;
        }

        // ---------- Health ----------

        public float MaxHealth(MineConfig config) => config.health[healthLevel].value;
        public float Health(MineConfig config) => health < 0 ? MaxHealth(config) : Mathf.Min(health, MaxHealth(config));

        /// <summary>Lowers health; returns true when the player faints.</summary>
        public bool Hurt(float amount, MineConfig config)
        {
            health = Mathf.Max(0, Health(config) - Mathf.Max(0, amount));
            return health <= 0;
        }

        public void Heal(float amount, MineConfig config) => health = Mathf.Min(MaxHealth(config), Health(config) + Mathf.Max(0, amount));

        public bool UseMedkit(MineConfig config)
        {
            if (medkits <= 0 || Health(config) >= MaxHealth(config)) return false;
            medkits--;
            Heal(config.medkit.power, config);
            return true;
        }

        public float JetFuel(MineConfig config) => config.jetpack[jetLevel].value;
    }
}
