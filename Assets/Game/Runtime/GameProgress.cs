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

    public enum Track { Tool, Backpack, Jetpack, Health, Fuel }

    public enum Pickup { Collected, BagFull, Gone }

    /// <summary>
    /// Save schema v4. Every payout is guarded by a bit or a counter, so repeating a call
    /// (double tap, reload, returning from an ad) never pays twice. New fields need defaults.
    /// Ore rides in the backpack until it is sold in the house; items take backpack slots too.
    /// </summary>
    [Serializable]
    public sealed class GameProgress
    {
        public const int CurrentVersion = 4;
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
        /// <summary>Visited landmarks; absent in earlier v3 saves, which start with no discoveries.</summary>
        public int sites;
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
        public int medkits, dynamite;
        public int fuelLevel, keys;
        public float fuel = -1;
        public bool doorOpened, hasWeapon;
        public int worldWidth = 14;
        /// <summary>Found secrets, one bit each.</summary>
        public int secrets;
        /// <summary>Buyer's order: questOre &lt; 0 means none yet. questRoll changes when the player asks for another one.</summary>
        public int questNumber, questOre = -1, questAmount, questReward, questRoll;
        /// <summary>Rewarded ads paid so far; a reward is granted only for the next number, once.</summary>
        public int adRewards;
        /// <summary>Unix seconds when the next rewarded ad may be offered.</summary>
        public long adReadyAt;

        public static GameProgress New(MineConfig config) => new GameProgress { seed = config.seed, worldWidth = config.width, fuel = config.fuelTank[0].value };

        public bool IsValid(MineConfig config)
        {
            if (version != CurrentVersion || seed != config.seed || coins < 0 || expeditions < 0) return false;
            if (worldWidth != config.width || fuelLevel < 0 || fuelLevel >= config.fuelTank.Length || keys < 0 || keys > 31) return false;
            if (float.IsNaN(fuel) || float.IsInfinity(fuel) || fuel < -1 || fuel > FuelCapacity(config)) return false;
            if ((doorOpened || finished) && keys != 31) return false;
            if (tool < 0 || tool >= config.tools.Length || maxDepth < 0 || maxDepth > config.depth) return false;
            if (bagLevel < 0 || bagLevel >= config.backpack.Length || jetLevel < 0 || jetLevel >= config.jetpack.Length) return false;
            if (healthLevel < 0 || healthLevel >= config.health.Length || float.IsNaN(health) || health > MaxHealth(config) || medkits < 0 || dynamite < 0) return false;
            if (ores == null || ores.Length > config.ores.Length) return false;
            foreach (int count in ores) if (count < 0) return false;
            if (digCredit < 0 || digCredit >= 100 || found == null || found.Length > config.LootChunkCount || terrain == null) return false;
            if (collection < 0 || collection >= 1 << config.collection.Length || specials < 0) return false;
            if (sites < 0 || sites >= 1 << MineSites.All.Length) return false;
            if (secrets < 0 || secrets >= 1 << Secrets.All.Length || adRewards < 0 || questNumber < 0 || questRoll < 0) return false;
            if (questOre >= config.ores.Length || questOre >= 0 && (questAmount <= 0 || questReward <= 0)) return false;
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
        public bool HasSite(int index) => index >= 0 && index < MineSites.All.Length && (sites & 1 << index) != 0;
        public bool DiscoverSite(int index)
        {
            if (index < 0 || index >= MineSites.All.Length || HasSite(index)) return false;
            sites |= 1 << index;
            return true;
        }
        public int CollectionCount { get { int n = 0; for (int bits = collection; bits != 0; bits &= bits - 1) n++; return n; } }

        // ---------- Backpack ----------

        public int Capacity(MineConfig config) => Mathf.RoundToInt(config.backpack[bagLevel].value);
        public int OreCount(int ore) => ore < ores.Length ? ores[ore] : 0;

        public int UsedSlots(MineConfig config)
        {
            int used = (scanner ? config.scanner.slots : 0) + medkits * config.medkit.slots + dynamite * config.dynamite.slots;
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
            if (item.Kind == LootKind.Key)
            {
                if (item.Key < 0 || item.Key >= 5 || HasKey(item.Key)) return Pickup.Gone;
                keys |= 1 << item.Key;
                return Pickup.Collected;
            }
            if (item.Kind == LootKind.Secret)
            {
                if (item.Secret < 0 || item.Secret >= Secrets.All.Length || HasSecret(item.Secret)) return Pickup.Gone;
                secrets |= 1 << item.Secret;
                coins += Secrets.All[item.Secret].Reward;
                return Pickup.Collected;
            }
            if (item.Kind == LootKind.Collectible)
            {
                if (item.Collectible < 0 || item.Collectible >= config.collection.Length || HasCollectible(item.Collectible)) return Pickup.Gone;
                collection |= 1 << item.Collectible;
                coins += config.firstDiscoveryCoins;
                return Pickup.Collected;
            }
            if (item.Ore < 0 || item.Ore >= config.ores.Length) return Pickup.Gone;
            if (item.Special >= 0 ? item.Special > 30 || HasSpecial(item.Special)
                : item.Chunk < 0 || item.Chunk >= config.LootChunkCount || item.Slot < 0 || item.Slot >= LootField.SlotsPerChunk || IsFound(item.Chunk, item.Slot))
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

        /// <summary>What the buyer pays for the backpack now, with the statue bonus.</summary>
        public int SaleValue(MineConfig config)
        {
            int value = BagValue(config);
            return HasSecret(Secrets.Statue) ? Mathf.RoundToInt(value * (1 + Secrets.StatueBonus)) : value;
        }

        /// <summary>Sells every ore piece; items stay. Returns the coins earned.</summary>
        public int Sell(MineConfig config)
        {
            int sold = SaleValue(config);
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

        public int Level(Track track) => track == Track.Fuel ? fuelLevel : track == Track.Tool ? tool : track == Track.Backpack ? bagLevel : track == Track.Jetpack ? jetLevel : healthLevel;

        public int Levels(Track track, MineConfig config) =>
            track == Track.Fuel ? config.fuelTank.Length : track == Track.Tool ? config.tools.Length : track == Track.Backpack ? config.backpack.Length : track == Track.Jetpack ? config.jetpack.Length : config.health.Length;

        public int NextPrice(Track track, MineConfig config)
        {
            int next = Level(track) + 1;
            if (next >= Levels(track, config)) return -1;
            return track == Track.Fuel ? config.fuelTank[next].price : track == Track.Tool ? config.tools[next].price : track == Track.Backpack ? config.backpack[next].price
                : track == Track.Jetpack ? config.jetpack[next].price : config.health[next].price;
        }

        /// <summary>Buys the next level; <paramref name="free"/> is the test button in the workshop.</summary>
        public bool Upgrade(Track track, MineConfig config, bool free = false)
        {
            int price = NextPrice(track, config);
            if (price < 0 || !free && coins < price) return false;
            if (!free) coins -= price;
            switch (track)
            {
                case Track.Tool: tool++; break;
                case Track.Backpack: bagLevel++; break;
                case Track.Jetpack: jetLevel++; break;
                case Track.Fuel: fuelLevel++; break;
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

        public bool BuyDynamite(MineConfig config)
        {
            if (coins < config.dynamite.price || FreeSlots(config) < config.dynamite.slots) return false;
            coins -= config.dynamite.price;
            dynamite++;
            return true;
        }

        public bool UseDynamite()
        {
            if (dynamite <= 0) return false;
            dynamite--;
            return true;
        }

        /// <summary>
        /// Repairs values that older balance made valid: a smaller fuel tank leaves saved petrol above the new capacity.
        /// </summary>
        public void Normalize(MineConfig config)
        {
            if (fuelLevel >= 0 && fuelLevel < config.fuelTank.Length && fuel > FuelCapacity(config)) fuel = FuelCapacity(config);
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

        public float FuelCapacity(MineConfig config) => config.fuelTank[fuelLevel].value;
        public float Petrol(MineConfig config) => fuel < 0 ? FuelCapacity(config) : fuel;
        public float JetConsumption(MineConfig config) => config.jetpack[jetLevel].value;
        public bool UseFuel(float amount, MineConfig config)
        {
            if (amount <= 0 || Petrol(config) + .0001f < amount) return false;
            fuel = Mathf.Max(0, Petrol(config) - amount);
            return true;
        }
        public void Refill(float amount, MineConfig config) => fuel = Mathf.Min(FuelCapacity(config), Petrol(config) + Mathf.Max(0, amount));
        public bool HasKey(int index) => index >= 0 && index < 5 && (keys & 1 << index) != 0;
        public int KeyCount { get { int n = 0; for (int bits = keys; bits != 0; bits &= bits - 1) n++; return n; } }
        public bool OpenDoor() { if (keys != 31) return false; doorOpened = true; return true; }
        public bool HasSecret(int index) => index >= 0 && index < Secrets.All.Length && (secrets & 1 << index) != 0;

        // ---------- Buyer's orders ----------

        public bool HasQuest => questOre >= 0;

        /// <summary>
        /// Makes sure an order is waiting. Orders ask for ore from zones the player has reached, mostly the deepest one,
        /// grow a little with every delivery and always fit in three quarters of the backpack.
        /// </summary>
        public void EnsureQuest(MineConfig config)
        {
            if (HasQuest) return;
            int reached = config.ZoneIndex(maxDepth);
            uint hash = config.Hash(questNumber, questRoll, 17, 613);
            // Two times in three the order comes from the deepest zone reached, otherwise from any earlier one.
            int zoneIndex = reached > 0 && hash % 3 == 0 ? (int)(hash / 3 % (uint)reached) : reached;
            var zone = config.zones[zoneIndex];
            int ore = config.PickOre(zone, hash / 7);
            int weight = 0, top = 1;
            foreach (var chance in zone.ores)
            {
                top = Mathf.Max(top, chance.weight);
                if (chance.ore == ore) weight = Mathf.Max(weight, chance.weight);
            }
            float rarity = Mathf.Sqrt(Mathf.Max(1, weight) / (float)top);
            int amount = Mathf.RoundToInt((3 + Mathf.Min(questNumber, 12) * 0.5f) * rarity);
            int fits = Mathf.Max(2, Capacity(config) * 3 / 4 / Mathf.Max(1, config.ores[ore].slots));
            questOre = ore;
            questAmount = Mathf.Clamp(amount, 2, fits);
            float bonus = 1.8f + 0.08f * Mathf.Min(questNumber, 12);
            questReward = Mathf.Max(5, Mathf.RoundToInt(questAmount * config.ores[ore].value * bonus / 5f) * 5);
        }

        public bool CanDeliver => HasQuest && OreCount(questOre) >= questAmount;

        /// <summary>Hands the ordered ore to the buyer. Returns the coins paid, or 0 when the order is not ready.</summary>
        public int DeliverQuest(MineConfig config)
        {
            if (!CanDeliver) return 0;
            ores[questOre] -= questAmount;
            int paid = questReward;
            coins += paid;
            questNumber++;
            questOre = -1;
            EnsureQuest(config);
            return paid;
        }

        /// <summary>Asks the buyer for another order of the same size class.</summary>
        public void SkipQuest(MineConfig config)
        {
            questRoll++;
            questOre = -1;
            EnsureQuest(config);
        }

        // ---------- Rewarded ads ----------

        public const int AdCooldown = 90;

        public int UpgradeSteps => tool + bagLevel + jetLevel + fuelLevel + healthLevel;

        /// <summary>Coins for one watched ad: grows with the upgrades bought, so it stays worth a look late in the game.</summary>
        public int AdReward(MineConfig config) => Mathf.RoundToInt(30 * Mathf.Pow(1.13f, UpgradeSteps) / 5f) * 5;

        public bool AdReady(long now) => now >= adReadyAt;

        /// <summary>
        /// Pays for the ad numbered <paramref name="ticket"/>. Only the next number pays, so a repeated or late
        /// callback from the platform never pays twice. Returns the coins granted.
        /// </summary>
        public int GrantAd(int ticket, long now, MineConfig config)
        {
            if (ticket != adRewards + 1) return 0;
            int reward = AdReward(config);
            adRewards = ticket;
            coins += reward;
            adReadyAt = now + AdCooldown;
            return reward;
        }
    }
}
