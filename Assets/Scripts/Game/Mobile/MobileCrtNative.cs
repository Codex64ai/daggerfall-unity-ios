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
// WHAT IT COSTS. One render target the size of the viewport. The COLOUR surface is the whole price
// in system memory - about 15 MB on an 11" iPad (2360x1640) and about 21 MB on a 12.9" (2732x2048) -
// because the depth surface is declared RenderTextureMemoryless.Depth and therefore lives only in
// the GPU's tile memory on Metal: nothing ever samples it, and both cameras that write into this
// target clear depth on entry, so there is no cross-pass dependency to preserve. (On a desktop
// graphics API the hint is ignored and depth costs its 4 bytes a pixel; iOS is what this is for.)
// Allocated only while the filter is on with retro mode off, released the moment it goes off. Plus
// one full-screen blit, which is the same blit the retro path already pays. There is no second copy
// of the frame and no grab pass.
//
// KNOWN EDGES, recorded rather than hidden:
//  - Screen-space maths that assumed "retro off means no target texture" was off by the docked HUD's
//    height while this path runs. Both sites - PlayerActivate's cursor ray and HUDPlaceMarker's
//    quest-marker labels - now gate on `mainCamera.targetTexture != null`, which is the fact rather
//    than a proxy for it, and their existing docked-HUD maths then covers this path too. Undocked,
//    the target is the whole screen and both were always exact.
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

        // Latched when RenderTexture.Create() fails, and cleared only when the player stops asking
        // for the path. Without it a failed 15-25 MB allocation is retried every LateUpdate for the
        // rest of the session - 60 allocation attempts and 60 log lines a second, under exactly the
        // memory pressure that made the first one fail, which is how a recoverable warning becomes a
        // jetsam kill. One attempt, one warning, then silence until the filter is switched off and on.
        static bool creationFailed;

        static DaggerfallSky skyRig;

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
                // The player has switched the filter (or retro mode) off, so a later "on" is a new
                // request and gets a fresh attempt. This is also the reset for the not-active case,
                // where Stop() above did not run: after a latched failure the path never becomes
                // active, so Stop() is not what clears the latch - this line is.
                creationFailed = false;
                return;
            }

            // Latched: the allocation failed once and the settings have not changed since. Bail
            // before touching the presenter, so the failure leaves the scene exactly as it found it.
            if (creationFailed)
                return;

            Camera main = GameManager.Instance.MainCamera;

            // The presenter's camera is asked how big its viewport is before its GameObject is
            // switched on - a Camera reports pixelWidth/Height from its rect and the screen whether
            // or not it is enabled, and activating it here would not run its ViewportChanger until
            // the next frame anyway. So the first frame of the path sizes the target from the whole
            // screen and the second corrects it if a docked large HUD has taken the bottom. Costs
            // one reallocation the first time the filter is switched on with the HUD docked.
            Vector2Int size = presenterCamera != null && presenterCamera.pixelWidth > 0 && presenterCamera.pixelHeight > 0
                ? MobileCrt.NativeTargetSize(presenterCamera.pixelWidth, presenterCamera.pixelHeight)
                : MobileCrt.NativeTargetSize(Screen.width, Screen.height);

            if (target == null || target.width != size.x || target.height != size.y)
                Recreate(size);

            if (target == null)
            {
                // Stop() clears the latch, because its usual caller is "the player switched it off".
                // This caller is not that: re-latch after it, or a failed RE-allocation (the path was
                // already running when the viewport changed) would loop.
                if (active)
                    Stop();
                creationFailed = true;
                return;
            }

            // Only now that there is a target worth presenting. The failure path above leaves the
            // presenter switched off, as retro-mode-off found it.
            if (!presenter.gameObject.activeSelf)
                presenter.gameObject.SetActive(true);

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

            // The depth surface never has to reach system memory on Metal, and it is half the naive
            // cost of this feature. Nothing samples it, MSAA is off (the other precondition), and
            // both cameras that render into this target clear depth on entry - Distant Terrain sets
            // clearFlags = Depth on Camera.main and on its stacked camera - so there is no
            // cross-pass depth dependency to preserve. Must be set before Create(); ignored by
            // graphics APIs that have no tile memory, where depth costs what it always did.
            target.memorylessMode = RenderTextureMemoryless.Depth;

            if (!target.Create())
            {
                // Once, and once only: the latch below is what stops the next LateUpdate coming
                // straight back here. It is raised again in Tick after the Stop() that follows a
                // failed re-allocation, because Stop() is also the "player switched it off" reset.
                creationFailed = true;
                Debug.LogWarning("[CRT] could not create the " + size.x + "x" + size.y + " native target - CRT filter stays off with retro mode off");
                target = null;
                return;
            }

            Debug.Log(string.Format("[CRT] native target {0}x{1} ({2:0.0} MB colour; depth memoryless on Metal)",
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
            //
            // FindObjectsOfTypeAll, not Camera.allCameras: the latter is documented as all ENABLED
            // cameras, and a camera that holds this target while its component or GameObject is
            // disabled is exactly the one that would keep a dangling reference across the Destroy.
            // (Distant Terrain's stacked camera is the real candidate.) The scene check is what
            // keeps this off camera components that live in loaded prefab ASSETS rather than in the
            // scene - they cannot be holding a runtime texture, but nothing here should touch them.
            foreach (Camera other in Resources.FindObjectsOfTypeAll<Camera>())
            {
                if (other != null && other.targetTexture == target && other.gameObject.scene.IsValid())
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

            // Switching the filter off and on is a new request and deserves a fresh allocation
            // attempt. Tick re-raises this immediately when the caller was a failed allocation
            // rather than the player.
            creationFailed = false;
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

            // Cached, and resolved through the NON-throwing overload. GameManager.SkyRig uses
            // GetMonoBehaviour<DaggerfallSky>() with errorIfNotFound defaulted to true, which logs
            // an error and THROWS when there is no sky rig; every other GameManager accessor this
            // path uses is the tolerant one, and this is a per-frame call on a path the player can
            // switch on at any moment. Unity's == sees a destroyed rig as null, so a scene change
            // re-resolves rather than sticking to a corpse.
            if (skyRig == null)
                skyRig = GameManager.GetMonoBehaviour<DaggerfallSky>(false);

            if (skyRig == null || skyRig.SkyCamera == null)
                return;

            if (skyRig.SkyCamera.targetTexture != to)
                skyRig.SkyCamera.targetTexture = to;
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

            // The cached presenter is gone (a scene change; Unity's == sees the destroyed component
            // as null), and so is everything we saved ABOUT it. presenterSourceWasAt in particular
            // is a RenderTexture belonging to the previous scene's RetroRenderer: leave it here and
            // the next Stop() writes a destroyed texture into the NEW presenter, which then blits
            // nothing - a black world under a live HUD until retro mode is toggled. Dropping it lets
            // SavePresenterState's own null-source fallback repopulate it from the new scene's
            // RetroPresentationTarget on the next frame, which is exactly what that fallback is for.
            presenterStateSaved = false;
            presenterSourceWasAt = null;
            presenterDepthWas = 0f;

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
