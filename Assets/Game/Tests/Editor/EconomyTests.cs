using NUnit.Framework;
using UnityEngine;

namespace Nubik.Tests
{
    public class EconomyTests
    {
        private MineConfig config;
        private GameProgress progress;
        private VoxelTerrain terrain;
        private LootField loot;

        [SetUp]
        public void Setup()
        {
            config = ScriptableObject.CreateInstance<MineConfig>();
            progress = GameProgress.New(config);
            terrain = new VoxelTerrain(config);
            loot = new LootField(terrain);
        }

        [TearDown] public void Cleanup() => Object.DestroyImmediate(config);

        private LootItem FirstFind => loot.Items.Find(item => item.Special == 0);
        private LootItem Ore(int skip = 0) => loot.Items.FindAll(item => item.Special < 0 && item.Kind == LootKind.Find)[skip];

        [Test] public void OreCannotBeTakenTwice()
        {
            var ore = Ore();
            Assert.AreEqual(Pickup.Collected, progress.Collect(ore, config));
            Assert.AreEqual(Pickup.Gone, progress.Collect(ore, config));
            Assert.AreEqual(1, progress.OreCount(ore.Ore));
            Assert.IsTrue(progress.IsFound(ore.Chunk, ore.Slot));
        }

        [Test] public void FullBagLeavesOreInTheWall()
        {
            int capacity = progress.Capacity(config);
            for (int i = 0; i < capacity; i++) Assert.AreEqual(Pickup.Collected, progress.Collect(Ore(i), config));
            var extra = Ore(capacity);
            Assert.AreEqual(Pickup.BagFull, progress.Collect(extra, config));
            Assert.IsFalse(progress.IsFound(extra.Chunk, extra.Slot), "Ore left behind can still be picked up later.");
            progress.Sell(config);
            Assert.AreEqual(Pickup.Collected, progress.Collect(extra, config));
        }

        [Test] public void ChestTakesTwoSlots()
        {
            var chest = loot.Items.Find(item => item.Kind == LootKind.Chest);
            Assert.AreEqual(2, chest.Slots);
            Assert.AreEqual(Pickup.Collected, progress.Collect(chest, config));
            Assert.AreEqual(2, progress.UsedSlots(config));
            Assert.AreEqual(config.ores[chest.Ore].value, progress.BagValue(config));
        }

        [Test] public void SaleCannotPayTwiceAndKeepsItems()
        {
            progress.coins = config.medkit.price;
            Assert.IsTrue(progress.BuyMedkit(config));
            progress.Collect(FirstFind, config);
            int value = progress.BagValue(config);
            Assert.Greater(value, 0);
            Assert.AreEqual(value, progress.Sell(config));
            Assert.AreEqual(0, progress.Sell(config));
            Assert.AreEqual(value, progress.coins);
            Assert.AreEqual(1, progress.medkits);
        }

        [Test] public void FaintingDropsOreButNotItems()
        {
            progress.scanner = true;
            progress.Collect(Ore(), config);
            progress.Collect(Ore(1), config);
            Assert.AreEqual(2, progress.DropOre());
            Assert.AreEqual(0, progress.BagValue(config));
            Assert.IsTrue(progress.scanner);
        }

        [TestCase(Track.Tool)]
        [TestCase(Track.Backpack)]
        [TestCase(Track.Jetpack)]
        [TestCase(Track.Health)]
        public void UpgradeRequiresFundsAndChargesExactlyOnce(Track track)
        {
            int price = progress.NextPrice(track, config);
            Assert.Greater(price, 0);
            progress.coins = price - 1;
            Assert.IsFalse(progress.Upgrade(track, config));
            progress.coins = price;
            Assert.IsTrue(progress.Upgrade(track, config));
            Assert.AreEqual(0, progress.coins);
            Assert.AreEqual(1, progress.Level(track));
        }

        [Test] public void EveryTrackStopsAtItsLastLevel()
        {
            foreach (var track in new[] { Track.Tool, Track.Backpack, Track.Jetpack, Track.Health })
            {
                progress.coins = 100000;
                while (progress.Upgrade(track, config)) { }
                Assert.AreEqual(progress.Levels(track, config) - 1, progress.Level(track));
                Assert.AreEqual(-1, progress.NextPrice(track, config));
            }
            Assert.AreEqual(8, config.tools.Length);
        }

        [Test] public void ItemsNeedFreeSlots()
        {
            progress.coins = 10000;
            int capacity = progress.Capacity(config);
            for (int i = 0; i < capacity - 1; i++) Assert.IsTrue(progress.BuyMedkit(config));
            Assert.IsFalse(progress.BuyScanner(config), "The scanner takes two slots; one is free.");
            Assert.IsTrue(progress.BuyMedkit(config));
            Assert.IsFalse(progress.BuyMedkit(config));
            Assert.AreEqual(capacity, progress.UsedSlots(config));
        }

