using System.Runtime.InteropServices;
using UnityEngine;

namespace Nubik
{
    public sealed class YandexBridge : MonoBehaviour
    {
        public static bool Paused { get; private set; }
        private bool inMine = true;
        private bool focused = true;
        private bool visible = true;
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void NubikReady();
        [DllImport("__Internal")] private static extern void NubikGameplay(int active);
#endif
        public void Ready()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            NubikReady();
#endif
            Apply();
        }
        public void SetInMine(bool value) { inMine = value; Apply(); }
        public void OnVisibility(string value) { visible = value == "1"; Apply(); }
        private void OnApplicationFocus(bool value) { focused = value; Apply(); }
        private void Apply()
        {
            Paused = !focused || !visible;
            Time.timeScale = Paused ? 0 : 1;
            AudioListener.pause = Paused;
#if UNITY_WEBGL && !UNITY_EDITOR
            NubikGameplay(inMine && !Paused ? 1 : 0);
#endif
        }
        private void OnDestroy() { Paused = false; Time.timeScale = 1; AudioListener.pause = false; }
    }
}
