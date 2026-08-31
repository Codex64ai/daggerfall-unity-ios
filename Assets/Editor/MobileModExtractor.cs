// Project:         Daggerfall Unity - iOS Touch Input Layer
// License:         MIT License
//
// Extracts a desktop-built .dfmod back into loose project assets so MobileModBuilder can
// repack it for iOS. Mods are distributed only as desktop AssetBundles; iOS needs its own
// bundles, so conversion means: load bundle -> write assets to disk (short names preserved
// verbatim - every DFU runtime lookup is by short name) -> rewrite the manifest -> rebuild.
//
// One mod at a time, from the command line (Convert is the same chain as a method call):
//
//   env DFU_MOD_IN="$HOME/Downloads/dream - sound.dfmod" DFU_MOD_OUT=$HOME/dev/dfu-mods \
//   Unity -batchmode -projectPath <proj> \
//     -executeMethod DaggerfallWorkshop.Game.Mobile.EditorTools.MobileModExtractor.ConvertFromEnv
//
//   DFU_MOD_IN            the desktop .dfmod to convert            required
//   DFU_MOD_OUT           where the rebuilt bundles are written    default ~/dev/dfu-mods
//   DFU_MOD_TARGETS       comma-separated Unity BuildTarget names  default iOS
//   DFU_MOD_AUDIO_TIMEOUT seconds allowed for one clip's load      default 10
//   DFU_MOD_TIMEOUT       seconds allowed for the whole run        default 14400
//
// NO -quit AND NO -nographics, and neither is an oversight. -nographics: see the GPU-blit note
// below. -quit: the conversion has to hand control back to Unity between steps (see the audio
// note), and -quit kills the process before the first frame of that ever runs - so it would
// convert NOTHING and exit 0. ConvertFromEnv refuses it outright rather than letting that
// happen, and exits by itself instead: 0 when a bundle was written, 1 on failure - including a
// conversion that saved nothing, which never gets a bundle - and 2 if the watchdog gives up.
//
// v1 handles Texture2D, AudioClip and TextAsset (added per task); everything else is
// skipped and counted in the report. Extraction output goes under Assets/Game/Mods/Converted/,
// which is gitignored - converted third-party content must never be committed.
//
// Bundle textures are normally compressed and non-readable, so decoding them means a GPU
// blit: run the editor WITHOUT -nographics or extraction refuses (see TexturePng.Encode).
//
// Two limitations worth knowing before converting a real mod. Textures are always re-encoded
// as .png, and DFU keys runtime lookups on the lowercased short name WITH its extension
// (ModManager.GetAssetName), so a source foo.tga becomes reachable only as foo.png; DFU's own
// convention is .png (TextureReplacement), so this is normally a no-op, but it is counted in
// the report rather than left silent. And asset paths are rebuilt from the source manifest,
// because AssetBundle.GetAllAssetNames lowercases them while Mod.FindAssetNames matches
// case-sensitively - an asset the manifest does not list cannot have its casing recovered.
//
// Audio comes back as 16-bit PCM .wav and nothing else. A bundle keeps an AudioClip as decoded
// samples, not as the file the author imported, so there is no original to copy out - the
// container is rebuilt around the samples (EncodeWav) and a source .ogg therefore changes its
// runtime lookup name to .wav, counted like the texture case.
//
// AND IT ONLY REACHES CLIPS THE AUTHOR IMPORTED AS DecompressOnLoad. AudioClip.GetData reads
// decoded PCM, so a Streaming clip (samples never in memory) and a CompressedInMemory one
// (samples still encoded, with no API that returns the encoded bytes) are both unreachable.
// DecompressOnLoad is Unity's own default, so a clip nobody configured converts fine; a clip
// somebody DID configure - which is what a large mod's music tends to be - may not. Those are
// skipped, counted under "AudioClip(streaming)" / "AudioClip(compressed)" and warned about
// individually, because for a music module that skip may be the entire module and the operator
// has to learn it from the report rather than from a silent game. There is no workaround on
// this side: the fix is the source audio, or a desktop rebuild of that module with its clips
// set to DecompressOnLoad.
//
// AND RESIDENCY IS NOT LOAD TYPE, which is why this tool is shaped like a driver rather than
// like a function. DecompressOnLoad says how a clip is decoded, not that it is decoded yet: a
// clip with Preload Audio Data off arrives with no samples and has to be asked for them, and a
// clip that ALSO has Load In Background set answers that request ASYNCHRONOUSLY - a load only
// Unity's main loop can integrate. Measured against DREAM's sound module: the operation reaches
// progress 1.00 and never flips isDone; Thread.Sleep, QueuePlayerLoopUpdate,
// DisplayProgressBar, AssetDatabase.Refresh, a synchronous LoadAsset and UnloadUnusedAssets all
// leave it Loading after 30s, and EditorApplication.wantsToQuit is never called under -quit so
// the quit cannot even be deferred. Returning to EditorApplication.update completes the same
// load in two ticks and 0.14s. That was 34 of 340 clips and 45% of that module's audio, so the
// extraction is an iterator (ExtractSteps) that yields while it waits, ConvertFromEnv steps it
// from EditorApplication.update, and the command line loses its -quit. Anything that still will
// not load after DFU_MOD_AUDIO_TIMEOUT is counted under "AudioClip(async)", so a regression
// here shows up as a number rather than as silence.
//
// Textures are not all colour. A compressed normal map has had its blue channel thrown away
// and the remaining two swizzled into whichever channels its block format codes best, so
// extracting one byte-for-byte produces an image that is not a normal map at all; DFU's
// *_Normal / *_Height / *_MetallicGloss suffixes are what tells them apart, and the extractor
// rebuilds z and keeps them out of sRGB (see NormalUnswizzlerFor).
//
// MobileConvertedModImporter at the bottom of this file decides how the extraction is then
// imported. That policy - compressed, not readable, size-capped, normal maps typed - is what
// makes a multi-gigabyte texture pack fit in an iPad's memory, so it is not cosmetic. Its
// levers are environment variables (MobileConvertedModPolicy), because they get tuned against
// a real device and recompiling per attempt is the wrong loop:
//
//   DFU_MOD_MAXTEXSIZE   cap, power of two 32-16384          default 1024
//   DFU_MOD_ASTC         iOS block size, 4x4|5x5|6x6|8x8|10x10|12x12   default 6x6
//   DFU_MOD_TEX_QUALITY  compressor effort 0-100             default 50
//   DFU_MOD_MIPS         master mipmap switch                default on
//   DFU_MOD_NOMIP        path substrings that get no mipmaps default DFU's 2D art
//   DFU_MOD_STREAM_MIPS  mipmap streaming                    default off (see the property)
//
// A change to one of these reaches a mod on its next conversion; Unity's import cache cannot
// see an environment variable move, so re-convert rather than expecting a reimport.
//
// A .dfmod is untrusted input: it is a file a stranger hands us, and the manifest inside it is
// the part of a mod an attacker fully controls. Every output path is therefore checked for
// containment under outputRoot before a single byte is written (see IsInsideRoot).
//
// Place in Assets/Editor/

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using FullSerializer;
using UnityEditor;
using UnityEngine;

namespace DaggerfallWorkshop.Game.Mobile.EditorTools
{
    /// <summary>The outcome of one extraction. The two counters answer different questions and
    /// must never be summed together: skippedByType counts assets that did NOT reach the
    /// extraction - an unsupported type, a colliding output path, a path that escaped the root,
    /// a write that failed - so its total is a genuine loss report and is what a caller should
    /// show as "N assets skipped" - and for audio it is the ONLY record that a clip could not be
    /// decoded at all, so it has to be read per key and not merely totalled. notesByType counts
    /// assets that WERE extracted but needed something said about them: a texture re-encoded to
    /// .png (or a clip to .wav) under a new runtime lookup name, or a path whose capitalisation
    /// the source manifest did not record.</summary>
    public class ExtractReport
    {
        public string manifestPath;
        public List<string> extracted = new List<string>();
        /// <summary>Bundle assets that were loaded into memory, and how many of those were
        /// handed back with Release. They must match. A conversion that loads three thousand
        /// assets and releases six is holding the whole mod resident, which is the difference
        /// between a large module converting and the editor being killed part way through - and
        /// it is invisible in every other number here, so it is reported on its own.</summary>
        public int loaded;
        public int released;
        public Dictionary<string, int> skippedByType = new Dictionary<string, int>();
        public Dictionary<string, int> notesByType = new Dictionary<string, int>();
    }

    public static class MobileModExtractor
    {
        /// <summary>Extracts a bundle without ever handing control back to Unity.
        ///
        /// Convenient, and it costs one thing: a clip whose audio data loads ASYNCHRONOUSLY can
        /// never become resident while this call is on the stack, because the load is integrated
        /// by the main loop this call is blocking. Those clips are refused at once under
        /// "AudioClip(async)" rather than waited on - waiting cannot work here, and a wait that
        /// cannot work is just a stall. ConvertFromEnv drives the same steps across editor ticks
        /// and does get them; see ExtractSteps.</summary>
        public static ExtractReport Extract(string dfmodPath, string outputRoot)
        {
            var report = new ExtractReport();
            IEnumerator steps = ExtractSteps(dfmodPath, outputRoot, report, false);
            while (steps.MoveNext()) { }
            return report;
        }

