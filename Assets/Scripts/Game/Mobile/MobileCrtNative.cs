// Project:         Daggerfall Unity - iOS Touch Input Layer
// License:         MIT License
//
// MOBILE: the CRT filter with retro mode OFF - "I want it during the full game as well and just
// as an option ... I don't want it to be tied down to one view" (Ikram, 2026-09-10).
//
// WHAT THIS DOES. Retro mode already owns a three-stage chain: Camera.main renders into a small
// render texture, RetroRenderer blits that into a 640x400 presentation texture, and
// RetroPresentation's own camera blits THAT to the backbuffer - which is where the CRT shader
// hooks in. With retro mode off there is no chain at all: Camera.main draws straight to the
// backbuffer and RetroPresentation is switched off. This file rebuilds the last stage of the
// chain at native resolution: it hands Camera.main a viewport-sized render texture, switches the
// presenter back on, and points it at that texture. From every other component's point of view
// the game is now "rendering into a texture", which is a state DFU, Distant Terrain, Dynamic
// Skies and the classic sky rig all already handle - because retro mode has always put them in it.
//
// WHY NOT AN IMAGE EFFECT ON THE MAIN CAMERA (design option B), OR A COMMAND BUFFER (C). Both
// leave the world in the backbuffer and try to filter it there, and both then have to answer the
// same two questions this file answers by construction:
//
//   1. THE VIEWPORT. With the large HUD docked, ViewportChanger gives Camera.main a partial
//      camera.rect. An OnRenderImage effect on a partial-rect camera is the configuration DFU's
//      own code calls out as not working ("Camera viewport does not work with render textures ...
//      need to adjust output to appropriately size render target instead", ViewportChanger.cs),
//      and getting the curvature and vignette to centre on the viewport rather than the screen
//      means re-deriving the mapping from camera.pixelRect and hoping Unity's intermediate is
//      sized the way you assumed. Here the render target IS the viewport, to the pixel, and the
//      presenter blits it into exactly the rect it was sized from: 1:1, nothing to derive.
//   2. CAMERA STACKING. Distant Terrain renders the far terrain from a SECOND camera at a lower
//      depth than Camera.main, and Dynamic Skies puts the skybox clear on whichever of the two
//      renders first. An effect on Camera.main sees only Camera.main's own intermediate; whether
//      the stacked camera's output is in it is Unity-version-dependent behaviour that would have
//      to be re-verified on every upgrade. Distant Terrain already copies Camera.main.targetTexture
//      onto its stacked camera (that is how it survives retro mode), so pointing Camera.main at a
//      texture puts BOTH cameras in it and the filter sees the finished composite. Free.
//
// WHAT IT COSTS. One render target the size of the viewport, colour plus depth: about 31 MB on an
// 11" iPad (2360x1640) and about 45 MB on a 12.9" (2732x2048), allocated only while the filter is
// on with retro mode off, released the moment it goes off. Plus one full-screen blit, which is the
// same blit the retro path already pays. There is no second copy of the frame and no grab pass.
//
// KNOWN EDGES, recorded rather than hidden:
//  - Screen-space maths that assumes "retro off means no target texture" is off by the docked HUD's
//    height while this path runs: PlayerActivate's cursor ray (mouse only - the touch layer does not
//    use it) and HUDPlaceMarker's quest-marker labels. Undocked, the target is the whole screen and
//    both are exact.
//  - The presenter camera is pushed to Camera.main.depth + 1 so it always presents after the world
//    is drawn; the retro path's own depth is left exactly as it was.
//
// Place in Assets/Scripts/Game/Mobile/

using UnityEngine;
using DaggerfallWorkshop.Utility;

namespace DaggerfallWorkshop.Game.Mobile
{
    /// <summary>
    /// Owns the native-resolution render target that lets the CRT filter run with retro mode off.
    /// </summary>
    public static class MobileCrtNative
    {
        static RenderTexture target;
        static bool active;

        static RetroPresentation presenter;
        static Camera presenterCamera;
        static RenderTexture presenterSourceWasAt;
        static float presenterDepthWas;
        static bool presenterStateSaved;

        /// <summary>Whether the native path is running right now: Camera.main is pointed at
        /// <see cref="Target"/> and the presenter is blitting it through the CRT material.</summary>
        public static bool Active
        {
            get { return active; }
        }

        /// <summary>The native path's render target, or null when the path is not running.</summary>
        public static RenderTexture Target
        {
            get { return target; }
        }

        /// <summary>
        /// Whether the player has asked for the native path and the shader is in this build. Does
        /// not say whether it is running - <see cref="Active"/> says that, and it only becomes true
        /// once there is a main camera and a presenter to drive.
        /// </summary>
        public static bool Wanted
        {
            get
            {
                // The two settings are read first so that MobileCrt.Material - which builds a
                // Material the first time it is asked - is never touched in a session where the
                // filter is off, which is every session by default.
                bool enabled = DaggerfallUnity.Settings.CRTFilter;
                int retroMode = DaggerfallUnity.Settings.RetroRenderingMode;
                if (!enabled || retroMode != 0)
                    return false;

                return MobileCrt.NativeActive(enabled, retroMode, MobileCrt.Material != null);
            }
        }

