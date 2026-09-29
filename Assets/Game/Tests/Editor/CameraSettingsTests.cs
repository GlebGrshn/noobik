using NUnit.Framework;
using UnityEngine;

namespace Nubik.Tests
{
    public class CameraSettingsTests
    {
        private const string Key = "nubik.settings.v1";
        private string previous;
        private bool existed;
        [SetUp] public void Setup() { existed = PlayerPrefs.HasKey(Key); previous = PlayerPrefs.GetString(Key); }
        [TearDown] public void Cleanup()
        {
            if (existed) PlayerPrefs.SetString(Key, previous); else PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
        }

        [Test] public void EarlierSettingsKeepTheirAudioAndDefaultCameraSpeed()
        {
            PlayerPrefs.SetString(Key, "{\"master\":0.4,\"music\":0.3,\"quality\":0}");
            var settings = GameSettings.Load();
            Assert.AreEqual(1f, settings.cameraSensitivity);
            Assert.AreEqual(.4f, settings.master);
            Assert.AreEqual(.3f, settings.music);
            Assert.AreEqual(0, settings.quality);
        }

        [TestCase(.05f, .25f)]
        [TestCase(1.35f, 1.35f)]
        [TestCase(9f, 2f)]
        [TestCase(0f, 1f)]
        public void CameraSpeedLoadsWithinPlayableBounds(float stored, float expected)
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(new GameSettings { cameraSensitivity = stored }));
            Assert.AreEqual(expected, GameSettings.Load().cameraSensitivity, .001f);
        }
    }
}