        /// <summary>The extraction, as steps rather than as one blocking call.
        ///
        /// It yields in exactly one place: waiting for a clip's asynchronous audio load. That is
        /// the only thing here that Unity cannot finish while this code holds the main thread,
        /// and it is worth the whole restructure because it is 45% of a real sound module's
        /// audio (see the file header).
        ///
        /// <paramref name="canYield"/> says whether anyone is actually going to pump this. When
        /// false - Extract, and therefore the self-test - an asynchronous load is refused
        /// immediately instead of waited on, because nothing will ever integrate it. When true
        /// the wait is real, and bounded by AudioLoadTimeoutSeconds so one bad clip cannot hang
        /// a conversion that no longer has -quit to end it.</summary>
        public static IEnumerator ExtractSteps(string dfmodPath, string outputRoot,
            ExtractReport report, bool canYield)
        {
            if (!File.Exists(dfmodPath))
                throw new FileNotFoundException("Desktop .dfmod not found", dfmodPath);

            AssetBundle ab = AssetBundle.LoadFromFile(dfmodPath);
            if (ab == null)
                throw new InvalidDataException("Could not load AssetBundle (wrong platform or corrupt): " + dfmodPath);

            try
            {
                // Read the manifest before anything else: besides identity it is the only record
                // of the author's original path capitalisation, which the extraction has to keep.
                ModInfo modInfo = ReadManifest(ab, dfmodPath);
                var originalCase = new Dictionary<string, string>(StringComparer.Ordinal);
                if (modInfo.Files != null)
                {
                    foreach (string file in modInfo.Files)
                    {
                        string norm = file.Replace('\\', '/');
                        originalCase[norm.ToLowerInvariant()] = norm;
                    }
                }

                // One output FILE may be claimed by only one bundle asset; see Claim().
                var claimed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                bool loggedAudioLoad = false;   // see TryEnsureAudioData: logged once, counted always

                foreach (string assetName in ab.GetAllAssetNames())
                {
                    if (assetName.EndsWith(ModManager.MODINFOEXTENSION, StringComparison.Ordinal))
                        continue; // rewritten below, not copied verbatim

                    // Notes describe assets that SURVIVED, so they are held until the write has
                    // actually happened and thrown away if it has not; see CommitNotes.
                    var notes = new List<string>();
                    string outPath = OutputPathFor(assetName, outputRoot, originalCase, notes);

                    // Before the asset is even loaded: a path that Unity would COMPILE or LOAD
                    // rather than import is refused outright. Leaving this to MobileModBuilder's
                    // script guard would be too late by a whole compilation - the file would
                    // already be in Assets/ and already built into the editor's assemblies.
                    if (IsProjectCodeFile(outPath))
                    {
                        Skipped(report, "code-file-refused");
                        Debug.LogWarning($"[MobileModExtractor] refusing to write {assetName} to " +
                            $"'{outPath}': the extraction root is inside Assets/, so Unity would " +
                            "compile or load that file rather than import it, and a .dfmod is " +
                            "untrusted input. The asset is skipped; the rest of the mod converts.");
                        continue;
                    }

                    var obj = ab.LoadAsset<UnityEngine.Object>(assetName);
                    if (obj != null)
                        report.loaded++;

                    // Every branch below is wrapped so the asset's native memory goes back the
                    // moment this tool is done with it - including the ones that refuse it. See
                    // Release: without this the loop holds every decoded clip and every decoded
                    // texture in the mod at once, which is what a multi-gigabyte module cannot
                    // afford. `continue` inside a try still runs the finally, which is what makes
                    // the skip and containment-failure paths release too.
                    try
                    {
                        if (obj is Texture2D tex2d)
                        {
                            // Textures are always re-encoded as .png, which moves the runtime lookup
                            // key with them (it is the short name WITH extension). Report it.
                            if (!outPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                            {
                                notes.Add("extension-rewritten");
                                Debug.LogWarning($"[MobileModExtractor] {assetName} is re-encoded as .png, " +
                                    "so its runtime lookup name changes with it");
                                outPath = Path.ChangeExtension(outPath, ".png");
                            }
                            if (!Claim(claimed, outPath, assetName, report))
                                continue;
                            // Whether the blit degammas is decided by the SOURCE texture's graphics
                            // format and by nothing else. Choosing it from the file NAME instead
                            // looks equivalent and is not: a stranger's bundle can perfectly well
                            // contain an sRGB-flagged "*_Height" (nothing forces a height map's
                            // sRGBTexture off, and the importer default is on), and reading that
                            // through a Linear RenderTexture degammas on sample with nothing
                            // re-encoding on write - every mid-tone byte moved, silently. The name
                            // rule still decides the DESTINATION policy, where it belongs, in
                            // MobileConvertedModImporter. Here the source is the only authority.
                            bool linear = !tex2d.isDataSRGB;
                            Func<Color32, Color32> unswizzle = IsNormalMapName(assetName)
                                ? NormalUnswizzlerFor(tex2d.format, tex2d.isDataSRGB, notes) : null;
                            if (!TryWriteFile(outPath, TexturePng.Encode(tex2d, linear, unswizzle),
                                    outputRoot, assetName, report))
                                continue;
                            report.extracted.Add(outPath);
                            CommitNotes(report, notes);
                        }
                        else if (obj is TextAsset textAsset)
                        {
                            if (!Claim(claimed, outPath, assetName, report))
                                continue;
                            if (!TryWriteFile(outPath, textAsset.bytes, outputRoot, assetName, report))
                                continue;
                            report.extracted.Add(outPath);
                            CommitNotes(report, notes);
                        }
                        else if (obj is AudioClip clip)
                        {
                            // A bundle keeps an AudioClip as samples, not as the author's file,
                            // so the extraction has to re-author a container - and the only one
                            // that can be written from raw samples with no encoder is PCM WAV.
                            //
                            // THE LIMIT OF THIS WHOLE APPROACH. AudioClip.GetData reads DECODED
                            // PCM, and only a DecompressOnLoad clip has any: Unity's own message is
                            // "Cannot get data on compressed samples for audio clip ... Changing the
                            // load type to DecompressOnLoad on the audio clip will fix this". A
                            // Streaming clip never had its samples in memory; a CompressedInMemory
                            // one holds them still encoded, and there is no API that hands the
                            // encoded bytes back either. Neither is recoverable here at all.
                            //
                            // Unity's own default for an audio import is DecompressOnLoad, so the
                            // clip an author never thought about DOES convert. The one an author
                            // thought about may not: CompressedInMemory and Streaming are exactly
                            // what a large mod's music gets set to, and a music module is where a
                            // whole-mod loss would land.
                            //
                            // They are refused up front rather than left to fail inside GetData for
                            // two reasons: GetData logs a Unity error per clip, and a music mod with
                            // a thousand of them would bury the report that matters under a thousand
                            // stack traces; and the load type is the actual diagnosis, so it belongs
                            // in the warning. Each is counted under its own key, because the two need
                            // different things from the mod author.
                            //
                            // Both refusals come BEFORE the rewrite and before Claim on purpose:
                            // a clip whose samples cannot be read is not a writer, so it must
                            // neither announce a rewrite it will not perform nor reserve an
                            // output path a sibling could have used (a streaming "song.ogg"
                            // beside a readable "song.wav" is one mod away).
                            if (clip.loadType != AudioClipLoadType.DecompressOnLoad)
                            {
                                Skipped(report, clip.loadType == AudioClipLoadType.Streaming
                                    ? "AudioClip(streaming)" : "AudioClip(compressed)");
                                Debug.LogWarning($"[MobileModExtractor] skipped {assetName}: its " +
                                    $"load type is {clip.loadType}, and AudioClip.GetData can only " +
                                    "read a clip imported as DecompressOnLoad. The samples are not " +
                                    "reachable through any API here, so this clip cannot be " +
                                    "converted from the bundle at all - it has to come from the " +
                                    "module's source audio, or from a rebuild of the desktop mod " +
                                    "with this clip set to DecompressOnLoad.");
                                continue;
                            }

                            // DecompressOnLoad says how the clip is decoded, NOT that it is
                            // decoded right now, and the difference cost 34 of DREAM's 340 sound
                            // effects on the first real conversion: every long ambient loop came
                            // back "GetData failed on a DecompressOnLoad clip", because its
                            // samples were simply not resident yet. DFU's own runtime already
                            // knew this - SoundReplacement does "if (audioClip.preloadAudioData
                            // || audioClip.LoadAudioData())" before touching a mod clip - and
                            // this is the same dance, done here.
                            var audio = new AudioLoad();
                            IEnumerator load = EnsureAudioData(clip, notes, canYield, audio);
                            while (load.MoveNext())
                                yield return null;   // let Unity integrate the async load
                            string audioDetail = audio.detail;
                            if (!audio.ok)
                            {
                                // An asynchronous load that never arrived is a DRIVER problem,
                                // not a property of the clip, so it keeps its own key: the same
                                // file converts once the converter is stepped across editor
                                // ticks. Anything else here is the clip itself.
                                Skipped(report, audio.unreachable
                                    ? "AudioClip(async)" : "AudioClip(nodata)");
                                Debug.LogWarning($"[MobileModExtractor] skipped {assetName}: " +
                                    $"{audioDetail}. The clip reports {clip.samples} samples " +
                                    $"across {clip.channels} channels but will not hand them " +
                                    "over, so it is skipped rather than written as silence." +
                                    (audio.unreachable
                                        ? " An asynchronous load is completed only by Unity's main" +
                                          " loop; run the converter WITHOUT -quit so it can be" +
                                          " driven across editor ticks (see README-iOS.md)."
                                        : string.Empty));
                                continue;
                            }
                            if (audioDetail != null && !loggedAudioLoad)
                            {
                                // Once per conversion, not once per clip: a module where every
                                // clip needs this would otherwise bury its own report. The count
                                // is in the summary line either way.
                                loggedAudioLoad = true;
                                Debug.Log($"[MobileModExtractor] {assetName}: {audioDetail}. " +
                                    "Further clips needing the same are counted in the report " +
                                    "rather than logged one by one.");
                            }

                            var samples = new float[clip.samples * clip.channels];
                            if (!clip.GetData(samples, 0))
                            {
                                // Backstop: the load type said this should have worked, and the
                                // samples are resident. Something else did not - a clip with no
                                // samples, or a Unity-version change in what GetData accepts -
                                // and either way it is a loss, not a note.
                                Skipped(report, "AudioClip(nodata)");
                                Debug.LogWarning($"[MobileModExtractor] skipped {assetName}: GetData " +
                                    $"failed on a resident {clip.loadType} clip ({clip.samples} " +
                                    $"samples, {clip.channels} channels), which is not supposed to " +
                                    "happen; the clip is skipped rather than written as silence.");
                                continue;
                            }

                            // Only now the container rewrite, and only for a clip that is
                            // actually going to be written. Announcing "re-encoded as .wav" and
                            // then announcing a skip is two warnings per clip, one of them
                            // false, on exactly the module whose log we most need to read - so
                            // it sits below BOTH refusals, not just the load-type one.
                            //
                            // And unlike the texture case this is a note, not a hazard. DFU
                            // looks mod audio up by EXTENSIONLESS name - TryImportSound and
                            // TryImportSong pass sound.ToString()/song.ToString() straight into
                            // ModManager.TryGetAsset, which asks AssetBundle.Contains - so a
                            // source .ogg arriving as .wav still answers to the same key. What
                            // does move is Mod.FindAssetNames(dir, ".ogg"), which filters a
                            // directory listing by extension; that is the residual risk, and it
                            // is why this is still counted rather than passed over in silence.
                            if (!outPath.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
                            {
                                notes.Add("extension-rewritten");
                                Debug.LogWarning($"[MobileModExtractor] {assetName} is re-encoded " +
                                    "as .wav. DFU's own audio lookups are extensionless, so this " +
                                    "does not change how the clip is found; a mod that enumerates " +
                                    "its own directory by \".ogg\" would no longer see it.");
                                outPath = Path.ChangeExtension(outPath, ".wav");
                            }
                            if (!Claim(claimed, outPath, assetName, report))
                                continue;
                            if (!TryWriteFile(outPath, EncodeWav(samples, clip.channels, clip.frequency),
                                    outputRoot, assetName, report))
                                continue;
                            report.extracted.Add(outPath);
                            CommitNotes(report, notes);
                        }
                        else
                        {
                            string type = obj ? obj.GetType().Name : "null";
                            Skipped(report, type);
                            Debug.LogWarning($"[MobileModExtractor] skipped {assetName} ({type} not supported yet)");
                        }
                    }
                    finally
                    {
                        Release(obj, report);
                    }
                }

                // Manifest identity is preserved; only Files points at the extraction.
                modInfo.Files = new List<string>(report.extracted);
                ModManager._serializer.TrySerialize(modInfo, out fsData data);
                string manifestOut = Path.Combine(outputRoot,
                    Path.GetFileNameWithoutExtension(dfmodPath) + ModManager.MODINFOEXTENSION);
                // Same containment check; but a conversion with no manifest is not a mod, so
                // unlike a single bad asset this one cannot be skipped past.
                if (!TryWriteFile(manifestOut, System.Text.Encoding.UTF8.GetBytes(fsJsonPrinter.PrettyJson(data)),
                        outputRoot, "the rewritten manifest", report))
                    throw new InvalidDataException(
                        "Refusing to write the rewritten manifest outside the extraction root: " + manifestOut);
                report.manifestPath = manifestOut;
            }
            finally
            {
                ab.Unload(true);
            }

            AssetDatabase.Refresh();
            foreach (string p in report.extracted)
                AssetDatabase.ImportAsset(p, ImportAssetOptions.ForceUpdate);
            LogSummary(report, dfmodPath, outputRoot);
        }

        /// <summary>Desktop .dfmod in, iOS .dfmod out, in one call: extract the bundle to loose
        /// assets under extractRoot, then repack the rewritten manifest for each target under
        /// bundleOutRoot. Returns the built bundle paths (one per target).
        ///
        /// Deliberately takes all four values as arguments and reads no environment: this is the
        /// call the self-test drives, and a function that reached for DFU_MOD_OUT on its own
        /// would be testable only by mutating the process environment. ConvertFromEnv below is
        /// where the environment is read, and it is the only place.</summary>
        public static string[] Convert(string dfmodPath, string extractRoot, string bundleOutRoot,
            BuildTarget[] targets)
        {
            var built = new List<string>();
            IEnumerator steps = ConvertSteps(dfmodPath, extractRoot, bundleOutRoot, targets,
                false, built);
            while (steps.MoveNext()) { }
            return built.ToArray();
        }

        /// <summary>The whole conversion as steps, so ConvertFromEnv can drive it across editor
        /// ticks; see ExtractSteps for why that matters.</summary>
        public static IEnumerator ConvertSteps(string dfmodPath, string extractRoot,
            string bundleOutRoot, BuildTarget[] targets, bool canYield, List<string> builtInto)
        {
            var report = new ExtractReport();
            IEnumerator steps = ExtractSteps(dfmodPath, extractRoot, report, canYield);
            while (steps.MoveNext())
                yield return null;

            // A conversion that saved nothing is a FAILURE, not a quiet success. Without this
            // the rewritten manifest lists no files, BuildMod packs the manifest alone, and the
            // operator gets a .dfmod that installs, loads, and does nothing at all - which is
            // exactly what dream - music.dfmod produces (0 of 81 clips). A bundle that cannot
            // possibly work must not be written, and the exit code has to say so, because the
            // whole point of a per-mod exit code is that a shell loop over a mods folder stops.
            if (report.extracted.Count == 0)
                throw new InvalidDataException(
                    "Converted nothing from " + Path.GetFileName(dfmodPath) + ": all " +
                    Total(report.skippedByType) + " assets were skipped [" +
                    Describe(report.skippedByType) + "]. Refusing to write a bundle that would " +
                    "install, load and contain no content. The extraction under '" + extractRoot +
                    "' is left in place so the skips can be inspected.");

            string[] builtPaths = MobileModBuilder.BuildMod(report.manifestPath, bundleOutRoot,
                targets);
            builtInto.AddRange(builtPaths);

            // Extract has ALREADY logged the extraction summary - counts, the per-key breakdown
            // of both dictionaries, the loaded/released pair - and already escalated it to a
            // warning if anything was lost. Repeating that here would print every number twice
            // and teach an operator to skim the one report they need to read. So this line adds
            // only the half Extract cannot know: that a rebuild happened, where the bundle
            // landed, and - the reason it is worth saying at all - that whatever the extraction
            // skipped is therefore ABSENT FROM THAT BUNDLE. That is the sentence an operator
            // needs when a converted mod is missing content, and it is the one nobody else here
            // is in a position to write.
            //
            // The two dictionaries are reported separately and never summed. skippedByType is a
            // loss (an unsupported type, a collision, a path that escaped the root, a failed
            // write, a clip whose samples GetData cannot reach); notesByType describes assets
            // that DID make it into the bundle and merely changed name or lost their recorded
            // capitalisation on the way. Adding them would inflate the loss with survivors and
            // turn a clean conversion into an alarming one.
            int skipped = Total(report.skippedByType);
            int noted = Total(report.notesByType);
            var line = new System.Text.StringBuilder();
            line.Append("[MobileModExtractor] converted ").Append(Path.GetFileName(dfmodPath))
                .Append(": ").Append(report.extracted.Count).Append(" assets rebuilt into ")
                .Append(string.Join(", ", builtPaths));
            if (skipped > 0)
                line.Append("; ").Append(skipped).Append(" NOT converted and therefore absent ")
                    .Append("from the rebuilt bundle [").Append(Describe(report.skippedByType)).Append(']');
            if (noted > 0)
                line.Append("; ").Append(noted).Append(" converted with a note [")
                    .Append(Describe(report.notesByType)).Append(']');
            if (skipped > 0)
                Debug.LogWarning(line.ToString());
            else
                Debug.Log(line.ToString());
        }

        /// <summary>Command-line entry point; see the CLI block at the top of this file. Mirrors
        /// MobileModBuilder.BuildFromEnv's shape - read the environment, do the work, and turn any
        /// exception into a non-zero exit so a shell loop over a mod folder stops on the failure
        /// instead of reporting success over a stack trace nobody read.
        ///
        /// The extraction root is NOT an environment variable. It is pinned under
        /// Assets/Game/Mods/Converted/, which is gitignored and which MobileConvertedModImporter
        /// scopes its whole import policy to by path prefix: an extraction landing anywhere else
        /// would silently import with Unity's defaults (uncompressed, 2048 cap, normal maps typed
        /// as colour) and could be committed by accident. Both of those are worth more than the
        /// flexibility.</summary>
        // The tick-driven conversion. Fields rather than locals because the work now spans
        // editor frames: ConvertFromEnv arms the driver and returns, and DriverTick carries it.
        static IEnumerator driverSteps;
        static List<string> driverBuilt;
        static DateTime driverStarted;

        /// <summary>The command-line entry point. RUN THIS WITHOUT -quit.
        ///
        /// That is not a style preference, it is the whole reason this is shaped like a driver.
        /// A clip whose audio data loads asynchronously becomes readable only when Unity's main
        /// loop integrates the load, so the conversion has to hand control back between steps
        /// rather than blocking - and -quit ends the process the moment this method returns,
        /// before a single tick can run. On DREAM's sound module that is 34 of 340 clips and 45%
        /// of the module's audio duration.
        ///
        /// So passing -quit is refused outright rather than tolerated. With it, this method
        /// would arm the driver, return, and be killed before any of the work happened - a
        /// conversion that writes nothing and exits 0, which is the worst outcome available.
        ///
        /// Exit codes are the contract for a shell loop over a mods folder: 0 only when a bundle
        /// was actually written, 1 for a failure (including "converted nothing"), 2 when the
        /// watchdog gave up.</summary>
        public static void ConvertFromEnv()
        {
            try
            {
                foreach (string arg in Environment.GetCommandLineArgs())
                {
                    if (!string.Equals(arg, "-quit", StringComparison.OrdinalIgnoreCase))
                        continue;
                    Debug.LogError(
                        "[MobileModExtractor] -quit is not compatible with ConvertFromEnv and " +
                        "would silently convert NOTHING: audio clips that load asynchronously " +
                        "become readable only when Unity's main loop runs, and -quit ends this " +
                        "process before a single frame of it. Re-run the same command without " +
                        "-quit; the converter exits by itself when it is done (0 on success, " +
                        "1 on failure, 2 on timeout). See README-iOS.md.");
                    EditorApplication.Exit(1);
                    return;
                }

                string input = Environment.GetEnvironmentVariable("DFU_MOD_IN");
                if (string.IsNullOrEmpty(input) || !File.Exists(input))
                    throw new InvalidOperationException(
                        "DFU_MOD_IN must point at a desktop .dfmod file (got: " +
                        (string.IsNullOrEmpty(input) ? "unset" : input) + ")");

                string outRoot = Environment.GetEnvironmentVariable("DFU_MOD_OUT");
                if (string.IsNullOrEmpty(outRoot))
                    outRoot = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.Personal), "dev/dfu-mods");

                string targetsVar = Environment.GetEnvironmentVariable("DFU_MOD_TARGETS");
                if (string.IsNullOrEmpty(targetsVar))
                    targetsVar = "iOS";
                BuildTarget[] targets = Array.ConvertAll(targetsVar.Split(','),
                    t => (BuildTarget)Enum.Parse(typeof(BuildTarget), t.Trim(), true));

                string extractRoot = "Assets/Game/Mods/Converted/" +
                    Path.GetFileNameWithoutExtension(input);

                driverBuilt = new List<string>();
                driverSteps = ConvertSteps(input, extractRoot, outRoot, targets, true, driverBuilt);
                driverStarted = DateTime.UtcNow;
                Debug.Log($"[MobileModExtractor] converting {Path.GetFileName(input)} " +
                    $"(per-clip audio cap {AudioLoadTimeoutSeconds():F0}s, run cap " +
                    $"{RunTimeoutSeconds() / 60:F0} min); this process exits by itself.");
                EditorApplication.update += DriverTick;
            }
            catch (Exception ex)
            {
                Debug.LogError("[MobileModExtractor] " + ex);
                EditorApplication.Exit(1);
            }
        }