        /// <summary>
        /// Re-asserts Camera.main's target texture if the native path is running, and says whether
        /// it did. RetroRenderer.UpdateRenderTarget calls this before it clears the target texture
        /// on the retro-off branch, so that a viewport change (which is what makes ViewportChanger
        /// call UpdateRenderTarget) cannot pull the target out from under this path mid-frame.
        /// </summary>
        public static bool ReassertTarget()
        {
            if (!active || target == null || !GameManager.HasInstance)
                return false;

            Camera main = GameManager.Instance.MainCamera;
            if (main == null)
                return false;

            if (main.targetTexture != target)
                main.targetTexture = target;
            return true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Hook()
        {
            GameObject go = new GameObject("MobileCrtNative");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<Driver>();
        }

        /// <summary>
        /// One frame of the native path. Driven from LateUpdate, which is after ViewportChanger has
        /// set this frame's camera rects and before any camera renders, so the target created here
        /// is the size of the rectangle this frame will actually blit it into.
        /// </summary>
        static void Tick()
        {
            // Cheapest test first, and deliberately: this runs every frame for the whole session,
            // and for almost every player it is three field reads that answer "no" - the settings
            // lookup short-circuits before anything searches the scene for a presenter.
            bool wanted = Wanted
                          && GameManager.HasInstance
                          && GameManager.Instance.MainCamera != null
                          && ResolvePresenter() != null;

            if (!wanted)
            {
                if (active)
                    Stop();
                return;
            }

            Camera main = GameManager.Instance.MainCamera;

            // The presenter has to be alive before its camera can be asked how big its viewport is,
            // so the first frame of the path sizes the target from the whole screen and the second
            // corrects it if a docked large HUD has taken the bottom. Costs one reallocation the
            // first time the filter is switched on with the HUD docked, and nothing after.
            if (!presenter.gameObject.activeSelf)
                presenter.gameObject.SetActive(true);

            Vector2Int size = presenterCamera != null && presenterCamera.pixelWidth > 0 && presenterCamera.pixelHeight > 0
                ? MobileCrt.NativeTargetSize(presenterCamera.pixelWidth, presenterCamera.pixelHeight)
                : MobileCrt.NativeTargetSize(Screen.width, Screen.height);

            if (target == null || target.width != size.x || target.height != size.y)
                Recreate(size);

            if (target == null)
            {
                if (active)
                    Stop();
                return;
            }

            SavePresenterState();

            // Camera.main renders into the target, at full rect: a viewport rect on a camera with a
            // target texture is the case DFU itself documents as not working, which is why the
            // target is sized to the viewport instead. ViewportChanger has the matching branch.
            if (main.targetTexture != target)
                main.targetTexture = target;
            if (main.rect != fullRect)
                main.rect = fullRect;

            // ... and the presenter blits it to the backbuffer, after the world is drawn. Distant
            // Terrain moves Camera.main to depth 3, so the presenter's serialized 0 would present
            // last frame's picture; tracking main's depth keeps it behind the world in every
            // configuration. The retro path never reaches here and keeps its own depth.
            if (presenterCamera != null)
            {
                float presentDepth = main.depth + 1f;
                if (presenterCamera.depth != presentDepth)
                    presenterCamera.depth = presentDepth;
            }

            presenter.RetroPresentationSource = target;

            // The classic sky rig draws from its own camera at a lower depth than main. RetroRenderer
            // does this for retro mode in its Update, which returns early while retro mode is off.
            RouteSkyCamera(target);

            active = true;
        }

        static readonly Rect fullRect = new Rect(0, 0, 1, 1);

        static void Recreate(Vector2Int size)
        {
            Release();

            // Depth 24 because this is the world's own depth buffer, not a presentation copy, and
            // sRGB (RenderTextureReadWrite.Default in this Linear project) because 8 bits of LINEAR
            // colour bands visibly in the darks - the retro path gets away with a linear target only
            // because it is 320x200 through a 259-colour palette. The shader is unaffected either
            // way: tex2D hands it linear light in both cases.
            target = new RenderTexture(size.x, size.y, 24, RenderTextureFormat.Default, RenderTextureReadWrite.Default);
            target.name = "MobileCrtNativeTarget";
            target.filterMode = FilterMode.Bilinear;    // the barrel warp resamples; Point would crawl
            target.wrapMode = TextureWrapMode.Clamp;
            target.antiAliasing = 1;                    // Camera.main is m_AllowMSAA 0
            target.useMipMap = false;
            target.autoGenerateMips = false;

            if (!target.Create())
            {
                Debug.LogWarning("[CRT] could not create the " + size.x + "x" + size.y + " native target - CRT filter stays off with retro mode off");
                target = null;
                return;
            }

            Debug.Log(string.Format("[CRT] native target {0}x{1} ({2:0.0} MB colour+depth)",
                size.x, size.y, MobileCrt.NativeTargetBytes(size.x, size.y) / (1024f * 1024f)));
        }

        static void Release()
        {
            if (target == null)
                return;

            if (GameManager.HasInstance && GameManager.Instance.MainCamera != null
                && GameManager.Instance.MainCamera.targetTexture == target)
                GameManager.Instance.MainCamera.targetTexture = null;

            RouteSkyCamera(null);

            // Every OTHER camera that was pointed here too. This is not defensive
            // over-engineering: Distant Terrain copies Camera.main.targetTexture onto its stacked
            // camera and only notices a change on its next Update, so destroying the texture first
            // would leave that camera rendering into a destroyed target for a frame. The list is
            // half a dozen cameras and this runs once, when the filter is switched off.
            foreach (Camera other in Camera.allCameras)
            {
                if (other != null && other.targetTexture == target)
                    other.targetTexture = null;
            }

            target.Release();
            Object.Destroy(target);
            target = null;
        }

        /// <summary>
        /// Puts everything back the way retro-mode-off expects to find it: no target texture, the
        /// presenter pointed at RetroRenderer's own presentation texture and switched off unless
        /// retro mode wants it, its depth restored, and the render target freed.
        /// </summary>
        static void Stop()
        {
            active = false;
            Release();

            if (presenter != null)
            {
                if (presenterStateSaved)
                {
                    presenter.RetroPresentationSource = presenterSourceWasAt;
                    if (presenterCamera != null)
                        presenterCamera.depth = presenterDepthWas;
                }
                presenter.gameObject.SetActive(DaggerfallUnity.Settings.RetroRenderingMode != 0);
            }

            RestoreMainCameraRect();

            presenterStateSaved = false;
        }

        /// <summary>
        /// Gives Camera.main its docked-large-HUD viewport back. ViewportChanger will not do it:
        /// it early-outs on `rect == lastViewportRect`, and while this path ran it was told the
        /// docked rect (which it recorded) and then set the camera to the full rect instead. So the
        /// frame after the filter goes off, the world would be drawn over the docked HUD until
        /// something else changed the viewport. Undocked this is the full rect and a no-op.
        /// </summary>
        static void RestoreMainCameraRect()
        {
            if (!GameManager.HasInstance || GameManager.Instance.MainCamera == null)
                return;

            float hud = 0f;
            if (DaggerfallUnity.Settings.LargeHUD && DaggerfallUnity.Settings.LargeHUDDocked
                && DaggerfallUI.HasInstance && DaggerfallUI.Instance.DaggerfallHUD != null)
                hud = DaggerfallUI.Instance.DaggerfallHUD.LargeHUD.ScreenHeight;

            Rect rect = MobileCrt.DockedViewportRect(Screen.height, hud);
            if (GameManager.Instance.MainCamera.rect != rect)
                GameManager.Instance.MainCamera.rect = rect;
        }

        static void SavePresenterState()
        {
            if (presenterStateSaved || presenter == null)
                return;

            presenterStateSaved = true;
            presenterSourceWasAt = presenter.RetroPresentationSource;
            presenterDepthWas = presenterCamera != null ? presenterCamera.depth : 0f;

            // RetroPresentationSource is a scene reference to RetroRenderer's presentation texture.
            // If it was somehow null (a mod that re-points the chain, or an early frame), restore to
            // the renderer's own target rather than to null, or turning the filter off with retro
            // mode ON would leave the presenter with nothing to blit.
            if (presenterSourceWasAt == null && GameManager.HasInstance && GameManager.Instance.RetroRenderer != null)
                presenterSourceWasAt = GameManager.Instance.RetroRenderer.RetroPresentationTarget;
        }

        static void RouteSkyCamera(RenderTexture to)
        {
            if (!GameManager.HasInstance)
                return;

            DaggerfallSky sky = GameManager.Instance.SkyRig;
            if (sky == null || sky.SkyCamera == null)
                return;

            if (sky.SkyCamera.targetTexture != to)
                sky.SkyCamera.targetTexture = to;
        }

        /// <summary>
        /// The presenter, cached. GameManager finds it with FindObjectOfType, which skips INACTIVE
        /// objects - and the presenter is exactly that whenever retro mode is off, which is the only
        /// state this path ever runs in. FindObjectsOfTypeAll is the fallback that does see it.
        /// </summary>
        static RetroPresentation ResolvePresenter()
        {
            if (presenter != null)
                return presenter;

            if (GameManager.HasInstance)
                presenter = GameManager.Instance.RetroPresenter;

            if (presenter == null)
            {
                RetroPresentation[] all = Resources.FindObjectsOfTypeAll<RetroPresentation>();
                foreach (RetroPresentation candidate in all)
                {
                    if (candidate != null && candidate.gameObject.scene.IsValid())
                    {
                        presenter = candidate;
                        break;
                    }
                }
            }

            presenterCamera = presenter != null ? presenter.GetComponent<Camera>() : null;
            return presenter;
        }

        class Driver : MonoBehaviour
        {
            void LateUpdate()
            {
                Tick();
            }

            void OnDisable()
            {
                if (active)
                    Stop();
            }
        }
    }
}
