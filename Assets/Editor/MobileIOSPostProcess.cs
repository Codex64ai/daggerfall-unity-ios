// Project:         Daggerfall Unity - iOS Touch Input Layer
// License:         MIT License
//
// Adds the Info.plist keys the port needs. Runs automatically after every iOS build.
//
// The project's existing Assets/Editor/PostProcessBuild.cs only handles Standalone
// targets, so nothing was touching the iOS plist before this.
//
// Place in Assets/Editor/

using UnityEngine;
using UnityEditor;
using UnityEditor.Callbacks;
#if UNITY_IOS
using UnityEditor.iOS.Xcode;
using System.IO;
#endif

namespace DaggerfallWorkshop.Game.Mobile.EditorTools
{
    public static class MobileIOSPostProcess
    {
        [PostProcessBuild(100)]
        public static void OnPostprocessBuild(BuildTarget target, string pathToBuiltProject)
        {
            if (target != BuildTarget.iOS)
                return;

#if UNITY_IOS
            string plistPath = Path.Combine(pathToBuiltProject, "Info.plist");
            if (!File.Exists(plistPath))
            {
                Debug.LogWarning("[MobileIOSPostProcess] Info.plist not found at " + plistPath);
                return;
            }

            PlistDocument plist = new PlistDocument();
            plist.ReadFromFile(plistPath);
            PlistElementDict root = plist.root;

            // --- game data sharing -------------------------------------------------
            // The app ships without Daggerfall's data (it is Bethesda's copyright), so the
            // player must be able to copy their own arena2 folder in. These two keys expose
            // the app's Documents directory to Finder and the Files app respectively.
            // Without BOTH, there is no way to get data onto the device short of a rebuild.
            root.SetBoolean("UIFileSharingEnabled", true);
            root.SetBoolean("LSSupportsOpeningDocumentsInPlace", true);

            // --- layout ------------------------------------------------------------
            // Split View / Slide Over resizes the window arbitrarily. The touch HUD is laid
            // out against a fixed reference resolution and the swipe threshold is calibrated
            // from screen dimensions, so a resizable window breaks both.
            root.SetBoolean("UIRequiresFullScreen", true);

            // --- convenience -------------------------------------------------------
            // Declares no non-exempt encryption, which skips the export-compliance
            // questionnaire on every TestFlight/App Store upload.
            root.SetBoolean("ITSAppUsesNonExemptEncryption", false);

            plist.WriteToFile(plistPath);

            // --- Xcode 16+ compatibility --------------------------------------------
            // Unity 2022's generated project predates two Xcode defaults that break it:
            //   1. The module verifier rejects UnityFramework's headers outright
            //      ("double-quoted include in framework header", "umbrella header does
            //      not include..."), failing the build with no code at fault.
            //   2. User-script sandboxing denies Unity's IL2CPP run-script phase read
            //      access outside the sandbox ("Sandbox: il2cpp deny(1) file-read-data").
            // Both are build settings, so switch them off on every target.
            string pbxPath = PBXProject.GetPBXProjectPath(pathToBuiltProject);
            PBXProject pbx = new PBXProject();
            pbx.ReadFromFile(pbxPath);

            string[] guids =
            {
                pbx.GetUnityMainTargetGuid(),
                pbx.GetUnityFrameworkTargetGuid(),
            };
            foreach (string guid in guids)
            {
                pbx.SetBuildProperty(guid, "ENABLE_USER_SCRIPT_SANDBOXING", "NO");
                pbx.SetBuildProperty(guid, "ENABLE_MODULE_VERIFIER", "NO");
                pbx.SetBuildProperty(guid, "CLANG_WARN_QUOTED_INCLUDE_IN_FRAMEWORK_HEADER", "NO");
            }
            pbx.WriteToFile(pbxPath);

            Debug.Log("[MobileIOSPostProcess] Info.plist + pbxproj updated:\n" +
                      "  ENABLE_MODULE_VERIFIER            = NO    (Xcode 16+ rejects Unity 2022 framework headers)\n" +
                      "  ENABLE_USER_SCRIPT_SANDBOXING     = NO    (IL2CPP script phase needs file access)\n" +
                      "  UIFileSharingEnabled              = true  (Finder file sharing)\n" +
                      "  LSSupportsOpeningDocumentsInPlace = true  (Files app access)\n" +
                      "  UIRequiresFullScreen              = true  (no Split View)\n" +
                      "  ITSAppUsesNonExemptEncryption     = false (skips export compliance)");
#else
            Debug.LogWarning("[MobileIOSPostProcess] Built for iOS but UNITY_IOS was not defined; " +
                             "Info.plist was NOT modified. Switch the active build target to iOS.");
#endif
        }
    }
}
