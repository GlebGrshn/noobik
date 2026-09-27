using System;
using System.Collections.Generic;

namespace Nubik
{
    [Serializable]
    public sealed class GameProgress
    {
        public int version = 1;
        public int seed;
        public int coins;
        public int backpack;
        public int pickTier;
        public int maxDepth;
        public int expeditions;
        public bool foundHelmet;
        public List<int> destroyed = new List<int>();

        public bool IsValid(MineConfig config)
        {
            return version == 1 && seed == config.seed && coins >= 0 && backpack >= 0 &&
                pickTier >= 0 && pickTier < config.pickDamage.Length && maxDepth >= 0 && maxDepth <= config.rows &&
                destroyed != null && destroyed.Count <= config.BlockCount &&
                destroyed.TrueForAll(id => id >= 0 && id < config.BlockCount) &&
                new HashSet<int>(destroyed).Count == destroyed.Count;
        }

        public bool BreakBlock(int id, MineConfig config)
        {
            if (id < 0 || id >= config.BlockCount || destroyed.Contains(id)) return false;
            destroyed.Add(id);
            coins += config.Coins(id);
            backpack += config.Loot(id);
            if (config.IsCollection(id) && !foundHelmet)
            {
                foundHelmet = true;
                coins += 30;
            }
            return true;
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
            int next = pickTier + 1;
            if (next >= config.pickPrices.Length || coins < config.pickPrices[next]) return false;
            coins -= config.pickPrices[next];
            pickTier = next;
            return true;
        }
    }
}