        /// <summary>One step of the conversion per editor frame - and, between steps, the only
        /// thing standing between a stalled load and a batch-mode Unity that never exits.
        ///
        /// The run cap is checked here rather than inside the work because here is where it can
        /// help: every wait in this converter yields, so a stall always comes back through this
        /// method. It does not interrupt a single long-running step, and it is not meant to -
        /// a texture module that legitimately takes an hour is not a stall.</summary>
        static void DriverTick()
        {
            try
            {
                double elapsed = (DateTime.UtcNow - driverStarted).TotalSeconds;
                if (elapsed > RunTimeoutSeconds())
                {
                    Debug.LogError($"[MobileModExtractor] giving up after {elapsed / 60:F1} " +
                        $"minutes (DFU_MOD_TIMEOUT={RunTimeoutSeconds() / 60:F0} min). The " +
                        "conversion is incomplete and no bundle should be trusted from this run.");
                    FinishDriver(2);
                    return;
                }

                if (driverSteps.MoveNext())
                    return;   // more to do; Unity gets its frame back, which is the point

                foreach (string built in driverBuilt)
                    Debug.Log("[MobileModExtractor] built " + built);
                Debug.Log($"[MobileModExtractor] done in {elapsed:F1}s.");
                FinishDriver(driverBuilt.Count > 0 ? 0 : 1);
            }
            catch (Exception ex)
            {
                Debug.LogError("[MobileModExtractor] " + ex);
                FinishDriver(1);
            }
        }

