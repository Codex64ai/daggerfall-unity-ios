// Project:         Daggerfall Unity - iOS Touch Input Layer
// License:         MIT License
//
// Headless verification of the touch layer's pure logic.
//
//   Menu: Tools > Daggerfall Mobile > Run Self Test
//   CLI:  -executeMethod DaggerfallWorkshop.Game.Mobile.EditorTools.MobileSelfTest.RunAll
//
// Run with "-batchmode -quit" but NOT with -nographics: the mod extractor tests decode
// compressed bundle textures through a GPU blit, which needs a real graphics device.
//
// Deliberately not NUnit: this project has no asmdefs, so everything lands in the
// predefined assemblies and test discovery there is unreliable. A plain -executeMethod
// entry point always works and exits non-zero on failure, which is what CI needs.
//
// Covers only logic that is genuinely device-independent - button edge derivation,
// unit conversion, threshold maths, state teardown. It cannot cover touch feel; that
// needs a finger.
//
// Place in Assets/Editor/

using System;
using DaggerfallWorkshop.Game.Mobile;
using DaggerfallWorkshop.Game.MagicAndEffects;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using FullSerializer;
using DaggerfallWorkshop.Utility;
using DaggerfallWorkshop.Utility.AssetInjection;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using DaggerfallConnect.Utility;
using UnityEditor;

namespace DaggerfallWorkshop.Game.Mobile.EditorTools
{
    public static class MobileSelfTest
    {
        static int passed;
        static int failed;
        static StringBuilder log;

        [MenuItem("Tools/Daggerfall Mobile/Run Self Test")]
        public static void RunAll()
        {
            passed = 0;
            failed = 0;
            log = new StringBuilder();
            log.AppendLine("=== Mobile touch layer self test ===");

            TestButtonEdges();
            TestLatchedButton();
            TestBackButtonEdges();
            TestScrollOneStepPerFrame();
            TestControllerForcesCursorOff();
            TestKeyboardForcesCursorOff();
            TestInputModeResolution();
            TestSwingModeDecision();
            TestClassicDrawerRules();
            TestBottomRowSpacing();
            TestLayoutOverrideStaleness();
            TestPointerKeepsCursorOverKeyboard();
            TestPointerDeltaScale();
            TestPointerLockDecision();
            TestPointerDrainDecision();
            TestPointerHoverToScreen();
            TestPointerScrollTicks();
            TestPointerFingerRule();
            TestPointerClickGrace();
            TestHardwareKeyboardTable();
            TestPointerDefaultActions();
            TestDpiFallback();
            TestThresholdMaths();
            TestThresholdRoundTrip();
            TestDeviceIndependence();
            TestRelinquish();
            TestContentPathRemap();
            TestUserContentFolders();
            TestMergeModFiles();
            TestBundledModManifests();
            TestBundledModLicences();
            TestWavDecoder();
            TestJourneyBearing();
            TestJourneyArrivalRect();
            TestJourneyCompressionClamp();
            TestJourneySpeedTiers();
            TestJourneyVitals();
            TestJourneyNightResume();
            TestJourneyLocationHold();
            TestJourneySeaRoute();
            TestJourneyStatusEffectPause();
            TestRouteRule();
            TestNightDecision();
            TestPassThroughGeometry();
            TestRoadData();
            TestRoadsInstallSurvivesSceneSwap();
            TestModsSwitchOwnsBothPrefs();
            TestSummerStartDate();
            TestModBundleRoundTrip();
            TestModScriptSkipRule();
            TestNormalReconstructRule();
            TestWavEncoderRule();
            TestConvertedModImportPolicy();
            TestPackTextureRules();
            TestModExtractorRoundTrip();
            TestModExtractorPathContainment();
            TestModExtractorSurvivesBadPaths();
            TestConverterGuardRules();
            TestMaterialTextureNaming();
            TestMaterialMapLookupNaming();
            TestNormalMapGatePremise();
            TestAssetStatsCounters();
            TestConversionRefusesEmptyResult();
            TestChunkedConversion();
            TestRoadDirectionReciprocity();
            TestRoadRouting();
            TestWaypointOvershoot();
            TestImmediateModeDrawGuards();
            TestSpellCastAnimNeverStrands();
            TestCastStateTearsDownOnFailure();
            TestEnsureReadable();
            TestMobileShadersFind();
            TestMobileCRT();
            TestMobileCRTRender();
            TestMobileCRTSettingsEndToEnd();
            TestMobileCRTUI();
            TestWODBiomesPort();
            TestBiomesClimateKey();
            TestWoDTerrainPort();
            TestDynamicSkiesShader();
            TestDynamicSkiesPresetTextures();
            TestDistantTerrainShader();
            TestDistantTerrainPort();
            TestDistantTerrainSliceBlit();
            TestRealGrassPort();
            TestModConflictOrder();
            TestPortedModGate();
            TestPortedModOrder();
            TestPortedModTitles();
            TestLocationLoaderRmbObjects();
            TestBiomesClimateSwapGuard();
            TestTravelOptionsBridge();
            TestTavernAlcohol();
            TestModSettingsSerialization();
            TestModLookupTolerantOfBuiltIns();
            TestFlcPathResolution();
            TestBuildStamp();

            log.AppendLine();
            log.AppendLine(string.Format("=== {0} passed, {1} failed ===", passed, failed));

            if (failed > 0)
            {
                Debug.LogError(log.ToString());
                EditorApplication.Exit(1);
            }
            else
            {
                Debug.Log(log.ToString());
            }
        }

        /// <summary>
        /// The user-content path arithmetic. Exercised with injected roots because on desktop
        /// MobileContentPath.Active is false and Override() is a deliberate no-op - otherwise
        /// the prefix matching and separator handling would never be tested anywhere.
        /// </summary>
        static void TestContentPathRemap()
        {
            const string shipped = "/app/Data/Raw";
            const string user = "/docs";

            // Player has the file: the user copy wins.
            Check(MobileContentPath.Remap(shipped + "/Textures/180_0-0.png", shipped, user,
                      p => p == "/docs/Textures/180_0-0.png") == "/docs/Textures/180_0-0.png",
                  "remap prefers an existing user file");

            // Player does not have it: falls back to the shipped file. This is the case that
            // matters most - 265 shipped quests must stay reachable.
            Check(MobileContentPath.Remap(shipped + "/Quests/S0000977.txt", shipped, user,
                      p => false) == shipped + "/Quests/S0000977.txt",
                  "remap falls back to the shipped file");

            // Paths outside the shipped root are left alone.
            Check(MobileContentPath.Remap("/somewhere/else/x.png", shipped, user, p => true)
                      == "/somewhere/else/x.png",
                  "remap ignores paths outside the shipped root");

            // The root itself must not remap to the user root wholesale.
            Check(MobileContentPath.Remap(shipped, shipped, user, p => true) == shipped,
                  "remap leaves the root itself alone");

            Check(MobileContentPath.Remap(null, shipped, user, p => true) == null,
                  "remap tolerates null");

            // A leading separator must not defeat Path.Combine.
            Check(MobileContentPath.Remap(shipped + "/Sound/a.wav", shipped, user,
                      p => p == "/docs/Sound/a.wav") == "/docs/Sound/a.wav",
                  "remap strips the leading separator");
        }

        /// <summary>
        /// The folders EnsureUserFolders() creates in Documents.
        ///
        /// Redirecting a loader through MobileContentPath is only half the job: if the folder is
        /// never created the player has no visible place to put the files and the feature looks
        /// broken. Every content type that resolves through Override()/UserFiles() must be listed,
        /// so this test is the thing that fails when a redirect is added and the folder is not.
        /// </summary>
        static void TestUserContentFolders()
        {
            string[] folders = MobileContentPath.UserFolderNames;
            var set = new HashSet<string>(folders);

            // Movies: VideoReplacement resolves "Movies" through Override(), but the folder was
            // missing from the list, so a player had to create it by hand before it could be used.
            Check(set.Contains("Movies"), "Documents/Movies is created for replacement videos");

            // SpellIcons: loose icon packs are enumerated out of this folder.
            Check(set.Contains("SpellIcons"), "Documents/SpellIcons is created for loose icon packs");

            // Presets: mod settings presets are read from Presets/<mod file name>/*.json through
            // the same redirect, but the folder was never created, so there was nowhere to put them.
            Check(set.Contains("Presets"), "Documents/Presets is created for mod settings presets");

            // The folders the port already relied on must not be dropped by a careless edit.
            Check(set.Contains("Mods") && set.Contains("Textures") && set.Contains("Textures/Img")
                  && set.Contains("Textures/CifRci") && set.Contains("Sound") && set.Contains("Quests")
                  && set.Contains("QuestPacks") && set.Contains("Books") && set.Contains("WorldData"),
                  "every previously supported content folder is still listed");

            Check(set.Count == folders.Length, "no folder is listed twice");

            // Relative, forward-slashed and non-empty: these are combined onto UserRoot.
            bool wellFormed = true;
            foreach (string f in folders)
            {
                if (string.IsNullOrEmpty(f) || f.Contains("\\") || Path.IsPathRooted(f))
                    wellFormed = false;
            }
            Check(wellFormed, "folder names are relative and forward-slashed");

            // The accessor must not hand out the live array.
            string[] copy = MobileContentPath.UserFolderNames;
            copy[0] = "clobbered";
            Check(MobileContentPath.UserFolderNames[0] != "clobbered",
                  "UserFolderNames returns a copy, not the backing array");
        }

        /// <summary>
        /// On iOS two folders hold .dfmod files: the player's Documents/Mods and the shipped
        /// StreamingAssets/Mods with the bundled MIT mods. The scanner merges them, player
        /// first, and a shipped file whose name the player also has is dropped - so a player
        /// who installs their own copy of a bundled mod gets theirs, not ours.
        /// </summary>
        static void TestMergeModFiles()
        {
            string p = "/Documents/Mods/", s = "/App/StreamingAssets/Mods/";

            string[] r = ModManager.MergeModFiles(
                new string[0], new[] { s + "JOTG.dfmod", s + "FixedDungeonExteriors.dfmod" });
            Check(r.Length == 2 && r[0] == s + "JOTG.dfmod", "shipped-only: all shipped files, in order");

            r = ModManager.MergeModFiles(new[] { p + "dream-sound.dfmod" }, new string[0]);
            Check(r.Length == 1 && r[0] == p + "dream-sound.dfmod", "player-only: unchanged");

            r = ModManager.MergeModFiles(new[] { p + "dream-sound.dfmod" }, new[] { s + "JOTG.dfmod" });
            Check(r.Length == 2 && r[0] == p + "dream-sound.dfmod" && r[1] == s + "JOTG.dfmod",
                  "disjoint: player first, then shipped");

            r = ModManager.MergeModFiles(
                new[] { p + "JOTG.dfmod" }, new[] { s + "JOTG.dfmod", s + "VariedWealthyHomes.dfmod" });
            Check(r.Length == 2 && r[0] == p + "JOTG.dfmod" && r[1] == s + "VariedWealthyHomes.dfmod",
                  "same file name in both: the player's copy is kept and the shipped one dropped");

            r = ModManager.MergeModFiles(null, null);
            Check(r != null && r.Length == 0, "null inputs give an empty list, not an exception");
        }

        /// <summary>
        /// Every fetched mod manifest (Assets/Game/Mods/*, minus the IOSPilot fixture) must
        /// parse, name no script, and reference only files that exist - the same rule the
        /// fetch script applies, enforced from the editor so a stale or hand-edited fetch
        /// cannot reach a build. Skips with a note when nothing has been fetched.
        /// </summary>
        static void TestBundledModManifests()
        {
            string[] manifests = MobileBuildSetup.BundledManifests();
            if (manifests.Length == 0)
            {
                log.AppendLine("  SKIP  bundled mod manifests (none fetched - run tools/bundled-mods/fetch.py)");
                return;
            }
            // The pin list (tools/bundled-mods/mods.json) is the source of truth for how many.
            int pinned = 0;
            try
            {
                string pins = File.ReadAllText("tools/bundled-mods/mods.json");
                pinned = System.Text.RegularExpressions.Regex.Matches(pins, "\"repo\"\\s*:").Count;
            }
            catch (Exception) { }
            Check(pinned > 0 && manifests.Length == pinned, "one fetched manifest per pinned mod",
                  manifests.Length + " fetched, " + pinned + " pinned");
            Check(!manifests.Any(m => m.Replace('\\', '/').Contains("/IOSPilot/")), "IOSPilot is never bundled");

            int bad = 0;
            var titles = new HashSet<string>();
            foreach (string path in manifests)
            {
                ModInfo info = null;
                bool ok = !ModManager._serializer.TryDeserialize(
                    fsJsonParser.Parse(File.ReadAllText(path)), ref info).Failed && info != null;
                if (!ok || string.IsNullOrWhiteSpace(info.ModTitle) || !titles.Add(info.ModTitle)) { bad++; continue; }
                if (info.Files == null || info.Files.Count == 0) { bad++; continue; }
                if (info.Files.Any(f => f.EndsWith(".cs") || f.EndsWith(".dll.bytes"))) { bad++; continue; }
                if (info.Files.Any(f => !File.Exists(f))) { bad++; continue; }
            }
            Check(bad == 0, "every bundled manifest parses, has a unique title, no scripts, and all files present",
                  bad + " bad");
        }

        /// <summary>
        /// MIT requires the notice to travel with the copy, so each shipped bundle must have its
        /// LICENSE beside it. Only meaningful after BuildBundledMods has run; skips otherwise.
        /// </summary>
        static void TestBundledModLicences()
        {
            string root = MobileBuildSetup.ShippedModsPath;
            string[] bundles = Directory.Exists(root)
                ? Directory.GetFiles(root, "*" + ModManager.MODEXTENSION, SearchOption.TopDirectoryOnly)
                : new string[0];
            if (bundles.Length == 0)
            {
                log.AppendLine("  SKIP  bundled mod licences (no bundles built yet - run MobileBuildSetup.BuildBundledMods)");
                return;
            }
            int missing = 0;
            foreach (string b in bundles)
            {
                string stem = Path.GetFileNameWithoutExtension(b);
                string lic = Path.Combine(root, "Licenses", stem + "-LICENSE.txt");
                // MIT text, or a Permission record for a pack redistributed by its author's leave
                // (Jay_H's quest packs) - never nothing.
                // MIT text, a Permission record, or another real licence (UBLaMF ships CC BY-NC-SA
                // in a License.md whose first line is a Bethesda note). fetch.py is the strict gate
                // on WHICH licences are acceptable; this only guards against a bundle with none.
                string text = File.Exists(lic) ? File.ReadAllText(lic).Trim() : "";
                bool ok = text.Length > 80;
                if (!ok)
                    missing++;
            }
            Check(missing == 0, "every shipped bundle has a licence or permission record beside it", missing + " missing");
            // StreamingAssets/Mods holds either every fetched pack (pack-zip workflow) or only the
            // built-in data bundles (DFU_BUNDLED_MODS=builtin, what an app build ships).
            int all = MobileBuildSetup.BundledManifests().Length, builtin = MobileBuildSetup.BuiltInEntryNames().Count;
            Check(bundles.Length == all || bundles.Length == builtin,
                  "one bundle per fetched manifest (all " + all + ") or per built-in entry (" + builtin + ")", bundles.Length + " bundles");
        }

        /// <summary>
        /// The name the engine asks a mod for when it wants an extra material map.
        ///
        /// CustomizeMaterial() now falls through to mods for MetallicGloss and Height, and that
        /// fallback is a lookup BY NAME inside an asset bundle: if the name the engine builds and
        /// the name the converter stored ever drift apart, the map is simply never found and there
        /// is no error to notice. This pins both sides against each other on the desktop, which is
        /// as far as it can be verified without a device.
        /// </summary>
        static void TestMaterialMapLookupNaming()
        {
            // The engine side, exactly as CustomizeMaterial asks for it.
            Check(TextureReplacement.GetName(108, 2, 0, TextureMap.Normal) == "108_2-0_Normal",
                  "GetName builds the documented map name",
                  TextureReplacement.GetName(108, 2, 0, TextureMap.Normal));
            Check(TextureReplacement.GetName(108, 2, 0, TextureMap.Height) == "108_2-0_Height"
                  && TextureReplacement.GetName(108, 2, 0, TextureMap.MetallicGloss) == "108_2-0_MetallicGloss",
                  "Height and MetallicGloss follow the same rule");

            // Albedo carries no suffix, so a map lookup can never collide with the base texture.
            Check(TextureReplacement.GetName(108, 2, 0) == "108_2-0",
                  "the albedo name carries no map suffix");

            // THE CONTRACT: the converter names bundle entries by DfuTextureName, the engine looks
            // them up by GetName. Same inputs must give the same string or the maps are invisible.
            string[] maps = { "Normal", "Height", "MetallicGloss", "Emission" };
            TextureMap[] enums = { TextureMap.Normal, TextureMap.Height, TextureMap.MetallicGloss, TextureMap.Emission };
            bool agree = MobileModExtractor.DfuTextureName(108, 2, 0, string.Empty)
                         == TextureReplacement.GetName(108, 2, 0);
            string mismatch = "";
            for (int i = 0; i < maps.Length; i++)
            {
                string converter = MobileModExtractor.DfuTextureName(108, 2, 0, maps[i]);
                string engine = TextureReplacement.GetName(108, 2, 0, enums[i]);
                if (converter != engine)
                {
                    agree = false;
                    mismatch = converter + " != " + engine;
                }
            }
            Check(agree, "converted bundle names match the names CustomizeMaterial looks up", mismatch);

            // Zero padding is the easiest way for the two to drift, so pin a low archive too.
            Check(MobileModExtractor.DfuTextureName(6, 0, 0, "Height") == TextureReplacement.GetName(6, 0, 0, TextureMap.Height)
                  && TextureReplacement.GetName(6, 0, 0, TextureMap.Height) == "006_0-0_Height",
                  "a single-digit archive is zero-padded on both sides",
                  TextureReplacement.GetName(6, 0, 0, TextureMap.Height));

            // These three are linear; importing them as sRGB would visibly wreck the shading.
            Check(TextureReplacement.IsLinearTextureMap(TextureMap.Normal)
                  && TextureReplacement.IsLinearTextureMap(TextureMap.Height)
                  && TextureReplacement.IsLinearTextureMap(TextureMap.MetallicGloss)
                  && !TextureReplacement.IsLinearTextureMap(TextureMap.Albedo),
                  "the extra material maps are linear and albedo is not");
        }

        /// <summary>
        /// The premise the normal-map gate in MaterialReader.GetMaterial() now rests on.
        ///
        /// That gate used to be (GenerateNormals || importedNormals) with importedNormals probing
        /// loose files only, which discarded a normal map imported from a mod bundle. It is now
        /// simply "did we end up with a normal map", which is EQUIVALENT only while a normal map
        /// cannot be generated behind the caller's back: generation needs settings.createNormalMap,
        /// and MaterialReader sets that solely inside its `if (GenerateNormals)` block.
        ///
        /// So the whole simplification hinges on CreateTextureSettings leaving createNormalMap
        /// false. If someone ever defaults it true, generated normals would start being applied for
        /// players who have GenerateNormals switched OFF - a silent visual change with nothing else
        /// to catch it. This is that catch.
        /// </summary>
        static void TestNormalMapGatePremise()
        {
            GetTextureSettings settings = DaggerfallWorkshop.Utility.TextureReader.CreateTextureSettings(180, 0, 0);

            Check(!settings.createNormalMap,
                  "CreateTextureSettings leaves normal-map generation off, so a non-null normalMap means it was imported");

            // The archive/record/frame must survive, since the gate reads them back off settings.
            Check(settings.archive == 180 && settings.record == 0 && settings.frame == 0,
                  "CreateTextureSettings carries the record identity through");

            // Emission generation is gated the same way by its caller; pin it for the same reason.
            Check(!settings.createEmissionMap,
                  "CreateTextureSettings leaves emission generation off too");
        }

        /// <summary>
        /// The mod-vs-loose map counters behind the diagnostics overlay.
        ///
        /// These exist to answer a question a screenshot cannot - "are bundled normal and height
        /// maps actually being applied?" - so a counter that miscounts is worse than none: it
        /// would be read as proof either way. The split is what carries the meaning, since loose
        /// files always worked and only the MOD column reflects the fix.
        /// </summary>
        static void TestAssetStatsCounters()
        {
            bool wasEnabled = MobileAssetStats.Enabled;
            MobileAssetStats.Reset();

            // Gated: with the overlay off nothing is recorded, which is what makes the counting
            // free in a release session.
            MobileAssetStats.Enabled = false;
            MobileAssetStats.CountApplied(TextureMap.Normal, true);
            MobileAssetStats.CountApplied(TextureMap.Height, false);
            Check(MobileAssetStats.ModNormal == 0 && MobileAssetStats.LooseHeight == 0,
                  "nothing is counted while the diagnostics overlay is off");

            MobileAssetStats.Enabled = true;
            MobileAssetStats.CountApplied(TextureMap.Normal, true);
            MobileAssetStats.CountApplied(TextureMap.Normal, true);
            MobileAssetStats.CountApplied(TextureMap.Normal, false);
            MobileAssetStats.CountApplied(TextureMap.Height, true);
            MobileAssetStats.CountApplied(TextureMap.MetallicGloss, false);
            MobileAssetStats.CountApplied(TextureMap.Emission, true);

            Check(MobileAssetStats.ModNormal == 2 && MobileAssetStats.LooseNormal == 1,
                  "mod and loose normals land in separate columns",
                  MobileAssetStats.ModNormal + "/" + MobileAssetStats.LooseNormal);
            Check(MobileAssetStats.ModHeight == 1 && MobileAssetStats.LooseHeight == 0
                  && MobileAssetStats.LooseMetallicGloss == 1 && MobileAssetStats.ModMetallicGloss == 0
                  && MobileAssetStats.ModEmission == 1,
                  "each map type has its own pair of counters");

            // A map with no column must not be silently folded into another one.
            MobileAssetStats.CountApplied(TextureMap.Albedo, true);
            MobileAssetStats.CountApplied(TextureMap.Mask, true);
            Check(MobileAssetStats.ModNormal == 2 && MobileAssetStats.ModHeight == 1
                  && MobileAssetStats.ModMetallicGloss == 0 && MobileAssetStats.ModEmission == 1,
                  "albedo and mask are ignored rather than miscounted");

            // The headline signal: "did anything at all come out of a bundle".
            Check(MobileAssetStats.AnyFromMods, "AnyFromMods is set by a mod-sourced application");

            MobileAssetStats.Reset();
            Check(!MobileAssetStats.AnyFromMods && MobileAssetStats.ModNormal == 0,
                  "Reset clears every counter");

            MobileAssetStats.Enabled = true;
            MobileAssetStats.CountApplied(TextureMap.Height, false);
            Check(!MobileAssetStats.AnyFromMods,
                  "a LOOSE application does not read as proof the bundle path works");

            // The overlay must actually show the numbers - a summary that silently dropped one
            // would defeat the whole exercise.
            MobileAssetStats.Reset();
            MobileAssetStats.CountApplied(TextureMap.Normal, true);
            MobileAssetStats.CountApplied(TextureMap.Height, true);
            string summary = MobileAssetStats.Summary();
            Check(summary.Contains("normal 1") && summary.Contains("height 1")
                  && summary.Contains("metallic 0") && summary.Contains("emission 0")
                  && summary.Contains("loose"),
                  "the overlay line reports every column", summary.Replace("\n", " | "));

            Check(MobileAssetStats.SkyLine(true, 4.25) == "sky dynamic gpu 4.3 ms"
                  && MobileAssetStats.SkyLine(false, -1) == "sky vanilla gpu n/a",
                  "diagnostics: sky line");

            MobileAssetStats.Reset();
            MobileAssetStats.Enabled = wasEnabled;
        }

        /// <summary>
        /// The hand-rolled RIFF/WAVE decoder that replaces the legacy WWW("file://") path on
        /// iOS. Written by hand, so it gets tested rather than trusted: a real file is built
        /// on disk with known sample values and decoded back.
        /// </summary>
        static void TestWavDecoder()
        {
            string path = Path.Combine(Path.GetTempPath(), "dfu_selftest.wav");

            const int rate = 22050;
            const int channels = 1;
            const int frames = 512;

            // 16-bit PCM mono, with a deliberate LIST chunk before 'data' so the decoder has
            // to walk the chunk list instead of assuming a 44-byte header.
            var pcm = new byte[frames * 2];
            for (int i = 0; i < frames; i++)
            {
                short v = (short)(i == 0 ? 0 : (i == 1 ? 32767 : (i == 2 ? -32768 : 1000)));
                pcm[i * 2] = (byte)(v & 0xff);
                pcm[i * 2 + 1] = (byte)((v >> 8) & 0xff);
            }

            byte[] junk = System.Text.Encoding.ASCII.GetBytes("INFOhello!!!");

            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                w.Write(0);                                       // patched below
                w.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));

                w.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
                w.Write(16);
                w.Write((short)1);                                 // PCM
                w.Write((short)channels);
                w.Write(rate);
                w.Write(rate * channels * 2);                      // byte rate
                w.Write((short)(channels * 2));                    // block align
                w.Write((short)16);                                // bits

                w.Write(System.Text.Encoding.ASCII.GetBytes("LIST"));
                w.Write(junk.Length);
                w.Write(junk);

                w.Write(System.Text.Encoding.ASCII.GetBytes("data"));
                w.Write(pcm.Length);
                w.Write(pcm);

                w.Flush();
                byte[] all = ms.ToArray();
                int riffSize = all.Length - 8;
                all[4] = (byte)(riffSize & 0xff);
                all[5] = (byte)((riffSize >> 8) & 0xff);
                all[6] = (byte)((riffSize >> 16) & 0xff);
                all[7] = (byte)((riffSize >> 24) & 0xff);
                File.WriteAllBytes(path, all);
            }

            AudioClip clip;
            bool ok = SoundReplacement.TryDecodeWavFromDisk(path, "selftest", out clip);

            Check(ok && clip != null, "wav decodes to a clip");
            if (ok && clip != null)
            {
                Check(clip.channels == channels, "wav channel count", "got " + clip.channels);
                Check(clip.frequency == rate, "wav sample rate", "got " + clip.frequency);
                Check(clip.samples == frames, "wav sample count (chunk walk found data)",
                      "got " + clip.samples);

                var got = new float[frames];
                clip.GetData(got, 0);
                Near(got[0], 0f, 0.001f, "wav sample 0 is silence");
                Near(got[1], 1f, 0.001f, "wav sample 1 is full positive");
                Near(got[2], -1f, 0.001f, "wav sample 2 is full negative");
            }

            // Malformed input must be refused, not throw - a bad file in a mod folder should
            // fall back to the original sound, not take the game down.
            string bad = Path.Combine(Path.GetTempPath(), "dfu_selftest_bad.wav");
            File.WriteAllBytes(bad, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });
            AudioClip badClip;
            bool badOk;
            try
            {
                badOk = SoundReplacement.TryDecodeWavFromDisk(bad, "bad", out badClip);
                Check(!badOk, "malformed wav is refused without throwing");
            }
            catch (System.Exception ex)
            {
                Check(false, "malformed wav is refused without throwing", ex.GetType().Name);
            }

            try { File.Delete(path); File.Delete(bad); } catch { }
        }

        #region Assertions


        // Bundle textures are imported without Read/Write; DFU's atlas builder and terrain-array
        // fallback need pixels. EnsureReadable must hand back a readable RGBA32 copy with the same
        // pixels, and must return readable textures untouched.
        static void TestEnsureReadable()
        {
            var readable = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            Check(ReferenceEquals(TextureReplacement.EnsureReadable(readable), readable), "EnsureReadable: readable texture returned as is");

            var src = new Texture2D(4, 2, TextureFormat.RGBA32, false);
            var px = new Color32[8];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32((byte)(i * 30), (byte)(255 - i * 30), 7, 255);
            src.SetPixels32(px);
            src.Apply(false, true);     // upload and drop the CPU copy: this is what a bundle texture looks like
            Check(!src.isReadable, "EnsureReadable: fixture is not readable");
            Texture2D copy = TextureReplacement.EnsureReadable(src);
            Check(copy != null && copy.isReadable, "EnsureReadable: copy is readable");
            Check(copy != null && copy.width == 4 && copy.height == 2 && copy.format == TextureFormat.RGBA32, "EnsureReadable: copy is RGBA32 of the same size");
            bool same = copy != null;
            if (same)
            {
                Color32[] got = copy.GetPixels32();
                for (int i = 0; i < px.Length && same; i++)
                    same = Mathf.Abs(got[i].r - px[i].r) <= 2 && Mathf.Abs(got[i].g - px[i].g) <= 2 && Mathf.Abs(got[i].b - px[i].b) <= 2;
            }
            Check(same, "EnsureReadable: pixels survive the blit");
            Check(ReferenceEquals(TextureReplacement.EnsureReadable(src), copy), "EnsureReadable: copy is cached per source");
            UnityEngine.Object.DestroyImmediate(readable); UnityEngine.Object.DestroyImmediate(src);
        }

        // MaterialReader must get the project's own shaders, never a copy embedded in a mod bundle.
        static void TestMobileShadersFind()
        {
            // The two billboard-batch names are in this loop as well as in the Names check below:
            // membership in the capture list is not resolution, and these two are what the Biomes
            // nature overrider and DaggerfallBillboardBatch itself build their atlas material with.
            foreach (string name in new[] { MaterialReader._DaggerfallDefaultShaderName, MaterialReader._DaggerfallBillboardShaderName, MaterialReader._DaggerfallTilemapTextureArrayShaderName, MaterialReader._DaggerfallBillboardBatchShaderName, MaterialReader._DaggerfallBillboardBatchNoShadowsShaderName })
            {
                Shader s = MobileShaders.Find(name);
                Check(s != null && s.name == name, "MobileShaders.Find resolves " + name);
            }
            Check(MobileShaders.Find("No/Such/Shader") == null, "MobileShaders.Find: unknown name falls through to Shader.Find (null)");
            Check(MobileShaders.Names.Contains(MaterialReader._DaggerfallBillboardBatchShaderName),
                "MobileShaders: billboard batch shader is captured");
            Check(MobileShaders.Names.Contains(MaterialReader._DaggerfallBillboardBatchNoShadowsShaderName),
                "MobileShaders: billboard batch no-shadows shader is captured");
            // Rebase tripwire. Everything above passes whether or not the engine file actually calls
            // MobileShaders: DaggerfallBillboardBatch.cs is an upstream file, and an upstream merge that
            // takes theirs at :318-319 / :382-383 restores the raw Shader.Find the patch exists to
            // remove - compiling cleanly and silently reopening the bundle-embedded-shader ambiguity.
            // So read the source. (Precedent: TestDynamicSkiesShader reads a shader file as text.)
            string batchSrc = System.IO.File.ReadAllText("Assets/Scripts/Internal/DaggerfallBillboardBatch.cs");
            Check(CountOccurrences(batchSrc, "MobileShaders.Find(MaterialReader._DaggerfallBillboardBatch") == 4,
                "DaggerfallBillboardBatch: both SetMaterial overloads build their material with MobileShaders.Find",
                CountOccurrences(batchSrc, "MobileShaders.Find(MaterialReader._DaggerfallBillboardBatch") + " call sites, expected 4");
            // "Shader.Find(" is not a substring of "MobileShaders.Find(" (the char before ".Find" is
            // 's'), so this is an exact test for a raw call, not a near-miss on the patched one.
            Check(!batchSrc.Contains("Shader.Find("),
                "DaggerfallBillboardBatch: no raw Shader.Find survives - a rebase that takes theirs fails here");
        }

        // Non-overlapping occurrence count, for the source-text checks.
        /// <summary>Drops // and /* */ comments from ShaderLab/HLSL source so a check for an
        /// absent symbol cannot be satisfied by a comment that merely mentions it.</summary>
        static string StripShaderComments(string source)
        {
            var sb = new StringBuilder(source.Length);
            for (int i = 0; i < source.Length; i++)
            {
                if (source[i] == '/' && i + 1 < source.Length && source[i + 1] == '/')
                {
                    while (i < source.Length && source[i] != '\n') i++;
                    if (i < source.Length) sb.Append('\n');
                }
                else if (source[i] == '/' && i + 1 < source.Length && source[i + 1] == '*')
                {
                    i += 2;
                    while (i + 1 < source.Length && !(source[i] == '*' && source[i + 1] == '/')) i++;
                    i++;
                }
                else sb.Append(source[i]);
            }
            return sb.ToString();
        }

        static int CountOccurrences(string haystack, string needle)
        {
            int n = 0, i = 0;
            while ((i = haystack.IndexOf(needle, i, StringComparison.Ordinal)) >= 0) { n++; i += needle.Length; }
            return n;
        }

        /// <summary>
        /// The braces-matched body of one method, so a check can say "the teardown releases X"
        /// rather than "the file mentions X somewhere". Feed it comment-stripped source (comments
        /// are where a method names what a NEIGHBOURING method does). Returns "" when the signature
        /// is not found, which fails the caller's check rather than passing it vacuously.
        /// </summary>
        static string MethodBody(string source, string signature)
        {
            int at = source.IndexOf(signature, StringComparison.Ordinal);
            if (at < 0) return "";
            int open = source.IndexOf('{', at);
            if (open < 0) return "";
            int depth = 0;
            for (int i = open; i < source.Length; i++)
            {
                if (source[i] == '{') depth++;
                else if (source[i] == '}' && --depth == 0)
                    return source.Substring(open, i - open + 1);
            }
            return "";
        }

        // The World of Daggerfall - Biomes port is compiled in but inert: MobilePortedMods starts it
        // explicitly behind a default-off launcher entry, so no [Invoke] may survive the copy, and the
        // Location Loader nature swap reads the climate map off the installer instead of its own bundle.
        static void TestWODBiomesPort()
        {
            Type invoke = typeof(DaggerfallWorkshop.Game.Utility.ModSupport.Invoke);
            MethodInfo biomesInit = typeof(WorldOfDaggerfall.WODBiomes).GetMethod("Init", BindingFlags.Public | BindingFlags.Static);
            MethodInfo overriderInit = typeof(WorldOfDaggerfall.NatureBatchOverriderInstaller).GetMethod("Init", BindingFlags.Public | BindingFlags.Static);
            Check(biomesInit != null && overriderInit != null, "WODBiomes: both Init entry points are public static");
            Check(biomesInit != null && biomesInit.GetCustomAttributes(invoke, false).Length == 0
                  && overriderInit != null && overriderInit.GetCustomAttributes(invoke, false).Length == 0,
                "WODBiomes: no [Invoke] survives the port - MobilePortedMods starts it");
            Type installer = typeof(WorldOfDaggerfall.NatureBatchOverriderInstaller);
            PropertyInfo climateProp = installer.GetProperty("ClimateMap", BindingFlags.Public | BindingFlags.Static);
            FieldInfo climateField = installer.GetField("ClimateMap", BindingFlags.Public | BindingFlags.Static);
            Check((climateProp != null && climateProp.PropertyType == typeof(Texture2D))
                  || (climateField != null && climateField.FieldType == typeof(Texture2D)),
                "WODBiomes: ClimateMap is exposed for the Location Loader nature swap");
        }

        // The Biomes nature swap keys off an exact colour match in a colour-key map, which is why the
        // map must import raw (Task 2) and why the port reads it through TextureReplacement.EnsureReadable:
        // one resampled or block-compressed pixel and #FFA500 stops being #FFA500. The atlas is pinned to
        // 1024 so archive 10030 never allocates upstream's transient 4096x4096 (85 MB) - and no lower,
        // because its 32 records run up to 130x153 / 115x272 / 113x296, ~225 k px once padded, which
        // 1024^2 clears by ~4.6x and 512^2 would only just hold.
        static void TestBiomesClimateKey()
        {
            Check(WorldOfDaggerfall.NatureBatchOverrider.IsSubtropicalKey(new Color32(255, 165, 0, 255)),
                "Biomes: #FFA500 is the subtropical key");
            Check(!WorldOfDaggerfall.NatureBatchOverrider.IsSubtropicalKey(new Color32(254, 165, 0, 255)),
                "Biomes: an off-by-one colour is not the key (exact match - this is why the map must not be ASTC)");
            Check(!WorldOfDaggerfall.NatureBatchOverrider.MapReadable(null),
                "Biomes: a missing climate map is not readable");
            Check(WorldOfDaggerfall.NatureBatchOverrider.AtlasMaxSize == 1024,
                "Biomes: atlas capped at 1024 (32 records up to 130x296, ~225k px padded - 512 would be marginal)");
            // A script-created texture is always readable, so the positive side of both guards is
            // constructible headlessly - without it a stub returning false unconditionally would pass.
            Texture2D scratch = new Texture2D(2, 2);
            Check(WorldOfDaggerfall.NatureBatchOverrider.MapReadable(scratch),
                "Biomes: a readable climate map is accepted");
            // The archive-10030 records arrive from the Daggerfall Expanded Textures bundle with
            // isReadable false, and Texture2D.PackTextures packs nothing from unreadable inputs while
            // logging only a warning - so "does this need EnsureReadable first" is the test that stands
            // between a green swap log and 32 blank palms.
            Texture2D unreadable = new Texture2D(2, 2);
            unreadable.Apply(false, true);      // makeNoLongerReadable - what isReadable:false yields at runtime
            Check(WorldOfDaggerfall.NatureBatchOverrider.NeedsReadableCopy(unreadable),
                "Biomes: a non-readable atlas record needs an EnsureReadable copy before packing");
            UnityEngine.Object.DestroyImmediate(unreadable);
            Check(!WorldOfDaggerfall.NatureBatchOverrider.NeedsReadableCopy(scratch)
                  && !WorldOfDaggerfall.NatureBatchOverrider.NeedsReadableCopy(null),
                "Biomes: a readable record is packed as-is and a missing one is not a copy candidate");
            // An import that found nothing is a failure, not an empty success: packing a zero-length
            // array leaves Apply() indexing an empty atlasRects for items laid out for archive 501.
            Check(WorldOfDaggerfall.NatureBatchOverrider.HasRecords(1)
                  && WorldOfDaggerfall.NatureBatchOverrider.HasRecords(32)
                  && !WorldOfDaggerfall.NatureBatchOverrider.HasRecords(0),
                "Biomes: an atlas with no records is not a usable atlas");
            UnityEngine.Object.DestroyImmediate(scratch);
            // The terrain provider's Mountain case gives Hammerfell its own ground archive, and the
            // region name it tests can be null before the player has a position - so null must fall
            // through to the unmodified archive rather than throw inside DaggerfallTerrain.
            // PromoteTerrainData, which has no try/catch of its own. A missing GPS is a different
            // story and is not covered here: GameManager.PlayerGPS throws instead of returning null.
            Check(WorldOfDaggerfall.WODTerrainMaterialProvider.IsHammerfellRegion("Alik'r Desert"),
                "Biomes: Alik'r Desert is a Hammerfell mountain region");
            Check(!WorldOfDaggerfall.WODTerrainMaterialProvider.IsHammerfellRegion("Daggerfall"),
                "Biomes: Daggerfall is not a Hammerfell mountain region");
            Check(!WorldOfDaggerfall.WODTerrainMaterialProvider.IsHammerfellRegion(null),
                "Biomes: a missing region name is not a Hammerfell mountain region (no throw)");
        }

        // World of Daggerfall - Terrain: the GPU terrain sampler, compiled in with its compute shaders
        // in Assets/Resources/WoDTerrain. These pin the parts of the port that are pure logic or pure
        // asset presence - the corrections that cannot be judged by eye on a screenshot.
        //
        //   LocationBufferSize   the shader writes locationHeightData[i] for every i < locationCount,
        //                        and locationCount is bounded by the 33x33 = 1089 map-pixel search
        //                        window; upstream sized the buffer 289. Out-of-range UAV writes are
        //                        silently dropped on D3D11 and UNDEFINED on Metal, so this constant is
        //                        what stands between the port and a command-buffer fault on device.
        //   Available            the mod has no CPU fallback generator of any kind. Without compute
        //                        support, or without both compute shaders loaded WITH their kernels,
        //                        Init must leave DaggerfallUnity.TerrainSampler alone rather than
        //                        install a sampler that can only produce garbage.
        //   Bands                upstream generates the world heightmap in one 500,000-thread dispatch,
        //                        which iOS's command-buffer execution limit can kill. Task 4 dispatches
        //                        it in row bands; this pins the split - every row covered exactly once,
        //                        including when the band count does not divide the height, and a height
        //                        that is not a whole number of thread-group rows refused outright.
        //   ReadbackStartRow     the kernel writes world row y to buffer row (499 - y), so the per-band
        //                        readback range is MIRRORED. This is the one line in the port where the
        //                        plan's own formula was wrong; it cannot run in the self-test (GPU,
        //                        DaggerfallUnity, a loaded mod), so the ranges are pinned arithmetically.
        //   StartupSample*       heightSampling.cginc reads shm/lhm through SampleBaseHeight on the
        //                        start-up path too. An unbound or out-of-range StructuredBuffer read is
        //                        undefined on Metal, so the uniforms that index those buffers have to be
        //                        proven in-bounds for every sample index the dispatch can produce.
        //   Resources.Load       the shaders ship in the app, not in the bundle, so the folder layout
        //                        Init loads through is itself the thing to verify.
        static void TestWoDTerrainPort()
        {
            // (N2) the world heightmap's dimensions are read from the same constants production reads
            // rather than written out as 500/1000 literals, so a change to either fails this suite
            // instead of leaving it green against numbers the port no longer uses.
            int mapW = DaggerfallConnect.Arena2.WoodsFile.MapWidth;
            int mapH = DaggerfallConnect.Arena2.WoodsFile.MapHeight;
            Check(Monobelisk.TerrainComputer.LocationBufferSize == 1089, "WoDTerrain: location buffer matches the shader's [1089] arrays (OOB write fix)");
            // (I1) the per-tile location arrays are now two statics at the shader's full [1089] length,
            // filled in place and always passed whole. Upstream's variable-length arrays were safe only
            // because it instantiated a fresh ComputeShader per tile; one held clone plus a first tile
            // over open water (locationCount 0) could otherwise have locked the array length at 1 for
            // the session and silently stopped flattening every town after it. Two things to hold: the
            // arrays really are the shader's length, and FillLocationArrays touches nothing at or past
            // the count it returns - which is what makes leaving the stale tail alone correct.
            Check(Monobelisk.TerrainComputer.LocationPositions.Length == Monobelisk.TerrainComputer.LocationBufferSize
                  && Monobelisk.TerrainComputer.LocationSizes.Length == Monobelisk.TerrainComputer.LocationBufferSize,
                "WoDTerrain: the location arrays are allocated at the shader's full array length, once");
            var fillPos = new Vector4[Monobelisk.TerrainComputer.LocationBufferSize];
            var fillSize = new Vector4[Monobelisk.TerrainComputer.LocationBufferSize];
            var sentinel = new Vector4(-7f, -7f, -7f, -7f);
            for (int i = 0; i < fillPos.Length; i++) { fillPos[i] = sentinel; fillSize[i] = sentinel; }
            int filled = Monobelisk.TerrainComputer.FillLocationArrays(
                new System.Collections.Generic.List<Rect> { new Rect(1f, 2f, 3f, 4f), new Rect(5f, 6f, 7f, 8f), new Rect(9f, 10f, 11f, 12f) },
                fillPos, fillSize);
            bool tailUntouched = true;
            for (int i = filled; i < fillPos.Length; i++)
                if (fillPos[i] != sentinel || fillSize[i] != sentinel) { tailUntouched = false; break; }
            Check(filled == 3 && tailUntouched
                  && fillPos[0] == new Vector4(1f, 2f, 0f, 0f) && fillSize[0] == new Vector4(3f, 4f, 0f, 0f)
                  && fillPos[2] == new Vector4(9f, 10f, 0f, 0f) && fillSize[2] == new Vector4(11f, 12f, 0f, 0f),
                "WoDTerrain: filling three locations returns 3 and leaves every later entry alone",
                "returned " + filled + ", tail " + (tailUntouched ? "untouched" : "overwritten"));
            // A zero-location tile - open water in the Iliac Bay, and the case that made the old
            // one-element guard array necessary - must still pass the full-length arrays and simply
            // report a count of nothing.
            Check(Monobelisk.TerrainComputer.FillLocationArrays(new System.Collections.Generic.List<Rect>(), fillPos, fillSize) == 0,
                "WoDTerrain: a tile with no locations reports count 0 without a shorter array");
            // MOBILE: (D1) the three arms of the per-tile suspicion test, each pinned separately. A
            // healthy 129x129 tile is roughly 0.02-0.30 normalised, so the healthy case must NOT fire or
            // the log fills with noise; each failure shape must, or a device log settles nothing.
            Check(!Monobelisk.TerrainComputer.IsSuspectTile(0.021f, 0.184f, 0),
                "WoDTerrain: a normal tile - shoreline, mountainside and all - is not reported as suspect");
            Check(Monobelisk.TerrainComputer.IsSuspectTile(0.02f, 0.02f, 0)
                  && Monobelisk.TerrainComputer.IsSuspectTile(0.0205f, 0.021f, 0),
                "WoDTerrain: a tile pinned at either ocean-level floor (0.02, 0.021) is suspect");
            Check(Monobelisk.TerrainComputer.IsSuspectTile(0.02f, 0.19f, 1),
                "WoDTerrain: a single NaN sample makes a tile suspect");
            Check(Monobelisk.TerrainComputer.IsSuspectTile(0.01f, 0.95f, 0),
                "WoDTerrain: 4,500 units of relief inside one tile is suspect");
            // The measured simulator baseline says a healthy tile of THIS generator routinely steps
            // 0.06-0.12 between adjacent samples (89 tiles of 92 at the reported map pixels), so a
            // "vertical wall" arm would fire on 97% of healthy terrain. maxStep is printed, never judged.
            Check(!Monobelisk.TerrainComputer.IsSuspectTile(0.0200f, 0.1306f, 0),
                "WoDTerrain: the measured healthy tile 462,53 (min .02 max .1306 step .1105) is not suspect");
            Check(Mathf.Approximately(Monobelisk.TerrainComputer.PitFloor, 0.0201f)
                  && Mathf.Approximately(Monobelisk.TerrainComputer.LocationFloor, 0.021f),
                "WoDTerrain: the two floors are the shader's own - 100/5000 and the 0.021 location floor");
            // (D1)(D2) WOODS.WLD's layout is index = x + y * MapWidth and the altered buffer holds
            // saturate()d heights x 255, so this must be byte/255 for the pixel asked for - the number
            // both the "base=" diagnostic and the flat fallback are built on - and -1, never an
            // exception and never a wrong pixel, for anything off the map or absent.
            var alteredWorld = new byte[DaggerfallConnect.Arena2.WoodsFile.MapWidth * DaggerfallConnect.Arena2.WoodsFile.MapHeight];
            alteredWorld[460 + 51 * DaggerfallConnect.Arena2.WoodsFile.MapWidth] = 51;
            Check(Mathf.Approximately(Monobelisk.TerrainComputer.WorldHeightmapSample(alteredWorld, 460, 51), 51f / 255f)
                  && Monobelisk.TerrainComputer.WorldHeightmapSample(alteredWorld, 461, 51) == 0f
                  && Monobelisk.TerrainComputer.WorldHeightmapSample(null, 460, 51) < 0f
                  && Monobelisk.TerrainComputer.WorldHeightmapSample(alteredWorld, -1, 51) < 0f
                  && Monobelisk.TerrainComputer.WorldHeightmapSample(alteredWorld, 460, DaggerfallConnect.Arena2.WoodsFile.MapHeight) < 0f,
                "WoDTerrain: the world heightmap sample is byte/255 at x + y * MapWidth, and -1 off the map");
            // (D3) the hole census is the measurement the pit fix has to move, so pin what it counts:
            // a map pixel at or under the ocean-floor byte with at least five of eight neighbours on
            // clear land is a hole; the same pixel with sea on one side is a COASTLINE and must not be
            // counted, or the number says "defect" everywhere the Iliac Bay meets the shore.
            int mw = DaggerfallConnect.Arena2.WoodsFile.MapWidth;
            var holeMap = new byte[mw * DaggerfallConnect.Arena2.WoodsFile.MapHeight];
            for (int hy = 160; hy <= 162; hy++)
                for (int hx = 111; hx <= 113; hx++)
                    holeMap[hx + hy * mw] = 16;
            holeMap[112 + 161 * mw] = 0;                       // the shape Player.log reported at 112,161
            Check(Monobelisk.TerrainComputer.CountWorldHeightmapHoles(holeMap) == 1,
                "WoDTerrain: a sea-level map pixel ringed by land counts as one world-heightmap hole");
            var coastMap = new byte[mw * DaggerfallConnect.Arena2.WoodsFile.MapHeight];
            for (int hy = 160; hy <= 162; hy++)
                coastMap[113 + hy * mw] = 16;                  // one column of land to the east, open sea west
            coastMap[112 + 161 * mw] = 0;
            Check(Monobelisk.TerrainComputer.CountWorldHeightmapHoles(coastMap) == 0
                  && Monobelisk.TerrainComputer.CountWorldHeightmapHoles(null) < 0
                  && Monobelisk.TerrainComputer.CountWorldHeightmapHoles(new byte[10]) < 0,
                "WoDTerrain: a coastline is not a hole, and a missing or short buffer reports -1");
            Check(Monobelisk.TerrainComputer.OceanFloorByte == 5 && Monobelisk.TerrainComputer.LandByte == 10
                  && Monobelisk.TerrainComputer.LandNeighboursForHole == 5,
                "WoDTerrain: the hole census thresholds are the generator's own floor (99/5000 = 5.05/255) and clear land");
            // MOBILE: (D4) the REPAIR is what turns that census into a fix. A hole takes the MEDIAN of
            // its land neighbours, so it lands inside the range the neighbourhood already holds and an
            // outlier (the 200 below, which a mean would chase) cannot move it. Run on a 5x5 so the
            // arithmetic is checkable by hand: sorted land = 12 20 30 40 40 60 80 200 and the
            // even-count convention is the upper middle, 40. The two readers this protects are the
            // travel map (WoodsFile.Buffer, drawn straight from these bytes - 459 inland map pixels
            // rendered as sea) and mapPixelHeights (every location's flatten target).
            const int rw = 5, rh = 5;
            var repairHole = new byte[]
            {
                 0,  0,  0,   0, 0,
                 0, 12, 20,  30, 0,
                 0, 40,  3,  40, 0,
                 0, 60, 80, 200, 0,
                 0,  0,  0,   0, 0,
            };
            // Odd count: one neighbour dropped to sea leaves seven land, sorted 12 20 30 44 50 60 70,
            // true median 44. The dropped corner is itself only two-land-neighboured, so it is not a
            // hole and the count stays 1.
            var repairOdd = new byte[]
            {
                 0,  0,  0,  0, 0,
                 0, 12, 20, 30, 0,
                 0, 44,  0, 50, 0,
                 0, 60, 70,  0, 0,
                 0,  0,  0,  0, 0,
            };
            Check(Monobelisk.TerrainComputer.RepairWorldHeightmapHoles(repairHole, rw, rh) == 1
                  && repairHole[2 + 2 * rw] == 40
                  && Monobelisk.TerrainComputer.RepairWorldHeightmapHoles(repairOdd, rw, rh) == 1
                  && repairOdd[2 + 2 * rw] == 44,
                "WoDTerrain: a world-heightmap hole is repaired to the median of its land neighbours");
            // The two exclusions the census makes must hold for the repair too, or it would fill the
            // Iliac Bay in: open sea has no land neighbours to take a median from, and a coastline
            // (land on one side only, fewer than five of eight) is real geography.
            var repairOcean = new byte[rw * rh];
            var repairCoast = new byte[]
            {
                 0, 0, 0, 30, 0,
                 0, 0, 0, 40, 0,
                 0, 0, 0, 50, 0,
                 0, 0, 0, 60, 0,
                 0, 0, 0, 70, 0,
            };
            Check(Monobelisk.TerrainComputer.RepairWorldHeightmapHoles(repairOcean, rw, rh) == 0
                  && repairOcean[2 + 2 * rw] == 0
                  && Monobelisk.TerrainComputer.RepairWorldHeightmapHoles(repairCoast, rw, rh) == 0
                  && repairCoast[2 + 2 * rw] == 0
                  && Monobelisk.TerrainComputer.RepairWorldHeightmapHoles(null, rw, rh) < 0
                  && Monobelisk.TerrainComputer.RepairWorldHeightmapHoles(new byte[10], rw, rh) < 0,
                "WoDTerrain: open sea and a coastline are never repaired, and a missing or short buffer reports -1");
            // A pass reads the map as it stood when the pass STARTED, so the outcome cannot depend on
            // the scan order - and that is exactly why a second pass is needed: (3,2) has only four
            // land neighbours until (2,2) is repaired, and it is scanned one column AFTER (2,2) in the
            // same pass. Two passes therefore close a two-pixel hole and stop.
            var repairTwoPass = new byte[]
            {
                 0,  0,  0,  0, 0,
                 0, 20, 20, 20, 0,
                 0, 20,  0,  0, 0,
                 0, 20, 20, 20, 0,
                 0,  0,  0,  0, 0,
            };
            Check(Monobelisk.TerrainComputer.RepairWorldHeightmapHoles(repairTwoPass, rw, rh) == 2
                  && repairTwoPass[2 + 2 * rw] == 20 && repairTwoPass[3 + 2 * rw] == 20
                  && Monobelisk.TerrainComputer.HoleRepairPasses == 2,
                "WoDTerrain: the second repair pass closes a hole the first cannot reach, and there are two");
            // MOBILE: (F2) Utility.ToBytes is the ONE conversion the mod's entire start-up world
            // heightmap passes through - the texture every location's flatten target is read from -
            // and upstream's `(byte)(uint)(f * 255f)` has no guard at all. NaN arrives as byte 0,
            // indistinguishable from ocean, so a collapsed map pixel becomes a location-sized pit
            // in silence; a negative wraps to a mountain; anything over 1 wraps back through 0. The
            // device reported `holes 14519` of 500,000 map pixels against 459 for the identical code
            // on macOS Metal, which is what an unguarded NaN looks like from the outside. The
            // healthy path has to stay byte-identical to upstream or every generated height moves.
            var healthyFloats = new float[] { 0f, 0.0198f, 0.0667f, 0.5f, 1f };
            int toByteSubs;
            var healthyBytes = Monobelisk.Utility.ToBytes(healthyFloats, out toByteSubs);
            bool byteIdentical = toByteSubs == 0 && healthyBytes.Length == healthyFloats.Length;
            for (int i = 0; i < healthyFloats.Length; i++)
                if (healthyBytes[i] != (byte)(uint)(healthyFloats[i] * 255f)) byteIdentical = false;
            Check(byteIdentical && healthyBytes[0] == 0 && healthyBytes[4] == 255,
                "WoDTerrain: ToBytes is byte-identical to upstream for in-range heights and substitutes nothing");
            // A bad float takes the byte of the map pixel immediately to the WEST - WOODS.WLD's
            // layout is index = x + y * MapWidth, so that is the previous element - rather than the
            // sea floor, which keeps it inside its own coastline. Every substitution is COUNTED and
            // the count is logged, because silently repairing the defect being hunted would be worse
            // than the defect.
            var badFloats = new float[] { 0.0667f, float.NaN, -0.004f, float.PositiveInfinity, float.NegativeInfinity, 0.08f };
            var badBytes = Monobelisk.Utility.ToBytes(badFloats, out toByteSubs);
            byte westByte = (byte)(uint)(0.0667f * 255f);
            Check(toByteSubs == 4 && badBytes[0] == westByte && badBytes[1] == westByte
                  && badBytes[2] == westByte && badBytes[3] == westByte && badBytes[4] == westByte
                  && badBytes[5] == (byte)(uint)(0.08f * 255f),
                "WoDTerrain: ToBytes replaces nan/inf/negative with the previous valid byte and counts each one");
            // Nothing lies west of element 0, so its fallback is the generator's own ocean floor
            // byte: ocean, not a mountain and not a hole.
            int leadSubs, nullSubs;
            var leadBytes = Monobelisk.Utility.ToBytes(new float[] { float.NaN, 0.09f }, out leadSubs);
            Check(leadSubs == 1 && leadBytes[0] == (byte)Monobelisk.TerrainComputer.OceanFloorByte
                  && Monobelisk.Utility.ToBytes(null, out nullSubs).Length == 0 && nullSubs == 0,
                "WoDTerrain: a leading bad float falls back to the ocean floor byte, and a null buffer converts to empty");
            // saturate() is what kept f <= 1 upstream, and -ffast-math is exactly what stops
            // guaranteeing that it does: (uint)(1.004f * 255f) is 256, which truncates to byte 0 -
            // the pit again, from the opposite direction.
            var overBytes = Monobelisk.Utility.ToBytes(new float[] { 1.004f, 2f }, out toByteSubs);
            Check(toByteSubs == 0 && overBytes[0] == 255 && overBytes[1] == 255,
                "WoDTerrain: ToBytes clamps above-one heights to 255 instead of wrapping through zero");
            // MOBILE: (F1) Unity hands the Metal shader compiler -ffast-math unless the shader says
            // otherwise (the air64 command line in UnityShaderCompiler); `disable_fastmath` is the
            // pragma that removes it and sits in the same recognised-pragma table as `kernel` in
            // 6000.3.23f1. BOTH .compute files need it: they share heightSampling.cginc and
            // noises.cginc, so compiling one with fast-math and one without would make the per-tile
            // terrain disagree with the world heightmap the locations are flattened against.
            // Comments are stripped first, so the note that explains the pragma cannot satisfy this.
            string wodMainCompute = StripShaderComments(File.ReadAllText("Assets/Resources/WoDTerrain/MainHeightmapComputer.compute"));
            string wodTileCompute = StripShaderComments(File.ReadAllText("Assets/Resources/WoDTerrain/TerrainComputer.compute"));
            Check(wodMainCompute.Contains("#pragma disable_fastmath") && wodTileCompute.Contains("#pragma disable_fastmath"),
                "WoDTerrain: both compute shaders are compiled without Metal fast-math");
            // MOBILE: (D3) 100 units was still a visible pit on the device - a player is about two
            // units tall, so that was fifty player heights of wall. 20 units spread over the fade
            // ring (64-576 vertex units) is a 2-5% grade, which reads as ground.
            Check(wodTileCompute.Contains("#define MAX_LOCATION_SINK (20.0 / newHeight)"),
                "WoDTerrain: a location may sink at most 20 units below the world heightmap at the sample");
            Check(Monobelisk.InterestingTerrains.Available(true, true) && !Monobelisk.InterestingTerrains.Available(false, true) && !Monobelisk.InterestingTerrains.Available(true, false),
                "WoDTerrain: needs compute support and both compute shaders");
            Check(Monobelisk.TerrainComputer.StartupBands == 10 && Monobelisk.TerrainComputer.GroupRowsY == 5,
                "WoDTerrain: the world heightmap is dispatched as ten bands of whole thread groups");
            var bands = Monobelisk.TerrainComputer.Bands(mapH, Monobelisk.TerrainComputer.StartupBands);
            int rows = 0; foreach (var b in bands) rows += b.rows;
            int evenBand = mapH / Monobelisk.TerrainComputer.StartupBands;
            Check(bands.Length == Monobelisk.TerrainComputer.StartupBands && rows == mapH && bands[0].yStart == 0
                  && bands[0].rows == evenBand && bands[bands.Length - 1].yStart == mapH - evenBand,
                "WoDTerrain: start-up dispatch splits " + mapH + " rows into " + Monobelisk.TerrainComputer.StartupBands + " bands");
            // Seven bands do not divide MapHeight, so this is the shape the remainder takes: every band
            // but the last a whole number of thread-group rows, the last one larger and taking the rest.
            var odd = Monobelisk.TerrainComputer.Bands(mapH, 7); int r2 = 0; foreach (var b in odd) r2 += b.rows;
            Check(r2 == mapH && odd.Length == 7 && odd[0].rows % Monobelisk.TerrainComputer.GroupRowsY == 0
                  && odd[6].rows == mapH - 6 * odd[0].rows && odd[6].rows > odd[0].rows,
                "WoDTerrain: uneven band split still covers every row, in whole groups (7 bands -> 6x" + odd[0].rows + " + " + odd[6].rows + ")");
            // The second Dispatch argument is a count of THREAD GROUPS, not rows: Dispatch(k, 1000/10,
            // rows/5, 1). A band whose row count is not a multiple of the kernel's y group size (5)
            // therefore either drops rows (integer division) or runs rows that belong to the next
            // band, and nothing downstream would say so - the world heightmap would simply be wrong
            // in horizontal stripes, on a code path that runs once at start-up.
            // Swept over several heights, not just the shipped 500: the invariant this check NAMES is a
            // property of Bands, and the last band - the one that takes the remainder - is exactly the
            // place where a height the sweep never tries could still lose rows.
            bool wholeGroups = true; string groupDetail = "";
            int[] sweptHeights = { 5, 10, 55, 100, 250, 500, 1005 };
            foreach (int h in sweptHeights)
            {
                for (int n = 1; n <= 120 && wholeGroups; n++)
                {
                    var bs = Monobelisk.TerrainComputer.Bands(h, n);
                    int y = 0;
                    foreach (var b in bs)
                    {
                        if (b.yStart != y || b.rows <= 0 || b.rows % Monobelisk.TerrainComputer.GroupRowsY != 0)
                        {
                            wholeGroups = false;
                            groupDetail = "Bands(" + h + ", " + n + "): band at yStart " + b.yStart + " has " + b.rows + " rows";
                            break;
                        }
                        y += b.rows;
                    }
                    if (wholeGroups && y != h)
                    {
                        wholeGroups = false;
                        groupDetail = "Bands(" + h + ", " + n + ") covers " + y + " rows, not " + h;
                    }
                }
                if (!wholeGroups) break;
            }
            Check(wholeGroups, "WoDTerrain: for every accepted height, every band is contiguous and a whole number of 5-row thread groups", groupDetail);
            // The height itself must be a whole number of groups, because the LAST band takes the
            // remainder unrounded: Bands(502, 10) would end in a 52-row band that Dispatch turns into 50
            // rows, and the two lost rows would be written by nobody - the exact failure the banding
            // exists to prevent. MapHeight is a const 500, so this guards a future source change.
            bool refusesOddHeight = false; string oddHeightDetail = "Bands(502, 10) returned without throwing";
            try
            {
                var bad = Monobelisk.TerrainComputer.Bands(502, 10);
                oddHeightDetail = "Bands(502, 10) returned " + bad.Length + " bands";
            }
            catch (System.ArgumentException) { refusesOddHeight = true; oddHeightDetail = ""; }
            Check(refusesOddHeight, "WoDTerrain: a height that is not a whole number of thread-group rows is refused, not truncated", oddHeightDetail);
            // The kernel writes world row y to buffer row (MapHeight - 1 - y), so a band that dispatches
            // rows [yStart, yStart + rows) lands in the MIRRORED range [MapHeight - yStart - rows,
            // MapHeight - yStart). Reading back yStart * MapWidth - the formula the plan carried - would
            // copy a region the band at the other end of the map has not dispatched yet, and the result
            // would be a stale stripe that nothing downstream can detect. Sorted, the ten ranges must
            // tile the whole 500,000-float buffer with no gap and no overlap.
            var ranges = new System.Collections.Generic.List<int[]>();
            foreach (var b in Monobelisk.TerrainComputer.Bands(mapH, Monobelisk.TerrainComputer.StartupBands))
                ranges.Add(new int[] { Monobelisk.TerrainComputer.ReadbackStartRow(mapH, b.yStart, b.rows) * mapW, b.rows * mapW });
            ranges.Sort((a, b) => a[0].CompareTo(b[0]));
            bool tiles = ranges.Count == Monobelisk.TerrainComputer.StartupBands;
            string tileDetail = tiles ? "" : ranges.Count + " ranges, not " + Monobelisk.TerrainComputer.StartupBands;
            int nextStart = 0;
            foreach (var r in ranges)
            {
                if (!tiles) break;
                if (r[0] != nextStart || r[1] <= 0)
                {
                    tiles = false;
                    tileDetail = "range [" + r[0] + ", " + (r[0] + r[1]) + ") does not start at " + nextStart;
                    break;
                }
                nextStart = r[0] + r[1];
            }
            if (tiles && nextStart != mapH * mapW)
            {
                tiles = false;
                tileDetail = "the ranges end at " + nextStart + ", not " + (mapH * mapW);
            }
            Check(tiles, "WoDTerrain: the " + Monobelisk.TerrainComputer.StartupBands + " mirrored readback ranges tile the "
                  + (mapH * mapW) + "-float buffer exactly once", tileDetail);
            // GetBiomeWeights calls SampleBaseHeight unconditionally, and that function indexes shm and
            // lhm with sd/ld/hDim/div. The start-up dispatch bound none of them until this polish round;
            // an unbound - or, with the wrong hDim, a wildly out-of-range - StructuredBuffer read is
            // UNDEFINED on Metal. The value SampleBaseHeight returns is dead on this path (w.land is
            // overwritten with loResBaseHeight when detailedHeights is false), so the only thing that has
            // to hold is that every read lands inside the two dummy buffers, for every index the kernel
            // can build: Idx(id.x, id.y, terrainSize) over id.x 0..999, id.y 0..499.
            var dummy = Monobelisk.TerrainComputer.StartupDummySizes;
            bool sampleInBounds = dummy.shm == 16 && dummy.lhm == 81;
            string sampleDetail = sampleInBounds ? "" : "dummy buffers are " + dummy.shm + "/" + dummy.lhm + ", not 16/81";
            for (int idx = 0; sampleInBounds && idx < mapW * mapH; idx++)
            {
                var reach = Monobelisk.TerrainComputer.StartupSampleMaxIndices(
                    idx, Monobelisk.TerrainComputer.StartupSampleDim, Monobelisk.TerrainComputer.StartupSampleDiv,
                    Monobelisk.TerrainComputer.StartupShmDim, Monobelisk.TerrainComputer.StartupLhmDim);
                if (reach.shm < 0 || reach.shm >= dummy.shm || reach.lhm < 0 || reach.lhm >= dummy.lhm)
                {
                    sampleInBounds = false;
                    sampleDetail = "sample index " + idx + " reaches shm[" + reach.shm + "] lhm[" + reach.lhm
                                 + "] of " + dummy.shm + "/" + dummy.lhm;
                }
            }
            Check(sampleInBounds, "WoDTerrain: the start-up dispatch's sampler uniforms keep every shm/lhm read inside the bound buffers", sampleDetail);
            // A .compute that fails to compile still loads as a NON-NULL asset carrying no kernels, so
            // a null check alone would pass on a broken shader. HasKernel is the compile evidence.
            ComputeShader terrainCS = Resources.Load<ComputeShader>("WoDTerrain/TerrainComputer");
            ComputeShader mainCS = Resources.Load<ComputeShader>("WoDTerrain/MainHeightmapComputer");
            Check(terrainCS != null && mainCS != null
                  && terrainCS.HasKernel("TerrainComputer") && terrainCS.HasKernel("TilemapComputer") && mainCS.HasKernel("CSMain"),
                "WoDTerrain: compute shaders are in Resources and compiled with their three kernels");
            Check(System.Attribute.GetCustomAttributes(typeof(Monobelisk.InterestingTerrains).GetMethod("Init"), typeof(DaggerfallWorkshop.Game.Utility.ModSupport.Invoke), false).Length == 0,
                "WoDTerrain: no [Invoke] survives");

            // A refused start must leave nothing behind. Init takes references to four 2048x1024 RGBA32
            // world maps (~43 MB GPU), a noise map and both compute shaders BEFORE it can know whether
            // the bundle is complete, and the gate that refuses fires on exactly the device that can
            // least afford to keep them. Seeded with a real asset and a scratch texture so the check
            // cannot pass vacuously against statics that were already null.
            Texture2D scratchMap = new Texture2D(2, 2);
            Monobelisk.InterestingTerrains.biomeMap = scratchMap;
            Monobelisk.InterestingTerrains.derivMap = scratchMap;
            Monobelisk.InterestingTerrains.portMap = scratchMap;
            Monobelisk.InterestingTerrains.roadMap = scratchMap;
            Monobelisk.InterestingTerrains.tileableNoise = scratchMap;
            Monobelisk.InterestingTerrains.csPrototype = terrainCS;
            Monobelisk.InterestingTerrains.mainHeightComputer = mainCS;
            Monobelisk.InterestingTerrains.ReleaseAssets();
            Check(Monobelisk.InterestingTerrains.biomeMap == null && Monobelisk.InterestingTerrains.derivMap == null
                  && Monobelisk.InterestingTerrains.portMap == null && Monobelisk.InterestingTerrains.roadMap == null
                  && Monobelisk.InterestingTerrains.tileableNoise == null
                  && Monobelisk.InterestingTerrains.csPrototype == null && Monobelisk.InterestingTerrains.mainHeightComputer == null,
                "WoDTerrain: a refused start drops the five maps and both compute shaders");
            UnityEngine.Object.DestroyImmediate(scratchMap);

            // The world heightmap is generated before the sampler is installed, and it rewrites
            // ContentReader.WoodsFileReader.Buffer - the travel map's own data - part way through. If it
            // throws, the buffer has to go back, and only then: with no copy of the original taken there
            // is nothing to restore and assigning null would blank the travel map instead of sparing it.
            byte[] originalCopy = new byte[4];
            byte[] altered = new byte[4];
            Check(Monobelisk.InterestingTerrains.ShouldRestoreWoodsBuffer(originalCopy, altered)
                  && !Monobelisk.InterestingTerrains.ShouldRestoreWoodsBuffer(null, altered)
                  && !Monobelisk.InterestingTerrains.ShouldRestoreWoodsBuffer(originalCopy, originalCopy),
                "WoDTerrain: a half-generated world heightmap puts WOODS.WLD back, and only when there is something to put back");

            TestWoDTerrainRoads();
        }

        // MOBILE: (R1) Basic Roads smoothing. The mod's road-aware branch was behind
        // `CompatibilityUtils.BasicRoadsLoaded`, i.e. "is there a mod titled BasicRoads" - which on
        // iOS is never true, because Basic Roads is compiled into the port. So the shader was handed
        // nine zero vectors for every tile and roads were painted across terrain generated as if they
        // did not exist. These pin the replacement gather: the byte source is injected, so this runs
        // with no mod, no world and no GPU, and the two things that could go wrong silently - the
        // direction bits meaning something different from MobileRoadNetwork's, and the 3x3 slot index
        // disagreeing with basicRoads.cginc - are exactly what is checked.
        static void TestWoDTerrainRoads()
        {
            // Written with MobileRoadNetwork's own constants, not literals: that is what pins the two
            // copies of Basic Roads' bitmask (N = 128 ... NW = 1) against each other. If either side
            // renumbers, roads get smoothed in the wrong direction and nothing else says so.
            const int centre = 4;   // si = (0 + 1) + (0 + 1) * 3
            int pathPixels;
            var oneTile = Monobelisk.Compatibility.BasicRoadsUtils.BuildRoadData(207, 213,
                (x, y) => (x == 207 && y == 213)
                    ? (byte)(DaggerfallWorkshop.Game.Mobile.MobileRoadNetwork.N
                           | DaggerfallWorkshop.Game.Mobile.MobileRoadNetwork.E)
                    : (byte)0,
                out pathPixels);
            Check(pathPixels == 1
                  && oneTile.N_E_S_W[centre] == new Vector4(1, 1, 0, 0)
                  && oneTile.NW_NE_SW_SE[centre] == Vector4.zero,
                "WoDTerrain roads: a north/east road at the tile's own map pixel reaches the shader's centre slot",
                "pathPixels " + pathPixels + " N_E_S_W " + oneTile.N_E_S_W[centre] + " NW_NE_SW_SE " + oneTile.NW_NE_SW_SE[centre]);

            // The neighbour slot, in the shader's own indexing: the pixel one east and one north
            // (map-pixel y - 1) is si = (1 + 1) + (-1 + 1) * 3 = 2.
            var neighbour = Monobelisk.Compatibility.BasicRoadsUtils.BuildRoadData(207, 213,
                (x, y) => (x == 208 && y == 212)
                    ? DaggerfallWorkshop.Game.Mobile.MobileRoadNetwork.SE : (byte)0,
                out pathPixels);
            Check(pathPixels == 1
                  && neighbour.NW_NE_SW_SE[2] == new Vector4(0, 0, 0, 1)
                  && neighbour.NW_NE_SW_SE[centre] == Vector4.zero,
                "WoDTerrain roads: a neighbouring map pixel's road lands in that neighbour's slot, not the centre's",
                "pathPixels " + pathPixels + " slot2 " + neighbour.NW_NE_SW_SE[2]);

            // At the map's western edge the x = -1 column does not exist. Upstream skips it, which
            // leaves those three slots zero rather than wrapping to map pixel 999 - a road smoothed
            // in from the far side of Tamriel.
            var edge = Monobelisk.Compatibility.BasicRoadsUtils.BuildRoadData(0, 213,
                (x, y) => 0xFF, out pathPixels);
            Check(pathPixels == 6
                  && edge.N_E_S_W[0] == Vector4.zero && edge.N_E_S_W[3] == Vector4.zero && edge.N_E_S_W[6] == Vector4.zero
                  && edge.N_E_S_W[centre] == new Vector4(1, 1, 1, 1)
                  && edge.NW_NE_SW_SE[centre] == new Vector4(1, 1, 1, 1),
                "WoDTerrain roads: the column off the western edge of the map stays empty instead of wrapping",
                "pathPixels " + pathPixels + " (expected 6)");

            // And before Init has run - the editor's state, and the state of any session with the
            // roads switch off - GetRoadData must hand the shader nine zero vectors rather than
            // throw on a null path array, which is what upstream's HasRoadPoint did.
            var idle = Monobelisk.Compatibility.BasicRoadsUtils.GetRoadData(207, 213);
            bool allZero = idle.N_E_S_W != null && idle.NW_NE_SW_SE != null
                           && idle.N_E_S_W.Length == 9 && idle.NW_NE_SW_SE.Length == 9;
            for (int i = 0; allZero && i < 9; i++)
                allZero = idle.N_E_S_W[i] == Vector4.zero && idle.NW_NE_SW_SE[i] == Vector4.zero;
            Check(allZero, "WoDTerrain roads: with no path source the shader gets nine zero vectors, not an exception");

            // The gather is now unconditional at start-up - the whole bug was a start-up gate that
            // could not fire on this platform - so the gate's absence is the check.
            string terrainsSrc = File.ReadAllText("Assets/Scripts/Game/Mobile/Ports/WorldOfDaggerfallTerrain/InterestingTerrains.cs");
            Check(!terrainsSrc.Contains("if (CompatibilityUtils.BasicRoadsLoaded)"),
                "WoDTerrain roads: BasicRoadsUtils.Init is no longer gated on a mod titled BasicRoads being loaded");
        }

        // The Dynamic Skies mod's procedural skybox shader ships compiled into the app with its
        // keyword variants pinned: once the variants are #defines, a material's keyword list has
        // no effect on rendering, so what must be proven is that the shader's own keyword space
        // no longer carries the moon/sun/colour variants, while the fog variants (Unity's own
        // multi_compile_fog) remain.
        static void TestDynamicSkiesShader()
        {
            Shader sky = Shader.Find("BLB/SkyBox/BLBProceduralSkybox");
            Check(sky != null, "DynamicSkies: skybox shader is in the project");
            if (sky == null) return;
            Check(sky.isSupported, "DynamicSkies: skybox shader compiles for this editor's graphics API");
            var names = sky.keywordSpace.keywordNames;
            Check(!names.Any(n => n.StartsWith("_MOONSPINOPTION") || n.StartsWith("_SECUNDASPINOPTION") || n.StartsWith("_SUNDISK") || n == "REDUCE_COLOR" || n == "PHASE_LIGHT"), "DynamicSkies: no moon/sun/colour keywords remain in the shader (variants pinned)");
            Check(names.Any(n => n.StartsWith("FOG_")), "DynamicSkies: fog variants remain");
            string src = System.IO.File.ReadAllText("Assets/Shaders/BLB/BLBProceduralSkybox.shader");
            Check(!src.Contains("#pragma multi_compile _") && !src.Contains("multi_compile_local"), "DynamicSkies: keyword pragmas are pinned (fog variants only)");
            Check(src.Contains("#pragma target 3.5"), "DynamicSkies: shader target pinned");
            Check(!src.Contains("sampler3D"), "DynamicSkies: unused 3D LUT sampler removed");
            // The four variants the removed keywords used to select are now hard-defined. Losing
            // one of these defines with its keyword already gone silently drops a feature (a moon
            // that spins, a full-colour sky, a low-quality sun disk) instead of failing to build.
            Check(src.Contains("#define _MOONSPINOPTION_TIDAL_LOCK"), "DynamicSkies: Masser tidal lock pinned by define");
            Check(src.Contains("#define _SECUNDASPINOPTION_TIDAL_LOCK"), "DynamicSkies: Secunda tidal lock pinned by define");
            Check(src.Contains("#define REDUCE_COLOR"), "DynamicSkies: colour reduction pinned by define");
            Check(src.Contains("#define _SUNDISK_HIGH_QUALITY"), "DynamicSkies: high-quality sun disk pinned by define");
        }



        /// <summary>
        /// Reads one section of an ini file into a dictionary. Enough of an ini parser for a
        /// defaults table: `[Section]` headers and `key=value` lines, `;`/`#` comments dropped.
        /// </summary>
        static Dictionary<string, string> ReadIniSection(string path, string section)
        {
            var values = new Dictionary<string, string>();
            bool inSection = false;
            foreach (string raw in File.ReadAllLines(path))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line[0] == ';' || line[0] == '#')
                    continue;
                if (line[0] == '[')
                {
                    inSection = line.Trim('[', ']') == section;
                    continue;
                }
                if (!inSection)
                    continue;
                int eq = line.IndexOf('=');
                if (eq > 0)
                    values[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
            }
            return values;
        }

        // The CRT presentation filter: our own single-pass shader (Daggerfall/Mobile/CRT) used as
        // the material argument of the one Graphics.Blit in RetroPresentation.OnRenderImage.
        // Three kinds of check, because the failure modes are of three kinds.
        //
        // 1. The pure rules (MobileCrt), called for real. The scanline count is the one that
        //    matters visually: it must come from the SOURCE raster (200 or 400 lines, from
        //    RetroRenderingMode) and never from device pixels, or a high-DPI panel moires.
        // 2. The shader as an asset: present, compiles, ONE texture fetch in the cheap tier, and
        //    pinned three ways (Always-Included list, the preloaded variant collection, the
        //    resulting GraphicsSettings entry) because it is resolved by name at runtime and
        //    referenced by no material in any scene - exactly the shape the build pipeline strips.
        // 3. The settings table. The research found two UNCLAMPED retro keys: a settings.ini
        //    carrying PalettizationLUTShift=0 makes RetroRenderer build a 64 MB Texture3D and
        //    stall for seconds (the code's own comment table says so), and
        //    PostProcessingInRetroMode outside 0..4 leaves postprocessMaterial null, which
        //    silently turns retro mode off. So the clamps are checked as source text AND
        //    behaviourally, by feeding the live SettingsManager a rogue ini value.
        static void TestMobileCRT()
        {
            // ---- 1. the pure rules ----
            Check(MobileCrt.Clamp01(-1f) == 0f && MobileCrt.Clamp01(0.5f) == 0.5f && MobileCrt.Clamp01(2f) == 1f,
                "MobileCRT: Clamp01 clamps both ends and passes the middle through",
                MobileCrt.Clamp01(-1f) + " / " + MobileCrt.Clamp01(0.5f) + " / " + MobileCrt.Clamp01(2f));
            Check(MobileCrt.ScanlineCount(1) == 200, "MobileCRT: 320x200 retro mode draws 200 scanlines",
                MobileCrt.ScanlineCount(1).ToString());
            Check(MobileCrt.ScanlineCount(2) == 400, "MobileCRT: 640x400 retro mode draws 400 scanlines",
                MobileCrt.ScanlineCount(2).ToString());
            // Mode 0 never reaches the shader (Active is false there), but the count must still be
            // a usable raster rather than 0: a zero would collapse the scanline phase to a constant.
            Check(MobileCrt.ScanlineCount(0) == 200, "MobileCRT: retro off still reports a usable scanline count",
                MobileCrt.ScanlineCount(0).ToString());

            // The count must come from the SOURCE raster's own height, not from the retro mode:
            // with LargeHUD + LargeHUDDocked, RetroRenderer.UpdateRenderTarget points the camera at
            // RetroTexture320x200_HUD (320x154) or RetroTexture640x400_HUD (640x308) and blits that
            // into the 640x400 presentation texture, stretched. Drawing 200 lines over a 154-line
            // raster beats against it at |200-154| = 46 cycles down the screen - the exact moire the
            // shader exists to avoid. ScanlineCountFor is the pure form; a raster height of 0 (no
            // renderer yet, headless) falls back to the mode's nominal count.
            Check(MobileCrt.ScanlineCountFor(154, 1) == 154,
                "MobileCRT: a docked large HUD's 320x154 raster draws 154 scanlines",
                MobileCrt.ScanlineCountFor(154, 1).ToString());
            Check(MobileCrt.ScanlineCountFor(308, 2) == 308,
                "MobileCRT: a docked large HUD's 640x308 raster draws 308 scanlines",
                MobileCrt.ScanlineCountFor(308, 2).ToString());
            Check(MobileCrt.ScanlineCountFor(200, 1) == 200 && MobileCrt.ScanlineCountFor(400, 2) == 400,
                "MobileCRT: an undocked raster draws its own 200 / 400 lines");
            Check(MobileCrt.ScanlineCountFor(0, 1) == 200 && MobileCrt.ScanlineCountFor(0, 2) == 400
                  && MobileCrt.ScanlineCountFor(-1, 2) == 400,
                "MobileCRT: no live raster falls back to the retro mode's nominal count",
                MobileCrt.ScanlineCountFor(0, 1) + " / " + MobileCrt.ScanlineCountFor(0, 2));
            // The editor has no GameManager, so the live path is the fallback here - what matters is
            // that it does not throw and does not answer zero.
            Check(MobileCrt.LiveRasterHeight == 0,
                "MobileCRT: LiveRasterHeight is 0 with no running game (headless-safe)",
                MobileCrt.LiveRasterHeight.ToString());

            foreach (int mode in new[] { 0, 1, 2 })
                foreach (bool enabled in new[] { false, true })
                    foreach (bool materialOk in new[] { false, true })
                    {
                        bool expected = enabled && mode != 0 && materialOk;
                        Check(MobileCrt.Active(enabled, mode, materialOk) == expected,
                            string.Format("MobileCRT: Active(enabled={0}, retroMode={1}, materialOk={2}) is {3}",
                                enabled, mode, materialOk, expected));
                    }

            // ---- 2. the shader ----
            const string shaderName = "Daggerfall/Mobile/CRT";
            Check(MobileCrt.ShaderName == shaderName, "MobileCRT: MobileCrt.ShaderName is the shader's name", MobileCrt.ShaderName);
            Shader crt = Shader.Find(shaderName);
            Check(crt != null, "MobileCRT: the CRT shader is in the project");
            Check(MobileShaders.Names.Contains(shaderName),
                "MobileCRT: the shader name is captured by MobileShaders (a mod bundle cannot shadow it)");
            Shader viaMobile = MobileShaders.Find(shaderName);
            Check(viaMobile != null && viaMobile.name == shaderName,
                "MobileCRT: MobileShaders.Find resolves the CRT shader");
            if (crt != null)
                Check(crt.isSupported, "MobileCRT: the CRT shader compiles for this editor's graphics API");

            const string shaderPath = "Assets/Shaders/Mobile/MobileCRT.shader";
            Check(File.Exists(shaderPath), "MobileCRT: " + shaderPath + " exists");
            if (File.Exists(shaderPath))
            {
                string raw = File.ReadAllText(shaderPath);
                Check(raw.Contains("License:         MIT License"), "MobileCRT: the shader carries the port's MIT header");
                // Comment-stripped from here on: the header names the GPL CRT shaders on purpose
                // (to say no code came from them), and an "it is not there" check that a comment
                // can satisfy is worthless.
                string src = StripShaderComments(raw);
                Check(src.Contains("Shader \"" + shaderName + "\""), "MobileCRT: the shader declares that name");
                Check(src.Contains("#pragma target 3.0"), "MobileCRT: shader target is 3.0");
                Check(src.Contains("#pragma multi_compile __ CRT_HALATION"),
                    "MobileCRT: the halation tier is one multi_compile with an off-by-default default");
                foreach (string uniform in new[] { "_MainTex", "_Curvature", "_Scanlines", "_ScanlineCount", "_Mask", "_Vignette" })
                    Check(src.Contains(uniform), "MobileCRT: the shader declares " + uniform);

                // The three defects the Task 1 review found, pinned as source text as well as by
                // the rendered check below.
                //
                // The vignette factor must be saturated. r2 = dot(centred, centred) reaches ~1.58
                // while still INSIDE the curved screen, so an unsaturated 1 - _Vignette * r2 goes
                // negative for any _Vignette above ~0.63 - and the slider's range is 0..1.
                Check(src.Contains("saturate(1.0 - _Vignette * r2)"),
                    "MobileCRT: the vignette factor is saturated (a negative factor would invert lit pixels)");
                // Scanline phase: raster = uv.y * _ScanlineCount, so an INTEGER raster is the seam
                // between two source rows and a half-integer is a row's centre. A CRT is brightest
                // along the centre of each line, so the darkening must peak on the integer - which
                // is 0.5 + 0.5 * cos(2*pi*raster), not 0.5 - 0.5 * cos.
                Check(CountOccurrences(src, "0.5 + 0.5 * cos(") == 3,
                    "MobileCRT: all three scanline evaluations are 0.5 + 0.5 * cos (dark on the seam)",
                    CountOccurrences(src, "0.5 + 0.5 * cos(") + " of 3");
                Check(!src.Contains("0.5 - 0.5 * cos("),
                    "MobileCRT: no inverted scanline phase survives (0.5 - 0.5 * cos would darken row centres)");
                // No gamma round trip: the project is Linear and the presentation render texture is
                // not sRGB, so tex2D already hands back linear light. Squaring the sample and taking
                // the square root at the end is an identity on the picture that applies every
                // modulation as sqrt(m) - about half the authored depth.
                Check(!src.Contains("sqrt(") && !src.Contains("col *= col"),
                    "MobileCRT: no fake gamma round trip (the project is Linear; modulations apply directly)");
                // The cost contract, and the whole reason this shader can ship on a 5.6 MP iPad:
                // the cheap tier is ONE dependent-read-free fetch, and the only extra fetches are
                // the two halation taps behind the keyword.
                int halationStart = src.IndexOf("#ifdef CRT_HALATION", StringComparison.Ordinal);
                Check(halationStart >= 0, "MobileCRT: the halation taps sit behind #ifdef CRT_HALATION");
                int fetches = CountOccurrences(src, "tex2D(");
                if (halationStart >= 0)
                {
                    int halationEnd = src.IndexOf("#endif", halationStart, StringComparison.Ordinal);
                    string halation = halationEnd > halationStart ? src.Substring(halationStart, halationEnd - halationStart) : "";
                    Check(CountOccurrences(halation, "tex2D(") == 2,
                        "MobileCRT: the halation tier adds exactly two taps",
                        CountOccurrences(halation, "tex2D(") + " taps");
                    Check(fetches - CountOccurrences(halation, "tex2D(") == 1,
                        "MobileCRT: the cheap tier is exactly one texture fetch",
                        fetches + " fetches in the file");
                }
                // Not a licence audit - a tripwire. No code from any of these was used or read;
                // if a name ever turns up in the CODE, someone pasted something in.
                foreach (string gpl in new[] { "crt-pi", "crt-geom", "crt-easymode", "crt-royale", "crt-lottes", "zfast" })
                    Check(!src.Contains(gpl), "MobileCRT: no " + gpl + " provenance in the shader code");
            }

            // Pinned three ways, because each alone is silently survivable until the filter turns
            // up as a null material (or a pink screen) on device.
            string guid = AssetDatabase.AssetPathToGUID(shaderPath);
            Check(!string.IsNullOrEmpty(guid), "MobileCRT: the shader asset has a GUID (it imported)");
            Check(File.ReadAllText("Assets/Editor/MobileBuildSetup.cs").Contains(shaderName),
                "MobileCRT: shader is in MobileBuildSetup's EnsureAlwaysIncludedShaders list");
            Check(!string.IsNullOrEmpty(guid) && File.ReadAllText("Assets/Shaders/RequiredShaderVariants.shadervariants").Contains(guid),
                "MobileCRT: shader has an entry in RequiredShaderVariants (variants survive stripping)");
            Check(!string.IsNullOrEmpty(guid) && File.ReadAllText("ProjectSettings/GraphicsSettings.asset").Contains(guid),
                "MobileCRT: ApplyIOSSettings pinned the shader into GraphicsSettings' always-included list");
            var collection = AssetDatabase.LoadAssetAtPath<ShaderVariantCollection>("Assets/Shaders/RequiredShaderVariants.shadervariants");
            Check(collection != null, "MobileCRT: the required-variants collection loads");
            if (collection != null && crt != null)
            {
                foreach (string[] keywords in new[] { new string[0], new[] { "CRT_HALATION" } })
                {
                    string label = keywords.Length == 0 ? "<no keywords>" : keywords[0];
                    try
                    {
                        Check(collection.Contains(new ShaderVariantCollection.ShaderVariant(crt, UnityEngine.Rendering.PassType.Normal, keywords)),
                            "MobileCRT: variant " + label + " is in the required-variants collection");
                    }
                    catch (ArgumentException ex)
                    {
                        Check(false, "MobileCRT: variant " + label + " is in the required-variants collection", ex.Message);
                    }
                }
            }

            // ---- the hook ----
            // RetroPresentation.cs is an upstream file of 31 lines, so an upstream merge that takes
            // theirs quietly removes the whole filter. Source text is the only guard: OnRenderImage
            // is a MonoBehaviour callback the editor never invokes.
            string presenter = StripShaderComments(File.ReadAllText("Assets/Scripts/Utility/RetroPresentation.cs"));
            Check(presenter.Contains("Graphics.Blit(RetroPresentationSource, null as RenderTexture, "),
                "MobileCRT hook: the presentation blit has a material overload");
            Check(presenter.Contains("Graphics.Blit(RetroPresentationSource, null as RenderTexture);"),
                "MobileCRT hook: the plain blit survives as the off path");
            Check(presenter.Contains("MobileCrt.Active("),
                "MobileCRT hook: the material path is gated by MobileCrt.Active");
            Check(presenter.Contains("MobileShaders.Find(MobileCrt.ShaderName)"),
                "MobileCRT hook: the material is built from MobileShaders.Find, not a raw Shader.Find");
            // "Shader.Find(" is not a substring of "MobileShaders.Find(" - the plural puts
            // "Shaders." before ".Find" - so this really does catch a raw lookup.
            Check(!presenter.Contains("Shader.Find("),
                "MobileCRT hook: no raw Shader.Find survives");

            // ---- 3. the settings table ----
            string settingsSrc = File.ReadAllText("Assets/Scripts/SettingsManager.cs");
            string[] loadLines =
            {
                "PostProcessingInRetroMode = GetInt(sectionVideo, \"PostProcessingInRetroMode\", 0, 4);",
                "PalettizationLUTShift = GetInt(sectionVideo, \"PalettizationLUTShift\", 1, 3);",
                "CRTFilter = GetBool(sectionVideo, \"CRTFilter\");",
                "CRTCurvature = GetFloat(sectionVideo, \"CRTCurvature\", 0f, 0.3f);",
                "CRTScanlines = GetFloat(sectionVideo, \"CRTScanlines\", 0f, 1f);",
                "CRTMask = GetFloat(sectionVideo, \"CRTMask\", 0f, 1f);",
                "CRTVignette = GetFloat(sectionVideo, \"CRTVignette\", 0f, 1f);",
            };
            foreach (string line in loadLines)
                Check(settingsSrc.Contains(line), "MobileCRT settings: LoadSettings reads `" + line.Trim() + "`");
            string[] saveLines =
            {
                "SetBool(sectionVideo, \"CRTFilter\", CRTFilter);",
                "SetFloat(sectionVideo, \"CRTCurvature\", CRTCurvature);",
                "SetFloat(sectionVideo, \"CRTScanlines\", CRTScanlines);",
                "SetFloat(sectionVideo, \"CRTMask\", CRTMask);",
                "SetFloat(sectionVideo, \"CRTVignette\", CRTVignette);",
            };
            foreach (string line in saveLines)
                Check(settingsSrc.Contains(line), "MobileCRT settings: SaveSettings writes `" + line.Trim() + "`");

            var defaults = ReadIniSection("Assets/Resources/defaults.ini.txt", "Video");
            string[][] expectedDefaults =
            {
                new[] { "CRTFilter", "False" },
                new[] { "CRTCurvature", "0.08" },
                new[] { "CRTScanlines", "0.35" },
                new[] { "CRTMask", "0.25" },
                new[] { "CRTVignette", "0.25" },
                // The iOS default the research asked for: shift 1 is an 8 MB LUT and ~850 ms to
                // build on a desktop; shift 2 is 1 MB and "slightly less crisp".
                new[] { "PalettizationLUTShift", "2" },
            };
            foreach (string[] pair in expectedDefaults)
            {
                string got;
                Check(defaults.TryGetValue(pair[0], out got) && got == pair[1],
                    "MobileCRT settings: defaults.ini [Video] " + pair[0] + "=" + pair[1],
                    defaults.TryGetValue(pair[0], out got) ? "found " + got : "key missing");
            }

            SettingsManager live = null;
            try { live = DaggerfallUnity.Settings; }
            catch (Exception ex) { log.AppendLine("  note  MobileCRT settings: live SettingsManager threw: " + ex.Message); }
            Check(live != null, "MobileCRT settings: the live SettingsManager loads");
            if (live != null)
            {
                foreach (string name in new[] { "CRTFilter", "CRTCurvature", "CRTScanlines", "CRTMask", "CRTVignette" })
                {
                    var prop = typeof(SettingsManager).GetProperty(name);
                    Type want = name == "CRTFilter" ? typeof(bool) : typeof(float);
                    Check(prop != null && prop.PropertyType == want,
                        "MobileCRT settings: SettingsManager exposes " + name + " as " + want.Name,
                        prop == null ? "property missing" : "is " + prop.PropertyType.Name);
                }

                // Behavioural clamping. The private helpers are what LoadSettings' right-hand sides
                // are, and SetData is how a rogue settings.ini value gets in front of them; nothing
                // is saved, so the editor's own settings.ini is untouched.
                const BindingFlags priv = BindingFlags.Instance | BindingFlags.NonPublic;
                var setData = typeof(SettingsManager).GetMethod("SetData", priv);
                var getData = typeof(SettingsManager).GetMethod("GetData", priv);
                var getInt = typeof(SettingsManager).GetMethod("GetInt", priv, null, new[] { typeof(string), typeof(string), typeof(int), typeof(int) }, null);
                var getFloat = typeof(SettingsManager).GetMethod("GetFloat", priv, null, new[] { typeof(string), typeof(string), typeof(float), typeof(float) }, null);
                Check(setData != null && getData != null && getInt != null && getFloat != null,
                    "MobileCRT settings: the SettingsManager ini helpers are reachable for the clamp check");
                if (setData != null && getData != null && getInt != null && getFloat != null)
                {
                    // key, rogue value, expected clamp result, min, max
                    var intCases = new[]
                    {
                        new object[] { "PalettizationLUTShift", "0", 1, 1, 3 },
                        new object[] { "PalettizationLUTShift", "9", 3, 1, 3 },
                        new object[] { "PostProcessingInRetroMode", "-1", 0, 0, 4 },
                        new object[] { "PostProcessingInRetroMode", "7", 4, 0, 4 },
                    };
                    foreach (object[] c in intCases)
                    {
                        string key = (string)c[0];
                        string original = (string)getData.Invoke(live, new object[] { "Video", key });
                        setData.Invoke(live, new object[] { "Video", key, (string)c[1] });
                        int got = (int)getInt.Invoke(live, new object[] { "Video", key, c[3], c[4] });
                        setData.Invoke(live, new object[] { "Video", key, original });
                        Check(got == (int)c[2],
                            "MobileCRT settings: a settings.ini " + key + "=" + c[1] + " reaches the app as " + c[2],
                            "got " + got);
                    }
                    var floatCases = new[]
                    {
                        new object[] { "CRTCurvature", "9", 0.3f, 0f, 0.3f },
                        new object[] { "CRTCurvature", "-1", 0f, 0f, 0.3f },
                        new object[] { "CRTScanlines", "-1", 0f, 0f, 1f },
                        new object[] { "CRTMask", "5", 1f, 0f, 1f },
                        new object[] { "CRTVignette", "5", 1f, 0f, 1f },
                    };
                    foreach (object[] c in floatCases)
                    {
                        string key = (string)c[0];
                        string original = (string)getData.Invoke(live, new object[] { "Video", key });
                        setData.Invoke(live, new object[] { "Video", key, (string)c[1] });
                        float got = (float)getFloat.Invoke(live, new object[] { "Video", key, c[3], c[4] });
                        setData.Invoke(live, new object[] { "Video", key, original });
                        Check(Mathf.Abs(got - (float)c[2]) < 1e-6f,
                            "MobileCRT settings: a settings.ini " + key + "=" + c[1] + " reaches the app as " + c[2],
                            "got " + got);
                    }
                }
            }
        }

        // F5 of the Task 1 review: the shader's OUTPUT, not its source text. A solid mid-grey
        // source blitted through the real material, read back, and three pixels examined. This
        // first pass is the check that would have caught the inverted scanline phase (F2), and it
        // pins "outside the curved screen is black" as behaviour rather than as a comment. The
        // vignette (F1) needs a float target to be visible at all and gets its own pass at the end
        // of this method - see F14 of the Task 2 review there.
        // Curvature 0.3 (the clamp's maximum) so the corner is unambiguously off-texture;
        // scanlines 1 so the modulation is at full depth; mask and vignette 0 so nothing else
        // moves a pixel. _ScanlineCount is 9 - an ODD count puts the exact centre of the image on
        // a row CENTRE (raster 4.5) rather than on a seam, which is what makes "the centre pixel
        // is not black" a meaningful assertion.
        static void TestMobileCRTRender()
        {
            const int dim = 256;
            const float curvature = 0.3f;
            const int count = 9;

            Shader shader = MobileShaders.Find(MobileCrt.ShaderName);
            Check(shader != null, "MobileCRT render: the shader resolves for the rendered check");
            if (shader == null)
                return;

            Material mat = new Material(shader);
            mat.SetFloat("_Curvature", curvature);
            mat.SetFloat("_Scanlines", 1f);
            mat.SetFloat("_ScanlineCount", count);
            mat.SetFloat("_Mask", 0f);
            mat.SetFloat("_Vignette", 0f);

            // Mid-grey, Point-filtered, linear (the presentation render texture is not sRGB either).
            Texture2D source = new Texture2D(8, 8, TextureFormat.RGBA32, false, true);
            Color32 grey = new Color32(128, 128, 128, 255);
            Color32[] fill = new Color32[8 * 8];
            for (int i = 0; i < fill.Length; i++)
                fill[i] = grey;
            source.SetPixels32(fill);
            source.Apply(false);
            source.filterMode = FilterMode.Point;
            source.wrapMode = TextureWrapMode.Clamp;

            RenderTexture rt = new RenderTexture(dim, dim, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            Color32[] px = null;
            if (rt.Create())
            {
                Graphics.Blit(source, rt, mat);
                RenderTexture wasActive = RenderTexture.active;
                RenderTexture.active = rt;
                Texture2D readback = new Texture2D(dim, dim, TextureFormat.RGBA32, false, true);
                readback.ReadPixels(new Rect(0, 0, dim, dim), 0, 0);
                readback.Apply(false);
                RenderTexture.active = wasActive;
                px = readback.GetPixels32();
                UnityEngine.Object.DestroyImmediate(readback);
            }
            rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);
            UnityEngine.Object.DestroyImmediate(source);
            UnityEngine.Object.DestroyImmediate(mat);

            Check(px != null && px.Length == dim * dim, "MobileCRT render: the CRT material blits and reads back");
            if (px == null || px.Length != dim * dim)
                return;

            // The centre column, so the barrel warp's x term is ~0 and uv.y is a function of the
            // row alone. Rows are searched for the two phases rather than hardcoded: the warp moves
            // uv.y away from the destination v, so which row lands on a seam is not (row / dim).
            //
            // Orientation does not matter here, and that is not luck: uv.y(1 - v) = 1 - uv.y(v)
            // under this warp, and cos(2*pi*(count - x)) == cos(2*pi*x) for an integer count, so a
            // vertically flipped blit gives the SAME scanline value for the same readback row.
            const int col = dim / 2;
            int seamRow = -1, centreRow = -1;
            float seamBest = 1f, centreBest = 1f;
            for (int row = dim / 2 - 32; row <= dim / 2 + 32; row++)
            {
                float phase = ScanlinePhase(row, col, dim, curvature, count);
                float toSeam = Mathf.Min(phase, 1f - phase);          // distance to an integer raster
                float toCentre = Mathf.Abs(phase - 0.5f);             // distance to a half-integer
                if (toSeam < seamBest) { seamBest = toSeam; seamRow = row; }
                if (toCentre < centreBest) { centreBest = toCentre; centreRow = row; }
            }

            int corner = px[0].r;
            int centreOfImage = px[(dim / 2) * dim + dim / 2].r;
            int seam = px[seamRow * dim + col].r;
            int rowCentre = px[centreRow * dim + col].r;
            log.AppendLine(string.Format(
                "  note  MobileCRT render: grey 128 in, curvature {0} / scanlines 1 / mask 0 / vignette 0, {1} lines over {2} rows -> corner(0,0)={3}, centre({4},{4})={5}, seam(row {6}, phase {7:0.000})={8}, row centre(row {9}, phase {10:0.000})={11}",
                curvature, count, dim, corner, dim / 2, centreOfImage,
                seamRow, ScanlinePhase(seamRow, col, dim, curvature, count), seam,
                centreRow, ScanlinePhase(centreRow, col, dim, curvature, count), rowCentre));

            // 1. Outside the curved screen there is no tube. At curvature 0.3 the corner samples
            //    uv.y ~ -0.29, so `inside` is 0 and the pixel is black - not dim, black.
            Check(corner <= 2, "MobileCRT render: the corner pixel is black (outside the curved screen)",
                "got " + corner);
            // 2. The middle of the picture is lit. With an odd scanline count the image centre sits
            //    on a row centre, where a CRT is at full brightness.
            Check(centreOfImage >= 64, "MobileCRT render: the centre pixel is lit (not black)",
                "got " + centreOfImage + " of 128");
            // 3. And the phase is the right way round: dark on the seam between two source rows,
            //    bright along the centre of a row. Inverted (0.5 - 0.5*cos) this comparison flips.
            Check(seam < rowCentre, "MobileCRT render: a scanline seam is darker than a row centre",
                "seam " + seam + " vs row centre " + rowCentre);
            Check(seam <= 16 && rowCentre >= 96,
                "MobileCRT render: at full depth the seam goes to black and the row centre keeps its value",
                "seam " + seam + " vs row centre " + rowCentre);

            // F14 of the Task 2 review. Everything above runs at _Vignette = 0, and on the ARGB32
            // target it could not have caught F1 even at _Vignette = 1: now that the fake gamma's
            // sqrt is gone, an unsaturated `1 - _Vignette * r2 < 0` multiplied into the colour
            // writes a negative that a unorm target clamps to 0 - pixel-identical to the saturated
            // form. A FLOAT target is the only readback that tells the two shaders apart, so the
            // vignette gets its own pass here rather than a claim it cannot support.
            //
            // Why there is a negative to find. r2 is dot(centred, centred) on the UNDISTORTED
            // coordinate while `inside` is a mask on the WARPED uv, so the largest r2 still inside
            // the screen is set by m * (1 + _Curvature * 2m^2) <= 1 along the diagonal: m ~ 0.748
            // at curvature 0.3, i.e. r2 ~ 1.12, and the unsaturated factor there is about -0.12.
            // Against a 0.502 source that is about -0.057 over ~348 of the 65,536 pixels, in a
            // lens-shaped band along the edges. Scanlines and mask are 0, so the vignette is the
            // only term that can drive a channel below zero.
            if (!SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf))
            {
                log.AppendLine("  SKIP  MobileCRT render: ARGBHalf is unsupported on this editor's graphics API, so the negative-channel check cannot run");
                return;
            }

            Material vmat = new Material(shader);
            vmat.SetFloat("_Curvature", curvature);
            vmat.SetFloat("_Scanlines", 0f);
            vmat.SetFloat("_ScanlineCount", count);
            vmat.SetFloat("_Mask", 0f);
            vmat.SetFloat("_Vignette", 1f);

            Texture2D vsource = new Texture2D(8, 8, TextureFormat.RGBA32, false, true);
            vsource.SetPixels32(fill);
            vsource.Apply(false);
            vsource.filterMode = FilterMode.Point;
            vsource.wrapMode = TextureWrapMode.Clamp;

            RenderTexture vrt = new RenderTexture(dim, dim, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
            Color[] vpx = null;
            if (vrt.Create())
            {
                Graphics.Blit(vsource, vrt, vmat);
                RenderTexture wasActive = RenderTexture.active;
                RenderTexture.active = vrt;
                // RGBAHalf, and GetPixels rather than GetPixels32: a Color32 conversion would clamp
                // the very negative this pass exists to see.
                Texture2D readback = new Texture2D(dim, dim, TextureFormat.RGBAHalf, false, true);
                readback.ReadPixels(new Rect(0, 0, dim, dim), 0, 0);
                readback.Apply(false);
                RenderTexture.active = wasActive;
                vpx = readback.GetPixels();
                UnityEngine.Object.DestroyImmediate(readback);
            }
            vrt.Release();
            UnityEngine.Object.DestroyImmediate(vrt);
            UnityEngine.Object.DestroyImmediate(vsource);
            UnityEngine.Object.DestroyImmediate(vmat);

            Check(vpx != null && vpx.Length == dim * dim,
                "MobileCRT render: the CRT material blits and reads back on a float target");
            if (vpx == null || vpx.Length != dim * dim)
                return;

            float worst = 0f;
            int worstIndex = -1;
            for (int i = 0; i < vpx.Length; i++)
            {
                float lo = Mathf.Min(vpx[i].r, Mathf.Min(vpx[i].g, vpx[i].b));
                if (lo < worst) { worst = lo; worstIndex = i; }
            }

            // Two probes on the centre column keep the negative check from being vacuous: the image
            // centre (r2 ~ 0, so the vignette leaves it alone) and a point 60% of the way out
            // (r2 ~ 0.36, so it keeps ~64% of its value). If the vignette ever stopped being
            // applied at all, "nothing is negative" would still pass and these two would not.
            float vCorner = vpx[0].r;
            float vCentre = vpx[(dim / 2) * dim + dim / 2].r;
            int edgeRow = Mathf.RoundToInt(0.8f * dim) - 1;      // v = 0.8, i.e. centred.y = 0.6
            float vEdge = vpx[edgeRow * dim + col].r;
            log.AppendLine(string.Format(
                "  note  MobileCRT render (ARGBHalf): grey 128 in, curvature {0} / vignette 1 / scanlines 0 / mask 0 -> lowest channel {1:0.0000}{2}, corner(0,0)={3:0.0000}, centre={4:0.0000}, 60% out (row {5})={6:0.0000}",
                curvature, worst,
                worstIndex < 0 ? "" : string.Format(" at ({0},{1})", worstIndex % dim, worstIndex / dim),
                vCorner, vCentre, edgeRow, vEdge));

            // 1. THE check. saturate() is the whole of F1's fix, and this is the only assertion in
            //    the project that can see it fail: drop the saturate and this band reads ~-0.057.
            Check(worst >= -0.0005f,
                "MobileCRT render: no colour channel goes negative at vignette 1 (the vignette is saturated)",
                "lowest channel " + worst.ToString("0.0000"));
            // 2. Outside the curved screen is black on the float target too, and not negative
            //    either - `inside` multiplies the whole colour away before the write.
            Check(Mathf.Abs(vCorner) <= 0.002f,
                "MobileCRT render: the corner pixel is black on the float target",
                "got " + vCorner.ToString("0.0000"));
            // 3. The vignette is running: the centre keeps the source's 0.502 and the 60% probe
            //    keeps about 0.32 of it.
            Check(vCentre > 0.45f && vCentre < 0.56f,
                "MobileCRT render: vignette 1 leaves the centre of the tube at full brightness",
                "got " + vCentre.ToString("0.0000"));
            Check(vEdge > 0.15f && vEdge < vCentre * 0.85f,
                "MobileCRT render: vignette 1 dims the picture towards the edge",
                "60% out " + vEdge.ToString("0.0000") + " vs centre " + vCentre.ToString("0.0000"));
        }

        /// <summary>
        /// The shader's scanline phase for one destination pixel, in fractions of a scanline period:
        /// 0 is the seam between two source rows, 0.5 is a row's centre. Mirrors MobileCRT.shader's
        /// barrel warp exactly (uv = centred * (1 + k * r2) * 0.5 + 0.5, raster = uv.y * count).
        /// </summary>
        static float ScanlinePhase(int row, int col, int dim, float curvature, int count)
        {
            float u = (col + 0.5f) / dim;
            float v = (row + 0.5f) / dim;
            float cx = u * 2f - 1f;
            float cy = v * 2f - 1f;
            float r2 = cx * cx + cy * cy;
            float uvY = cy * (1f + curvature * r2) * 0.5f + 0.5f;
            float raster = uvY * count;
            return raster - Mathf.Floor(raster);
        }

        // F4 of the Task 1 review: one clamp checked END TO END through the real load path. The
        // nine checks in TestMobileCRT feed SettingsManager's own GetInt/GetFloat(min, max) - i.e.
        // they exercise Mathf.Clamp, and they passed before the clamps were added to LoadSettings.
        // This one writes a rogue value into the editor's own settings.ini, constructs a
        // SettingsManager (whose constructor is LoadSettings), reads the public property, and puts
        // the file back byte for byte - and then checks that it did.
        static void TestMobileCRTSettingsEndToEnd()
        {
            SettingsManager live = null;
            try { live = DaggerfallUnity.Settings; }
            catch (Exception ex) { log.AppendLine("  note  MobileCRT ini: live SettingsManager threw: " + ex.Message); }
            if (live == null)
                return;

            const BindingFlags priv = BindingFlags.Instance | BindingFlags.NonPublic;
            var settingsName = typeof(SettingsManager).GetMethod("SettingsName", priv);
            Check(settingsName != null, "MobileCRT ini: SettingsManager.SettingsName is reachable");
            if (settingsName == null)
                return;

            string dir = live.PersistentDataPath;
            string iniPath = Path.Combine(dir, (string)settingsName.Invoke(live, new object[] { false }));
            string bakPath = Path.Combine(dir, (string)settingsName.Invoke(live, new object[] { true }));
            Check(File.Exists(iniPath), "MobileCRT ini: the editor's settings.ini exists to test against", iniPath);
            if (!File.Exists(iniPath))
                return;

            string originalIni = File.ReadAllText(iniPath);
            string originalBak = File.Exists(bakPath) ? File.ReadAllText(bakPath) : null;
            try
            {
                // Rogue values a hand-edited ini can carry. PalettizationLUTShift=0 is the one that
                // matters: RetroRenderer would build a (256)^3 RGBA32 Texture3D - 64 MB and ~7 s on
                // the main thread by the engine's own comment table.
                string rogue = SetIniValue(originalIni, "PalettizationLUTShift", "0");
                rogue = SetIniValue(rogue, "PostProcessingInRetroMode", "-1");
                rogue = SetIniValue(rogue, "CRTCurvature", "9");
                rogue = SetIniValue(rogue, "CRTVignette", "-3");
                Check(rogue.Contains("PalettizationLUTShift = 0") && rogue.Contains("PostProcessingInRetroMode = -1")
                      && rogue.Contains("CRTCurvature = 9") && rogue.Contains("CRTVignette = -3"),
                    "MobileCRT ini: all four rogue values went into the ini text (a missed key would make the clamp checks vacuous)");
                File.WriteAllText(iniPath, rogue);

                SettingsManager loaded = new SettingsManager();
                Check(loaded.PalettizationLUTShift == 1,
                    "MobileCRT ini: a settings.ini PalettizationLUTShift=0 reaches the app as 1, through LoadSettings",
                    "got " + loaded.PalettizationLUTShift);
                Check(loaded.PostProcessingInRetroMode == 0,
                    "MobileCRT ini: a settings.ini PostProcessingInRetroMode=-1 reaches the app as 0, through LoadSettings",
                    "got " + loaded.PostProcessingInRetroMode);
                Check(Mathf.Abs(loaded.CRTCurvature - 0.3f) < 1e-6f,
                    "MobileCRT ini: a settings.ini CRTCurvature=9 reaches the app as 0.3, through LoadSettings",
                    "got " + loaded.CRTCurvature);
                Check(loaded.CRTVignette == 0f,
                    "MobileCRT ini: a settings.ini CRTVignette=-3 reaches the app as 0, through LoadSettings",
                    "got " + loaded.CRTVignette);
            }
            finally
            {
                // The load path rewrites both files (a .bak of what it read, then SyncIniData's
                // write-back), so both are restored, and the restore is itself a check.
                File.WriteAllText(iniPath, originalIni);
                if (originalBak != null)
                    File.WriteAllText(bakPath, originalBak);
                else if (File.Exists(bakPath))
                    File.Delete(bakPath);
            }

            Check(File.ReadAllText(iniPath) == originalIni,
                "MobileCRT ini: the editor's settings.ini is left exactly as it was found");
            Check(originalBak == null ? !File.Exists(bakPath) : File.ReadAllText(bakPath) == originalBak,
                "MobileCRT ini: the settings backup is left exactly as it was found");
        }

        /// <summary>Replaces one `key = value` line in an ini's text, leaving everything else alone.</summary>
        static string SetIniValue(string ini, string key, string value)
        {
            string[] lines = ini.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string trimmed = lines[i].TrimStart();
                if (!trimmed.StartsWith(key, StringComparison.Ordinal))
                    continue;
                string rest = trimmed.Substring(key.Length).TrimStart();
                if (rest.Length == 0 || rest[0] != '=')
                    continue;
                bool cr = lines[i].EndsWith("\r", StringComparison.Ordinal);
                lines[i] = key + " = " + value + (cr ? "\r" : "");
                return string.Join("\n", lines);
            }
            return ini;
        }

        // The CRT filter's two front ends. There is no way to click either of them from a headless
        // editor, so this is the shape of them: the page type exists, implements DFU's config-page
        // interface, is registered with GameEffectsConfigWindow next to Retro Mode, and carries the
        // four sliders at the clamp ranges the settings loader enforces; and MobileSettingsPanel
        // carries the three thumb-reachable rows, writing DaggerfallUnity.Settings rather than the
        // panel's own PlayerPrefs (DFU owns these values and persists them in settings.ini).
        static void TestMobileCRTUI()
        {
            Type pageType = typeof(DaggerfallWorkshop.Game.UserInterfaceWindows.GameEffectsConfigWindow)
                .Assembly.GetType("DaggerfallWorkshop.Game.UserInterfaceWindows.CRTConfigPage");
            Check(pageType != null, "MobileCRT UI: CRTConfigPage exists in the UserInterfaceWindows namespace");
            if (pageType != null)
            {
                Check(typeof(DaggerfallWorkshop.Game.UserInterfaceWindows.IGameEffectConfigPage).IsAssignableFrom(pageType),
                    "MobileCRT UI: CRTConfigPage implements IGameEffectConfigPage");
                Check(pageType.BaseType == typeof(DaggerfallWorkshop.Game.UserInterfaceWindows.GameEffectConfigPage),
                    "MobileCRT UI: CRTConfigPage derives from GameEffectConfigPage (it gets the tip/slider helpers)",
                    pageType.BaseType == null ? "no base type" : pageType.BaseType.Name);
                // Key is what the window files the page under and what the effect list selects on,
                // so a duplicate or a missing one is a dictionary exception at Setup time.
                object instance = null;
                try { instance = Activator.CreateInstance(pageType); }
                catch (Exception ex) { log.AppendLine("  note  MobileCRT UI: CRTConfigPage would not construct: " + ex.Message); }
                var keyProp = pageType.GetProperty("Key");
                string key = (instance != null && keyProp != null) ? keyProp.GetValue(instance, null) as string : null;
                Check(key == "crtFilter", "MobileCRT UI: CRTConfigPage.Key is crtFilter", key ?? "null");
            }

            const string windowPath = "Assets/Scripts/Game/UserInterfaceWindows/GameEffectsConfigWindow.cs";
            Check(File.Exists(windowPath), "MobileCRT UI: " + windowPath + " exists");
            if (File.Exists(windowPath))
            {
                string window = StripShaderComments(File.ReadAllText(windowPath));
                Check(window.Contains("AddConfigPage(new CRTConfigPage());"),
                    "MobileCRT UI: GameEffectsConfigWindow registers the CRT page");
                int retro = window.IndexOf("AddConfigPage(new RetroModeConfigPage());", StringComparison.Ordinal);
                int crt = window.IndexOf("AddConfigPage(new CRTConfigPage());", StringComparison.Ordinal);
                Check(retro >= 0 && crt == window.IndexOf("AddConfigPage(new CRTConfigPage());", retro, StringComparison.Ordinal)
                      && crt > retro && crt - retro < 120,
                    "MobileCRT UI: the CRT page sits next to Retro Mode, where a player will look for it",
                    "retro at " + retro + ", crt at " + crt);
            }

            const string pagePath = "Assets/Scripts/Game/UserInterfaceWindows/CRTConfigPage.cs";
            Check(File.Exists(pagePath), "MobileCRT UI: " + pagePath + " exists");
            if (File.Exists(pagePath))
            {
                string raw = File.ReadAllText(pagePath);
                Check(raw.Contains("License:         MIT License"), "MobileCRT UI: the page carries the port's MIT header");
                string page = StripShaderComments(raw);
                // One toggle and four sliders, and the sliders' ranges must be the loader's clamps -
                // a slider that can write 5.0 into CRTMask makes the clamp the only thing standing
                // between the ini and a black screen.
                foreach (string setting in new[] { "CRTFilter", "CRTCurvature", "CRTScanlines", "CRTMask", "CRTVignette" })
                    Check(page.Contains("DaggerfallUnity.Settings." + setting),
                        "MobileCRT UI: the page drives DaggerfallUnity.Settings." + setting);
                Check(page.Contains("SetIndicator(0f, MobileCrt.MaxCurvature,"),
                    "MobileCRT UI: the curvature slider's range is MobileCrt.MaxCurvature (0.3), the loader's clamp");
                Check(CountOccurrences(page, "SetIndicator(0f, 1f,") == 3,
                    "MobileCRT UI: the three amplitude sliders run 0..1, the loader's clamp",
                    CountOccurrences(page, "SetIndicator(0f, 1f,") + " of 3");
                // Two things a player cannot discover by looking, so the tip has to say them.
                Check(page.Contains("HUD stays sharp"), "MobileCRT UI: the tip text says the HUD stays sharp on purpose");
                Check(page.Contains("crops the edges"), "MobileCRT UI: the tip text says curvature crops the edges");
                Check(page.Contains("Needs retro mode"), "MobileCRT UI: the tip text says retro mode is required");
            }

            const string panelPath = "Assets/Scripts/Game/Mobile/MobileSettingsPanel.cs";
            string panel = StripShaderComments(File.ReadAllText(panelPath));
            Check(panel.Contains("\"Retro mode\""), "MobileCRT UI: the settings panel has a Retro mode row");
            Check(panel.Contains("\"Aspect\""), "MobileCRT UI: the settings panel has an Aspect row");
            Check(panel.Contains("\"CRT filter\""), "MobileCRT UI: the settings panel has a CRT filter row");
            foreach (string setting in new[] { "RetroRenderingMode", "RetroModeAspectCorrection", "CRTFilter" })
                Check(panel.Contains("DaggerfallUnity.Settings." + setting),
                    "MobileCRT UI: the settings panel row writes DaggerfallUnity.Settings." + setting);
            // Retro mode owns render targets and the aspect viewport, so a change has to be deployed
            // (RetroModeConfigPage does the same) - and DFU persists these in settings.ini, not in
            // the panel's PlayerPrefs.
            Check(panel.Contains("DeployCoreGameEffectSettings(CoreGameEffectSettingsGroups.RetroMode)"),
                "MobileCRT UI: changing retro mode from the panel deploys the retro-mode effect group");
            Check(panel.Contains("DaggerfallUnity.Settings.SaveSettings()"),
                "MobileCRT UI: the panel's DFU rows persist through SaveSettings, not PlayerPrefs");
            // F13 of the Task 2 review. AddToggle's caller-owns-persistence case (key == null)
            // caches the row's state in a closure local, and the panel is built once per session -
            // so without a refreshDynamic hook the CRT filter row shows a stale ON/OFF after the
            // value is changed in the Game Effects window, and the next tap flips the stale cache
            // into a write the row's own equality guard then swallows. Source text, because the
            // row is UGUI built at runtime and there is no seam to call from the editor.
            Check(panel.Contains("if (key == null)")
                  && panel.Contains("refreshDynamic += () => { current = get(); paint(); };"),
                "MobileCRT UI: a caller-owned AddToggle row re-reads its value when the panel reopens");
        }

        // Distant Terrain's far-terrain shader is compiled into the app, and its whole reason for
        // being rewritten is memory: upstream sampled twelve 2048^2 tileset ATLASES that
        // DistantTerrain.cs built at runtime (~270 MB resident). The rewrite samples three
        // Texture2DArrays instead (summer / winter / rain, 224 x 64^2 slices each, ~15 MB total),
        // which also takes the fragment stage from twelve samplers to three. These checks are the
        // guard on that: the shader must exist and compile, the atlas machinery must be gone, the
        // array uniforms and the slice contract Task 4's C# binds against must be present, and the
        // visual logic the port promised to keep (skirt, cutout discard, alpha:fade, depth pass)
        // must still be there. Source text, not reflection, because a shader's uniforms are not
        // enumerable from managed code.
        static void TestDistantTerrainShader()
        {
            const string shaderName = "Daggerfall/DistantTerrain/DistantTerrainTilemap";
            Shader far = Shader.Find(shaderName);
            Check(far != null, "DistantTerrain: far-terrain shader is in the project");
            if (far == null) return;
            Check(far.isSupported, "DistantTerrain: far-terrain shader compiles for this editor's graphics API");

            // Code only: the rewrite's comments name the upstream atlas symbols on purpose (they
            // explain what each replacement replaced), and "it is gone" checks that a comment can
            // satisfy are worthless.
            string cginc = StripShaderComments(File.ReadAllText("Assets/Shaders/DistantTerrain/FarTerrainCommon.cginc"));
            string src = StripShaderComments(File.ReadAllText("Assets/Shaders/DistantTerrain/DistantTerrainTilemap.shader"));

            // The texture-array source.
            Check(CountOccurrences(cginc, "UNITY_DECLARE_TEX2DARRAY(") == 3,
                "DistantTerrain: exactly three tile texture arrays are declared",
                CountOccurrences(cginc, "UNITY_DECLARE_TEX2DARRAY(") + " declarations");
            foreach (string uniform in new[] { "_TileArraySummer", "_TileArrayWinter", "_TileArrayRain" })
            {
                Check(cginc.Contains("UNITY_DECLARE_TEX2DARRAY(" + uniform + ")") && src.Contains(uniform),
                    "DistantTerrain: " + uniform + " is declared and exposed as a property");
            }
            Check(cginc.Contains("int _SlicesPerBiome") && src.Contains("_SlicesPerBiome"),
                "DistantTerrain: _SlicesPerBiome (56) is the slice-block stride the C# binds");
            Check(cginc.Contains("biome * _SlicesPerBiome + record"),
                "DistantTerrain: slice index is biome * _SlicesPerBiome + record (matches DistantTerrain.SliceIndex)");
            // The mip-count contract. The tile sample selects its level EXPLICITLY
            // (UNITY_SAMPLE_TEX2DARRAY_LOD - array GRAD sampling has a seam bug), and an explicit lod
            // past an array's last level is undefined in HLSL, not clamped. So the shader must own a
            // ceiling and the C# must push the packed arrays' real mip count into it: without both
            // halves an array with a shorter chain than a 64^2 tileset's six levels samples black.
            Check(cginc.Contains("int _TileArrayMipCount") && src.Contains("_TileArrayMipCount"),
                "DistantTerrain: _TileArrayMipCount is declared and exposed as a property");
            Check(cginc.Contains("clamp(lod, 0.0f, (float)max(_TileArrayMipCount - 1, 0))"),
                "DistantTerrain: every tile-array sample clamps its explicit lod to _TileArrayMipCount - 1");

            // The atlas machinery is gone - this is the memory fix, so its absence is the test.
            Check(!cginc.Contains("getColorByTextureAtlasIndex"),
                "DistantTerrain: the atlas cell/gutter sampler getColorByTextureAtlasIndex is gone");
            Check(!cginc.Contains("tex2Dgrad("),
                "DistantTerrain: no tex2Dgrad on an atlas sampler survives");
            Check(!cginc.Contains("sampler2D _TileAtlasTex") && !src.Contains("_TileAtlasTex"),
                "DistantTerrain: none of the twelve 2048^2 atlas samplers survive");
            // Both files: the uniforms are declared as shader PROPERTIES in the .shader and consumed
            // in the .cginc, so checking only the include would pass a re-added
            // `_AtlasSize("...", Float) = 2048.0` property block - the neighbouring _TileAtlasTex
            // check already tests both for the same reason.
            Check(!cginc.Contains("_AtlasSize") && !cginc.Contains("_GutterSize")
                  && !src.Contains("_AtlasSize") && !src.Contains("_GutterSize"),
                "DistantTerrain: atlas size / gutter uniforms are gone with the atlases");

            // Dead code the port drops.
            Check(!cginc.Contains("_CameraDepthTexture"),
                "DistantTerrain: the dead _CameraDepthTexture read in fcolor is deleted");
            Check(!cginc.Contains("sampler2D _TilemapTex") && !cginc.Contains("sampler2D _BumpMap"),
                "DistantTerrain: the legacy _TilemapTex / _BumpMap samplers are deleted");
            Check(!src.Contains("#pragma glsl"),
                "DistantTerrain: the dead #pragma glsl is deleted");
            Check(CountOccurrences(src, "#pragma target 3.5") == 2,
                "DistantTerrain: both surface programs target 3.5 (texture arrays)",
                CountOccurrences(src, "#pragma target 3.5") + " occurrences");

            // Visual logic the rewrite promised to keep untouched.
            Check(CountOccurrences(src, "alpha:fade") == 2, "DistantTerrain: alpha:fade kept on both surface programs");
            Check(src.Contains("#pragma vertex vertDepth") && src.Contains("ColorMask 0"), "DistantTerrain: the depth-only pass is kept");
            Check(src.Contains("farTerrainCulled") && src.Contains("discard"), "DistantTerrain: the near-terrain cutout discard is kept");
            Check(cginc.Contains("applyFarTerrainSkirt") && cginc.Contains("_SkirtDepth"), "DistantTerrain: the boundary skirt is kept");
            Check(cginc.Contains("applySnowCaps") && cginc.Contains("applyWoodlandDirt") && cginc.Contains("treeSpeckMask") && cginc.Contains("_HighlightLocations"),
                "DistantTerrain: snow caps, woodland dirt, tree specks and location beacons are kept");
            Check(CountOccurrences(src, "multi_compile_local __ ENABLE_WATER_REFLECTIONS") == 2,
                "DistantTerrain: the one mod keyword is unchanged");

            // Pinned so the iOS build cannot strip it: Always-Included list, the preloaded variant
            // collection, and the resulting GraphicsSettings entry (all three, because each alone
            // is silently survivable until the shader turns up pink on device).
            string guid = AssetDatabase.AssetPathToGUID("Assets/Shaders/DistantTerrain/DistantTerrainTilemap.shader");
            Check(File.ReadAllText("Assets/Editor/MobileBuildSetup.cs").Contains(shaderName),
                "DistantTerrain: shader is in MobileBuildSetup's EnsureAlwaysIncludedShaders list");
            Check(File.ReadAllText("Assets/Shaders/RequiredShaderVariants.shadervariants").Contains(guid),
                "DistantTerrain: shader has an entry in RequiredShaderVariants (variants survive stripping)");
            // ...and the entry names variants this shader actually has: the ShaderVariant constructor
            // throws when the pass type or keyword set does not exist, which is precisely the mistake
            // a hand-written .shadervariants entry makes, and it would only show up as a warning in a
            // player log much later.
            var collection = AssetDatabase.LoadAssetAtPath<ShaderVariantCollection>("Assets/Shaders/RequiredShaderVariants.shadervariants");
            Check(collection != null, "DistantTerrain: the required-variants collection loads");
            if (collection != null)
            {
                foreach (string[] keywords in new[] { new string[0], new[] { "ENABLE_WATER_REFLECTIONS" } })
                {
                    string label = keywords.Length == 0 ? "<no keywords>" : keywords[0];
                    try
                    {
                        Check(collection.Contains(new ShaderVariantCollection.ShaderVariant(far, UnityEngine.Rendering.PassType.ForwardBase, keywords)),
                            "DistantTerrain: ForwardBase variant " + label + " is in the required-variants collection");
                    }
                    catch (ArgumentException ex)
                    {
                        Check(false, "DistantTerrain: ForwardBase variant " + label + " is in the required-variants collection", ex.Message);
                    }
                }
            }
            Check(File.ReadAllText("ProjectSettings/GraphicsSettings.asset").Contains(guid),
                "DistantTerrain: ApplyIOSSettings pinned the shader into GraphicsSettings' always-included list");
        }


        // The Distant Terrain port itself: the C# that binds Task 3's shader contract, gates its own
        // start, and tears the far terrain down when world entry fails. All of it is compiled into
        // the app (Assets/Scripts/Game/Mobile/Ports/DistantTerrain), started by MobilePortedMods
        // rather than by an [Invoke] loader, and inert until then. The checks are of two kinds: pure
        // functions called for real (the slice contract the shader reads, the availability gate, the
        // tileset compatibility rule, the memory arithmetic, the timing cadence), and source text for
        // the things that only exist at world entry on a device - the log literals a Player.log
        // reader greps for, the teardown, and the TerrainData trim.
        // The fallback the port converts tile slices through when the driver refuses
        // Graphics.ConvertTexture on array elements - which Metal does, for a genuine ARGB32 ->
        // RGBA32 conversion, on the iOS simulator (diag-report §4). Everything else about the pack
        // is checkable without a GPU; this is not, because the failure modes that matter are all
        // things only a driver can get wrong: the wrong source slice, a vertically flipped blit, a
        // channel order carried across instead of converted, a mip chain that never got generated.
        // So this drives the real helpers on the editor's real GPU (Metal here, the same family as
        // the device) and reads the result back. Runs only with graphics - the whole suite already
        // requires that (TestModExtractorRoundTrip decodes a DXT1 fixture by blit).
        static void TestDistantTerrainSliceBlit()
        {
            const int dim = 8;
            // Two slices, told apart by BLUE, with a red ramp across x and a green ramp up y so a
            // flip or a transpose cannot pass. Values stay in 64..232: the sampler linearises and
            // the render target re-encodes under a linear colour space, and the dark end of that
            // round trip is where 8-bit quantisation bites.
            Texture2DArray src = new Texture2DArray(dim, dim, 2, TextureFormat.ARGB32, true, false);
            for (int slice = 0; slice < 2; slice++)
            {
                Color32[] px = new Color32[dim * dim];
                for (int y = 0; y < dim; y++)
                    for (int x = 0; x < dim; x++)
                        px[y * dim + x] = new Color32((byte)(64 + 24 * x), (byte)(64 + 24 * y), (byte)(slice == 0 ? 64 : 224), 255);
                src.SetPixels32(px, slice, 0);
            }
            src.Apply(true);

            Texture2DArray dst = new Texture2DArray(dim, dim, 2, TextureFormat.RGBA32, true, false);
            RenderTexture scratch = global::DistantTerrain.DistantTerrain.CreateSliceScratch(dst);
            Check(scratch != null && scratch.width == dim && scratch.mipmapCount == dst.mipmapCount,
                "DistantTerrain: the scratch surface is one slice of the destination array, mip chain included",
                scratch == null ? "CreateSliceScratch returned null" : scratch.width + "x" + scratch.height + ", " + scratch.mipmapCount + " mips");
            if (scratch == null)
            {
                UnityEngine.Object.DestroyImmediate(src);
                UnityEngine.Object.DestroyImmediate(dst);
                return;
            }

            // Source slice 1 into destination slice 0: if either index were ignored the blue channel
            // (or the slice read back) gives it away.
            global::DistantTerrain.DistantTerrain.BlitSlice(src, 1, dst, 0, scratch);
            global::DistantTerrain.DistantTerrain.DestroySliceScratch(scratch);

            Color32[] mip0 = ReadArraySlice(dst, 0, 0, dim);
            Check(mip0 != null, "DistantTerrain: the blitted slice can be read back off the GPU");
            if (mip0 != null)
            {
                Func<int, int, Color32> at = (x, y) => mip0[y * dim + x];
                Check(NearColor(at(0, 0), 64, 64, 224) && NearColor(at(dim - 1, 0), 232, 64, 224)
                      && NearColor(at(0, dim - 1), 64, 232, 224),
                    "DistantTerrain: the fallback blit converts ARGB32 -> RGBA32 with the right slice, orientation and channel order",
                    at(0, 0) + " / " + at(dim - 1, 0) + " / " + at(0, dim - 1));
            }
            // ...and the mip chain the blit only wrote level 0 of. An array bound to the far-terrain
            // shader is sampled at an EXPLICIT lod of up to five, so a chain of black levels is a
            // black horizon, not a soft one.
            Color32[] mip1 = ReadArraySlice(dst, 0, 1, dim / 2);
            Check(mip1 != null && NearColor(mip1[0], 76, 76, 224),
                "DistantTerrain: GenerateMips filled the scratch surface's chain and it was copied with the slice",
                mip1 == null ? "mip 1 unreadable" : mip1[0].ToString());

            UnityEngine.Object.DestroyImmediate(src);
            UnityEngine.Object.DestroyImmediate(dst);

            // ...and the same path DOWNSAMPLING, which is what the 2026-09-10 fix asks of it: the
            // destination is always TileSliceSize, so a replacement pack's 1024^2 or 256^2 slices
            // reach it through this blit rather than being refused. Only a driver can get this
            // wrong - the scale, the slice indices under a scale, and whether the mip chain of the
            // RESAMPLED level 0 is generated at all - so it is driven for real, 4x down.
            const int bigDim = dim * 4;
            Texture2DArray big = new Texture2DArray(bigDim, bigDim, 2, TextureFormat.ARGB32, true, false);
            for (int slice = 0; slice < 2; slice++)
            {
                Color32[] px = new Color32[bigDim * bigDim];
                for (int y = 0; y < bigDim; y++)
                    for (int x = 0; x < bigDim; x++)
                        // Left half / right half, told apart in RED and BLUE so a flip or a transpose
                        // survives the scale, and flat within each half so the answer does not depend
                        // on which mip level the sampler picks for a 4x reduction.
                        px[y * bigDim + x] = slice == 0
                            ? new Color32(96, 96, 96, 255)
                            : (x < bigDim / 2 ? new Color32(96, 160, 224, 255) : new Color32(224, 160, 96, 255));
                big.SetPixels32(px, slice, 0);
            }
            big.Apply(true);

            Texture2DArray packed = new Texture2DArray(dim, dim, 2, TextureFormat.RGBA32, true, false);
            // Slice 0 gets a sentinel so "the blit wrote only the slice it was given" is a check
            // against a known value rather than against whatever an uninitialised slice holds.
            Color32[] sentinel = new Color32[dim * dim];
            for (int i = 0; i < sentinel.Length; i++) sentinel[i] = new Color32(128, 128, 128, 255);
            packed.SetPixels32(sentinel, 0, 0);
            packed.Apply(true);

            RenderTexture downScratch = global::DistantTerrain.DistantTerrain.CreateSliceScratch(packed);
            Check(downScratch != null && downScratch.width == dim && downScratch.height == dim,
                "DistantTerrain: the scratch surface is the DESTINATION's size, not the source's - which is what makes the blit a resample",
                downScratch == null ? "CreateSliceScratch returned null" : downScratch.width + "x" + downScratch.height);
            if (downScratch != null)
            {
                // Source slice 1 (the two-tone one) into destination slice 1, 4x down.
                global::DistantTerrain.DistantTerrain.BlitSlice(big, 1, packed, 1, downScratch);
                global::DistantTerrain.DistantTerrain.DestroySliceScratch(downScratch);

                Color32[] down = ReadArraySlice(packed, 1, 0, dim);
                Check(down != null, "DistantTerrain: the resampled slice can be read back off the GPU");
                if (down != null)
                {
                    Check(NearColor(down[0], 96, 160, 224)
                          && NearColor(down[dim - 1], 224, 160, 96)
                          && NearColor(down[(dim - 1) * dim], 96, 160, 224),
                        "DistantTerrain: a 32x32 source slice resamples into an 8x8 destination slice with the right slice, orientation and channel order",
                        down[0] + " / " + down[dim - 1] + " / " + down[(dim - 1) * dim]);
                }
                // The chain the shader samples at an explicit lod. A downsampled slice cannot carry
                // the source's own mips across - they are a chain for the wrong dimension - so
                // GenerateMips on the resampled level 0 is the only thing that fills it.
                Color32[] downMip = ReadArraySlice(packed, 1, 1, dim / 2);
                Check(downMip != null && NearColor(downMip[0], 96, 160, 224),
                    "DistantTerrain: the resampled slice's mip chain is generated from the resampled level 0, not carried across",
                    downMip == null ? "mip 1 unreadable" : downMip[0].ToString());
                // ...and the sentinel slice is untouched: a resample that spilled across elements
                // would be a garbage horizon, and it is the kind of thing only a driver decides.
                Color32[] untouched = ReadArraySlice(packed, 0, 0, dim);
                Check(untouched != null && NearColor(untouched[0], 128, 128, 128),
                    "DistantTerrain: the resample writes only the destination slice it was given",
                    untouched == null ? "slice 0 unreadable" : untouched[0].ToString());
            }

            UnityEngine.Object.DestroyImmediate(big);
            UnityEngine.Object.DestroyImmediate(packed);
        }

        /// <summary>Reads one mip level of one Texture2DArray slice back through a RenderTexture.</summary>
        static Color32[] ReadArraySlice(Texture2DArray array, int slice, int mip, int dim)
        {
            RenderTexture rt = new RenderTexture(dim, dim, 0, RenderTextureFormat.ARGB32,
                array.graphicsFormat == UnityEngine.Experimental.Rendering.GraphicsFormat.R8G8B8A8_SRGB
                    ? RenderTextureReadWrite.sRGB : RenderTextureReadWrite.Linear);
            if (!rt.Create()) { UnityEngine.Object.DestroyImmediate(rt); return null; }
            Graphics.CopyTexture(array, slice, mip, rt, 0, 0);
            RenderTexture wasActive = RenderTexture.active;
            RenderTexture.active = rt;
            Texture2D readback = new Texture2D(dim, dim, TextureFormat.RGBA32, false, false);
            readback.ReadPixels(new Rect(0, 0, dim, dim), 0, 0);
            readback.Apply(false);
            RenderTexture.active = wasActive;
            Color32[] px = readback.GetPixels32();
            UnityEngine.Object.DestroyImmediate(readback);
            rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);
            return px;
        }

        /// <summary>8-bit round trips through a sampler and a render target are near, not exact.</summary>
        static bool NearColor(Color32 c, int r, int g, int b)
        {
            return Math.Abs(c.r - r) <= 10 && Math.Abs(c.g - g) <= 10 && Math.Abs(c.b - b) <= 10;
        }

        static void TestDistantTerrainPort()
        {
            // The slice contract: slice = biome * 56 + record, biome 0 desert / 1 mountain /
            // 2 woodland / 3 swamp. The shader computes the same expression from _SlicesPerBiome.
            Check(global::DistantTerrain.DistantTerrain.SlicesPerBiome == 56, "DistantTerrain: 56 records per biome tileset, the shader's _SlicesPerBiome");
            Check(global::DistantTerrain.DistantTerrain.SliceIndex(0, 0) == 0
                  && global::DistantTerrain.DistantTerrain.SliceIndex(1, 0) == 56
                  && global::DistantTerrain.DistantTerrain.SliceIndex(2, 3) == 115
                  && global::DistantTerrain.DistantTerrain.SliceIndex(3, 55) == 223,
                "DistantTerrain: SliceIndex is biome * 56 + record (3,55 -> 223)");
            var slices = new HashSet<int>();
            for (int biome = 0; biome < 4; biome++)
                for (int record = 0; record < 56; record++)
                    slices.Add(global::DistantTerrain.DistantTerrain.SliceIndex(biome, record));
            Check(slices.Count == 224 && slices.Min() == 0 && slices.Max() == 223,
                "DistantTerrain: the four biome tilesets fill 224 slices with no gap and no overlap",
                slices.Count + " distinct");

            // The availability gate. Nothing is created unless the shader compiled AND the bundle
            // carries the three mountain tables AND the river/coast map.
            bool gateOk = true;
            for (int mask = 0; mask < 8; mask++)
            {
                bool shader = (mask & 1) != 0, csvs = (mask & 2) != 0, deriv = (mask & 4) != 0;
                if (global::DistantTerrain.DistantTerrain.Available(shader, csvs, deriv) != (shader && csvs && deriv))
                    gateOk = false;
            }
            Check(gateOk, "DistantTerrain: Available needs the shader, the three CSVs and the deriv map (all eight cases)");

            // Inert until MobilePortedMods calls Init: nothing in this editor session has, so both
            // flags must still be false, and the [Invoke] attribute must not have survived the port
            // (it would make DFU's own loader start the mod behind the launcher switch's back).
            Check(!global::DistantTerrain.DistantTerrainPort.Installed && !global::DistantTerrain.DistantTerrainPort.Running,
                "DistantTerrain: Installed and Running are false until Init runs");
            MethodInfo init = typeof(global::DistantTerrain.DistantTerrainPort).GetMethod("Init");
            Check(init != null && init.IsStatic && init.GetParameters().Length == 1
                  && init.GetParameters()[0].ParameterType == typeof(InitParams),
                "DistantTerrain: Init(InitParams) is the entry point MobilePortedMods calls");
            Check(init != null && Attribute.GetCustomAttributes(init, typeof(Invoke), false).Length == 0,
                "DistantTerrain: no [Invoke] survives");

            // The iOS reach preset, and the derived fade band. The shader computes
            // fadeRange = _BlendEnd - _BlendStart + 1, so a start above the end inverts the band and
            // fades the whole far terrain to nothing - blendStart is derived, never configured.
            Check(global::DistantTerrain.DistantTerrainPort.DefaultBlendEnd == 60000f,
                "DistantTerrain: blendEnd defaults to 60000 (half of upstream's reach)");
            Check(global::DistantTerrain.DistantTerrainPort.DefaultMainCameraFarClipPlane == 15000f,
                "DistantTerrain: mainCameraFarClipPlane defaults to 15000");
            Check(global::DistantTerrain.DistantTerrainRenderConfig.BlendEnd == 60000f
                  && global::DistantTerrain.DistantTerrainRenderConfig.MainCameraFarClipPlane == 15000f,
                "DistantTerrain: the settings holder starts at those defaults, so a bundle with no Rendering section keeps them");
            Check(global::DistantTerrain.DistantTerrainPort.BlendStartFor(60000f) == 50000f
                  && global::DistantTerrain.DistantTerrainPort.BlendStartFor(120000f) == 100000f,
                "DistantTerrain: blendStart is derived from blendEnd at upstream's 100000/120000 proportion");
            Check(global::DistantTerrain.DistantTerrainPort.BlendStartFor(global::DistantTerrain.DistantTerrainPort.DefaultBlendEnd)
                  < global::DistantTerrain.DistantTerrainPort.DefaultBlendEnd,
                "DistantTerrain: the fade band is never inverted (blendStart < blendEnd)");

            // The destination shape, which is now a CONSTANT of the port rather than something the
            // source tilesets decide. Ikram's DREAM install (device log 2026-09-10) had per-archive
            // slice sizes - 1024x1024 beside 256x256 beside vanilla 64x64 - and the old same-size
            // precondition refused the season and with it the whole far terrain, silently, for every
            // session. Every packed slice is TileSliceSize with the full mip chain of that size.
            Check(global::DistantTerrain.DistantTerrain.TileSliceSize == 64
                  && global::DistantTerrain.DistantTerrain.TileSliceSize == global::DistantTerrain.DistantTerrain.VanillaSliceDim,
                "DistantTerrain: the packed slices are always 64x64 - vanilla resolution, which is all the horizon can show",
                global::DistantTerrain.DistantTerrain.TileSliceSize.ToString());
            Check(global::DistantTerrain.DistantTerrain.MipChainLength(64) == 7
                  && global::DistantTerrain.DistantTerrain.MipChainLength(1) == 1
                  && global::DistantTerrain.DistantTerrain.MipChainLength(2) == 2
                  && global::DistantTerrain.DistantTerrain.MipChainLength(256) == 9,
                "DistantTerrain: MipChainLength counts down to 1x1 (64 -> 7)");
            Check(global::DistantTerrain.DistantTerrain.TileSliceMipCount
                      == global::DistantTerrain.DistantTerrain.MipChainLength(global::DistantTerrain.DistantTerrain.TileSliceSize)
                  && global::DistantTerrain.DistantTerrain.TileSliceMipCount == 7,
                "DistantTerrain: _TileArrayMipCount is the 64-slice chain, 7 - the constant and the derivation agree",
                global::DistantTerrain.DistantTerrain.TileSliceMipCount.ToString());
            Check(global::DistantTerrain.DistantTerrain.SliceNeedsResample(1024, 1024)
                  && global::DistantTerrain.DistantTerrain.SliceNeedsResample(256, 256)
                  && global::DistantTerrain.DistantTerrain.SliceNeedsResample(64, 32)
                  && !global::DistantTerrain.DistantTerrain.SliceNeedsResample(64, 64),
                "DistantTerrain: a source that is not already 64x64 is resampled, whatever direction it is out by");

            // The packing precondition - all that is left of it. Sizes and mip chains are no longer
            // compared to each other (they are resampled); the only refusal is a source that is not a
            // usable surface at all. FORMAT was never part of it, because World of Daggerfall -
            // Biomes legitimately hands back RGBA32 for one climate variant of a season and ARGB32
            // for another, and refusing that cost the entire far terrain in the Task 9 simulator run.
            string detail;
            int[] w = { 64, 64, 64, 64 }, h = { 64, 64, 64, 64 }, f = { 5, 5, 5, 5 }, m = { 7, 7, 7, 7 };
            Check(global::DistantTerrain.DistantTerrain.TilesetsPackable(w, h, m, out detail) && detail == string.Empty,
                "DistantTerrain: four vanilla 64x64 tilesets pack into one array");
            Check(global::DistantTerrain.DistantTerrain.TilesetsPackable(
                      new[] { 1024, 1024, 1024, 256 }, new[] { 1024, 1024, 1024, 256 }, new[] { 11, 11, 11, 9 }, out detail),
                "DistantTerrain: Ikram's DREAM set - 1024x1024 beside 256x256 - packs, it does not refuse", detail);
            Check(global::DistantTerrain.DistantTerrain.TilesetsPackable(
                      new[] { 64, 1024, 64, 64 }, new[] { 64, 1024, 64, 64 }, new[] { 7, 11, 7, 7 }, out detail),
                "DistantTerrain: the winter case (one replaced archive beside three vanilla) packs too", detail);
            Check(global::DistantTerrain.DistantTerrain.TilesetsPackable(w, h, new[] { 7, 7, 7, 1 }, out detail),
                "DistantTerrain: a tileset that shipped without a mip chain packs (the blit builds one)", detail);
            Check(!global::DistantTerrain.DistantTerrain.TilesetsPackable(w, h, new[] { 7, 7, 7, 0 }, out detail)
                  && detail.Contains("mip"),
                "DistantTerrain: a tileset with NO mip level at all is not a surface - refused, naming it", detail);
            Check(!global::DistantTerrain.DistantTerrain.TilesetsPackable(new[] { 64, 0, 64, 64 }, h, m, out detail)
                  && detail.Contains("0"),
                "DistantTerrain: a zero-dimension tileset is refused, naming the size", detail);
            Check(!global::DistantTerrain.DistantTerrain.TilesetsPackable(null, h, m, out detail)
                  && !global::DistantTerrain.DistantTerrain.TilesetsPackable(w, h, new[] { 7, 7 }, out detail),
                "DistantTerrain: nothing to pack, or ragged parallel arrays, is not packable");
            Check(global::DistantTerrain.DistantTerrain.TilesetsSameSize(w, h, m, out detail)
                  && !global::DistantTerrain.DistantTerrain.TilesetFormatsAgree(new[] { 5, 5, 10, 5 }),
                "DistantTerrain: a mixed-format set still packs (it is converted), and is recognised as mixed", detail);
            Check(!global::DistantTerrain.DistantTerrain.TilesetsSameSize(new[] { 64, 128, 64, 64 }, h, m, out detail) && detail.Contains("128"),
                "DistantTerrain: TilesetsSameSize still answers the cross-season question, naming the size", detail);
            Check(!global::DistantTerrain.DistantTerrain.TilesetsSameSize(w, h, new[] { 7, 7, 7, 1 }, out detail) && detail.Contains("mip"),
                "DistantTerrain: ...and a differing mip chain, which is the other half of that invariant", detail);
            Check(!global::DistantTerrain.DistantTerrain.TilesetsSameSize(null, h, m, out detail),
                "DistantTerrain: nothing to compare is not compatible");
            // The strict predicate the no-copy-support refusal names in its detail: what would have
            // had to be true for plain slice copies to be enough.
            Check(global::DistantTerrain.DistantTerrain.TilesetsCompatible(w, h, f, m, out detail) && detail == string.Empty,
                "DistantTerrain: the strict CopyTexture predicate passes four identical tilesets");
            Check(!global::DistantTerrain.DistantTerrain.TilesetsCompatible(w, h, new[] { 5, 5, 10, 5 }, m, out detail) && detail.Contains("format"),
                "DistantTerrain: the strict CopyTexture predicate refuses a recompressed tileset, naming the format", detail);
            Check(global::DistantTerrain.DistantTerrain.TilesetFormatsAgree(f)
                  && !global::DistantTerrain.DistantTerrain.TilesetFormatsAgree(new[] { 5, 4 })
                  && !global::DistantTerrain.DistantTerrain.TilesetFormatsAgree(null),
                "DistantTerrain: TilesetFormatsAgree decides whether the pack copies or converts");

            // The per-slice decision, whole truth table - two rows decided by SHAPE AND FORMAT, with
            // no runtime input: same size + same mip chain + same format is a Copy, ANYTHING ELSE is
            // a Blit. Graphics.ConvertTexture takes no Texture2DArray as a source in this
            // Unity at all (the simulator console gives Unity's own reason, "Graphics.ConvertTexture
            // does not support a Texture2DArray as source"), so it is not a candidate for either row
            // and probing it could only ever print a red Unity error. Mip count is in the Copy row
            // for a hard reason: Graphics.CopyTexture of a whole element demands the two agree on how
            // many levels there are, and the destination's chain is the port's own constant now.
            const int dstDim = global::DistantTerrain.DistantTerrain.TileSliceSize;
            const int dstMips = global::DistantTerrain.DistantTerrain.TileSliceMipCount;
            Func<int, int, int, TextureFormat, global::DistantTerrain.DistantTerrain.SlicePack> packMethod =
                (sw, sh, sm, sf) => global::DistantTerrain.DistantTerrain.SlicePackMethod(
                    sw, sh, sm, sf, dstDim, dstDim, dstMips, TextureFormat.ARGB32);
            Check(packMethod(dstDim, dstDim, dstMips, TextureFormat.ARGB32)
                      == global::DistantTerrain.DistantTerrain.SlicePack.Copy
                  && global::DistantTerrain.DistantTerrain.SlicePackMethod(64, 64, 7, TextureFormat.RGBA32,
                         64, 64, 7, TextureFormat.RGBA32) == global::DistantTerrain.DistantTerrain.SlicePack.Copy,
                "DistantTerrain: a source that already IS a destination slice - same size, same mip chain, same format - is copied (vanilla: all 224)");
            Check(packMethod(dstDim, dstDim, dstMips, TextureFormat.RGBA32)
                      == global::DistantTerrain.DistantTerrain.SlicePack.Blit
                  && global::DistantTerrain.DistantTerrain.SlicePackMethod(64, 64, 7, TextureFormat.RGBA32,
                         64, 64, 7, TextureFormat.ARGB32) == global::DistantTerrain.DistantTerrain.SlicePack.Blit,
                "DistantTerrain: a differing format is blitted through a RenderTexture, in either direction and on every runtime");
            Check(packMethod(1024, 1024, 11, TextureFormat.ARGB32)
                      == global::DistantTerrain.DistantTerrain.SlicePack.Blit
                  && packMethod(256, 256, 9, TextureFormat.ARGB32)
                      == global::DistantTerrain.DistantTerrain.SlicePack.Blit
                  && packMethod(32, 32, 6, TextureFormat.ARGB32)
                      == global::DistantTerrain.DistantTerrain.SlicePack.Blit,
                "DistantTerrain: a differing SIZE is resampled by the same blit - which is the DREAM case, and used to refuse the season");
            Check(packMethod(dstDim, dstDim, 1, TextureFormat.ARGB32)
                      == global::DistantTerrain.DistantTerrain.SlicePack.Blit,
                "DistantTerrain: a same-size source with no mip chain is blitted too - CopyTexture would refuse the element, the blit builds the chain");
            // ...and Convert is not merely unreached, it is not expressible: the enum has exactly the
            // two members the two rows produce, so no later edit can revive the probe by accident.
            string[] slicePackNames = System.Enum.GetNames(typeof(global::DistantTerrain.DistantTerrain.SlicePack));
            Check(slicePackNames.Length == 2
                  && System.Array.IndexOf(slicePackNames, "Copy") >= 0
                  && System.Array.IndexOf(slicePackNames, "Blit") >= 0,
                "DistantTerrain: SlicePack is Copy or Blit only - there is no Convert outcome to fall into",
                string.Join(", ", slicePackNames));

            // The mip-count uniform's C# half: the number pushed is the packed arrays' own, floored
            // at 1 so an unpacked/mip-less array clamps the shader's explicit lod to level 0 rather
            // than sampling past the end of the chain.
            Check(global::DistantTerrain.DistantTerrain.TileArrayMipCount(7) == 7
                  && global::DistantTerrain.DistantTerrain.TileArrayMipCount(1) == 1
                  && global::DistantTerrain.DistantTerrain.TileArrayMipCount(0) == 1
                  && global::DistantTerrain.DistantTerrain.TileArrayMipCount(-3) == 1
                  && global::DistantTerrain.DistantTerrain.TileArrayMipCount((Texture2DArray)null) == 1,
                "DistantTerrain: _TileArrayMipCount is the packed arrays' mip count, never below 1");

            // The memory the port promised: three 224-slice arrays of 64^2 ARGB32 with mips, ~15 MB
            // against the ~270 MB of twelve 2048^2 atlases the rewrite replaced - and now a promise
            // rather than a hope, because the destination shape does not depend on what is installed.
            long oneArray = global::DistantTerrain.DistantTerrain.ArrayBytes(64, 64, 224, 7, 4);
            long three = 3 * oneArray;
            Check(global::DistantTerrain.DistantTerrain.ArrayBytes(64, 64, 224, 1, 4) == 224L * 64 * 64 * 4,
                "DistantTerrain: ArrayBytes without mips is width * height * slices * bytes");
            Check(three / (1024 * 1024) >= 13 && three / (1024 * 1024) <= 16,
                "DistantTerrain: the three packed arrays are ~15 MB (the line the port logs once)",
                (three / (1024 * 1024)) + " MB");
            Check(global::DistantTerrain.DistantTerrain.BytesPerPixel(TextureFormat.ARGB32) == 4
                  && global::DistantTerrain.DistantTerrain.BytesPerPixel(TextureFormat.RGB24) == 3
                  && global::DistantTerrain.DistantTerrain.BytesPerPixel(TextureFormat.R8) == 1,
                "DistantTerrain: uncompressed bytes per texel by format");
            // ...which the arithmetic can state outright: a 1024^2 pack would have been 256 times the
            // footprint under the old "adopt the sources' size" rule, and is 15 MB under this one.
            Check(global::DistantTerrain.DistantTerrain.ArrayBytes(1024, 1024, 224, 11, 4) / oneArray >= 250,
                "DistantTerrain: a 1024^2 destination would have been ~256x the memory - which is why the destination is fixed",
                (global::DistantTerrain.DistantTerrain.ArrayBytes(1024, 1024, 224, 11, 4) / (1024 * 1024)) + " MB per array");
            Check(3 * global::DistantTerrain.DistantTerrain.ArrayBytes(
                      global::DistantTerrain.DistantTerrain.TileSliceSize,
                      global::DistantTerrain.DistantTerrain.TileSliceSize,
                      4 * global::DistantTerrain.DistantTerrain.SlicesPerBiome,
                      global::DistantTerrain.DistantTerrain.TileSliceMipCount, 4) == three,
                "DistantTerrain: the ~15 MB figure is the constants' own arithmetic, not a separate assumption");

            // The render-target guard on the destination format. The format normally kept is the
            // sources' own when the four agree - vanilla ARGB32, which makes the pack plain copies -
            // but the scratch surface a resample goes through is a RenderTexture of that format, and
            // no driver renders into ASTC or DXT. The iOS DREAM conversion ships exactly that, so a
            // compressed set that also needs resampling falls back to RGBA32 rather than failing to
            // create the surface and refusing the season.
            Check(global::DistantTerrain.DistantTerrain.CanBlitInto(TextureFormat.ARGB32)
                  && global::DistantTerrain.DistantTerrain.CanBlitInto(TextureFormat.RGBA32)
                  && global::DistantTerrain.DistantTerrain.CanBlitInto(TextureFormat.RGB24),
                "DistantTerrain: an uncompressed colour format can be the scratch surface");
            Check(!global::DistantTerrain.DistantTerrain.CanBlitInto(TextureFormat.ASTC_6x6)
                  && !global::DistantTerrain.DistantTerrain.CanBlitInto(TextureFormat.DXT1)
                  && !global::DistantTerrain.DistantTerrain.CanBlitInto(TextureFormat.DXT5)
                  && !global::DistantTerrain.DistantTerrain.CanBlitInto(TextureFormat.BC7),
                "DistantTerrain: a compressed format cannot - which is what the iOS DREAM conversion produces");

            // Timing cadence: the first ten map-pixel crosses, then every twenty-fifth.
            bool cadence = true;
            for (int i = 1; i <= 10; i++) if (!global::DistantTerrain.DistantTerrain.ShouldLogMapPixelUpdate(i)) cadence = false;
            for (int i = 11; i <= 24; i++) if (global::DistantTerrain.DistantTerrain.ShouldLogMapPixelUpdate(i)) cadence = false;
            if (!global::DistantTerrain.DistantTerrain.ShouldLogMapPixelUpdate(25)) cadence = false;
            if (global::DistantTerrain.DistantTerrain.ShouldLogMapPixelUpdate(26)) cadence = false;
            if (!global::DistantTerrain.DistantTerrain.ShouldLogMapPixelUpdate(50)) cadence = false;
            Check(cadence, "DistantTerrain: map-pixel timing logs the first ten crosses then every twenty-fifth");

            // The shader this port binds to is the one Task 3 pinned, by the name the port holds.
            Check(global::DistantTerrain.DistantTerrainPort.ShaderName == "Daggerfall/DistantTerrain/DistantTerrainTilemap",
                "DistantTerrain: the port names the shader Task 3 pinned");
            Check(Shader.Find(global::DistantTerrain.DistantTerrainPort.ShaderName) != null,
                "DistantTerrain: that shader name resolves in this project");

            // The bundle assets the gate looks for are the ones the fetched mod actually carries.
            var csvNames = global::DistantTerrain.DistantTerrainPort.CsvFileNames;
            Check(csvNames.Length == 3 && csvNames.Contains("Mountains.csv") && csvNames.Contains("Mountains_Small.csv")
                  && csvNames.Contains("Mountains_Foothills.csv"),
                "DistantTerrain: the three mountain tables are the gate's CSV list");
            Check(global::DistantTerrain.DistantTerrainPort.DerivMapFileName == "daggerfall_deriv_map.png",
                "DistantTerrain: the river/coast map is named as the bundle names it");

            // Source text, for what only happens at world entry on a device.
            const string portDir = "Assets/Scripts/Game/Mobile/Ports/DistantTerrain/";
            string port = File.ReadAllText(portDir + "DistantTerrain.cs");
            string startup = File.ReadAllText(portDir + "_startupMod.cs");
            // Code only for the "it is gone" checks: this port's comments name the upstream symbols
            // they replaced on purpose (that is how a reader of the diff learns what moved where),
            // and a check a comment can fail is a check that punishes explanation.
            string portCode = StripShaderComments(port);
            string startupCode = StripShaderComments(startup);

            // The dropped files, and everything that referenced them.
            Check(!File.Exists(portDir + "DistantTerrainFlyMap.cs") && !File.Exists(portDir + "ThirteenthPassageEffect.cs"),
                "DistantTerrain: the fly-map and the spell are not in the port");
            Check(!startupCode.Contains("Wenzil.Console") && !startupCode.Contains("ConsoleCommandsDatabase")
                  && !startupCode.Contains("RegisterEffectTemplate") && !startupCode.Contains("ThirteenthPassage")
                  && !startupCode.Contains("KeyCode"),
                "DistantTerrain: the console command, the spell registration and the hotkey setting are gone with them");
            Check(!portCode.Contains("Application.Quit()"),
                "DistantTerrain: the port never quits the app over a scene reference (Init runs at the title, where there is none)");

            // The shader contract, bound from C#: three arrays and the stride, no atlas slots.
            Check(CountOccurrences(port, "SetTexture(\"_TileArray") == 3,
                "DistantTerrain: all three tile arrays are bound (summer, winter, rain - the shader picks per fragment)");
            Check(port.Contains("SetInt(\"_SlicesPerBiome\", SlicesPerBiome)"),
                "DistantTerrain: _SlicesPerBiome is pushed from the same constant SliceIndex uses");
            Check(port.Contains("SetInt(\"_TileArrayMipCount\", TileArrayMipCount(tileArraySummer))"),
                "DistantTerrain: _TileArrayMipCount is pushed from the packed array itself, not assumed");
            Check(!portCode.Contains("_TileAtlasTex") && !portCode.Contains("GetTerrainTilesetTexture"),
                "DistantTerrain: the twelve atlas binds and the calls that built them are gone");
            Check(port.Contains("int slice = SliceIndex(b, record);")
                  && port.Contains("Graphics.CopyTexture(src[b], record, dst, slice)"),
                "DistantTerrain: the pack is a GPU slice copy at the contract's index");
            Check(port.Contains("dst.wrapMode = TextureWrapMode.Repeat")
                  && port.Contains("dst.filterMode = dfUnity.MaterialReader.MainFilterMode"),
                "DistantTerrain: the packed arrays tile (Repeat) and keep the tilesets' point filtering");

            // The TerrainData trim: the far terrain paints nothing through splat/basemap/detail.
            Check(port.Contains("terrainData.SetDetailResolution(16, 8)")
                  && port.Contains("terrainData.alphamapResolution = 16")
                  && port.Contains("terrainData.baseMapResolution = 16"),
                "DistantTerrain: alphamap, basemap and detail resolutions are at their minimum");

            // Failure handling: a throw at world entry is contained and undone.
            Check(port.Contains("void TearDownFarTerrain()") && port.Contains("catch (Exception ex)")
                  && port.Contains("Camera.main.farClipPlane = savedMainCameraFarClipPlane")
                  && port.Contains("Camera.main.clearFlags = savedMainCameraClearFlags"),
                "DistantTerrain: a failed build tears down and gives the main camera back its clip plane and clear flags");
            Check(port.Contains("DistantTerrainPort.MarkInstalled();"),
                "DistantTerrain: Installed is set from the far terrain build, not from Init");

            // The teardown's full debt, checked against the port's own written-down contract rather
            // than against a list retyped here. Destroying the far-terrain GameObject destroys the
            // Terrain COMPONENT; the TerrainData, the runtime Material and the 1024^2 readable
            // tilemap texture are free-standing objects that survive it, so a teardown that only
            // nulls the fields leaks ~12-16 MB for the session - in the handler whose whole purpose
            // is to undo a half-built world. Braces-matched body, so a Destroy elsewhere in the file
            // cannot satisfy it.
            string teardownBody = MethodBody(portCode, "void TearDownFarTerrain()");
            string[] notDestroyed = global::DistantTerrain.DistantTerrain.TeardownDestroys
                .Where(o => !teardownBody.Contains("Destroy(" + o + ")")).ToArray();
            Check(teardownBody.Length > 0 && notDestroyed.Length == 0,
                "DistantTerrain: the teardown destroys every object it allocated (TerrainData, material, tilemap texture, cameras, RT)",
                teardownBody.Length == 0 ? "TearDownFarTerrain body not found" : "not destroyed: " + string.Join(", ", notDestroyed));
            string[] notNulled = global::DistantTerrain.DistantTerrain.TeardownNulls
                .Where(f => !teardownBody.Contains(f + " = null")).ToArray();
            Check(teardownBody.Length > 0 && notNulled.Length == 0,
                "DistantTerrain: the teardown nulls every field it dropped, the 4.2 MB Color32 tilemap included",
                teardownBody.Length == 0 ? "TearDownFarTerrain body not found" : "not nulled: " + string.Join(", ", notNulled));
            // OnDestroy owes the same three; upstream only dropped the references there.
            string onDestroyBody = MethodBody(portCode, "void OnDestroy()");
            Check(onDestroyBody.Contains("Destroy(terrain.terrainData)")
                  && onDestroyBody.Contains("Destroy(terrainMaterial)")
                  && onDestroyBody.Contains("Destroy(textureTerrainInfoTileMap)"),
                "DistantTerrain: OnDestroy frees the TerrainData, the material and the tilemap texture too");
            // And the refusal path inside GenerateWorldTerrain, which destroys the Terrain object it
            // just built. The TerrainData behind it is not reachable from the teardown at that point
            // (the `terrain` field is only assigned after this method returns true), so it has to go
            // here or nowhere.
            string generateBody = MethodBody(portCode, "private bool GenerateWorldTerrain()");
            Check(generateBody.Contains("Destroy(terrain.terrainData)") && generateBody.Contains("Destroy(terrainGameObject)"),
                "DistantTerrain: the tileset refusal destroys the TerrainData it created, not just the Terrain object");
            // Running/Installed are cleared where they are set - see the sky's poll, which waits on
            // Running whenever it is true and would otherwise never release after a failed build.
            Check(teardownBody.Contains("DistantTerrainPort.MarkStopped();"),
                "DistantTerrain: the teardown clears Running and Installed (MarkStopped, symmetric with MarkInstalled)");
            MethodInfo markStopped = typeof(global::DistantTerrain.DistantTerrainPort)
                .GetMethod("MarkStopped", BindingFlags.Static | BindingFlags.NonPublic);
            Check(markStopped != null && markStopped.GetParameters().Length == 0,
                "DistantTerrain: MarkStopped() exists beside MarkInstalled()");

            // The cross-season array check. Per season, PackSeason already refuses four archives
            // that cannot share one array; this is the gap that left - the shader takes ONE
            // dimension (max of _TileArraySummer_TexelSize.z/.w) and computes the mip LOD for winter
            // and rain from it, because snow caps always sample winter and snow-free climates always
            // sample summer. Fake dimensions, three "seasons", so the rule is exercised without a GPU.
            string seasonDetail;
            Check(global::DistantTerrain.DistantTerrain.TilesetsSameSize(
                      new[] { 64, 64, 64 }, new[] { 64, 64, 64 }, new[] { 7, 7, 7 }, out seasonDetail),
                "DistantTerrain: three identical packed arrays pass the cross-season check");
            Check(!global::DistantTerrain.DistantTerrain.TilesetsSameSize(
                      new[] { 64, 128, 64 }, new[] { 64, 128, 64 }, new[] { 7, 8, 7 }, out seasonDetail)
                  && seasonDetail.Contains("128"),
                "DistantTerrain: a winter array packed at 128 while summer is 64 is refused, and the detail names the size",
                seasonDetail);
            Check(!global::DistantTerrain.DistantTerrain.TilesetsSameSize(
                      new[] { 64, 64, 64 }, new[] { 64, 64, 64 }, new[] { 7, 7, 1 }, out seasonDetail)
                  && seasonDetail.Contains("mip"),
                "DistantTerrain: a rain array with no mip chain while summer has seven is refused",
                seasonDetail);
            Check(portCode.Contains("tileArraySummer.width, tileArrayWinter.width, tileArrayRain.width"),
                "DistantTerrain: BuildTileArrays runs that check across the three PACKED arrays, not only within each season");
            // ...and only across the three DIMENSIONS. The seasons are sampled from independent
            // arrays, so one packed RGBA32 beside one packed in its sources' own format is fine;
            // making format part of the cross-season rule would refuse a legitimate Biomes set twice.
            Check(portCode.Contains("BlitSlice(src[b], record, dst, slice, scratch);")
                  && portCode.Contains("SystemInfo.copyTextureSupport == UnityEngine.Rendering.CopyTextureSupport.None"),
                "DistantTerrain: PackSeason converts formats with a GPU blit and only refuses where no GPU copy exists at all");
            // The destination shape, in the allocation itself. This is the 2026-09-10 fix: the array
            // is built at the port's own constants, so no combination of installed packs can change
            // its size, its mip chain or its memory. It used to be `widths[0], heights[0], ...,
            // mipCounts[0]` - the FIRST SOURCE's shape - which is why four sources of four shapes had
            // to be refused, and why Ikram's DREAM install had no far terrain at all.
            Check(portCode.Contains("new Texture2DArray(TileSliceSize, TileSliceSize, archives.Length * SlicesPerBiome,")
                  && portCode.Contains("dstFormat, TileSliceMipCount, false);")
                  && !portCode.Contains("new Texture2DArray(widths[0], heights[0]"),
                "DistantTerrain: the packed array is allocated at TileSliceSize/TileSliceMipCount, never at a source's shape");
            Check(MethodBody(portCode, "Texture2DArray PackSeason(TextureReader reader, string season, int[] archives)")
                      .Contains("TilesetsPackable(widths, heights, mipCounts, out detail)")
                  && !MethodBody(portCode, "Texture2DArray PackSeason(TextureReader reader, string season, int[] archives)")
                      .Contains("TilesetsSameSize("),
                "DistantTerrain: PackSeason's only precondition is TilesetsPackable - the size-equality refusal is gone from the packing path");
            Check(portCode.Contains("formatsAgree && (!anyResample || CanBlitInto(src[0].format))"),
                "DistantTerrain: the destination format falls back to RGBA32 when a compressed agreed format would also have to be resampled (an iOS DREAM pack)");
            Check(portCode.Contains("if (SliceNeedsResample(widths[b], heights[b])) anyResample = true;"),
                "DistantTerrain: that fallback is decided from the sources' sizes against TileSliceSize, by the pure predicate");
            // ...and the blit is chosen per ARCHIVE, from the source's whole shape against the
            // destination's. `formatsAgree` is false as soon as one of the four disagrees; sending the
            // ones already in the destination format through Graphics.ConvertTexture is a same-format
            // round trip that Metal answers with FALSE, and that refused the entire far terrain under
            // Biomes ("could not convert archive 3 record 0 from RGBA32 to RGBA32"). A matching source
            // is a plain slice copy; a source of any other size, chain or format is resampled.
            Check(portCode.Contains("SlicePack method = SlicePackMethod(src[b].width, src[b].height, src[b].mipmapCount, src[b].format,")
                  && portCode.Contains("dst.width, dst.height, dst.mipmapCount, dstFormat);")
                  && portCode.Contains("if (method == SlicePack.Copy)"),
                "DistantTerrain: copy-or-resample is decided per archive by the pure SlicePackMethod, from the two shapes and formats and nothing else, so a source that already is a destination slice is never sent through a converting path");
            // And the assertion on the uniform the shader clamps its explicit tile lod to.
            Check(MethodBody(portCode, "bool BuildTileArrays()").Contains("packedMipCount != TileSliceMipCount"),
                "DistantTerrain: BuildTileArrays checks the packed chain really is the 64-slice chain rather than assuming it");
            // The converting path itself, and the scratch surface it converts through - created
            // lazily on the first slice that needs it, so an all-Copy season allocates no render
            // target at all.
            Check(portCode.Contains("BlitSlice(src[b], record, dst, slice, scratch);")
                  && portCode.Contains("scratch = CreateSliceScratch(dst);")
                  && portCode.Contains("RenderTexture scratch = null;"),
                "DistantTerrain: a differing slice is blitted through a scratch RenderTexture that is only created when one is needed");
            Check(portCode.Contains("Graphics.Blit(src, scratch, srcSlice, 0);")
                  && portCode.Contains("scratch.GenerateMips();")
                  && portCode.Contains("Graphics.CopyTexture(scratch, 0, dst, dstSlice);"),
                "DistantTerrain: the fallback is a sampler fetch of the source slice into a destination-format RenderTexture, mips regenerated, copied back whole");
            Check(portCode.Contains("desc.graphicsFormat = dst.graphicsFormat;")
                  && portCode.Contains("desc.mipCount = Mathf.Max(1, dst.mipmapCount);"),
                "DistantTerrain: the scratch surface takes the destination array's exact graphics format and mip chain (CopyTexture demands both)");
            Check(MethodBody(portCode, "Texture2DArray PackSeason(TextureReader reader, string season, int[] archives)")
                      .Contains("DestroySliceScratch(scratch);"),
                "DistantTerrain: the scratch RenderTexture is released whichever way PackSeason leaves");

            // The beacons. Upstream shipped HighlightLocations true with RuntimeVisible false (baked
            // but hidden behind the End key); this port drops the hotkey, so the master switch is the
            // whole gate and its default is the iOS preset. The bundle's own modsettings.json still
            // says true - fetched content this port does not patch - so it is treated as unset.
            Check(!global::DistantTerrain.DistantTerrainLocationConfig.HighlightLocations,
                "DistantTerrain: the location beacons default OFF in code (the iOS preset lives here, not in the fetched modsettings.json)");
            Check(global::DistantTerrain.DistantTerrainLocationConfig.RuntimeVisible,
                "DistantTerrain: RuntimeVisible stays true - with the End key gone it would otherwise hide the beacons even when asked for");
            Check(!global::DistantTerrain.DistantTerrainPort.HighlightLocationsFrom(false, true)
                  && !global::DistantTerrain.DistantTerrainPort.HighlightLocationsFrom(false, false)
                  && global::DistantTerrain.DistantTerrainPort.HighlightLocationsFrom(true, true)
                  && !global::DistantTerrain.DistantTerrainPort.HighlightLocationsFrom(true, false),
                "DistantTerrain: only a settings file the player has on disk can turn the beacons on (all four cases)");
            // MOBILE: and the ordering that makes that gate mean anything. ModSettingsData.LoadLocalValues
            // creates the directory and Save()s a modsettings.json when the player has none, so the FIRST
            // mod.GetSettings() in Init brings the file into existence - probe after it and the answer is
            // always true and the beacons are always on. Init makes nine GetSettings() calls; the probe
            // has to precede all of them, which is a statement about the ORDER of two lines and nothing a
            // runtime check can see. So: source text, over the body of Init alone.
            int initAt = startupCode.IndexOf("public static void Init(InitParams initParams)", StringComparison.Ordinal);
            int initEnd = initAt >= 0 ? startupCode.IndexOf("\n        }", initAt, StringComparison.Ordinal) : -1;
            string initBody = initAt >= 0 && initEnd > initAt ? startupCode.Substring(initAt, initEnd - initAt) : "";
            int probeAt = initBody.IndexOf("UserSettingsFilePresent()", StringComparison.Ordinal);
            // Init does not call GetSettings( itself - it calls the nine Load*Settings() helpers that
            // do - so the first settings READ in Init is whichever comes first of the two forms.
            var firstReadMatch = System.Text.RegularExpressions.Regex.Match(
                initBody, @"GetSettings\(|Load[A-Za-z]*Settings\(");
            int firstSettingsReadAt = firstReadMatch.Success ? firstReadMatch.Index : -1;
            Check(initBody.Length > 0 && probeAt >= 0 && firstSettingsReadAt >= 0
                  && probeAt < firstSettingsReadAt,
                "DistantTerrain: Init probes for the player's settings file BEFORE the first settings read (which would create the file)",
                initBody.Length == 0 ? "could not isolate the Init body"
                    : "probe at " + probeAt + ", first settings read ("
                      + (firstReadMatch.Success ? firstReadMatch.Value : "none") + ") at " + firstSettingsReadAt
                      + " (body " + initBody.Length + " chars)");

            // The log literals. These are the lines Task 8 documents, Task 9 greps for in the
            // simulator run and the device hand-off asks for; a reworded one is a broken contract.
            foreach (string literal in new[]
            {
                "[DistantTerrain] far terrain built in {0} ms (heightmap {1} ms, carve {2} ms, lifts {3} ms, tilemap {4} ms, arrays {5} ms, other {6} ms)",
                "[DistantTerrain] map-pixel update {0} ms",
                "[DistantTerrain] arrays {0} MB",
                "[DistantTerrain] far terrain ready",
                "[DistantTerrain] far terrain failed: ",
                "[DistantTerrain] tileset arrays mismatch: ",
                "[DistantTerrain] tileset arrays packed: {0} copied, {1} resampled{2} ({3})",
                "[DistantTerrain] tileset arrays mip chain: packed {0} levels, a {1}x{1} slice is {2}",
                "[DistantTerrain] far terrain: pos={0:F1},{1:F1},{2:F1} size={3:F1},{4:F1},{5:F1} heightScale={6:F1} ",
                "layer={7} stackedCamera mask={8} near={9} far={10} depth={11} targetTexture={12} main.far={13} ",
                "shader={14} supported={15} material={16} renderer={17} drawHeightmap={18} ",
                "activeInHierarchy={19} parent={20}",
            })
                Check(port.Contains(literal), "DistantTerrain: log literal \"" + literal + "\"");
            Check(startup.Contains("[DistantTerrain] not available: "),
                "DistantTerrain: log literal \"[DistantTerrain] not available: \"");
            // And the two literals that must NOT come back. The probe printed a red Unity error
            // ("Graphics.ConvertTexture does not support a Texture2DArray as source.") once per
            // Biomes session and a port warning beside it to explain the red line; with no probe
            // there is nothing to explain, and `tileset arrays converted:` named an outcome that can
            // no longer happen. Pinned as an ABSENCE because a revived probe would still pass every
            // other check here.
            Check(!portCode.Contains("Graphics.ConvertTexture")
                  && !port.Contains("Graphics.ConvertTexture will not convert array")
                  && !port.Contains("[DistantTerrain] tileset arrays converted:"),
                "DistantTerrain: no ConvertTexture call, no warning explaining its red error line, and no `tileset arrays converted:` outcome");
            // ...and the two that the fixed destination retired. `tileset arrays blitted:` named the
            // format conversion alone, which is now one of three things the same blit does, and
            // `tileset arrays are NxN slices` warned about a destination that can no longer be
            // anything but 64x64.
            Check(!port.Contains("[DistantTerrain] tileset arrays blitted:")
                  && !port.Contains("[DistantTerrain] tileset arrays are {0}x{0} slices"),
                "DistantTerrain: the two lines the fixed 64x64 destination retired are gone - one `tileset arrays packed:` per season says what happened instead");
            Check(!port.Contains("[Distant Terrain]") && !startup.Contains("[Distant Terrain]"),
                "DistantTerrain: one log prefix, so grepping [DistantTerrain] finds every line the port writes");
            // The fifth stage is measured, not just printed: the pack runs near the end of
            // GenerateWorldTerrain, after the other three stages are recorded and before the tilemap
            // stopwatch starts, so a literal with an `arrays` field and no stopwatch behind it would
            // print a constant zero and read as "free".
            Check(portCode.Contains("lastArraysMs = arraysWatch.Elapsed.TotalMilliseconds")
                  && portCode.Contains("bool arraysBuilt = BuildTileArrays();"),
                "DistantTerrain: the arrays stage is timed around BuildTileArrays, not inferred");
            // And the sixth field is the residue, not a sixth stopwatch: total minus the five, so an
            // unmeasured cost (the TerrainData allocation, the reparent, the camera setup, or
            // whatever a later edit adds without a stage) shows up instead of vanishing into a total
            // that no longer adds up. Clamped, so five roundings cannot print a negative.
            Check(portCode.Contains("if (otherMs < 0) otherMs = 0;")
                  && portCode.Contains("long otherMs = totalMs - ("),
                "DistantTerrain: the `other` field is the total minus the five stages, clamped at zero");
        }


        // Two name-only rules the sky depends on. The converter decides a texture is a normal map
        // from its name alone, and Dynamic Skies names its cloud normals without the "_Normal"
        // suffix DFU writes, so the rule has to cover a bare "Normal" tail as well. And every
        // texture the shipped presets name must actually be in the fetched bundle: a name that is
        // not there binds null on the material and draws a black sky.
        static void TestDynamicSkiesPresetTextures()
        {
            // Names DREAM SKY and the default presets use for normal maps must be recognised as normals by the converter.
            Check(MobileModExtractor.IsNormalMapName("CdMCloudsNormal"), "DynamicSkies: CdMCloudsNormal is a normal map by name");
            Check(MobileModExtractor.IsNormalMapName("2k_sky_3_Normal"), "DynamicSkies: 2k_sky_3_Normal is a normal map by name");
            Check(!MobileModExtractor.IsNormalMapName("DefaultStars"), "DynamicSkies: star map is not a normal map");
            // No mods at all: null, and one expected "preset texture missing" warning in the log.
            Check(BLBSkybox.LoadPresetTexture(null, null, "nothing") == null, "DynamicSkies: fallback with no mods returns null without throwing");
            Check(BLBSkybox.LoadPresetTexture(null, null, "") == null, "DynamicSkies: an empty preset texture name returns null quietly");
            // Everything above is pure name logic. What follows reads the fetched bundle, which is
            // gitignored: skip with a note rather than throwing out of RunAll and losing the tally.
            string root = "Assets/Game/Mods/DynamicSkies";
            if (!Directory.Exists(root) || !Directory.Exists(root + "/Textures"))
            {
                log.AppendLine("  SKIP  Dynamic Skies preset textures (not fetched - run tools/bundled-mods/fetch.py --only DynamicSkies)");
                return;
            }
            // The default preset JSON names only textures the fetched bundle actually has. Every *.json
            // under the mod folder is read rather than one named folder: the presets live in
            // SkyboxSettings today, and one added elsewhere later must hold to the same rule.
            var have = new HashSet<string>(Directory.GetFiles(root + "/Textures").Where(f => !f.EndsWith(".meta")).Select(f => Path.GetFileNameWithoutExtension(f)));
            var wanted = new HashSet<string>();
            foreach (string json in Directory.GetFiles(root, "*.json", SearchOption.AllDirectories))
                foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(File.ReadAllText(json), "TextureFile\\\\?\":\\s*\\\\?\"([^\"\\\\]+)"))
                    wanted.Add(m.Groups[1].Value);
            string absent = string.Join(",", wanted.Where(w => !have.Contains(w)).OrderBy(w => w).ToArray());
            Check(wanted.Count >= 9 && absent.Length == 0,
                "DynamicSkies: every texture named by the default presets is in the bundle (" + wanted.Count + ": " + string.Join(",", wanted.OrderBy(w => w).ToArray()) + ")",
                "not in the bundle: " + absent);
        }


        // The conflict prompt reorders mods by title: the chosen member (plus whatever depends on it)
        // lands just below the last competing member, everything else keeps its place.
        static void TestModConflictOrder()
        {
            Func<string, IEnumerable<string>> deps = t => t == "VE Roads" ? new[] { "VE Base" } : Enumerable.Empty<string>();
            var order = new List<string> { "DREAM 1", "DREAM 2", "VE Base", "VE Roads", "Kokey", "Quests" };

            var ve = MobileModConflicts.MoveBelow(order, new HashSet<string> { "VE Base" }, new HashSet<string> { "DREAM 1", "DREAM 2", "Kokey" }, deps);
            Check(string.Join(",", ve.ToArray()) == "DREAM 1,DREAM 2,Kokey,VE Base,VE Roads,Quests", "MoveBelow: chosen and its dependents move below the last rival", string.Join(",", ve.ToArray()));

            var dream = MobileModConflicts.MoveBelow(order, new HashSet<string> { "DREAM 1", "DREAM 2" }, new HashSet<string> { "VE Base", "Kokey" }, deps);
            Check(string.Join(",", dream.ToArray()) == "VE Base,VE Roads,Kokey,DREAM 1,DREAM 2,Quests", "MoveBelow: multi-part chosen keeps its internal order", string.Join(",", dream.ToArray()));

            var none = MobileModConflicts.MoveBelow(order, new HashSet<string> { "Kokey" }, new HashSet<string> { "Absent" }, deps);
            Check(string.Join(",", none.ToArray()) == string.Join(",", order.ToArray()), "MoveBelow: no rival present leaves the order alone");

            var already = MobileModConflicts.MoveBelow(order, new HashSet<string> { "Kokey" }, new HashSet<string> { "DREAM 1", "VE Base" }, deps);
            Check(string.Join(",", already.ToArray()) == "DREAM 1,DREAM 2,VE Base,VE Roads,Kokey,Quests", "MoveBelow: chosen already below its rivals is a no-op");

            Check(MobileModConflicts.Signature(new[] { "Vanilla Enhanced", "DREAM" }) == "DREAM|Vanilla Enhanced", "Signature is order-independent");
            var dreamMember = MobileModConflicts.Groups[0].Members[0];
            Check(MobileModConflicts.Matches(dreamMember, "DREAM - TEXTURES (3 of 10)") && MobileModConflicts.Matches(dreamMember, "DREAM - Sprites (1 of 2)")
                  && !MobileModConflicts.Matches(dreamMember, "DREAM - SOUND"), "Matches: DREAM member covers textures and sprites parts only");
            Check(MobileModConflicts.Groups.All(g => g.Members.Length >= 2 && g.Question.Contains("{mods}")), "every conflict group has two or more members and a templated question");
            string q2 = MobileModConflicts.QuestionFor(MobileModConflicts.Groups[0], new List<string> { "Vanilla Enhanced", "Kokey's Temperate" });
            Check(q2.StartsWith("Vanilla Enhanced and Kokey's Temperate replace"), "QuestionFor names the two installed members", q2);
            string q3 = MobileModConflicts.QuestionFor(MobileModConflicts.Groups[0], new List<string> { "DREAM", "Vanilla Enhanced", "Kokey's Temperate" });
            Check(q3.StartsWith("DREAM, Vanilla Enhanced and Kokey's Temperate replace"), "QuestionFor lists three members with commas", q3);
        }


        static void TestPortedModGate()
        {
            Check(string.Join(",", MobilePortedMods.Gate(true, true, true).Select(b => b ? "1" : "0").ToArray()) == "1,1,1", "Gate: all on run");
            Check(string.Join(",", MobilePortedMods.Gate(false, true, true).Select(b => b ? "1" : "0").ToArray()) == "0,0,0", "Gate: without RR nothing runs");
            Check(string.Join(",", MobilePortedMods.Gate(true, false, true).Select(b => b ? "1" : "0").ToArray()) == "1,0,0", "Gate: C&C needs Items");
            Check(string.Join(",", MobilePortedMods.Gate(true, true, false).Select(b => b ? "1" : "0").ToArray()) == "1,1,0", "Gate: C&C off leaves the others");
        }

        static void TestPortedModOrder()
        {
            Check(string.Join(",", MobilePortedMods.OrderedPriorities(2, 5, 9, 40).Select(i => i.ToString()).ToArray()) == "2,5,9", "order: already RR<Items<C&C is kept");
            Check(string.Join(",", MobilePortedMods.OrderedPriorities(0, 1, 2, 2).Select(i => i.ToString()).ToArray()) == "0,1,2", "order: contiguous correct order kept");
            Check(string.Join(",", MobilePortedMods.OrderedPriorities(0, 2, 1, 44).Select(i => i.ToString()).ToArray()) == "45,46,47", "order: C&C before Items moves all three to the end in order");
            Check(string.Join(",", MobilePortedMods.OrderedPriorities(3, 1, 2, 10).Select(i => i.ToString()).ToArray()) == "11,12,13", "order: RR last moves all three to the end in order");
        }

        static void TestPortedModTitles()
        {
            Check(System.Array.IndexOf(MobilePortedMods.Titles, "Dynamic Skies") >= 0, "PortedMods: Dynamic Skies is a default-off title");
            Check(MobilePortedMods.SkyRuns(true, true) && !MobilePortedMods.SkyRuns(true, false) && !MobilePortedMods.SkyRuns(false, true), "PortedMods: sky runs only when its entry exists and is on");
            Check(MobilePortedMods.Gate(true, true, true).Length == 3, "PortedMods: survival gate unchanged by the sky entry");
            Check(MobilePortedMods.SkySceneReady(true, true) && !MobilePortedMods.SkySceneReady(true, false) && !MobilePortedMods.SkySceneReady(false, true) && !MobilePortedMods.SkySceneReady(false, false), "PortedMods: the sky starts only when both the sun light and the camera are in the scene");
            Check(MobilePortedMods.LLTitle == "Location Loader" && System.Array.IndexOf(MobilePortedMods.Titles, MobilePortedMods.LLTitle) >= 0, "PortedMods: Location Loader is a default-off title");
            // MOBILE: the version the launcher's MODS page shows for the built-in Location Loader
            // entry. "0.3" is KABoissonneault's own at pin a5e7a18; the suffix says this build also
            // carries carademono's object-type-5 backport, which is the difference between WoD's 24
            // farm/dock prefabs rendering and rendering as empty clearings. THIRD-PARTY.md describes
            // the port that way, so the string the player sees must too.
            Check(System.IO.File.ReadAllText("Assets/Scripts/Game/Mobile/MobileMods.cs")
                      .Contains("ll.ModInfo.ModVersion = \"0.3+type5\";"),
                  "PortedMods: the built-in Location Loader entry is versioned 0.3+type5, as the docs say");
            Check(MobilePortedMods.WoDTitle == "World of Daggerfall" && System.Array.IndexOf(MobilePortedMods.Titles, MobilePortedMods.WoDTitle) >= 0, "PortedMods: World of Daggerfall is a default-off title");
            Check(MobilePortedMods.WodRuns(true, true, true), "PortedMods: World of Daggerfall runs when Location Loader started and Daggerfall Expanded Textures is on");
            Check(!MobilePortedMods.WodRuns(true, false, true) && !MobilePortedMods.WodRuns(false, false, false), "PortedMods: World of Daggerfall does not run when its own entry is off");
            // The first argument is whether Location Loader actually STARTED, not whether it is switched
            // on: a location mod is nothing without the loader reading it, so an LL Init that threw must
            // keep WoD out too rather than log "started World of Daggerfall" under "Location Loader start failed".
            Check(!MobilePortedMods.WodRuns(false, true, true), "PortedMods: World of Daggerfall does not run when Location Loader is off or its Init failed");
            // WoD's manifest depends on Daggerfall Expanded Textures for the textures its scenery uses.
            Check(!MobilePortedMods.WodRuns(true, true, false), "PortedMods: World of Daggerfall does not run when Daggerfall Expanded Textures is off or missing");
            Check(MobilePortedMods.DETFileName == "daggerfall expanded textures", "PortedMods: the Daggerfall Expanded Textures dependency is matched on the bundle file name WoD's manifest names");
            Check(!string.IsNullOrEmpty(MobilePortedMods.WoDDetNote) && MobilePortedMods.WoDDetNote.Contains("Expanded Textures"), "PortedMods: the World of Daggerfall dependency note names Daggerfall Expanded Textures");

            // World of Daggerfall - Biomes: a data bundle of its own, off by default, textured out of
            // Daggerfall Expanded Textures. It re-skins terrain and swaps nature billboards on its own,
            // so unlike WoD it needs neither Location Loader nor WoD - the gate is its switch and DET.
            Check(MobilePortedMods.BiomesTitle == "World of Daggerfall - Biomes" && System.Array.IndexOf(MobilePortedMods.Titles, MobilePortedMods.BiomesTitle) >= 0, "PortedMods: Biomes is a default-off title");
            Check(MobilePortedMods.BiomesRuns(true, true) && !MobilePortedMods.BiomesRuns(true, false) && !MobilePortedMods.BiomesRuns(false, true) && !MobilePortedMods.BiomesRuns(false, false), "PortedMods: Biomes runs only with its switch and Daggerfall Expanded Textures on");
            Check(MobilePortedMods.BiomesDetNote.Contains("Expanded Textures"), "PortedMods: Biomes note names the missing dependency");

            // World of Daggerfall - Terrain: its own data bundle, off by default, and gated by nothing
            // - Basic Roads is optional to it and Daggerfall Expanded Textures is not its dependency at
            // all. It replaces DaggerfallUnity.TerrainSampler, so it is started late: an Init that threw
            // there cannot then cost the mods before it their start.
            Check(MobilePortedMods.TerrainTitle == "World of Daggerfall - Terrain" && System.Array.IndexOf(MobilePortedMods.Titles, MobilePortedMods.TerrainTitle) >= 0, "PortedMods: World of Daggerfall - Terrain is a default-off title");
            // Titles is what DefaultOff walks, so this pins the default-off coverage and the dependency
            // ORDER OF THAT LIST - not the start order, which is the statement sequence in StartEnabled
            // and is pinned by the block comment there. Distant Terrain and then Real Grass were
            // appended after it, so the Terrain entry is now third from last.
            Check(MobilePortedMods.Titles[MobilePortedMods.Titles.Length - 3] == MobilePortedMods.TerrainTitle, "PortedMods: World of Daggerfall - Terrain is third from last in Titles, the list DefaultOff walks");

            // MOBILE: Distant Terrain (World of Daggerfall flavour). Gated by nothing but its own
            // switch, like the Terrain port, and started after it - last of the immediate Inits,
            // because the sky's deferred start below now waits on the stacked camera this port
            // creates. The title is the bundle's own ModTitle: get it wrong and the launcher entry
            // simply never resolves, so the mod is silently absent rather than broken.
            Check(MobilePortedMods.DistantTitle == "Distant Terrain of the World of Daggerfall",
                "PortedMods: the Distant Terrain title is the ModTitle its bundle declares",
                MobilePortedMods.DistantTitle);
            // ...and that literal is checked against the fetched manifest itself where it is present.
            // The fetched folder is gitignored, so a clone that has not run fetch.py skips this.
            string distantManifest = "Assets/Game/Mods/DistantTerrainWoD/distantterrain.dfmod.json";
            if (File.Exists(distantManifest))
            {
                var manifestMatch = System.Text.RegularExpressions.Regex.Match(File.ReadAllText(distantManifest), "\"ModTitle\"\\s*:\\s*\"([^\"]*)\"");
                Check(manifestMatch.Success && manifestMatch.Groups[1].Value == MobilePortedMods.DistantTitle,
                    "PortedMods: DistantTitle is the ModTitle in distantterrain.dfmod.json",
                    manifestMatch.Success ? manifestMatch.Groups[1].Value : "no ModTitle in " + distantManifest);
            }
            else log.AppendLine("  SKIP  DistantTitle against the fetched manifest (not fetched - run tools/bundled-mods/fetch.py --only DistantTerrainWoD)");
            Check(System.Array.IndexOf(MobilePortedMods.Titles, MobilePortedMods.DistantTitle) >= 0,
                "PortedMods: Distant Terrain is a default-off title");
            // Real Grass was appended after it (same move the Terrain entry made when Distant
            // Terrain arrived), so Distant Terrain is now next to last.
            Check(MobilePortedMods.Titles[MobilePortedMods.Titles.Length - 2] == MobilePortedMods.DistantTitle,
                "PortedMods: Distant Terrain is next to last in Titles, after World of Daggerfall - Terrain");

            // MOBILE: Real Grass. The only compiled-in mod that uses Unity's terrain DETAIL
            // renderer, so it takes none of DFU's four terrain slots and is gated by nothing but
            // its own switch - it stacks with WoD Terrain, Distant Terrain, Biomes and Basic Roads
            // rather than competing with them. Started last of the immediate Inits, after Distant
            // Terrain, because its promotion hook only ever fires on terrains promoted afterwards
            // and it must not sit between the Terrain sampler swap and the sky's deferred start.
            // Its Init declines by logging "[RealGrass] not available: ..." and returning (no
            // detail shaders, or no grass texture in the bundle), never by throwing, so it wants
            // the four-argument StartOne with Installed as the flag.
            Check(MobilePortedMods.GrassTitle == "Real Grass",
                "PortedMods: the Real Grass title is the ModTitle its bundle declares",
                MobilePortedMods.GrassTitle);
            // ...and that literal is checked against the fetched manifest itself where it is
            // present. The fetched folder is gitignored, so a clone that has not run fetch.py skips it.
            string grassManifest = "Assets/Game/Mods/RealGrass/RealGrass.dfmod.json";
            if (File.Exists(grassManifest))
            {
                var grassMatch = System.Text.RegularExpressions.Regex.Match(File.ReadAllText(grassManifest), "\"ModTitle\"\\s*:\\s*\"([^\"]*)\"");
                Check(grassMatch.Success && grassMatch.Groups[1].Value == MobilePortedMods.GrassTitle,
                    "PortedMods: GrassTitle is the ModTitle in RealGrass.dfmod.json",
                    grassMatch.Success ? grassMatch.Groups[1].Value : "no ModTitle in " + grassManifest);
            }
            else log.AppendLine("  SKIP  GrassTitle against the fetched manifest (not fetched - run tools/bundled-mods/fetch.py --only RealGrass)");
            Check(System.Array.IndexOf(MobilePortedMods.Titles, MobilePortedMods.GrassTitle) >= 0,
                "PortedMods: Real Grass is a default-off title");
            Check(MobilePortedMods.Titles[MobilePortedMods.Titles.Length - 1] == MobilePortedMods.GrassTitle,
                "PortedMods: Real Grass is last in Titles, after Distant Terrain");
            // The launcher block itself: the four-argument StartOne, the Installed flag and the
            // [RealGrass] hint, placed after the Distant Terrain block and before the sky is
            // handed back. Asked of the source text because there is no scene to start it in.
            string portedSrc = StripShaderComments(File.ReadAllText("Assets/Scripts/Game/Mobile/MobilePortedMods.cs"));
            Check(portedSrc.Contains("StartOne(GrassTitle,")
                  && portedSrc.Contains("RealGrass.RealGrassPort.Init(new InitParams(")
                  && portedSrc.Contains("() => RealGrass.RealGrassPort.Installed")
                  && portedSrc.Contains("\"[RealGrass]\""),
                "PortedMods: Real Grass is started by the four-argument StartOne with Installed and the [RealGrass] hint");
            int distantAt = portedSrc.IndexOf("StartOne(DistantTitle,", StringComparison.Ordinal);
            int grassAt = portedSrc.IndexOf("StartOne(GrassTitle,", StringComparison.Ordinal);
            int returnAt = portedSrc.IndexOf("return SkyRuns(", StringComparison.Ordinal);
            Check(distantAt >= 0 && grassAt > distantAt && returnAt > grassAt,
                "PortedMods: the Real Grass block sits after Distant Terrain's and before the sky is handed back",
                distantAt + " < " + grassAt + " < " + returnAt);
            Check(portedSrc.Contains("Mod grass = Entry(GrassTitle);")
                  && portedSrc.Contains("if (grass != null && grass.Enabled)"),
                "PortedMods: Real Grass runs only when its launcher entry exists and is on - off by default");

            // The sky's scene poll, now four-argument. Dynamic Skies' Init picks its stacked-camera
            // branch from what is in the scene at the moment it runs, so when Distant Terrain is
            // running the sky must not start until that camera exists - otherwise the branch is
            // decided by a race between two deferred starts. Contract, stated independently of the
            // implementation: sunLight && mainCamera && (!distantRunning || stackedCamera).
            bool skyTableOk = true;
            for (int mask = 0; mask < 16; mask++)
            {
                bool sun = (mask & 1) != 0, cam = (mask & 2) != 0, running = (mask & 4) != 0, stacked = (mask & 8) != 0;
                if (MobilePortedMods.SkySceneReady(sun, cam, running, stacked) != (sun && cam && (!running || stacked)))
                    skyTableOk = false;
            }
            Check(skyTableOk, "PortedMods: SkySceneReady is sunLight && mainCamera && (!distantRunning || stackedCamera) (all sixteen cases)");
            // The two rows the whole change exists for, named so a regression says which one broke.
            Check(!MobilePortedMods.SkySceneReady(true, true, true, false),
                "PortedMods: the sky waits when Distant Terrain is running and the stacked camera is not up yet");
            Check(MobilePortedMods.SkySceneReady(true, true, false, false),
                "PortedMods: the sky does not wait for a stacked camera when Distant Terrain is not running");
            // The two-argument form is kept for the callers that never knew about Distant Terrain:
            // it must mean exactly "Distant Terrain is not running", not "the stacked camera is up".
            bool forwardOk = true;
            for (int mask = 0; mask < 4; mask++)
            {
                bool sun = (mask & 1) != 0, cam = (mask & 2) != 0;
                if (MobilePortedMods.SkySceneReady(sun, cam) != MobilePortedMods.SkySceneReady(sun, cam, false, false))
                    forwardOk = false;
            }
            Check(forwardOk, "PortedMods: the two-argument SkySceneReady forwards to (sunLight, mainCamera, false, false)");
            Check(MobilePortedMods.SkyStackedCameraWait == "[PortedMods] Dynamic Skies waiting for Distant Terrain's stacked camera",
                "PortedMods: the stacked-camera wait line is the literal a Player.log reader greps for",
                MobilePortedMods.SkyStackedCameraWait);

            // That wait is bounded. A far terrain that threw or refused now clears Running from its
            // teardown, which releases the poll on the next pass - but the bound is what stops ANY
            // other reason for an absent stacked camera stranding Dynamic Skies for a whole session
            // on one log line. Only passes spent with the rest of the scene up and that one camera
            // missing are counted, so a long title screen never spends the budget.
            Check(MobilePortedMods.SkyStackedCameraWaitPasses == 15,
                "PortedMods: the sky waits fifteen 1 Hz passes for the stacked camera before starting anyway",
                MobilePortedMods.SkyStackedCameraWaitPasses.ToString());
            bool giveUpOk = true;
            for (int passes = 0; passes <= 20; passes++)
                if (MobilePortedMods.GiveUpOnStackedCamera(passes) != (passes >= 15))
                    giveUpOk = false;
            Check(giveUpOk && !MobilePortedMods.GiveUpOnStackedCamera(14) && MobilePortedMods.GiveUpOnStackedCamera(15),
                "PortedMods: GiveUpOnStackedCamera flips at the fifteenth wasted pass and not before (0-20)");
            Check(MobilePortedMods.SkyStackedCameraGiveUp == "[PortedMods] Dynamic Skies starting without Distant Terrain's stacked camera",
                "PortedMods: the give-up line is the literal a Player.log reader greps for",
                MobilePortedMods.SkyStackedCameraGiveUp);

            // Dynamic Skies' own branch, which the give-up path lands on. Upstream keyed it on the
            // DistantTerrain OBJECT; that object is DontDestroyOnLoad and survives a teardown that
            // destroyed the camera, so a fallback start took the else branch, left stackedCam null
            // AND skipped `cameraClearExterior = Skybox` - the line that stops CameraClearManager
            // unsetting the skybox after an exterior transition. Keyed on the camera it is identical
            // when Distant Terrain is off and correct when its build failed.
            string skyboxSrc = StripShaderComments(File.ReadAllText("Assets/Scripts/Game/Mobile/Ports/DynamicSkies/BLBSkybox.cs"));
            Check(skyboxSrc.Contains("GameObject goCam = GameObject.Find(\"stackedCamera\");")
                  && !skyboxSrc.Contains("GameObject.Find(\"DistantTerrain\")"),
                "DynamicSkies: the stacked-camera branch is keyed on the camera, not on the DistantTerrain object");
            Check(skyboxSrc.Contains("[DynamicSkies] clear flags on: player camera")
                  && skyboxSrc.Contains("[DynamicSkies] clear flags on: stackedCamera"),
                "DynamicSkies: the branch says which camera the sky's clear flags landed on");
            // MOBILE: and the third branch, for a far terrain that turns up after Init's bounded poll
            // gave up. Without it the player-camera branch keeps forcing playerCam.clearFlags = Skybox
            // every frame, which clears over the stacked camera and hides the far terrain for the rest
            // of the session with nothing in the log to say so - the failure this line makes visible.
            Check(skyboxSrc.Contains("[DynamicSkies] clear flags on: stackedCamera (late)")
                  && skyboxSrc.Contains("DistantTerrain.DistantTerrainPort.Running")
                  && skyboxSrc.Contains("RebindLateStackedCamera();"),
                "DynamicSkies: LateUpdate re-binds a stacked camera that arrived after Init gave up, and says so once");

            // Start order, from the source: Distant Terrain's Init runs after the Terrain port's and
            // before StartEnabled hands the sky back for its deferred start. Comments are stripped so
            // a check for an absent symbol cannot be satisfied by a comment that names it.
            string launcherSrc = StripShaderComments(File.ReadAllText("Assets/Scripts/Game/Mobile/MobilePortedMods.cs"));
            int terrainInitAt = launcherSrc.IndexOf("Monobelisk.InterestingTerrains.Init");
            int distantInitAt = launcherSrc.IndexOf("DistantTerrainPort.Init");
            int skyReturnAt = launcherSrc.LastIndexOf("return SkyRuns");
            Check(terrainInitAt >= 0 && distantInitAt > terrainInitAt && skyReturnAt > distantInitAt,
                "PortedMods: Distant Terrain starts after World of Daggerfall - Terrain and before the sky is handed back for its deferred start",
                "terrain " + terrainInitAt + ", distant " + distantInitAt + ", sky return " + skyReturnAt);
            // Installed only becomes true at StreamingWorld.OnReady, long after Init returns, so the
            // launcher's four-argument StartOne would always write "did not start" if it asked that.
            Check(launcherSrc.Contains("DistantTerrainPort.Running") && !launcherSrc.Contains("DistantTerrainPort.Installed"),
                "PortedMods: the launcher asks Running, not the world-entry Installed flag");

            // A mod whose Init throws must not take the mods after it - or the sky's deferred start -
            // down with it. The LogError below is this check working, not a failure.
            bool ranClean = false;
            bool cleanSaidStarted = MobilePortedMods.StartOne("self test clean mod", () => ranClean = true);
            bool threwSaidStarted = MobilePortedMods.StartOne("self test throwing mod", () => { throw new InvalidOperationException("self test: deliberate Init failure"); });
            Check(cleanSaidStarted && ranClean && !threwSaidStarted, "PortedMods: a mod whose Init throws is contained and is not logged as started");

            // The overload for a mod that declines by logging and returning - every WoD Terrain refusal
            // does. "started" must follow the mod's own answer, not merely a clean return. The Log/
            // LogError lines these three write are the checks working, not failures.
            bool ranInstalled = false;
            bool installedSaidStarted = MobilePortedMods.StartOne("self test installed mod", () => ranInstalled = true, () => true, "[SelfTest]");
            bool declinedSaidStarted = MobilePortedMods.StartOne("self test declining mod", () => { }, () => false, "[SelfTest]");
            bool overloadThrewSaidStarted = MobilePortedMods.StartOne("self test throwing mod (installed check)",
                () => { throw new InvalidOperationException("self test: deliberate Init failure"); }, () => true, "[SelfTest]");
            Check(installedSaidStarted && ranInstalled, "PortedMods: StartOne with an installed check runs Init and reports started when the mod installed");
            Check(!declinedSaidStarted, "PortedMods: StartOne with an installed check reports did not start when Init declined without throwing");
            Check(!overloadThrewSaidStarted, "PortedMods: StartOne with an installed check contains a throwing Init and reports not started");
        }

        class FakeJourney : IJourneyState
        {
            public bool Active { get; set; }
            public bool FollowingRoad { get; set; }
            public int Pauses; public string LastHud;
            public void Pause() { Pauses++; }
            public void Hud(string text) { LastHud = text; }
        }

        static void TestTravelOptionsBridge()
        {
            var j = new FakeJourney { Active = true, FollowingRoad = true };
            object got = null;
            Check(MobileTravelOptionsBridge.Handle("isTravelActive", null, (m, d) => got = d, j) && (bool)got, "bridge: isTravelActive answers the pilot state");
            j.Active = false; got = null;
            MobileTravelOptionsBridge.Handle("isTravelActive", null, (m, d) => got = d, j);
            Check(got is bool && !(bool)got, "bridge: isTravelActive false when idle");
            MobileTravelOptionsBridge.Handle("pauseTravel", null, null, j);
            Check(j.Pauses == 0, "bridge: pauseTravel when idle does nothing");
            j.Active = true;
            MobileTravelOptionsBridge.Handle("pauseTravel", null, null, j);
            Check(j.Pauses == 1, "bridge: pauseTravel while travelling pauses once");
            MobileTravelOptionsBridge.Handle("showMessage", "You are cold.", null, j);
            Check(j.LastHud == "You are cold.", "bridge: showMessage reaches the HUD");
            got = null; MobileTravelOptionsBridge.Handle("isFollowingRoad", null, (m, d) => got = d, j);
            Check(got is bool && (bool)got, "bridge: isFollowingRoad true on a road while travelling");
            j.FollowingRoad = false; got = null; MobileTravelOptionsBridge.Handle("isPathFollowing", null, (m, d) => got = d, j);
            Check(got is bool && !(bool)got, "bridge: isPathFollowing false off road");
            bool called = false;
            Check(!MobileTravelOptionsBridge.Handle("somethingElse", null, (m, d) => called = true, j) && !called, "bridge: unknown message ignored, no callback");
        }

        static void TestTavernAlcohol()
        {
            Check(MobileDrunkenness.AlcoholFor(0, 50) == 6 && MobileDrunkenness.AlcoholFor(3, 50) == 15, "drunk: ale 8 and wine 20 scale to 6 and 15 at endurance 50");
            Check(MobileDrunkenness.AlcoholFor(0, 100) == 4 && MobileDrunkenness.AlcoholFor(0, 0) == 8, "drunk: endurance 100 halves a drink, 0 leaves it whole");
            Check(MobileDrunkenness.AlcoholFor(4, 50) == 0 && MobileDrunkenness.AlcoholFor(10, 50) == 0 && MobileDrunkenness.AlcoholFor(-1, 50) == 0, "drunk: food rows carry none");
            Check(MobileDrunkenness.StageFor(0, 60) == MobileDrunkenness.Stage.Sober && MobileDrunkenness.StageFor(20, 60) == MobileDrunkenness.Stage.Sober, "drunk: below a third of endurance is sober");
            Check(MobileDrunkenness.StageFor(21, 60) == MobileDrunkenness.Stage.Tipsy && MobileDrunkenness.StageFor(40, 60) == MobileDrunkenness.Stage.Tipsy, "drunk: tipsy between a third and two thirds");
            Check(MobileDrunkenness.StageFor(41, 60) == MobileDrunkenness.Stage.Drunk && MobileDrunkenness.StageFor(60, 60) == MobileDrunkenness.Stage.Drunk, "drunk: drunk up to endurance");
            Check(MobileDrunkenness.StageFor(61, 60) == MobileDrunkenness.Stage.BlindDrunk && MobileDrunkenness.StageFor(96, 100) == MobileDrunkenness.Stage.BlindDrunk, "drunk: past endurance (capped at 95) you pass out");
            Check(MobileDrunkenness.Sobered(50, false) == 48 && MobileDrunkenness.Sobered(50, true) == 44 && MobileDrunkenness.Sobered(1, true) == 0, "drunk: sobers 2 awake, 6 asleep, never below 0");
            int sp, ag; MobileDrunkenness.Penalties(MobileDrunkenness.Stage.Tipsy, out sp, out ag);
            Check(sp == -5 && ag == 0, "drunk: tipsy is a small speed penalty only");
            MobileDrunkenness.Penalties(MobileDrunkenness.Stage.Drunk, out sp, out ag);
            Check(sp == -15 && ag == -10, "drunk: drunk costs speed and agility");
            Check(MobileDrunkenness.RobberyLoss(1000) == 40 && MobileDrunkenness.RobberyLoss(150) == 15 && MobileDrunkenness.RobberyLoss(5) == 0, "drunk: robbery takes a tenth, at most 40");
            Check(MobileDrunkenness.PassOutHours(0) == 4 && MobileDrunkenness.PassOutHours(3) == 7 && MobileDrunkenness.PassOutHours(9) == 7, "drunk: pass out lasts 4 to 7 hours");
            MobileDrunkenness.Level = 250; Check(MobileDrunkenness.Level == 100, "drunk: level clamps at 100"); MobileDrunkenness.Level = -5; Check(MobileDrunkenness.Level == 0, "drunk: level clamps at 0");
        }

        /// <summary>
        /// Mods.json must carry the choice, not an empty object. Mod opts in to serialization
        /// (fsMemberSerialization.OptIn), so without [SerializeField] on the four members the
        /// engine reads back, FullSerializer wrote "[{},{},...]" and no enabled/priority choice
        /// ever survived a relaunch. ModManager.LoadModSettings matches on Title, so Title must
        /// round-trip too.
        /// </summary>
        static void TestModSettingsSerialization()
        {
            Mod mod = new Mod();
            mod.ModInfo.ModTitle = "Self test mod";
            mod.Enabled = false;

            fsData data = null;
            fsResult result = new fsSerializer().TrySerialize<List<Mod>>(new List<Mod>() { mod }, out data);
            Check(!result.Failed, "mod settings: a list of mods serializes", result.FormattedMessages);
            string json = fsJsonPrinter.CompressedJson(data);
            Check(json.Contains("\"Title\":\"Self test mod\""), "mod settings: Title is written", json);
            Check(json.Contains("\"Enabled\":false"), "mod settings: Enabled is written", json);
            Check(json.Contains("\"LoadPriority\":"), "mod settings: LoadPriority is written", json);
            Check(!json.Contains("[{}]"), "mod settings: the entry is not an empty object", json);

            // Read it back the way LoadModSettings does, with a load priority the writer never used.
            string edited = json.Replace("\"LoadPriority\":0", "\"LoadPriority\":7");
            List<Mod> back = new List<Mod>();
            fsResult readResult = new fsSerializer().TryDeserialize<List<Mod>>(fsJsonParser.Parse(edited), ref back);
            Check(!readResult.Failed && back.Count == 1, "mod settings: the list deserializes", readResult.FormattedMessages);
            if (back.Count == 1)
            {
                Check(back[0].Title == "Self test mod", "mod settings: Title survives the round trip", back[0].Title);
                Check(back[0].Enabled == false, "mod settings: Enabled survives the round trip");
                Check(back[0].LoadPriority == 7, "mod settings: LoadPriority survives the round trip", back[0].LoadPriority.ToString());
            }

            // A start-up gate that switches a mod off writes the settings again - but by then
            // ModManager.Init has dropped every mod the player had switched off out of its list, so
            // serializing that list alone shrank Mods.json to the enabled mods (10 entries to 3 on
            // the simulator run). A mod with no entry defaults to enabled, so every switched-off mod
            // came back on at the next launch. The write merges the dropped entries back in.
            Mod previousA = new Mod(); previousA.ModInfo.ModTitle = "A"; previousA.Enabled = false;
            Mod currentA = new Mod(); currentA.ModInfo.ModTitle = "A"; currentA.Enabled = true;
            // Mod.LoadPriority has an internal setter, so B gets its priority the way
            // LoadModSettings gives one to a mod - through the serializer.
            List<Mod> dropped = new List<Mod>();
            fsResult droppedResult = new fsSerializer().TryDeserialize<List<Mod>>(
                fsJsonParser.Parse("[{\"Title\":\"B\",\"Enabled\":false,\"LoadPriority\":7}]"), ref dropped);
            Check(!droppedResult.Failed && dropped.Count == 1 && dropped[0].LoadPriority == 7,
                  "mod settings: a dropped entry with a load priority is readable", droppedResult.FormattedMessages);
            Mod previousB = dropped.Count == 1 ? dropped[0] : new Mod();
            List<Mod> merged = ModManager.MergeModSettings(new List<Mod>() { currentA },
                                                           new List<Mod>() { previousA, previousB });
            Check(merged.Count == 2, "mod settings: a mod dropped from the list still gets an entry",
                  merged.Count.ToString());
            Mod mergedOff = merged.Find(m => m.Title == "B");
            Check(mergedOff != null && !mergedOff.Enabled,
                  "mod settings: the dropped mod keeps its switched-off setting");
            Check(mergedOff != null && mergedOff.LoadPriority == 7,
                  "mod settings: the dropped mod keeps its load priority",
                  mergedOff == null ? "no entry" : mergedOff.LoadPriority.ToString());
            Mod mergedBoth = merged.Find(m => m.Title == "A");
            Check(mergedBoth != null && mergedBoth.Enabled,
                  "mod settings: a mod in both lists takes its current value, not the older one");
            Check(merged.Count == 2 && merged[0].Title == "A",
                  "mod settings: current mods come first, in order",
                  merged.Count == 0 ? "empty" : merged[0].Title);
        }

        /// <summary>
        /// Mod lookup by file name must survive the port's built-in entries.
        ///
        /// Roads and tracks, Real travel, Summer start and the TravelOptions bridge are Mod objects
        /// built in code, never loaded from a .dfmod, so their FileName is null. The upstream lambda
        /// called FileName.Equals directly and threw a NullReferenceException as soon as the walk
        /// reached one: every frame the MODS window was open (CheckDependencies), and on every
        /// conflict reorder (MobileModConflicts.MoveBelow), which is why a player's conflict choices
        /// silently never applied.
        /// </summary>
        static void TestModLookupTolerantOfBuiltIns()
        {
            Mod builtIn = new Mod();
            Check(builtIn.FileName == null, "mod lookup: a mod built in code really has no file name");
            Check(!ModManager.FileNameMatches(builtIn, "roads"), "mod lookup: a null file name never matches");
            Check(!ModManager.FileNameMatches(null, "roads"), "mod lookup: a null mod never matches");
            Check(!ModManager.FileNameMatches(builtIn, null), "mod lookup: a null name never matches");

            // Positive case: FileName is set through a private setter, so reach it the way the
            // loader does rather than through a constructor that wants a real asset bundle.
            Mod loaded = new Mod();
            typeof(Mod).GetProperty("FileName").GetSetMethod(true).Invoke(loaded, new object[] { "dreamtextures" });
            Check(ModManager.FileNameMatches(loaded, "dreamtextures"), "mod lookup: an equal file name matches");
            Check(!ModManager.FileNameMatches(loaded, "DREAMTEXTURES"), "mod lookup: the match stays ordinal, not case folded");
            Check(!ModManager.FileNameMatches(loaded, null), "mod lookup: a real mod does not match a null name");
            Check(!ModManager.FileNameMatches(loaded, "dreamtexture") && !ModManager.FileNameMatches(loaded, "dreamtexturess"),
                "mod lookup: a name that is merely close does not match");

            // MOBILE: '-' and ' ' are the same character in a mod file name. DREAM - SKY's manifest
            // declares `Dependencies: [{ "Name": "dynamic skies" }]` while the port ships that data as
            // dynamic-skies.dfmod, so FileName is `dynamic-skies` and an ordinal Equals made DFU's
            // launcher warn that the pair "might not work" for the whole of every session - while the
            // sky worked perfectly, because MobilePortedMods starts it directly and never asks DFU's
            // dependency machinery. Both directions, because either side can be the one with the
            // hyphen: a dependency naming `dynamic skies` against our bundle, or a bundle named with
            // a space against a dependency written with a hyphen.
            Mod hyphenated = new Mod();
            typeof(Mod).GetProperty("FileName").GetSetMethod(true).Invoke(hyphenated, new object[] { "dynamic-skies" });
            Mod spaced = new Mod();
            typeof(Mod).GetProperty("FileName").GetSetMethod(true).Invoke(spaced, new object[] { "dynamic skies" });
            Check(ModManager.FileNameMatches(hyphenated, "dynamic skies"),
                "mod lookup: dynamic-skies.dfmod satisfies DREAM - SKY's `dynamic skies` dependency");
            Check(ModManager.FileNameMatches(spaced, "dynamic-skies"),
                "mod lookup: ...and the other way round, so whichever spelling a bundle ships under resolves");
            Check(ModManager.FileNameMatches(hyphenated, "dynamic-skies") && ModManager.FileNameMatches(spaced, "dynamic skies"),
                "mod lookup: the exact spellings still match, both of them");
            Check(!ModManager.FileNameMatches(hyphenated, "dynamicskies") && !ModManager.FileNameMatches(spaced, "dynamicskies"),
                "mod lookup: the separator has to be PRESENT - only not spelled a particular way - so `dynamicskies` does not match");
            Check(!ModManager.FileNameMatches(hyphenated, "dynamic_skies")
                  && !ModManager.FileNameMatches(hyphenated, "dynamic.skies")
                  && !ModManager.FileNameMatches(hyphenated, "Dynamic Skies"),
                "mod lookup: no other character joins the pair, and case is still case");

            // And the DET gate in MobilePortedMods asks the same question about a name that has a
            // space in it already, so the new tolerance must not have moved that answer.
            Mod det = new Mod();
            typeof(Mod).GetProperty("FileName").GetSetMethod(true).Invoke(det, new object[] { "daggerfall expanded textures" });
            Check(ModManager.FileNameMatches(det, "daggerfall expanded textures")
                  && !ModManager.FileNameMatches(det, "daggerfall expanded texture"),
                "mod lookup: the DET gate's own file name still matches exactly and nothing else");
        }

        /// <summary>
        /// FLC animations (DREAM ships 16 HD Daedra summoning files) resolve from the player's
        /// Movies folder first and fall back to arena2, the same order the shipped build used -
        /// only the folder is now the redirected one, so files in Documents/Movies are seen.
        /// </summary>
        static void TestFlcPathResolution()
        {
            Func<string, bool> exists = p => p == Path.Combine("/m", "AZURA.FLC");

            Check(DaggerfallWorkshop.Game.UserInterface.FLCPlayer.ResolvePath("AZURA.FLC", "/m", "/a", exists)
                      == Path.Combine("/m", "AZURA.FLC"),
                  "flc: a file in the movies folder wins");

            Check(DaggerfallWorkshop.Game.UserInterface.FLCPlayer.ResolvePath("BOETHIAH.FLC", "/m", "/a", exists)
                      == Path.Combine("/a", "BOETHIAH.FLC"),
                  "flc: anything else falls back to arena2");
        }

        /// <summary>
        /// Player.log opens with the build it came from. Twice this week a log kept from an older
        /// build was read as if it were the current one and sent a test down the wrong path.
        /// </summary>
        static void TestBuildStamp()
        {
            string stamp = MobileLog.BuildStamp("DFU Test", "0.1.9", "net.codex64.daggerfall.test",
                                                "abc123", "6000.3.23f1", "1.1.1");
            Check(stamp.StartsWith("[Build] ", StringComparison.Ordinal), "build stamp: starts with the [Build] tag", stamp);
            Check(stamp.Contains("DFU Test") && stamp.Contains("0.1.9")
                  && stamp.Contains("net.codex64.daggerfall.test") && stamp.Contains("abc123")
                  && stamp.Contains("6000.3.23f1") && stamp.Contains("1.1.1"),
                  "build stamp: names product, version, bundle id, build guid, Unity and DFU versions", stamp);
        }

        /// <summary>
        /// Location Loader object type 5 (RMB block), backported from
        /// drcarademono/DFU-LocationLoader@896a574 (branch rmb-object).
        ///
        /// World of Daggerfall 0.4.0 was authored against Location Loader 0.4.x, which places a whole
        /// RMB block as a prefab object (type 5). Our pin a5e7a18 (upstream tip, LL 0.3) rejects type 5
        /// in LocationHelper.ValidateValue, so the object is dropped at parse time and 24 prefab
        /// definitions - all 32,218 wilderness farmsteads and the 382 generic docks/lighthouses - spawn
        /// as empty flattened clearings. These checks pin the parse half of the backport: the type
        /// constant, the optional groundPlane element, and that genuinely unknown types are still
        /// rejected. The spawn half (RMBLayout) needs a running game and cannot be tested here.
        /// </summary>
        static void TestLocationLoaderRmbObjects()
        {
            Check(global::LocationLoader.LocationObject.TypeRMB == 5,
                  "LocationObject.TypeRMB is 5",
                  global::LocationLoader.LocationObject.TypeRMB.ToString());

            // The same public parser the runtime uses: LocationResourceManager reads prefab
            // definitions through LocationHelper.LoadLocationPrefab(XmlDocument).
            const string withGroundPlane =
                "<locationPrefab>" +
                "<height>17</height><width>17</width>" +
                "<object>" +
                "<type>5</type><objectID>0</objectID><name>FARMAA01.RMB</name>" +
                "<posX>-51.2</posX><posY>0.1</posY><posZ>-51.2</posZ>" +
                "<scaleX>1</scaleX><scaleY>1</scaleY><scaleZ>1</scaleZ>" +
                "<groundPlane>true</groundPlane>" +
                "</object>" +
                "</locationPrefab>";

            var prefab = ParseLocationPrefabXml(withGroundPlane);
            Check(prefab != null && prefab.obj.Count == 1,
                  "a type 5 object survives parsing",
                  prefab == null ? "prefab was null" : prefab.obj.Count.ToString());
            if (prefab != null && prefab.obj.Count == 1)
            {
                var obj = prefab.obj[0];
                Check(obj.type == global::LocationLoader.LocationObject.TypeRMB && obj.name == "FARMAA01.RMB",
                      "the parsed type 5 object keeps its type and block name",
                      obj.type + " / " + obj.name);
                Check(obj.groundPlane,
                      "<groundPlane>true</groundPlane> is read into groundPlane");
            }
            else
            {
                Check(false, "the parsed type 5 object keeps its type and block name", "no object parsed");
                Check(false, "<groundPlane>true</groundPlane> is read into groundPlane", "no object parsed");
            }

            // groundPlane is optional. No WoD prefab omits it today (all 24 type-5 prefabs carry an
            // explicit <groundPlane>: the 10 farms true, the 14 docks/lighthouses false), so this is a
            // parser-contract check: a missing element must leave the default false rather than throw
            // the object out.
            const string withoutGroundPlane =
                "<locationPrefab>" +
                "<height>17</height><width>17</width>" +
                "<object>" +
                "<type>5</type><objectID>0</objectID><name>FARMAA01.RMB</name>" +
                "<posX>-51.2</posX><posY>0.1</posY><posZ>-51.2</posZ>" +
                "<scaleX>1</scaleX><scaleY>1</scaleY><scaleZ>1</scaleZ>" +
                "</object>" +
                "</locationPrefab>";

            var bare = ParseLocationPrefabXml(withoutGroundPlane);
            Check(bare != null && bare.obj.Count == 1 && !bare.obj[0].groundPlane,
                  "a type 5 object with no <groundPlane> parses with groundPlane false",
                  bare == null ? "prefab was null" : bare.obj.Count.ToString());

            // Widening ValidateValue must not turn it into a rubber stamp: an unknown type is still
            // dropped, and still warns.
            const string unknownType =
                "<locationPrefab>" +
                "<height>17</height><width>17</width>" +
                "<object>" +
                "<type>99</type><objectID>0</objectID><name>FARMAA01.RMB</name>" +
                "<posX>0</posX><posY>0</posY><posZ>0</posZ>" +
                "<scaleX>1</scaleX><scaleY>1</scaleY><scaleZ>1</scaleZ>" +
                "</object>" +
                "</locationPrefab>";

            var unknown = ParseLocationPrefabXml(unknownType);
            Check(unknown != null && unknown.obj.Count == 0,
                  "an unknown object type is still rejected",
                  unknown == null ? "prefab was null" : unknown.obj.Count.ToString());

            // MOBILE: LocationResourceManager derived the mod's bundle folder with a bare
            // `Substring(17)` at six sites. The input is the first asset name of EVERY enabled mod, so
            // one bundle on the device whose assets are not under "assets/game/mods/" threw
            // ArgumentOutOfRangeException out of Start and cost Location Loader every location for the
            // session - not just that mod's. The prefix arithmetic is now one pure function.
            Check(global::LocationLoader.LocationHelper.ModFolderPrefix(
                      "assets/game/mods/worldofdaggerfall/locations/0/foo.txt") == "assets/game/mods/worldofdaggerfall"
                  && global::LocationLoader.LocationHelper.ModFolderPrefix(
                      "Assets/Game/Mods/WorldOfDaggerfall/Locations/0/foo.txt") == "Assets/Game/Mods/WorldOfDaggerfall",
                  "LL: the mod folder prefix is the path up to the first '/' after assets/game/mods/, in either case",
                  global::LocationLoader.LocationHelper.ModFolderPrefix(
                      "assets/game/mods/worldofdaggerfall/locations/0/foo.txt") ?? "null");
            // The three ways it used to throw or silently mis-parse: too short for the Substring, no
            // '/' after the prefix (IndexOf -1 made the prefix sixteen characters, one short, and
            // matched nothing), and an asset that is not under the prefix at all. All three now return
            // null and the caller skips that mod.
            Check(global::LocationLoader.LocationHelper.ModFolderPrefix("assets/game") == null
                  && global::LocationLoader.LocationHelper.ModFolderPrefix("assets/game/mods/") == null
                  && global::LocationLoader.LocationHelper.ModFolderPrefix("assets/game/mods/loose.txt") == null
                  && global::LocationLoader.LocationHelper.ModFolderPrefix("assets/resources/thing/x.txt") == null
                  && global::LocationLoader.LocationHelper.ModFolderPrefix("") == null
                  && global::LocationLoader.LocationHelper.ModFolderPrefix(null) == null,
                  "LL: a short, prefixless or slashless asset name yields no prefix instead of throwing");
            Check(global::LocationLoader.LocationHelper.modAssetPrefix.Length == 17,
                  "LL: the prefix whose length upstream hard-coded as 17 really is 17 characters",
                  global::LocationLoader.LocationHelper.modAssetPrefix.Length.ToString());
            // And no bare Substring(17) survives in the file the guard replaced them in.
            Check(!System.IO.File.ReadAllText(
                      "Assets/Scripts/Game/Mobile/Ports/LocationLoader/LocationResourceManager.cs")
                  .Contains("Substring(17)"),
                  "LL: LocationResourceManager has no unguarded Substring(17) left");

            // Rebase tripwire for the AddProps guard. RMBLayout.cs is an upstream file and the fix is
            // four lines; an upstream merge that takes theirs restores the discarded GetModelData
            // return, and the symptom is not a compile error but WoD's two dock blocks silently
            // vanishing again behind an unattributed NullReferenceException. Both call sites must read
            // the return value (AddModels' own, which was always there, and AddProps'), and neither may
            // discard it. Comments are stripped so the comment explaining the patch cannot satisfy it.
            string rmbSrc = StripShaderComments(
                System.IO.File.ReadAllText("Assets/Scripts/Utility/RMBLayout.cs"));
            Check(CountOccurrences(rmbSrc, "GetModelData(") == 2
                  && CountOccurrences(rmbSrc, "hasModelData = dfUnity.MeshReader.GetModelData(") == 2
                  && CountOccurrences(rmbSrc, "if (!hasModelData)") == 2,
                  "RMBLayout: both AddModels and AddProps skip a model id ARCH3D.BSA has no record for",
                  CountOccurrences(rmbSrc, "GetModelData(") + " call sites, "
                  + CountOccurrences(rmbSrc, "hasModelData = dfUnity.MeshReader.GetModelData(") + " read, "
                  + CountOccurrences(rmbSrc, "if (!hasModelData)") + " guarded");
        }

        // Location Loader's type-5 RMB blocks get WoD Biomes' subtropical nature swap, but only when
        // the Biomes entry actually started and the climate map can be sampled on the CPU. Both halves
        // of that gate are pure, so they can be pinned here; the swap itself needs a streamed world.
        static void TestBiomesClimateSwapGuard()
        {
            // A script-created texture is readable, so the positive branch is constructible headlessly -
            // without it a bare `return false;` would satisfy the two negatives.
            Texture2D readableMap = new Texture2D(1, 1);
            Check(!global::LocationLoader.BiomesClimateSwap.ShouldSwap(false, null)
                  && !global::LocationLoader.BiomesClimateSwap.ShouldSwap(true, null)
                  && !global::LocationLoader.BiomesClimateSwap.ShouldSwap(false, readableMap)
                  && global::LocationLoader.BiomesClimateSwap.ShouldSwap(true, readableMap),
                  "LL: type-5 nature swap needs Biomes running and a readable map");
            UnityEngine.Object.DestroyImmediate(readableMap);
        }

        /// <summary>
        /// The Real Grass port: the forced configuration, the cost arithmetic that justifies it,
        /// the detail-map fold, the availability gate, and the three engine shader pins. This is
        /// the one ported mod that renders through Unity's own terrain detail path rather than a
        /// shader of its own, so the pins are the whole insurance against grass that draws nothing.
        /// </summary>
        static void TestRealGrassPort()
        {
            // ---- 1. the forced configuration ----
            Check(global::RealGrass.RealGrassPort.DetailResolution == 128
                  && global::RealGrass.RealGrassPort.DetailResolutionPerPatch == 16,
                "RealGrass: detail resolution is 128 at 16 per patch, not upstream's 256 at 8",
                global::RealGrass.RealGrassPort.DetailResolution + "/" + global::RealGrass.RealGrassPort.DetailResolutionPerPatch);
            Check(global::RealGrass.RealGrassPort.ForcedStyle == global::RealGrass.GrassStyle.Classic
                  && (int)global::RealGrass.RealGrassPort.ForcedStyle == 0,
                "RealGrass: style is forced Classic - one grass layer, and the only licence-clean textures");
            Check(global::RealGrass.RealGrassPort.ForcedBillboard,
                "RealGrass: billboards are forced on - GrassBillboard, not FBX prototypes on the Standard shader");
            Check(!global::RealGrass.RealGrassPort.ForcedTerrainStones
                  && !global::RealGrass.RealGrassPort.ForcedWaterPlants
                  && !global::RealGrass.RealGrassPort.ForcedFlyingInsects
                  && !global::RealGrass.RealGrassPort.ForcedTextureOverride,
                "RealGrass: stones, water plants, fireflies and the loose-file texture override are all forced off");
            Check(global::RealGrass.RealGrassPort.DefaultDetailDistance == 40f
                  && global::RealGrass.RealGrassPort.DefaultDetailDensity == 0.6f,
                "RealGrass: the two dials default to 40 m and 0.6 (upstream shipped 120 and 1.0)");
            // The dials are FIELDS, not constants: there is no modsettings.json in the bundle, so
            // nothing reads them from a file today and a later settings hook must be able to move
            // them without touching the port. They must still start on the defaults.
            Check(global::RealGrass.RealGrassPort.DetailDistance == global::RealGrass.RealGrassPort.DefaultDetailDistance
                  && global::RealGrass.RealGrassPort.DetailDensity == global::RealGrass.RealGrassPort.DefaultDetailDensity,
                "RealGrass: DetailDistance / DetailDensity start on their defaults",
                global::RealGrass.RealGrassPort.DetailDistance + " / " + global::RealGrass.RealGrassPort.DetailDensity);
            FieldInfo distanceField = typeof(global::RealGrass.RealGrassPort).GetField("DetailDistance");
            FieldInfo densityField = typeof(global::RealGrass.RealGrassPort).GetField("DetailDensity");
            Check(distanceField != null && distanceField.IsStatic && !distanceField.IsLiteral && !distanceField.IsInitOnly
                  && densityField != null && densityField.IsStatic && !densityField.IsLiteral && !densityField.IsInitOnly,
                "RealGrass: the two dials are settable public static fields, so a settings hook can come later");

            // ---- 2. the cost arithmetic that justifies the configuration ----
            Check(global::RealGrass.RealGrassPort.DetailPatchesPerTerrain(128, 16) == 64,
                "RealGrass: (128, 16) is 64 detail patches per terrain - DFU's own patch density",
                global::RealGrass.RealGrassPort.DetailPatchesPerTerrain(128, 16).ToString());
            Check(global::RealGrass.RealGrassPort.DetailPatchesPerTerrain(256, 8) == 1024,
                "RealGrass: upstream's (256, 8) is 1,024 patches per terrain - the 16x this port drops",
                global::RealGrass.RealGrassPort.DetailPatchesPerTerrain(256, 8).ToString());
            Check(global::RealGrass.RealGrassPort.DetailPatchesPerTerrain(
                      global::RealGrass.RealGrassPort.DetailResolution,
                      global::RealGrass.RealGrassPort.DetailResolutionPerPatch) == 64,
                "RealGrass: the constants and the arithmetic agree");
            Check(global::RealGrass.RealGrassPort.DetailPatchesPerTerrain(0, 16) == 0
                  && global::RealGrass.RealGrassPort.DetailPatchesPerTerrain(128, 0) == 0,
                "RealGrass: the patch count tolerates a zero without dividing by it");
            Check(global::RealGrass.RealGrassPort.LiveTerrainCount(3) == 49
                  && global::RealGrass.RealGrassPort.LiveTerrainCount(0) == 1
                  && global::RealGrass.RealGrassPort.LiveTerrainCount(-1) == 0,
                "RealGrass: TerrainDistance 3 is a 49-terrain ring - the promotion handler's real load");
            Near(global::RealGrass.RealGrassPort.DetailDataMegabytes(128, 1, 49), 0.766f, 0.01f,
                "RealGrass: one 128 layer across 49 terrains is ~0.77 MB of resident detail data");
            Near(global::RealGrass.RealGrassPort.DetailDataMegabytes(256, 5, 49), 15.3f, 0.05f,
                "RealGrass: upstream's five 256 layers across 49 terrains is ~15.3 MB (the research figure)");
            Check(global::RealGrass.RealGrassPort.DetailDataMegabytes(128, 0, 49) == 0f
                  && global::RealGrass.RealGrassPort.DetailDataMegabytes(0, 1, 49) == 0f,
                "RealGrass: the memory figure is 0 with nothing to hold");

            // ---- 3. the scatter mode, and the fold that is written for it ----
            // A detail-map value means one of two things and the two are 16x apart: under
            // CoverageMode 0..255 is how much of the cell's ground the detail covers (area
            // normalised), under InstanceCountMode it is an instance count capped at 16 - below
            // upstream's own thick density, so upstream's values could not be represented at all.
            // DFU builds terrains with a bare `new TerrainData()` and nothing else in this project
            // touches the mode, so the port sets it rather than inheriting an engine default.
            Check(global::RealGrass.RealGrassPort.ForcedScatterMode == DetailScatterMode.CoverageMode,
                "RealGrass: the forced scatter mode is CoverageMode - values are coverage, ceiling 255",
                global::RealGrass.RealGrassPort.ForcedScatterMode.ToString());
            Check(global::RealGrass.RealGrassPort.ForcedScatterMode != DetailScatterMode.InstanceCountMode,
                "RealGrass: it is NOT InstanceCountMode, whose 16-per-cell ceiling is below upstream's thick density");
            // The fold is two steps. Upstream addressed a 256^2 detail map for a 128^2 tilemap; at
            // detail resolution 128 four of its cells become one, so the four sub-cell writes must
            // ACCUMULATE (or the last would erase the first three) and the accumulated sum must
            // then be AVERAGED - because under coverage semantics a cell four times as large
            // carries the mean of the four it replaced, not their sum. Summing was 4x upstream's
            // grass per square metre; the mean is parity, which is what this port is for.
            Check(global::RealGrass.RealGrassPort.AccumulateSubCell(10, 20) == 30,
                "RealGrass: the accumulator adds, it does not overwrite");
            Check(global::RealGrass.RealGrassPort.AccumulateSubCell(250, 20) == 270,
                "RealGrass: the accumulator does NOT clamp - clamping a partial sum would bias the mean down",
                global::RealGrass.RealGrassPort.AccumulateSubCell(250, 20).ToString());
            Check(global::RealGrass.RealGrassPort.AccumulateSubCell(0, -5) == 0
                  && global::RealGrass.RealGrassPort.AccumulateSubCell(-5, 0) == 0,
                "RealGrass: the accumulator never returns a negative");
            // The mean, over the four sub-cells a folded cell stands for.
            Check(global::RealGrass.RealGrassPort.FoldDetailValue(24, 4, 255) == 6
                  && global::RealGrass.RealGrassPort.FoldDetailValue(80, 4, 255) == 20,
                "RealGrass: the fold is the MEAN of the four sub-cells - four thick writes of 6 fold to 6, not 24",
                global::RealGrass.RealGrassPort.FoldDetailValue(24, 4, 255).ToString());
            // Sub-cells upstream never wrote count as zero: a tile whose grass sat in one corner
            // covers a quarter of the folded cell, and the mean says so.
            Check(global::RealGrass.RealGrassPort.FoldDetailValue(20, 4, 255) == 5,
                "RealGrass: one sub-cell of 20 out of four folds to 5 - partial coverage stays partial",
                global::RealGrass.RealGrassPort.FoldDetailValue(20, 4, 255).ToString());
            // Upstream's densities are integers, so the mean is rounded to nearest, halves up -
            // a floor would shave up to three quarters of a unit off every cell, which at a thin
            // density of 2 is most of the grass.
            Check(global::RealGrass.RealGrassPort.FoldDetailValue(6, 4, 255) == 2
                  && global::RealGrass.RealGrassPort.FoldDetailValue(7, 4, 255) == 2
                  && global::RealGrass.RealGrassPort.FoldDetailValue(2, 4, 255) == 1,
                "RealGrass: the mean rounds to the nearest integer, halves up - it does not floor",
                global::RealGrass.RealGrassPort.FoldDetailValue(6, 4, 255) + "/"
                  + global::RealGrass.RealGrassPort.FoldDetailValue(7, 4, 255) + "/"
                  + global::RealGrass.RealGrassPort.FoldDetailValue(2, 4, 255));
            Check(global::RealGrass.RealGrassPort.FoldDetailValue(4000, 4, 255) == 255
                  && global::RealGrass.RealGrassPort.FoldDetailValue(200, 4, 16) == 16,
                "RealGrass: the fold clamps to Unity's detail scatter ceiling, whatever it is");
            Check(global::RealGrass.RealGrassPort.FoldDetailValue(0, 4, 255) == 0
                  && global::RealGrass.RealGrassPort.FoldDetailValue(-5, 4, 255) == 0
                  && global::RealGrass.RealGrassPort.FoldDetailValue(20, 4, 0) == 0
                  && global::RealGrass.RealGrassPort.FoldDetailValue(20, 0, 255) == 20,
                "RealGrass: the fold never returns a negative, respects a zero ceiling, and tolerates a zero divisor");
            // m3 was tautological (MaxDetailValue is initialised from the fallback). What is worth
            // asserting is that the fallback IS the ceiling of the mode the port forces.
            Check(global::RealGrass.RealGrassPort.FallbackMaxDetailValue == 255
                  && global::RealGrass.RealGrassPort.ForcedScatterMode == DetailScatterMode.CoverageMode,
                "RealGrass: the fallback ceiling is 255 because the forced mode is the one whose ceiling is 255",
                global::RealGrass.RealGrassPort.MaxDetailValue.ToString());
            Check(global::RealGrass.DetailMap.UpstreamResolution == 256
                  && global::RealGrass.DetailMap.Fold == 2
                  && global::RealGrass.DetailMap.SubCells == 4,
                "RealGrass: two upstream cells per axis, four per cell - the divisor of the mean",
                global::RealGrass.DetailMap.Fold + "/" + global::RealGrass.DetailMap.SubCells);

            // The layers themselves: allocated once, cleared per promotion, 128 square, and only
            // the grass layer exists in this configuration. RealGrassOptions defaults to Classic
            // with every optional feature off, which is exactly the port's forced configuration.
            var options = new global::RealGrass.RealGrassOptions();
            var density = new global::RealGrass.Density()
            {
                GrassThick = new global::RealGrass.Range<int>(6, 20),
                GrassThin = new global::RealGrass.Range<int>(2, 9),
                WaterPlants = new global::RealGrass.Range<int>(1, 5),
                DesertPlants = new global::RealGrass.Range<int>(4, 8),
            };
            var densityManager = new global::RealGrass.DensityManager(null, options, density);
            int[,] grassCells = densityManager.Grass.Cells;
            Check(grassCells.GetLength(0) == 128 && grassCells.GetLength(1) == 128,
                "RealGrass: a detail layer is a 128x128 int[,], not upstream's 256x256",
                grassCells.GetLength(0) + "x" + grassCells.GetLength(1));
            Check(densityManager.GrassDetails == null && densityManager.GrassAccents == null
                  && densityManager.WaterPlants == null && densityManager.Rocks == null,
                "RealGrass: Classic with no stones or plants allocates ONE layer, the grass one");
            densityManager.InitDetailsLayers();
            Check(ReferenceEquals(grassCells, densityManager.Grass.Cells),
                "RealGrass: InitDetailsLayers clears the cached array, it does not allocate a new one");
            int[,] empty0 = global::RealGrass.DensityManager.Empty;
            // (5, 6) and (5, 7) are two of the four upstream cells of tile (2, 3). Four writes of
            // 5 must come back as 5 (parity with upstream), and two writes of 5 as 3 (half the
            // tile's ground covered, rounded up) - so the pair is written with 5s here.
            densityManager.Grass[5, 6] = 5;
            densityManager.Grass[5, 7] = 5;
            Check(densityManager.Grass.Cells[2, 3] == 10 && densityManager.Grass[5, 6] == 10,
                "RealGrass: upstream's four sub-cell writes land in one cell and accumulate",
                densityManager.Grass.Cells[2, 3].ToString());
            densityManager.FoldDetailLayers();
            Check(densityManager.Grass.Cells[2, 3] == 3,
                "RealGrass: FoldDetailLayers averages the accumulated sum over the four sub-cells (10 -> 3)",
                densityManager.Grass.Cells[2, 3].ToString());
            densityManager.FoldDetailLayers();
            Check(densityManager.Grass.Cells[2, 3] == 3,
                "RealGrass: the fold is idempotent - a second call cannot average an averaged layer",
                densityManager.Grass.Cells[2, 3].ToString());
            densityManager.InitDetailsLayers();
            Check(densityManager.Grass.Cells[2, 3] == 0,
                "RealGrass: the next promotion starts from a cleared layer");
            // Parity ACROSS THE RESOLUTION CHANGE, end to end: all four sub-cells of a tile written
            // with the same thick density come back as that density - not 4x it, which the summing
            // fold gave. It models upstream as writing one value four times, which upstream does
            // not do (it draws RandomThick() four times, independently) - that is deliberate: this
            // pins the FOLD, and the mean of four equal draws is the only case where the fold's
            // output is a fixed number. What it does not pin, and cannot, is what a coverage value
            // of 19 puts on the ground - see RealGrassPort.ForcedScatterMode.
            densityManager.Grass[8, 10] = 19; densityManager.Grass[8, 11] = 19;
            densityManager.Grass[9, 10] = 19; densityManager.Grass[9, 11] = 19;
            densityManager.FoldDetailLayers();
            Check(densityManager.Grass.Cells[4, 5] == 19,
                "RealGrass: four sub-cells at upstream's thick density fold to that same density - resolution parity, not 4x",
                densityManager.Grass.Cells[4, 5].ToString());
            densityManager.InitDetailsLayers();
            Check(ReferenceEquals(empty0, global::RealGrass.DensityManager.Empty),
                "RealGrass: the blanking array is cached, not reallocated per read (StopMod reads it per layer per terrain)");
            int[,] empty = global::RealGrass.DensityManager.Empty;
            Check(empty.GetLength(0) == 128 && empty.GetLength(1) == 128,
                "RealGrass: the blanking array matches the detail store, so SetDetailLayer accepts it",
                empty.GetLength(0) + "x" + empty.GetLength(1));

            // ---- 4. the gate, and inertness ----
            bool gateOk = true;
            for (int mask = 0; mask < 4; mask++)
            {
                bool shaders = (mask & 1) != 0, texture = (mask & 2) != 0;
                if (global::RealGrass.RealGrassPort.Available(shaders, texture) != (shaders && texture))
                    gateOk = false;
            }
            Check(gateOk, "RealGrass: Available needs the detail shaders AND a Classic grass texture (all four cases)");
            Check(!global::RealGrass.RealGrassPort.Installed,
                "RealGrass: Installed is false until Init runs - nothing in this editor session started it");
            MethodInfo init = typeof(global::RealGrass.RealGrassPort).GetMethod("Init");
            Check(init != null && init.IsStatic && init.GetParameters().Length == 1
                  && init.GetParameters()[0].ParameterType == typeof(InitParams),
                "RealGrass: Init(InitParams) is the entry point MobilePortedMods calls");
            Check(init != null && Attribute.GetCustomAttributes(init, typeof(Invoke), false).Length == 0,
                "RealGrass: no [Invoke] survives - the launcher switch is the only way in");
            Check(global::RealGrass.RealGrassPort.GrassTextureNames.Length == 2
                  && global::RealGrass.RealGrassPort.GrassTextureNames[0] == "BrownGrass_tex"
                  && global::RealGrass.RealGrassPort.GrassTextureNames[1] == "GreenGrass_tex",
                "RealGrass: the gate asks for exactly the two Classic textures the bundle ships");

            // ---- 5. the sources ----
            const string portDir = "Assets/Scripts/Game/Mobile/Ports/RealGrass/";
            string[] portFiles = { "RealGrass.cs", "DensityManager.cs", "DetailPrototypesManager.cs", "Range.cs" };
            foreach (string file in portFiles)
            {
                Check(File.Exists(portDir + file), "RealGrass: " + portDir + file + " exists");
                if (!File.Exists(portDir + file)) continue;
                string raw = File.ReadAllText(portDir + file);
                Check(raw.Contains("License:         MIT License"),
                    "RealGrass: " + file + " keeps the upstream MIT licence header");
                Check(raw.Contains("Copyright (c) 2016-2019 Uncanny_Valley, TheLacus"),
                    "RealGrass: " + file + " names the upstream copyright holders");
                // Comments stripped: the headers and the MOBILE notes name what was removed on
                // purpose, and an "it is gone" check a comment can satisfy is worthless.
                string code = StripShaderComments(raw);
                Check(!code.Contains("[Invoke("),
                    "RealGrass: " + file + " carries no [Invoke] attribute");
                Check(!code.Contains("GetSettings(") && !code.Contains("LoadSettings()")
                      && !code.Contains("LoadSettingsCallback"),
                    "RealGrass: " + file + " does not read mod settings - there is no modsettings.json in the bundle");
                Check(!code.Contains("RegisterCommands"),
                    "RealGrass: " + file + " has no console commands (External/RealGrassConsoleCommands.cs is not ported)");
            }
            Check(!File.Exists(portDir + "RealGrassConsoleCommands.cs")
                  && !Directory.Exists(portDir + "External"),
                "RealGrass: the console-command file is not in the port at all");

            string densitySrc = StripShaderComments(File.ReadAllText(portDir + "DensityManager.cs"));
            Check(densitySrc.Contains("const int size = RealGrassPort.DetailResolution")
                  && !densitySrc.Contains("const int size = 256") && !densitySrc.Contains("new int[256, 256]"),
                "RealGrass: no 256-square layer allocation survives in DensityManager");
            Check(densitySrc.Contains("System.Array.Clear(Cells, 0, Cells.Length)"),
                "RealGrass: the layers are cleared with Array.Clear, not reallocated per promotion");
            Check(CountOccurrences(densitySrc, "new DetailMap()") == 5,
                "RealGrass: the five layers are allocated once, in the constructor",
                CountOccurrences(densitySrc, "new DetailMap()") + " allocations");
            Check(densitySrc.Contains("RealGrassPort.AccumulateSubCell(Cells[fy, fx], value)")
                  && !densitySrc.Contains("RealGrassPort.FoldDetailValue(Cells[fy, fx]"),
                "RealGrass: the indexer only accumulates - the averaging is one pass, not one per write");
            Check(densitySrc.Contains("RealGrassPort.FoldDetailValue(sum, subCells, max)")
                  && densitySrc.Contains("Grass.FoldToMean()"),
                "RealGrass: FoldToMean is where the mean is taken, and FoldDetailLayers walks the layers");
            Check(densitySrc.Contains("emptyMap ?? (emptyMap = EmptyMap())"),
                "RealGrass: the blanking array is allocated once and cached, not per read");

            string grassSrc = StripShaderComments(File.ReadAllText(portDir + "RealGrass.cs"));
            Check(grassSrc.Contains("SetDetailResolution(RealGrassPort.DetailResolution, RealGrassPort.DetailResolutionPerPatch)")
                  && !grassSrc.Contains("SetDetailResolution(256, 8)"),
                "RealGrass: the promotion sets (DetailResolution, DetailResolutionPerPatch), never (256, 8)");
            Check(grassSrc.Contains("terrainData.detailWidth != RealGrassPort.DetailResolution"),
                "RealGrass: SetDetailResolution is skipped when the store is already that shape (it reallocates)");
            Check(grassSrc.Contains("terrainData.SetDetailScatterMode(RealGrassPort.ForcedScatterMode)")
                  && grassSrc.Contains("terrainData.detailScatterMode != RealGrassPort.ForcedScatterMode"),
                "RealGrass: the promotion sets the forced scatter mode explicitly, and only when it differs");
            // Order matters twice over: switching scatter mode ERASES existing detail placements,
            // so it has to precede the first SetDetailLayer of the promotion; and the mean can only
            // be taken once the density pass has finished accumulating, which is also before it.
            int modeAt = grassSrc.IndexOf("SetDetailScatterMode(", StringComparison.Ordinal);
            int foldAt = grassSrc.IndexOf("densityManager.FoldDetailLayers()", StringComparison.Ordinal);
            int layerAt = grassSrc.IndexOf("SetDetailLayer(", StringComparison.Ordinal);
            Check(modeAt >= 0 && foldAt > modeAt && layerAt > foldAt,
                "RealGrass: scatter mode, then the fold to the mean, then SetDetailLayer - in that order",
                modeAt + " < " + foldAt + " < " + layerAt);
            Check(grassSrc.Contains(", scatter {4}/{5})")
                  && grassSrc.Contains("terrainData.detailScatterMode, RealGrassPort.MaxDetailValue"),
                "RealGrass: the memory line writes the scatter mode and the ceiling that was read");
            Check(grassSrc.Contains("[RealGrass] details on ") && grassSrc.Contains("[RealGrass] detail data ~")
                  && grassSrc.Contains("[RealGrass] not available: ")
                  && grassSrc.Contains("[RealGrass] terrain details failed: "),
                "RealGrass: the four log lines a Player.log is read for are all present");
            Check(global::RealGrass.RealGrass.CounterInterval == 25,
                "RealGrass: the counter line is written every 25th promotion",
                global::RealGrass.RealGrass.CounterInterval.ToString());

            // The Desert branch. Upstream asks mod.GetAsset<Texture2D>("DesertGrass_tex") there, an
            // asset that exists in NO version of this mod (the desert art is the VMblast
            // DesertGrass.psd, which this port does not ship), so upstream drew no desert grass in
            // Billboard style and logged a failed load on every climate change.
            string protoSrc = StripShaderComments(File.ReadAllText(portDir + "DetailPrototypesManager.cs"));
            Check(protoSrc.Contains("SetGrass(brownGrass, brownGrass)") && !protoSrc.Contains("SetGrass(desertGrass, desertGrass)"),
                "RealGrass: Desert draws BrownGrass_tex - upstream's DesertGrass_tex does not exist");
            // Every ResetColor of the water-plants layer must be inside a WaterPlants test. Asked
            // by proximity rather than by an exact snippet so that reformatting cannot pass it.
            const string resetCall = "ResetColor(DetailPrototypes[WaterPlants])";
            bool resetsGuarded = CountOccurrences(protoSrc, resetCall) > 0;
            for (int at = protoSrc.IndexOf(resetCall, StringComparison.Ordinal); at >= 0;
                 at = protoSrc.IndexOf(resetCall, at + resetCall.Length, StringComparison.Ordinal))
            {
                int from = Math.Max(0, at - 120);
                if (!protoSrc.Substring(from, at - from).Contains("options.WaterPlants"))
                    resetsGuarded = false;
            }
            Check(resetsGuarded,
                "RealGrass: the water-plants colour reset is behind its option (index 0 is the GRASS layer)");

            // ---- 6. the three engine shader pins ----
            // Real Grass has no shaders of its own: it renders through Unity's stock terrain detail
            // path. Those shaders are in the Editor's unity_builtin_extra but absent from
            // libiPhone-lib.a and from the iOS player's default resources, nothing in the project
            // references them (DFU builds every Terrain at runtime, no scene holds one), so an
            // IL2CPP build is free to strip them - and stripped, the grass renders as NOTHING, with
            // no error at all. Hence: named in the port, in the build setup, and in GraphicsSettings.
            string[] detailShaders = global::RealGrass.RealGrassPort.ShaderNames;
            Check(detailShaders.Length == 3
                  && detailShaders[0] == "Hidden/TerrainEngine/Details/Vertexlit"
                  && detailShaders[1] == "Hidden/TerrainEngine/Details/WavingDoublePass"
                  && detailShaders[2] == "Hidden/TerrainEngine/Details/BillboardWavingDoublePass",
                "RealGrass: the three shader names are Unity 6000.3's own (verified against unity_builtin_extra)",
                string.Join(", ", detailShaders));

            string buildSetup = File.ReadAllText("Assets/Editor/MobileBuildSetup.cs");
            var pinned = new List<Shader>();
            var settingsObjects = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
            if (settingsObjects != null && settingsObjects.Length > 0)
            {
                SerializedProperty list = new SerializedObject(settingsObjects[0]).FindProperty("m_AlwaysIncludedShaders");
                if (list != null)
                    for (int i = 0; i < list.arraySize; i++)
                        pinned.Add(list.GetArrayElementAtIndex(i).objectReferenceValue as Shader);
            }
            Check(pinned.Count > 0, "RealGrass: GraphicsSettings' always-included list is readable", pinned.Count + " entries");

            foreach (string name in detailShaders)
            {
                Shader shader = Shader.Find(name);
                Check(shader != null, "RealGrass: " + name + " resolves by name in this Unity version");
                if (shader != null)
                    Check(shader.isSupported, "RealGrass: " + name + " compiles for this editor's graphics API");
                Check(buildSetup.Contains(name),
                    "RealGrass: " + name + " is in MobileBuildSetup's EnsureAlwaysIncludedShaders list");
                // The pin itself, by OBJECT: a built-in shader has no project GUID, so the asset
                // text names it only by a fileID into unity_builtin_extra - a number no test should
                // hard-code. Comparing the loaded shader is the same question, asked robustly.
                Check(shader != null && pinned.Contains(shader),
                    "RealGrass: ApplyIOSSettings pinned " + name + " into GraphicsSettings' always-included list");
            }
            // MobileShaders is deliberately NOT extended: these are engine shaders, so there is no
            // captured copy to prefer and no mod bundle that could shadow them by name.
            Check(!MobileShaders.Names.Contains(detailShaders[0]),
                "RealGrass: the engine detail shaders are not in MobileShaders' captured list (they are not mod shaders)");
        }

        static global::LocationLoader.LocationPrefab ParseLocationPrefabXml(string xml)
        {
            var doc = new System.Xml.XmlDocument();
            doc.LoadXml(xml);
            return global::LocationLoader.LocationHelper.LoadLocationPrefab(doc);
        }

        static void Check(bool condition, string name, string detail = "")
        {
            if (condition)
            {
                passed++;
                log.AppendLine("  PASS  " + name);
            }
            else
            {
                failed++;
                log.AppendLine("  FAIL  " + name + (string.IsNullOrEmpty(detail) ? "" : "   -> " + detail));
            }
        }

        static void Near(float actual, float expected, float tol, string name)
        {
            bool ok = Mathf.Abs(actual - expected) <= tol;
            Check(ok, name, string.Format("expected ~{0}, got {1}", expected, actual));
        }

        #endregion

        #region Tests

        /// <summary>
        /// The input-mode table. Auto must reproduce the shipped detection behaviour exactly;
        /// the three overrides must ignore detection in the directions that matter - a phantom
        /// joystick (the iOS 26 Simulator lists one) must not be able to hide the touch HUD in
        /// Touch mode, and Controller mode must work with nothing listed at all.
        /// </summary>
        static void TestInputModeResolution()
        {
            EffectiveInput e = MobileInput.ResolveInput(MobileInputMode.Auto, false, false, false);
            Check(e.TouchHud && !e.Controller && !e.Keyboard && !e.Mouse, "auto: nothing physical -> touch HUD");

            e = MobileInput.ResolveInput(MobileInputMode.Auto, true, false, false);
            Check(!e.TouchHud && e.Controller, "auto: pad detected -> pad drives, touch stands down");

            e = MobileInput.ResolveInput(MobileInputMode.Auto, false, true, true);
            Check(!e.TouchHud && e.Keyboard && e.Mouse && !e.Controller, "auto: keyboard + pointer detected -> both drive");

            e = MobileInput.ResolveInput(MobileInputMode.Touch, true, true, true);
            Check(e.TouchHud && !e.Controller && !e.Keyboard && !e.Mouse, "touch: phantom pad, keyboard and pointer all ignored");

            e = MobileInput.ResolveInput(MobileInputMode.KeyboardMouse, true, false, false);
            Check(!e.TouchHud && e.Keyboard && !e.Mouse && !e.Controller,
                  "kb+mouse: keyboard counts without a keystroke, pad ignored, no pointer until one connects");

            e = MobileInput.ResolveInput(MobileInputMode.KeyboardMouse, false, false, true);
            Check(e.Mouse && e.Keyboard && !e.TouchHud, "kb+mouse: connected pointer drives look and cursor");

            e = MobileInput.ResolveInput(MobileInputMode.Controller, false, true, true);
            Check(!e.TouchHud && e.Controller && !e.Keyboard && !e.Mouse,
                  "controller: pad path on with nothing listed; keyboard and pointer stand down");
        }

        /// <summary>
        /// WeaponSwingMode: touch swipes need hold-and-drag (0); everyone else keeps what they
        /// chose in the launcher - which is where "click to attack" was being lost. With a
        /// classic window open the player's value must be the one in memory, because that is
        /// the only time settings.ini gets written.
        /// </summary>
        static void TestSwingModeDecision()
        {
            Check(MobileInput.ResolveSwingMode(1, true, false, false, false) == 0, "touch play imposes hold-and-drag");
            Check(MobileInput.ResolveSwingMode(1, false, false, false, false) == 1, "click-to-attack off: mouse/pad keep the launcher's click mode");
            Check(MobileInput.ResolveSwingMode(1, true, true, false, false) == 1, "window open -> player's own value, so saves keep it");
            Check(MobileInput.ResolveSwingMode(0, false, false, false, false) == 0, "vanilla stays vanilla");

            // The port's own switches.
            Check(MobileInput.ResolveSwingMode(0, false, false, true, false) == 1,
                  "click-to-attack on: pointer/pad get click mode even when the launcher says vanilla");
            Check(MobileInput.ResolveSwingMode(2, false, false, false, false) == 2,
                  "click-to-attack off: pointer/pad keep the launcher's choice");
            Check(MobileInput.ResolveSwingMode(1, true, false, true, false) == 0,
                  "touch without tap-to-attack still swipes");
            Check(MobileInput.ResolveSwingMode(0, true, false, true, true) == 1,
                  "tap-to-attack on: touch runs click mode");
            Check(MobileInput.ResolveSwingMode(0, true, true, true, true) == 0,
                  "window open -> launcher value regardless of switches");
        }

        /// <summary>
        /// Classic docked mode hides the MENU toggle and puts the travel map in its slot.
        /// TWO rules have to agree for that to work, and either one alone breaks the very
        /// button the change exists to expose: the drawer must hold its panel open in
        /// classic mode (MAP lives inside that panel, and a button in a deactivated
        /// container is dead however visible the layout thinks it is), and MenuToggle's
        /// never-hide exemption must lift there (it exists so the drawer is always
        /// reachable - pointless once the drawer never closes).
        /// </summary>
        static void TestClassicDrawerRules()
        {
            Check(!MobileButtonDrawer.PanelShown(false, false, false),
                  "fullscreen: a closed drawer stays closed");
            Check(MobileButtonDrawer.PanelShown(true, false, false),
                  "fullscreen: MENU opens the drawer");
            Check(MobileButtonDrawer.PanelShown(false, true, false),
                  "the layout editor forces the drawer open so its icons can be dragged");
            // The closed case is the one that matters: closeOnSelection and the auto-close
            // timer both drive open back to false, and in classic mode neither may take
            // the travel map off the screen.
            Check(MobileButtonDrawer.PanelShown(false, false, true),
                  "classic: a closed drawer still shows, so MAP survives a selection and the auto-close timer");
            Check(MobileButtonDrawer.PanelShown(true, false, true)
                  && MobileButtonDrawer.PanelShown(false, true, true),
                  "classic: open or forced open, the panel shows either way");

            Check(MobileHudLayout.ExemptFromHiding("MenuToggle", false),
                  "fullscreen: MENU can never hide - it is the only way into the drawer");
            Check(!MobileHudLayout.ExemptFromHiding("MenuToggle", true),
                  "classic: MENU may hide, the drawer it opens is already open");
            Check(!MobileHudLayout.ExemptFromHiding("Map", false)
                  && !MobileHudLayout.ExemptFromHiding("Map", true),
                  "no element but MENU is ever exempt from hiding");
        }

        /// <summary>
        /// No two cells of the bottom HUD band may intersect.
        ///
        /// This is the test the row needed and did not have. COMBAT was authored at 6.52
        /// against a row that ended at 5.95; MODE was added at 6.20 later, nobody re-checked
        /// the neighbour, and the two buttons overlapped by 0.18in for months - a tap in the
        /// shared strip hit whichever happened to be on top. The numbers now live in one
        /// table (MobileHudBuilder.BottomRow) precisely so they can be checked as a set, and
        /// this runs that check for whoever adds the next button.
        ///
        /// Geometry: every cell anchors bottom-right with a bottom-right pivot, so marginX is
        /// the distance from the right screen edge to its RIGHT edge and it extends widthIn
        /// further left. The table is ordered right to left, so each cell must start at or
        /// beyond the previous one's left extent.
        /// </summary>
        static void TestBottomRowSpacing()
        {
            var row = MobileHudBuilder.BottomRow;

            Check(row.Length >= 8, "the bottom row table still describes the whole band",
                  "length=" + row.Length);

            bool ordered = true, clear = true;
            string worst = "";
            float worstOverlap = 0f;

            for (int i = 1; i < row.Length; i++)
            {
                var left = row[i];
                var right = row[i - 1];

                if (left.marginX <= right.marginX)
                    ordered = false;

                float overlap = right.LeftExtent - left.marginX;
                if (overlap > 0.0005f)
                {
                    clear = false;
                    if (overlap > worstOverlap)
                    {
                        worstOverlap = overlap;
                        worst = right.name + "/" + left.name;
                    }
                }
            }

            Check(ordered, "the bottom row table runs right to left with no repeated slot");
            Check(clear, "no two bottom-row buttons overlap",
                  worst + " overlap " + worstOverlap.ToString("0.###") + "in");

            // The contiguous action cells - WEAPON through MODE - keep the documented pitch.
            // MENU sits further out with its own gap, and COMBAT is placed by its left edge
            // because it is wider than a cell, so both are checked by clearance alone above.
            bool stepped = true;
            for (int i = 1; i < row.Length; i++)
            {
                if (row[i - 1].name == "MenuToggle" || row[i].name == "Combat")
                    continue;
                if (Mathf.Abs((row[i].marginX - row[i - 1].marginX)
                              - MobileHudBuilder.BottomRowStepIn) > 0.0005f)
                    stepped = false;
            }
            Check(stepped, "the action cells step the documented 0.57in");

            // The regression itself: COMBAT clears MODE by the row's own gap, no more and no
            // less. Placed here rather than left implicit so a future re-space of the row has
            // to state its intent about this pair.
            var mode = System.Array.Find(row, c => c.name == "Mode");
            var combat = System.Array.Find(row, c => c.name == "Combat");
            Near(combat.marginX - mode.LeftExtent, MobileHudBuilder.BottomRowGapIn, 0.0005f,
                 "COMBAT clears MODE by the row gap");

            // And the band as a whole still fits the screen it is authored for. At hudScale 1
            // the leftmost extent is measured from the right edge, so this is the minimum
            // screen width the defaults need: an iPad Pro 11in is 9.05in wide in landscape and
            // the narrowest 264ppi iPad is 8.18in, so 8in is the budget to defend. Anything
            // narrower is a phone, where the player scales the whole HUD down and the band
            // scales with it.
            float leftmost = 0f;
            for (int i = 0; i < row.Length; i++)
                leftmost = Mathf.Max(leftmost, row[i].LeftExtent);

            Check(leftmost <= 8.0f, "the bottom row still fits the narrowest supported iPad",
                  "leftmost extent " + leftmost.ToString("0.##") + "in from the right edge");
        }

        /// <summary>
        /// A saved position must not shadow a default that has since moved.
        ///
        /// The failure this pins is not hypothetical: the COMBAT/MODE overlap above could be
        /// fixed in the builder and still be on the player's screen afterwards, because a
        /// PlayerPrefs position override wins over the built-in default unconditionally and
        /// forever. Stamping the default a drag was made against turns that into a decision
        /// the code can make per element.
        /// </summary>
        static void TestLayoutOverrideStaleness()
        {
            Vector2 authored = new Vector2(6.77f, 0.10f);

            Check(MobileHudLayout.OverrideSurvives(true, authored, authored),
                  "an override made against the current default is kept");

            // The case the whole mechanism exists for: COMBAT's default moved 6.52 -> 6.77.
            Check(!MobileHudLayout.OverrideSurvives(true, new Vector2(6.52f, 0.10f), authored),
                  "an override made against a default that has since moved is discarded");

            Check(!MobileHudLayout.OverrideSurvives(true, new Vector2(6.77f, 0.35f), authored),
                  "a moved default is caught on the y axis too");

            // Only the elements whose defaults actually moved are invalidated - a blunt
            // schema bump would throw away the drags the player made on everything else.
            Vector2 untouched = new Vector2(0.20f, 3.87f);
            Check(MobileHudLayout.OverrideSurvives(true, untouched, untouched),
                  "an unrelated element's override survives a change elsewhere in the layout");

            // Migration: overrides saved before the stamp existed carry no default to compare
            // against, and the release that adds the stamp is itself a layout change, so they
            // go. Positions only - scale and hidden are never stamped or pruned.
            Check(!MobileHudLayout.OverrideSurvives(false, Vector2.zero, authored),
                  "a pre-stamp override is discarded, because its default is unknowable");
            Check(!MobileHudLayout.OverrideSurvives(false, authored, authored),
                  "no stamp means discard even if the payload happens to match");

            // Floats make the round trip through PlayerPrefs; that must not read as a move.
            Check(MobileHudLayout.OverrideSurvives(true, new Vector2(6.7700005f, 0.1000001f), authored),
                  "float noise in a stamp is not a moved default");
        }

        /// <summary>A queued click must produce exactly one Down frame and one Up frame.</summary>
        static void TestButtonEdges()
        {
            MobileInput.ResetButtons();
            MobileInput.QueueClick(0, 3);

            int downs = 0, ups = 0, heldFrames = 0;
            for (int i = 0; i < 8; i++)
            {
                MobileInput.TickButtons();
                if (MobileInput.GetMouseButtonDown(0)) downs++;
                if (MobileInput.GetMouseButtonUp(0)) ups++;
                if (MobileInput.GetMouseButton(0)) heldFrames++;
            }

            Check(downs == 1, "click yields exactly one Down", "downs=" + downs);
            Check(ups == 1, "click yields exactly one Up", "ups=" + ups);
            Check(heldFrames == 3, "click held for 3 frames", "held=" + heldFrames);
            MobileInput.ResetButtons();
        }

        /// <summary>Long-press latch stays down until explicitly released.</summary>
        static void TestLatchedButton()
        {
            MobileInput.ResetButtons();
            MobileInput.SetLatched(0, true);

            for (int i = 0; i < 4; i++)
                MobileInput.TickButtons();

            Check(MobileInput.GetMouseButton(0), "latched button stays held");
            Check(!MobileInput.GetMouseButtonDown(0), "latched button does not re-fire Down");

            MobileInput.SetLatched(0, false);
            MobileInput.TickButtons();
            Check(MobileInput.GetMouseButtonUp(0), "releasing latch yields Up");
            MobileInput.ResetButtons();
        }

        /// <summary>
        /// The back channel matters: every classic window closes on GetBackButtonUp(),
        /// so a press with no release edge would never close anything.
        /// </summary>
        static void TestBackButtonEdges()
        {
            MobileInput.ResetButtons();
            MobileInput.QueueBack(3);

            int downs = 0, ups = 0;
            for (int i = 0; i < 8; i++)
            {
                MobileInput.TickButtons();
                if (MobileInput.GetBackButtonDown()) downs++;
                if (MobileInput.GetBackButtonUp()) ups++;
            }

            Check(downs == 1, "back yields exactly one Down", "downs=" + downs);
            Check(ups == 1, "back yields exactly one Up (windows close on Up)", "ups=" + ups);
            MobileInput.ResetButtons();
        }

        /// <summary>BaseScreenComponent only reads the sign, so emit one step per frame.</summary>
        static void TestScrollOneStepPerFrame()
        {
            MobileInput.ResetButtons();
            MobileInput.QueueScroll(3f);

            int steps = 0;
            for (int i = 0; i < 6; i++)
            {
                MobileInput.TickButtons();
                if (!Mathf.Approximately(MobileInput.MouseScroll, 0f))
                {
                    steps++;
                    Check(Mathf.Abs(MobileInput.MouseScroll) <= 1.0001f,
                          "scroll step magnitude <= 1 (frame " + i + ")");
                }
            }
            Check(steps == 3, "3 queued ticks emit 3 frames of scroll", "steps=" + steps);
            MobileInput.ResetButtons();
        }

        /// <summary>
        /// Critical integration rule: with a gamepad connected the touch cursor must stand
        /// down so DFU's own controller cursor keeps the pointer.
        /// </summary>
        static void TestControllerForcesCursorOff()
        {
            bool savedController = MobileInput.ControllerActive;

            MobileInput.ControllerActive = false;
            MobileInput.VirtualCursorActive = true;
            Check(MobileInput.VirtualCursorActive, "cursor active with no gamepad");

            MobileInput.ControllerActive = true;
            Check(!MobileInput.VirtualCursorActive, "gamepad forces virtual cursor OFF");

            MobileInput.ControllerActive = false;
            Check(MobileInput.VirtualCursorActive, "cursor restored when gamepad disconnects");

            MobileInput.VirtualCursorActive = false;
            MobileInput.ControllerActive = savedController;
        }

        /// <summary>
        /// A hardware keyboard must stand the touch layer down exactly like a gamepad,
        /// otherwise the classic UI gets the virtual cursor while the player is typing.
        /// </summary>
        static void TestKeyboardForcesCursorOff()
        {
            bool savedKeyboard = MobileInput.KeyboardActive;
            bool savedController = MobileInput.ControllerActive;

            MobileInput.ControllerActive = false;
            MobileInput.KeyboardActive = false;
            MobileInput.VirtualCursorActive = true;
            Check(MobileInput.VirtualCursorActive, "cursor active with no physical input");

            MobileInput.KeyboardActive = true;
            Check(!MobileInput.VirtualCursorActive, "keyboard forces virtual cursor OFF");
            Check(MobileInput.PhysicalInputActive, "PhysicalInputActive true for keyboard");

            MobileInput.KeyboardActive = false;
            Check(MobileInput.VirtualCursorActive, "cursor restored when keyboard idles");

            MobileInput.VirtualCursorActive = false;
            MobileInput.KeyboardActive = savedKeyboard;
            MobileInput.ControllerActive = savedController;
        }

        /// <summary>
        /// A real pointer DRIVES the virtual cursor rather than standing it down: hover feeds
        /// the position and GCMouse buttons feed the clicks, so the classic UI never consults
        /// Unity's phantom-held Input.GetMouseButton(0). That must hold even with a hardware
        /// keyboard active (Magic Keyboard = keyboard + trackpad together), where the keyboard
        /// alone would have switched the cursor off. A gamepad still wins outright.
        /// </summary>
        static void TestPointerKeepsCursorOverKeyboard()
        {
            bool savedKeyboard = MobileInput.KeyboardActive;
            bool savedController = MobileInput.ControllerActive;
            bool savedMouse = MobileInput.MouseActive;

            MobileInput.ControllerActive = false;
            MobileInput.KeyboardActive = false;
            MobileInput.MouseActive = true;
            MobileInput.VirtualCursorActive = true;
            Check(MobileInput.VirtualCursorActive, "pointer alone keeps the virtual cursor");
            Check(MobileInput.PhysicalInputActive, "PhysicalInputActive true for pointer");

            MobileInput.KeyboardActive = true;
            Check(MobileInput.VirtualCursorActive, "pointer + keyboard keeps the virtual cursor");

            MobileInput.MouseActive = false;
            Check(!MobileInput.VirtualCursorActive, "keyboard alone still forces cursor OFF");

            MobileInput.MouseActive = true;
            MobileInput.ControllerActive = true;
            Check(!MobileInput.VirtualCursorActive, "gamepad beats pointer for the cursor");

            MobileInput.VirtualCursorActive = false;
            MobileInput.KeyboardActive = savedKeyboard;
            MobileInput.ControllerActive = savedController;
            MobileInput.MouseActive = savedMouse;
        }

        /// <summary>
        /// GCMouse reports raw counts; Unity's "Mouse X/Y" axes are counts x 0.1 (the project's
        /// InputManager.asset sensitivity). Matching that keeps DFU's own mouse-sensitivity
        /// setting meaning the same thing it does on PC. Y is positive-up in both systems, so
        /// the flip is OFF by default and only exists as a device-verification escape hatch.
        /// </summary>
        static void TestPointerDeltaScale()
        {
            Vector2 d = MobilePointer.ScaleDelta(new Vector2(40f, -20f), 0.1f, false);
            Near(d.x, 4f, 0.0001f, "delta X scaled by 0.1");
            Near(d.y, -2f, 0.0001f, "delta Y scaled by 0.1, sign kept");

            Vector2 f = MobilePointer.ScaleDelta(new Vector2(40f, -20f), 0.1f, true);
            Near(f.y, 2f, 0.0001f, "flipY inverts Y only");
            Near(f.x, 4f, 0.0001f, "flipY leaves X alone");

            Vector2 z = MobilePointer.ScaleDelta(Vector2.zero, 0.1f, true);
            Check(z == Vector2.zero, "zero delta stays zero");
        }

        /// <summary>
        /// The pointer is locked exactly when PlayerMouseLook would have locked it on PC:
        /// a pointer is in use, no classic window is open, the game is not paused, and the
        /// engine has hidden its cursor. Any one of those failing releases the pointer, so
        /// menus, the pause screen and the ActivateCursor toggle all get the arrow back.
        /// </summary>
        static void TestPointerLockDecision()
        {
            Check(MobilePointer.ShouldLock(true, false, false, false), "locks in plain gameplay");
            Check(!MobilePointer.ShouldLock(false, false, false, false), "no pointer -> no lock");
            Check(!MobilePointer.ShouldLock(true, true, false, false), "menu open -> unlocked");
            Check(!MobilePointer.ShouldLock(true, false, true, false), "paused -> unlocked");
            Check(!MobilePointer.ShouldLock(true, false, false, true), "engine cursor visible -> unlocked");
        }

        /// <summary>
        /// Regression for the first device build: the cursor-stage pump ran before the
        /// gameplay pump every frame and drained the deltas in live play, so the pointer
        /// locked and then never moved. Draining is legal in exactly one state - paused with
        /// no classic window open.
        /// </summary>
        static void TestPointerDrainDecision()
        {
            Check(!MobilePointer.ShouldDrainInCursorStage(false, false), "live play -> never drain (the camera owns the deltas)");
            Check(MobilePointer.ShouldDrainInCursorStage(false, true), "paused, no window -> drain");
            Check(!MobilePointer.ShouldDrainInCursorStage(true, true), "menu open -> menu pump owns it, no drain here");
            Check(!MobilePointer.ShouldDrainInCursorStage(true, false), "menu open unpaused -> no drain here");
        }

        /// <summary>
        /// Hover arrives normalised (0..1, bottom-left origin) so the plugin never has to
        /// agree with Unity about contentScaleFactor. Corners must land on the pixel edges.
        /// </summary>
        static void TestPointerHoverToScreen()
        {
            Vector2 c = MobilePointer.HoverToScreen(0.5f, 0.5f, 2000, 1000);
            Near(c.x, 1000f, 0.001f, "hover centre X");
            Near(c.y, 500f, 0.001f, "hover centre Y");

            Vector2 tl = MobilePointer.HoverToScreen(0f, 1f, 2000, 1000);
            Near(tl.x, 0f, 0.001f, "hover left edge");
            Near(tl.y, 1000f, 0.001f, "hover top edge (bottom-left origin)");

            Vector2 over = MobilePointer.HoverToScreen(1.5f, -0.5f, 2000, 1000);
            Near(over.x, 2000f, 0.001f, "hover clamps X into the screen");
            Near(over.y, 0f, 0.001f, "hover clamps Y into the screen");
        }

        /// <summary>
        /// Scroll wheel/trackpad values have no defined range, so the accumulator emits at
        /// most one classic-UI step per frame once it crosses the threshold, and carries
        /// nothing over - a hard flick must not keep a list scrolling for seconds.
        /// </summary>
        static void TestPointerScrollTicks()
        {
            float acc = 0.2f;
            Check(MobilePointer.ScrollTicks(ref acc, 0.5f) == 0, "below threshold -> no tick");
            Near(acc, 0.2f, 0.0001f, "sub-threshold scroll is kept");

            acc = 0.7f;
            Check(MobilePointer.ScrollTicks(ref acc, 0.5f) == 1, "above threshold -> one tick up");
            Near(acc, 0f, 0.0001f, "tick consumes the accumulator");

            acc = -30f;
            Check(MobilePointer.ScrollTicks(ref acc, 0.5f) == -1, "large flick -> still exactly one tick down");
            Near(acc, 0f, 0.0001f, "large flick does not carry over");
        }

        /// <summary>
        /// A touch counts as a FINGER (and so hands control back to the touch layer) only if it
        /// is not an indirect device and no pointer button is down. iPadOS delivers pointer
        /// clicks as touches - without this rule every click would flip the touch HUD back on.
        /// </summary>
        static void TestPointerFingerRule()
        {
            Check(MobilePointer.IsFingerTouch(TouchType.Direct, false, float.MaxValue, 0f), "direct touch, no button -> finger");
            Check(MobilePointer.IsFingerTouch(TouchType.Stylus, false, float.MaxValue, 0f), "pencil counts as a finger");
            Check(!MobilePointer.IsFingerTouch(TouchType.Indirect, false, float.MaxValue, 0f), "indirect touch -> not a finger");
            Check(!MobilePointer.IsFingerTouch(TouchType.Direct, true, float.MaxValue, 0f), "touch while a pointer button is held -> pointer click, not a finger");
        }

        /// <summary>
        /// Fallback layout when KeyBinds.txt has no mouse bindings to capture: Daggerfall's
        /// own defaults, left = activate, right = swing. Anything else is unbound.
        /// </summary>
        static void TestPointerDefaultActions()
        {
            InputManager.Actions a;
            Check(MobilePointer.TryDefaultAction(0, out a) && a == InputManager.Actions.ActivateCenterObject, "left button -> ActivateCenterObject");
            Check(MobilePointer.TryDefaultAction(1, out a) && a == InputManager.Actions.SwingWeapon, "right button -> SwingWeapon");
            Check(!MobilePointer.TryDefaultAction(2, out a), "middle button unbound by default");
            Check(!MobilePointer.TryDefaultAction(-1, out a), "invalid button unbound");
        }

        /// <summary>Screen.dpi returns 0 on some devices; the fallback must hold.</summary>
        static void TestDpiFallback()
        {
            Check(MobileInput.Dpi > 1f, "Dpi is usable (fallback works)", "dpi=" + MobileInput.Dpi);
            Near(MobileInput.InchesToPixels(1f), MobileInput.Dpi, 0.01f, "1 inch == dpi pixels");
            Near(MobileInput.InchesToPixels(0f), 0f, 0.001f, "0 inches == 0 pixels");
        }

        static void TestThresholdMaths()
        {
            // 0.9in at 264dpi on a 2752px longest edge, scale 0.15
            float t = MobileInputController.ComputeAttackThreshold(0.9f, 0.15f, 264f, 2752f);
            Near(t, (0.9f * 264f * 0.15f) / 2752f, 1e-6f, "threshold formula matches derivation");
            Check(t > 0f && t < 1f, "threshold in sane range", "t=" + t);
        }

        static void TestThresholdRoundTrip()
        {
            const float inches = 0.9f, scale = 0.15f, dpi = 264f, dim = 2752f;
            float t = MobileInputController.ComputeAttackThreshold(inches, scale, dpi, dim);
            float px = MobileInputController.RequiredSwipePixels(t, scale, dim);
            Near(px, inches * dpi, 0.5f, "round trip recovers the physical distance");
        }

        /// <summary>
        /// The whole point of DPI normalisation: the same setting must mean the same
        /// PHYSICAL swipe on a dense phone and a large tablet, even though the old
        /// screen-fraction approach differed by ~2x.
        /// </summary>
        static void TestDeviceIndependence()
        {
            const float inches = 0.9f, scale = 0.15f;

            // iPhone 17 Pro class: ~460dpi, 2622px longest edge
            float tPhone = MobileInputController.ComputeAttackThreshold(inches, scale, 460f, 2622f);
            float pxPhone = MobileInputController.RequiredSwipePixels(tPhone, scale, 2622f);

            // 13in iPad Pro class: ~264dpi, 2752px longest edge
            float tPad = MobileInputController.ComputeAttackThreshold(inches, scale, 264f, 2752f);
            float pxPad = MobileInputController.RequiredSwipePixels(tPad, scale, 2752f);

            Near(pxPhone / 460f, inches, 0.02f, "phone requires 0.9in of travel");
            Near(pxPad / 264f, inches, 0.02f, "tablet requires 0.9in of travel");
            Check(!Mathf.Approximately(pxPhone, pxPad),
                  "pixel counts differ while physical distance matches");
        }

        /// <summary>
        /// Teardown must hand the pointer back, or the classic UI is left with a frozen
        /// cursor and no fallback.
        /// </summary>
        static void TestRelinquish()
        {
            MobileInput.VirtualCursorActive = true;
            MobileInput.QueueClick(0);
            MobileInput.TickButtons();

            MobileInput.Relinquish();

            Check(!MobileInput.VirtualCursorActive, "Relinquish clears VirtualCursorActive");
            Check(!MobileInput.GetMouseButton(0), "Relinquish clears button state");
            Check(MobileInput.Mode == MobileControlMode.Gameplay, "Relinquish resets mode");
        }

        /// <summary>
        /// A journey steers by bearing alone, so a wrong bearing walks the player away from
        /// the destination for the entire trip. Unity yaw: 0 faces +Z, 90 faces +X.
        /// </summary>
        static void TestJourneyBearing()
        {
            Near(MobileJourneyPilot.BearingDegrees(0f, 0f, 0f, 100f), 0f, 0.01f,
                 "bearing: due north is 0");
            Near(MobileJourneyPilot.BearingDegrees(0f, 0f, 100f, 0f), 90f, 0.01f,
                 "bearing: due east is 90");
            Near(MobileJourneyPilot.BearingDegrees(0f, 0f, 0f, -100f), 180f, 0.01f,
                 "bearing: due south is 180");
            Near(MobileJourneyPilot.BearingDegrees(0f, 0f, -100f, 0f), 270f, 0.01f,
                 "bearing: due west is 270");
            Near(MobileJourneyPilot.BearingDegrees(0f, 0f, 100f, 100f), 45f, 0.01f,
                 "bearing: north-east is 45");

            // Never negative - the value is compared and logged, so a stable range matters.
            bool allInRange = true;
            for (int deg = 0; deg < 360; deg += 15)
            {
                float rad = deg * Mathf.Deg2Rad;
                float b = MobileJourneyPilot.BearingDegrees(
                    0f, 0f, Mathf.Sin(rad) * 500f, Mathf.Cos(rad) * 500f);
                if (b < 0f || b >= 360.01f)
                    allInRange = false;
            }
            Check(allInRange, "bearing: always normalised to 0-360");

            // Offset start position must not change the bearing - only the delta matters.
            Near(MobileJourneyPilot.BearingDegrees(5000f, -3000f, 5000f, -2900f), 0f, 0.01f,
                 "bearing: independent of absolute position");
        }

        /// <summary>
        /// The arrival rect is the location's rect grown on all four sides, so a journey stops
        /// outside the gates rather than walking itself into the location.
        /// </summary>
        static void TestJourneyArrivalRect()
        {
            Rect location = new Rect(10000f, 20000f, 400f, 600f);
            Rect arrival = MobileJourneyPilot.ArrivalRect(location);

            Check(arrival.Contains(new Vector2(location.center.x, location.center.y)),
                  "arrival rect: contains the location centre");

            // Grown, not shrunk, on every side.
            Check(arrival.xMin < location.xMin && arrival.xMax > location.xMax &&
                  arrival.yMin < location.yMin && arrival.yMax > location.yMax,
                  "arrival rect: grown on all four sides");

            // A point just outside the location but inside the margin must count as arrived,
            // which is the whole point of widening it.
            Check(arrival.Contains(new Vector2(location.xMin - 500f, location.center.y)),
                  "arrival rect: a point in the margin counts as arrived");

            // Far outside must not.
            Check(!arrival.Contains(new Vector2(location.xMin - 5000f, location.center.y)),
                  "arrival rect: a distant point does not count as arrived");

            Near(arrival.width - location.width, (arrival.height - location.height), 0.01f,
                 "arrival rect: margin applied equally to both axes");
        }

        static void TestJourneyCompressionClamp()
        {
            Check(MobileJourneyController.ClampCompression(0) >= 1,
                  "compression: zero clamps to at least 1x (time cannot stop)");
            Check(MobileJourneyController.ClampCompression(-50) >= 1,
                  "compression: negative clamps to at least 1x (time cannot reverse)");
            Check(MobileJourneyController.ClampCompression(9999) <=
                  MobileJourneyController.MaxTimeCompression,
                  "compression: absurd values clamp to the maximum");
            Check(MobileJourneyController.ClampCompression(20) == 20,
                  "compression: a legal value passes through unchanged");
            Check(MobileJourneyController.ClampCompression(
                      MobileJourneyController.DefaultTimeCompression) ==
                  MobileJourneyController.DefaultTimeCompression,
                  "compression: the default is itself legal");
        }

        /// <summary>
        /// The ceiling follows the transport (device decision): 50x on foot, 150x mounted,
        /// 200x by ship. Cautious vs reckless no longer changes speed.
        /// </summary>
        static void TestJourneySpeedTiers()
        {
            Check(MobileJourneyController.CapForTransport(TransportModes.Foot) == 50, "tiers: foot caps at 50x");
            Check(MobileJourneyController.CapForTransport(TransportModes.Horse) == 150, "tiers: horse caps at 150x");
            Check(MobileJourneyController.CapForTransport(TransportModes.Cart) == 150, "tiers: cart rides like a horse");
            Check(MobileJourneyController.CapForTransport(TransportModes.Ship) == 200, "tiers: ship caps at 200x");
            Check(MobileJourneyController.LoadPreferredCompression(TransportModes.Foot) >= 1 &&
                  MobileJourneyController.LoadPreferredCompression(TransportModes.Foot) <= 50,
                  "tiers: the remembered foot speed is within 1x..50x");
            Check(MobileJourneyController.LoadPreferredCompression(TransportModes.Horse) <= 150,
                  "tiers: the remembered horse speed never exceeds 150x");
        }

        /// <summary>
        /// The road rule that replaced "the road must be longer than the off-road ends" - which
        /// binned most medium trips. Plus the reset that used to wipe the planned route is a
        /// code-shape bug the tests cannot see; it is documented in Resume().
        /// </summary>
        /// <summary>
        /// The engine kills a player outright when fatigue reaches zero with enemies nearby or
        /// in water (PlayerEntity_OnExhausted -> SetHealth(0)); otherwise they collapse for an
        /// hour. A journey runs at 20-30x, so reckless travel with no fatigue guard walked the
        /// player into that death (device report: "healthy, only stamina low, just died").
        /// The guard must apply in EVERY mode, and must camp when resting is possible.
        /// </summary>
        static void TestJourneyVitals()
        {
            var V = MobileJourneyController.VitalsAction.Continue;
            Check(MobileJourneyController.DecideVitals(100, 100, cautious: false, enemiesNearby: false, swimming: false)
                  == MobileJourneyController.VitalsAction.Continue, "vitals: healthy and rested -> continue");
            Check(MobileJourneyController.DecideVitals(100, 15, cautious: false, enemiesNearby: false, swimming: false)
                  == MobileJourneyController.VitalsAction.Camp, "vitals: RECKLESS + low fatigue -> camp (not walk on to collapse)");
            Check(MobileJourneyController.DecideVitals(100, 15, cautious: true, enemiesNearby: false, swimming: false)
                  == MobileJourneyController.VitalsAction.Camp, "vitals: cautious + low fatigue -> camp");
            Check(MobileJourneyController.DecideVitals(100, 15, cautious: false, enemiesNearby: true, swimming: false)
                  == MobileJourneyController.VitalsAction.Stop, "vitals: low fatigue + enemies nearby -> stop (cannot rest; engine would kill at 0)");
            Check(MobileJourneyController.DecideVitals(100, 15, cautious: false, enemiesNearby: false, swimming: true)
                  == MobileJourneyController.VitalsAction.Stop, "vitals: low fatigue in water -> stop (exhaustion in water is death)");
            Check(MobileJourneyController.DecideVitals(3, 100, cautious: true, enemiesNearby: false, swimming: false)
                  == MobileJourneyController.VitalsAction.Stop, "vitals: cautious + low health -> stop");
            Check(MobileJourneyController.DecideVitals(3, 100, cautious: false, enemiesNearby: false, swimming: false)
                  == MobileJourneyController.VitalsAction.Continue, "vitals: reckless accepts low health (its stated trade)");
            Check(MobileJourneyController.DecideVitals(100, 20, cautious: false, enemiesNearby: false, swimming: false)
                  == MobileJourneyController.VitalsAction.Camp, "vitals: exactly the threshold counts as low");
            Check(MobileJourneyController.DecideVitals(100, 21, cautious: false, enemiesNearby: false, swimming: false)
                  == MobileJourneyController.VitalsAction.Continue, "vitals: one above the threshold continues");
        }

        /// <summary>
        /// Resuming after a camp used to reset the night flag, so a player who closed the rest
        /// screen without sleeping was asked to camp again at once, forever (probe run 6: three
        /// camps in under a second). The flag must survive a resume while it is still night.
        /// </summary>
        static void TestJourneyNightResume()
        {
            Check(MobileJourneyController.NightFlagOnResume(isNightNow: true, wasHandled: true),
                  "night: resuming into the same night keeps 'handled' (no instant re-camp)");
            Check(!MobileJourneyController.NightFlagOnResume(isNightNow: false, wasHandled: true),
                  "night: resuming by day clears 'handled' for the coming night");
            Check(!MobileJourneyController.NightFlagOnResume(isNightNow: true, wasHandled: false),
                  "night: a fresh journey at night has not handled tonight yet");
            Check(!MobileJourneyController.NightFlagOnResume(isNightNow: false, wasHandled: false),
                  "night: day, nothing handled");
        }

        /// <summary>
        /// A town is built some seconds after the player's pixel enters it. Until then the journey
        /// must stand still: no arrival, no "stop here?" for a town that is not there, no walking
        /// on through the empty footprint. Capped so a location that never builds cannot pin it.
        /// </summary>
        static void TestJourneyLocationHold()
        {
            Check(MobileJourneyController.ShouldHoldForLocation(hasLocation: true, locationBuilt: false, heldSeconds: 0f, maxHoldSeconds: 20f),
                  "hold: in a location that is not built yet -> hold");
            Check(!MobileJourneyController.ShouldHoldForLocation(hasLocation: true, locationBuilt: true, heldSeconds: 0f, maxHoldSeconds: 20f),
                  "hold: location built -> travel on");
            Check(!MobileJourneyController.ShouldHoldForLocation(hasLocation: false, locationBuilt: false, heldSeconds: 0f, maxHoldSeconds: 20f),
                  "hold: wilderness never holds");
            Check(!MobileJourneyController.ShouldHoldForLocation(hasLocation: true, locationBuilt: false, heldSeconds: 20f, maxHoldSeconds: 20f),
                  "hold: the cap releases a location that never builds");
            Check(MobileJourneyController.ShouldHoldForLocation(hasLocation: true, locationBuilt: false, heldSeconds: 19.9f, maxHoldSeconds: 20f),
                  "hold: just under the cap still holds");
        }

        /// <summary>
        /// Ship on Daggerfall's travel map is selectable anywhere; it only changes what ocean
        /// pixels cost. The pilot cannot sail, and used to walk across open water at 50x. Any
        /// route whose straight line crosses ocean falls back to classic fast travel.
        /// </summary>
        static void TestJourneySeaRoute()
        {
            Check(MobileJourneyController.RouteCanBeWalked(0), "sea: an all-land route is walked");
            Check(!MobileJourneyController.RouteCanBeWalked(1), "sea: one ocean pixel -> classic fast travel");
            Check(!MobileJourneyController.RouteCanBeWalked(40), "sea: a crossing -> classic fast travel");
            Check(MobileJourneyController.RouteCanBeWalked(-1), "sea: a negative count (never computed) is treated as land");
        }

        /// <summary>
        /// Poison and disease pause while a journey walks (they would otherwise run at 50x and
        /// kill on the road, where vanilla fast travel heals to full). The gate is the pilot's
        /// Active flag; with no journey running it must be off, so desktop and vanilla travel are
        /// untouched.
        /// </summary>
        static void TestJourneyStatusEffectPause()
        {
            Check(!MobileJourneyPilot.Active, "status pause: no journey is running in the editor");
            Check(!MobileJourneyController.StatusEffectsPaused,
                  "status pause: with no journey, poison and disease tick normally");
            // Camping releases the pilot and jumps the clock; the pause must hold across it, or
            // the first tick after the jump applies every missed minute at once (probe run 17).
            Check(MobileJourneyController.StatusEffectsPausedFor(pilotActive: false, restingForJourney: true),
                  "status pause: camping mid-journey keeps poison paused across the time jump");
            Check(MobileJourneyController.StatusEffectsPausedFor(pilotActive: true, restingForJourney: false),
                  "status pause: a walking pilot pauses");
            Check(!MobileJourneyController.StatusEffectsPausedFor(pilotActive: false, restingForJourney: false),
                  "status pause: off the road, effects tick");

            // Vanilla warns "you are not well and may not survive an extended period of travel"
            // before any fast travel while poisoned or diseased. A walking journey pauses both,
            // so the warning is kept only for classic (teleport) trips.
            Check(MobileJourneyController.WarnNotWellBeforeTravel(unwell: true, journeyWillWalk: false),
                  "not-well warning: sick + classic trip -> warn (vanilla)");
            Check(!MobileJourneyController.WarnNotWellBeforeTravel(unwell: true, journeyWillWalk: true),
                  "not-well warning: sick + walking journey -> no warning (effects pause)");
            Check(!MobileJourneyController.WarnNotWellBeforeTravel(unwell: false, journeyWillWalk: false),
                  "not-well warning: healthy -> no warning");

            // Ship ticked on an all-land route: ask before walking; never for classic trips.
            Check(MobileJourneyController.ShipPromptNeeded(travelShip: true, journeyWillWalk: true),
                  "ship prompt: Ship + land route that would be walked -> ask");
            Check(!MobileJourneyController.ShipPromptNeeded(travelShip: true, journeyWillWalk: false),
                  "ship prompt: Ship + sea route -> classic teleport, no prompt");
            Check(!MobileJourneyController.ShipPromptNeeded(travelShip: false, journeyWillWalk: true),
                  "ship prompt: Ship off -> walk without asking");
        }

        static void TestRouteRule()
        {
            Check(MobileJourneyController.RouteWorthTaking(30, 10, 35), "route: a road with short off-road ends is taken");
            Check(MobileJourneyController.RouteWorthTaking(3, 10, 12), "route: a short road is still taken if reaching it is cheap");
            Check(!MobileJourneyController.RouteWorthTaking(30, 40, 20), "route: refused when the detour outweighs the trip");
            Check(!MobileJourneyController.RouteWorthTaking(1, 0, 5), "route: a one-pixel route is not a route");
        }

        /// <summary>Nightfall decision table: what the travel popup's option means at dusk.</summary>
        static void TestNightDecision()
        {
            var N = MobileJourneyController.NightAction.None;
            Check(MobileJourneyController.DecideNight(false, false, false, false, 100, 5) == N, "night: daytime does nothing");
            Check(MobileJourneyController.DecideNight(true, true, false, false, 100, 5) == N, "night: decided once per night");
            Check(MobileJourneyController.DecideNight(true, false, false, true, 100, 5) == MobileJourneyController.NightAction.Camp,
                  "night: camp out camps, even in a town");
            Check(MobileJourneyController.DecideNight(true, false, true, true, 100, 5) == MobileJourneyController.NightAction.Inn,
                  "night: inns mode in a town takes a room");
            Check(MobileJourneyController.DecideNight(true, false, true, false, 100, 5) == MobileJourneyController.NightAction.TravelOn,
                  "night: inns mode in the wild walks on to the next town");
            Check(MobileJourneyController.DecideNight(true, false, true, true, 3, 5) == MobileJourneyController.NightAction.CampNoGold,
                  "night: inns mode without the gold camps outside the walls");
            Check(MobileJourneyController.DecideNight(true, false, true, true, 0, 0) == MobileJourneyController.NightAction.Inn,
                  "night: free rooms (knightly order) cost nothing");
            Check(MobileJourneyController.HoursUntilDawn(18) == 12 && MobileJourneyController.HoursUntilDawn(2) == 4 &&
                  MobileJourneyController.HoursUntilDawn(23) == 7,
                  "night: hours to dawn wrap past midnight");
        }

        /// <summary>
        /// Crossing a settlement: the exit point must be on the far side of its footprint along
        /// the bearing, plus the margin - and never behind the player.
        /// </summary>
        static void TestPassThroughGeometry()
        {
            Rect town = new Rect(1000f, 1000f, 2000f, 2000f);      // x 1000..3000, y 1000..3000

            // Heading north (yaw 0) from the south edge: leave through y = 3000.
            Vector2 e = MobileJourneyPilot.ExitPointThroughRect(town, new Vector2(2000f, 1000f), 0f, 100f);
            Near(e.x, 2000f, 0.5f, "pass-through: north exit keeps x");
            Near(e.y, 3100f, 0.5f, "pass-through: north exit is the far edge plus margin");

            // Heading east (yaw 90) from inside: leave through x = 3000.
            e = MobileJourneyPilot.ExitPointThroughRect(town, new Vector2(1500f, 2000f), 90f, 50f);
            Near(e.x, 3050f, 0.5f, "pass-through: east exit is the far edge plus margin");
            Near(e.y, 2000f, 0.5f, "pass-through: east exit keeps y");

            // Already past it, heading away: just the margin ahead.
            e = MobileJourneyPilot.ExitPointThroughRect(town, new Vector2(2000f, 3500f), 0f, 100f);
            Near(e.y, 3600f, 0.5f, "pass-through: beyond the town, a short hop forward");

            Check(Mathf.Abs(Mathf.DeltaAngle(MobileJourneyPilot.TurnToward(10f, 350f, 5f), 5f)) < 0.01f,
                  "steering: turns the short way round and no faster than the step");
            Check(Mathf.Abs(Mathf.DeltaAngle(MobileJourneyPilot.TurnToward(10f, 20f, 90f), 20f)) < 0.01f,
                  "steering: a big step reaches the target");
        }

        /// <summary>The ported path data is present and looks like a road network.</summary>
        static void TestRoadData()
        {
            Check(MobileRoadNetwork.Available, "roads: path data loaded from Resources");
            if (!MobileRoadNetwork.Available)
                return;

            int withPath = 0;
            for (int y = 0; y < MobileRoadNetwork.Height; y += 3)
                for (int x = 0; x < MobileRoadNetwork.Width; x += 3)
                    if (MobileRoadNetwork.HasAnyPath(x, y))
                        withPath++;

            // Sampled every third pixel, so this is a shape check rather than a census: a
            // network covers a small but non-trivial slice of the world. Zero means the data
            // did not really load; a huge number means it is not a network at all.
            Check(withPath > 200, "roads: network is not empty",
                  "sampled pixels carrying a path: " + withPath);
            Check(withPath < MobileRoadNetwork.Width * MobileRoadNetwork.Height / 9 / 2,
                  "roads: network is sparse, as a road network should be",
                  "sampled pixels carrying a path: " + withPath);

            Check(!MobileRoadNetwork.InBounds(-1, 0) &&
                  !MobileRoadNetwork.InBounds(0, MobileRoadNetwork.Height),
                  "roads: bounds reject out-of-world pixels");
        }

        /// <summary>
        /// The bug that hid the roads: the texturing was assigned once, before any scene, to a
        /// DaggerfallUnity the game scene then replaced - whose fresh DefaultTerrainTexturing
        /// nobody overrode. Model exactly that: install, swap in a default (what a new
        /// DaggerfallUnity's field initialiser does), and require the install to come back.
        /// </summary>
        static void TestRoadsInstallSurvivesSceneSwap()
        {
            bool savedPref = MobileMods.Roads;
            try
            {
                MobileMods.Roads = true;
                DaggerfallUnity dfUnity = DaggerfallUnity.Instance;
                dfUnity.TerrainTexturing = new DefaultTerrainTexturing();
                Check(!MobileRoads.Active, "roads: default texturing reads as not active");

                MobileRoads.InstallOnLiveInstance();
                Check(dfUnity.TerrainTexturing is BasicRoads.BasicRoadsTexturing,
                      "roads: install lands on the live DaggerfallUnity");
                Check(MobileRoads.Active && !MobileRoads.RestartRequired,
                      "roads: Active reflects the live instance");

                // A scene swap: the new DaggerfallUnity arrives with a default texturing.
                dfUnity.TerrainTexturing = new DefaultTerrainTexturing();
                Check(!MobileRoads.Active && MobileRoads.RestartRequired,
                      "roads: a replaced texturing is reported honestly");

                MobileRoads.InstallOnLiveInstance();
                Check(MobileRoads.Active, "roads: re-installed after the swap");

                MobileMods.Roads = false;
                dfUnity.TerrainTexturing = new DefaultTerrainTexturing();
                MobileRoads.InstallOnLiveInstance();
                Check(!MobileRoads.Active, "roads: not installed while the preference is off");
            }
            finally
            {
                MobileMods.Roads = savedPref;
                if (DaggerfallUnity.HasInstance)
                    DaggerfallUnity.Instance.TerrainTexturing = new DefaultTerrainTexturing();
            }
        }

        /// <summary>
        /// A right-click's touch can be seen a frame before GameController reports the button.
        /// Inside the grace window it must not count as a finger, or the touch HUD flashes on
        /// every attack. Outside it, with no button held, a direct touch is a finger.
        /// </summary>
        static void TestPointerClickGrace()
        {
            Check(!MobilePointer.IsFingerTouch(TouchType.Direct, false, 0.05f, 0.4f),
                  "grace: touch right after pointer activity is the click, not a finger");
            Check(MobilePointer.IsFingerTouch(TouchType.Direct, false, 1.0f, 0.4f),
                  "grace: a touch well after pointer activity is a finger");
            Check(!MobilePointer.IsFingerTouch(TouchType.Direct, true, 5f, 0.4f),
                  "grace: button held is never a finger");
            Check(!MobilePointer.IsFingerTouch(TouchType.Indirect, false, 5f, 0.4f),
                  "grace: indirect touch is never a finger");

            Vector2 big = MobilePointer.ClampDelta(new Vector2(3000f, -4000f), 250f);
            Check(Mathf.Abs(big.magnitude - 250f) < 0.01f, "delta clamp: a lock-transition spike is capped");
            Check(Mathf.Abs(big.x / big.y - 3000f / -4000f) < 0.001f, "delta clamp: direction preserved");
            Check(MobilePointer.ClampDelta(new Vector2(3f, 4f), 250f) == new Vector2(3f, 4f),
                  "delta clamp: ordinary movement untouched");

            Check(MobileInput.SecondTapConfirms(true, true, 4, 4, 0.5f, 0.3f),
                  "second tap: slow re-tap on the same row confirms");
            Check(!MobileInput.SecondTapConfirms(true, true, 4, 4, 0.2f, 0.3f),
                  "second tap: a fast pair is the engine's double-click, not ours");
            Check(!MobileInput.SecondTapConfirms(true, true, 5, 4, 0.5f, 0.3f),
                  "second tap: a different row only selects");
            Check(!MobileInput.SecondTapConfirms(false, true, 4, 4, 0.5f, 0.3f),
                  "second tap: keyboard/programmatic selection never confirms");
            Check(!MobileInput.SecondTapConfirms(true, true, -1, -1, 0.5f, 0.3f),
                  "second tap: empty selection never confirms");

            int open = 0;
            for (uint h = 0; h < 1000u; h++)
                if (MobileJourneyController.CautiousEncounterGateOpen(h, 25)) open++;
            Check(open > 150 && open < 350,
                  "encounter gate: ~25% of hours are open (got " + open + "/1000)");
            Check(MobileJourneyController.CautiousEncounterGateOpen(7u, 25) ==
                  MobileJourneyController.CautiousEncounterGateOpen(7u, 25),
                  "encounter gate: deterministic for the same hour");
            bool anyOpen0 = false, allOpen100 = true;
            for (uint h = 0; h < 200u; h++)
            {
                anyOpen0 |= MobileJourneyController.CautiousEncounterGateOpen(h, 0);
                allOpen100 &= MobileJourneyController.CautiousEncounterGateOpen(h, 100);
            }
            Check(!anyOpen0, "encounter gate: 0% never opens");
            Check(allOpen100, "encounter gate: 100% always open");

            // Fresh install: both built-in mods must start OFF (release requirement,
            // 2026-08-31). With no pref keys and no ModManager, the flags fall back to
            // their shipped defaults - which must be false.
            bool hadRoads = PlayerPrefs.HasKey("DFMobile.mod.roads");
            bool hadTravel = PlayerPrefs.HasKey("DFMobile.journeymode");
            int savedRoadsPref = PlayerPrefs.GetInt("DFMobile.mod.roads", 0);
            int savedTravelPref = PlayerPrefs.GetInt("DFMobile.journeymode", 0);
            try
            {
                PlayerPrefs.DeleteKey("DFMobile.mod.roads");
                PlayerPrefs.DeleteKey("DFMobile.journeymode");
                Check(!MobileMods.Roads, "fresh install: Roads & tracks starts off");
                Check(!MobileMods.RealTravel, "fresh install: Real travel starts off");
            }
            finally
            {
                if (hadRoads) PlayerPrefs.SetInt("DFMobile.mod.roads", savedRoadsPref);
                if (hadTravel) PlayerPrefs.SetInt("DFMobile.journeymode", savedTravelPref);
            }
        }

        /// <summary>The HID table must round-trip and cover what Daggerfall binds by default.</summary>
        static void TestHardwareKeyboardTable()
        {
            KeyCode[] must = { KeyCode.W, KeyCode.A, KeyCode.S, KeyCode.D, KeyCode.Space, KeyCode.Return,
                               KeyCode.Escape, KeyCode.LeftShift, KeyCode.UpArrow, KeyCode.F5, KeyCode.Alpha0,
                               KeyCode.Keypad0, KeyCode.Tab, KeyCode.BackQuote };
            bool ok = true;
            foreach (KeyCode k in must)
            {
                int hid = MobileHardwareKeyboard.ToHid(k);
                if (hid < 0 || MobileHardwareKeyboard.FromHid(hid) != k)
                    ok = false;
            }
            Check(ok, "keyboard: HID table round-trips the default bindings");
            Check(MobileHardwareKeyboard.FromHid(4) == KeyCode.A && MobileHardwareKeyboard.FromHid(29) == KeyCode.Z,
                  "keyboard: letters follow HID usage order");
            Check(MobileHardwareKeyboard.FromHid(0) == KeyCode.None && MobileHardwareKeyboard.ToHid(KeyCode.Mouse0) < 0,
                  "keyboard: unknown codes are None / -1, so callers fall back");
            bool held;
            Check(!MobileHardwareKeyboard.TryGetKey(KeyCode.W, out held) && !held,
                  "keyboard: no plugin in the editor -> fall back to Unity");
        }

        /// <summary>Two switches since the 2026-08-30 split: each drives only its own flag.</summary>
        static void TestModsSwitchOwnsBothPrefs()
        {
            bool savedRoads = MobileMods.Roads;
            bool savedTravel = MobileMods.RealTravel;
            try
            {
                MobileMods.Roads = true;
                MobileMods.RealTravel = false;
                Check(MobileRoads.Enabled && !MobileJourneyController.JourneyModeEnabled,
                      "mods: roads alone - scenery without the journey system");
                MobileMods.Roads = false;
                MobileMods.RealTravel = true;
                Check(!MobileRoads.Enabled && MobileJourneyController.JourneyModeEnabled,
                      "mods: travel alone - journeys follow road data invisibly");
                MobileJourneyController.JourneyModeEnabled = false;      // a stale flag
                MobileMods.ApplySaved();
                Check(MobileJourneyController.JourneyModeEnabled, "mods: ApplySaved re-asserts the saved choice");
            }
            finally
            {
                MobileMods.Roads = savedRoads;
                MobileMods.RealTravel = savedTravel;
            }
        }

        /// <summary>
        /// The date a new character starts on. The switch is off by default and the off case
        /// must be byte-for-byte the classic 13:30 4th Morning Star 3E405 - purists get the
        /// shipwreck date whatever else this port does. On, only the MONTH moves, to Midyear,
        /// which is Summer; day, year and time of day are untouched.
        /// </summary>
        static void TestSummerStartDate()
        {
            // Default is off. Read through a fresh pref name so a developer who has turned
            // the switch on for themselves does not turn this assertion into a lie.
            const string pref = "DFMobile.mod.summerstart";
            bool hadPref = PlayerPrefs.HasKey(pref);
            int savedPref = PlayerPrefs.GetInt(pref, 0);
            bool savedFlag = MobileMods.SummerStart;
            try
            {
                PlayerPrefs.DeleteKey(pref);
                Check(PlayerPrefs.GetInt(pref, 0) == 0,
                      "summer start: the preference defaults to off (vanilla winter date)");

                DaggerfallDateTime vanilla = new DaggerfallDateTime();
                vanilla.SetClassicGameStartTime();

                DaggerfallDateTime off = new DaggerfallDateTime();
                MobileStartSeason.ApplyNewGameStartTime(off, false);
                Check(off.Year == vanilla.Year && off.Month == vanilla.Month
                      && off.Day == vanilla.Day && off.Hour == vanilla.Hour
                      && off.Minute == vanilla.Minute,
                      "summer start off: the classic start date is untouched",
                      off.Year + "/" + off.Month + "/" + off.Day + " " + off.Hour + ":" + off.Minute);
                Check(off.ToClassicDaggerfallTime() == vanilla.ToClassicDaggerfallTime(),
                      "summer start off: classic minutes match, so LastGameMinutes is unchanged");
                Check(off.SeasonValue == DaggerfallDateTime.Seasons.Winter
                      && off.MonthValue == DaggerfallDateTime.Months.MorningStar,
                      "summer start off: still the 4th of Morning Star, in winter");

                DaggerfallDateTime on = new DaggerfallDateTime();
                MobileStartSeason.ApplyNewGameStartTime(on, true);
                Check(on.SeasonValue == DaggerfallDateTime.Seasons.Summer,
                      "summer start on: the new character wakes up in summer",
                      on.SeasonValue.ToString());
                Check(on.MonthValue == DaggerfallDateTime.Months.Midyear
                      && MobileStartSeason.SummerMonth == 5,
                      "summer start on: the month is Midyear (5), the middle summer month",
                      on.Month.ToString());
                Check(on.Day == vanilla.Day && on.Year == vanilla.Year,
                      "summer start on: same day of month and same year (3E405)",
                      on.Day + "/" + on.Year);
                Check(on.Hour == vanilla.Hour && on.Minute == vanilla.Minute
                      && on.Hour == 13 && on.Minute == 30,
                      "summer start on: still 13:30, so light and shop hours do not shift",
                      on.Hour + ":" + on.Minute);

                // Only the month may differ between the two.
                Check(on.ToClassicDaggerfallTime() != vanilla.ToClassicDaggerfallTime(),
                      "summer start on: the clock really did move");

                // A null clock must not throw - the new-game path calls this before anything
                // else touches WorldTime.
                MobileStartSeason.ApplyNewGameStartTime(null, true);
                Check(true, "summer start: a null date time is ignored rather than throwing");

                // The pref round-trips through MobileMods, and MobileStartSeason reads it.
                MobileMods.SummerStart = true;
                Check(MobileStartSeason.Enabled, "summer start: MobileStartSeason reads the mod switch");
                MobileMods.SummerStart = false;
                Check(!MobileStartSeason.Enabled, "summer start: turning the switch off is honoured");
            }
            finally
            {
                MobileMods.SummerStart = savedFlag;
                if (hadPref)
                    PlayerPrefs.SetInt(pref, savedPref);
                else
                    PlayerPrefs.DeleteKey(pref);
                PlayerPrefs.Save();
            }
        }

        /// <summary>
        /// The whole iOS mod pipeline in one pass: pack the pilot manifest into a .dfmod,
        /// load it back, and look up the replacement exactly the way ModManager does at
        /// runtime. Also pins the refusal of script mods - iOS is IL2CPP, no JIT.
        /// </summary>
        static void TestModBundleRoundTrip()
        {
            const string manifest = "Assets/Game/Mods/IOSPilot/ios-pilot.dfmod.json";
            const string outRoot = "Temp/MobileModBuilderTest";
            if (Directory.Exists(outRoot))
                Directory.Delete(outRoot, true);

            // Import settings must be normalized (NPOT scaling would silently resize).
            AssetDatabase.ImportAsset("Assets/Game/Mods/IOSPilot/PICK03I0.IMG.png",
                ImportAssetOptions.ForceUpdate);

            string[] built = MobileModBuilder.BuildMod(manifest, outRoot,
                new[] { BuildTarget.StandaloneOSX });
            Check(built.Length == 1 && File.Exists(built[0]),
                  "builder produces a .dfmod", built.Length > 0 ? built[0] : "no output");

            AssetBundle ab = AssetBundle.LoadFromFile(built[0]);
            Check(ab != null, "built bundle loads in the editor");
            if (ab != null)
            {
                bool hasManifest = false;
                foreach (string n in ab.GetAllAssetNames())
                    if (n.EndsWith(".dfmod.json")) hasManifest = true;
                Check(hasManifest, "bundle carries its manifest (Mod ctor requires it)");

                // Exactly the lookup ModManager.TryGetAsset does at runtime.
                Check(ab.Contains("PICK03I0.IMG"), "bundle answers to the runtime texture name");
                var tex = ab.LoadAsset<Texture2D>("PICK03I0.IMG");
                Check(tex != null && tex.width == 320 && tex.height == 200,
                      "replacement texture loads at 320x200",
                      tex ? tex.width + "x" + tex.height : "null");
                ab.Unload(true);
            }

            // Script mods must be refused loudly, not built silently.
            Directory.CreateDirectory(outRoot);
            string scriptManifest = Path.Combine(outRoot, "script-mod.dfmod.json");
            File.WriteAllText(scriptManifest,
                "{\"ModTitle\":\"Script Mod\",\"GUID\":\"test-script-mod\"," +
                "\"Files\":[\"Assets/Fake/Thing.cs\"]}");
            bool refused = false;
            try { MobileModBuilder.BuildMod(scriptManifest, outRoot, new[] { BuildTarget.StandaloneOSX }); }
            catch (System.NotSupportedException) { refused = true; }
            Check(refused, "builder refuses script mods (no JIT on iOS)");

            Directory.Delete(outRoot, true);
        }

        /// <summary>
        /// The engine-side half of the same rule: iOS runs IL2CPP, so mod scripts can be
        /// neither compiled from source nor Assembly.Load-ed. The guard must fire only for
        /// mods that actually carry sources, leaving asset-only mods completely alone.
        /// </summary>
        static void TestModScriptSkipRule()
        {
            // iOS runs IL2CPP: no JIT, so mod scripts can be neither compiled nor loaded.
            // Asset-only mods (sources == 0) must be untouched by the guard.
            Check(!Mod.ShouldSkipScriptCompilation(0, true), "asset-only mod, JIT: no skip");
            Check(!Mod.ShouldSkipScriptCompilation(0, false), "asset-only mod, no JIT: no skip");
            Check(!Mod.ShouldSkipScriptCompilation(2, true), "script mod, JIT: compiles");
            Check(Mod.ShouldSkipScriptCompilation(2, false), "script mod, no JIT: skips");
            Check(Mod.RuntimeScriptsSupported, "editor/desktop supports mod scripts");
        }

        /// <summary>
        /// Normal maps do not survive a naive extraction. A compressed normal map does not
        /// store its blue channel at all: DXT5nm keeps x in alpha and y in green (RGB are
        /// thrown away), BC5 keeps x and y in red and green. Writing those bytes straight
        /// out yields a PNG that looks like a normal map to no one - the same family of
        /// silent corruption as a blank texture, but harder to see. z has to be rebuilt from
        /// x and y, which is possible because a tangent-space normal is a unit vector.
        /// </summary>
        static void TestNormalReconstructRule()
        {
            // Flat up-normal (0,0,1): x=y=0 -> encoded 128,128,255.
            var flat = MobileModExtractor.ReconstructNormalPixel(new Color32(255, 128, 0, 128), true);
            Check(flat.r == 128 && flat.g == 128 && flat.b >= 254, "DXTnm flat normal reconstructs (x from alpha)");
            var flatBc5 = MobileModExtractor.ReconstructNormalPixel(new Color32(128, 128, 0, 255), false);
            Check(flatBc5.r == 128 && flatBc5.g == 128 && flatBc5.b >= 254, "BC5 flat normal reconstructs (x from red)");
            // Fully tilted +x: x=1, y=0 -> z=0. Zero is the MIDDLE of the encoding, not the
            // bottom of it: every channel of a tangent-space normal map is stored as
            // (n * 0.5 + 0.5), which is what Unity's UnpackNormal undoes, so a collapsed z
            // encodes to 128 and not to 0.
            var tilt = MobileModExtractor.ReconstructNormalPixel(new Color32(0, 128, 0, 255), true);
            Check(tilt.r == 255 && tilt.b == 128, "tilted normal keeps x, z collapses to encoded zero",
                  "r=" + tilt.r + " b=" + tilt.b);
            // A vector that is not unit length must not produce NaN or wrap around: x=y=1
            // gives 1-x*x-y*y = -1, and Mathf.Sqrt of a negative is NaN, which casts to a
            // garbage byte. The max(0,..) clamp is what stops a corrupt source pixel from
            // becoming a corrupt output pixel.
            var over = MobileModExtractor.ReconstructNormalPixel(new Color32(255, 255, 0, 255), false);
            Check(over.r == 255 && over.g == 255 && over.b == 128 && over.a == 255,
                  "over-unit x,y clamps to z=0 instead of NaN", "b=" + over.b);
            // Alpha is always opaque: the extracted png is a data texture, and a 0 alpha
            // would let a later importer treat it as transparent.
            Check(flat.a == 255 && flatBc5.a == 255 && tilt.a == 255, "reconstructed normal is opaque");

            // Which textures get this treatment is decided by name alone - a bundle texture
            // records nothing about the importer settings it was built with - so the naming
            // rule is the whole of the classification and both the extractor and the converted
            // -mod import policy read it from here. DFU appends "_" + the TextureMap enum name
            // (TextureReplacement.GetName), and its own IsLinearTextureMap calls exactly
            // Normal, Height and MetallicGloss linear: Emission and Mask are colour, and
            // forcing those linear would regrade them as badly as leaving a normal in sRGB.
            const string dfuName = "Assets/Textures/004_0-0";
            Check(MobileModExtractor.IsNormalMapName(dfuName + "_Normal.png"), "DFU _Normal suffix is a normal map");
            Check(MobileModExtractor.IsNormalMapName(dfuName + "_normal.PNG"), "suffix match ignores case");
            Check(!MobileModExtractor.IsNormalMapName(dfuName + ".png"), "an albedo is not a normal map");
            // The underscore is required of every map except Normal. Dynamic Skies names its cloud
            // normal map "CdMCloudsNormal", so a bare "Normal" tail counts as well - deliberately
            // wider than DFU's own naming, and safe because NormalUnswizzlerFor passes an
            // sRGB-flagged source through untouched, so a colour texture that merely shares the
            // tail is never swizzled.
            Check(MobileModExtractor.IsNormalMapName("Assets/Textures/wallNormal.png"),
                  "a bare 'Normal' tail is a normal map: 'wallNormal' counts");
            Check(!MobileModExtractor.IsLinearMapName("Assets/Textures/wallHeight.png"),
                  "the underscore is still required of the other maps: 'wallHeight' is not a height map");
            Check(!MobileModExtractor.IsNormalMapName(dfuName + "_Height.png"), "a height map is not a normal map");
            Check(MobileModExtractor.IsLinearMapName(dfuName + "_Normal.png")
                  && MobileModExtractor.IsLinearMapName(dfuName + "_Height.png")
                  && MobileModExtractor.IsLinearMapName(dfuName + "_MetallicGloss.png"),
                  "normal, height and metallic/gloss are linear (as in DFU's IsLinearTextureMap)");
            Check(!MobileModExtractor.IsLinearMapName(dfuName + ".png")
                  && !MobileModExtractor.IsLinearMapName(dfuName + "_Emission.png")
                  && !MobileModExtractor.IsLinearMapName(dfuName + "_Mask.png"),
                  "albedo, emission and mask stay sRGB colour");
        }

        /// <summary>
        /// The WAV container the extractor writes, as a pure function. A bundle's AudioClip is
        /// float samples in memory and nothing else - whatever the author imported is gone - so
        /// the extraction has to build a file format from scratch, and a header that is wrong by
        /// one field produces a file every tool refuses or, worse, one that decodes at the wrong
        /// rate. The layout is the standard 44-byte canonical RIFF/WAVE: "RIFF", size-8, "WAVE",
        /// "fmt " with 16 bytes of PCM fields, then "data" with the payload size.
        /// </summary>
        static void TestWavEncoderRule()
        {
            // The brief's shape check: four mono samples at 8kHz is 44 header bytes + 8 payload,
            // and +1.0 is the top of the 16-bit range.
            byte[] wav = MobileModExtractor.EncodeWav(new float[] { 0f, 1f, -1f, 0f }, 1, 8000);
            Check(wav.Length == 44 + 8 && wav[0] == (byte)'R'
                  && BitConverter.ToInt16(wav, 46) == short.MaxValue,
                  "EncodeWav writes 16-bit PCM with RIFF header", "len=" + wav.Length);

            // Every field of the header, by offset. These are what a decoder actually reads;
            // a plausible-looking file with byteRate or blockAlign wrong plays at the wrong
            // speed rather than failing loudly, which is the failure mode worth pinning.
            Check(System.Text.Encoding.ASCII.GetString(wav, 0, 4) == "RIFF"
                  && System.Text.Encoding.ASCII.GetString(wav, 8, 4) == "WAVE"
                  && System.Text.Encoding.ASCII.GetString(wav, 12, 4) == "fmt "
                  && System.Text.Encoding.ASCII.GetString(wav, 36, 4) == "data",
                  "canonical chunk ids at the canonical offsets");
            Check(BitConverter.ToInt32(wav, 4) == wav.Length - 8
                  && BitConverter.ToInt32(wav, 40) == 8,
                  "RIFF size is the file minus 8; data size is the payload",
                  "riff=" + BitConverter.ToInt32(wav, 4) + " data=" + BitConverter.ToInt32(wav, 40));
            Check(BitConverter.ToInt32(wav, 16) == 16 && BitConverter.ToInt16(wav, 20) == 1
                  && BitConverter.ToInt16(wav, 34) == 16,
                  "fmt chunk is 16 bytes, format tag 1 (PCM), 16 bits per sample");
            Check(BitConverter.ToInt16(wav, 22) == 1 && BitConverter.ToInt32(wav, 24) == 8000
                  && BitConverter.ToInt32(wav, 28) == 8000 * 1 * 2
                  && BitConverter.ToInt16(wav, 32) == 1 * 2,
                  "mono 8kHz: byteRate = freq*channels*2, blockAlign = channels*2",
                  "byteRate=" + BitConverter.ToInt32(wav, 28) + " align=" + BitConverter.ToInt16(wav, 32));

            // Stereo changes two derived fields and nothing else; getting them from the channel
            // count rather than assuming mono is the difference between a stereo song and a
            // stereo song played at half speed.
            byte[] st = MobileModExtractor.EncodeWav(new float[] { 0f, 0f, 0f, 0f }, 2, 44100);
            Check(BitConverter.ToInt16(st, 22) == 2 && BitConverter.ToInt32(st, 28) == 44100 * 2 * 2
                  && BitConverter.ToInt16(st, 32) == 4 && st.Length == 44 + 8,
                  "stereo derives byteRate and blockAlign from the channel count",
                  "byteRate=" + BitConverter.ToInt32(st, 28) + " align=" + BitConverter.ToInt16(st, 32));

            // Clamping is not decoration. AudioClip.GetData can hand back samples outside
            // [-1,1] - a mod mastered hot, or any DSP that overshot - and the naive cast wraps:
            // 1.5*32767 is 49150, which truncates to -16386 and turns a loud peak into a loud
            // click of the opposite sign. Clamp, do not wrap.
            byte[] hot = MobileModExtractor.EncodeWav(new float[] { 1.5f, -1.5f, float.NaN }, 1, 8000);
            Check(BitConverter.ToInt16(hot, 44) == short.MaxValue
                  && BitConverter.ToInt16(hot, 46) == -short.MaxValue,
                  "samples past full scale clamp instead of wrapping round",
                  "hi=" + BitConverter.ToInt16(hot, 44) + " lo=" + BitConverter.ToInt16(hot, 46));
            Check(BitConverter.ToInt16(hot, 48) == 0, "a NaN sample becomes silence, not a garbage byte",
                  "nan=" + BitConverter.ToInt16(hot, 48));

            // An empty clip is a legal one: header only, and no decoder is asked to read past it.
            byte[] empty = MobileModExtractor.EncodeWav(new float[0], 1, 22050);
            Check(empty.Length == 44 && BitConverter.ToInt32(empty, 40) == 0,
                  "an empty clip still produces a valid header-only file", "len=" + empty.Length);
        }

        /// <summary>
        /// The converted-mod import policy, as pure rules. This is the memory-critical part of
        /// the pipeline: against 1.72GB of DREAM textures plus ~3.7GB of sprite modules on an
        /// 8GB iPad, the size cap, the mipmap decision and the ASTC block size are the three
        /// numbers that decide whether the pack loads or iOS kills the app. They are read from
        /// environment variables so they can be tuned against a device without a recompile,
        /// which means the PARSING is now part of the policy: an operator typo that silently
        /// fell back to "whatever the platform picks" would undo the whole point of naming them.
        /// </summary>
        static void TestConvertedModImportPolicy()
        {
            // Defaults, stated as assertions so a change to one is a deliberate act.
            Check(MobileConvertedModPolicy.DefaultMaxTextureSize == 1024,
                  "default cap is 1024, not Unity's never-downscale 2048",
                  "" + MobileConvertedModPolicy.DefaultMaxTextureSize);
            Check(MobileConvertedModPolicy.ParseAstcBlock(
                      MobileConvertedModPolicy.DefaultAstcBlock, "6x6")
                  == TextureImporterFormat.ASTC_6x6, "default iOS block is ASTC 6x6 (3.56 bpp)");

            // Sizes. Unity accepts powers of two from 32 to 16384 and nothing else, so a typo
            // must fall back loudly rather than become the policy.
            Check(MobileConvertedModPolicy.ParseSize("2048", 1024) == 2048, "a valid cap is honoured");
            Check(MobileConvertedModPolicy.ParseSize(" 512 ", 1024) == 512, "whitespace is tolerated");
            Check(MobileConvertedModPolicy.ParseSize(null, 1024) == 1024, "unset keeps the default");
            Check(MobileConvertedModPolicy.ParseSize("", 1024) == 1024, "empty keeps the default");
            Check(MobileConvertedModPolicy.ParseSize("1000", 1024) == 1024, "a non-power-of-two is refused");
            Check(MobileConvertedModPolicy.ParseSize("16", 1024) == 1024, "an absurdly small cap is refused");
            Check(MobileConvertedModPolicy.ParseSize("banana", 1024) == 1024, "garbage is refused");

            // Booleans, in the spellings a shell user actually types.
            Check(MobileConvertedModPolicy.ParseBool("1", false)
                  && MobileConvertedModPolicy.ParseBool("true", false)
                  && MobileConvertedModPolicy.ParseBool("ON", false), "1/true/on are true");
            Check(!MobileConvertedModPolicy.ParseBool("0", true)
                  && !MobileConvertedModPolicy.ParseBool("no", true)
                  && !MobileConvertedModPolicy.ParseBool("Off", true), "0/no/off are false");
            Check(MobileConvertedModPolicy.ParseBool(null, true)
                  && !MobileConvertedModPolicy.ParseBool("maybe", false), "unset and garbage keep the default");

            // ASTC block sizes: the bytes-per-pixel lever.
            Check(MobileConvertedModPolicy.ParseAstcBlock("4x4", "6x6") == TextureImporterFormat.ASTC_4x4
                  && MobileConvertedModPolicy.ParseAstcBlock("8x8", "6x6") == TextureImporterFormat.ASTC_8x8
                  && MobileConvertedModPolicy.ParseAstcBlock("12x12", "6x6") == TextureImporterFormat.ASTC_12x12,
                  "every block size Unity defines is reachable");
            Check(MobileConvertedModPolicy.ParseAstcBlock("7x7", "6x6") == TextureImporterFormat.ASTC_6x6,
                  "a block size Unity does not define falls back rather than guessing");
            Check(MobileConvertedModPolicy.ParseQuality("100", 50) == 100
                  && MobileConvertedModPolicy.ParseQuality("-1", 50) == 50
                  && MobileConvertedModPolicy.ParseQuality("101", 50) == 50,
                  "compressor quality is clamped to 0-100 or refused");

            // The mipmap rule. Mipmaps cost 33% resident across the whole pack, and 2D art
            // drawn at 1:1 never samples them. Which assets those are is derived from DFU's own
            // conventions, not invented: TextureReplacement serves IMG images and CIF/RCI
            // images (paperdolls, portraits, weapon animations, UI) - and a MOD can only serve
            // them under a short name carrying the original .IMG/.CIF/.RCI filename, because
            // that name is the runtime lookup key (TryImportImage/TryImportCifRci ->
            // ModManager.TryGetAsset). So the name is a real signal even though a bundled mod's
            // internal directory layout is the author's own business.
            string[] markers = MobileConvertedModPolicy.DefaultNoMipMarkers;
            Check(MobileConvertedModPolicy.ShouldMipmap("Assets/Textures/004_0-0.png", markers),
                  "a world texture is minified in use and keeps mipmaps");
            Check(MobileConvertedModPolicy.ShouldMipmap("Assets/Textures/210_1-0_Normal.png", markers),
                  "a billboard's normal map keeps mipmaps too");
            Check(!MobileConvertedModPolicy.ShouldMipmap("Assets/UI/BOOK00I0.IMG.png", markers),
                  "an IMG image is drawn 1:1 and gets none");
            Check(!MobileConvertedModPolicy.ShouldMipmap("Assets/Art/TFAC00I0.RCI_0-0.png", markers),
                  "a paperdoll/portrait RCI record gets none");
            Check(!MobileConvertedModPolicy.ShouldMipmap("Assets/Art/WEAPON01.CIF_3-2.png", markers),
                  "a CIF weapon frame gets none");
            Check(!MobileConvertedModPolicy.ShouldMipmap("Assets/Textures/CifRci/anything.png", markers),
                  "DFU's own CifRci directory is recognised as well as the name");
            Check(MobileConvertedModPolicy.ShouldMipmap("Assets/Textures/Images/004_0-0.png", markers),
                  "a folder merely called Images is not the .img marker");
            // The rule is overridable wholesale, because the real pack's internal paths have not
            // been inspected and a silently wrong guess is exactly what must not ship.
            string[] custom = MobileConvertedModPolicy.ParseList(" /paperdoll/ , .ui ", markers);
            Check(custom.Length == 2 && custom[0] == "/paperdoll/" && custom[1] == ".ui",
                  "the no-mipmap list is overridable and trimmed");
            Check(!MobileConvertedModPolicy.ShouldMipmap("Assets/x/Paperdoll/a.png", custom)
                  && MobileConvertedModPolicy.ShouldMipmap("Assets/UI/BOOK00I0.IMG.png", custom),
                  "an override replaces the defaults rather than adding to them");
            Check(MobileConvertedModPolicy.ParseList(null, markers) == markers
                  && MobileConvertedModPolicy.ParseList("  ", markers) == markers,
                  "unset or blank keeps DFU's derived defaults");

            // The same marker list now decides a second, heavier question: whether an asset has
            // a PIXEL-EXACT contract with DFU's code. UI art is drawn 1:1 (no mipmaps) and is
            // also sliced with GetPixels rects computed from its own dimensions, so neither its
            // size nor its format may be optimised.
            Check(MobileConvertedModPolicy.IsClassicUiArt("Assets/UI/TALK01I0.IMG.png", markers)
                  && MobileConvertedModPolicy.IsClassicUiArt("Assets/Art/WEAPON01.CIF_3-2.png", markers)
                  && MobileConvertedModPolicy.IsClassicUiArt("Assets/Art/TFAC00I0.RCI_0-0.png", markers),
                  "classic UI art is recognised for the dimension/format contract");
            Check(!MobileConvertedModPolicy.IsClassicUiArt("Assets/Textures/004_0-0.png", markers)
                  && !MobileConvertedModPolicy.IsClassicUiArt("Assets/Textures/210_1-0_Normal.png", markers),
                  "world textures are not, and keep the memory-optimised policy");
            // Terrain tiles are the class the POT rounding must NOT touch: DFU builds a
            // Texture2DArray sized from the first replacement record and silently drops any
            // record whose width/height/format differs, which is a hole in the terrain rather
            // than a visible error.
            Check(MobileConvertedModPolicy.IsTerrainTileTexture("Assets/Textures/302_5-0.png")
                  && MobileConvertedModPolicy.IsTerrainTileTexture("Assets/Textures/002_0-0.png")
                  && MobileConvertedModPolicy.IsTerrainTileTexture("Assets/Textures/404_55-0_Normal.png"),
                  "terrain tile archives are recognised (ground sets + winter/rain variants)");
            Check(!MobileConvertedModPolicy.IsTerrainTileTexture("Assets/Textures/210_1-0.png")
                  && !MobileConvertedModPolicy.IsTerrainTileTexture("Assets/Textures/TALK01I0.IMG.png")
                  && !MobileConvertedModPolicy.IsTerrainTileTexture("Assets/Textures/nonsense.png"),
                  "a billboard archive, UI art and an unnumbered name are not terrain tiles");
            Check(MobileConvertedModPolicy.UiFormat == TextureImporterFormat.ASTC_4x4
                  && MobileConvertedModPolicy.MaxUiTextureSize == 16384,
                  "UI art takes the 4x4 block and no size cap",
                  MobileConvertedModPolicy.UiFormat + " / " + MobileConvertedModPolicy.MaxUiTextureSize);

            // Which source formats count as compressed. The list is of COMPRESSED families so an
            // unfamiliar format reads as uncompressed - the safe direction, since being wrong
            // that way costs size and the other way breaks a window.
            Check(MobileModExtractor.IsCompressedFormat(TextureFormat.BC7)
                  && MobileModExtractor.IsCompressedFormat(TextureFormat.DXT5)
                  && MobileModExtractor.IsCompressedFormat(TextureFormat.DXT1Crunched)
                  && MobileModExtractor.IsCompressedFormat(TextureFormat.ASTC_6x6),
                  "block-compressed formats are recognised");
            Check(!MobileModExtractor.IsCompressedFormat(TextureFormat.RGBA32)
                  && !MobileModExtractor.IsCompressedFormat(TextureFormat.ARGB32)
                  && !MobileModExtractor.IsCompressedFormat(TextureFormat.RGB24)
                  && !MobileModExtractor.IsCompressedFormat(TextureFormat.Alpha8),
                  "the uncompressed layouts an author leaves UI art in are not");

            // The audio half of the policy: songs stream, effects sit compressed in memory.
            // Both directions cost something real if they are got wrong - a resident song is
            // megabytes the device never gets back, a streamed effect misses the frame it was
            // triggered on - and the streaming side is the only part of this policy that is
            // NOT what Unity would have done anyway, which makes it the part worth a test. It
            // is checked here rather than through a fixture because reaching it needs a file
            // over 2MB, and committing 2MB of silence to prove a comparison is not a trade
            // this repo should make.
            const long mb = 1024 * 1024;
            Check(MobileConvertedModPolicy.LoadTypeForSize(64 * 1024)
                      == AudioClipLoadType.CompressedInMemory,
                  "a sound effect stays compressed in memory, never streamed");
            Check(MobileConvertedModPolicy.LoadTypeForSize(30 * mb) == AudioClipLoadType.Streaming,
                  "a song streams instead of sitting resident");
            // The threshold is read against the extraction's own output, which is always
            // uncompressed 16-bit PCM, so it is a duration rule wearing a size: 2MB is ~12s of
            // mono 22kHz. Both sides of the boundary are pinned so a later "just round it up"
            // cannot quietly move songs into memory.
            Check(MobileConvertedModPolicy.LoadTypeForSize(MobileConvertedModPolicy.StreamingThresholdBytes)
                      == AudioClipLoadType.CompressedInMemory
                  && MobileConvertedModPolicy.LoadTypeForSize(
                      MobileConvertedModPolicy.StreamingThresholdBytes + 1)
                      == AudioClipLoadType.Streaming,
                  "the boundary itself is an effect; one byte past it is a song");
            Check(MobileConvertedModPolicy.StreamingThresholdBytes == 2 * mb
                  && Mathf.Abs(MobileConvertedModPolicy.VorbisQuality - 0.7f) < 0.001f,
                  "the audio policy's two constants are the argued-for ones",
                  MobileConvertedModPolicy.StreamingThresholdBytes + "B q"
                      + MobileConvertedModPolicy.VorbisQuality);
        }

        /// <summary>
        /// The fetched-pack importer's raw-data exception, as a pure rule. Most bundled packs
        /// want ASTC 6x6 and no CPU copy, but World of Daggerfall - Biomes has two texture
        /// classes that ASTC breaks outright: its climate_map.png is a colour-key image read
        /// back with GetPixel and compared EXACTLY (a lossy block would move #FFA500 by a
        /// digit and the biome would simply never match), and its 224 terrain tiles are
        /// point-filtered records DFU decompresses into an ARGB32 Texture2DArray anyway, so
        /// compressing them buys nothing and risks the silent record-drop that a format
        /// mismatch causes. Keyed on the mod FOLDER name, which is the mods.json entry name.
        ///
        /// The second exception, LinearData, is World of Daggerfall - Terrain: five world maps a
        /// compute shader samples as numbers. Those need sRGB off (the project is Linear, so the
        /// default sRGB read would remap every value) and no compression, but no CPU copy.
        /// </summary>
        // MOBILE: ModResources/, not upstream's Resources/. A folder literally named Resources under
        // Assets/ is packed into every player build by Unity, unconditionally - which would put this
        // pending-licence mod's 3.5 MB of data in a public IPA outside every private_only guard - so
        // tools/bundled-mods/fetch.py renames it on the way in (mods.json "rename_dirs"), and
        // fetch.py --check refuses any fetched mod that still holds one.
        const string DistantDerivMap = "Assets/Game/Mods/DistantTerrainWoD/ModResources/daggerfall_deriv_map.png";

        static void TestPackTextureRules()
        {
            Check(MobileModPackTextureRules.For("Assets/Game/Mods/WorldOfDaggerfallBiomes/Assets/Maps/climate_map.png") == MobileModPackTextureRules.Rule.RawData,
                "PackTextureRules: the Biomes climate map is raw data");
            Check(MobileModPackTextureRules.For("Assets/Game/Mods/WorldOfDaggerfallBiomes/Textures/Terrain/Subtropical/004_0-0.png") == MobileModPackTextureRules.Rule.RawData,
                "PackTextureRules: Biomes terrain tiles are raw data");
            Check(MobileModPackTextureRules.For("Assets/Game/Mods/WorldOfDaggerfall/Textures/Rocks/rock_01.png") == MobileModPackTextureRules.Rule.Default,
                "PackTextureRules: other mods keep the default ASTC rule");
            Check(MobileModPackTextureRules.For("Assets\\Game\\Mods\\WorldOfDaggerfallBiomes\\Assets\\Maps\\climate_map.png") == MobileModPackTextureRules.Rule.RawData,
                "PackTextureRules: backslash paths are normalized");
            Check(MobileModPackTextureRules.NoMips("Assets/Game/Mods/WorldOfDaggerfallBiomes/Assets/Maps/climate_map.png")
                  && !MobileModPackTextureRules.NoMips("Assets/Game/Mods/WorldOfDaggerfallBiomes/Textures/Terrain/Subtropical/004_0-0.png"),
                "PackTextureRules: only the climate map drops mips");
            // MOBILE: the second exception class. World of Daggerfall - Terrain's five world maps are
            // read as NUMBERS by a compute shader (heights, biome weights, port flags), never shown, so
            // they need the opposite of the default path in two independent ways: sRGB sampling would
            // remap every value (the project is Linear) and block compression would quantise them.
            // Unlike RawData they stay non-readable - only the GPU reads them.
            Check(MobileModPackTextureRules.For("Assets/Game/Mods/WorldOfDaggerfallTerrain/Assets/Maps/daggerfall_deriv_map.png") == MobileModPackTextureRules.Rule.LinearData,
                "PackTextureRules: the Terrain world maps are linear data (sRGB off, uncompressed)");
            Check(MobileModPackTextureRules.For("Assets/Game/Mods/WorldOfDaggerfallBiomes/Assets/Maps/climate_map.png") == MobileModPackTextureRules.Rule.RawData,
                "PackTextureRules: Biomes stays raw data after adding the linear rule");
            Check(MobileModPackTextureRules.LinearDataMods.Contains("WorldOfDaggerfallTerrain"), "PackTextureRules: Terrain is in the linear-data list");
            // MOBILE: every texture the shipped compute shaders read is a level-0 fetch - SampleLevel(
            // ..., 0) in TerrainComputer.compute and basicRoads.cginc, "float sampleLevel = 0" in
            // heightSampling.cginc - so a mip chain on these maps is memory nothing can ever sample:
            // ~11 MB of GPU residency across the five (a 2048x1024 RGBA32 map costs 8 MB, its chain
            // another 2.7 MB). The importer branch reads this constant, so pinning it here pins the
            // import; the meta's "enableMipMap: 0" is the other half of the proof.
            Check(MobileModPackTextureRules.NoMipsForLinearData,
                "PackTextureRules: linear-data maps carry no mip chain (the compute shaders only fetch level 0)");
            // A name in BOTH lists would silently take whichever loop For() walks first (raw data),
            // giving a compute-only data map the readable, mipped, sRGB-untouched treatment - the
            // opposite of what it needs, with nothing in any log to say which rule won.
            Check(MobileModPackTextureRules.ListsDisjoint,
                "PackTextureRules: no mod name is in both the raw-data and the linear-data list");
            // MOBILE: Distant Terrain (WoD flavour). Its daggerfall_deriv_map.png is the river/coast
            // mask DistantTerrain.ApplyDerivativeHeightmap reads with GetPixels32 and compares each
            // pixel against a 0-255 threshold, so it needs the raw-data treatment (readable,
            // uncompressed) for the same reason the Biomes colour key does.
            Check(MobileModPackTextureRules.For(DistantDerivMap) == MobileModPackTextureRules.Rule.RawData,
                "PackTextureRules: the Distant Terrain deriv map is raw data");
            Check(MobileModPackTextureRules.RawDataMods.Contains("DistantTerrainWoD"),
                "PackTextureRules: Distant Terrain is in the raw-data list");
            // Nothing samples this map on the GPU - it is CPU-read once at world entry - so the mip
            // chain is pure memory. (The Biomes tiles ARE drawn, which is why NoMips is per-file.)
            Check(MobileModPackTextureRules.NoMips(DistantDerivMap),
                "PackTextureRules: the Distant Terrain deriv map drops mips");
            // The map is 8-bit greyscale and the carve reads .r only, so one channel is all that is
            // needed: R8 instead of RGBA32 is a 4x saving on both the GPU copy and the readable CPU
            // copy this texture is obliged to keep.
            Check(MobileModPackTextureRules.SingleChannel(DistantDerivMap)
                  && !MobileModPackTextureRules.SingleChannel("Assets/Game/Mods/WorldOfDaggerfallBiomes/Assets/Maps/climate_map.png"),
                "PackTextureRules: only the Distant Terrain deriv map imports single-channel");
            // MOBILE: World of Daggerfall - Terrain ships a DIFFERENT file with the SAME name
            // (RGBA, 5000x2500, a compute-shader input) at WorldOfDaggerfallTerrain/Assets/Maps/.
            // Keying either rule on the bare file name would silently drag it into the wrong branch:
            // R8 would throw away three of its channels and NoMips would change what the compute
            // shader reads. Both rules are keyed on mod folder + file name for exactly this reason.
            const string terrainDerivMap = "Assets/Game/Mods/WorldOfDaggerfallTerrain/Assets/Maps/daggerfall_deriv_map.png";
            Check(!MobileModPackTextureRules.SingleChannel(terrainDerivMap)
                  && !MobileModPackTextureRules.NoMips(terrainDerivMap)
                  && MobileModPackTextureRules.For(terrainDerivMap) == MobileModPackTextureRules.Rule.LinearData,
                "PackTextureRules: the Terrain mod's same-named deriv map is untouched by the Distant Terrain rules");
            // MOBILE: the assumption the R8 override rests on. Texture2D.GetPixels32 documents a
            // limited format list; if R8 did not survive it, the carve would read zeros and every
            // far-terrain cell would become ocean. Cheaper to pin here than to find out on a device.
            bool r8ok = false;
            string r8detail = "";
            try
            {
                var probe = new Texture2D(2, 2, TextureFormat.R8, false);
                probe.LoadRawTextureData(new byte[] { 0, 64, 128, 255 });
                probe.Apply(false, false);
                Color32[] back = probe.GetPixels32();
                r8ok = back.Length == 4 && back[0].r == 0 && back[1].r == 64 && back[2].r == 128 && back[3].r == 255;
                r8detail = "GetPixels32 .r = " + string.Join(",", back.Select(c => c.r.ToString()));
                UnityEngine.Object.DestroyImmediate(probe);
            }
            catch (Exception ex) { r8detail = ex.GetType().Name + ": " + ex.Message; }
            Check(r8ok, "PackTextureRules: R8 survives GetPixels32 in .r (what the deriv carve reads)", r8detail);
            // MOBILE: the deriv map is 5000x2500 on disk and the importer clamps it to maxTextureSize
            // 2048 (same value for the editor platform and for iOS, so the editor's imported size IS
            // the shipped size). The carve maps the image onto the 1000x500 world-map grid by the
            // texture's OWN width/height, so any size at or above the grid is correct; what matters is
            // that it still oversamples the grid, or thin rivers would be lost between cells.
            var derivTex = AssetDatabase.LoadAssetAtPath<Texture2D>(DistantDerivMap);
            Check(derivTex != null && derivTex.width >= 1000 && derivTex.height >= 500,
                "PackTextureRules: the imported deriv map still oversamples the 1000x500 world grid",
                derivTex == null ? "asset did not load" : derivTex.width + "x" + derivTex.height);
            // MOBILE: the rules above are pure functions; these four are the IMPORT they are supposed
            // to produce, asserted against the asset on disk. Until now R8, readability, the absent
            // mip chain and the iOS 2048 clamp were proven only by a one-off manual sabotage/restore
            // whose .meta snapshots live in a gitignored workspace and will never run again - so an
            // importer edit that quietly reverted this file to RGBA32, or re-enabled mips, left the
            // suite green and cost 12 MB and a broken carve on a device. The asset is fetched
            // content, so a clone that has not run fetch.py skips them by name rather than adding a
            // second permanent FAIL beside the oversample check above.
            if (derivTex == null)
            {
                log.AppendLine("  SKIP  the Distant Terrain deriv map's import guarantees (R8 / readable / no mips / iOS 63-2048-overridden) - not fetched, run tools/bundled-mods/fetch.py --only DistantTerrainWoD");
            }
            else
            {
                // R8, not RGBA32: the carve reads .r only, so three of four channels would be waste
                // on both the GPU copy and the readable CPU copy this texture is obliged to keep.
                Check(derivTex.format == TextureFormat.R8,
                    "PackTextureRules: the imported deriv map is R8 (a quarter of RGBA32, on both copies)",
                    derivTex.format.ToString());
                // ApplyDerivativeHeightmap calls GetPixels32 on it at world entry.
                Check(derivTex.isReadable,
                    "PackTextureRules: the imported deriv map is readable (ApplyDerivativeHeightmap reads it with GetPixels32)");
                // Nothing samples it on the GPU, so a mip chain is memory nothing can ever reach.
                Check(derivTex.mipmapCount == 1,
                    "PackTextureRules: the imported deriv map carries no mip chain",
                    derivTex.mipmapCount + " mip levels");
                // And the half the editor's own imported texture cannot show: the iOS override. The
                // default platform block resolves to R8 here too, so only this asks the shipped
                // question - format 63 (TextureImporterFormat.R8), 2048, and actually overridden
                // rather than inheriting whatever the default block happens to say.
                var derivImporter = AssetImporter.GetAtPath(DistantDerivMap) as TextureImporter;
                TextureImporterPlatformSettings iosSettings =
                    derivImporter != null ? derivImporter.GetPlatformTextureSettings("iPhone") : null;
                Check(iosSettings != null && iosSettings.overridden
                      && iosSettings.format == TextureImporterFormat.R8
                      && iosSettings.maxTextureSize == 2048,
                    "PackTextureRules: the deriv map's iOS override is R8 (63) at 2048 and is overridden",
                    iosSettings == null ? "no TextureImporter at " + DistantDerivMap
                        : "format " + iosSettings.format + " (" + (int)iosSettings.format + "), maxTextureSize "
                          + iosSettings.maxTextureSize + ", overridden " + iosSettings.overridden);
            }
            // MOBILE: and the reason the literal above says ModResources/. Unity packs the contents of
            // every folder named `Resources` under Assets/ into every player build - no reference
            // needed, no launcher switch consulted, no way to exclude it - so a fetched mod that kept
            // upstream's Resources/ would ship its data inside every IPA, outside the private_only
            // guard that keeps a pending-licence mod out of the public pack. fetch.py renames it; this
            // is the editor-side half of that guard (fetch.py --check is the other).
            string[] fetchedResourceDirs = Directory.Exists("Assets/Game/Mods")
                ? Directory.GetDirectories("Assets/Game/Mods", "Resources", SearchOption.AllDirectories)
                : new string[0];
            Check(fetchedResourceDirs.Length == 0,
                "PackTextureRules: no fetched mod holds a Unity Resources/ folder (it would ship in every IPA)",
                fetchedResourceDirs.Length > 0 ? string.Join(", ", fetchedResourceDirs) : "none under Assets/Game/Mods");
            // Every check above pins the rule against the SAME literal the rule holds, so a rename or a
            // case change of the mods.json entry - which is the fetched folder name, which is what For()
            // matches - leaves this suite green while 225 textures silently revert to ASTC and the
            // colour key stops matching. Tie the literal to the pin list instead. (Precedent for reading
            // mods.json: TestBundledModManifests.)
            string modPins = "";
            try { modPins = File.ReadAllText("tools/bundled-mods/mods.json"); } catch (Exception) { }
            string[] pinnedRules = MobileModPackTextureRules.RawDataMods
                .Concat(MobileModPackTextureRules.LinearDataMods).ToArray();
            string[] unpinned = pinnedRules
                .Where(m => !System.Text.RegularExpressions.Regex.IsMatch(
                    modPins, "\"name\"\\s*:\\s*\"" + System.Text.RegularExpressions.Regex.Escape(m) + "\""))
                .ToArray();
            Check(modPins.Length > 0 && pinnedRules.Length > 0 && unpinned.Length == 0,
                "PackTextureRules: every raw-data and linear-data mod name is a mods.json entry name",
                unpinned.Length > 0 ? "not in mods.json: " + string.Join(", ", unpinned)
                                    : pinnedRules.Length + " names, " + modPins.Length + "B of pins");
            // MOBILE: an AssetPostprocessor whose GetVersion() never changes has its import results
            // cached against a hash that does not know the rules moved. Every rule change above -
            // RawData, LinearData, R8, the 2048 clamp, NoMips - therefore relied on someone forcing a
            // reimport by hand, and ApplyAll does not force pack folders. A silently stale texture in
            // a bundle looks right in the Editor and is wrong on the device, which is the worst
            // failure mode this pipeline has. Both pack postprocessors must therefore override it.
            Check(new MobileModPackTextureImporter().GetVersion() >= 1
                  && new MobileModTextureImporter().GetVersion() >= 1,
                "PackTextureRules: both texture postprocessors version their rules, so a rule change reimports",
                "pack " + new MobileModPackTextureImporter().GetVersion()
                + ", pilot " + new MobileModTextureImporter().GetVersion());
        }

        /// <summary>
        /// The reverse direction of the mod pipeline. Third-party mods (DREAM-class) ship
        /// only as desktop AssetBundles, so iOS support means unpacking one back into loose
        /// project assets and repacking it. This packs a synthetic desktop .dfmod, extracts
        /// it, and rebuilds - what survives the full circle is what a converted mod gets.
        ///
        /// NEEDS A REAL GRAPHICS DEVICE: the bundle texture is compressed and non-readable, so
        /// extracting it goes through a GPU blit. Do not run this suite with -nographics.
        /// </summary>
        static void TestModExtractorRoundTrip()
        {
            const string fixtureManifest = "Assets/Editor/TestFixtures/ExtractorFixture/fixture-mod.dfmod.json";
            const string bundleDir = "Temp/MobileModExtractorTest";
            const string extractRoot = "Assets/Game/Mods/Converted/__test__";
            if (Directory.Exists(bundleDir)) Directory.Delete(bundleDir, true);
            if (Directory.Exists(extractRoot)) { Directory.Delete(extractRoot, true); File.Delete(extractRoot + ".meta"); AssetDatabase.Refresh(); }

            // 1. Make a "desktop mod" the way the outside world does: build for StandaloneOSX.
            string[] built = MobileModBuilder.BuildMod(fixtureManifest, bundleDir,
                new[] { BuildTarget.StandaloneOSX });

            // 2. Extract it back.
            var report = MobileModExtractor.Extract(built[0], extractRoot);
            Check(File.Exists(report.manifestPath), "extractor writes a manifest", report.manifestPath);
            Check(report.extracted.Count == 10, "extractor writes eight textures + textasset + audio clip",
                  "extracted=" + report.extracted.Count);

            // 3. Path tail and short names preserved.
            string tex = report.extracted.Find(p => p.EndsWith("fixture_tex.png"));
            string txt = report.extracted.Find(p => p.EndsWith("fixture_data.json"));
            string nrm = report.extracted.Find(p => p.EndsWith("fixture_wall_Normal.png"));
            string hgt = report.extracted.Find(p => p.EndsWith("fixture_wall_Height.png"));
            string wav = report.extracted.Find(p => p.EndsWith("fixture_beep.wav"));
            string rdbl = report.extracted.Find(p => p.EndsWith("fixture_readable.png"));
            string uiArt = report.extracted.Find(p => p.EndsWith("fixture_ui.IMG.png"));
            string uiCmp = report.extracted.Find(p => p.EndsWith("fixture_uic.CIF.png"));
            // Mod.FindAssetNames accepts an asset whose directory ENDS WITH the requested one
            // and compares with a case-sensitive CompareOrdinal, while callers pass literal
            // capitalised paths ("Assets/Textures"). AssetBundle.GetAllAssetNames hands back
            // everything lowercased, so the extraction has to recover the manifest's own casing
            // - and keep the leading "Assets/" - or a converted mod silently loses loose-file
            // injection while every other check here still passes.
            Check(tex != null && tex.Replace('\\', '/').Contains(
                      "/Assets/Editor/TestFixtures/ExtractorFixture/fixture_tex.png"),
                  "manifest path casing and Assets/ prefix preserved", tex);
            Check(!report.notesByType.ContainsKey("unlisted-in-manifest"),
                  "every bundle asset matched a manifest entry (casing recoverable)");
            Check(txt != null && File.ReadAllText(txt).Contains("\"value\":42"),
                  "textasset bytes preserved");

            // fixture_tex.tga and fixture_tex.png collapse onto one output path once the texture
            // extension is rewritten. Overwriting would lose an asset and list the survivor twice
            // in the rebuilt manifest, so the clash must be reported instead. Both fixtures carry
            // the same pixels, so which one wins does not change anything else in this test.
            int collisions;
            report.skippedByType.TryGetValue("collision", out collisions);
            Check(collisions == 1, "colliding output path reported, not overwritten",
                  "collision=" + collisions);
            // The two counters answer different questions and the boundary between them is
            // exactly here. fixture_lone.tga has no .png twin: it really is extracted, under a
            // changed runtime lookup name, so it earns the note. fixture_tex.tga is ALSO an
            // extension rewrite, but it loses the collision and never reaches disk - and a note
            // is a claim about a survivor, so it must not be counted. One rewrite, not two: a
            // note banked before the write would report an asset as both rewritten-and-extracted
            // and skipped, in the same run.
            string lone = report.extracted.Find(p => p.EndsWith("fixture_lone.png"));
            Check(lone != null && File.Exists(lone),
                  "a .tga with no .png twin is extracted as .png", lone ?? "missing");
            int rewritten;
            report.notesByType.TryGetValue("extension-rewritten", out rewritten);
            Check(rewritten == 1, "only the rewrite that actually reached disk is noted",
                  "extension-rewritten=" + rewritten);

            // 3b. THE CHECK THAT MATTERS for textures. Everything above passes on a blank
            // image: the name, the path, the size and the manifest are all still right when
            // the pixels are gone. The bundle texture is DXT1 and non-readable, so extraction
            // must go through the GPU blit, and a blit with no graphics device is a silent
            // no-op that yields a uniform grey. Compare against the fixture's generator
            // pattern - pixel (x,y) = (4x, 4y, (x^y)*4) - which only real decoded data matches.
            var decoded = new Texture2D(2, 2);
            bool loaded = tex != null && decoded.LoadImage(File.ReadAllBytes(tex));
            Check(loaded && decoded.width == 64 && decoded.height == 64,
                  "extracted png decodes at 64x64",
                  loaded ? decoded.width + "x" + decoded.height : "did not decode");

            Color32[] px = loaded ? decoded.GetPixels32() : new Color32[0];
            var seen = new HashSet<int>();
            foreach (Color32 c in px)
                seen.Add((c.r << 16) | (c.g << 8) | c.b);
            Check(seen.Count > 100, "extracted texture is not a solid fill",
                  "distinct colours=" + seen.Count + " (1 means the blit produced a flat fill)");

            // DXT1 is lossy, so allow a margin - but one far tighter than a grey wash.
            const int dxtTolerance = 16;
            int[,] samples = { { 0, 0 }, { 17, 42 }, { 32, 32 }, { 63, 63 }, { 20, 40 }, { 5, 58 } };
            int worst = 0;
            string worstAt = "none";
            for (int i = 0; loaded && i < samples.GetLength(0); i++)
            {
                int x = samples[i, 0], y = samples[i, 1];
                // GetPixels32 runs bottom-up; the fixture pattern is written top-down.
                Color32 got = px[(63 - y) * 64 + x];
                int dr = Mathf.Abs(got.r - 4 * x % 256);
                int dg = Mathf.Abs(got.g - 4 * y % 256);
                int db = Mathf.Abs(got.b - (x ^ y) * 4 % 256);
                int d = Mathf.Max(dr, Mathf.Max(dg, db));
                if (d > worst) { worst = d; worstAt = string.Format("({0},{1}) got {2},{3},{4} want {5},{6},{7}",
                    x, y, got.r, got.g, got.b, 4 * x % 256, 4 * y % 256, (x ^ y) * 4 % 256); }
            }
            Check(loaded && worst <= dxtTolerance, "extracted pixels match the fixture pattern",
                  "worst channel delta=" + worst + " at " + worstAt);
            UnityEngine.Object.DestroyImmediate(decoded);

            // 3b-bis. THE READABLE-AND-COMPRESSED CASE, which is what real mod art actually is
            // and which every fixture above misses. fixture_readable.png is fixture_tex.png with
            // Read/Write Enabled ticked and nothing else changed, so it arrives in the bundle
            // block-compressed AND readable - and Texture2D.EncodeToPNG serialises only a few
            // uncompressed layouts, so on that texture it returns NULL. Silently: it does not
            // throw, so a fast path that treats only exceptions as a decline hands the null
            // straight out, and the write turns it into "ArgumentNullException: Value cannot be
            // null" with no texture named anywhere in it.
            //
            // That is not a hypothetical either. It cost 180 of the 330 textures in DREAM's
            // "hud & menu" module - every readable BC7 one - while the other 150 converted, so
            // the module looked like a partial success rather than a bug. The pixel comparison
            // is the half that matters: it proves the blit actually took over and produced the
            // real image, rather than the null merely being swapped for a blank.
            Check(rdbl != null && File.Exists(rdbl),
                  "a readable COMPRESSED texture extracts at all (EncodeToPNG returns null on it)",
                  rdbl ?? "missing");
            int noContent;
            report.skippedByType.TryGetValue("no-content", out noContent);
            Check(noContent == 0 && !report.skippedByType.ContainsKey("write-failed"),
                  "and it does not arrive at the write as a null buffer",
                  "no-content=" + noContent);
            var rdec = new Texture2D(2, 2);
            bool rLoaded = rdbl != null && rdec.LoadImage(File.ReadAllBytes(rdbl));
            Check(rLoaded && rdec.width == 64 && rdec.height == 64,
                  "extracted readable-compressed png decodes at 64x64",
                  rLoaded ? rdec.width + "x" + rdec.height : "did not decode");
            Color32[] rpx = rLoaded ? rdec.GetPixels32() : new Color32[0];
            int rWorst = 0;
            string rWorstAt = "none";
            for (int i = 0; rLoaded && i < samples.GetLength(0); i++)
            {
                int x = samples[i, 0], y = samples[i, 1];
                Color32 got = rpx[(63 - y) * 64 + x];       // GetPixels32 is bottom-up
                int d = Mathf.Max(Mathf.Abs(got.r - 4 * x % 256),
                        Mathf.Max(Mathf.Abs(got.g - 4 * y % 256),
                                  Mathf.Abs(got.b - (x ^ y) * 4 % 256)));
                if (d > rWorst) { rWorst = d; rWorstAt = string.Format("({0},{1}) got {2},{3},{4}",
                    x, y, got.r, got.g, got.b); }
            }
            Check(rLoaded && rWorst <= dxtTolerance,
                  "the blit took over and produced the real image, not a blank",
                  "worst channel delta=" + rWorst + " at " + rWorstAt);
            UnityEngine.Object.DestroyImmediate(rdec);

            // 3c. THE SAME CHECK FOR NORMAL MAPS, where "the bytes came out" is even further
            // from "the asset survived". Unity does not store a normal map as an image of one:
            // it throws the blue channel away and swizzles what is left into the two channels
            // its block format codes best, so a byte-for-byte extraction produces a white image
            // (DXT5nm) or a blue-less one (BC5) that re-imports as an ordinary colour texture
            // and lights nothing. The fixture is generated from real unit normals, so a correct
            // extraction reproduces all three channels of the source pattern; a wrong one misses
            // blue by ~100. Which swizzle Unity actually used is recorded rather than assumed.
            int unswizzled = 0;
            string layout = "none";
            foreach (var kv in report.notesByType)
                if (kv.Key.StartsWith("normal-")) { layout = kv.Key; unswizzled += kv.Value; }
            // The layout is named in the check itself rather than asserted: which of the two
            // swizzles Unity picks is a build-target and Unity-version decision, and pinning it
            // would make this a test of Unity. What must hold is that exactly one normal map was
            // recognised and classified - the pixel comparison below is what proves the branch
            // chosen was the right one, since the wrong one misses blue by about 100.
            Check(unswizzled == 1, "normal map recognised, layout recorded: " + layout,
                  "notes=" + string.Join(",", new List<string>(report.notesByType.Keys).ToArray()));

            var dn = new Texture2D(2, 2);
            bool nLoaded = nrm != null && dn.LoadImage(File.ReadAllBytes(nrm));
            Check(nLoaded && dn.width == 64 && dn.height == 64, "extracted normal png decodes at 64x64",
                  nLoaded ? dn.width + "x" + dn.height : "did not decode");
            Color32[] npx = nLoaded ? dn.GetPixels32() : new Color32[0];
            int nWorst = 0;
            string nWorstAt = "none";
            for (int i = 0; nLoaded && i < samples.GetLength(0); i++)
            {
                int x = samples[i, 0], y = samples[i, 1];
                Color32 got = npx[(63 - y) * 64 + x];       // GetPixels32 is bottom-up
                // The fixture's generator: a unit normal fanning out across the square.
                float fx = ((x / 63f) * 2f - 1f) * 0.5f;
                float fy = ((y / 63f) * 2f - 1f) * 0.5f;
                float fz = Mathf.Sqrt(Mathf.Max(0f, 1f - fx * fx - fy * fy));
                int wr = Mathf.RoundToInt((fx * 0.5f + 0.5f) * 255f);
                int wg = Mathf.RoundToInt((fy * 0.5f + 0.5f) * 255f);
                int wb = Mathf.RoundToInt((fz * 0.5f + 0.5f) * 255f);
                int d = Mathf.Max(Mathf.Abs(got.r - wr),
                        Mathf.Max(Mathf.Abs(got.g - wg), Mathf.Abs(got.b - wb)));
                if (d > nWorst) { nWorst = d; nWorstAt = string.Format("({0},{1}) got {2},{3},{4} want {5},{6},{7}",
                    x, y, got.r, got.g, got.b, wr, wg, wb); }
            }
            Check(nLoaded && nWorst <= 16, "extracted normal map reconstructs x, y AND z",
                  "worst channel delta=" + nWorst + " at " + nWorstAt);
            UnityEngine.Object.DestroyImmediate(dn);

            // 3c-bis. THE DEGAMMA PIN. The blit's gamma behaviour is decided by the SOURCE
            // texture's graphics format, never by what the file is called. fixture_wall_Height
            // is deliberately left sRGB - which is what a mod author who never touched the
            // importer ships, and the real DREAM texture pack does contain *_Height assets - so
            // a converter that picked "linear" from the "_Height" suffix would sample it
            // degamma'd with nothing re-encoding on write. That is not a rounding error: a
            // mid-tone 128 comes out as 55. The fixture is a ramp through every mid-tone, so
            // any sRGB/linear mix-up in either direction lands far outside this tolerance.
            var dh = new Texture2D(2, 2);
            bool hLoaded = hgt != null && dh.LoadImage(File.ReadAllBytes(hgt));
            Check(hLoaded && dh.width == 64 && dh.height == 64, "extracted height png decodes at 64x64",
                  hLoaded ? dh.width + "x" + dh.height : "did not decode");
            Color32[] hpx = hLoaded ? dh.GetPixels32() : new Color32[0];
            int hWorst = 0;
            string hWorstAt = "none";
            for (int i = 0; hLoaded && i < samples.GetLength(0); i++)
            {
                int x = samples[i, 0], y = samples[i, 1];
                Color32 got = hpx[(63 - y) * 64 + x];       // GetPixels32 is bottom-up
                int want = ((x + y) * 2) % 256;            // the fixture's generator
                int d = Mathf.Max(Mathf.Abs(got.r - want),
                        Mathf.Max(Mathf.Abs(got.g - want), Mathf.Abs(got.b - want)));
                if (d > hWorst) { hWorst = d; hWorstAt = string.Format("({0},{1}) got {2},{3},{4} want {5}",
                    x, y, got.r, got.g, got.b, want); }
            }
            Check(hLoaded && hWorst <= 16, "sRGB-flagged height map survives without a degamma",
                  "worst channel delta=" + hWorst + " at " + hWorstAt);
            UnityEngine.Object.DestroyImmediate(dh);

            // 3d. The import policy that makes a multi-gigabyte pack fit on the device. The
            // extraction lands under Assets/Game/Mods/Converted/, which MobileConvertedModImporter
            // owns, so the settings below are the postprocessor's doing and not Unity's defaults.
            // A normal map imported as a colour texture is silently wrong in exactly the way this
            // whole test exists to catch, and npotScale is the one asserted setting Unity does NOT
            // default to - it pins that the postprocessor actually ran on the colour texture too.
            var nrmImp = AssetImporter.GetAtPath(nrm) as TextureImporter;
            Check(nrmImp != null && nrmImp.textureType == TextureImporterType.NormalMap,
                  "extracted *_Normal re-imports as a normal map",
                  nrmImp == null ? "no importer" : nrmImp.textureType.ToString());
            var hgtImp = AssetImporter.GetAtPath(hgt) as TextureImporter;
            Check(hgtImp != null && hgtImp.textureType == TextureImporterType.Default
                  && !hgtImp.sRGBTexture,
                  "extracted *_Height re-imports as linear data, not colour",
                  hgtImp == null ? "no importer" : "sRGB=" + hgtImp.sRGBTexture);
            var texImp = AssetImporter.GetAtPath(tex) as TextureImporter;
            Check(texImp != null && texImp.textureType == TextureImporterType.Default
                  && texImp.sRGBTexture,
                  "extracted colour texture stays sRGB colour",
                  texImp == null ? "no importer" : texImp.textureType.ToString());
            // Unity already defaults isReadable false, Compressed and mipmapEnabled true, so
            // those three would pass with the postprocessor deleted - they are regression pins,
            // not proof it ran. The four below are not Unity defaults and cannot pass by
            // accident: npotScale defaults to ToNearest, maxTextureSize to 2048, and a texture
            // has no iOS platform override at all until something writes one.
            Check(texImp != null
                  && texImp.textureCompression == TextureImporterCompression.Compressed,
                  "converted textures are compressed");

            // THE READ/WRITE FLAG IS THE AUTHOR'S, AND THE CONVERTER MUST NOT OVERRIDE IT.
            // Forcing it off saved a CPU-side copy and froze the game on a device: DFU's
            // TryImportTexture only LOGS when a non-readable texture reaches a caller that needs
            // pixels and returns it anyway, and ImageReader's GetPixels32 then throws - every
            // frame, inside the UI draw loop, which looks like a hang and is not one. DFU says
            // whose call it is in its own remark: "It is up to mod authors to ensure that
            // textures from asset bundles have `Read/Write Enabled` flag set when required."
            // 202 of the 330 textures in DREAM's hud & menu module have it set.
            //
            // The two fixtures are the same 64x64 image and differ ONLY in that flag, so this
            // pair can have no other explanation. fixture_readable.png is also the one that
            // proves EncodeToPNG's null path, which is why it is readable in the first place.
            var rdblImp = rdbl != null ? AssetImporter.GetAtPath(rdbl) as TextureImporter : null;
            Check(rdblImp != null && rdblImp.isReadable,
                  "a texture whose author marked it readable comes out READABLE",
                  rdblImp == null ? "no importer" : "isReadable=" + rdblImp.isReadable);
            Check(texImp != null && !texImp.isReadable,
                  "a texture whose author did not stays non-readable, keeping the memory saving",
                  texImp == null ? "no importer" : "isReadable=" + texImp.isReadable);
            // And the carrier itself: a dot-prefixed file, so Unity never imports it as an asset
            // and it can never reach a rebuilt bundle.
            string sidecar = Path.Combine(extractRoot, MobileModExtractor.ReadableSidecarName);
            Check(File.Exists(sidecar), "the extraction records the author's flags for the import",
                  sidecar);
            Check(File.Exists(sidecar) && File.ReadAllText(sidecar).Contains("fixture_readable.png")
                  && !File.ReadAllText(sidecar).Contains("fixture_tex.png"),
                  "and it lists exactly the readable one",
                  File.Exists(sidecar) ? File.ReadAllText(sidecar).Replace("\n", " | ") : "missing");
            Check(!report.extracted.Exists(p => p.EndsWith(MobileModExtractor.ReadableSidecarName)),
                  "the sidecar is not an extracted asset and cannot reach the bundle");

            // 3d-bis. CLASSIC UI ART KEEPS ITS DIMENSIONS AND ITS FORMAT. This is the second
            // contract we broke by optimising: DaggerfallTalkWindow slices its background with
            // GetPixels rects computed as classic 320x200 coordinates scaled by the REPLACEMENT
            // texture's own width - so DREAM's 1920x1200 art (exactly 6x the classic canvas)
            // gives integer rects, and our 1024 clamp turned that into 3.2x, truncating every
            // one of them. The window came up with blank panels and dead buttons. Separately,
            // the author left TALK02I0/TALK03I0 as RGBA32 because DFU reads sub-rects out of
            // them, and we compressed them anyway.
            //
            // fixture_ui.IMG.png is 1200 pixels wide - deliberately past the 1024 world-texture
            // cap - and uncompressed at source. fixture_uic.CIF.png is the same art with a UI
            // name and a COMPRESSED source. Between them they pin both halves.
            var uiImp = uiArt != null ? AssetImporter.GetAtPath(uiArt) as TextureImporter : null;
            // ...but the UI path is UNTOUCHED by that: its dimensions are read by DFU's own
            // arithmetic, so it keeps None and keeps its exact size.
            Check(uiImp != null && uiImp.npotScale == TextureImporterNPOTScale.None,
                  "classic UI art still keeps exact dimensions - no rounding",
                  uiImp == null ? "no importer" : uiImp.npotScale.ToString());
            Check(uiImp != null && uiImp.maxTextureSize == MobileConvertedModPolicy.MaxUiTextureSize,
                  "classic UI art is never downscaled: its dimensions are the contract",
                  uiImp == null ? "no importer" : "max=" + uiImp.maxTextureSize);
            Check(uiImp != null
                  && uiImp.textureCompression == TextureImporterCompression.Uncompressed,
                  "UI art whose author left it uncompressed stays uncompressed",
                  uiImp == null ? "no importer" : uiImp.textureCompression.ToString());
            var uiIos = uiImp != null
                ? uiImp.GetPlatformTextureSettings(MobileConvertedModPolicy.IosPlatform) : null;
            Check(uiIos != null && uiIos.format == TextureImporterFormat.RGBA32
                  && uiIos.maxTextureSize == MobileConvertedModPolicy.MaxUiTextureSize,
                  "and iOS names RGBA32 rather than letting the platform pick a block format",
                  uiIos == null ? "no settings" : uiIos.format + " max=" + uiIos.maxTextureSize);
            // The bytes on disk, not just the importer: the extracted PNG must still be 1200
            // wide. A clamp that survived would show up here as 1024 - which is what this check
            // first reported, from the FIXTURE's own nPOTScale: ToNearest rounding 1200 down
            // before it ever reached the bundle. The fixture is nPOTScale None for that reason;
            // the number below has to be measuring our policy, not Unity's rounding.
            var uiDec = new Texture2D(2, 2);
            bool uiLoaded = uiArt != null && uiDec.LoadImage(File.ReadAllBytes(uiArt));
            Check(uiLoaded && uiDec.width == 1200 && uiDec.height == 8,
                  "the extracted UI art really is still 1200x8",
                  uiLoaded ? uiDec.width + "x" + uiDec.height : "did not decode");
            UnityEngine.Object.DestroyImmediate(uiDec);

            // A COMPRESSED UI source still has to be re-encoded - iOS cannot decode BC7/DXT -
            // but it takes the 4x4 block, the only one that cannot introduce an alignment DFU's
            // own arithmetic does not already satisfy (SpellIconCollection refuses an atlas
            // "compressed with a block-based format but icons are not multiple of 4").
            var uicImp = uiCmp != null ? AssetImporter.GetAtPath(uiCmp) as TextureImporter : null;
            var uicIos = uicImp != null
                ? uicImp.GetPlatformTextureSettings(MobileConvertedModPolicy.IosPlatform) : null;
            Check(uicImp != null
                  && uicImp.textureCompression == TextureImporterCompression.Compressed,
                  "a compressed UI source stays compressed - iOS cannot decode BC7",
                  uicImp == null ? "no importer" : uicImp.textureCompression.ToString());
            Check(uicIos != null && uicIos.format == MobileConvertedModPolicy.UiFormat
                  && uicIos.format == TextureImporterFormat.ASTC_4x4,
                  "and it takes the 4x4 block, which cannot break the alignment maths",
                  uicIos == null ? "no settings" : uicIos.format.ToString());
            Check(uicImp != null && uicImp.maxTextureSize == MobileConvertedModPolicy.MaxUiTextureSize,
                  "compressed UI art is not downscaled either",
                  uicImp == null ? "no importer" : "max=" + uicImp.maxTextureSize);

            // The second signal, found by measuring the real module rather than by theory: a
            // texture the author left UNCOMPRESSED *and* marked READABLE is one they expect code
            // to read pixels from, whatever it is called. fixture_readable.png is exactly that
            // shape without a classic UI name, and DREAM's "renameSaveButtonBackgroundColor"
            // says in its own name that something samples it.
            string pixTex = report.extracted.Find(p => p.EndsWith("fixture_pixels.png"));
            var pixImp = pixTex != null ? AssetImporter.GetAtPath(pixTex) as TextureImporter : null;
            Check(pixImp != null
                  && pixImp.textureCompression == TextureImporterCompression.Uncompressed
                  && pixImp.maxTextureSize == MobileConvertedModPolicy.MaxUiTextureSize,
                  "uncompressed+readable art keeps both, even without a classic UI name",
                  pixImp == null ? "no importer"
                    : pixImp.textureCompression + " max=" + pixImp.maxTextureSize);
            // And the signal really needs BOTH halves: fixture_readable.png is readable but its
            // source is COMPRESSED, so it stays on the memory-optimised policy.
            Check(rdblImp != null
                  && rdblImp.maxTextureSize == MobileConvertedModPolicy.MaxTextureSize(),
                  "readable alone is not the signal - a compressed source stays capped",
                  rdblImp == null ? "no importer" : "max=" + rdblImp.maxTextureSize);

            // And the split holds: a WORLD texture keeps the memory-optimised policy, because
            // that is where the gigabytes are and it has no pixel-exact contract.
            Check(texImp != null && texImp.maxTextureSize == MobileConvertedModPolicy.MaxTextureSize()
                  && texImp.maxTextureSize != MobileConvertedModPolicy.MaxUiTextureSize,
                  "a world texture still takes the size cap - the split is real",
                  texImp == null ? "no importer" : "max=" + texImp.maxTextureSize);
            var worldIos = texImp != null
                ? texImp.GetPlatformTextureSettings(MobileConvertedModPolicy.IosPlatform) : null;
            Check(worldIos != null && worldIos.format == MobileConvertedModPolicy.IosFormat()
                  && worldIos.format != MobileConvertedModPolicy.UiFormat,
                  "and the tunable ASTC block, not the UI one",
                  worldIos == null ? "no settings" : worldIos.format.ToString());
            // World art is now allowed to round to a power of two, because Unity CANNOT compress
            // a non-power-of-two texture that has mipmaps - it silently returns RGBA32, which is
            // how DREAM's mobs module ended up costing nine times the texture RAM it should.
            // Rounding costs world art nothing: maxTextureSize already resizes it.
            Check(texImp != null && texImp.npotScale == TextureImporterNPOTScale.ToNearest,
                  "world textures may round to a power of two, so ASTC can actually apply",
                  texImp == null ? "no importer" : texImp.npotScale.ToString());
            // Against the policy's value, not a literal: this must keep passing when an operator
            // is tuning the cap against a device, which is the whole reason it is an env var.
            // The default itself (1024, below Unity's never-downscale 2048) is pinned in
            // TestConvertedModImportPolicy.
            Check(texImp != null && texImp.maxTextureSize == MobileConvertedModPolicy.MaxTextureSize(),
                  "converted textures take their size cap from the policy",
                  texImp == null ? "no importer" : "max=" + texImp.maxTextureSize
                      + " policy=" + MobileConvertedModPolicy.MaxTextureSize());
            var ios = texImp != null
                ? texImp.GetPlatformTextureSettings(MobileConvertedModPolicy.IosPlatform) : null;
            Check(ios != null && ios.overridden,
                  "converted textures carry an explicit iOS override",
                  ios == null ? "no settings" : "overridden=" + ios.overridden);
            Check(ios != null && ios.format == MobileConvertedModPolicy.IosFormat()
                  && ios.maxTextureSize == MobileConvertedModPolicy.MaxTextureSize()
                  && ios.compressionQuality == MobileConvertedModPolicy.CompressionQuality(),
                  "iOS override names the ASTC block, the cap and the compressor quality",
                  ios == null ? "no settings"
                    : ios.format + " " + ios.maxTextureSize + " q" + ios.compressionQuality);
            // World textures ARE minified, so this one keeps its mipmaps; the 2D-art rule is
            // exercised as a pure function in TestConvertedModImportPolicy, because no fixture
            // path here can stand in for a real paperdoll's.
            Check(texImp != null && texImp.mipmapEnabled,
                  "a world texture keeps its mipmaps");
            Check(texImp != null && !texImp.streamingMipmaps,
                  "mipmap streaming stays off (QualitySettings has it disabled project-wide)");
            // The extraction root is deleted at the end of this test, so its .meta files never
            // survive to be inspected by hand. Record what the policy actually produced.
            if (texImp != null && nrmImp != null && hgtImp != null && ios != null)
                Debug.Log(string.Format("[MobileSelfTest] converted-mod import policy produced: " +
                    "colour type={0} readable={1} compression={2} mips={3} stream={4} npot={5} " +
                    "sRGB={6} max={7}; iOS override={8} fmt={9} max={10} q={11}; " +
                    "normal type={12} sRGB={13}; height type={14} sRGB={15}",
                    texImp.textureType, texImp.isReadable, texImp.textureCompression,
                    texImp.mipmapEnabled, texImp.streamingMipmaps, texImp.npotScale,
                    texImp.sRGBTexture, texImp.maxTextureSize,
                    ios.overridden, ios.format, ios.maxTextureSize, ios.compressionQuality,
                    nrmImp.textureType, nrmImp.sRGBTexture,
                    hgtImp.textureType, hgtImp.sRGBTexture));

            // 3e. AUDIO. A bundle holds an AudioClip as decoded float samples and nothing
            // else - the author's .wav/.ogg source is not in there - so extraction means
            // re-authoring a container around the samples. The header checks below are the
            // cheap half; the tone check after them is the half that matters, because a WAV of
            // pure silence has a perfectly correct header, the right length and the right name.
            Check(wav != null && File.Exists(wav), "audio clip extracted as .wav", wav ?? "missing");
            byte[] wavBytes = wav != null ? File.ReadAllBytes(wav) : new byte[0];
            Check(wavBytes.Length > 44
                  && wavBytes[0] == (byte)'R' && wavBytes[1] == (byte)'I'
                  && wavBytes[2] == (byte)'F' && wavBytes[3] == (byte)'F',
                  "extracted audio is a RIFF file with a payload", "bytes=" + wavBytes.Length);

            var clip = wav != null ? AssetDatabase.LoadAssetAtPath<AudioClip>(wav) : null;
            Check(clip != null && clip.frequency == 22050 && clip.channels == 1,
                  "extracted clip re-imports at the fixture's rate and channel count",
                  clip == null ? "no clip" : clip.frequency + "Hz x" + clip.channels);
            Check(clip != null && clip.length > 0.24f && clip.length < 0.26f,
                  "extracted clip is the fixture's 0.25s",
                  clip == null ? "no clip" : clip.length.ToString("F4") + "s");

            // THE CHECK THAT MATTERS for audio. Correlate the written PCM against the fixture's
            // own 440Hz generator (sin and cos, so phase does not matter) and against a decoy
            // frequency that is not in the fixture at all. Silence, a DC offset, a half-rate
            // header or samples that wrapped instead of clamping all leave the 440Hz magnitude
            // far from the fixture's 0.8 amplitude; only real, correctly-rated audio lands on it.
            double sin440 = 0, cos440 = 0, sinDecoy = 0, cosDecoy = 0, peak = 0;
            int frames = Mathf.Max(0, (wavBytes.Length - 44) / 2);
            for (int i = 0; i < frames; i++)
            {
                double v = BitConverter.ToInt16(wavBytes, 44 + i * 2) / 32767.0;
                if (Math.Abs(v) > peak) peak = Math.Abs(v);
                double t = i / 22050.0;
                sin440 += v * Math.Sin(2 * Math.PI * 440 * t);
                cos440 += v * Math.Cos(2 * Math.PI * 440 * t);
                sinDecoy += v * Math.Sin(2 * Math.PI * 1300 * t);
                cosDecoy += v * Math.Cos(2 * Math.PI * 1300 * t);
            }
            double mag440 = frames > 0 ? 2 * Math.Sqrt(sin440 * sin440 + cos440 * cos440) / frames : 0;
            double magDecoy = frames > 0 ? 2 * Math.Sqrt(sinDecoy * sinDecoy + cosDecoy * cosDecoy) / frames : 0;
            Check(frames > 5000 && frames < 6000, "extracted PCM holds ~0.25s of 22050Hz mono frames",
                  "frames=" + frames);
            Check(mag440 > 0.5 && mag440 < 1.0 && magDecoy < 0.1,
                  "extracted audio still IS the fixture's 440Hz tone",
                  "440Hz=" + mag440.ToString("F3") + " decoy1300Hz=" + magDecoy.ToString("F3")
                      + " peak=" + peak.ToString("F3"));

            // THE LOUD SKIPS, and the reason this test carries three audio fixtures that are
            // byte-for-byte the same sound. AudioClip.GetData reads DECODED PCM, so it serves
            // only a clip the author imported as DecompressOnLoad; Unity says so itself
            // ("Cannot get data on compressed samples for audio clip ... Changing the load type
            // to DecompressOnLoad on the audio clip will fix this"). The other two load types
            // are therefore not extractable AT ALL by this route, and the only difference
            // between these three fixtures is the load type in their .meta - so a skip here can
            // have no other cause.
            //
            // Unity's default is DecompressOnLoad, so the clip an author never configured does
            // convert - fixture_beep.wav carries Unity's own generated .meta and is the proof.
            // But music is the part of a mod an author DOES configure, and both of the settings
            // they would reach for are unreadable. If DREAM's 273MB music module turns out to be
            // streamed, the whole module is unconvertible and this report is the only place
            // anyone would find that out - so it is counted per load type and warned about per
            // clip, never silently totalled.
            int streamSkipped, packedSkipped, noData, asyncSkipped;
            report.skippedByType.TryGetValue("AudioClip(streaming)", out streamSkipped);
            report.skippedByType.TryGetValue("AudioClip(compressed)", out packedSkipped);
            report.skippedByType.TryGetValue("AudioClip(nodata)", out noData);
            report.skippedByType.TryGetValue("AudioClip(async)", out asyncSkipped);
            Check(streamSkipped == 1, "a Streaming clip is skipped loudly, not silently dropped",
                  "AudioClip(streaming)=" + streamSkipped);
            Check(packedSkipped == 1,
                  "a CompressedInMemory clip is skipped loudly too, for the same reason",
                  "AudioClip(compressed)=" + packedSkipped);
            Check(noData == 0, "no clip reached the GetData backstop: the load type caught both",
                  "AudioClip(nodata)=" + noData);

            // RESIDENCY, which is a different question from load type and was learned the
            // expensive way: DecompressOnLoad says how a clip is DECODED, not that it is decoded
            // yet. DREAM's sound module lost 34 of 340 clips to this - every long ambient loop,
            // each with Preload Audio Data off - and each reported "GetData failed on a
            // DecompressOnLoad clip", which named the wrong thing entirely.
            //
            // fixture_async.wav is the same 440Hz tone as the others with Load In Background set
            // in its .meta, which is exactly what DREAM's ambients have. It is the regression
            // pin for the half of that defect this converter can see: the clip is NOT resident,
            // its load is asynchronous, and an asynchronous load can only be completed by
            // Unity's main loop - which a synchronous -executeMethod is blocking. So it is
            // refused IMMEDIATELY under its own key rather than waited on: the 30s-per-clip wait
            // this replaced turned one module into a 17-minute stall that still reported the
            // wrong cause. AudioClip(async) is deliberately not AudioClip(nodata), because it
            // says "the driver could not", not "the clip cannot" - the same file converts the
            // moment the converter is stepped across editor ticks instead of blocking them.
            Check(asyncSkipped == 1,
                  "a Load-In-Background clip is refused at once under its own key, not mis-blamed",
                  "AudioClip(async)=" + asyncSkipped + " AudioClip(nodata)=" + noData);
            Check(report.extracted.Find(p => p.EndsWith("fixture_async.wav")) == null,
                  "the asynchronous clip really is absent from the extraction");
            Check(report.extracted.Find(p => p.EndsWith("fixture_stream.wav")) == null
                  && report.extracted.Find(p => p.EndsWith("fixture_packed.wav")) == null,
                  "the unreadable clips really are absent from the extraction");
            // Four audio fixtures now: one that converts, and three that cannot, each for its
            // own distinct reason and each counted separately. A module's report says which.
            Check(streamSkipped + packedSkipped + asyncSkipped == 3 && noData == 0,
                  "each way a clip can be unreadable is counted apart from the others",
                  "streaming=" + streamSkipped + " compressed=" + packedSkipped
                      + " async=" + asyncSkipped + " nodata=" + noData);
            Check(!report.notesByType.ContainsKey("AudioClip(streaming)")
                  && !report.notesByType.ContainsKey("AudioClip(compressed)"),
                  "a skip is a loss, so it is never filed as a note about a survivor");

            // EVERY LOADED ASSET WAS HANDED BACK. The objects a bundle serves are not its
            // compressed bytes: a DecompressOnLoad clip is decoded to PCM in native memory at
            // load, and a texture decodes the same way, so a loop that loads a whole module
            // before its first unload holds the whole module DECODED. On DREAM's music module
            // that is the difference between converting and being killed part way through, and
            // no other number in this report would show it.
            //
            // This is the cheap regression catch for that: the two counters are incremented in
            // different places - loaded at the LoadAsset call site, released inside Release
            // itself - so deleting the release, or letting one branch escape the try/finally
            // that performs it, drives them apart. This fixture deliberately exercises the
            // awkward paths as well as the happy one: two clips refused on load type, one
            // refused on residency, one texture losing a collision, ten assets written. If any
            // of those paths stopped releasing, this is what would notice.
            Check(report.loaded > 0 && report.released == report.loaded,
                  "every bundle asset the loop loaded was released again, on every path",
                  "released=" + report.released + " loaded=" + report.loaded);

            // ...and the release is not a no-op. The counter above proves Release was CALLED;
            // it cannot prove it did anything, and "did anything" is the genuinely uncertain
            // half - Resources.UnloadAsset is documented to do nothing for assets that came from
            // the editor's AssetDatabase, and this check is what established that it does
            // nothing for an editor-side BUNDLE asset either (it was written asserting the
            // object would be destroyed, and it failed). So the audio path does not rest on it:
            // UnloadAudioData is the call that carries the dominant term, and this is what says
            // so out loud. Reload the source bundle, take a clip that really is decoded and
            // resident - GetData succeeding is only true of loaded PCM - release it, and require
            // the samples to be gone afterwards.
            //
            // Either outcome counts as freed: loadState back to Unloaded, or the whole object
            // destroyed if a future Unity does make UnloadAsset bite here. A release that did
            // nothing leaves loadState at Loaded and fails.
            AssetBundle probe = AssetBundle.LoadFromFile(built[0]);
            var probeClip = probe != null ? probe.LoadAsset<AudioClip>("fixture_beep") : null;
            var probeSamples = probeClip != null
                ? new float[probeClip.samples * probeClip.channels] : new float[0];
            Check(probeClip != null && probeClip.GetData(probeSamples, 0) && probeSamples.Length > 0
                  && probeClip.loadState == AudioDataLoadState.Loaded,
                  "the probe clip really is decoded and resident before the release",
                  probeClip == null ? "no clip"
                    : probeSamples.Length + " samples, " + probeClip.loadState);
            MobileModExtractor.Release(probeClip);
            Check(probeClip == null || probeClip.loadState == AudioDataLoadState.Unloaded,
                  "Release actually drops the decoded PCM; it is not a no-op",
                  probeClip == null ? "object destroyed outright"
                    : "loadState after release: " + probeClip.loadState);
            if (probe != null)
                probe.Unload(true);

            // THE TEXTURE PROBE, and the reason it is a separate one. The clip check above is
            // mildly confounded: Release calls UnloadAudioData first, so by the time
            // Resources.UnloadAsset sees the clip its samples are already gone, and "the object
            // survived" might have been a fact about that state rather than about UnloadAsset.
            // A Texture2D goes through no such preparatory call, so this is the clean question -
            // and it is the question that decides whether converting a 1.72GB texture module
            // accumulates every decoded texture until the Unload after the loop, or does not.
            //
            // The assertion below encodes the MEASURED answer, so it is a statement of fact
            // about this Unity, not a wish: if a future Unity starts honouring UnloadAsset for
            // an editor-side bundle asset, this fails and the residual it documents is gone.
            // Either way the fact is logged, because it is what a conversion has to be
            // scheduled around.
            AssetBundle texProbe = AssetBundle.LoadFromFile(built[0]);
            // A name with no .tga twin: fixture_tex exists twice in this bundle and which one a
            // short-name load returns is not something this check should depend on.
            var probeTex = texProbe != null
                ? texProbe.LoadAsset<Texture2D>("fixture_wall_Height") : null;
            Check(probeTex != null && probeTex.width == 64,
                  "the probe texture loaded from the bundle before the release",
                  probeTex == null ? "no texture" : probeTex.width + "x" + probeTex.height);
            MobileModExtractor.Release(probeTex);
            bool textureFreed = probeTex == null;
            Debug.Log("[MobileSelfTest] MEASURED: Resources.UnloadAsset on an editor-side bundle "
                + "Texture2D " + (textureFreed ? "DOES destroy it - Release covers the texture path"
                    : "does NOT destroy it - a texture module accumulates until AssetBundle.Unload"));
            Check(!textureFreed,
                  "measured: Release does NOT free a bundle texture in the editor "
                  + "(so a large texture module still accumulates until Unload)",
                  "texture after release: " + (textureFreed ? "destroyed" : "still alive"));
            if (texProbe != null)
                texProbe.Unload(true);

            // 3f. The audio half of the import policy - MobileConvertedModImporter.OnPreprocessAudio -
            // which nothing in the suite could reach until audio was extracted, because the
            // postprocessor is scoped to the extraction root and nothing had ever landed an
            // AudioClip there. Songs must stream (a megabyte-per-minute resident song is what
            // the memory budget cannot afford) and sound effects must not (a streamed effect
            // stutters on its first frame), and that split is decided by file size here.
            var clipImp = wav != null ? AssetImporter.GetAtPath(wav) as AudioImporter : null;
            var sampleSettings = clipImp != null ? clipImp.defaultSampleSettings
                                                 : default(AudioImporterSampleSettings);
            // Two of the three are proof rather than pins: measured against the control below,
            // Unity 6 defaults a .wav to DecompressOnLoad at quality 1.0, so the load type and
            // the quality here are both the postprocessor's doing. Vorbis happens to coincide
            // with Unity's default and is a regression pin only. The Streaming branch
            // for songs cannot be reached by any fixture small enough to commit, so it is
            // pinned as a pure rule in TestConvertedModImportPolicy instead.
            Check(clipImp != null && sampleSettings.compressionFormat == AudioCompressionFormat.Vorbis,
                  "converted audio is Vorbis, not raw PCM",
                  clipImp == null ? "no importer" : sampleSettings.compressionFormat.ToString());
            Check(clipImp != null && sampleSettings.loadType == AudioClipLoadType.CompressedInMemory,
                  "a small clip is a sound effect: compressed in memory, never streamed",
                  clipImp == null ? "no importer" : sampleSettings.loadType.ToString());
            Check(clipImp != null && Mathf.Abs(sampleSettings.quality - 0.7f) < 0.001f,
                  "converted audio carries the policy's Vorbis quality",
                  clipImp == null ? "no importer" : sampleSettings.quality.ToString("F3"));

            // NON-DEFAULTNESS, MEASURED AGAINST A CONTROL rather than against a remembered
            // value. fixture_beep.wav is the same bytes as the extracted file and sits outside
            // the extraction root, so the postprocessor never touches it; its .meta is the one
            // Unity generated, committed as-is. That makes it a recorded snapshot of Unity's
            // defaults, not a live reading of them - if Unity's defaults ever move, the meta
            // will not follow, and the third check below is what would say so. What the
            // comparison does buy is real: the assertions read both importers instead of
            // hard-coding "0.7 differs from 1.0", so they state the property that matters
            // (the policy changed something) rather than two literals that happen to differ.
            const string defaultFixture =
                "Assets/Editor/TestFixtures/ExtractorFixture/fixture_beep.wav";
            var srcImp = AssetImporter.GetAtPath(defaultFixture) as AudioImporter;
            var srcSettings = srcImp != null ? srcImp.defaultSampleSettings
                                             : default(AudioImporterSampleSettings);
            string audioSettings = srcImp == null || clipImp == null ? "no importer"
                : "default=" + srcSettings.compressionFormat + "/" + srcSettings.loadType
                  + "/q" + srcSettings.quality.ToString("F2")
                  + " converted=" + sampleSettings.compressionFormat + "/"
                  + sampleSettings.loadType + "/q" + sampleSettings.quality.ToString("F2");
            Check(srcImp != null && clipImp != null
                  && Mathf.Abs(srcSettings.quality - sampleSettings.quality) > 0.001f,
                  "the converted clip's Vorbis quality is not the importer default",
                  audioSettings);
            Check(srcImp != null && clipImp != null
                  && srcSettings.loadType != sampleSettings.loadType,
                  "the converted clip's load type is not the importer default either",
                  audioSettings);
            // And the source really is readable, which is what makes it a control AND what
            // makes the skips above attributable to the load type and nothing else. It is also
            // the check that would notice if a future Unity stopped defaulting to
            // DecompressOnLoad, which would make this fixture stop representing the default.
            Check(srcImp != null && srcSettings.loadType == AudioClipLoadType.DecompressOnLoad,
                  "Unity's default load type is DecompressOnLoad, so an unconfigured clip converts",
                  audioSettings);

            if (clipImp != null)
                Debug.Log(string.Format("[MobileSelfTest] converted-mod audio policy produced: " +
                    "format={0} loadType={1} quality={2} -> clip {3}Hz x{4} {5}s " +
                    "(Unity's defaults for the same file: format={6} loadType={7} quality={8})",
                    sampleSettings.compressionFormat, sampleSettings.loadType, sampleSettings.quality,
                    clip == null ? 0 : clip.frequency, clip == null ? 0 : clip.channels,
                    clip == null ? 0f : clip.length,
                    srcImp == null ? "?" : srcSettings.compressionFormat.ToString(),
                    srcImp == null ? "?" : srcSettings.loadType.ToString(),
                    srcImp == null ? "?" : srcSettings.quality.ToString("F2")));

            // 3g. THE WATCHDOG. Without -quit there is nothing left to end this process, so the
            // one place the converter waits - an asynchronous audio load - has to be able to
            // give up. Drive the same steps in yielding mode, but pumped by this loop rather
            // than by the editor, so the load can never actually complete: exactly the stall the
            // cap exists for. With the cap set to a second, the run must END, count the clip
            // under AudioClip(async), and keep everything else.
            //
            // This also pins the accounting through the timeout path, which is the one most
            // likely to leak: a clip abandoned mid-load still has to be released.
            string previousCap = Environment.GetEnvironmentVariable(
                MobileModExtractor.AudioTimeoutVar);
            Environment.SetEnvironmentVariable(MobileModExtractor.AudioTimeoutVar, "1");
            const string watchdogRoot = "Assets/Game/Mods/Converted/__watchdog__";
            if (Directory.Exists(watchdogRoot)) { Directory.Delete(watchdogRoot, true); File.Delete(watchdogRoot + ".meta"); }
            var watchdogReport = new ExtractReport();
            var watchdogStarted = DateTime.UtcNow;
            int pumped = 0;
            double watchdogSeconds;
            // try/finally, because leaking a one-second audio cap into the rest of the suite
            // would make every later check quietly wrong in a way nothing here would explain.
            try
            {
                IEnumerator watchdogSteps = MobileModExtractor.ExtractSteps(
                    built[0], watchdogRoot, watchdogReport, true);
                while (watchdogSteps.MoveNext())
                {
                    pumped++;
                    if ((DateTime.UtcNow - watchdogStarted).TotalSeconds > 60)
                        break;      // the test's own backstop; reaching it IS the failure
                }
            }
            finally
            {
                watchdogSeconds = (DateTime.UtcNow - watchdogStarted).TotalSeconds;
                Environment.SetEnvironmentVariable(
                    MobileModExtractor.AudioTimeoutVar, previousCap);
            }
            Check(Environment.GetEnvironmentVariable(MobileModExtractor.AudioTimeoutVar)
                      == previousCap,
                  "the watchdog test restores the audio cap it borrowed");

            int watchdogAsync;
            watchdogReport.skippedByType.TryGetValue("AudioClip(async)", out watchdogAsync);
            Check(watchdogSeconds < 30, "a stalled audio load gives up instead of hanging the run",
                  "finished in " + watchdogSeconds.ToString("F1") + "s after " + pumped + " pumps");
            Check(watchdogAsync == 1, "the abandoned clip is counted, not silently dropped",
                  "AudioClip(async)=" + watchdogAsync);
            Check(watchdogReport.released == watchdogReport.loaded && watchdogReport.loaded > 0,
                  "and it is still released, even though its load never finished",
                  "released=" + watchdogReport.released + " loaded=" + watchdogReport.loaded);
            Check(watchdogReport.extracted.Count == report.extracted.Count,
                  "everything that does not depend on the stalled clip still converts",
                  "extracted=" + watchdogReport.extracted.Count);
            // And it yielded MORE than once. The driver can only check its run cap between
            // steps, so an extraction that yields only where it waits for audio leaves a
            // texture module - which waits for nothing - running inside a single step with the
            // watchdog unreachable. Now that -quit is gone there would be nothing left to end
            // such a stall, so the heartbeat is load-bearing, not cosmetic.
            Check(pumped > 1, "the extraction yields periodically, so the run cap is reachable",
                  "yields=" + pumped);
            if (Directory.Exists(watchdogRoot))
            {
                Directory.Delete(watchdogRoot, true);
                File.Delete(watchdogRoot + ".meta");
            }

            // 4. Rewritten manifest points at extracted files, keeps identity.
            ModInfo info = null;
            ModManager._serializer.TryDeserialize(
                fsJsonParser.Parse(File.ReadAllText(report.manifestPath)), ref info);
            Check(info != null && info.ModTitle == "Extractor Fixture"
                  && info.GUID == "0d2c4a68-9e1f-4b7a-8c35-6d0e2f4a6b8c",
                  "manifest identity preserved");
            Check(info != null && info.Files.Count == 10
                  && info.Files.TrueForAll(f => File.Exists(f)),
                  "manifest Files rewritten to extracted paths");

            // 5. Full circle, and THROUGH THE SHIPPED ENTRY POINT rather than around it.
            // Convert is the one call an operator makes - ConvertFromEnv is a thin env wrapper
            // over it - so hand-assembling extract-then-BuildMod here would leave the chain
            // itself the only part of the pipeline nothing exercises: a Convert that passed
            // the wrong root to BuildMod, or dropped the rebuild entirely, would still let
            // every check above pass. It also re-extracts on top of the extraction this test
            // has already made, which is the only place anything proves a second conversion
            // onto a populated root does not trip over its own output.
            string[] rebuilt = MobileModExtractor.Convert(built[0], extractRoot, bundleDir,
                new[] { BuildTarget.StandaloneOSX });
            Check(rebuilt.Length == 1, "Convert returns one built bundle per requested target",
                  "built=" + rebuilt.Length);
            Check(rebuilt.Length == 1 && File.Exists(rebuilt[0])
                  && rebuilt[0].Replace('\\', '/').EndsWith(
                      bundleDir + "/" + BuildTarget.StandaloneOSX + "/fixture-mod.dfmod"),
                  "Convert built into the bundle root it was given, under the target's folder",
                  rebuilt.Length == 1 ? rebuilt[0] : "no path");
            AssetBundle ab = AssetBundle.LoadFromFile(rebuilt[0]);
            Check(ab != null && ab.Contains("fixture_tex"), "rebuilt bundle answers to short name");
            if (ab != null)
            {
                var t = ab.LoadAsset<Texture2D>("fixture_tex");
                Check(t != null && t.width == 64 && t.height == 64, "rebuilt texture is 64x64",
                      t ? t.width + "x" + t.height : "null");
                ab.Unload(true);
            }

            // Cleanup.
            Directory.Delete(bundleDir, true);
            Directory.Delete(extractRoot, true);
            File.Delete(extractRoot + ".meta");
            AssetDatabase.Refresh();
        }

        /// <summary>
        /// A .dfmod is untrusted input - a file a stranger hands us - and the converter is meant
        /// to be exposed publicly. The manifest inside the bundle is the part an attacker fully
        /// controls, so an unconstrained Files entry ("../../.ssh/authorized_keys", or an absolute
        /// path, which Path.Combine would let win outright) is an arbitrary file write, not a
        /// theoretical one. Every output path must therefore be proven inside the extraction root
        /// before any byte is written.
        /// </summary>
        static void TestModExtractorPathContainment()
        {
            // The decision itself, as a pure function. Normalisation, not string matching.
            const string root = "Assets/Game/Mods/Converted/probe";
            Check(MobileModExtractor.IsInsideRoot(Path.Combine(root, "tex.png"), root),
                  "containment: a plain path inside the root is allowed");
            Check(MobileModExtractor.IsInsideRoot(Path.Combine(root, "Assets/Textures/water.png"), root),
                  "containment: a nested path inside the root is allowed");
            Check(!MobileModExtractor.IsInsideRoot(Path.Combine(root, "../escape.png"), root),
                  "containment: .. climbing out of the root is refused");
            Check(!MobileModExtractor.IsInsideRoot("/tmp/dfu-extractor-evil.png", root),
                  "containment: an absolute path is refused");
            // Path.Combine would happily build this, and a naive StartsWith would accept it.
            Check(!MobileModExtractor.IsInsideRoot(root + "-evil/x.png", root),
                  "containment: a sibling sharing a name prefix is refused");
            // .. that resolves back inside is legitimate; refusing it would be a false positive.
            Check(MobileModExtractor.IsInsideRoot(Path.Combine(root, "sub/../ok.png"), root),
                  "containment: .. that resolves back inside is allowed");

            // End to end, through Extract, with a genuinely hostile bundle. MobileModBuilder
            // cannot produce one - it validates every manifest entry - so pack it the way an
            // attacker would, giving one asset an addressable name that climbs out of the root.
            const string hostileManifest = "Assets/Editor/TestFixtures/ExtractorFixture/hostile-mod.dfmod.json";
            const string payload = "Assets/Editor/TestFixtures/ExtractorFixture/hostile_payload.json";
            // A distinct asset: Unity refuses to pack the same one into a bundle twice.
            const string escapePayload = "Assets/Editor/TestFixtures/ExtractorFixture/hostile_escape_payload.json";
            const string escapeName = "../dfu-extractor-escape.json";
            // A third distinct asset, addressed inside the bundle as C# SOURCE. This is not a
            // hypothetical: the extraction root lives under Assets/, so a .cs written there is
            // compiled into the editor's own assemblies on the next refresh - during the very
            // run that wrote it - which is a far worse outcome than the exception
            // MobileModBuilder would eventually have thrown. addressableNames is what makes the
            // repro honest: the bundle really does carry an asset named .cs, and the manifest
            // really does list it, so the extractor's path logic resolves it exactly as it would
            // an attacker's. (No .cs can be committed as a fixture for the obvious reason.)
            const string scriptPayload = "Assets/Editor/TestFixtures/ExtractorFixture/hostile_script_payload.json";
            const string scriptName = "assets/editor/testfixtures/extractorfixture/hostile_script.cs";
            const string bundleDir = "Temp/MobileModExtractorEscapeTest";
            const string extractRoot = "Assets/Game/Mods/Converted/__escape__";
            const string escapeTarget = "Assets/Game/Mods/Converted/dfu-extractor-escape.json";
            if (Directory.Exists(bundleDir)) Directory.Delete(bundleDir, true);
            if (Directory.Exists(extractRoot)) { Directory.Delete(extractRoot, true); File.Delete(extractRoot + ".meta"); AssetDatabase.Refresh(); }
            File.Delete(escapeTarget);

            // hostile-mod.dfmod.json's Files lists the escaping name, and one bundle asset is
            // addressed by it, so the extractor resolves that entry exactly as it would a real
            // attacker's - through the manifest lookup, straight into an output path.
            var build = new AssetBundleBuild[1];
            build[0].assetBundleName = "hostile-mod.dfmod";
            build[0].assetNames = new[] { payload, escapePayload, scriptPayload, hostileManifest };
            build[0].addressableNames = new[] {
                "assets/editor/testfixtures/extractorfixture/hostile_payload.json",
                escapeName,
                scriptName,
                "assets/editor/testfixtures/extractorfixture/hostile-mod.dfmod.json" };

            Directory.CreateDirectory(bundleDir);
            BuildPipeline.BuildAssetBundles(bundleDir, build,
                BuildAssetBundleOptions.ChunkBasedCompression, BuildTarget.StandaloneOSX);
            string hostileBundle = Path.Combine(bundleDir, "hostile-mod.dfmod");

            var report = MobileModExtractor.Extract(hostileBundle, extractRoot);

            int escapes;
            report.skippedByType.TryGetValue("path-escape", out escapes);
            Check(escapes == 1, "hostile manifest entry is refused", "path-escape=" + escapes);
            Check(!File.Exists(escapeTarget) && !File.Exists("Assets/Game/Mods/Converted/" + Path.GetFileName(escapeName)),
                  "nothing was written outside the extraction root");
            Check(report.extracted.Count == 1 && report.extracted[0].EndsWith("hostile_payload.json"),
                  "the legitimate asset still extracts alongside the refused ones",
                  "extracted=" + report.extracted.Count);

            // MEASURED, not assumed: the path logic really does turn a bundle asset named .cs
            // plus a manifest entry spelling it .cs into a .cs output path under Assets/. That
            // is the hazard, and the refusal is what stops it - before the asset is even loaded,
            // so nothing about it can reach disk. Relying on MobileModBuilder's script guard
            // instead would be too late by a whole compilation.
            int codeRefused;
            report.skippedByType.TryGetValue("code-file-refused", out codeRefused);
            Check(codeRefused == 1, "a bundle asset named .cs is refused before it can be written",
                  "code-file-refused=" + codeRefused);
            Check(Directory.GetFiles("Assets/Game/Mods/Converted", "*.cs",
                      SearchOption.AllDirectories).Length == 0,
                  "no C# source reached the project, where Unity would have compiled it");

            Directory.Delete(bundleDir, true);
            Directory.Delete(extractRoot, true);
            File.Delete(extractRoot + ".meta");
            AssetDatabase.Refresh();
        }

        /// <summary>
        /// Containment is not the only way a manifest path fails to become a file. A mod listing
        /// both "clash" (a TextAsset) and "clash/inner.json" is fully contained and fully legal,
        /// but one of the two must lose - a name cannot be a file and a directory at once - and an
        /// unguarded write would throw straight out of Extract, costing the operator every other
        /// asset in the mod. The same manifest also spells one file two ways, which must count as
        /// a collision rather than two assets: keyed on the raw string they look distinct, so the
        /// second would quietly overwrite the first and the rebuilt manifest would list it twice,
        /// which Unity then refuses to pack at all.
        /// </summary>
        /// <summary>
        /// The two rules that guard the converter's own process, as pure functions.
        ///
        /// The first decides what may never be written into the project at all. The extraction
        /// root is under Assets/, so Unity treats whatever lands there as project content the
        /// instant it appears: a .cs is compiled into the editor's live assemblies, a plugin
        /// binary is loaded, an .asmdef restructures compilation and an .rsp rewrites compiler
        /// flags project-wide. A .dfmod is a file a stranger hands us. The end-to-end proof that
        /// the refusal fires is in TestModExtractorPathContainment; this pins the rule itself,
        /// including the two spellings that must stay ALLOWED because they are what real DFU
        /// mods carry and they are inert TextAssets.
        ///
        /// The second is the watchdog's clock. Without -quit there is nothing else to stop this
        /// process, so a timeout that a typo could turn into "no timeout" would be worse than no
        /// watchdog at all - it would look like one.
        /// </summary>
        /// <summary>
        /// The naming used when a texture is reached through a MATERIAL, which is the one thing
        /// in this converter that fails silently and destructively if it is wrong.
        ///
        /// DREAM's world retexture ships 1201 Materials and no addressable textures, so the only
        /// way to convert it is to pull the textures out and name them ourselves. A texture
        /// written under the wrong name does not error - DFU finds it and replaces the WRONG
        /// art, and nobody discovers that until they walk past the wrong wall. So the rule is:
        /// parse the material's name into archive/record/frame, regenerate the name from those
        /// numbers, and refuse outright when it does not parse.
        ///
        /// The authority is TextureReplacement.GetName: "{archive:000}_{record}-{frame}", plus
        /// "_{TextureMap}" for everything except Albedo.
        /// </summary>
        static void TestMaterialTextureNaming()
        {
            // Canonical form, including the zero padding a material called "6_0-0" would lack.
            Check(MobileModExtractor.DfuTextureName(6, 0, 0, string.Empty) == "006_0-0"
                  && MobileModExtractor.DfuTextureName(6, 0, 0, "Normal") == "006_0-0_Normal"
                  && MobileModExtractor.DfuTextureName(302, 55, 0, string.Empty) == "302_55-0",
                  "names are rebuilt in DFU's form, zero-padded, suffix only when not albedo",
                  MobileModExtractor.DfuTextureName(6, 0, 0, "Normal"));
            Check(MobileModExtractor.DfuTextureName(1234, 7, 2, "MetallicGloss") == "1234_7-2_MetallicGloss",
                  "an archive wider than three digits is not truncated",
                  MobileModExtractor.DfuTextureName(1234, 7, 2, "MetallicGloss"));

            int a, r, f;
            Check(MobileModExtractor.TryParseDfuTextureName("006_0-0", out a, out r, out f)
                  && a == 6 && r == 0 && f == 0, "the canonical name parses back to its numbers");
            Check(MobileModExtractor.TryParseDfuTextureName("302_55-3", out a, out r, out f)
                  && a == 302 && r == 55 && f == 3, "multi-digit record and frame parse");
            // Round trip: whatever parses must regenerate to a name the engine looks up.
            Check(MobileModExtractor.TryParseDfuTextureName("6_0-0", out a, out r, out f)
                  && MobileModExtractor.DfuTextureName(a, r, f, string.Empty) == "006_0-0",
                  "an unpadded source name is regenerated padded, not copied through");

            // REFUSALS. Each of these would, if coerced into a name, replace the wrong art.
            Check(!MobileModExtractor.TryParseDfuTextureName("brick_wall", out a, out r, out f)
                  && !MobileModExtractor.TryParseDfuTextureName("006", out a, out r, out f)
                  && !MobileModExtractor.TryParseDfuTextureName("006_0", out a, out r, out f)
                  && !MobileModExtractor.TryParseDfuTextureName("006_0-", out a, out r, out f)
                  && !MobileModExtractor.TryParseDfuTextureName("", out a, out r, out f)
                  && !MobileModExtractor.TryParseDfuTextureName(null, out a, out r, out f),
                  "anything that is not exactly archive_record-frame is refused, not guessed");
            // A name that ALREADY carries a map suffix is a map name, not a base name: parsing it
            // would append a second suffix and produce "006_0-0_Normal_Normal".
            Check(!MobileModExtractor.TryParseDfuTextureName("006_0-0_Normal", out a, out r, out f),
                  "a name that already has a TextureMap suffix is not a base name");

            // Property -> suffix, from MaterialReader.Uniforms.Textures.
            Check(MobileModExtractor.TextureMapForProperty("_MainTex") == string.Empty,
                  "albedo carries no suffix, as GetName does it");
            Check(MobileModExtractor.TextureMapForProperty("_BumpMap") == "Normal"
                  && MobileModExtractor.TextureMapForProperty("_ParallaxMap") == "Height"
                  && MobileModExtractor.TextureMapForProperty("_EmissionMap") == "Emission"
                  && MobileModExtractor.TextureMapForProperty("_MetallicGlossMap") == "MetallicGloss",
                  "each of DFU's four non-albedo maps gets its own TextureMap suffix");
            // _OcclusionMap is real and DREAM sets it; DFU's TextureMap has no name for it, and
            // TextureMap.Mask has no material property. Neither may be invented.
            Check(MobileModExtractor.TextureMapForProperty("_OcclusionMap") == null
                  && MobileModExtractor.TextureMapForProperty("_DetailAlbedoMap") == null
                  && MobileModExtractor.TextureMapForProperty("_Anything") == null,
                  "a property DFU has no TextureMap for is refused, never guessed");

            // The written suffixes must be the ones the rest of this converter already keys its
            // colour-space and normal-map rules off, or a material-sourced normal map would be
            // imported as ordinary colour.
            Check(MobileModExtractor.IsNormalMapName(
                      MobileModExtractor.DfuTextureName(6, 0, 0, "Normal") + ".png")
                  && MobileModExtractor.IsLinearMapName(
                      MobileModExtractor.DfuTextureName(6, 0, 0, "Height") + ".png")
                  && MobileModExtractor.IsLinearMapName(
                      MobileModExtractor.DfuTextureName(6, 0, 0, "MetallicGloss") + ".png"),
                  "material-sourced maps are recognised by the existing suffix rules");
            Check(!MobileModExtractor.IsLinearMapName(
                      MobileModExtractor.DfuTextureName(6, 0, 0, string.Empty) + ".png"),
                  "and a material-sourced albedo is still colour");
        }

        static void TestConverterGuardRules()
        {
            Check(MobileModExtractor.IsProjectCodeFile("Assets/Game/Mods/Converted/x/Foo.cs"),
                  "C# source is refused: Unity would compile it into the running editor");
            Check(MobileModExtractor.IsProjectCodeFile("x/Foo.CS"), "the check ignores case");
            Check(MobileModExtractor.IsProjectCodeFile("x/plug.dll")
                  && MobileModExtractor.IsProjectCodeFile("x/plug.dylib")
                  && MobileModExtractor.IsProjectCodeFile("x/plug.so")
                  && MobileModExtractor.IsProjectCodeFile("x/plug.a"),
                  "plugin binaries are refused: Unity would load them");
            Check(MobileModExtractor.IsProjectCodeFile("x/Mod.asmdef")
                  && MobileModExtractor.IsProjectCodeFile("x/Mod.asmref")
                  && MobileModExtractor.IsProjectCodeFile("x/csc.rsp"),
                  "assembly definitions and compiler response files are refused too");
            // The allowed half matters just as much: these are the spellings DFU mods actually
            // use for script content, they are inert TextAssets, and refusing them would break
            // conversions for no safety gain. MobileModBuilder still refuses to REBUILD a mod
            // that carries them, which is the right place for that decision.
            Check(!MobileModExtractor.IsProjectCodeFile("x/Foo.cs.txt")
                  && !MobileModExtractor.IsProjectCodeFile("x/Foo.dll.bytes"),
                  "the .cs.txt / .dll.bytes spellings stay extractable - they are inert text");
            Check(!MobileModExtractor.IsProjectCodeFile("x/tex.png")
                  && !MobileModExtractor.IsProjectCodeFile("x/sound.wav")
                  && !MobileModExtractor.IsProjectCodeFile("x/data.json")
                  && !MobileModExtractor.IsProjectCodeFile(null),
                  "ordinary content, and a null path, are not code");
            // Native plugin sources: Unity gives these to PluginImporter and compiles them into
            // the player. Verified in this project on Assets/Plugins/iOS/DFMobilePointer.mm,
            // whose .meta is a PluginImporter. The rule is folder-independent on purpose, so it
            // does not matter whether Unity 6 still restricts that to a Plugins/ folder.
            Check(MobileModExtractor.IsProjectCodeFile("x/native.m")
                  && MobileModExtractor.IsProjectCodeFile("x/native.mm")
                  && MobileModExtractor.IsProjectCodeFile("x/native.c")
                  && MobileModExtractor.IsProjectCodeFile("x/native.cpp")
                  && MobileModExtractor.IsProjectCodeFile("x/native.h")
                  && MobileModExtractor.IsProjectCodeFile("x/native.swift"),
                  "native plugin sources are refused: Unity compiles them into the player");
            Check(MobileModExtractor.IsProjectCodeFile("x/lib.jar")
                  && MobileModExtractor.IsProjectCodeFile("x/lib.aar"),
                  "Android plugin archives are refused too");
            // A .meta is not content: it is the file that tells Unity how to import its
            // NEIGHBOUR. A hostile one rewrites a sibling's importer settings, or claims a GUID
            // that already belongs to a project asset - corrupting the project without ever
            // writing an asset.
            Check(MobileModExtractor.IsProjectCodeFile("x/tex.png.meta"),
                  "a .meta is refused: it rewrites how the file beside it is imported");

            // The sweep budget: bytes, not asset counts, because a mod's assets are wildly
            // uneven and it is the bytes that exhaust the machine.
            Check(MobileModExtractor.DefaultSweepBudgetBytes == 256L * 1024 * 1024,
                  "the default sweep budget is the argued-for 256MB",
                  MobileModExtractor.DefaultSweepBudgetBytes / (1024 * 1024) + "MB");

            // The watchdog clock. 10s is not a guess: a 790,320-sample clip from DREAM's sound
            // module completes in two editor ticks and 0.14s once the main loop is being handed
            // back, so the default is ~70x the measured worst case.
            Check(MobileModExtractor.DefaultAudioLoadTimeoutSeconds == 10
                  && MobileModExtractor.DefaultRunTimeoutSeconds == 4 * 60 * 60,
                  "the measured per-clip cap and the whole-run backstop are the argued-for ones",
                  MobileModExtractor.DefaultAudioLoadTimeoutSeconds + "s / "
                      + MobileModExtractor.DefaultRunTimeoutSeconds / 3600 + "h");
            // The disk floor shares this parser, so the same "never becomes no-limit" rule
            // covers it: a mistyped DFU_MOD_MIN_FREE_GB must not disable the guard.
            Check(MobileModExtractor.DefaultMinFreeGb == 4
                  && MobileModExtractor.ParsePositiveNumber("nope", 4, "G") == 4
                  && MobileModExtractor.ParsePositiveNumber("0", 4, "G") == 4,
                  "the disk floor is 4GB and a typo cannot switch it off");
            Check(MobileModExtractor.FreeBytesFor(".") != 0,
                  "free space on this volume is readable (or reported unknown, never zero)",
                  "free=" + MobileModExtractor.FreeBytesFor("."));
            Check(MobileModExtractor.ParsePositiveNumber("2.5", 10, "T") == 2.5
                  && MobileModExtractor.ParsePositiveNumber(" 30 ", 10, "T") == 30,
                  "a positive number of seconds is honoured, whitespace and all");
            Check(MobileModExtractor.ParsePositiveNumber(null, 10, "T") == 10
                  && MobileModExtractor.ParsePositiveNumber("", 10, "T") == 10
                  && MobileModExtractor.ParsePositiveNumber("soon", 10, "T") == 10
                  && MobileModExtractor.ParsePositiveNumber("0", 10, "T") == 10
                  && MobileModExtractor.ParsePositiveNumber("-5", 10, "T") == 10,
                  "unset, garbage, zero and negative all keep the default - never 'no timeout'");
        }

        /// <summary>
        /// A conversion that saved nothing must FAIL, not succeed quietly.
        ///
        /// This is the shape of the dream - music.dfmod case: every clip is unreadable, so
        /// extracted is empty, the rewritten manifest lists no files, and a build from it packs
        /// the manifest and nothing else. That bundle installs, loads, and contains no content -
        /// and the operator has no way to tell from an exit code of 0. Worse for a shell loop
        /// over a mods folder, which is how ten modules get converted: it would sail past.
        ///
        /// The fixture is the smallest possible version of it - one clip the extractor cannot
        /// read - so the assertion is about the RULE rather than about any particular mod.
        /// </summary>
        static void TestConversionRefusesEmptyResult()
        {
            const string emptyManifest = "Assets/Editor/TestFixtures/ExtractorFixture/empty-mod.dfmod.json";
            const string bundleDir = "Temp/MobileModExtractorEmptyTest";
            const string outDir = "Temp/MobileModExtractorEmptyOut";
            const string extractRoot = "Assets/Game/Mods/Converted/__empty__";
            if (Directory.Exists(bundleDir)) Directory.Delete(bundleDir, true);
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
            if (Directory.Exists(extractRoot)) { Directory.Delete(extractRoot, true); File.Delete(extractRoot + ".meta"); AssetDatabase.Refresh(); }

            string[] built = MobileModBuilder.BuildMod(emptyManifest, bundleDir,
                new[] { BuildTarget.StandaloneOSX });

            bool threw = false;
            string message = "no exception";
            try
            {
                MobileModExtractor.Convert(built[0], extractRoot, outDir,
                    new[] { BuildTarget.StandaloneOSX });
            }
            catch (Exception ex)
            {
                threw = true;
                message = ex.Message;
            }
            Check(threw, "a conversion that extracted nothing fails instead of returning", message);
            Check(threw && message.Contains("Converted nothing"),
                  "and it says so in words an operator can act on", message);
            // The point of failing is that no bundle exists to be installed by mistake.
            Check(!Directory.Exists(outDir)
                  || Directory.GetFiles(outDir, "*.dfmod", SearchOption.AllDirectories).Length == 0,
                  "no bundle is written for a conversion that would contain nothing");

            // ...but ONE SLICE of a run is a different question, and conflating the two stopped
            // dream - textures dead on its first slice. That module is 3443 textures alongside
            // 1201 Materials, GameObjects and Transforms, so a slice can legitimately draw only
            // types this converter does not handle. That is a fact about the slice, not a failed
            // conversion, and the other nine slices had real work in them.
            const string sliceRootA = "Assets/Game/Mods/Converted/__empty_s1__";
            const string sliceRootB = "Assets/Game/Mods/Converted/__empty_s2__";
            foreach (string r in new[] { sliceRootA, sliceRootB })
                if (Directory.Exists(r)) { Directory.Delete(r, true); File.Delete(r + ".meta"); }
            AssetDatabase.Refresh();

            bool sliceThrew = false;
            string sliceMessage = "no exception";
            try
            {
                MobileModExtractor.Convert(built[0], sliceRootA, outDir,
                    new[] { BuildTarget.StandaloneOSX }, 0, 2);
            }
            catch (Exception ex) { sliceThrew = true; sliceMessage = ex.Message; }
            Check(!sliceThrew, "a slice containing only unsupported types is NOT a failure",
                  sliceMessage);
            Check(!Directory.Exists(outDir)
                  || Directory.GetFiles(outDir, "*.dfmod", SearchOption.AllDirectories).Length == 0,
                  "and it still writes no bundle for itself");

            // The LAST slice is the one that can tell "this slice was empty" from "the whole
            // module converted nothing", because by then it can see whether any sibling slice
            // produced a bundle. Nothing did here, so this must still fail.
            bool lastThrew = false;
            string lastMessage = "no exception";
            try
            {
                MobileModExtractor.Convert(built[0], sliceRootB, outDir,
                    new[] { BuildTarget.StandaloneOSX }, 1, 2);
            }
            catch (Exception ex) { lastThrew = true; lastMessage = ex.Message; }
            Check(lastThrew && lastMessage.Contains("in any of 2 slices"),
                  "but a run where EVERY slice was empty still fails, on the last one",
                  lastMessage);

            foreach (string r in new[] { sliceRootA, sliceRootB })
                if (Directory.Exists(r)) { Directory.Delete(r, true); File.Delete(r + ".meta"); }

            Directory.Delete(bundleDir, true);
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
            if (Directory.Exists(extractRoot))
            {
                Directory.Delete(extractRoot, true);
                File.Delete(extractRoot + ".meta");
            }
            AssetDatabase.Refresh();
        }

        /// <summary>
        /// Converting a module in SLICES has to produce what one pass would have produced.
        ///
        /// Three DREAM modules have never converted, and not for want of RAM: Unity's import
        /// cache fills the disk (25GB on an 800MB module, on a machine with 22GB free). The
        /// cache can only be cleared with Unity stopped, so a slice is a whole process and the
        /// shell drives one per slice. That only helps if the slices actually tile the module -
        /// an asset lost at a boundary would be a silent hole in a converted mod, which is
        /// exactly the class of bug this whole suite exists to catch.
        ///
        /// So: extract the same bundle once whole and once in three slices, and require the
        /// union to be identical AND the slices to be disjoint. Set equality alone would pass
        /// if an asset appeared in two slices; disjointness alone would pass if one went
        /// missing. Both, or neither is worth much.
        /// </summary>
        static void TestChunkedConversion()
        {
            const string fixtureManifest = "Assets/Editor/TestFixtures/ExtractorFixture/fixture-mod.dfmod.json";
            const string bundleDir = "Temp/MobileModExtractorChunkTest";
            const string wholeRoot = "Assets/Game/Mods/Converted/__chunk_whole__";
            if (Directory.Exists(bundleDir)) Directory.Delete(bundleDir, true);
            foreach (string stale in Directory.GetDirectories("Assets/Game/Mods/Converted",
                         "__chunk*", SearchOption.TopDirectoryOnly))
            {
                Directory.Delete(stale, true);
                File.Delete(stale + ".meta");
            }
            AssetDatabase.Refresh();

            string[] built = MobileModBuilder.BuildMod(fixtureManifest, bundleDir,
                new[] { BuildTarget.StandaloneOSX });

            // One pass, for the reference set.
            var whole = MobileModExtractor.Extract(built[0], wholeRoot);
            var wholeSet = new HashSet<string>(StringComparer.Ordinal);
            foreach (string p in whole.extracted)
                wholeSet.Add(MobileModExtractor.RelativeToRoot(p, wholeRoot));

            // Three slices, each an independent extraction into its own folder - which is what
            // the real thing does, so that the previous slice's assets are not left on disk.
            var unionSet = new HashSet<string>(StringComparer.Ordinal);
            var guids = new HashSet<string>(StringComparer.Ordinal);
            var titles = new HashSet<string>(StringComparer.Ordinal);
            int overlap = 0, sliceTotal = 0;
            var sliceRoots = new List<string>();
            for (int i = 0; i < 3; i++)
            {
                string root = "Assets/Game/Mods/Converted/__chunk" + i + "__";
                sliceRoots.Add(root);
                var report = new ExtractReport();
                IEnumerator steps = MobileModExtractor.ExtractSteps(built[0], root, report, false, i, 3);
                while (steps.MoveNext()) { }
                sliceTotal += report.extracted.Count;
                foreach (string p in report.extracted)
                    if (!unionSet.Add(MobileModExtractor.RelativeToRoot(p, root)))
                        overlap++;

                ModInfo info = null;
                ModManager._serializer.TryDeserialize(
                    fsJsonParser.Parse(File.ReadAllText(report.manifestPath)), ref info);
                if (info != null) { guids.Add(info.GUID); titles.Add(info.ModTitle); }
                // The slice's own manifest must be named for the slice, or three slices would
                // overwrite one another's bundle.
                Check(report.manifestPath.Replace('\\', '/').EndsWith(
                          "fixture-mod (" + (i + 1) + " of 3).dfmod.json"),
                      "slice " + (i + 1) + " writes its own manifest", report.manifestPath);
            }

            Check(wholeSet.Count > 0 && unionSet.SetEquals(wholeSet),
                  "three slices extract exactly what one pass extracts",
                  "whole=" + wholeSet.Count + " union=" + unionSet.Count);
            Check(overlap == 0 && sliceTotal == wholeSet.Count,
                  "and no asset appears in two slices",
                  "overlap=" + overlap + " sliceTotal=" + sliceTotal);
            Check(guids.Count == 3, "each slice gets its own GUID - a shared one is a real clash",
                  "distinct GUIDs=" + guids.Count);
            Check(titles.Count == 3, "and its own title, so DFU's mod list is legible",
                  "distinct titles=" + titles.Count);
            // Derived, not random: converting the same module twice must produce the same
            // identities, or every re-conversion installs duplicates instead of replacing.
            Check(MobileModExtractor.DerivedGuid("abc", 1, 3) == MobileModExtractor.DerivedGuid("abc", 1, 3)
                  && MobileModExtractor.DerivedGuid("abc", 1, 3) != MobileModExtractor.DerivedGuid("abc", 2, 3)
                  && MobileModExtractor.DerivedGuid("abc", 1, 3) != MobileModExtractor.DerivedGuid("xyz", 1, 3),
                  "slice GUIDs are derived: stable across runs, distinct across slices");
            // A single slice must be indistinguishable from no slicing at all.
            // Colliding assets MUST share a slice, or both survive and the module ships two
            // mods claiming the same short name. This is the check that caught the first,
            // position-based slicing.
            Check(MobileModExtractor.SliceKeyOf("root/A/foo.tga")
                      == MobileModExtractor.SliceKeyOf("root/A/foo.png")
                  && MobileModExtractor.SliceKeyOf("root/A/FOO.PNG")
                      == MobileModExtractor.SliceKeyOf("root/a/foo.png"),
                  "assets that can collide onto one file share a slice key");
            Check(MobileModExtractor.SliceKeyOf("root/A/foo.png")
                      != MobileModExtractor.SliceKeyOf("root/B/foo.png"),
                  "but the same name in different folders does not");
            Check(MobileModExtractor.SliceOf("x", 4) == MobileModExtractor.SliceOf("x", 4)
                  && MobileModExtractor.SliceOf("x", 4) >= 0
                  && MobileModExtractor.SliceOf("x", 4) < 4,
                  "slice assignment is stable and in range");
            Check(MobileModExtractor.SliceName("x/dream - mobs.dfmod", 0, 1) == "dream - mobs"
                  && MobileModExtractor.SliceName("x/dream - mobs.dfmod", 1, 4) == "dream - mobs (2 of 4)",
                  "one slice is just the module; several are numbered",
                  MobileModExtractor.SliceName("x/dream - mobs.dfmod", 1, 4));

            // The per-asset contracts have to survive slicing: each slice writes the readable
            // sidecar for ITS OWN assets, or a sliced module loses the flags a whole one keeps.
            int sidecars = 0;
            foreach (string root in sliceRoots)
                if (File.Exists(Path.Combine(root, MobileModExtractor.ReadableSidecarName)))
                    sidecars++;
            Check(sidecars == 3, "every slice carries its own readable-texture sidecar",
                  "sidecars=" + sidecars);

            Directory.Delete(bundleDir, true);
            foreach (string root in sliceRoots)
            {
                Directory.Delete(root, true);
                File.Delete(root + ".meta");
            }
            Directory.Delete(wholeRoot, true);
            File.Delete(wholeRoot + ".meta");
            AssetDatabase.Refresh();
        }

        static void TestModExtractorSurvivesBadPaths()
        {
            const string clashManifest = "Assets/Editor/TestFixtures/ExtractorFixture/clash-mod.dfmod.json";
            const string okPayload = "Assets/Editor/TestFixtures/ExtractorFixture/fixture_data.json";
            const string filePayload = "Assets/Editor/TestFixtures/ExtractorFixture/hostile_payload.json";
            const string innerPayload = "Assets/Editor/TestFixtures/ExtractorFixture/hostile_escape_payload.json";
            const string dupePayload = "Assets/Editor/TestFixtures/ExtractorFixture/clash_dupe_payload.json";
            const string bundleDir = "Temp/MobileModExtractorClashTest";
            const string extractRoot = "Assets/Game/Mods/Converted/__clash__";
            if (Directory.Exists(bundleDir)) Directory.Delete(bundleDir, true);
            if (Directory.Exists(extractRoot)) { Directory.Delete(extractRoot, true); File.Delete(extractRoot + ".meta"); AssetDatabase.Refresh(); }

            const string dir = "assets/editor/testfixtures/extractorfixture/";
            var build = new AssetBundleBuild[1];
            build[0].assetBundleName = "clash-mod.dfmod";
            build[0].assetNames = new[] { okPayload, filePayload, innerPayload, dupePayload, clashManifest };
            build[0].addressableNames = new[] {
                dir + "clash_ok.json",
                dir + "clash",                      // a file...
                dir + "clash/inner.json",           // ...and the same name as a directory
                dir + "sub/../clash_ok.json",       // a second spelling of clash_ok.json
                dir + "clash-mod.dfmod.json" };

            Directory.CreateDirectory(bundleDir);
            BuildPipeline.BuildAssetBundles(bundleDir, build,
                BuildAssetBundleOptions.ChunkBasedCompression, BuildTarget.StandaloneOSX);

            var report = MobileModExtractor.Extract(Path.Combine(bundleDir, "clash-mod.dfmod"), extractRoot);

            // Whichever of the file/directory pair the bundle happens to enumerate first, exactly
            // one of them is unwritable - so these hold without depending on that order.
            int writeFailed;
            report.skippedByType.TryGetValue("write-failed", out writeFailed);
            Check(writeFailed == 1, "an unwritable path costs only its own asset",
                  "write-failed=" + writeFailed);

            int collisions;
            report.skippedByType.TryGetValue("collision", out collisions);
            Check(collisions == 1, "two spellings of one file are one collision, not two assets",
                  "collision=" + collisions);

            Check(report.extracted.Count == 2, "the rest of the mod still extracts",
                  "extracted=" + report.extracted.Count);

            Directory.Delete(bundleDir, true);
            Directory.Delete(extractRoot, true);
            File.Delete(extractRoot + ".meta");
            AssetDatabase.Refresh();
        }

        /// <summary>
        /// THE CHECK THAT MATTERS. Each direction bit must pair with the opposite bit on the
        /// neighbour it points at. If the direction-to-offset mapping had a sign error - most
        /// easily on north, since Daggerfall map pixel Y grows southward - reciprocity would
        /// collapse and every route would run the wrong way. Verified against the real data
        /// rather than assumed from reading it.
        /// </summary>
        static void TestRoadDirectionReciprocity()
        {
            if (!MobileRoadNetwork.Available)
                return;

            byte[] bits = { MobileRoadNetwork.N, MobileRoadNetwork.NE, MobileRoadNetwork.E,
                            MobileRoadNetwork.SE, MobileRoadNetwork.S, MobileRoadNetwork.SW,
                            MobileRoadNetwork.W, MobileRoadNetwork.NW };
            byte[] opposite = { MobileRoadNetwork.S, MobileRoadNetwork.SW, MobileRoadNetwork.W,
                                MobileRoadNetwork.NW, MobileRoadNetwork.N, MobileRoadNetwork.NE,
                                MobileRoadNetwork.E, MobileRoadNetwork.SE };
            int[] dx = { 0, 1, 1, 1, 0, -1, -1, -1 };
            int[] dy = { -1, -1, 0, 1, 1, 1, 0, -1 };

            int checked_ = 0, reciprocal = 0;

            for (int y = 1; y < MobileRoadNetwork.Height - 1 && checked_ < 4000; y++)
            {
                for (int x = 1; x < MobileRoadNetwork.Width - 1 && checked_ < 4000; x++)
                {
                    byte here = MobileRoadNetwork.PathsAt(x, y);
                    if (here == 0)
                        continue;

                    for (int d = 0; d < 8; d++)
                    {
                        if ((here & bits[d]) == 0)
                            continue;

                        checked_++;
                        byte there = MobileRoadNetwork.PathsAt(x + dx[d], y + dy[d]);
                        if ((there & opposite[d]) != 0)
                            reciprocal++;
                    }
                }
            }

            Check(checked_ > 500, "roads: found enough connections to test",
                  "connections examined: " + checked_);

            float ratio = checked_ > 0 ? (float)reciprocal / checked_ : 0f;
            Check(ratio > 0.9f, "roads: direction offsets agree with the data (reciprocity)",
                  string.Format("{0:P1} of {1} connections were reciprocal - a low value means " +
                                "the direction-to-offset mapping is wrong", ratio, checked_));
        }

        /// <summary>
        /// A route must be walkable: every step adjacent to the last, and every step actually
        /// carrying the path bit that permits it. A route that teleports or crosses open
        /// country would walk the player through terrain with no road under them.
        /// </summary>
        static void TestRoadRouting()
        {
            if (!MobileRoadNetwork.Available)
                return;

            // Find a start on the network, and a target far enough to be a real search.
            DFPosition start = null, target = null;
            for (int y = 20; y < MobileRoadNetwork.Height - 20 && start == null; y += 7)
                for (int x = 20; x < MobileRoadNetwork.Width - 20 && start == null; x += 7)
                    if (MobileRoadNetwork.HasAnyPath(x, y))
                        start = new DFPosition(x, y);

            if (start == null)
            {
                Check(false, "roads: found a starting pixel on the network");
                return;
            }

            for (int r = 6; r <= 40 && target == null; r += 2)
            {
                for (int d = 0; d < 8 && target == null; d++)
                {
                    int[] ox = { 0, 1, 1, 1, 0, -1, -1, -1 };
                    int[] oy = { -1, -1, 0, 1, 1, 1, 0, -1 };
                    int tx = start.X + ox[d] * r, ty = start.Y + oy[d] * r;
                    if (MobileRoadNetwork.InBounds(tx, ty) && MobileRoadNetwork.HasAnyPath(tx, ty))
                        target = new DFPosition(tx, ty);
                }
            }

            Check(target != null, "roads: found a distant pixel on the network to route to");
            if (target == null)
                return;

            System.Collections.Generic.List<DFPosition> route =
                MobileRoadNetwork.FindRoute(start.X, start.Y, target.X, target.Y);

            // No route between two arbitrary network pixels is a legitimate outcome - the
            // network is not fully connected - so absence is not a failure. What must never
            // happen is a route that is not walkable.
            if (route == null)
            {
                Check(true, "roads: unconnected pair correctly reports no route");
                return;
            }

            Check(route.Count > 0, "roads: route is non-empty");
            Check(route[route.Count - 1].X == target.X && route[route.Count - 1].Y == target.Y,
                  "roads: route ends at the destination");

            bool contiguous = true, onNetwork = true;
            DFPosition prev = start;
            foreach (DFPosition step in route)
            {
                int sx = step.X - prev.X, sy = step.Y - prev.Y;
                if (Mathf.Abs(sx) > 1 || Mathf.Abs(sy) > 1 || (sx == 0 && sy == 0))
                    contiguous = false;
                if (!MobileRoadNetwork.HasAnyPath(step.X, step.Y))
                    onNetwork = false;
                prev = step;
            }

            Check(contiguous, "roads: every step is adjacent to the last (no teleports)");
            Check(onNetwork, "roads: every step is on the network (no open country)");

            Check(MobileRoadNetwork.FindRoute(start.X, start.Y, start.X, start.Y).Count == 0,
                  "roads: routing to where you already are is an empty route");
        }


        /// <summary>
        /// A waypoint must not be steppable-over. Its own rect is 512 world units where a map
        /// pixel is 32768, so at high time compression a single frame covers far more than the
        /// rect - and a fixed arrival radius would be passed straight through, leaving the
        /// journey steering at a waypoint behind it indefinitely.
        /// </summary>
        static void TestWaypointOvershoot()
        {
            // Standing still or walking: the waypoint's own size governs.
            float still = MobileJourneyPilot.WaypointRadius(0f);
            Check(still > 0f, "waypoint: radius is positive when stationary");
            Near(MobileJourneyPilot.WaypointRadius(10f), still, 0.01f,
                 "waypoint: slow movement does not shrink the radius");

            // Fast: the radius must exceed the distance covered, or the waypoint is skipped.
            float[] speeds = { 500f, 2000f, 20000f, 200000f };
            bool alwaysCatchable = true;
            foreach (float perFrame in speeds)
            {
                if (MobileJourneyPilot.WaypointRadius(perFrame) <= perFrame)
                    alwaysCatchable = false;
            }
            Check(alwaysCatchable,
                  "waypoint: radius always exceeds one frame of travel, at any speed");

            // Monotonic - faster must never mean a smaller catch radius.
            bool monotonic = MobileJourneyPilot.WaypointRadius(100f) <=
                             MobileJourneyPilot.WaypointRadius(1000f) &&
                             MobileJourneyPilot.WaypointRadius(1000f) <=
                             MobileJourneyPilot.WaypointRadius(10000f);
            Check(monotonic, "waypoint: radius grows with speed");
        }

        /// <summary>
        /// Every OnGUI that draws through DaggerfallUI.DrawTexture must guard on
        /// EventType.Repaint. On the desktop path DrawTexture wraps GUI.DrawTexture, which
        /// Unity silently ignores outside a repaint event, so a missing guard costs nothing
        /// and stays invisible. On Metal - macOS upstream, and iOS here - it wraps
        /// Graphics.DrawTexture, an immediate draw that Unity documents as repaint-only.
        /// Unguarded, it also runs on layout and input events, into whatever render target is
        /// current and in an undefined rect. That is what put a hard-edged red block over the
        /// top of the screen while the player was taking hits, on top of the correct
        /// full-screen damage tint. Source scan rather than a behavioural test because the
        /// symptom only exists in a Metal player, but the rule it breaks is textual.
        /// </summary>
        static void TestImmediateModeDrawGuards()
        {
            string[] sources = Directory.GetFiles("Assets/Scripts", "*.cs", SearchOption.AllDirectories);
            var unguarded = new System.Collections.Generic.List<string>();
            int drawSites = 0;

            foreach (string file in sources)
            {
                string text = File.ReadAllText(file);
                if (!text.Contains("void OnGUI") || !text.Contains("DaggerfallUI.DrawTexture"))
                    continue;

                drawSites++;
                if (!text.Contains("EventType.Repaint"))
                    unguarded.Add(Path.GetFileName(file));
            }

            // Guards the premise: if the scan stops finding these files it has silently
            // stopped testing anything, and would keep passing.
            Check(drawSites >= 6, "the scan still finds the OnGUI sites that draw textures",
                  "sites=" + drawSites);

            Check(unguarded.Count == 0,
                  "every OnGUI drawing through DaggerfallUI.DrawTexture guards on EventType.Repaint",
                  string.Join(", ", unguarded.ToArray()));
        }

        /// <summary>
        /// The first-person spell cast animation must always terminate.
        ///
        /// FPSSpellCasting clears currentFrame back to -1 in exactly one place - inside the
        /// animation coroutine's loop body - and raises the release-frame event from inside that
        /// same body. That event synchronously runs arbitrary game logic: EntityEffectManager
        /// releases the spell there, assigning the bundle, starting effects and refreshing HUD
        /// icons and text. Unity terminates a coroutine for good once MoveNext() throws, so an
        /// exception escaping any listener used to leave currentFrame stranded at the release
        /// frame - the casting hands frozen on screen, and IsPlayingAnim blocking every later
        /// cast for the rest of the session. Self-targeted buffs (Chameleon, Slowfall) run far
        /// more listener code inside that coroutine than missile spells do, which is why those
        /// were the spells seen to strand on device.
        ///
        /// Drives the real coroutine by pumping MoveNext() the way Unity's scheduler does,
        /// including its stop-on-throw behaviour, so this reproduces the stuck pose rather than
        /// merely restating the fix.
        /// </summary>
        static void TestSpellCastAnimNeverStrands()
        {
            Type type = typeof(FPSSpellCasting);
            const BindingFlags priv = BindingFlags.NonPublic | BindingFlags.Instance;

            FieldInfo currentFrameField = type.GetField("currentFrame", priv);
            FieldInfo currentAnimsField = type.GetField("currentAnims", priv);
            FieldInfo frameIndicesField = type.GetField("frameIndices", priv);
            MethodInfo animMethod = type.GetMethod("AnimateSpellCast", priv);
            Type recordType = type.GetNestedType("AnimationRecord", BindingFlags.NonPublic);

            // Guards the premise: if these stop resolving, every assertion below would pass
            // vacuously while testing nothing.
            bool wired = currentFrameField != null && currentAnimsField != null &&
                         frameIndicesField != null && animMethod != null && recordType != null;
            Check(wired, "spell cast: the animation state machine is still reachable to test");
            if (!wired)
                return;

            FieldInfo castAnimsField = type.GetField("castAnims", priv);
            Check(castAnimsField != null, "spell cast: the animation cache is still reachable to test");
            if (castAnimsField == null)
                return;

            GameObject go = new GameObject("SelfTest_FPSSpellCasting");
            bool logging = Debug.unityLogger.logEnabled;
            try
            {
                FPSSpellCasting casting = go.AddComponent<FPSSpellCasting>();
                int frameCount = ((int[])frameIndicesField.GetValue(casting)).Length;
                var castAnims = (System.Collections.IDictionary)castAnimsField.GetValue(casting);

                // Seeding the cache lets PlayOneShot run for real without arena2 on disk:
                // SetCurrentAnims returns the cached entry before it touches any file.
                castAnims[ElementTypes.Magic] = Array.CreateInstance(recordType, frameCount);
                castAnims[ElementTypes.Fire] = Array.CreateInstance(recordType, 0);

                // Stands in for whatever throws on device. The point is not which listener fails
                // but that a cast must survive any of them.
                int raised = 0;
                FPSSpellCasting.OnReleaseFrameEventHandler thrower = delegate
                {
                    raised++;
                    throw new InvalidOperationException("self test: listener failed at release frame");
                };
                casting.OnReleaseFrame += thrower;

                // The listener's exception is expected here and is logged by the fix; keep it out
                // of the build log so a genuine failure stays easy to spot.
                Debug.unityLogger.logEnabled = false;
                casting.PlayOneShot(ElementTypes.Magic);
                bool entered = (int)currentFrameField.GetValue(casting) == 0;
                bool survived = PumpAnim(animMethod, casting, frameCount * 4);

                // The invariant that actually broke on device: after a cast whose listener threw,
                // the next cast must still be accepted.
                bool secondAccepted = false;
                bool secondReachedRelease = false;
                int raisedAfterFirst = raised;
                if (!casting.IsPlayingAnim)
                {
                    casting.PlayOneShot(ElementTypes.Magic);
                    secondAccepted = (int)currentFrameField.GetValue(casting) == 0;
                    PumpAnim(animMethod, casting, frameCount * 4);
                    secondReachedRelease = raised > raisedAfterFirst;
                }
                Debug.unityLogger.logEnabled = logging;

                Check(entered, "spell cast: a normal cast enters the animation");
                Check(raised > 0, "spell cast: the release frame is actually reached",
                      "raised=" + raised);
                Check(survived,
                      "spell cast: a throwing release-frame listener does not kill the animation coroutine");
                Check((int)currentFrameField.GetValue(casting) < 0,
                      "spell cast: a throwing release-frame listener leaves no stuck casting pose",
                      "currentFrame=" + currentFrameField.GetValue(casting));
                Check(secondAccepted,
                      "spell cast: a second cast is still accepted after a listener threw on the first");
                Check(secondReachedRelease,
                      "spell cast: the second cast reaches its release frame, so it really casts");

                casting.OnReleaseFrame -= thrower;

                // A cast with nothing to animate must still release its spell - losing the
                // animation must not cost the player the spell - and must do it exactly once.
                int quietRaised = 0;
                FPSSpellCasting.OnReleaseFrameEventHandler counter = delegate { quietRaised++; };
                casting.OnReleaseFrame += counter;

                Debug.unityLogger.logEnabled = false;
                casting.PlayOneShot(ElementTypes.Fire);
                bool stayedOutOfAnim = !casting.IsPlayingAnim;
                PumpAnim(animMethod, casting, 12);
                Debug.unityLogger.logEnabled = logging;

                Check(stayedOutOfAnim,
                      "spell cast: a cast with no animation does not enter the animation state");
                Check(quietRaised == 1,
                      "spell cast: a cast with no animation still releases its spell exactly once",
                      "raised=" + quietRaised);
                Check((int)currentFrameField.GetValue(casting) < 0,
                      "spell cast: an empty animation leaves no stuck casting pose",
                      "currentFrame=" + currentFrameField.GetValue(casting));
                Check(!casting.IsPlayingAnim,
                      "spell cast: IsPlayingAnim always clears, so later casts stay possible");

                casting.OnReleaseFrame -= counter;
            }
            finally
            {
                Debug.unityLogger.logEnabled = logging;
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        /// <summary>
        /// The other half of the same failure, and the one that actually stopped casting on device.
        ///
        /// EntityEffectManager.Update() re-fires any ready spell flagged instantCast on the very
        /// next frame, and every caster-only spell is flagged that way. The release handler clears
        /// readySpell and instantCast at the very end, after AssignBundle - so when AssignBundle
        /// threw part way (HUD icon refresh runs there for caster-only spells), the spell applied
        /// but the ready spell was never cleared. It then re-cast every frame, holding
        /// castInProgress true, and SetReadySpell() refuses new spells while that is set: the first
        /// cast worked and the cast button was dead from then on. The teardown has to be in a
        /// finally, so pin that it stays there.
        /// </summary>
        static void TestCastStateTearsDownOnFailure()
        {
            string path = "Assets/Scripts/Game/MagicAndEffects/EntityEffectManager.cs";
            Check(File.Exists(path), "cast state: EntityEffectManager is where expected", path);
            if (!File.Exists(path))
                return;

            string text = File.ReadAllText(path);
            int handler = text.IndexOf("private void PlayerSpellCasting_OnReleaseFrame", StringComparison.Ordinal);
            Check(handler >= 0, "cast state: the release handler is still there to check");
            if (handler < 0)
                return;

            // Bounded to the handler body so an unrelated finally elsewhere cannot satisfy this.
            int end = text.IndexOf("private void EntityEffectBroker_OnNewMagicRound", handler, StringComparison.Ordinal);
            if (end < 0)
                end = Math.Min(text.Length, handler + 4000);
            string body = text.Substring(handler, end - handler);

            int fin = body.IndexOf("finally", StringComparison.Ordinal);
            Check(fin >= 0, "cast state: the release handler tears the cast state down in a finally");
            if (fin < 0)
                return;

            string teardown = body.Substring(fin);
            Check(teardown.Contains("readySpell = null"),
                  "cast state: readySpell is cleared in the finally, so it cannot re-cast forever");
            Check(teardown.Contains("instantCast = false"),
                  "cast state: instantCast is cleared in the finally, so Update() stops re-firing it");
        }

        /// <summary>
        /// Steps the animation coroutine the way Unity's scheduler would, reproducing the part
        /// that matters here: Unity never resumes a coroutine whose MoveNext() threw.
        /// </summary>
        /// <returns>False if the coroutine died on an exception.</returns>
        static bool PumpAnim(MethodInfo animMethod, FPSSpellCasting casting, int steps)
        {
            IEnumerator anim = (IEnumerator)animMethod.Invoke(casting, null);
            for (int i = 0; i < steps; i++)
            {
                try
                {
                    if (!anim.MoveNext())
                        return true;
                }
                catch (Exception)
                {
                    return false;
                }
            }

            return true;
        }

        #endregion




    }
}
