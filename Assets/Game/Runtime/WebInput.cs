using System.Runtime.InteropServices;
using UnityEngine;

namespace Nubik
{
    /// <summary>
    /// Pointer lock and mouse deltas. In WebGL the page owns the lock (it must be requested inside a click)
    /// and may refuse it; then the game falls back to looking around with the right button held.
    /// </summary>
    public static class WebInput
    {
        /// <summary>Unity's native safe area intersected with browser CSS safe-area insets.</summary>
        public static Rect SafeArea
        {
            get
            {
                var safe = Screen.safeArea;
#if UNITY_WEBGL && !UNITY_EDITOR
                float left = NubikSafeInset(0) * Screen.width, bottom = NubikSafeInset(1) * Screen.height;
                float right = (1 - NubikSafeInset(2)) * Screen.width, top = (1 - NubikSafeInset(3)) * Screen.height;
                safe = Rect.MinMaxRect(Mathf.Max(safe.xMin, left), Mathf.Max(safe.yMin, bottom),
                    Mathf.Min(safe.xMax, right), Mathf.Min(safe.yMax, top));
#endif
                return safe;
            }
        }
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern float NubikSafeInset(int side);
        [DllImport("__Internal")] private static extern int NubikPointerState();
        [DllImport("__Internal")] private static extern void NubikWantLock(int want);
        [DllImport("__Internal")] private static extern float NubikTakeMouseX();
        [DllImport("__Internal")] private static extern float NubikTakeMouseY();
        public static bool Locked => NubikPointerState() == 1;
        public static bool LockUnavailable => NubikPointerState() == 2;
        public static void WantLock(bool want) => NubikWantLock(want ? 1 : 0);
        /// <summary>Mouse movement in screen pixels since the last call, y up.</summary>
        public static Vector2 TakeMouseDelta() => new Vector2(NubikTakeMouseX(), -NubikTakeMouseY());
#else
        public static bool Locked => Cursor.lockState == CursorLockMode.Locked;
        public static bool LockUnavailable => false;
        public static void WantLock(bool want)
        {
            if (!want && Cursor.lockState == CursorLockMode.Locked) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        }
        /// <summary>Called from the click that starts play.</summary>
        public static void LockNow() { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
        public static Vector2 TakeMouseDelta() => new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")) * 10;
#endif
    }
}
