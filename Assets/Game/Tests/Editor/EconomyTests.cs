using NUnit.Framework;
using UnityEngine;

namespace Nubik.Tests
{
    public class EconomyTests
    {
        private MineConfig config;
        private GameProgress progress;
        [SetUp] public void Setup() { config = ScriptableObject.CreateInstance<MineConfig>(); progress = new GameProgress { seed = config.seed }; }
        [TearDown] public void Cleanup() => Object.DestroyImmediate(config);

        [Test] public void BlockCannotPayTwice()
        {
            Assert.IsTrue(progress.BreakBlock(2, config));
            int coins = progress.coins, loot = progress.backpack;
            Assert.IsFalse(progress.BreakBlock(2, config));
            Assert.AreEqual(coins, progress.coins); Assert.AreEqual(loot, progress.backpack);
        }
        [Test] public void SaleCannotPayTwice()
        {
            progress.BreakBlock(2, config);
            int before = progress.coins, loot = progress.backpack;
            Assert.Greater(loot, 0); Assert.AreEqual(loot, progress.Sell());
            Assert.AreEqual(0, progress.Sell()); Assert.AreEqual(before + loot, progress.coins);
        }
        [Test] public void UpgradeRequiresFundsAndChargesExactlyOnce()
        {
            Assert.IsFalse(progress.Upgrade(config));
            progress.coins = 60;
            Assert.IsTrue(progress.Upgrade(config));
            Assert.AreEqual(0, progress.coins); Assert.AreEqual(1, progress.pickTier);
            Assert.IsFalse(progress.Upgrade(config));
        }
        [Test] public void MaximumTierDoesNotCharge()
        {
            progress.pickTier = 2; progress.coins = 1000;
            Assert.IsFalse(progress.Upgrade(config)); Assert.AreEqual(1000, progress.coins);
        }
        [Test] public void CollectionRewardIsPermanent()
        {
            int id = 17;
            progress.BreakBlock(id, config);
            Assert.IsTrue(progress.foundHelmet);
            Assert.AreEqual(config.Coins(id) + 30, progress.coins);
            progress.Sell(); Assert.IsTrue(progress.foundHelmet);
        }
        [Test] public void SaveRoundTripPreservesTransactions()
        {
            progress.BreakBlock(2, config); progress.BreakBlock(17, config); progress.Sell();
            var loaded = ProgressStore.Decode(JsonUtility.ToJson(progress), config);
            Assert.NotNull(loaded); Assert.AreEqual(progress.coins, loaded.coins);
            Assert.IsTrue(loaded.foundHelmet); Assert.IsFalse(loaded.BreakBlock(2, config));
        }
        [TestCase("")] [TestCase("{}")] [TestCase("not json")] [TestCase("{\"version\":99,\"seed\":28092026}")]
        public void CorruptOrFutureSaveIsRejected(string json) => Assert.IsNull(ProgressStore.Decode(json, config));
        [Test] public void InvalidBlockCannotPay() { Assert.IsFalse(progress.BreakBlock(-1, config)); Assert.IsFalse(progress.BreakBlock(150, config)); Assert.AreEqual(0, progress.coins); }
        [Test] public void DuplicateSavedBlockIsRejected() { progress.destroyed.Add(2); progress.destroyed.Add(2); Assert.IsFalse(progress.IsValid(config)); }
        [Test] public void FirstUpgradeReachableOnCentralRoute()
        {
            for (int row = 0; row < 20; row++) progress.BreakBlock(row * 5 + 2, config);
            progress.Sell(); Assert.GreaterOrEqual(progress.coins, 60);
            Assert.IsTrue(progress.Upgrade(config));
        }
        [Test] public void AllBlockRewardsStayInConfiguredRanges()
        {
            for (int id = 0; id < config.BlockCount; id++)
            {
                Assert.That(config.Coins(id), Is.InRange(2, 4));
                Assert.IsTrue(config.Loot(id) == 0 || config.Loot(id) >= 12 && config.Loot(id) <= 25);
            }
        }
    }
}
