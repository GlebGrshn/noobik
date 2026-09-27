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

    /// <summary>
    /// Save schema v2. Every payout is guarded by a bit or a counter, so repeating a call
    /// (double tap, reload, returning from an ad) never pays twice. New fields need defaults.
    /// </summary>
    [Serializable]
    public sealed class GameProgress
    {
        public const int CurrentVersion = 2;
        public int version = CurrentVersion;
        public int seed;
        public int coins;
        public int backpack;
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
        /// <summary>Last underground spot, for the descend button.</summary>
        public bool hasDive;
        public Vector3 dive;
        public float diveYaw;
        public long[] found = new long[0];
        public List<ChunkSave> terrain = new List<ChunkSave>();

        public static GameProgress New(MineConfig config) => new GameProgress { seed = config.seed };

        public bool IsValid(MineConfig config)
        {
            if (version != CurrentVersion || seed != config.seed || coins < 0 || backpack < 0 || expeditions < 0) return false;
            if (tool < 0 || tool >= config.tools.Length || maxDepth < 0 || maxDepth > config.depth) return false;
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

        /// <summary>Adds dirt value in hundredths and returns whole coins paid.</summary>
        public int AddDigValue(int hundredths)
        {
            if (hundredths <= 0) return 0;
            digCredit += hundredths;
            int paid = digCredit / 100;
            digCredit %= 100;
            coins += paid;
            return paid;
        }

        /// <summary>Puts a find or a chest into the backpack once.</summary>
        public bool Collect(LootItem item, MineConfig config)
        {
            switch (item.Kind)
            {
                case LootKind.Collectible:
                    if (item.Collectible < 0 || item.Collectible >= config.collection.Length || HasCollectible(item.Collectible)) return false;
                    collection |= 1 << item.Collectible;
                    coins += config.firstDiscoveryCoins;
                    return true;
                default:
                    if (item.Special >= 0)
                    {
                        if (item.Special > 30 || HasSpecial(item.Special)) return false;
                        specials |= 1 << item.Special;
                    }
                    else
                    {
                        if (item.Chunk < 0 || item.Chunk >= config.ChunkCount || item.Slot < 0 || item.Slot >= LootField.SlotsPerChunk || IsFound(item.Chunk, item.Slot)) return false;
                        if (found.Length <= item.Chunk) Array.Resize(ref found, item.Chunk + 1);
                        found[item.Chunk] |= 1L << item.Slot;
                    }
                    backpack += item.Value;
                    return true;
            }
        }

        public int Sell()
        {
            int sold = backpack;
            coins += sold;
            backpack = 0;
            return sold;
        }

        public bool Upgrade(MineConfig config)
        {
            int next = tool + 1;
            if (next >= config.tools.Length || coins < config.tools[next].price) return false;
            coins -= config.tools[next].price;
            tool = next;
            return true;
        }
    }
}