        [Test] public void ScannerIsBoughtOnce()
        {
            progress.coins = config.scanner.price * 2;
            Assert.IsTrue(progress.BuyScanner(config));
            Assert.IsFalse(progress.BuyScanner(config));
            Assert.AreEqual(config.scanner.price, progress.coins);
            Assert.AreEqual(config.scanner.slots, progress.UsedSlots(config));
        }

        [Test] public void HealthHurtsHealsAndFaints()
        {
            float max = progress.MaxHealth(config);
            Assert.AreEqual(max, progress.Health(config));
            Assert.IsFalse(progress.Hurt(max * 0.6f, config));
            progress.medkits = 1;
            Assert.IsTrue(progress.UseMedkit(config));
            Assert.AreEqual(Mathf.Min(max, max * 0.4f + config.medkit.power), progress.Health(config), 0.01f);
            Assert.IsFalse(progress.UseMedkit(config), "No medkits left.");
            Assert.IsTrue(progress.Hurt(max * 5, config));
            Assert.AreEqual(0, progress.Health(config));
        }

        [Test] public void HealthUpgradeArrivesFilled()
        {
            progress.Hurt(10, config);
            progress.coins = progress.NextPrice(Track.Health, config);
            progress.Upgrade(Track.Health, config);
            Assert.AreEqual(progress.MaxHealth(config) - 10, progress.Health(config), 0.01f);
        }

        [Test] public void CollectionRewardIsPermanentAndPaidOnce()
        {
            var helmet = loot.Items.Find(item => item.Collectible == 0);
            Assert.AreEqual(Pickup.Collected, progress.Collect(helmet, config));
            Assert.AreEqual(config.firstDiscoveryCoins, progress.coins);
            Assert.AreEqual(Pickup.Gone, progress.Collect(helmet, config));
            Assert.AreEqual(config.firstDiscoveryCoins, progress.coins);
            Assert.AreEqual(0, progress.UsedSlots(config), "Collection items take no backpack space.");
            Assert.IsTrue(progress.HasCollectible(0));
        }

        [Test] public void SaveRoundTripPreservesTransactionsAndTerrain()
        {
            progress.Collect(FirstFind, config);
            progress.Collect(loot.Items.Find(item => item.Collectible == 0), config);
            progress.scanner = true;
            progress.jetLevel = 2;
            progress.Hurt(12, config);
            terrain.Dig(new Vector3(0, -0.2f, -2), 0.9f, 4);
            foreach (int chunk in terrain.EditedChunks()) progress.terrain.Add(new ChunkSave { chunk = chunk, data = terrain.Encode(chunk) });
            var loaded = ProgressStore.Decode(ProgressStore.Encode(progress), config);
            Assert.NotNull(loaded);
            Assert.AreEqual(progress.coins, loaded.coins);
            Assert.AreEqual(progress.BagValue(config), loaded.BagValue(config));
            Assert.IsTrue(loaded.scanner);
            Assert.AreEqual(2, loaded.jetLevel);
            Assert.AreEqual(progress.Health(config), loaded.Health(config), 0.01f);
            Assert.IsTrue(loaded.HasCollectible(0));
            Assert.AreEqual(Pickup.Gone, loaded.Collect(FirstFind, config));
            var restored = new VoxelTerrain(config);
            foreach (var entry in loaded.terrain) Assert.IsTrue(restored.Decode(entry.chunk, entry.data));
            Assert.AreEqual(terrain.Sample(new Vector3(0, -0.4f, -2)), restored.Sample(new Vector3(0, -0.4f, -2)));
        }

        [TestCase("")]
        [TestCase("{}")]
        [TestCase("not json")]
        [TestCase("{\"version\":99,\"seed\":28092026}")]
        [TestCase("{\"version\":3,\"seed\":1}")]
        [TestCase("{\"version\":3,\"seed\":28092026,\"bagLevel\":42}")]
        public void CorruptOrFutureSaveIsRejected(string json) => Assert.IsNull(ProgressStore.Decode(json, config));