        static void FinishDriver(int exitCode)
        {
            EditorApplication.update -= DriverTick;
            driverSteps = null;
            EditorApplication.Exit(exitCode);
        }

        static ModInfo ReadManifest(AssetBundle ab, string dfmodPath)
        {
            ModInfo modInfo = null;
            foreach (string assetName in ab.GetAllAssetNames())
            {
                if (!assetName.EndsWith(ModManager.MODINFOEXTENSION, StringComparison.Ordinal))
                    continue;
                var manifestAsset = ab.LoadAsset<TextAsset>(assetName);
                if (manifestAsset != null)
                    ModManager._serializer.TryDeserialize(fsJsonParser.Parse(manifestAsset.text), ref modInfo);
                break;
            }
            if (modInfo == null || string.IsNullOrWhiteSpace(modInfo.ModTitle))
                throw new InvalidDataException("Bundle has no readable .dfmod.json manifest: " + dfmodPath);
            return modInfo;
        }

        /// <summary>Rebuilds the asset's path under outputRoot from the source manifest's own
        /// Files entry, keeping the author's capitalisation and the leading "Assets/".
        /// Both matter: DFU locates loose mod content with Mod.FindAssetNames, which accepts an
        /// asset whose directory *ends with* the requested one and compares with a case-sensitive
        /// CompareOrdinal - so the lowercase "assets/textures" that AssetBundle.GetAllAssetNames
        /// hands back would never match a caller's "Assets/Textures" (TextAssetReader and
        /// WorldDataReplacement both pass literal capitalised paths), and the converted mod would
        /// silently lose loose-file injection. Only when the manifest does not list the asset at
        /// all is the lowercase bundle path used, stripped of its "assets/" prefix.</summary>
        static string OutputPathFor(string bundleAssetName, string outputRoot,
            Dictionary<string, string> originalCase, List<string> notes)
        {
            string tail = bundleAssetName.Replace('\\', '/');
            string original;
            if (originalCase.TryGetValue(tail.ToLowerInvariant(), out original))
                return Path.Combine(outputRoot, original);

            notes.Add("unlisted-in-manifest");
            Debug.LogWarning($"[MobileModExtractor] {bundleAssetName} is not listed in the source " +
                "manifest, so its original capitalisation cannot be recovered; DFU's case-sensitive " +
                "directory lookups may not find it");
            if (tail.StartsWith("assets/", StringComparison.Ordinal))
                tail = tail.Substring("assets/".Length);
            return Path.Combine(outputRoot, tail);
        }

        /// <summary>Reserves an output path for one bundle asset, returning false if another
        /// already holds it - most easily a foo.tga and a foo.png, which collapse onto the same
        /// file once the texture extension is rewritten. Overwriting would lose one asset outright
        /// and list the survivor twice in the rebuilt manifest, so the first writer wins and the
        /// clash is reported rather than resolved silently.</summary>
        static bool Claim(Dictionary<string, string> claimed, string outPath, string assetName,
            ExtractReport report)
        {
            // Key on the RESOLVED path: the manifest is attacker-controlled and "A/x.png",
            // "A/./x.png" and "A/sub/../x.png" are three spellings of one file. Keyed on the raw
            // string they would look like three separate assets, so the later writes would
            // silently overwrite the earlier one and the rebuilt manifest would list the same
            // file more than once - which Unity then refuses to pack at all.
            string key;
            try { key = Path.GetFullPath(outPath); }
            catch (Exception) { key = outPath; }

            string owner;
            if (!claimed.TryGetValue(key, out owner))
            {
                claimed[key] = assetName;
                return true;
            }

            Skipped(report, "collision");
            Debug.LogWarning($"[MobileModExtractor] {assetName} and {owner} both map to {outPath}; " +
                $"keeping {owner} and skipping {assetName} rather than overwriting it");
            return false;
        }

        /// <summary>True when the name carries DFU's normal-map suffix. The name is all there is
        /// to go on: TextureReplacement.GetName writes "_" + the TextureMap enum name onto every
        /// non-albedo map ("004_0-0_Normal.png"), while a texture inside a bundle carries no
        /// record of the importer settings it was built with.</summary>
        public static bool IsNormalMapName(string assetName)
        {
            return HasMapSuffix(assetName, "Normal");
        }

        /// <summary>True when the name says the texture holds numbers rather than colours - the
        /// same three maps DFU itself treats as linear in TextureReplacement.IsLinearTextureMap.
        /// Emission and Mask are deliberately absent: those are colour, and forcing them linear
        /// would regrade them exactly as badly as leaving a normal map in sRGB does.</summary>
        public static bool IsLinearMapName(string assetName)
        {
            return HasMapSuffix(assetName, "Normal")
                || HasMapSuffix(assetName, "Height")
                || HasMapSuffix(assetName, "MetallicGloss");
        }

