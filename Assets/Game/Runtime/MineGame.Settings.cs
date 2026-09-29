using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Nubik
{
    /// <summary>
    /// Graphics levels and the camera's smoothing. Low: lower resolution, no shadows, cheaper textures. Normal: the
    /// balanced default. Ultra: full resolution, anti-aliasing, soft shadows further away and more lamp light.
    /// </summary>
    public sealed partial class MineGame
    {
        private float eyeLift;
        private UniversalRenderPipelineAsset pipeline;

        private void ApplyGraphics()
        {
            int level = GameSettings.Current.quality;
            // The browser paces frames best on its own; a fixed target makes Unity WebGL fall back to timers and stutter.
            Application.targetFrameRate = Application.platform == RuntimePlatform.WebGLPlayer ? -1 : 60;
            Shader.SetGlobalFloat("_NubikLowDetail", level == GameSettings.Low ? 1 : 0);
            sun.shadows = level == GameSettings.Low ? LightShadows.None : level == GameSettings.Ultra ? LightShadows.Soft : LightShadows.Hard;
            view.farClipPlane = level == GameSettings.Low ? 150 : 220;
            yard?.SetDetail(level);
#if !UNITY_EDITOR
            // The pipeline asset is shared project data in the editor; only a player may change it at run time.
            if (pipeline == null) pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (pipeline == null) return;
            pipeline.renderScale = level == GameSettings.Low ? .65f : level == GameSettings.Normal ? .85f : 1f;
            pipeline.msaaSampleCount = level == GameSettings.Ultra ? 4 : 1;
            pipeline.supportsHDR = false;
            pipeline.shadowDistance = level == GameSettings.Ultra ? 70 : 45;
            pipeline.maxAdditionalLightsCount = level == GameSettings.Low ? 2 : level == GameSettings.Normal ? 4 : 8;
#endif
        }

        /// <summary>
        /// Steps up and down small bumps of the dug ground move the body in jumps; the eye follows them smoothly, so the
        /// view does not jerk while walking over uneven earth.
        /// </summary>
        private void SmoothEye(float previousFeet, bool grounded, float dt)
        {
            float moved = body.transform.position.y - previousFeet;
            if (grounded && Mathf.Abs(moved) < .5f && Mathf.Abs(moved) > .015f) eyeLift -= moved;
            else if (!grounded) eyeLift = Mathf.MoveTowards(eyeLift, 0, dt * 4);
            eyeLift = Mathf.Clamp(eyeLift, -.5f, .5f);
            eyeLift = Mathf.Lerp(eyeLift, 0, 1 - Mathf.Exp(-14 * dt));
        }

        /// <summary>Where the eye sits over the feet this frame: the smoothed step and the shake of slams and blasts.</summary>
        private Vector3 EyeOffset(float shake) => Vector3.up * (EyeHeight + eyeLift) + (Vector3)Random.insideUnitCircle * shake * .07f;
    }
}
