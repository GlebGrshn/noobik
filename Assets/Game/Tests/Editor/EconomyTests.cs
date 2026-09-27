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

        [Test] public void FindCannotPayTwice()
        {
            var find = loot.Items.Find(item => item.Special < 0 && item.Kind == LootKind.Find);
            Assert.IsTrue(progress.Collect(find, config));
            int backpack = progress.backpack;
            Assert.IsFalse(progress.Collect(find, config));
            Assert.AreEqual(backpack, progress.backpack);
            Assert.IsTrue(progress.IsFound(find.Chunk, find.Slot));
        }

        [Test] public void SpecialCannotPayTwice()
        {
            Assert.IsTrue(progress.Collect(FirstFind, config));
            Assert.IsFalse(progress.Collect(FirstFind, config));
            Assert.AreEqual(FirstFind.Value, progress.backpack);
        }

        [Test] public void SaleCannotPayTwice()
        {
            progress.Collect(FirstFind, config);
            int loot = progress.backpack;
            Assert.Greater(loot, 0);
            Assert.AreEqual(loot, progress.Sell());
            Assert.AreEqual(0, progress.Sell());
            Assert.AreEqual(loot, progress.coins);
        }

        [Test] public void UpgradeRequiresFundsAndChargesExactlyOnce()
        {
            Assert.IsFalse(progress.Upgrade(config));
            progress.coins = config.tools[1].price;
            Assert.IsTrue(progress.Upgrade(config));
            Assert.AreEqual(0, progress.coins);
            Assert.AreEqual(1, progress.tool);
            Assert.IsFalse(progress.Upgrade(config));
        }

        [Test] public void MaximumTierDoesNotCharge()
        {
            progress.tool = config.tools.Length - 1;
            progress.coins = 5000;
            Assert.IsFalse(progress.Upgrade(config));
            Assert.AreEqual(5000, progress.coins);
        }

        [Test] public void CollectionRewardIsPermanentAndPaidOnce()
        {
            var helmet = loot.Items.Find(item => item.Collectible == 0);
            Assert.IsTrue(progress.Collect(helmet, config));
            Assert.AreEqual(config.firstDiscoveryCoins, progress.coins);
            Assert.IsFalse(progress.Collect(helmet, config));
            Assert.AreEqual(config.firstDiscoveryCoins, progress.coins);
            progress.Sell();
            Assert.IsTrue(progress.HasCollectible(0));
        }

        [Test] public void DigCreditPaysWholeCoinsOnly()
        {
            Assert.AreEqual(0, progress.AddDigValue(60));
            Assert.AreEqual(1, progress.AddDigValue(60));
            Assert.AreEqual(20, progress.digCredit);
            Assert.AreEqual(1, progress.coins);
        }

        [Test] public void SaveRoundTripPreservesTransactionsAndTerrain()
        {
            progress.Collect(FirstFind, config);
            progress.Collect(loot.Items.Find(item => item.Collectible == 0), config);
            progress.Sell();
            terrain.Dig(new Vector3(0, -0.2f, -2), 0.9f, 4);
            foreach (int chunk in terrain.EditedChunks()) progress.terrain.Add(new ChunkSave { chunk = chunk, data = terrain.Encode(chunk) });
            var loaded = ProgressStore.Decode(ProgressStore.Encode(progress), config);
            Assert.NotNull(loaded);
            Assert.AreEqual(progress.coins, loaded.coins);
            Assert.IsTrue(loaded.HasCollectible(0));
            Assert.IsFalse(loaded.Collect(FirstFind, config));
            var restored = new VoxelTerrain(config);
            foreach (var entry in loaded.terrain) Assert.IsTrue(restored.Decode(entry.chunk, entry.data));
            Assert.AreEqual(terrain.Sample(new Vector3(0, -0.4f, -2)), restored.Sample(new Vector3(0, -0.4f, -2)));
        }

        [TestCase("")]
        [TestCase("{}")]
        [TestCase("not json")]
        [TestCase("{\"version\":99,\"seed\":28092026}")]
        [TestCase("{\"version\":2,\"seed\":1}")]
        public void CorruptOrFutureSaveIsRejected(string json) => Assert.IsNull(ProgressStore.Decode(json, config));

        [Test] public void GridPrototypeSaveMigrates()
        {
            const string v1 = "{\"version\":1,\"seed\":28092026,\"coins\":75,\"backpack\":12,\"pickTier\":1,\"maxDepth\":9,\"expeditions\":2,\"foundHelmet\":true,\"destroyed\":[2,7,17]}";
            var migrated = ProgressStore.Decode(v1, config);
            Assert.NotNull(migrated);
            Assert.AreEqual(GameProgress.CurrentVersion, migrated.version);
            Assert.AreEqual(75, migrated.coins);
            Assert.AreEqual(12, migrated.backpack);
            Assert.AreEqual(1, migrated.tool);
            Assert.AreEqual(2, migrated.expeditions);
            Assert.AreEqual(0, migrated.maxDepth);
            Assert.IsTrue(migrated.HasCollectible(0));
            Assert.IsFalse(migrated.Collect(loot.Items.Find(item => item.Collectible == 0), config));
            Assert.AreEqual(75, migrated.coins);
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

        [Test] public void GeneratedFindsStayInsideTheYardAndInRange()
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
                Assert.That(item.Value, Is.InRange(zone.findMin, zone.findMax));
                Assert.Less(item.Slot, LootField.SlotsPerChunk);
            }
        }

        [Test] public void LootGenerationIsDeterministic()
        {
            var again = new LootField(new VoxelTerrain(config));
            Assert.AreEqual(loot.Items.Count, again.Items.Count);
            for (int i = 0; i < loot.Items.Count; i += 37)
            {
                Assert.AreEqual(loot.Items[i].Position, again.Items[i].Position);
                Assert.AreEqual(loot.Items[i].Value, again.Items[i].Value);
            }
        }
    }
}
