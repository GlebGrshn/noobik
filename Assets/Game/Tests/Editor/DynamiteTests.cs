using NUnit.Framework;
using UnityEngine;

namespace Nubik.Tests
{
    /// <summary>Dynamite, the smaller starting tank and the test upgrade button.</summary>
    public class DynamiteTests
    {
        private MineConfig config;
        [SetUp] public void Setup() => config = ScriptableObject.CreateInstance<MineConfig>();
        [TearDown] public void Cleanup() => Object.DestroyImmediate(config);

        [Test] public void DynamiteTakesBackpackSlotsAndIsSpentOnce()
        {
            var p = GameProgress.New(config);
            Assert.IsFalse(p.BuyDynamite(config), "Costs money");
            p.coins = config.dynamite.price * 2;
            int free = p.FreeSlots(config);
            Assert.IsTrue(p.BuyDynamite(config));
            Assert.AreEqual(1, p.dynamite);
            Assert.AreEqual(free - config.dynamite.slots, p.FreeSlots(config));
            Assert.AreEqual(config.dynamite.price, p.coins);
            var copy = ProgressStore.Decode(ProgressStore.Encode(p), config);
            Assert.NotNull(copy);
            Assert.AreEqual(1, copy.dynamite);
            Assert.IsTrue(p.UseDynamite());
            Assert.IsFalse(p.UseDynamite(), "Nothing left to throw");
            Assert.AreEqual(0, p.dynamite);
            p.medkits = p.Capacity(config);
            p.coins = 999;
            Assert.IsFalse(p.BuyDynamite(config), "A full backpack has no room");
            p = GameProgress.New(config); p.dynamite = -1;
            Assert.IsNull(ProgressStore.Decode(ProgressStore.Encode(p), config));
        }

        [Test] public void BlastClearsTheHardestRockButNotTheBedrock()
        {
            var terrain = new VoxelTerrain(config);
            // The crystal zone has natural caves: take solid rock, preferably a crystal vein.
            Vector3 Solid(float x, float z)
            {
                Vector3 best = Vector3.zero;
                for (float y = -95.25f; y > -112; y -= .5f)
                {
                    var p = new Vector3(x, y, z);
                    if (terrain.Sample(p) < 250) continue;
                    if (config.IsVein(p)) return p;
                    if (best == Vector3.zero) best = p;
                }
                Assert.AreNotEqual(Vector3.zero, best);
                return best;
            }
            var deep = Solid(0.25f, 0.25f);
            var result = terrain.Dig(deep, config.dynamite.power, config.dynamiteDamage);
            Assert.IsTrue(result.Changed);
            Assert.IsTrue(terrain.IsAir(deep), config.Rock(deep).nameEn + " must give way");
            // One hit of the first shovel barely scratches such rock.
            var other = Solid(-3.75f, 3.75f);
            terrain.Dig(other, config.tools[0].radius, config.tools[0].damage);
            Assert.IsFalse(terrain.IsAir(other));
            var bottom = new Vector3(0.25f, config.Bottom + 0.1f, 0.25f);
            terrain.Dig(bottom, config.dynamite.power, config.dynamiteDamage);
            Assert.IsFalse(terrain.IsAir(bottom), "The bedrock holds");
        }

        [Test] public void StartingTankIsSmallAndGrowsWithEveryLevel()
        {
            Assert.LessOrEqual(config.fuelTank[0].value, 20);
            for (int i = 1; i < config.fuelTank.Length; i++) Assert.Greater(config.fuelTank[i].value, config.fuelTank[i - 1].value);
            // The first tank still lifts the player out of the first zone.
            float climb = config.fuelTank[0].value / config.jetpack[0].value * config.jetMaxRise;
            Assert.Greater(climb, 30);
        }

        [Test] public void SavesFromTheBiggerTankLoadWithPetrolTrimmed()
        {
            var p = GameProgress.New(config);
            var json = System.Text.RegularExpressions.Regex.Replace(ProgressStore.Encode(p), "\"fuel\":[-0-9.eE+]+", "\"fuel\":40.0");
            Assert.IsTrue(json.Contains("\"fuel\":40.0"));
            var old = ProgressStore.Decode(json, config);
            Assert.NotNull(old, "An old save is not thrown away over petrol");
            Assert.AreEqual(old.FuelCapacity(config), old.fuel);
        }

        [Test] public void TestUpgradeIsFreeAndStillStopsAtTheTop()
        {
            var p = GameProgress.New(config);
            for (int i = 0; i < 20; i++) p.Upgrade(Track.Backpack, config, true);
            Assert.AreEqual(config.backpack.Length - 1, p.bagLevel);
            Assert.AreEqual(0, p.coins);
            Assert.IsFalse(p.Upgrade(Track.Backpack, config, true));
            Assert.IsFalse(p.Upgrade(Track.Tool, config), "Paying still needs coins");
        }
    }
}
