// Project:         Daggerfall Unity - iOS Touch Input Layer
// License:         MIT License
//
// Applies the iOS player settings from ios/IOS-BUILD.md programmatically, so nobody
// has to click through inspector panels and nothing gets forgotten.
//
// Menu:  Tools > Daggerfall Mobile > Apply iOS Player Settings
// CLI:   -executeMethod DaggerfallWorkshop.Game.Mobile.EditorTools.MobileBuildSetup.ApplyIOSSettings
//
// Deliberately uses only long-stable PlayerSettings APIs. Notably it does NOT touch
// Api Compatibility Level (the project already ships .NET Framework / level 6 and
// changing it breaks DFU) and does NOT touch bitcode (removed by Apple in Xcode 14+;
// the Unity toggle varies by version, so referencing it risks a compile error).
//
// Place in Assets/Editor/

using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace DaggerfallWorkshop.Game.Mobile.EditorTools
{
    public static class MobileBuildSetup
    {
        [MenuItem("Tools/Daggerfall Mobile/Apply iOS Player Settings")]
        public static void ApplyIOSSettings()
        {
            var log = new System.Text.StringBuilder();
            log.AppendLine("[MobileBuildSetup] Applying iOS player settings");

            // --- scripting / stripping -------------------------------------------
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.iOS, ScriptingImplementation.IL2CPP);
            log.AppendLine("  scripting backend      = IL2CPP (forced on iOS)");

            PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.iOS, ManagedStrippingLevel.Minimal);
            log.AppendLine("  managed stripping      = Minimal (with link.xml, prevents reflection stripping)");

            // MUST match Standalone. The project's GLOBAL apiCompatibilityLevel is 6 =
            // NET_Standard_2_0, and only Standalone overrides to 3. iOS therefore
            // inherited .NET Standard 2.0, where System.CodeDom does not exist - which
            // breaks Assets/Game/Addons/CSharpCompiler with 66 CS1069 errors and stops
            // Assembly-CSharp from building at all. NET_Unity_4_8 == NET_4_6 == 3.
            PlayerSettings.SetApiCompatibilityLevel(BuildTargetGroup.iOS, ApiCompatibilityLevel.NET_Unity_4_8);
            log.AppendLine("  api compatibility      = NET_Unity_4_8 (.NET Framework) - required by CSharpCompiler");

            PlayerSettings.stripEngineCode = true;
            log.AppendLine("  strip engine code      = true (engine only, not managed reflection targets)");

            // --- deployment target ------------------------------------------------
            PlayerSettings.iOS.targetOSVersionString = "13.0";
            log.AppendLine("  min iOS version        = 13.0 (repo shipped 10.0, which modern Xcode rejects)");

            PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneAndiPad;
            log.AppendLine("  target device          = iPhone + iPad");

            // --- rendering --------------------------------------------------------
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.iOS, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.iOS, new[] { GraphicsDeviceType.Metal });
            log.AppendLine("  graphics APIs          = Metal only (OpenGLES removed)");

            // --- orientation ------------------------------------------------------
            // Landscape lock is not cosmetic: WeaponManager._longestDim is cached once in
            // Start() from Mathf.Max(Screen.width, Screen.height). A portrait rotation
            // mid-session leaves the swipe-attack threshold calibrated to the wrong axis.
            // AutoRotation, NOT a fixed orientation. Setting a fixed value makes Unity
            // ignore the allowedAutorotate* flags and emit a single entry in
            // UISupportedInterfaceOrientations - so the app refuses to rotate and half of
            // all tablet users are holding it upside down. AutoRotation plus portrait
            // disabled gives both landscapes while still protecting the gesture
            // calibration (WeaponManager._longestDim is cached once in Start()).
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            log.AppendLine("  orientation            = autorotate, both landscapes, no portrait");

            PlayerSettings.statusBarHidden = true;
            log.AppendLine("  status bar             = hidden");

            // Touches that BEGIN near screen edges are delayed by iPadOS's own gesture
            // recognizers (home indicator, control centre) unless the app defers them.
            // The joysticks live in that bottom band, so without this their response is
            // intermittent - the OS eats or delays the first touch samples.
            PlayerSettings.iOS.deferSystemGesturesMode = UnityEngine.iOS.SystemGestureDeferMode.All;
            PlayerSettings.iOS.hideHomeButton = true;
            log.AppendLine("  system gestures        = deferred on all edges (joysticks live in the gesture band)");
            log.AppendLine("  home indicator         = auto-hidden");

            // --- probe define -----------------------------------------------------
            // DFU_IOS_PROBE=1 makes MobileControllerProbe self-activate, turning the build
            // into a guided controller-mapping probe. Kept as a scripting define rather than
            // a runtime toggle because the touch HUD - and with it the settings panel that
            // would carry the toggle - is hidden the moment a controller connects, which is
            // exactly when the probe is needed. Cleared on a normal build so a probe define
            // cannot survive into a release by accident.
            bool probeBuild = System.Environment.GetEnvironmentVariable("DFU_IOS_PROBE") == "1";
            const string probeDefine = "DFU_IOS_PROBE";
            string defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup.iOS);
            var defineList = new System.Collections.Generic.List<string>(
                defines.Split(new[] { ';' }, System.StringSplitOptions.RemoveEmptyEntries));
            defineList.RemoveAll(d => d.Trim() == probeDefine);
            if (probeBuild)
                defineList.Add(probeDefine);
            PlayerSettings.SetScriptingDefineSymbolsForGroup(BuildTargetGroup.iOS,
                string.Join(";", defineList.ToArray()));
            log.AppendLine("  controller probe       = " + (probeBuild ? "*** ENABLED (DFU_IOS_PROBE) ***" : "off"));

            // --- report -----------------------------------------------------------
            log.AppendLine("  api compat (verify)    = " +
                PlayerSettings.GetApiCompatibilityLevel(BuildTargetGroup.iOS));

            string linkXml = Path.Combine(Application.dataPath, "link.xml");
            log.AppendLine("  link.xml               = " + (File.Exists(linkXml) ? "present" : "*** MISSING ***"));

            string mobileDir = Path.Combine(Application.dataPath, "Scripts/Game/Mobile");
            int mobileCount = Directory.Exists(mobileDir) ? Directory.GetFiles(mobileDir, "*.cs").Length : 0;
            log.AppendLine("  mobile scripts         = " + mobileCount + " (expect 7)");

            EnsureAlwaysIncludedShaders(log);

            AssetDatabase.SaveAssets();
            Debug.Log(log.ToString());
        }

        /// <summary>
        /// The classic UI's Metal path creates its materials with Shader.Find at runtime,
        /// and nothing in the project references those shaders - so the build pipeline is
        /// free to strip them, which on device means null materials and invisible UI.
        /// Pin them into GraphicsSettings' Always Included Shaders list.
        /// </summary>
        static void EnsureAlwaysIncludedShaders(System.Text.StringBuilder log)
        {
            string[] names =
            {
                "Daggerfall/UIBlit",
                "Daggerfall/UIBlend",
                "Daggerfall/PixelFont",
                "Daggerfall/SDFFont",
            };

            var settings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
            if (settings == null || settings.Length == 0)
            {
                log.AppendLine("  ! could not open GraphicsSettings - shader pinning skipped");
                return;
            }

            SerializedObject so = new SerializedObject(settings[0]);
            SerializedProperty list = so.FindProperty("m_AlwaysIncludedShaders");
            if (list == null)
            {
                log.AppendLine("  ! m_AlwaysIncludedShaders not found - shader pinning skipped");
                return;
            }

            int added = 0;
            foreach (string name in names)
            {
                Shader shader = Shader.Find(name);
                if (shader == null)
                {
                    log.AppendLine("  ! shader not found in project: " + name);
                    continue;
                }

                bool present = false;
                for (int i = 0; i < list.arraySize; i++)
                {
                    if (list.GetArrayElementAtIndex(i).objectReferenceValue == shader)
                    {
                        present = true;
                        break;
                    }
                }
                if (present)
                    continue;

                list.InsertArrayElementAtIndex(list.arraySize);
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = shader;
                added++;
            }

            if (added > 0)
                so.ApplyModifiedProperties();

            log.AppendLine("  always-included shaders  = " + added + " newly pinned (UIBlit/UIBlend/fonts, anti-stripping)");
        }

        /// <summary>
        /// Generates the Xcode project. Unity's iOS pipeline emits an Xcode project rather
        /// than an .ipa, so this is the step that runs IL2CPP and produces something Xcode
        /// can open and sign.
        ///
        /// CLI: -executeMethod ...MobileBuildSetup.BuildIOS
        /// Output path comes from the DFU_IOS_BUILD_PATH env var, else ~/dev/dfu-ios-build.
        /// </summary>
        public static void BuildIOS()
        {
            ApplyIOSSettings();

            if (!BuildAddressables())
                return;

            string outPath = System.Environment.GetEnvironmentVariable("DFU_IOS_BUILD_PATH");
            if (string.IsNullOrEmpty(outPath))
            {
                string home = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
                outPath = Path.Combine(home, "dev", "dfu-ios-build");
            }
            Directory.CreateDirectory(outPath);

            string[] scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                Debug.LogError("[MobileBuildSetup] no enabled scenes in Build Settings - nothing to build.");
                EditorApplication.Exit(1);
                return;
            }

            Debug.Log("[MobileBuildSetup] building iOS -> " + outPath + "\n  scenes:\n    " +
                      string.Join("\n    ", scenes));

            // RELEASE by default. A Development build runs a debug transport plus
            // player-connection multicast spam - sustained extra CPU that, stacked on
            // combat rendering, starved iPadOS's thermalmonitord into a kernel watchdog
            // panic (panic-full 2026-08-19: "no successful checkins from thermalmonitord
            // in 180 seconds"). Set DFU_IOS_DEV=1 for a dev build when console debugging
            // is actually needed; Debug.Log reaches the device console either way.
            bool devBuild = System.Environment.GetEnvironmentVariable("DFU_IOS_DEV") == "1";

            BuildPlayerOptions opts = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outPath,
                target = BuildTarget.iOS,
                targetGroup = BuildTargetGroup.iOS,
                options = devBuild
                    ? BuildOptions.Development | BuildOptions.AllowDebugging
                    : BuildOptions.None,
            };
            Debug.Log("[MobileBuildSetup] build flavour = " + (devBuild ? "DEVELOPMENT" : "RELEASE"));

            UnityEditor.Build.Reporting.BuildReport report = BuildPipeline.BuildPlayer(opts);
            UnityEditor.Build.Reporting.BuildSummary summary = report.summary;

            Debug.Log(string.Format(
                "[MobileBuildSetup] build result = {0}\n  errors {1}, warnings {2}\n  size {3:0.0} MB\n  time {4}\n  output {5}",
                summary.result, summary.totalErrors, summary.totalWarnings,
                summary.totalSize / (1024f * 1024f), summary.totalTime, summary.outputPath));

            if (summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                Debug.LogError("[MobileBuildSetup] iOS build FAILED");
                EditorApplication.Exit(1);
            }
        }

        /// <summary>
        /// Builds Addressables content for the active platform.
        ///
        /// Not optional. Daggerfall Unity's localized text lives in
        /// Assets/Localization/StringTables and is loaded through Unity Localization, which
        /// resolves it via Addressables. In the editor that works off the asset database, so
        /// it looks fine - but a player build with no built Addressables content shows
        /// "{LocaleText-NotFound}" for every string, which is easy to mistake for a broken
        /// font or a missing data file.
        ///
        /// Must run with the active build target already set to the target platform, since
        /// the content is written per-platform into Assets/StreamingAssets/aa.
        /// </summary>
        [MenuItem("Tools/Daggerfall Mobile/Build Addressables")]
        public static bool BuildAddressables()
        {
            try
            {
                Debug.Log("[MobileBuildSetup] building Addressables content for " +
                          EditorUserBuildSettings.activeBuildTarget + " ...");

                UnityEditor.AddressableAssets.Settings.AddressableAssetSettings.BuildPlayerContent();

                // Addressables 1.19+ writes to Addressables.BuildPath
                // (Library/com.unity.addressables/aa/<platform>) and Unity copies it into
                // the player during BuildPlayer. Older setups wrote straight into
                // Assets/StreamingAssets/aa. Accept either, or a false negative here aborts
                // a perfectly good build.
                string projectRoot = Directory.GetParent(Application.dataPath).FullName;
                string[] candidates =
                {
                    Path.Combine(projectRoot, "Library/com.unity.addressables/aa"),
                    Path.Combine(Application.dataPath, "StreamingAssets/aa"),
                };

                foreach (string aa in candidates)
                {
                    if (!Directory.Exists(aa))
                        continue;

                    string[] files = Directory.GetFiles(aa, "*", SearchOption.AllDirectories);
                    bool catalog = false;
                    bool strings = false;
                    foreach (string file in files)
                    {
                        string leaf = Path.GetFileName(file).ToLowerInvariant();
                        if (leaf.Contains("catalog")) catalog = true;
                        if (leaf.Contains("string-tables")) strings = true;
                    }

                    Debug.Log(string.Format(
                        "[MobileBuildSetup] Addressables content: {0} files in {1}\n" +
                        "  catalog: {2}   localization string tables: {3}",
                        files.Length, aa, catalog ? "yes" : "NO", strings ? "yes" : "NO"));

                    if (!catalog)
                        continue;

                    if (!strings)
                        Debug.LogWarning("[MobileBuildSetup] no localization string-table bundle found - " +
                                         "text may still show as {LocaleText-NotFound}");
                    return true;
                }

                Debug.LogError("[MobileBuildSetup] no Addressables catalog found in any expected " +
                               "location - localized text would show as {LocaleText-NotFound}");
                return false;
            }
            catch (System.Exception ex)
            {
                Debug.LogError("[MobileBuildSetup] Addressables build failed: " + ex.Message);
                return false;
            }
        }

        [MenuItem("Tools/Daggerfall Mobile/Switch Active Target to iOS")]
        public static void SwitchToIOS()
        {
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.iOS, BuildTarget.iOS);
            Debug.Log("[MobileBuildSetup] Active build target -> iOS");
        }

        /// <summary>
        /// CLI convenience: settings + HUD in one pass.
        /// -executeMethod DaggerfallWorkshop.Game.Mobile.EditorTools.MobileBuildSetup.ApplyAll
        /// </summary>
        public const string GameScenePath = "Assets/Scenes/DaggerfallUnityGame.unity";

        public static void ApplyAll()
        {
            ApplyIOSSettings();

            // MobileHudBuilder edits the OPEN scene. In batchmode nothing is loaded, so
            // without this the HUD would be built into an empty throwaway scene and lost.
            // The game scene is the right target: GameManager, InputManager and the player
            // live here. The startup scene keeps Unity's touch-to-mouse emulation instead.
            if (!File.Exists(GameScenePath))
            {
                Debug.LogError("[MobileBuildSetup] game scene not found at " + GameScenePath +
                               " - HUD not built.");
                return;
            }

            EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            Debug.Log("[MobileBuildSetup] opened " + GameScenePath);

            MobileHudBuilder.Build();

            // Artwork last: it needs the HUD objects to exist before it can assign to them.
            MobileIconImporter.ImportAndAssign();

            EditorSceneManager.MarkAllScenesDirty();
            bool saved = EditorSceneManager.SaveOpenScenes();
            Debug.Log("[MobileBuildSetup] scene saved = " + saved);

            AssetDatabase.SaveAssets();
            Debug.Log("[MobileBuildSetup] ApplyAll complete");
        }
    }
}
