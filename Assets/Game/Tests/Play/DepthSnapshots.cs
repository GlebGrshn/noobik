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
    /// Smoke tests of the real scene: depths, ore, the house, falls and the jetpack. Camera frames go to
    /// TestResults/Snapshots. Run with graphics (no -nographics) to get pictures:
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
            Shot("ui_welcome", true);
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
            // No way up but climbing, the jetpack or the rescuers.
            Assert.IsTrue(game.CanRescue);
            game.CallRescue();
            yield return Frames(30);
            Assert.AreEqual(0, game.Depth);
            Assert.IsTrue(Yard.InsideHouse(Body(game).transform.position));
            Shot("house_inside");
            game.DebugPlace(Yard.SurfaceSpawn, Yard.SurfaceYaw, 10);
            yield return Frames(30);
            Shot("99_surface_again");
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
            Assert.AreEqual(0, game.Progress.OrePieces);

            // Aim at the starter nugget from above and exercise the actual digging ray.
            var first = field.Items.Find(item => item.Special == 0);
            game.DebugPlace(first.Position + new Vector3(0, 0.6f, 0), 0, 85);
            Physics.SyncTransforms();
            Shot("ore_before_pickup");
            Invoke(game, "Swing");
            Assert.IsTrue(first.Taken);
            Assert.AreEqual(1, game.Progress.OreCount(first.Ore));
            Invoke(game, "Swing");
            Assert.AreEqual(1, game.Progress.OrePieces, "An ore hit must pay only once.");
        }

        [UnityTest]
        public IEnumerator OreSellsOnlyAtTheBuyer()
        {
            var game = Object.FindAnyObjectByType<MineGame>();
            PlayByTouch(game);
            game.DebugDig("12");
            yield return Frames(3);
            game.Progress.ores = new int[game.config.ores.Length];
            game.Progress.ores[1] = 4;
            int coins = game.Progress.coins, value = game.Progress.BagValue(game.config);
            game.DebugPlace(Yard.SurfaceSpawn, Yard.SurfaceYaw, 5);
            yield return new WaitForSeconds(0.5f);
            Assert.AreEqual(value, game.Progress.BagValue(game.config), "Coming up does not sell anything.");
            Assert.AreEqual(Station.None, game.Station);
            game.DebugPlace(Yard.CounterPoint + Vector3.up * 0.15f, 0, 5);
            yield return Frames(5);
            Assert.AreEqual(Station.Counter, game.Station);
            Shot("house_counter");
            game.OpenHouse();
            yield return Frames(3);
            Shot("ui_house_sell", true);
            game.SellOre();
            Assert.AreEqual(coins + value, game.Progress.coins);
            Assert.AreEqual(0, game.Progress.OrePieces);
            game.SellOre();
            Assert.AreEqual(coins + value, game.Progress.coins, "A second sale pays nothing.");
            game.GetComponent<MineHud>().ShowHouse(1);
            yield return Frames(3);
            Shot("ui_house_upgrades", true);
            game.GetComponent<MineHud>().ShowHouse(2);
            yield return Frames(3);
            Shot("ui_house_items", true);
        }

        [UnityTest]
        public IEnumerator ShortFallHurtsLongFallFaintsAndDropsOre()
        {
            var game = Object.FindAnyObjectByType<MineGame>();
            PlayByTouch(game);
            game.DebugDig("4");
            yield return Frames(3);
            var shaft = new Vector3(0.8f, 0.2f, -1.2f);
            game.DebugPlace(shaft, 0, 60);
            yield return new WaitForSeconds(1.5f);
            Assert.Less(game.Health, game.MaxHealth, "A four metre drop hurts.");
            Assert.Greater(game.Health, 0);

            game.DebugDig("24");
            yield return Frames(3);
            game.Progress.ores = new int[game.config.ores.Length];
            game.Progress.ores[0] = 3;
            game.DebugPlace(shaft, 0, 60);
            yield return new WaitForSeconds(3f);
            Assert.AreEqual(0, game.Progress.OrePieces, "Fainting leaves the ore in the mine.");
            Assert.AreEqual(game.MaxHealth, game.Health, "Wakes up rested.");
            Assert.IsTrue(Yard.InsideHouse(Body(game).transform.position));
        }

        [UnityTest]
        public IEnumerator JetpackLiftsAndBurnsFuel()
        {
            var game = Object.FindAnyObjectByType<MineGame>();
            PlayByTouch(game);
            game.Progress.coins = game.Progress.NextPrice(Track.Jetpack, game.config);
            game.Buy(Track.Jetpack);
            Assert.IsTrue(game.HasJetpack);
            game.DebugDig("10");
            yield return new WaitForSeconds(0.5f);
            float start = Body(game).transform.position.y;
            var jump = game.GetComponent<MineHud>().Jump;
            jump.OnPointerDown(null);
            yield return new WaitForSeconds(1.2f);
            float top = Body(game).transform.position.y;
            jump.OnPointerUp(null);
            Assert.Greater(top - start, 2.5f, "The jetpack climbs out of the shaft.");
            Assert.Less(game.Fuel, game.FuelMax);
        }

        [UnityTest]
        public IEnumerator AbandonedSitesHaveWalkableFloorsAndPersistentDiscoveries()
        {
            var game = Object.FindAnyObjectByType<MineGame>();
            PlayByTouch(game);
            game.Progress.jetLevel = 1;
            game.Engage();
            for (int i = 0; i < MineSites.All.Length; i++)
            {
                var site = MineSites.All[i];
                game.DebugPlace(site.Origin + new Vector3(0, .3f, .4f), 0, 10);
                yield return new WaitForSeconds(.5f);
                Assert.IsTrue(game.Progress.HasSite(i));
                Assert.AreEqual(i, game.CurrentSite);
                Assert.AreEqual(site.Depth, game.Depth);
                Assert.IsTrue(Body(game).isGrounded, "The old walkway must support the player.");
                Assert.Greater(game.Health, 0);
                yield return new WaitForSeconds(3.5f);
                game.DebugPlace(site.Origin + new Vector3(0, .15f, -.8f), 0, 8);
                yield return Frames(5);
                Shot("site_" + site.Depth, true);
                var cache = Field<LootField>(game, "loot").Items.Find(item => item.Special == site.CacheId);
                Assert.NotNull(cache.View, "The cache should be visible inside the room.");
                Assert.IsFalse(cache.Taken);
            }
            game.SaveNow();
            Assert.AreEqual(7, ProgressStore.Load(game.config).sites);
            game.DebugGoto("counter");
            yield return Frames(5);
            game.OpenHouse();
            var hud = game.GetComponent<MineHud>();
            typeof(MineHud).GetMethod("SelectTab", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(hud, new object[] { 3 });
            yield return Frames(5);
            Shot("ui_exploration_journal", true);
        }

        private static void PlayByTouch(MineGame game)
        {
            // The batch editor cannot lock the mouse; touch mode lets the player move.
            typeof(MineGame).GetField("<TouchMode>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(game, true);
        }

        private static CharacterController Body(MineGame game) => Field<CharacterController>(game, "body");

        private static T Field<T>(MineGame game, string name) =>
            (T)typeof(MineGame).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(game);

        private static void Invoke(MineGame game, string name, params object[] args) =>
            typeof(MineGame).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(game, args);

        private static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++) yield return null;
        }

        private static void Shot(string name, bool includeUi = false)
        {
            var camera = Camera.main;
            if (camera == null || SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            var target = new RenderTexture(960, 540, 24);
            var canvas = GameObject.Find("Game UI").GetComponent<Canvas>();
            if (includeUi)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = .1f;
                Canvas.ForceUpdateCanvases();
            }
            camera.targetTexture = target;
            camera.Render();
            camera.targetTexture = null;
            if (includeUi) canvas.renderMode = RenderMode.ScreenSpaceOverlay;
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
