using NUnit.Framework;
using UnityEngine;

namespace Nubik.Tests
{
    /// <summary>Secrets, the buyer's orders and rewarded ads: each pays once and never breaks the save.</summary>
    public class RewardsTests
    {
        private MineConfig config;
        [SetUp] public void Setup() => config = ScriptableObject.CreateInstance<MineConfig>();
        [TearDown] public void Cleanup() => Object.DestroyImmediate(config);

        private GameProgress Reload(GameProgress p) => ProgressStore.Decode(ProgressStore.Encode(p), config);

        [Test] public void SecretsAreHiddenInsideTheYardAndPayOnce()
        {
            var terrain = new VoxelTerrain(config);
            MineSites.Carve(terrain); Expedition.Prepare(terrain); Secrets.Prepare(terrain);
            var field = new LootField(terrain);
            var p = GameProgress.New(config);
            // A full backpack does not matter: secrets are not ore.
            p.medkits = p.Capacity(config);
            float edge = config.width / 2f - 0.55f;
            for (int i = 0; i < Secrets.All.Length; i++)
            {
                var secret = Secrets.All[i];
                var item = field.Items.Find(x => x.Secret == i);
                Assert.NotNull(item);
                Assert.AreEqual(LootKind.Secret, item.Kind);
                if (!secret.OnSurface)
                {
                    Assert.Less(Mathf.Abs(secret.Position.x) + secret.Pocket.x, edge + .1f, secret.Name + " must be diggable");
                    Assert.Less(Mathf.Abs(secret.Position.z) + secret.Pocket.z, edge + .1f, secret.Name + " must be diggable");
                    Assert.Greater(secret.Position.y, config.FloorY + 1);
                    foreach (var site in MineSites.All) Assert.Greater(Mathf.Abs(secret.Position.y + site.Depth), 4, secret.Name + " overlaps " + site.Name);
                    foreach (var key in Expedition.Keys) Assert.Greater(Vector3.Distance(secret.Position, key.Position), 3, secret.Name + " overlaps " + key.Name);
                }
                Assert.IsTrue(field.IsExposed(item), secret.Name + " sits in its own pocket");
                int coins = p.coins;
                Assert.AreEqual(Pickup.Collected, p.Collect(item, config));
                Assert.AreEqual(coins + secret.Reward, p.coins);
                Assert.AreEqual(Pickup.Gone, p.Collect(item, config), "A secret pays once");
                Assert.AreEqual(coins + secret.Reward, p.coins);
            }
            Assert.AreEqual(Secrets.All.Length, Secrets.Count(p));
            var copy = Reload(p);
            Assert.NotNull(copy);
            var again = new LootField(terrain);
            again.MarkTaken(copy);
            Assert.IsTrue(again.Items.FindAll(x => x.Kind == LootKind.Secret).TrueForAll(x => x.Taken));
        }

        [Test] public void NoOreFloatsInsideASecretPocket()
        {
            var field = new LootField(new VoxelTerrain(config));
            foreach (var item in field.Items)
            {
                if (item.Kind != LootKind.Find || item.Special >= 0) continue;
                foreach (var secret in Secrets.All)
                {
                    if (secret.OnSurface) continue;
                    var p = item.Position - (secret.Position + Vector3.up * (secret.Pocket.y - .35f));
                    bool inside = Mathf.Abs(p.x) < secret.Pocket.x && Mathf.Abs(p.y) < secret.Pocket.y && Mathf.Abs(p.z) < secret.Pocket.z;
                    Assert.IsFalse(inside, "Ore floats in the pocket of " + secret.Name);
                }
            }
        }

        [Test] public void GoldenStatueRaisesSalePrices()
        {
            var p = GameProgress.New(config);
            p.ores = new int[config.ores.Length];
            p.ores[2] = 10;
            int plain = p.SaleValue(config);
            Assert.AreEqual(p.BagValue(config), plain);
            p.secrets |= 1 << Secrets.Statue;
            Assert.AreEqual(Mathf.RoundToInt(plain * 1.1f), p.SaleValue(config));
            int coins = p.coins;
            Assert.AreEqual(p.SaleValue(config), p.Sell(config));
            Assert.AreEqual(coins + Mathf.RoundToInt(plain * 1.1f), p.coins);
        }

        [Test] public void OrdersFollowProgressionAndFitTheBackpack()
        {
            var p = GameProgress.New(config);
            for (int roll = 0; roll < 40; roll++)
            {
                p.questRoll = roll; p.questOre = -1;
                p.EnsureQuest(config);
                Assert.IsTrue(System.Array.Exists(config.zones[0].ores, c => c.ore == p.questOre), "A new player only gets first-zone ore");
                Assert.LessOrEqual(p.questAmount * config.ores[p.questOre].slots, Mathf.Max(2, p.Capacity(config) * 3 / 4));
                Assert.GreaterOrEqual(p.questAmount, 2);
                Assert.Greater(p.questReward, p.questAmount * config.ores[p.questOre].value, "An order pays more than selling");
            }
            // Deep players mostly get deep ore, sometimes something from above.
            p.maxDepth = 95;
            int deep = 0;
            for (int roll = 0; roll < 60; roll++)
            {
                p.questRoll = roll; p.questOre = -1;
                p.EnsureQuest(config);
                if (System.Array.Exists(config.zones[2].ores, c => c.ore == p.questOre)) deep++;
                Assert.IsFalse(config.ores[p.questOre].chest, "Orders ask for ore, not chests");
            }
            Assert.Greater(deep, 30);
            Assert.Less(deep, 60);
        }

