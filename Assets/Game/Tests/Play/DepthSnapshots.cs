using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Nubik.PlayTests
{
    /// <summary>
    /// Smoke test of the real scene: drops the player to several depths and writes camera frames
    /// to TestResults/Snapshots. Run with graphics (no -nographics) to get pictures:
    /// Unity -batchmode -runTests -testPlatform PlayMode -projectPath .
    /// The editor save is kept aside and restored.
    /// </summary>
    public class DepthSnapshots
    {
        private const string Key = "nubik.progress.v1";
        private string saved, savedBackup;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            saved = PlayerPrefs.GetString(Key, null);
            savedBackup = PlayerPrefs.GetString(Key + ".backup", null);
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.DeleteKey(Key + ".backup");
            yield return SceneManager.LoadSceneAsync("Mine");
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator RestoreSave()
        {
            // Remove the test game before restoring preferences so its quit/focus callbacks cannot overwrite them.
            var scene = SceneManager.GetSceneByName("Mine");
            SceneManager.CreateScene("Test cleanup");
            if (scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            Restore(Key, saved);
            Restore(Key + ".backup", savedBackup);
            PlayerPrefs.Save();
        }

        private static void Restore(string key, string value)
        {
            if (string.IsNullOrEmpty(value)) PlayerPrefs.DeleteKey(key);
            else PlayerPrefs.SetString(key, value);
        }

        [UnityTest]
        public IEnumerator EveryZoneRendersAndTheDoorIsReached()
        {
            var game = Object.FindAnyObjectByType<MineGame>();
            Assert.NotNull(game);
            yield return Frames(30);
            game.SetView(0, 36);
            yield return Frames(3);
            Shot("00_surface");
            foreach (int depth in new[] { 12, 45, 90 })
            {
                game.DebugDig(depth.ToString());
                yield return Frames(8);
                Assert.AreEqual(depth, game.Depth, 1);
                game.SetView(200, 5);
                yield return Frames(3);
                Shot(depth.ToString("000") + "_wall");
            }
            game.DebugDig(game.config.depth.ToString());
            yield return Frames(8);
            Assert.IsTrue(game.Progress.finished);
            game.SetView(-14, 8);
            yield return Frames(3);
            Shot("120_door");
            game.ReturnToSurface();
            game.CloseShop();
            game.SetView(0, 10);
            yield return Frames(30);
            Shot("99_surface_again");
            Assert.AreEqual(0, game.Depth);
        }

        [UnityTest]
        public IEnumerator RevealedOreStaysVisibleUntilHit()
        {
            var game = Object.FindAnyObjectByType<MineGame>();
            game.DebugDig("12");
            yield return Frames(3);
            var field = Field<LootField>(game, "loot");
            int rendered = 0;
            foreach (var item in field.Items)
            {
                if (item.Kind != LootKind.Find || item.View == null) continue;
                Assert.IsFalse(item.Taken, "Revealing ore must not collect it.");
                var renderers = item.View.GetComponentsInChildren<Renderer>();
                Assert.AreEqual(3, renderers.Length);
                foreach (var renderer in renderers)
                    Assert.Less(Vector3.Distance(renderer.bounds.center, item.Position), 0.25f,
                        "Nugget geometry must stay beside its hitbox (unsigned offset regression).");
                rendered++;
            }
            Assert.Greater(rendered, 0);
            Assert.AreEqual(0, game.Progress.backpack);

            // Aim at the starter nugget from above and exercise the actual digging ray.
            var first = field.Items.Find(item => item.Special == 0);
            Invoke(game, "Teleport", first.Position + new Vector3(0, 0.6f, 0));
            game.SetView(0, 85);
            Physics.SyncTransforms();
            Shot("ore_before_pickup");
            Invoke(game, "Swing");
            Assert.IsTrue(first.Taken);
            Assert.AreEqual(first.Value, game.Progress.backpack);
            int amount = game.Progress.backpack;
            Invoke(game, "Swing");
            Assert.AreEqual(amount, game.Progress.backpack, "An ore hit must pay only once.");
        }

        [UnityTest]
        public IEnumerator ReturningSellsOnceAndKeepsDivePosition()
        {
            var game = Object.FindAnyObjectByType<MineGame>();
            game.DebugDig("12");
            yield return Frames(3);
            int coins = game.Progress.coins, trips = game.Progress.expeditions;
            game.Progress.backpack = 47;
            game.ReturnToSurface();
            Assert.AreEqual(0, game.Progress.backpack);
            Assert.AreEqual(coins + 47, game.Progress.coins);
            Assert.AreEqual(trips + 1, game.Progress.expeditions);
            Assert.IsTrue(game.Progress.hasDive);
            Assert.Less(game.Progress.dive.y, -10);
            game.ReturnToSurface();
            Assert.AreEqual(coins + 47, game.Progress.coins);
            Assert.AreEqual(trips + 1, game.Progress.expeditions);
        }

        [UnityTest]
        public IEnumerator WalkingOutSellsWithoutOpeningShop()
        {
            var game = Object.FindAnyObjectByType<MineGame>();
            game.DebugDig("3");
            yield return Frames(3);
            var body = Field<CharacterController>(game, "body");
            game.Progress.backpack = 31;
            int coins = game.Progress.coins;
            yield return Frames(3);
            Assert.AreEqual(31, game.Progress.backpack, "No sale while underground.");
            Invoke(game, "Teleport", Yard.SurfaceSpawn + Vector3.up * 2);
            yield return Frames(3);
            Assert.AreEqual(31, game.Progress.backpack, "Do not sell during a jump.");
            Invoke(game, "Teleport", Yard.SurfaceSpawn);
            body.Move(Vector3.down * 0.2f);
            yield return Frames(3);
            Assert.AreEqual(0, game.Progress.backpack);
            Assert.AreEqual(coins + 31, game.Progress.coins);
            Assert.IsFalse(game.GetComponent<MineHud>().PanelOpen);
            yield return Frames(3);
            Assert.AreEqual(coins + 31, game.Progress.coins);
        }

        private static T Field<T>(MineGame game, string name) =>
            (T)typeof(MineGame).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(game);

        private static void Invoke(MineGame game, string name, params object[] args) =>
            typeof(MineGame).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(game, args);

        private static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++) yield return null;
        }

        private static void Shot(string name)
        {
            var camera = Camera.main;
            if (camera == null || SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            var target = new RenderTexture(960, 540, 24);
            camera.targetTexture = target;
            camera.Render();
            camera.targetTexture = null;
            RenderTexture.active = target;
            var image = new Texture2D(960, 540, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 960, 540), 0, 0);
            image.Apply();
            RenderTexture.active = null;
            Directory.CreateDirectory("TestResults/Snapshots");
            File.WriteAllBytes("TestResults/Snapshots/" + name + ".png", image.EncodeToPNG());
            Object.Destroy(image);
            target.Release();
        }
    }
}