        static bool HasMapSuffix(string assetName, string map)
        {
            return Path.GetFileNameWithoutExtension(assetName)
                .EndsWith("_" + map, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Picks the per-pixel fixup a normal map needs to become an ordinary RGB
        /// normal map again, or null when it already is one.
        ///
        /// A compressed normal map is not an image of a normal map. Unity drops z at import
        /// time - it is recoverable, so spending bits on it is waste - and stores what is left
        /// in whichever two channels its block format codes best: DXT5nm puts y in green and x
        /// in ALPHA (rgb are left as 1s), BC5 is a two-channel format holding x and y in red
        /// and green with nothing in blue or alpha. Written straight to a .png either one is
        /// garbage: DXT5nm reads as a white image, BC5 as a flat blue-less one, and both
        /// re-import as ordinary colour textures that light nothing correctly. Rebuilding z
        /// with ReconstructNormalPixel restores the standard encoding every shader expects.
        ///
        /// The decision is made on the format because that is where the swizzle actually lives;
        /// an uncompressed or BC7 normal map already holds x,y,z in r,g,b and is passed through
        /// unchanged. An sRGB-flagged source is passed through too, and that check has to come
        /// first: Unity never marks a NormalMap-typed texture sRGB, so an sRGB source named
        /// "*_Normal" is an ordinary colour texture that merely shares the suffix, and
        /// unswizzling its perfectly good rgb would be the corruption rather than the fix.
        /// Every outcome is counted so a real conversion can be checked rather than trusted.</summary>
        static Func<Color32, Color32> NormalUnswizzlerFor(TextureFormat format, bool sourceIsSRGB,
            List<string> notes)
        {
            if (sourceIsSRGB)
            {
                notes.Add("normal-srgb-source-kept-as-is");
                return null;
            }
            switch (format)
            {
                case TextureFormat.DXT5:
                case TextureFormat.DXT5Crunched:
                    notes.Add("normal-unswizzled-dxt5nm");
                    return c => ReconstructNormalPixel(c, true);
                case TextureFormat.BC5:
                    notes.Add("normal-unswizzled-bc5");
                    return c => ReconstructNormalPixel(c, false);
                default:
                    notes.Add("normal-rgb-kept-as-is");
                    return null;
            }
        }

        /// <summary>Rebuilds one standard tangent-space normal pixel from a swizzled one.
        /// x comes from alpha for DXT5nm and from red for BC5; y is always green. Both are
        /// decoded from [0,255] to [-1,1], z is recovered as sqrt(1 - x^2 - y^2) - which is
        /// what makes dropping it lossless-in-principle, since a tangent-space normal is a
        /// unit vector with z >= 0 - and all three are re-encoded as n * 0.5 + 0.5, the
        /// mapping Unity's UnpackNormal undoes. So a collapsed z encodes to 128, not to 0.
        /// The max(0, ...) matters: block compression error alone can push x^2 + y^2 just past
        /// 1, and Mathf.Sqrt of a negative is NaN, which casts to an arbitrary byte.</summary>
        public static Color32 ReconstructNormalPixel(Color32 swizzled, bool xInAlpha)
        {
            float x = ((xInAlpha ? swizzled.a : swizzled.r) / 255f) * 2f - 1f;
            float y = (swizzled.g / 255f) * 2f - 1f;
            float z = Mathf.Sqrt(Mathf.Max(0f, 1f - x * x - y * y));
            return new Color32(
                (byte)Mathf.RoundToInt((x * 0.5f + 0.5f) * 255f),
                (byte)Mathf.RoundToInt((y * 0.5f + 0.5f) * 255f),
                (byte)Mathf.RoundToInt((z * 0.5f + 0.5f) * 255f), 255);
        }

        /// <summary>Wraps raw float samples in a canonical 44-byte RIFF/WAVE header as 16-bit
        /// little-endian PCM - the one audio container that can be written from samples alone,
        /// with no encoder. This is the whole of the audio extraction: an AssetBundle stores an
        /// AudioClip as decoded samples and keeps nothing of the file the author imported, so
        /// there is no original to copy out and a container has to be built around them.
        ///
        /// 16-bit is not a compromise here. It is the width Unity's own importer decodes to for
        /// everything it does not keep as float, it is what the converted mod is re-encoded FROM
        /// (MobileConvertedModImporter puts it straight back into Vorbis), and it halves an
        /// intermediate that is already hundreds of megabytes for a music pack.
        ///
        /// Samples are clamped rather than cast. GetData can hand back values outside [-1,1] -
        /// a hot master, or any DSP that overshot - and the naive cast WRAPS: 1.5 scales to
        /// 49150, which truncates to -16386 and turns a loud peak into an equally loud click of
        /// the opposite sign. A NaN is flattened to silence for the same reason: casting one to
        /// an integer is undefined and yields whatever the platform's conversion happens to do.
        /// </summary>
        public static byte[] EncodeWav(float[] samples, int channels, int frequency)
        {
            if (samples == null)
                samples = new float[0];
            // A clip that reports nonsense must still produce a file a decoder can open.
            channels = Mathf.Max(1, channels);
            frequency = Mathf.Max(1, frequency);

            const short formatPcm = 1;
            const short bitsPerSample = 16;
            short blockAlign = (short)(channels * (bitsPerSample / 8));
            int byteRate = frequency * blockAlign;
            int dataBytes = samples.Length * (bitsPerSample / 8);

            var stream = new MemoryStream(44 + dataBytes);
            // BinaryWriter is little-endian on every runtime .NET defines, which is exactly
            // what RIFF wants; nothing here needs a byte order swap.
            using (var w = new BinaryWriter(stream))
            {
                w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                w.Write(36 + dataBytes);              // everything after this field
                w.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
                w.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
                w.Write(16);                          // PCM fmt chunk size
                w.Write(formatPcm);
                w.Write((short)channels);
                w.Write(frequency);
                w.Write(byteRate);
                w.Write(blockAlign);
                w.Write(bitsPerSample);
                w.Write(System.Text.Encoding.ASCII.GetBytes("data"));
                w.Write(dataBytes);
                foreach (float sample in samples)
                {
                    float s = float.IsNaN(sample) ? 0f : Mathf.Clamp(sample, -1f, 1f);
                    w.Write((short)Mathf.RoundToInt(s * short.MaxValue));
                }
                w.Flush();
                // GetBuffer, not ToArray: the stream was created with exactly this capacity and
                // filled exactly full, so its internal array already IS the answer and ToArray
                // would copy every byte of it a second time - ~115MB of pure copy for one long
                // song. The equality check is what makes that safe rather than assumed; if the
                // two ever diverge the copy is still there to fall back on.
                byte[] buffer = stream.GetBuffer();
                return buffer.Length == stream.Length ? buffer : stream.ToArray();
            }
        }

        public const string AudioTimeoutVar = "DFU_MOD_AUDIO_TIMEOUT";
        public const string RunTimeoutVar = "DFU_MOD_TIMEOUT";

        /// <summary>Wall clock allowed for ONE clip's asynchronous audio load. Measured, not
        /// picked: a 790,320-sample clip out of DREAM's sound module completes in two editor
        /// ticks and 0.14s once the main loop is being handed back. Ten seconds is seventy times
        /// that, which is slack enough for a far larger clip on a far slower disk and still tight
        /// enough that a module full of genuinely stuck clips fails in minutes rather than
        /// sitting there. It matters more than it used to: without -quit there is nothing else
        /// to end the process.</summary>
        public const double DefaultAudioLoadTimeoutSeconds = 10;

        /// <summary>Wall clock allowed for a whole conversion before it is abandoned. This is a
        /// backstop, not a schedule - the per-clip cap above is what actually catches a stall,
        /// because a stall is a wait and every wait yields through here. Four hours is set
        /// against the biggest thing anyone means to convert (a 1.72GB texture module) with room
        /// to spare, on the principle that killing a legitimate long conversion would be a worse
        /// failure than a slow one. Raise it with DFU_MOD_TIMEOUT for something larger.</summary>
        public const double DefaultRunTimeoutSeconds = 4 * 60 * 60;

        public static double AudioLoadTimeoutSeconds()
        {
            return ParseSeconds(Environment.GetEnvironmentVariable(AudioTimeoutVar),
                DefaultAudioLoadTimeoutSeconds, AudioTimeoutVar);
        }

        public static double RunTimeoutSeconds()
        {
            return ParseSeconds(Environment.GetEnvironmentVariable(RunTimeoutVar),
                DefaultRunTimeoutSeconds, RunTimeoutVar);
        }

        /// <summary>A positive number of seconds, or the default. A typo must not silently become
        /// "no timeout", which is the one value a watchdog must never take.</summary>
        public static double ParseSeconds(string raw, double fallback, string name)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return fallback;
            double value;
            if (double.TryParse(raw.Trim(), System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out value)
                && value > 0 && !double.IsInfinity(value))
                return value;
            Debug.LogWarning($"[MobileModExtractor] {name}='{raw}' is not a positive number of " +
                $"seconds; keeping {fallback}");
            return fallback;
        }

        /// <summary>True when writing this path into the project would hand a stranger's mod to
        /// a COMPILER or a LOADER rather than to an importer.
        ///
        /// The extraction root is inside Assets/, so anything written there is picked up by
        /// Unity immediately: a .cs is compiled into the editor's own assemblies mid-run, a
        /// .dll/.dylib/.so/.a is loaded as a plugin, an .asmdef or .asmref restructures
        /// compilation, and an .rsp changes the compiler flags for the whole project. A .dfmod
        /// is a file a stranger hands us, so none of those may ever be written, and the check
        /// belongs here rather than in MobileModBuilder's script guard - by the time the builder
        /// throws, the file is already on disk and already compiled.
        ///
        /// Note what is NOT on this list: ".cs.txt" and ".dll.bytes", the spellings DFU mods
        /// actually use, whose extensions are .txt and .bytes. Those are inert TextAssets and
        /// extract normally; MobileModBuilder still refuses to REBUILD a mod that carries them.
        /// </summary>
        public static bool IsProjectCodeFile(string path)
        {
            switch (Path.GetExtension(path ?? string.Empty).ToLowerInvariant())
            {
                case ".cs":                                   // compiled into the editor, live
                case ".dll": case ".dylib": case ".so": case ".a":   // loaded as a plugin
                case ".asmdef": case ".asmref":               // restructures compilation
                case ".rsp":                                  // rewrites compiler flags
                case ".jslib": case ".jspre":                 // linked into a WebGL build
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>What EnsureAudioData found out. An iterator cannot have an out parameter,
        /// so the answer comes back in this instead.</summary>
        class AudioLoad
        {
            public bool ok;
            public bool unreachable;   // asynchronous, and nobody is pumping the main loop
            public string detail;
        }

        /// <summary>Makes a clip's samples actually resident, and says what it had to do.
        ///
        /// AudioClipLoadType.DecompressOnLoad describes how a clip is decoded, NOT whether it is
        /// decoded YET - different questions, and conflating them cost 34 of DREAM's 340 sound
        /// effects on the first real conversion. Every one reported "GetData failed on a
        /// DecompressOnLoad clip"; every one had preloadAudioData=False, so its samples were
        /// simply not there. DFU's own runtime has always done this dance before using a mod
        /// clip (SoundReplacement: "if (audioClip.preloadAudioData || audioClip.LoadAudioData())").
        ///
        /// A Load In Background clip loads ASYNCHRONOUSLY, and an asynchronous load is integrated
        /// by Unity's main loop. That is why this yields rather than sleeps: measured against the
        /// real module, 30s of Thread.Sleep, QueuePlayerLoopUpdate, DisplayProgressBar,
        /// AssetDatabase.Refresh, a synchronous LoadAsset and UnloadUnusedAssets ALL leave the
        /// clip Loading, while simply returning to EditorApplication.update completes the same
        /// load in two ticks and 0.14s. Yielding is the only thing that works.
        ///
        /// When nobody is pumping (canYield false) the wait is skipped entirely rather than
        /// spun: it could not succeed, and the 30s-per-clip version of that turned one module
        /// into a 17-minute stall.
        ///
        /// Loading on demand does not undo the memory work: Release runs from the loop's finally
        /// on every path including these, so whatever this makes resident is handed straight
        /// back. Peak stays one clip.</summary>
        static IEnumerator EnsureAudioData(AudioClip clip, List<string> notes, bool canYield,
            AudioLoad outcome)
        {
            outcome.ok = false;
            outcome.unreachable = false;
            outcome.detail = null;

            if (clip.loadState == AudioDataLoadState.Loaded)
            {
                outcome.ok = true;
                yield break;
            }

            string state = $"preloadAudioData={clip.preloadAudioData}, " +
                           $"loadInBackground={clip.loadInBackground}, loadState={clip.loadState}";

            if (clip.loadInBackground && !canYield)
            {
                outcome.unreachable = true;
                outcome.detail = $"samples are not resident and load asynchronously ({state}), " +
                                 "and this run is not driven across editor ticks";
                yield break;
            }

            if (!clip.LoadAudioData())
            {
                outcome.detail = $"samples are not resident ({state}) and LoadAudioData() refused";
                yield break;
            }
            notes.Add("audio-loaded-on-demand");

            if (clip.loadState == AudioDataLoadState.Loading)
            {
                notes.Add("audio-load-awaited");
                DateTime started = DateTime.UtcNow;
                while (clip.loadState == AudioDataLoadState.Loading)
                {
                    double waited = (DateTime.UtcNow - started).TotalSeconds;
                    if (waited > AudioLoadTimeoutSeconds())
                    {
                        outcome.unreachable = true;
                        outcome.detail = $"samples are not resident ({state}) and were still " +
                                         $"loading after {waited:F1}s";
                        yield break;
                    }
                    yield return null;   // the ONLY thing that lets Unity finish the load
                }
            }

            if (clip.loadState != AudioDataLoadState.Loaded)
            {
                outcome.detail = $"samples are not resident ({state}) and the load ended in " +
                                 $"{clip.loadState}";
                yield break;
            }

            outcome.ok = true;
            outcome.detail = $"samples were not resident ({state}); loaded on demand";
        }

        /// <summary>Hands one bundle asset's native memory back the moment this tool is done
        /// with it, instead of letting the whole mod accumulate until AssetBundle.Unload at the
        /// end of the run.
        ///
        /// This is not a tidiness measure. The objects a bundle hands out are NOT the compressed
        /// bytes on disk: a DecompressOnLoad AudioClip is decoded to PCM in native memory at load
        /// time, so a music module that is 273MB of Vorbis on disk is some multiple of that
        /// resident once every clip in it has been loaded. Holding all of them at once - which is
        /// what the loop did while its only unload was the one after the loop - is the single
        /// most likely reason a first conversion of a real module dies rather than finishing.
        ///
        /// UnloadAudioData is the call that carries this, and that is a MEASURED statement, not
        /// the obvious ordering. Resources.UnloadAsset looks like it should subsume the audio
        /// case - it is the documented counterpart of AssetBundle.LoadAsset and destroys the
        /// native object - but it is documented to do nothing for an asset that came from the
        /// editor's AssetDatabase, and the self-test established that it does nothing for an
        /// editor-side BUNDLE asset either: the check was first written asserting the object
        /// would be destroyed, and it failed with the clip still alive. UnloadAudioData does
        /// bite, which is fortunate, because decoded PCM is the term that dominates on a music
        /// module. The test now pins the effect (samples gone) rather than the mechanism.
        ///
        /// Resources.UnloadAsset is still called, for everything that is not a GameObject or a
        /// Component (the two types for which it is undefined, hence the guard). It costs
        /// nothing, it is correct where it works, and it is the right call to have in place if
        /// this ever runs outside an editor process. But it does NOT free an editor-side bundle
        /// TEXTURE, and that is measured rather than suspected: the self-test loads a Texture2D
        /// straight from a bundle - no preparatory unload of any kind, so nothing confounds it -
        /// releases it, and finds the object still alive. There is no per-texture equivalent of
        /// UnloadAudioData, so THE TEXTURE HALF OF A LARGE MODULE STILL ACCUMULATES until the
        /// Unload(true) after the loop. A 1.72GB texture pack should be scheduled on that
        /// understanding. The audio half, which was the acute one, is genuinely fixed.
        ///
        /// Neither call is a correctness problem here because nothing holds a reference past
        /// this point: the report keeps output PATHS, the notes keep strings, and the bytes have
        /// already been written.
        ///
        /// There is deliberately no periodic Resources.UnloadUnusedAssets sweep. That call walks
        /// the whole loaded object graph, so its cost grows with everything the editor has open
        /// rather than with this mod, and picking an interval for it would mean picking a number
        /// with no measurement behind it. Releasing each asset by name at the point it stops
        /// being needed leaves nothing for a sweep to find.
        /// </summary>
        public static void Release(UnityEngine.Object obj, ExtractReport report = null)
        {
            if (obj == null)
                return;

            var clip = obj as AudioClip;
            if (clip != null)
                clip.UnloadAudioData();

            if (!(obj is GameObject) && !(obj is Component))
                Resources.UnloadAsset(obj);

            if (report != null)
                report.released++;
        }

        /// <summary>The one line worth reading when a real conversion has just produced a
        /// thousand warnings. An entirely unconvertible module - every clip streamed, say -
        /// otherwise looks like a wall of individually reasonable warnings and an empty output
        /// directory, which is obvious only to someone who reads the whole log. This states the
        /// verdict, and raises its own voice to a warning when anything was lost, so it survives
        /// a log filtered to warnings and errors.</summary>
        static void LogSummary(ExtractReport report, string dfmodPath, string outputRoot)
        {
            int skipped = Total(report.skippedByType);
            string line = $"[MobileModExtractor] {Path.GetFileName(dfmodPath)}: extracted " +
                $"{report.extracted.Count}, skipped {skipped} [{Describe(report.skippedByType)}], " +
                $"noted {Total(report.notesByType)} [{Describe(report.notesByType)}], " +
                $"released {report.released}/{report.loaded} loaded -> {outputRoot}";
            if (skipped > 0)
                Debug.LogWarning(line);
            else
                Debug.Log(line);
        }

        static int Total(Dictionary<string, int> counts)
        {
            int total = 0;
            foreach (var kv in counts)
                total += kv.Value;
            return total;
        }

        /// <summary>Renders a counter dictionary as "key=n, key=n", sorted so two runs of the
        /// same conversion produce comparable lines.</summary>
        static string Describe(Dictionary<string, int> counts)
        {
            if (counts.Count == 0)
                return "none";
            var parts = new List<string>();
            foreach (var kv in counts)
                parts.Add(kv.Key + "=" + kv.Value);
            parts.Sort(StringComparer.Ordinal);
            return string.Join(", ", parts.ToArray());
        }

        /// <summary>The asset did not make it into the extraction.</summary>
        static void Skipped(ExtractReport report, string key) { Bump(report.skippedByType, key); }

        /// <summary>Records the notes gathered for one asset - and is called only once that
        /// asset's bytes are on disk. Notes are the half of the report that describes SURVIVORS,
        /// so a note banked before the write would let a texture that TryWriteFile then refused
        /// be counted as "extracted, under a new name" in the same run that counts it as a
        /// write-failure: two contradictory claims about one asset, in one report.</summary>
        static void CommitNotes(ExtractReport report, List<string> notes)
        {
            foreach (string note in notes)
                Bump(report.notesByType, note);
        }

        static void Bump(Dictionary<string, int> counts, string key)
        {
            counts.TryGetValue(key, out int n);
            counts[key] = n + 1;
        }

        /// <summary>The single choke point for every byte this tool writes - no other code here
        /// touches the filesystem - so containment enforced here cannot be bypassed by a later
        /// code path. A rejected entry is skipped and counted rather than fatal: one hostile or
        /// malformed path in a large mod must not cost the operator the other 99% of it.</summary>
        static bool TryWriteFile(string path, byte[] bytes, string outputRoot, string sourceName,
            ExtractReport report)
        {
            if (!IsInsideRoot(path, outputRoot))
            {
                Skipped(report, "path-escape");
                Debug.LogWarning($"[MobileModExtractor] refusing to write {sourceName}: '{path}' " +
                    $"resolves outside the extraction root '{outputRoot}'. A .dfmod is untrusted " +
                    "input, so this entry is skipped; the rest of the mod still converts.");
                return false;
            }

            // Containment is not the only way a legal-looking path fails to be writable. A mod
            // listing both "a" (a TextAsset) and "a/b.png" is entirely contained and entirely
            // legal, but one of the two must lose: CreateDirectory raises IOException when "a" is
            // already a file, WriteAllBytes raises UnauthorizedAccessException when "a" is already
            // a directory. An over-long path component fails the same way, after passing
            // GetFullPath. None of that may cost the operator the rest of a large mod.
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllBytes(path, bytes);
            }
            catch (Exception ex)
            {
                Skipped(report, "write-failed");
                Debug.LogWarning($"[MobileModExtractor] could not write {sourceName} to '{path}': " +
                    $"{ex.GetType().Name}: {ex.Message}. Skipping it; the rest of the mod still converts.");
                return false;
            }
            return true;
        }

        /// <summary>True when candidate resolves to a location strictly inside root.
        ///
        /// The manifest and the bundle's own asset names are attacker-controlled, so a Files entry
        /// of "../../../.ssh/authorized_keys" - or an absolute path, which Path.Combine would let
        /// win outright and discard the root - would otherwise be an arbitrary file write.
        ///
        /// Both sides are resolved with GetFullPath first, so this is normalisation rather than
        /// naive string matching: "sub/../ok.png" stays inside and is accepted, while ".." that
        /// genuinely climbs out is not. The root is compared with a trailing separator so a
        /// sibling that merely shares a name prefix ("&lt;root&gt;-evil") cannot pass. The
        /// comparison is ordinal but case-insensitive, because APFS and HFS+ are case-insensitive
        /// by default and a case-flipped prefix would otherwise slip past on macOS.</summary>
        public static bool IsInsideRoot(string candidate, string root)
        {
            if (string.IsNullOrEmpty(candidate) || string.IsNullOrEmpty(root))
                return false;

            string fullCandidate, fullRoot;
            try
            {
                fullCandidate = Path.GetFullPath(candidate);
                fullRoot = Path.GetFullPath(root);
            }
            catch (Exception)
            {
                return false;   // malformed, too long, or illegal characters: never write there
            }

            if (fullRoot[fullRoot.Length - 1] != Path.DirectorySeparatorChar)
                fullRoot += Path.DirectorySeparatorChar;

            // Strictly inside: the root directory itself is not a valid destination for a file.
            return fullCandidate.Length > fullRoot.Length
                && fullCandidate.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>PNG encoding for bundle textures. Compressed/non-readable textures cannot
    /// EncodeToPNG directly; a GPU blit to an ARGB32 RenderTexture decodes any GPU-readable
    /// format (DXT included), then ReadPixels brings it back to the CPU.</summary>
    static class TexturePng
    {
        /// <param name="linear">The texture holds numbers, not colours (normal, height,
        /// metallic/gloss). The project renders in linear space, so an sRGB round trip would
        /// silently regrade every byte of a data map.</param>
        /// <param name="perPixel">Optional fixup applied to the decoded RGBA32 pixels before
        /// they are encoded - how a swizzled normal map gets its blue channel back.</param>
        public static byte[] Encode(Texture2D src, bool linear, Func<Color32, Color32> perPixel = null)
        {
            if (src.isReadable)
            {
                try
                {
                    if (perPixel == null)
                        return src.EncodeToPNG();
                    return EncodePixels(Transform(src.GetPixels32(), perPixel),
                                        src.width, src.height, linear);
                }
                catch (Exception) { /* fall through to GPU path */ }
            }
            // Graphics.Blit against the null device is a silent no-op: ReadPixels then returns
            // a uniform grey and the extraction looks entirely successful - right name, right
            // path, right size, no pixels. Corrupting a mod quietly is worse than not
            // converting it, so refuse instead of guessing.
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                throw new InvalidOperationException(
                    "Cannot decode texture '" + src.name + "' (format " + src.format +
                    ", not readable): decoding a compressed or non-readable bundle texture needs a " +
                    "real graphics device, and this Unity process has none. Re-run the extraction " +
                    "WITHOUT the -nographics flag ('-batchmode -quit' on its own is fine).");

            var rt = RenderTexture.GetTemporary(src.width, src.height, 0,
                RenderTextureFormat.ARGB32,
                linear ? RenderTextureReadWrite.Linear : RenderTextureReadWrite.sRGB);
            var prev = RenderTexture.active;
            try
            {
                Graphics.Blit(src, rt);
                RenderTexture.active = rt;
                var tmp = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false, linear);
                tmp.ReadPixels(new Rect(0, 0, src.width, src.height), 0, 0);
                if (perPixel != null)
                    tmp.SetPixels32(Transform(tmp.GetPixels32(), perPixel));
                tmp.Apply();
                byte[] png = tmp.EncodeToPNG();
                UnityEngine.Object.DestroyImmediate(tmp);
                return png;
            }
            finally
            {
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);
            }
        }

        static Color32[] Transform(Color32[] pixels, Func<Color32, Color32> perPixel)
        {
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = perPixel(pixels[i]);
            return pixels;
        }

        static byte[] EncodePixels(Color32[] pixels, int width, int height, bool linear)
        {
            var tmp = new Texture2D(width, height, TextureFormat.RGBA32, false, linear);
            try
            {
                tmp.SetPixels32(pixels);
                tmp.Apply();
                return tmp.EncodeToPNG();
            }
            finally { UnityEngine.Object.DestroyImmediate(tmp); }
        }
    }

    /// <summary>The tunable half of the converted-mod import policy: the three settings that
    /// actually decide whether a multi-gigabyte pack fits in an iPad's memory, plus the rules
    /// that are content-dependent rather than universal.
    ///
    /// Every lever reads an environment variable, in the shape MobileModBuilder already uses for
    /// DFU_MOD_OUT / DFU_MOD_TARGETS, because these will be tuned AGAINST A REAL DEVICE and a
    /// loop that needs a recompile per attempt is the wrong loop. Values are read at import time,
    /// so a converted mod picks up a change on its next conversion (extraction rewrites the files
    /// and re-imports them); to re-apply a change to an already-converted mod, reconvert it or
    /// delete its folder under the extraction root, since Unity's import cache has no idea an
    /// environment variable moved.
    ///
    /// Nothing here is guessed silently. Where the right value depends on content this exposes
    /// the rule instead of inventing one, and the defaults are argued for in the members below.
    /// </summary>
    internal static class MobileConvertedModPolicy
    {
        public const string Root = "Assets/Game/Mods/Converted/";

        /// <summary>Unity's platform name for iOS texture overrides (BuildTargetGroup.iOS).</summary>
        public const string IosPlatform = "iPhone";

        public const string MaxSizeVar = "DFU_MOD_MAXTEXSIZE";
        public const string MipsVar = "DFU_MOD_MIPS";
        public const string StreamMipsVar = "DFU_MOD_STREAM_MIPS";
        public const string NoMipVar = "DFU_MOD_NOMIP";
        public const string AstcVar = "DFU_MOD_ASTC";
        public const string QualityVar = "DFU_MOD_TEX_QUALITY";

        /// <summary>1024, not Unity's 2048. This is the single biggest lever there is - it is
        /// quadratic - and 2048 means "never downscale anything", which is not a memory policy.
        /// A 2048 texture is ~1.87MB as ASTC 6x6 and ~0.47MB at 1024; against 1.72GB of DREAM
        /// textures plus ~3.7GB of sprite modules on a device that jetsams an app somewhere
        /// under 4GB, a 4x cut on the largest thing in the build is the difference the task
        /// exists to make. It is also not a wild quality claim: an iPad's panel is ~2360 across,
        /// so a wall filling a quarter of it is being sampled at roughly 600 pixels. Raise it
        /// with DFU_MOD_MAXTEXSIZE=2048 when a specific pack proves it needs it.</summary>
        public const int DefaultMaxTextureSize = 1024;

        /// <summary>ASTC 6x6 = 3.56 bits/pixel, and is Unity's own iOS default for Compressed;
        /// naming it explicitly is what makes it tunable rather than a platform accident. 8x8
        /// (2.0 bpp) is the next lever if 1024 is not enough; 4x4 (8.0 bpp) is where to go if
        /// normal maps band, since they share this setting.</summary>
        public const string DefaultAstcBlock = "6x6";

        /// <summary>Unity's "Normal" compressor quality. Quality here costs import time, not
        /// bytes - the block size fixes the size - so it is safe to raise for a final pack.</summary>
        public const int DefaultCompressionQuality = 50;

        /// <summary>Substrings of an asset path that mean "this is 2D art drawn at 1:1, mipmaps
        /// are 33% resident for nothing". Derived from DFU's own conventions rather than
        /// invented: TextureReplacement keeps IMG images under Textures/Img and CIF/RCI images
        /// (paperdolls, portraits, weapon animations, UI) under Textures/CifRci, and - which is
        /// what makes this work for a bundled mod, whose internal directory layout is the
        /// author's own - a mod can only serve those images at all under a short name carrying
        /// the original .IMG/.CIF/.RCI filename, because that name is the runtime lookup key
        /// (TryImportImage / TryImportCifRci -> ModManager.TryGetAsset).
        ///
        /// World textures and billboards are NOT in this list on purpose: those are minified
        /// constantly and the sampling cost of having no mipmaps outweighs their third.
        ///
        /// This is the one rule whose correctness depends on content the converter has not been
        /// pointed at yet, so it is overridable wholesale with DFU_MOD_NOMIP.</summary>
        public static readonly string[] DefaultNoMipMarkers =
            { ".img", ".cif", ".rci", "/textures/img/", "/textures/cifrci/" };

        public static int MaxTextureSize()
        {
            return ParseSize(Env(MaxSizeVar), DefaultMaxTextureSize);
        }

        public static bool MipmapsAllowed()
        {
            return ParseBool(Env(MipsVar), true);
        }

        /// <summary>Off by default, and not because it looked risky. Mipmap streaming is a
        /// QUALITY SETTING first: every level in this project's ProjectSettings/QualitySettings
        /// .asset carries streamingMipmapsActive: 0, so the importer flag would be inert today -
        /// it would cost a re-import and buy nothing. Turning the quality setting on is outside
        /// this converter, and beyond that the streaming system picks mip levels from renderer
        /// bounds, which is a question about how DFU assigns these textures to materials at
        /// runtime that this task has not answered. Unverified, therefore off, therefore
        /// exposed: DFU_MOD_STREAM_MIPS=1 once someone has checked both halves on a device.
        /// </summary>
        public static bool StreamingMipmaps()
        {
            return ParseBool(Env(StreamMipsVar), false);
        }

        public static int CompressionQuality()
        {
            return ParseQuality(Env(QualityVar), DefaultCompressionQuality);
        }

        public static TextureImporterFormat IosFormat()
        {
            return ParseAstcBlock(Env(AstcVar), DefaultAstcBlock);
        }

        public static string[] NoMipMarkers()
        {
            return ParseList(Env(NoMipVar), DefaultNoMipMarkers);
        }

        /// <summary>Vorbis quality for converted audio. 0.7 rather than Unity's 1.0: the top of
        /// the scale spends bandwidth on detail a phone speaker or a pair of earbuds cannot
        /// resolve, and this is applied to every clip in a 74MB sound pack and a 273MB music
        /// one at once.</summary>
        public const float VorbisQuality = 0.7f;

        /// <summary>Above this, a clip is treated as a song and streamed; below it, as a sound
        /// effect and kept compressed in memory. The split matters in both directions - a
        /// resident song is megabytes the device does not get back, and a streamed sound effect
        /// misses the frame it was triggered on - so neither setting is safe as a blanket rule.
        ///
        /// It is measured on the EXTRACTED file, which this converter always writes as
        /// uncompressed 16-bit PCM, so it is really a duration threshold wearing a size: 2MB is
        /// about 12 seconds of mono 22kHz or 6 seconds of stereo 44.1kHz. That is comfortably
        /// above any sound effect and far below any song, which is exactly where a threshold
        /// that has to separate the two wants to sit.</summary>
        public const long StreamingThresholdBytes = 2 * 1024 * 1024;

        /// <summary>The song/effect split, as a decision rather than an expression buried in the
        /// postprocessor - the streaming half is unreachable from a small test fixture, and an
        /// untested branch on the memory-critical path is the thing this suite exists to
        /// prevent.</summary>
        public static AudioClipLoadType LoadTypeForSize(long fileBytes)
        {
            return fileBytes > StreamingThresholdBytes
                ? AudioClipLoadType.Streaming            // songs
                : AudioClipLoadType.CompressedInMemory;  // sound effects
        }

        static string Env(string name) { return Environment.GetEnvironmentVariable(name); }

        /// <summary>True when this asset is minified in use and should carry mipmaps.</summary>
        public static bool ShouldMipmap(string assetPath, string[] noMipMarkers)
        {
            if (string.IsNullOrEmpty(assetPath) || noMipMarkers == null)
                return true;
            string path = assetPath.Replace('\\', '/');
            foreach (string marker in noMipMarkers)
            {
                if (string.IsNullOrEmpty(marker))
                    continue;
                if (path.IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0)
                    return false;
            }
            return true;
        }

        /// <summary>A texture size Unity will accept: powers of two from 32 to 16384. An
        /// operator typo must not quietly become a policy, so anything else keeps the default
        /// and says so.</summary>
        public static int ParseSize(string raw, int fallback)
        {
            if (string.IsNullOrEmpty(raw))
                return fallback;
            int value;
            if (int.TryParse(raw.Trim(), out value) && value >= 32 && value <= 16384
                && (value & (value - 1)) == 0)
                return value;
            Debug.LogWarning($"[MobileConvertedModPolicy] {MaxSizeVar}='{raw}' is not a power of " +
                $"two between 32 and 16384; keeping {fallback}");
            return fallback;
        }

        public static bool ParseBool(string raw, bool fallback)
        {
            if (string.IsNullOrEmpty(raw))
                return fallback;
            switch (raw.Trim().ToLowerInvariant())
            {
                case "1": case "true": case "yes": case "on": return true;
                case "0": case "false": case "no": case "off": return false;
                default:
                    Debug.LogWarning($"[MobileConvertedModPolicy] '{raw}' is not a yes/no value; " +
                        $"keeping {fallback}");
                    return fallback;
            }
        }

        public static int ParseQuality(string raw, int fallback)
        {
            if (string.IsNullOrEmpty(raw))
                return fallback;
            int value;
            if (int.TryParse(raw.Trim(), out value) && value >= 0 && value <= 100)
                return value;
            Debug.LogWarning($"[MobileConvertedModPolicy] {QualityVar}='{raw}' is not 0-100; " +
                $"keeping {fallback}");
            return fallback;
        }

        /// <summary>Maps an ASTC block spelling ("6x6") to the importer format. Only the block
        /// sizes Unity actually defines are accepted; a typo falls back rather than silently
        /// leaving the platform to choose, which is the state this lever exists to end.</summary>
        public static TextureImporterFormat ParseAstcBlock(string raw, string fallback)
        {
            string block = string.IsNullOrEmpty(raw) ? fallback : raw.Trim().ToLowerInvariant();
            switch (block)
            {
                case "4x4": return TextureImporterFormat.ASTC_4x4;
                case "5x5": return TextureImporterFormat.ASTC_5x5;
                case "6x6": return TextureImporterFormat.ASTC_6x6;
                case "8x8": return TextureImporterFormat.ASTC_8x8;
                case "10x10": return TextureImporterFormat.ASTC_10x10;
                case "12x12": return TextureImporterFormat.ASTC_12x12;
            }
            Debug.LogWarning($"[MobileConvertedModPolicy] {AstcVar}='{raw}' is not one of " +
                $"4x4/5x5/6x6/8x8/10x10/12x12; keeping {fallback}");
            return ParseAstcBlock(fallback, fallback == DefaultAstcBlock ? "6x6" : DefaultAstcBlock);
        }

        public static string[] ParseList(string raw, string[] fallback)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return fallback;
            var parts = new List<string>();
            foreach (string part in raw.Split(','))
            {
                string trimmed = part.Trim();
                if (trimmed.Length > 0)
                    parts.Add(trimmed);
            }
            return parts.Count > 0 ? parts.ToArray() : fallback;
        }
    }