        [Test] public void OrderDeliversOnceAndGrowsTheNextOne()
        {
            var p = GameProgress.New(config);
            p.EnsureQuest(config);
            Assert.IsTrue(p.HasQuest);
            int ore = p.questOre, need = p.questAmount, reward = p.questReward;
            Assert.IsFalse(p.CanDeliver);
            Assert.AreEqual(0, p.DeliverQuest(config), "Nothing to deliver yet");
            p.ores = new int[config.ores.Length];
            p.ores[ore] = need + 1;
            Assert.IsTrue(p.CanDeliver);
            int coins = p.coins;
            Assert.AreEqual(reward, p.DeliverQuest(config));
            Assert.AreEqual(coins + reward, p.coins);
            Assert.AreEqual(1, p.ores[ore], "Only the ordered amount goes");
            Assert.AreEqual(1, p.questNumber);
            Assert.IsTrue(p.HasQuest, "The buyer has the next order ready");
            var copy = Reload(p);
            Assert.NotNull(copy);
            Assert.AreEqual(p.questOre, copy.questOre);
            Assert.AreEqual(p.questReward, copy.questReward);
            // Skipping replaces the order but pays nothing.
            coins = p.coins;
            int before = p.questRoll;
            p.SkipQuest(config);
            Assert.AreEqual(before + 1, p.questRoll);
            Assert.AreEqual(coins, p.coins);
            Assert.IsTrue(p.HasQuest);
            // Later orders ask for more of the same ore.
            var late = GameProgress.New(config);
            late.questNumber = 12; late.bagLevel = config.backpack.Length - 1;
            late.EnsureQuest(config);
            var early = GameProgress.New(config);
            early.bagLevel = config.backpack.Length - 1;
            early.questOre = -1;
            early.EnsureQuest(config);
            Assert.Greater(late.questReward / (float)(late.questAmount * config.ores[late.questOre].value),
                early.questReward / (float)(early.questAmount * config.ores[early.questOre].value));
        }

        [Test] public void AdRewardGrowsWithUpgradesAndPaysEachTicketOnce()
        {
            var p = GameProgress.New(config);
            int first = p.AdReward(config);
            Assert.GreaterOrEqual(first, 25);
            Assert.AreEqual(0, first % 5);
            p.tool = config.tools.Length - 1; p.bagLevel = 3; p.fuelLevel = 2;
            Assert.Greater(p.AdReward(config), first * 3);
            p.bagLevel = config.backpack.Length - 1; p.jetLevel = config.jetpack.Length - 1; p.healthLevel = config.health.Length - 1; p.fuelLevel = config.fuelTank.Length - 1;
            Assert.Less(p.AdReward(config), config.tools[config.tools.Length - 1].price / 2, "One ad never buys the drill");

            long now = 1_000_000;
            Assert.IsTrue(p.AdReady(now));
            int coins = p.coins, reward = p.AdReward(config);
            Assert.AreEqual(0, p.GrantAd(2, now, config), "Only the next ticket pays");
            Assert.AreEqual(reward, p.GrantAd(1, now, config));
            Assert.AreEqual(0, p.GrantAd(1, now, config), "A repeated callback pays nothing");
            Assert.AreEqual(coins + reward, p.coins);
            Assert.IsFalse(p.AdReady(now + GameProgress.AdCooldown - 1));
            Assert.IsTrue(p.AdReady(now + GameProgress.AdCooldown));
            var copy = Reload(p);
            Assert.NotNull(copy);
            Assert.AreEqual(0, copy.GrantAd(1, now + 999, config), "The ticket survives a reload");
        }

        [Test] public void BrokenRewardFieldsAreRejected()
        {
            var p = GameProgress.New(config);
            p.EnsureQuest(config);
            Assert.NotNull(Reload(p));
            p.questReward = 0; Assert.IsNull(Reload(p));
            p = GameProgress.New(config); p.secrets = 1 << Secrets.All.Length; Assert.IsNull(Reload(p));
            p = GameProgress.New(config); p.adRewards = -1; Assert.IsNull(Reload(p));
            p = GameProgress.New(config); p.questOre = config.ores.Length; p.questAmount = 1; p.questReward = 5; Assert.IsNull(Reload(p));
        }

        [Test] public void OlderV4SavesLoadWithNoSecretsOrdersOrAds()
        {
            var p = GameProgress.New(config);
            p.coins = 77;
            var json = ProgressStore.Encode(p);
            // Strip the new fields as an older build would have written the save.
            foreach (var field in new[] { "secrets", "questNumber", "questOre", "questAmount", "questReward", "questRoll", "adRewards", "adReadyAt" })
                json = System.Text.RegularExpressions.Regex.Replace(json, ",\"" + field + "\":-?[0-9]+", "");
            Assert.IsFalse(json.Contains("questOre"));
            var old = ProgressStore.Decode(json, config);
            Assert.NotNull(old);
            Assert.AreEqual(77, old.coins);
            Assert.AreEqual(0, old.secrets);
            Assert.IsFalse(old.HasQuest);
            Assert.AreEqual(0, old.adRewards);
        }
    }
}
