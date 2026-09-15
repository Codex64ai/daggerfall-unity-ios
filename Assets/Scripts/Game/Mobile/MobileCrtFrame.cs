// Project:         Daggerfall Unity - iOS Touch Input Layer
// License:         MIT License
//
// MOBILE: the CRT filter over the WHOLE FRAME - "the CRT doesn't affect the UI or the player first
// person sprites so it looks off" (Ikram, 2026-09-15).
//
// WHAT ESCAPED THE FILTER, AND WHY. The filter has always been one blit on the world's presentation
// (RetroPresentation.OnRenderImage, over RetroRenderer's texture in retro mode and over
// MobileCrtNative's target with retro mode off). DFU's HUD, its menus and the first-person weapon
// are not drawn by any camera: DaggerfallUI draws them in OnGUI and FPSWeapon draws the weapon
// there too, which runs AFTER every camera and every blit. No camera hook, no image effect and no
// command buffer can reach them - the frame they belong to does not exist until OnGUI has finished.
//
// SO THE PASS RUNS WHERE THE FRAME IS FINISHED: a WaitForEndOfFrame coroutine, which Unity documents
// as "after all cameras and GUI are rendered, before displaying the frame on screen". At that point
// the backbuffer holds exactly what the player is about to see. The pass copies it into a
// screen-sized render texture (ScreenCapture.CaptureScreenshotIntoRenderTexture, which is GPU-side -
// no readback, no stall), draws it back over the backbuffer through the CRT material, and at
// coverage 1 redraws the touch controls sharp on top.
//
// THE OTHER DESIGN CONSIDERED, and why it is not this one. Option B was to leave the world going
// into MobileCrtNative's render texture and REDIRECT IMGUI into it - set RenderTexture.active in an
// early-OnGUI hook and restore it in a late one, so DaggerfallUI and FPSWeapon draw into the target
// and the presenter's single blit filters world and HUD together, with no extra copy. It does not
// work, for a reason that has nothing to do with whether IMGUI can be redirected:
//
//   * the presenter is a CAMERA, and every camera renders BEFORE OnGUI. Whatever OnGUI draws into
//     the target arrives after the blit that presented it, and Camera.main clears the target at the
//     top of the next frame - so the HUD would never be presented at all, not even one frame late.
//     Moving the presentation to end-of-frame fixes that, but then option B is option A minus the
//     capture, and it has to pay for the saving twice over:
//   * Camera.main would have to render into a SCREEN-sized target for the IMGUI coordinates to land
//     where they do on screen, and "camera viewport does not work with render textures" is DFU's own
//     rule - so the docked large HUD, which today shortens the world's raster to keep its aspect,
//     would have the world stretched behind it instead. A visible regression for large-HUD players.
//   * and the touch canvas, the menus' own render order and Distant Terrain's stacked camera would
//     all have to be re-reasoned against a target that is no longer the viewport.
//
// Option A touches none of that: the frame is composed exactly as it is today, by the code that
// already knows how, and the pass filters the result. The price is one full-screen copy. In exchange
// coverage 1 and 2 switch the native path OFF (MobileCrt.NativeActive), which GIVES BACK the
// 15-21 MB viewport-sized target that path allocates - so whole-frame coverage costs less memory
// than the world-only filter it replaces, not more.
//
// WHY THE BLIT IS A GL QUAD AND NOT Graphics.Blit. Unity's own documentation: "If dest is null, the
// screen backbuffer is used as the blit destination, EXCEPT if the main camera is currently set to
// render to a RenderTexture, in which case the blit uses the render target of the main camera as
// destination." With retro mode on, Camera.main IS pointed at the retro texture at this moment, so
// Graphics.Blit(source, null, material) would filter the frame into the 320x200 retro raster instead
// of onto the screen - the filtered picture would simply never appear, and would corrupt the next
// frame's world. `RenderTexture.active = null` followed by an explicit full-screen quad says
// "the backbuffer" and means it, in every configuration, and it is also where the capture's
// orientation is compensated for.
//
// Place in Assets/Scripts/Game/Mobile/

using System.Collections;
using UnityEngine;

