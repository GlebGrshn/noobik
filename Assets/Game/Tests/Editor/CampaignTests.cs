using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Nubik.Tests
{
    public class CampaignTests
    {
        private MineConfig config;
        [SetUp] public void Setup() => config = ScriptableObject.CreateInstance<MineConfig>();
        [TearDown] public void Cleanup() => Object.DestroyImmediate(config);

        [Test] public void PetrolIsSharedSavedAndTankUpgradeIsIndependentOfJetEfficiency()
        {
            var p = GameProgress.New(config);
            float full = p.FuelCapacity(config);
            Assert.Greater(p.JetConsumption(config), 0);
            Assert.IsTrue(p.UseFuel(p.JetConsumption(config) * 2, config));
            Assert.IsTrue(p.UseFuel(config.drillFuelPerHit, config));
            Assert.AreEqual(full - p.JetConsumption(config) * 2 - config.drillFuelPerHit, p.Petrol(config), .001f);
            var copy = ProgressStore.Decode(ProgressStore.Encode(p), config);
            Assert.NotNull(copy); Assert.AreEqual(p.fuel, copy.fuel);
            p.coins = 10000; p.Upgrade(Track.Jetpack, config);
            Assert.Less(p.JetConsumption(config), config.jetpack[0].value);
            Assert.AreEqual(full, p.FuelCapacity(config));
            p.Upgrade(Track.Fuel, config); Assert.Greater(p.FuelCapacity(config), full);
            p.fuel = 0; Assert.IsFalse(p.UseFuel(config.drillFuelPerHit, config));
            p.Refill(9999, config); Assert.AreEqual(p.FuelCapacity(config), p.fuel);
        }

        [Test] public void KeysIgnoreBackpackCapacityAndDoorRequiresAllFive()
        {
            var p = GameProgress.New(config); p.medkits = p.Capacity(config);
            var terrain = new VoxelTerrain(config); MineSites.Carve(terrain); Expedition.Prepare(terrain);
            var field = new LootField(terrain);
            for (int i = 0; i < 5; i++)
            {
                Assert.IsFalse(p.OpenDoor());
                var key = field.Items.Find(x => x.Key == i);
                Assert.NotNull(key); Assert.IsTrue(field.IsExposed(key));
                Assert.AreEqual(Pickup.Collected, p.Collect(key, config));
                Assert.AreEqual(Pickup.Gone, p.Collect(key, config));
            }
            Assert.AreEqual(5, p.KeyCount); Assert.IsTrue(p.OpenDoor()); Assert.IsFalse(p.finished);
            Assert.AreEqual(p.Capacity(config), p.UsedSlots(config)); Assert.AreEqual(0, p.coins);
            p.DropOre(); p.Hurt(1000, config); Assert.AreEqual(5, p.KeyCount);
            var copy = ProgressStore.Decode(ProgressStore.Encode(p), config);
            Assert.IsTrue(copy.doorOpened); Assert.AreEqual(31, copy.keys);
        }

        [Test] public void OreIsThinnedWithoutMovingSurvivingFinds()
        {
            config.oreDensity = 1;
            var dense = new LootField(new VoxelTerrain(config)).Items.Where(x => x.Kind == LootKind.Find && x.Special < 0).ToDictionary(x => x.Chunk * 64 + x.Slot);
            config.oreDensity = 2f / 3f;
            var sparse = new LootField(new VoxelTerrain(config)).Items.Where(x => x.Kind == LootKind.Find && x.Special < 0).ToArray();
            Assert.That(sparse.Length / (float)dense.Count, Is.InRange(.61f, .70f));
            foreach (var ore in sparse)
            {
                var original = dense[ore.Chunk * 64 + ore.Slot];
                Assert.AreEqual(original.Position, ore.Position); Assert.AreEqual(original.Ore, ore.Ore);
            }
        }

        [Test] public void V3MigrationPreservesWorldExcavationPurchasesAndCollectedOre()
        {
            var oldConfig = Object.Instantiate(config); oldConfig.width = 12;
            try
            {
                var old = new VoxelTerrain(oldConfig);
                var at = new Vector3(-3, -10, 2);
                old.Dig(at, 1.4f, 999);
                var p = GameProgress.New(oldConfig); p.version = 3; p.finished = true;
                p.coins = 654; p.tool = 6; p.bagLevel = 2; p.jetLevel = 2; p.collection = 3; p.specials = 1 << 16; p.sites = 1;
                p.found = new long[oldConfig.ChunkCount]; p.found[4] = 1L << 7;
                foreach (int chunk in old.EditedChunks()) p.terrain.Add(new ChunkSave { chunk = chunk, data = old.Encode(chunk) });
                var migrated = ProgressStore.Decode(ProgressStore.Encode(p), config);
                Assert.NotNull(migrated); Assert.AreEqual(14, migrated.worldWidth); Assert.AreEqual(654, migrated.coins);
                Assert.AreEqual(6, migrated.tool); Assert.AreEqual(2, migrated.jetLevel); Assert.AreEqual(2, migrated.bagLevel);
                Assert.AreEqual(3, migrated.collection); Assert.IsTrue(migrated.HasSpecial(16)); Assert.IsTrue(migrated.HasSite(0));
                Assert.IsTrue(migrated.IsFound(21, 7)); Assert.IsFalse(migrated.finished);
                var restored = new VoxelTerrain(config);
                foreach (var entry in migrated.terrain) Assert.IsTrue(restored.Decode(entry.chunk, entry.data));
                Assert.IsTrue(restored.IsAir(at));
                Assert.IsTrue(restored.IsAir(at + new Vector3(.5f, 0, 0)));
            }
            finally { Object.DestroyImmediate(oldConfig); }
        }

        [Test] public void BossWarnsBeforeDamageAllowsDodgingAndRewardsWeakPointTiming()
        {
            var boss = new BossBattle(); Assert.AreEqual(0, boss.Shoot(true)); boss.Begin();
            Assert.AreEqual(48, boss.Shoot(true));
            Assert.AreEqual(0, boss.Tick(2.1f, new Vector3(0, 0, -8)));
            Assert.AreEqual(BattlePhase.Warning, boss.Phase); Assert.AreEqual(18, boss.Shoot(true));
            Assert.AreEqual(0, boss.Tick(.5f, new Vector3(0, 0, -8)), "No invisible immediate attack.");
            Assert.AreEqual(0, boss.Tick(2, new Vector3(5, 0, -8)), "Leaving the marker dodges the tentacle.");
            boss.Tick(2, new Vector3(-5, 0, -8)); Assert.AreEqual(1, boss.Pattern);
            Assert.Greater(boss.Tick(2, new Vector3(-5, 0, -8)), 0);
            boss.Tick(2, new Vector3(5, 0, -8)); Assert.AreEqual(2, boss.Pattern);
            Assert.AreEqual(0, boss.Tick(2, new Vector3(5, 2, -8)), "Jump or jetpack clears the ground wave.");
            while (boss.Health > 0) boss.Shoot(true);
            Assert.AreEqual(BattlePhase.Won, boss.Phase); Assert.AreEqual(0, boss.Tick(50, Vector3.zero));
            Assert.AreEqual(0, boss.Shoot(true));
        }
    }
}
