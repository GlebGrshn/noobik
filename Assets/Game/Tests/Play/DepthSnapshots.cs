using System.Collections;
using System.IO;
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

        [TearDown]
        public void RestoreSave()
        {
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