namespace DaggerfallWorkshop.Game.Mobile
{
    /// <summary>
    /// The end-of-frame CRT pass: filters the finished frame (world, HUD, menus, weapon) at
    /// CRTCoverage 1 and 2, and redraws the touch controls sharp over it at 1.
    /// </summary>
    public static class MobileCrtFrame
    {
        // The capture. Screen-sized, colour only - there is no depth to keep, nothing is rendered
        // into it, and it is written once a frame by the capture and read once by the quad.
        static RenderTexture capture;
        static bool active;
        static bool creationFailed;

        static readonly int mainTexId = Shader.PropertyToID("_MainTex");
        static readonly int curvatureId = Shader.PropertyToID("_Curvature");
        static readonly int scanlinesId = Shader.PropertyToID("_Scanlines");
        static readonly int scanlineCountId = Shader.PropertyToID("_ScanlineCount");
        static readonly int maskId = Shader.PropertyToID("_Mask");
        static readonly int vignetteId = Shader.PropertyToID("_Vignette");

        /// <summary>
        /// Whether the capture comes back with its rows in the opposite order to the one a texture
        /// is sampled in. ScreenCapture.CaptureScreenshotIntoRenderTexture captures in the
        /// GRAPHICS API's native framebuffer orientation rather than in texture order, and Metal
        /// (like D3D) numbers framebuffer rows from the top while a texture's v runs from the
        /// bottom - so the copy is upside down and the quad's v is run backwards to put it back.
        /// Verified from the simulator screenshots rather than assumed: with this wrong the whole
        /// frame appears mirrored top to bottom, which is not a subtle failure.
        /// </summary>
        public const bool CaptureIsFlipped = true;

        /// <summary>Whether the end-of-frame pass is running right now.</summary>
        public static bool Active
        {
            get { return active; }
        }

        /// <summary>The screen-sized capture the pass filters, or null when it is not running.</summary>
        public static RenderTexture Capture
        {
            get { return capture; }
        }