        [Test] public void GridPrototypeSaveMigrates()
        {
            const string v1 = "{\"version\":1,\"seed\":28092026,\"coins\":75,\"backpack\":12,\"pickTier\":2,\"maxDepth\":9,\"expeditions\":2,\"foundHelmet\":true,\"destroyed\":[2,7,17]}";
            var migrated = ProgressStore.Decode(v1, config);
            Assert.NotNull(migrated);
            Assert.AreEqual(GameProgress.CurrentVersion, migrated.version);
            Assert.AreEqual(87, migrated.coins, "The old backpack total is paid out.");
            Assert.AreEqual(3, migrated.tool, "Old steel maps to the steel level.");
            Assert.AreEqual(2, migrated.expeditions);
            Assert.AreEqual(0, migrated.maxDepth);
            Assert.IsTrue(migrated.HasCollectible(0));
            Assert.AreEqual(Pickup.Gone, migrated.Collect(loot.Items.Find(item => item.Collectible == 0), config));
        }

        [Test] public void FirstPersonV2SaveMigrates()
        {
            const string v2 = "{\"version\":2,\"seed\":28092026,\"coins\":40,\"backpack\":31,\"tool\":3,\"maxDepth\":44,\"expeditions\":5,\"collection\":3,\"specials\":1}";
            var migrated = ProgressStore.Decode(v2, config);
            Assert.NotNull(migrated);
            Assert.AreEqual(GameProgress.CurrentVersion, migrated.version);
            Assert.AreEqual(71, migrated.coins);
            Assert.AreEqual(6, migrated.tool, "Old crystal maps to the crystal level.");
            Assert.AreEqual(44, migrated.maxDepth);
            Assert.AreEqual(2, migrated.CollectionCount);
            Assert.AreEqual(migrated.MaxHealth(config), migrated.Health(config));
            Assert.AreEqual(0, migrated.UsedSlots(config));
        }

        [Test] public void ForeignTerrainChunkIsRejected()
        {
            progress.terrain.Add(new ChunkSave { chunk = config.ChunkCount, data = "AAAA" });
            Assert.IsFalse(progress.IsValid(config));
        }

        [Test] public void EveryCollectibleIsPlacedInsideTheYardAtItsDepth()
        {
            float edge = config.width / 2f;
            for (int i = 0; i < config.collection.Length; i++)
            {
                var item = loot.Items.Find(candidate => candidate.Collectible == i);
                Assert.NotNull(item);
                Assert.Less(Mathf.Abs(item.Position.x), edge - 0.5f);
                Assert.Less(Mathf.Abs(item.Position.z), edge - 0.5f);
                Assert.AreEqual(config.collection[i].depth, -item.Position.y, 1f);
            }
        }

        [Test] public void FirstFindIsVisibleFromTheStart()
        {
            Assert.IsTrue(loot.IsExposed(FirstFind));
            Assert.Less(Vector3.Distance(FirstFind.Position, Yard.FirstSpawn), config.reach + 1.6f);
        }

        [Test] public void OreMatchesItsZone()
        {
            float edge = config.width / 2f;
            Assert.Greater(loot.Items.Count, 200);
            foreach (var item in loot.Items)
            {
                Assert.Less(Mathf.Abs(item.Position.x), edge);
                Assert.Less(Mathf.Abs(item.Position.z), edge);
                Assert.Greater(item.Position.y, config.FloorY);
                if (item.Kind != LootKind.Find || item.Special >= 0) continue;
                var zone = config.Zone(-item.Position.y);
                Assert.IsTrue(System.Array.Exists(zone.ores, chance => chance.ore == item.Ore), "Ore " + item.Ore + " does not belong to " + zone.nameEn);
                Assert.AreEqual(config.ores[item.Ore].value, item.Value);
                Assert.Less(item.Slot, LootField.SlotsPerChunk);
            }
        }

        [Test] public void DeeperOreIsWorthMore()
        {
            float Average(int zone)
            {
                int weight = 0, value = 0;
                foreach (var chance in config.zones[zone].ores) { weight += chance.weight; value += chance.weight * config.ores[chance.ore].value; }
                return value / (float)weight;
            }
            for (int zone = 1; zone < config.zones.Length; zone++) Assert.Greater(Average(zone), Average(zone - 1) * 2);
        }

        [Test] public void FirstTripPaysForTheFirstShovel()
        {
            // A starting backpack full of average first-zone ore buys the copper shovel.
            float weight = 0, value = 0;
            foreach (var chance in config.zones[0].ores) { weight += chance.weight; value += chance.weight * config.ores[chance.ore].value; }
            Assert.GreaterOrEqual(value / weight * config.backpack[0].value, config.tools[1].price);
        }

        [Test] public void LootGenerationIsDeterministic()
        {
            var again = new LootField(new VoxelTerrain(config));
            Assert.AreEqual(loot.Items.Count, again.Items.Count);
            for (int i = 0; i < loot.Items.Count; i += 37)
            {
                Assert.AreEqual(loot.Items[i].Position, again.Items[i].Position);
                Assert.AreEqual(loot.Items[i].Ore, again.Items[i].Ore);
            }
        }
    }
}
