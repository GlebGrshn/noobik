using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Nubik.Tests
{
    public class DepthsTests
    {
        private MineConfig config;
        [SetUp] public void Setup() => config = ScriptableObject.CreateInstance<MineConfig>();
        [TearDown] public void Cleanup() => Object.DestroyImmediate(config);

        [Test] public void LavaBurnsAtTheSurfaceButNotBesideAboveOrFarBelow()
        {
            foreach (var pool in Depths.Lava)
            {
                var at = new Vector3(pool.Center.x, pool.Surface, pool.Center.z);
                Assert.IsTrue(pool.Burns(at));
                Assert.IsTrue(pool.Burns(at - Vector3.up * .3f));
                Assert.IsFalse(pool.Burns(at + Vector3.up * 1.5f));
                Assert.IsFalse(pool.Burns(at - Vector3.up * 3));
                Assert.IsTrue(pool.Burns(new Vector3(pool.Center.x, pool.Floor - 1.7f, pool.Center.z)), "Flying up into the lava from below burns too.");
                Assert.IsFalse(pool.Burns(at + Vector3.right * pool.Half.x));
                Assert.IsFalse(pool.Burns(at + Vector3.forward * pool.Half.z));
            }
        }

        [Test] public void MagmaStartsAt100AndDoesNotMoveOlderCaverns()
        {
            Assert.AreEqual(2, config.ZoneIndex(99.99f));
            Assert.AreEqual(3, config.ZoneIndex(100));
            Assert.AreEqual("Магма", config.Zone(119).nameRu);
            Assert.Greater(config.zones[3].rock.hardness, config.zones[2].rock.hardness);
            Assert.IsTrue(config.zones[3].ores.Any(x => x.ore == 11));
            Assert.IsFalse(config.zones.Any(z => z.ores.Any(x => x.ore == Depths.StarMetal)), "Star metal is only inside the meteorite.");
            var before = Object.Instantiate(config);
            before.zones = before.zones.Take(3).ToArray();
            try
            {
                var old = new VoxelTerrain(before); var fresh = new VoxelTerrain(config);
                for (float y = -73; y >= -119; y -= 2)
                    for (float x = -5; x <= 5; x += 2)
                        for (float z = -5; z <= 5; z += 2)
                            Assert.AreEqual(old.Sample(new Vector3(x,y,z)), fresh.Sample(new Vector3(x,y,z)), "Adding a biome must not reroll caves.");
            }
            finally { Object.DestroyImmediate(before); }
        }

        [Test] public void DepthRoomsAreIdempotentAndDoNotRefillSavedExcavation()
        {
            var terrain = new VoxelTerrain(config);
            var dug = new Vector3(-1, -108, 4);
            terrain.Dig(dug, 1.3f, 999);
            Depths.Prepare(terrain);
            var encoded = terrain.EditedChunks().ToDictionary(i => i, terrain.Encode);
            Depths.Prepare(terrain);
            foreach (var entry in encoded) Assert.AreEqual(entry.Value, terrain.Encode(entry.Key));
            var loaded = new VoxelTerrain(config);
            foreach (var entry in encoded) Assert.IsTrue(loaded.Decode(entry.Key, entry.Value));
            Depths.Prepare(loaded);
            Assert.IsTrue(loaded.IsAir(dug));
            Assert.IsTrue(loaded.IsAir(Depths.CraterCenter));
            foreach (var pool in Depths.Lava) Assert.IsTrue(loaded.IsAir(pool.Center));
            var loot = new LootField(loaded);
            foreach (var item in loot.Items.Where(x => !x.Meteor && x.Kind == LootKind.Find))
            {
                foreach (var pool in Depths.Lava) Assert.IsFalse(pool.InPocket(item.Position), "Random loot must not spawn in lava.");
                var d = item.Position - Depths.CraterCenter;
                Assert.IsFalse(Mathf.Abs(d.x)<Depths.CraterHalf.x && Mathf.Abs(d.y)<Depths.CraterHalf.y && Mathf.Abs(d.z)<Depths.CraterHalf.z);
            }
        }

        [Test] public void MeteorLootNeedsSpacePaysOnceAndSurvivesReload()
        {
            var loot = new LootField(new VoxelTerrain(config));
            var fragments = loot.Items.Where(x => x.Meteor).ToArray();
            Assert.AreEqual(3, fragments.Length);
            Assert.AreEqual(3, fragments.Select(x => x.Special).Distinct().Count());
            var p = GameProgress.New(config); p.meteor = true; p.medkits = p.Capacity(config) - 1;
            Assert.AreEqual(Pickup.BagFull, p.Collect(fragments[0], config));
            Assert.IsFalse(p.HasSpecial(Depths.FirstFragment));
            p.medkits = 0;
            foreach (var item in fragments)
            {
                Assert.AreEqual(Depths.StarMetal, item.Ore);
                Assert.AreEqual(2, item.Slots);
                Assert.AreEqual(Pickup.Collected, p.Collect(item, config));
                Assert.AreEqual(Pickup.Gone, p.Collect(item, config));
            }
            Assert.AreEqual(6, p.UsedSlots(config)); Assert.AreEqual(1350, p.BagValue(config));
            var restored = ProgressStore.Decode(ProgressStore.Encode(p), config);
            Assert.NotNull(restored); Assert.IsTrue(restored.meteor);
            var regenerated = new LootField(new VoxelTerrain(config)); regenerated.MarkTaken(restored);
            Assert.IsTrue(regenerated.Items.Where(x => x.Meteor).All(x => x.Taken));
            string older = ProgressStore.Encode(GameProgress.New(config)).Replace("\"meteor\":false,", "");
            Assert.IsFalse(ProgressStore.Decode(older, config).meteor);
        }

        [Test] public void EveryNewSoundAndAmbienceHasOneNonEmptyClip()
        {
            var clips = Resources.LoadAll<AudioClip>("Audio");
            var names = new[] { "click", "step", "step_stone", "land", "clank", "drill", "harpoon", "roar", "slam", "warn", "boss_hit", "lava", "hum", "sizzle" }.Concat(GameAudio.Ambiences);
            foreach (string name in names)
            {
                var matches = clips.Where(c => c.name == name).ToArray();
                Assert.AreEqual(1, matches.Length, name + " must resolve unambiguously from Resources");
                Assert.Greater(matches[0].length, name.StartsWith("ambient_") ? 10 : .02f);
            }
        }
    }
}