        /// <summary>
        /// Whether the player has asked for whole-frame coverage and the shader is in this build.
        /// The two settings are read first so MobileCrt.Material - which builds a Material the first
        /// time it is asked - is never touched in a session where the filter is off.
        /// </summary>
        public static bool Wanted
        {
            get
            {
                bool enabled = DaggerfallUnity.Settings.CRTFilter;
                int coverage = DaggerfallUnity.Settings.CRTCoverage;
                if (!enabled || MobileCrt.ClampCoverage(coverage) == MobileCrt.CoverageWorld)
                    return false;

                return MobileCrt.FrameActive(enabled, MobileCrt.Material != null, coverage);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Hook()
        {
            GameObject go = new GameObject("MobileCrtFrame");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<Driver>();
        }

        // ---- the pass ---------------------------------------------------------------------

        static void Pass()
        {
            bool wanted = Wanted;
            SampleCost(wanted);

            if (!wanted)
            {
                if (active || touchTaken)
                    Stop();
                // A later "on" is a new request and gets a fresh allocation attempt. This is also
                // the reset for the not-active case, where Stop() did not run: after a latched
                // failure the pass never becomes active, so this line is what clears the latch.
                creationFailed = false;
                return;
            }

            if (creationFailed)
                return;

            int w = Screen.width;
            int h = Screen.height;
            if (w < 1 || h < 1)
                return;     // iOS reports 0 for a frame or two across a rotation

            Vector2Int size = MobileCrt.NativeTargetSize(w, h);
            if (capture == null || capture.width != size.x || capture.height != size.y)
                Recreate(size);

            if (capture == null)
            {
                if (active || touchTaken)
                    Stop();
                creationFailed = true;
                return;
            }

            int coverage = MobileCrt.ClampCoverage(DaggerfallUnity.Settings.CRTCoverage);

            // Coverage 1 keeps the touch controls out of the captured frame by having them drawn by
            // a camera of our own instead of by the overlay canvas. Done BEFORE the capture, so the
            // first frame of the pass already captures a screen without them rather than one with
            // them baked in.
            if (MobileCrt.TouchControlsSharp(coverage))
                TakeTouchCanvas();
            else if (touchTaken)
                ReleaseTouchCanvas();

            Material crt = MobileCrt.Material;
            if (crt == null)
                return;

            // GPU-side: the frame never leaves the GPU and nothing stalls on a readback.
            ScreenCapture.CaptureScreenshotIntoRenderTexture(capture);

            crt.SetTexture(mainTexId, capture);
            crt.SetFloat(curvatureId, MobileCrt.ClampCurvature(DaggerfallUnity.Settings.CRTCurvature));
            crt.SetFloat(scanlinesId, MobileCrt.Clamp01(DaggerfallUnity.Settings.CRTScanlines));
            // Retro mode locks the line count to the raster (MobileCrt.FrameScanlineCountFor): the
            // backbuffer holds the retro picture upscaled, so one line per raster row lands in the
            // seams instead of beating against them.
            crt.SetFloat(scanlineCountId, MobileCrt.FrameScanlineCount(
                DaggerfallUnity.Settings.RetroRenderingMode, DaggerfallUnity.Settings.CRTScanlineCount));
            crt.SetFloat(maskId, MobileCrt.Clamp01(DaggerfallUnity.Settings.CRTMask));
            crt.SetFloat(vignetteId, MobileCrt.Clamp01(DaggerfallUnity.Settings.CRTVignette));

            DrawFullScreen(crt);

            // ...and now the controls, sharp, on the filtered frame. A camera is never redirected:
            // one with no target texture draws to the backbuffer, full stop.
            if (MobileCrt.TouchControlsSharp(coverage) && touchCamera != null && touchCanvas != null)
                touchCamera.Render();

            active = true;
        }

        /// <summary>
        /// The filtered frame, drawn over the backbuffer. RenderTexture.active = null is the
        /// backbuffer unconditionally (see the header for why Graphics.Blit is not), and the quad
        /// is in 0..1 clip space via GL.LoadOrtho, which is the shape UnityObjectToClipPos in a
        /// blit shader expects.
        /// </summary>
        static void DrawFullScreen(Material crt)
        {
            RenderTexture.active = null;

            float v0 = CaptureIsFlipped ? 1f : 0f;
            float v1 = CaptureIsFlipped ? 0f : 1f;

            crt.SetPass(0);
            GL.PushMatrix();
            GL.LoadOrtho();
            GL.Begin(GL.QUADS);
            GL.TexCoord2(0f, v0); GL.Vertex3(0f, 0f, 0f);
            GL.TexCoord2(1f, v0); GL.Vertex3(1f, 0f, 0f);
            GL.TexCoord2(1f, v1); GL.Vertex3(1f, 1f, 0f);
            GL.TexCoord2(0f, v1); GL.Vertex3(0f, 1f, 0f);
            GL.End();
            GL.PopMatrix();
        }

        static void Recreate(Vector2Int size)
        {
            ReleaseCapture();

            // No depth: nothing renders into this, it is written by the capture and read by one
            // quad. Bilinear because the barrel warp resamples (Point would crawl), and the same
            // sRGB-by-default read/write the native path uses - 8 bits of linear colour bands in
            // the darks.
            capture = new RenderTexture(size.x, size.y, 0, RenderTextureFormat.Default, RenderTextureReadWrite.Default);
            capture.name = "MobileCrtFrameCapture";
            capture.filterMode = FilterMode.Bilinear;
            capture.wrapMode = TextureWrapMode.Clamp;
            capture.antiAliasing = 1;
            capture.useMipMap = false;
            capture.autoGenerateMips = false;

            if (!capture.Create())
            {
                creationFailed = true;
                Debug.LogWarning("[CRT] could not create the " + size.x + "x" + size.y
                                 + " frame capture - whole-frame coverage stays off");
                capture = null;
                return;
            }

            // The one line the plan asks for, and the whole price of the feature in one place.
            Debug.Log(string.Format("[CRT] frame pass {0}x{1} (coverage {2}; {3:0.0} MB capture)",
                size.x, size.y, MobileCrt.ClampCoverage(DaggerfallUnity.Settings.CRTCoverage),
                MobileCrt.NativeTargetBytes(size.x, size.y) / (1024f * 1024f)));
        }

        static void ReleaseCapture()
        {
            if (capture == null)
                return;

            RenderTexture was = capture;
            capture = null;
            if (RenderTexture.active == was)
                RenderTexture.active = null;
            was.Release();
            Object.Destroy(was);
        }

        static void Stop()
        {
            active = false;
            ReleaseTouchCanvas();
            ReleaseCapture();
            creationFailed = false;
        }

        // ---- the touch canvas at coverage 1 -------------------------------------------------

        static Canvas touchCanvas;
        static Camera touchCamera;
        static bool touchTaken;
        static RenderMode touchModeWas;
        static Camera touchWorldCameraWas;
        static float touchPlaneWas;
        static int touchLayerWas;
        static bool worldMaskCleared;

        /// <summary>The camera coverage 1 draws the touch controls with, or null.</summary>
        public static Camera TouchCamera
        {
            get { return touchCamera; }
        }

        static void TakeTouchCanvas()
        {
            if (touchTaken && touchCanvas != null && touchCamera != null)
            {
                // Re-asserted every frame, both of them cheap. The world camera is a prefab
                // instance whose serialized mask has the HUD bit set, so anything that respawns the
                // player brings the bit back and Camera.main starts drawing a screen-sized quad a
                // hundred units in front of the HUD camera. And the layer has to reach graphics the
                // runtime UI builders add after the canvas was taken.
                ClearWorldMask();
                ApplyLayer(touchCanvas.gameObject, MobileCrt.TouchUILayer);
                return;
            }

            if (touchCanvas == null)
                touchCanvas = FindTouchCanvas();
            if (touchCanvas == null)
                return;

            if (touchCamera == null)
                touchCamera = BuildTouchCamera();
            if (touchCamera == null)
                return;

            touchModeWas = touchCanvas.renderMode;
            touchWorldCameraWas = touchCanvas.worldCamera;
            touchPlaneWas = touchCanvas.planeDistance;
            touchLayerWas = touchCanvas.gameObject.layer;

            ApplyLayer(touchCanvas.gameObject, MobileCrt.TouchUILayer);
            touchCanvas.renderMode = RenderMode.ScreenSpaceCamera;
            touchCanvas.worldCamera = touchCamera;
            touchCanvas.planeDistance = TouchPlaneDistance;
            ClearWorldMask();

            touchTaken = true;
        }

        static void ReleaseTouchCanvas()
        {
            if (!touchTaken)
                return;

            touchTaken = false;

            if (touchCanvas != null)
            {
                // Overlay again, and back on the layer it was built on: a canvas left on layer 5
                // with the world camera no longer rendering that layer is invisible to nothing, but
                // a MOD that puts world objects there would find them gone.
                touchCanvas.renderMode = touchModeWas;
                touchCanvas.worldCamera = touchWorldCameraWas;
                touchCanvas.planeDistance = touchPlaneWas;
                ApplyLayer(touchCanvas.gameObject, touchLayerWas);
            }

            RestoreWorldMask();
        }

        /// <summary>Canvas plane distance, well inside the HUD camera's clip range.</summary>
        public const float TouchPlaneDistance = 100f;

        /// <summary>
        /// The touch HUD's canvas. MobileHudLayout holds the reference the builder wired, and
        /// FindObjectsOfTypeAll is what sees it while the HUD is hidden (an inactive GameObject is
        /// invisible to FindObjectOfType, and the HUD is hidden whenever a classic window is open).
        /// </summary>
        static Canvas FindTouchCanvas()
        {
            foreach (MobileHudLayout layout in Resources.FindObjectsOfTypeAll<MobileHudLayout>())
            {
                if (layout != null && layout.canvas != null && layout.gameObject.scene.IsValid())
                    return layout.canvas;
            }
            return null;
        }

        static Camera BuildTouchCamera()
        {
            GameObject go = new GameObject("MobileCrtTouchCamera");
            Object.DontDestroyOnLoad(go);
            Camera cam = go.AddComponent<Camera>();

            // DISABLED on purpose: a screen-space-camera canvas is drawn as part of its camera's
            // render, and this camera's render is the one thing that must happen AFTER the filtered
            // frame is on the backbuffer. So Unity never runs it and the pass calls Render() itself.
            cam.enabled = false;
            cam.clearFlags = CameraClearFlags.Depth;    // the filtered world underneath must survive
            cam.cullingMask = MobileCrt.TouchUILayerMask;
            cam.depth = 100f;
            cam.orthographic = false;
            cam.fieldOfView = 60f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = TouchPlaneDistance * 2f;
            cam.rect = new Rect(0f, 0f, 1f, 1f);        // whatever ViewportChanger does to Camera.main
            cam.targetTexture = null;                   // a target here would put the HUD back inside the picture
            cam.allowHDR = false;
            cam.allowMSAA = false;
            cam.useOcclusionCulling = false;
            cam.depthTextureMode = DepthTextureMode.None;
            return cam;
        }

        static void ClearWorldMask()
        {
            Camera world = GameManager.HasInstance ? GameManager.Instance.MainCamera : Camera.main;
            if (world == null)
                return;

            int mask = world.cullingMask;
            if ((mask & MobileCrt.TouchUILayerMask) == 0)
                return;

            worldMaskCleared = true;
            world.cullingMask = MobileCrt.WithoutTouchUILayer(mask);
        }

        static void RestoreWorldMask()
        {
            if (!worldMaskCleared)
                return;

            worldMaskCleared = false;
            Camera world = GameManager.HasInstance ? GameManager.Instance.MainCamera : Camera.main;
            if (world != null)
                world.cullingMask |= MobileCrt.TouchUILayerMask;
        }

        /// <summary>
        /// Puts a whole subtree on one layer. Every graphic the HUD camera has to draw must be on
        /// the one layer it renders, however deep it sits - and the runtime UI builders
        /// (MobileLayoutEditor, MobileSettingsPanel) create their objects on their parent's layer,
        /// so anything they add after this inherits it.
        /// </summary>
        static void ApplyLayer(GameObject root, int layer)
        {
            if (root == null)
                return;

            root.layer = layer;
            Transform t = root.transform;
            for (int i = 0; i < t.childCount; i++)
                ApplyLayer(t.GetChild(i).gameObject, layer);
        }

        // ---- the cost line -------------------------------------------------------------------
        // One line per side of a toggle, and nothing at all in a session that never turns the
        // filter on: the OFF window is only reported once the pass has run at least once, so the
        // pair that appears in a log is always "with it" and "without it" from the same session on
        // the same scene.

        const int CostWindow = 300;

        static bool costStateWasOn;
        static bool costReported;
        static bool costEverOn;
        static int costFrames;
        static float costSeconds;

        static void SampleCost(bool on)
        {
            if (on != costStateWasOn)
            {
                costStateWasOn = on;
                costReported = false;
                costFrames = 0;
                costSeconds = 0f;
                if (on)
                    costEverOn = true;
            }

            if (costReported)
                return;

            costFrames++;
            costSeconds += Time.unscaledDeltaTime;
            if (costFrames < CostWindow)
                return;

            costReported = true;
            if (!on && !costEverOn)
                return;     // a session that never switched it on says nothing

            Debug.Log(string.Format("[CRT] frame pass {0}: mean {1:0.00} ms over {2} frames",
                on ? "ON (coverage " + MobileCrt.ClampCoverage(DaggerfallUnity.Settings.CRTCoverage) + ")" : "OFF",
                costSeconds / costFrames * 1000f, costFrames));
        }

        /// <summary>
        /// The end-of-frame driver. WaitForEndOfFrame is the whole reason this is a coroutine and
        /// not a LateUpdate: it is the only point in the loop that is after OnGUI, which is where
        /// DFU's HUD, its menus and the first-person weapon are drawn.
        /// </summary>
        class Driver : MonoBehaviour
        {
            IEnumerator Start()
            {
                WaitForEndOfFrame endOfFrame = new WaitForEndOfFrame();
                while (true)
                {
                    yield return endOfFrame;
                    Pass();
                }
            }

            void OnDisable()
            {
                if (active || touchTaken)
                    Stop();
            }
        }
    }
}
