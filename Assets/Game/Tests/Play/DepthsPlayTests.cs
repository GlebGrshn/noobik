using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;

namespace Nubik.PlayTests
{
    public partial class DepthSnapshots
    {
        [UnityTest] public IEnumerator LavaDamagesOnlyWhilePlayingAndRescuePreservesKeys()
        {
            var game = Object.FindAnyObjectByType<MineGame>(); PlayByTouch(game);
            var pool = Depths.Lava[0];
            var at = new Vector3(pool.Center.x, pool.Surface, pool.Center.z);
            game.DebugPlace(at, 0, 20);
            float health = game.Health;
            yield return new WaitForSeconds(.6f);
            Assert.IsTrue(game.InLava, "Feet: " + Body(game).transform.position + ", liquid: " + pool.Surface);
            Assert.Less(game.Health, health - 8);
            Shot("magma_lava_damage", true);
            game.OpenMenu(); health = game.Health;
            yield return new WaitForSeconds(.4f);
            Assert.AreEqual(health, game.Health, .01f, "Paused lava does not hurt.");
            game.Engage(); game.Progress.keys = 1; game.Progress.health = 1;
            yield return new WaitForSeconds(.3f);
            Assert.Greater(Body(game).transform.position.y, -1);
            Assert.AreEqual(game.MaxHealth, game.Health);
            Assert.IsTrue(game.Progress.HasKey(0));
            game.DebugPlace(at + Vector3.up * 2, 0, 20);
            Invoke(game, "UpdateDepths", .1f);
            Assert.IsFalse(game.InLava, "Jetpack height clears the heat.");
        }

        [UnityTest] public IEnumerator MeteorResistsToolsCracksWithNearbyBlastAndStaysOpen()
        {
            var game = Object.FindAnyObjectByType<MineGame>();
            var loot = Field<LootField>(game, "loot");
            var fragments = loot.Items.Where(x => x.Meteor).ToArray();
            Assert.IsTrue(game.MeteorIntact);
            Assert.IsTrue(fragments.All(x => !x.Exposed && x.View == null));
            // Camera level with the meteor's centre; real tool ray hits its collider.
            game.DebugPlace(Depths.Meteor + new Vector3(0, -1.55f, -1.65f), 0, 0);
            game.Progress.tool = game.config.tools.Length - 1;
            game.Progress.fuel = 15;
            Invoke(game, "BuildTool"); Invoke(game, "Swing");
            yield return Frames(2);
            Assert.IsTrue(game.MeteorIntact); Assert.IsFalse(game.Progress.meteor);
            Invoke(game, "Explode", Depths.Meteor + Vector3.left * 8);
            Assert.IsTrue(game.MeteorIntact, "An unrelated explosion does not unlock it.");
            game.DebugPlace(new Vector3(1, -112, 0), 0, 0);
            Invoke(game, "Explode", Depths.Meteor + Vector3.forward * .8f);
            yield return Frames(2);
            Assert.IsFalse(game.MeteorIntact); Assert.IsTrue(game.Progress.meteor);
            Assert.IsTrue(fragments.All(x => x.Exposed && x.View != null));
            Invoke(game, "Collect", fragments[0]);
            Assert.AreEqual(1, game.Progress.OreCount(Depths.StarMetal));
            Invoke(game, "Explode", Depths.Meteor);
            Assert.AreEqual(3, loot.Items.Count(x => x.Meteor), "Repeated blasts never duplicate rewards.");
            game.SaveNow();
            yield return SceneManager.LoadSceneAsync("Mine"); yield return null;
            game = Object.FindAnyObjectByType<MineGame>();
            Assert.IsFalse(game.MeteorIntact);
            loot = Field<LootField>(game, "loot");
            Assert.AreEqual(1, loot.Items.Count(x => x.Meteor && x.Taken));
            Assert.AreEqual(2, loot.Items.Count(x => x.Meteor && !x.Taken && x.Exposed));
        }
    }
}
