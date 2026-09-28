using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Nubik.Tests
{
    public class ExplorationTests
    {
        private MineConfig config;
        [SetUp] public void Setup() => config = ScriptableObject.CreateInstance<MineConfig>();
        [TearDown] public void Cleanup() => Object.DestroyImmediate(config);

        [Test] public void RoomsPreserveOldExcavationAndDoNotGrowOnReload()
        {
            var old = new VoxelTerrain(config);
            old.Dig(new Vector3(3, -35, -3), 1.5f, 100);
            var terrain = new VoxelTerrain(config);
            foreach (int chunk in old.EditedChunks()) Assert.IsTrue(terrain.Decode(chunk, old.Encode(chunk)));
            MineSites.Carve(terrain);
            Assert.IsTrue(terrain.IsAir(new Vector3(3, -35, -3)));
            foreach (var site in MineSites.All)
            {
                Assert.IsTrue(terrain.IsAir(site.Origin + new Vector3(0, 1.55f, 1)));
                Assert.IsTrue(terrain.IsAir(site.Cache));
                Assert.IsFalse(terrain.IsAir(site.Origin + new Vector3(config.width / 2, 1, 0)));
            }
            var saved = new Dictionary<int, string>();
            foreach (int chunk in terrain.EditedChunks()) saved.Add(chunk, terrain.Encode(chunk));
            var reload = new VoxelTerrain(config);
            foreach (var entry in saved) Assert.IsTrue(reload.Decode(entry.Key, entry.Value));
            reload.ClearDirty();
            MineSites.Carve(reload);
            Assert.AreEqual(0, reload.DirtyChunks.Count, "Reload must not erode room edges again.");
            foreach (var entry in saved) Assert.AreEqual(entry.Value, reload.Encode(entry.Key));
        }

        [Test] public void SiteCachesRespectCapacityAndCannotBeCollectedTwice()
        {
            var terrain = new VoxelTerrain(config);
            MineSites.Carve(terrain);
            var loot = new LootField(terrain);
            var progress = GameProgress.New(config);
            progress.medkits = progress.Capacity(config) - 1;
            foreach (var site in MineSites.All)
            {
                var cache = loot.Items.Find(item => item.Special == site.CacheId);
                Assert.NotNull(cache);
                Assert.IsTrue(loot.IsExposed(cache));
                Assert.AreEqual(Pickup.BagFull, progress.Collect(cache, config));
                Assert.IsFalse(progress.HasSpecial(site.CacheId));
                progress.medkits = 0;
                Assert.AreEqual(Pickup.Collected, progress.Collect(cache, config));
                Assert.AreEqual(Pickup.Gone, progress.Collect(cache, config));
                Assert.AreEqual(0, progress.coins, "Cache is carried to the buyer, not paid immediately.");
                progress.ores = new int[0];
                progress.medkits = progress.Capacity(config) - 1;
            }
            var copy = JsonUtility.FromJson<GameProgress>(JsonUtility.ToJson(progress));
            var reloadedLoot = new LootField(new VoxelTerrain(config));
            reloadedLoot.MarkTaken(copy);
            foreach (var site in MineSites.All) Assert.IsTrue(reloadedLoot.Items.Find(item => item.Special == site.CacheId).Taken);
        }

        [Test] public void RoomOreIsAnchoredAndRetainsItsSaveIdentity()
        {
            var terrain = new VoxelTerrain(config);
            var before = new LootField(terrain);
            MineSites.Carve(terrain);
            var after = new LootField(terrain);
            Assert.AreEqual(before.Items.Count, after.Items.Count);
            for (int i = 0; i < before.Items.Count; i++)
            {
                Assert.AreEqual(before.Items[i].Position, after.Items[i].Position);
                Assert.AreEqual(before.Items[i].Chunk, after.Items[i].Chunk);
                Assert.AreEqual(before.Items[i].Slot, after.Items[i].Slot);
            }
            foreach (var site in MineSites.All)
            {
                var wallOre = MineSites.AnchorOre(site.Origin + new Vector3(.1f, 1.5f, 2), terrain);
                Assert.Greater(terrain.Sample(wallOre), 70, "Ore belongs at a wall, not in the empty room.");
                Assert.IsTrue(terrain.IsExposed(wallOre, .5f));
                Assert.AreEqual(wallOre, MineSites.AnchorOre(wallOre, terrain));
            }
        }

        [Test] public void DiscoveryIsPersistentAndRequiresEnteringTheRoom()
        {
            var progress = GameProgress.New(config);
            for (int i = 0; i < MineSites.All.Length; i++)
            {
                var site = MineSites.All[i];
                Assert.IsFalse(site.Contains(new Vector3(0, -site.Depth, -3)));
                Assert.IsFalse(site.Contains(new Vector3(0, -site.Depth + 4, 1)));
                Assert.IsTrue(site.Contains(new Vector3(0, -site.Depth + .2f, 1)));
                Assert.IsTrue(progress.DiscoverSite(i));
                Assert.IsFalse(progress.DiscoverSite(i));
            }
            var copy = JsonUtility.FromJson<GameProgress>(JsonUtility.ToJson(progress));
            Assert.IsTrue(copy.IsValid(config));
            for (int i = 0; i < MineSites.All.Length; i++) Assert.IsTrue(copy.HasSite(i));
            var legacy = JsonUtility.FromJson<GameProgress>("{\"version\":3,\"seed\":" + config.seed + "}");
            Assert.AreEqual(0, legacy.sites);
        }
    }
}
