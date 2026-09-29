using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Nubik.PlayTests
{
    /// <summary>Music by depth, the settings window, and the living yard. Keeps the editor's save and settings.</summary>
    public class YardAndSettingsTests
    {
        private const string Key = "nubik.progress.v1", SettingsKey = "nubik.settings.v1";
        private string saved, savedBackup, savedSettings;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            saved = PlayerPrefs.GetString(Key, null);
            savedBackup = PlayerPrefs.GetString(Key + ".backup", null);
            savedSettings = PlayerPrefs.GetString(SettingsKey, null);
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.DeleteKey(Key + ".backup");
            yield return SceneManager.LoadSceneAsync("Mine");
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator RestoreSave()
        {
            var scene = SceneManager.GetSceneByName("Mine");
            SceneManager.CreateScene("Test cleanup");
            if (scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            Restore(Key, saved);
            Restore(Key + ".backup", savedBackup);
            Restore(SettingsKey, savedSettings);
            PlayerPrefs.Save();
        }

        private static void Restore(string key, string value)
        {
            if (string.IsNullOrEmpty(value)) PlayerPrefs.DeleteKey(key);
            else PlayerPrefs.SetString(key, value);
        }

        [UnityTest]
        public IEnumerator MusicFollowsDepthAndSettingsApply()
        {
            var game = Object.FindAnyObjectByType<MineGame>();
            var audio = Object.FindAnyObjectByType<GameAudio>();
            for (int i = 0; i < 4; i++) Assert.IsTrue(audio.MusicLoaded(i), GameAudio.Music[i] + " is in Resources/Music");
            yield return new WaitForSeconds(3);
            Assert.Greater(audio.MusicWeight(0), .2f, "The yard track rises at the surface.");
            Assert.AreEqual(0, audio.MusicWeight(2), .001f);
            game.DebugDig("40");
            yield return new WaitForSeconds(4);
            Assert.Greater(audio.MusicWeight(1), .2f, "The slate brings the second track in.");
            Assert.Less(audio.MusicWeight(0), audio.MusicWeight(1), "…while the first fades out slowly.");

            var hud = game.GetComponent<MineHud>();
            game.OpenMenu();
            hud.OpenSettings();
            yield return null;
            Assert.IsTrue(hud.PanelOpen);
            float previousSensitivity = GameSettings.Current.cameraSensitivity;
            var sensitivity = GameObject.Find("Camera sensitivity").GetComponent<Slider>();
            sensitivity.value = 50;
            Assert.AreEqual(.5f, GameSettings.Current.cameraSensitivity);
            var lookMethod = typeof(MineGame).GetMethod("Look", BindingFlags.Instance | BindingFlags.NonPublic);
            var yawField = typeof(MineGame).GetField("yaw", BindingFlags.Instance | BindingFlags.NonPublic);
            float before = (float)yawField.GetValue(game);
            hud.Look.AddDelta(new Vector2(10, 0)); lookMethod.Invoke(game, null);
            float slowTurn = Mathf.DeltaAngle(before, (float)yawField.GetValue(game));
            sensitivity.value = 150;
            before = (float)yawField.GetValue(game);
            hud.Look.AddDelta(new Vector2(10, 0)); lookMethod.Invoke(game, null);
            float fastTurn = Mathf.DeltaAngle(before, (float)yawField.GetValue(game));
            Assert.Greater(slowTurn, 0);
            Assert.AreEqual(slowTurn * 3, fastTurn, .001f, "The slider scales actual camera rotation.");
            hud.CloseSettings();
            Assert.AreEqual(1.5f, GameSettings.Load().cameraSensitivity, "Closing settings persists sensitivity.");
            hud.OpenSettings();
            Assert.AreEqual(150, sensitivity.value, "Opening settings reflects the saved selection.");
            Shot("ui_settings", true);
            GameSettings.Set(s => { s.music = .3f; s.quality = GameSettings.Low; });
            yield return null;
            Assert.AreEqual(LightShadows.None, GameObject.Find("Sun").GetComponent<Light>().shadows, "Low has no shadows.");
            GameSettings.Set(s => s.quality = GameSettings.Ultra);
            yield return null;
            Assert.AreEqual(LightShadows.Soft, GameObject.Find("Sun").GetComponent<Light>().shadows);
            hud.ClosePanel();
            Assert.IsFalse(hud.SettingsOpen);
            Assert.IsTrue(PlayerPrefs.GetString(SettingsKey, "").Contains("\"quality\":2"), "Settings are saved on closing.");
            GameSettings.Set(s => { s.music = .7f; s.quality = GameSettings.Normal; s.cameraSensitivity = previousSensitivity; });
        }

        [UnityTest]
        public IEnumerator TheYardIsAlive()
        {
            var game = Object.FindAnyObjectByType<MineGame>();
            yield return null;
            var frog = GameObject.Find("Frog").transform;
            var dogChest = GameObject.Find("Dog chest").transform;
            var swing = GameObject.Find("Tire swing").transform;
            Vector3 frogAt = frog.position, chest = dogChest.localScale;
            var swingAt = swing.localRotation;
            yield return new WaitForSeconds(9);
            Assert.AreNotEqual(frogAt, frog.position, "The frog hops.");
            Assert.AreNotEqual(chest, dogChest.localScale, "The dog breathes.");
            Assert.AreNotEqual(swingAt, swing.localRotation, "The swing sways.");
            // A few frames of the new corners of the yard for review.
            Look(game, new Vector3(-8.2f, .05f, -4.5f), Yard.PondCenter + Vector3.up * .2f);
            yield return new WaitForSeconds(.3f);
            Shot("yard_pond");
            Look(game, new Vector3(8.5f, .05f, -1), new Vector3(12, .6f, -3));
            yield return new WaitForSeconds(.3f);
            Shot("yard_garden");
            Look(game, new Vector3(9, .05f, 5.5f), new Vector3(12.8f, .5f, 4.1f));
            yield return new WaitForSeconds(.3f);
            Shot("yard_dog");
            Look(game, new Vector3(-9.5f, .05f, 6), new Vector3(-12.6f, 1.2f, 10));
            yield return new WaitForSeconds(.3f);
            Shot("yard_swing");
            game.DebugPlace(new Vector3(0, .05f, -12), 0, -35);
            yield return new WaitForSeconds(.3f);
            Shot("yard_sky");
            game.DebugPlace(new Vector3(-3, .05f, -9), 15, 4);
            yield return new WaitForSeconds(.3f);
            Shot("yard_overview");
        }

        private static void Look(MineGame game, Vector3 feet, Vector3 target)
        {
            var eye = feet + Vector3.up * 1.55f;
            var d = target - eye;
            game.DebugPlace(feet, Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, -Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg);
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