    /// <summary>Import policy for CONVERTED mods, and the reason a 1.7GB texture pack can be
    /// opened on an 8GB iPad at all. These are the opposite trade-offs from the pilot content's:
    /// there the assets are small and wanted on the CPU, here every default that keeps a second
    /// copy of a texture around is a copy the device cannot afford.
    ///
    /// isReadable false drops the CPU-side mirror of every texture, which is pure waste for
    /// content only the GPU ever samples and would otherwise double the cost. npotScale None
    /// keeps exact dimensions, since DFU's XML/uv metadata is written against the authored size
    /// and rescaling silently misaligns it. Normal maps are typed from the DFU *_Normal suffix so
    /// Unity compresses them as normal maps and shaders unpack them correctly; the other two
    /// linear maps are marked non-sRGB, matching DFU's own IsLinearTextureMap - note that this
    /// deliberately NORMALISES a map whose author left the sRGB default on, which is a change of
    /// behaviour relative to their desktop bundle and the reason the extraction side refuses to
    /// make that decision from the filename. Audio: songs stream, effects sit compressed in
    /// memory - a streamed sound effect would stutter, an in-memory song is megabytes resident.
    ///
    /// The three levers that actually move the memory number - size cap, mipmaps, ASTC block -
    /// live in MobileConvertedModPolicy, where they can be changed without a recompile.
    ///
    /// Scoped to the extraction root, so nothing else in the project is touched.</summary>
    class MobileConvertedModImporter : AssetPostprocessor
    {
        // These settings are applied at import time and nowhere else, so a policy change only
        // reaches assets already on disk if this number moves. Note that it invalidates the
        // import cache for every texture and audio clip in the PROJECT, not only the converted
        // ones - the scope check below runs per import, long after the version is compared - so
        // bumping it costs a full re-import. Change it when the policy changes, not otherwise.
        // An environment lever moving does NOT move this: reconvert the mod instead.
        public override uint GetVersion() { return 2; }

        static bool InScope(string assetPath)
        {
            return assetPath.Replace('\\', '/')
                .StartsWith(MobileConvertedModPolicy.Root, StringComparison.Ordinal);
        }

        void OnPreprocessTexture()
        {
            if (!InScope(assetPath)) return;
            var importer = (TextureImporter)assetImporter;
            importer.npotScale = TextureImporterNPOTScale.None;   // exact sizes: DFU XML/uv metadata depends on them
            importer.isReadable = false;                          // no CPU copy - memory matters here
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.maxTextureSize = MobileConvertedModPolicy.MaxTextureSize();
            importer.mipmapEnabled = MobileConvertedModPolicy.MipmapsAllowed()
                && MobileConvertedModPolicy.ShouldMipmap(assetPath,
                    MobileConvertedModPolicy.NoMipMarkers());
            importer.streamingMipmaps = MobileConvertedModPolicy.StreamingMipmaps();

            // Same naming rule the extraction itself used, so what was written linear is read
            // back linear. NormalMap implies linear, hence the else.
            if (MobileModExtractor.IsNormalMapName(assetPath))
                importer.textureType = TextureImporterType.NormalMap;
            else if (MobileModExtractor.IsLinearMapName(assetPath))
                importer.sRGBTexture = false;                     // data, not colour

            // The default platform settings above are what the editor and a desktop build use;
            // the device is the point, and on iOS "Compressed" without an explicit format lets
            // the platform pick the block size - the single number that decides how many bytes
            // per pixel a 3.7GB pack costs. Name it.
            var ios = importer.GetPlatformTextureSettings(MobileConvertedModPolicy.IosPlatform);
            ios.overridden = true;
            ios.maxTextureSize = MobileConvertedModPolicy.MaxTextureSize();
            ios.textureCompression = TextureImporterCompression.Compressed;
            ios.format = MobileConvertedModPolicy.IosFormat();
            ios.compressionQuality = MobileConvertedModPolicy.CompressionQuality();
            importer.SetPlatformTextureSettings(ios);
        }

        void OnPreprocessAudio()
        {
            if (!InScope(assetPath)) return;
            var importer = (AudioImporter)assetImporter;
            var settings = importer.defaultSampleSettings;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = MobileConvertedModPolicy.VorbisQuality;
            settings.loadType = MobileConvertedModPolicy.LoadTypeForSize(
                new FileInfo(assetPath).Length);
            importer.defaultSampleSettings = settings;
        }
    }
}
