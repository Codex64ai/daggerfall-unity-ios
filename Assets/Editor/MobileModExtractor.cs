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
//   DFU_MOD_SWEEP_MB      released bytes before a memory sweep     default 256 (0 = off)
//   DFU_MOD_CHUNK_COUNT   convert the module in this many slices   default 1
//   DFU_MOD_CHUNK_INDEX   which slice this run converts, 1-based   default 1
//   DFU_MOD_MIN_FREE_GB   refuse to start below this much disk     default 4
//   DFU_MOD_KEEP_EXTRACTION  keep the loose files after a build    default off
//
// SLICING, for the modules that do not fit. The three biggest DREAM modules have never
// converted, and RAM was never the problem: Unity's import cache fills the DISK - 25GB while
// converting an 800MB module. That cache can only be cleared with Unity stopped, so a slice is
// a whole PROCESS: run one invocation per slice, clear the cache in between, and peak disk
// follows the slice instead of the module. Each slice is an independent, valid mod
// ("dream - mobs (2 of 4).dfmod"), with its own title and its own derived GUID, and DFU loads
// them all. See README-iOS.md for the loop to run.
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
        /// <summary>Bytes of asset memory released since the last sweep (it resets), the total
        /// across the whole conversion (it does not), and how many sweeps happened.
        ///
        /// The TOTAL is the number worth reading. It says how much decoded asset memory a module
        /// asks the machine to hold if none of it is reclaimed, which is the one quantity that
        /// predicts whether a very large pack converts at all - and it is measured per module
        /// rather than guessed from file size, because the ratio between the two is not
        /// constant.</summary>
        public long releasedBytes;
        public long releasedBytesTotal;
        public int sweeps;
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
            return ExtractSteps(dfmodPath, outputRoot, report, canYield, 0, 1);
        }

        /// <summary>The extraction, optionally as ONE SLICE of a larger module.
        ///
        /// Three modules in the DREAM pack have never converted, and not for want of RAM: the
        /// import cache fills the disk. Library/Artifacts reached 25GB converting an 800MB
        /// module on a machine with 22GB free, and even a 246MB module once died inside a Unity
        /// write with "Failed to write compressed chunk ... Error: 14". The cache can only be
        /// cleared with Unity stopped, so the unit of work has to be a whole PROCESS: the shell
        /// runs one invocation per slice and deletes the cache between them, and peak disk
        /// becomes a function of the slice rather than of the module.
        ///
        /// Slicing is by a stable hash of each asset's OUTPUT PATH minus its extension, which
        /// spreads assets evenly without clustering a module's large art into one slice, and -
        /// the part that matters - keeps assets that would COLLIDE together. See SliceKeyOf: the
        /// first version of this sliced by position, and the self-test caught it letting a
        /// foo.tga and a foo.png both survive in different slices when one pass would have kept
        /// only one. Every asset belongs to exactly one slice, so the slices are disjoint and
        /// their union is the whole module - pinned by converting a fixture in one slice and in
        /// three and requiring identical output.</summary>
        public static IEnumerator ExtractSteps(string dfmodPath, string outputRoot,
            ExtractReport report, bool canYield, int chunkIndex, int chunkCount)
        {
            if (chunkCount < 1)
                throw new ArgumentOutOfRangeException("chunkCount", chunkCount,
                    "A conversion has at least one slice.");
            if (chunkIndex < 0 || chunkIndex >= chunkCount)
                throw new ArgumentOutOfRangeException("chunkIndex", chunkIndex,
                    "Slice index must be in [0, " + chunkCount + ").");

            if (!File.Exists(dfmodPath))
                throw new FileNotFoundException("Desktop .dfmod not found", dfmodPath);

            DateTime lastImportYield = DateTime.UtcNow;
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
                bool loggedAudioLoad = false;   // see EnsureAudioData: logged once, counted always
                long sweepBudget = SweepBudgetBytes();
                DateTime lastYield = DateTime.UtcNow;
                // What the AUTHOR chose about each texture, for the import policy to honour.
                // See ReadableSidecarName. Facts are recorded here; the policy decides which of
                // them it acts on.
                var textureFlags = new Dictionary<string, string>(StringComparer.Ordinal);

                // Sorted so the order - and therefore which asset wins a collision - cannot
                // depend on however Unity happened to enumerate the bundle.
                var allAssets = new List<string>(ab.GetAllAssetNames());
                allAssets.Sort(StringComparer.Ordinal);

                foreach (string assetName in allAssets)
                {
                    if (assetName.EndsWith(ModManager.MODINFOEXTENSION, StringComparison.Ordinal))
                        continue; // rewritten below, not copied verbatim

                    // Notes describe assets that SURVIVED, so they are held until the write has
                    // actually happened and thrown away if it has not; see CommitNotes.
                    var notes = new List<string>();
                    string outPath = OutputPathFor(assetName, outputRoot, originalCase, notes);

                    // Is this asset in the slice being converted? The key is taken RELATIVE to
                    // the extraction root, because each slice extracts into its own folder and a
                    // key containing that folder would hash differently in every slice - which
                    // it did, until the union check reported both missing and duplicated assets.
                    // See SliceKeyOf for why it is the path minus its extension.
                    if (chunkCount > 1)
                    {
                        string sliceKey = RelativeToRoot(outPath, outputRoot) ?? outPath;
                        if (SliceOf(SliceKeyOf(sliceKey), chunkCount) != chunkIndex)
                            continue;
                    }

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
                            // The author's own decisions, recorded verbatim: Read/Write Enabled,
                            // and whether they left the texture UNCOMPRESSED. Both turned out to
                            // be load-bearing contracts rather than preferences.
                            string flags = (tex2d.isReadable ? "R" : string.Empty)
                                + (IsCompressedFormat(tex2d.format) ? string.Empty : "U");
                            if (flags.Length > 0)
                                textureFlags[outPath] = flags;
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

                    // THE SWEEP. Release drops this tool's reference to the asset, but dropping a
                    // reference is not reclaiming memory: an earlier round measured that
                    // Resources.UnloadAsset does not free an editor-side bundle texture at all,
                    // so without this the decoded copy of every texture in the mod accumulates
                    // until the bundle is unloaded at the very end. Measured on DREAM's hud &
                    // menu module that was ~600MB above an idle editor for 92MB of input, which
                    // projects badly onto a 1.72GB pack.
                    //
                    // UnloadUnusedAssets is a DIFFERENT mechanism from UnloadAsset: it collects
                    // assets nothing references any more, which is exactly what Release has just
                    // made these. The editor-only Immediate form is used because the runtime one
                    // returns an AsyncOperation, and an AsyncOperation cannot complete inside a
                    // step that is holding the main thread - the same trap the audio load walked
                    // into.
                    if (sweepBudget > 0 && report.releasedBytes >= sweepBudget)
                    {
                        report.releasedBytes = 0;
                        report.sweeps++;
                        EditorUtility.UnloadUnusedAssetsImmediate();
                        if (canYield)
                            yield return null;
                    }

                    // And a heartbeat, so the run cap is REACHABLE. Everything above happens
                    // inside one MoveNext for a module with no audio to wait on, and the driver
                    // can only check its watchdog between steps - so without this a GPU or
                    // import stall on a texture module would hang forever now that -quit is
                    // gone. Time-based rather than every-Nth-asset: it costs one frame per
                    // quarter second regardless of how big or small the assets are.
                    if (canYield && (DateTime.UtcNow - lastYield).TotalSeconds > HeartbeatSeconds)
                    {
                        lastYield = DateTime.UtcNow;
                        yield return null;
                    }
                }

                // The author's readable flags, written where the import policy can find them.
                // This has to happen BEFORE AssetDatabase.Refresh below, because that is what
                // triggers the import that consults it.
                WriteReadableSidecar(outputRoot, textureFlags, report);

                // Manifest identity is preserved; only Files points at the extraction - except
                // for a SLICE, which has to become a mod in its own right. DFU loads several
                // mods happily, so a chunked module ships as several .dfmod files, but each one
                // needs its own name in the mod list and its own GUID: two mods sharing a GUID
                // is not a cosmetic clash, it is the identity DFU keys on.
                //
                // The GUID is DERIVED rather than random, so converting the same module twice
                // produces the same identities and re-installing a slice replaces it instead of
                // adding a duplicate. MD5 of the original GUID plus the slice number is enough:
                // this needs determinism and distinctness, not unpredictability.
                string sliceName = SliceName(dfmodPath, chunkIndex, chunkCount);
                if (chunkCount > 1)
                {
                    modInfo.ModTitle = modInfo.ModTitle + " (" + (chunkIndex + 1) + " of " +
                        chunkCount + ")";
                    modInfo.GUID = DerivedGuid(modInfo.GUID, chunkIndex, chunkCount);
                }
                modInfo.Files = new List<string>(report.extracted);
                ModManager._serializer.TrySerialize(modInfo, out fsData data);
                string manifestOut = Path.Combine(outputRoot,
                    sliceName + ModManager.MODINFOEXTENSION);
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
            {
                AssetDatabase.ImportAsset(p, ImportAssetOptions.ForceUpdate);
                // The import loop is per-asset and can be the long pole on a texture module -
                // every one of these is a compress-to-ASTC - so it gets the same heartbeat.
                // (AssetDatabase.Refresh and BuildMod are single calls that cannot be broken up;
                // the run cap cannot interrupt those, which is stated rather than pretended.)
                if (canYield && (DateTime.UtcNow - lastImportYield).TotalSeconds > HeartbeatSeconds)
                {
                    lastImportYield = DateTime.UtcNow;
                    yield return null;
                }
            }
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
            return Convert(dfmodPath, extractRoot, bundleOutRoot, targets, 0, 1);
        }

        /// <summary>Converts ONE SLICE of a module; see ExtractSteps for why slices exist.</summary>
        public static string[] Convert(string dfmodPath, string extractRoot, string bundleOutRoot,
            BuildTarget[] targets, int chunkIndex, int chunkCount)
        {
            var built = new List<string>();
            IEnumerator steps = ConvertSteps(dfmodPath, extractRoot, bundleOutRoot, targets,
                false, built, chunkIndex, chunkCount);
            while (steps.MoveNext()) { }
            return built.ToArray();
        }

        /// <summary>The whole conversion as steps, so ConvertFromEnv can drive it across editor
        /// ticks; see ExtractSteps for why that matters.</summary>
        public static IEnumerator ConvertSteps(string dfmodPath, string extractRoot,
            string bundleOutRoot, BuildTarget[] targets, bool canYield, List<string> builtInto)
        {
            return ConvertSteps(dfmodPath, extractRoot, bundleOutRoot, targets, canYield,
                builtInto, 0, 1);
        }

        public static IEnumerator ConvertSteps(string dfmodPath, string extractRoot,
            string bundleOutRoot, BuildTarget[] targets, bool canYield, List<string> builtInto,
            int chunkIndex, int chunkCount)
        {
            // Disk before anything else. A conversion that runs out of space dies INSIDE a Unity
            // write - "Failed to write compressed chunk to the archive ... Error: 14" - which
            // reads like a corrupt bundle rather than a full disk, and cost a real afternoon.
            RequireFreeSpace(extractRoot, "starting");

            var report = new ExtractReport();
            IEnumerator steps = ExtractSteps(dfmodPath, extractRoot, report, canYield,
                chunkIndex, chunkCount);
            while (steps.MoveNext())
                yield return null;

            // And again before the build, which is the write that actually failed last time.
            RequireFreeSpace(extractRoot, "about to build the bundle for " +
                SliceName(dfmodPath, chunkIndex, chunkCount));

            // A conversion that saved nothing is a FAILURE, not a quiet success. Without this
            // the rewritten manifest lists no files, BuildMod packs the manifest alone, and the
            // operator gets a .dfmod that installs, loads, and does nothing at all - which is
            // exactly what dream - music.dfmod produces (0 of 81 clips). A bundle that cannot
            // possibly work must not be written, and the exit code has to say so, because the
            // whole point of a per-mod exit code is that a shell loop over a mods folder stops.
            if (report.extracted.Count == 0)
                throw new InvalidDataException(
                    "Converted nothing from " +
                    SliceName(dfmodPath, chunkIndex, chunkCount) + ": all " +
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
            line.Append("[MobileModExtractor] converted ")
                .Append(SliceName(dfmodPath, chunkIndex, chunkCount))
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
        public const string ChunkIndexVar = "DFU_MOD_CHUNK_INDEX";
        public const string ChunkCountVar = "DFU_MOD_CHUNK_COUNT";
        public const string KeepExtractionVar = "DFU_MOD_KEEP_EXTRACTION";

        /// <summary>A positive whole number, or the default. Used for the slice index and count,
        /// where a typo silently becoming 1 would quietly convert a fraction of a module and
        /// report success.</summary>
        public static int ParseCount(string raw, int fallback)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return fallback;
            int value;
            if (int.TryParse(raw.Trim(), out value) && value >= 1)
                return value;
            throw new InvalidOperationException(
                "'" + raw + "' is not a positive whole number; refusing to guess, because " +
                "guessing here converts part of a module and calls it a success.");
        }

        static IEnumerator driverSteps;
        static List<string> driverBuilt;
        static string driverExtractRoot;
        static DateTime driverStarted;
        // Resolved ONCE when the driver is armed, not re-read every frame. Re-parsing per tick
        // meant a mistyped DFU_MOD_TIMEOUT logged its "keeping the default" warning on every
        // editor frame for the length of the run - thousands of identical lines through the one
        // log an operator has to read.
        static double driverRunTimeout;

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

                int chunkCount = ParseCount(Environment.GetEnvironmentVariable(ChunkCountVar), 1);
                int chunkIndex = ParseCount(Environment.GetEnvironmentVariable(ChunkIndexVar), 1) - 1;
                if (chunkIndex < 0 || chunkIndex >= chunkCount)
                    throw new InvalidOperationException(
                        ChunkIndexVar + " is 1-based and must be between 1 and " +
                        ChunkCountVar + " (" + chunkCount + "); got " +
                        (Environment.GetEnvironmentVariable(ChunkIndexVar) ?? "unset"));

                // Each slice gets its OWN extraction folder. Sharing one would leave the
                // previous slice's assets on disk to be re-imported - which is the cost this
                // whole mechanism exists to bound - and would overwrite its readable sidecar.
                string extractRoot = "Assets/Game/Mods/Converted/" +
                    SliceName(input, chunkIndex, chunkCount);

                driverExtractRoot = extractRoot;
                driverBuilt = new List<string>();
                driverSteps = ConvertSteps(input, extractRoot, outRoot, targets, true,
                    driverBuilt, chunkIndex, chunkCount);
                driverStarted = DateTime.UtcNow;
                driverRunTimeout = RunTimeoutSeconds();
                Debug.Log($"[MobileModExtractor] converting " +
                    $"{SliceName(input, chunkIndex, chunkCount)}" +
                    (chunkCount > 1 ? $" [slice {chunkIndex + 1}/{chunkCount}]" : string.Empty) +
                    $" ({FreeBytesFor(extractRoot) / (1024.0 * 1024 * 1024):F1}GB disk free, " +
                    $"floor {MinFreeGb():F1}GB; per-clip audio cap {AudioLoadTimeoutSeconds():F0}s, " +
                    $"run cap {driverRunTimeout / 60:F0} min, sweep budget " +
                    $"{SweepBudgetBytes() / (1024 * 1024)}MB); this process exits by itself.");
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
                if (elapsed > driverRunTimeout)
                {
                    Debug.LogError($"[MobileModExtractor] giving up after {elapsed / 60:F1} " +
                        $"minutes (DFU_MOD_TIMEOUT={driverRunTimeout / 60:F0} min). The " +
                        "conversion is incomplete and no bundle should be trusted from this run.");
                    FinishDriver(2);
                    return;
                }

                if (driverSteps.MoveNext())
                    return;   // more to do; Unity gets its frame back, which is the point

                foreach (string built in driverBuilt)
                    Debug.Log("[MobileModExtractor] built " + built);
                ReleaseExtraction(driverExtractRoot);
                // Re-read the clock: `elapsed` above was sampled BEFORE the step that just ran,
                // and a module with no audio to wait on does all of its work inside the very
                // first one - which reported a 30-second conversion as "done in 0.0s".
                Debug.Log("[MobileModExtractor] done in " +
                    (DateTime.UtcNow - driverStarted).TotalSeconds.ToString("F1") + "s.");
                FinishDriver(driverBuilt.Count > 0 ? 0 : 1);
            }
            catch (Exception ex)
            {
                Debug.LogError("[MobileModExtractor] " + ex);
                FinishDriver(1);
            }
        }

        /// <summary>Deletes an extraction once its bundle exists, because the extraction is an
        /// intermediate and the bundle is the product.
        ///
        /// This is half of what bounds peak disk. A converted module's loose PNGs are larger
        /// than the module itself - 155MB of PNG from a 92MB bundle - and on a multi-gigabyte
        /// pack that is the difference between finishing and filling the disk. It happens only
        /// after a SUCCESSFUL build: a failed conversion leaves everything in place, because
        /// then the extraction is the evidence. DFU_MOD_KEEP_EXTRACTION=1 keeps it regardless.
        ///
        /// It does not touch Library/Artifacts, which is the other half and cannot be deleted
        /// while Unity is running - that is why slices are separate processes.</summary>
        static void ReleaseExtraction(string extractRoot)
        {
            if (string.IsNullOrEmpty(extractRoot)
                || ParseBoolLoose(Environment.GetEnvironmentVariable(KeepExtractionVar)))
                return;
            try
            {
                if (Directory.Exists(extractRoot))
                {
                    Directory.Delete(extractRoot, true);
                    File.Delete(extractRoot + ".meta");
                    Debug.Log($"[MobileModExtractor] removed the extraction at '{extractRoot}' " +
                        $"({FreeBytesFor(".") / (1024.0 * 1024 * 1024):F1}GB free now). " +
                        $"Set {KeepExtractionVar}=1 to keep it.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MobileModExtractor] could not remove '{extractRoot}': " +
                    $"{ex.GetType().Name}. The bundle is built; delete it by hand to reclaim disk.");
            }
        }

        static bool ParseBoolLoose(string raw)
        {
            return MobileConvertedModPolicy.ParseBool(raw, false);
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
            return ParsePositiveNumber(Environment.GetEnvironmentVariable(AudioTimeoutVar),
                DefaultAudioLoadTimeoutSeconds, AudioTimeoutVar);
        }

        public static double RunTimeoutSeconds()
        {
            return ParsePositiveNumber(Environment.GetEnvironmentVariable(RunTimeoutVar),
                DefaultRunTimeoutSeconds, RunTimeoutVar);
        }

        /// <summary>A positive number, or the default. Used for the timeouts and for the disk
        /// floor: a typo must not silently become "no limit", which is the one value a guard
        /// must never take.</summary>
        public static double ParsePositiveNumber(string raw, double fallback, string name)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return fallback;
            double value;
            if (double.TryParse(raw.Trim(), System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out value)
                && value > 0 && !double.IsInfinity(value))
                return value;
            Debug.LogWarning($"[MobileModExtractor] {name}='{raw}' is not a positive number; " +
                $"keeping {fallback}");
            return fallback;
        }

        /// <summary>How often the extraction hands a frame back purely so the driver can check
        /// its watchdog. A quarter second is far below any timeout worth setting and costs one
        /// frame per four assets-worth of work at worst, which is nothing against a conversion
        /// measured in tens of seconds.</summary>
        const double HeartbeatSeconds = 0.25;

        public const string SweepVar = "DFU_MOD_SWEEP_MB";

        /// <summary>How much released asset memory may pile up before the converter asks Unity
        /// to actually reclaim it. 0 disables the sweep entirely.
        ///
        /// The point of a BYTE budget rather than an every-N-assets cadence: it is the bytes that
        /// exhaust the machine, and a mod's assets are wildly uneven - DREAM's hud &amp; menu
        /// module mixes 1920x1200 screens with 64x64 icons, so "every 50 assets" means something
        /// different in every folder of the same mod. A budget makes the peak a function of the
        /// BUDGET rather than of the module, which is the property that decides whether a
        /// 1.72GB pack converts on a given machine at all.
        ///
        /// 256MB by default - and BE HONEST ABOUT WHAT THAT DEFAULT IS WORTH. Measured on the
        /// hud &amp; menu module (92MB of bundle, 330 textures, ~416MB of asset memory) the sweep
        /// does NOT reduce peak RSS: off runs peaked at 1438 and 1769MB, a 256MB budget (1 sweep)
        /// at 1746MB, and a 32MB budget (13 sweeps) at 1789MB, all inside the same run-to-run
        /// band, with wall clock identical at 32s throughout. So it is neither a win nor a cost
        /// at that size, and the peak there is an early transient rather than accumulation.
        ///
        /// It is on by default anyway, for one reason: accumulation is the only term here that
        /// GROWS WITH THE MODULE, and the pack this tool exists for is nineteen times larger
        /// than the one that could be measured. That is an extrapolation, not a result. Turn it
        /// off with DFU_MOD_SWEEP_MB=0, and read the "holding NNNMB of asset memory" figure in
        /// the summary line to see whether it could ever have mattered for a given module.
        /// Modules smaller than the budget never sweep at all and pay nothing.</summary>
        public const long DefaultSweepBudgetBytes = 256L * 1024 * 1024;

        public static long SweepBudgetBytes()
        {
            string raw = Environment.GetEnvironmentVariable(SweepVar);
            if (string.IsNullOrWhiteSpace(raw))
                return DefaultSweepBudgetBytes;
            long mb;
            if (long.TryParse(raw.Trim(), out mb) && mb >= 0)
                return mb * 1024 * 1024;
            Debug.LogWarning($"[MobileModExtractor] {SweepVar}='{raw}' is not a whole number of " +
                $"megabytes (0 disables); keeping {DefaultSweepBudgetBytes / (1024 * 1024)}MB");
            return DefaultSweepBudgetBytes;
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
                // Native plugin sources. Unity hands these to PluginImporter and compiles them
                // into the player for whatever platforms the importer enables - verified in this
                // project, where Assets/Plugins/iOS/DFMobilePointer.mm carries a PluginImporter.
                // Whether Unity 6 still restricts that to a Plugins/ folder could NOT be
                // confirmed here (the project has no native source outside Plugins/), and it
                // does not matter: this rule is folder-independent, so a mod cannot smuggle one
                // in by choosing a path, whichever way Unity behaves.
                case ".m": case ".mm": case ".c": case ".cpp": case ".h": case ".swift":
                case ".jar": case ".aar":                     // Android plugins
                // And a .meta is not content at all - it is the file that tells Unity how to
                // import the file BESIDE it. A hostile one can rewrite a sibling asset's
                // importer settings or claim a GUID that already belongs to a project asset,
                // which is a way to corrupt the project without writing a single asset.
                case ".meta":
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

            // Measure BEFORE unloading anything, while the asset still holds its memory. This is
            // what drives the sweep: not how many assets have gone by, but how many bytes.
            if (report != null)
            {
                try
                {
                    long size = UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(obj);
                    report.releasedBytes += size;
                    report.releasedBytesTotal += size;
                }
                catch (Exception) { /* a size we cannot read is not worth failing a conversion for */ }
            }

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
                $"released {report.released}/{report.loaded} loaded holding " +
                $"{report.releasedBytesTotal / (1024 * 1024)}MB of asset memory, {report.sweeps} " +
                $"memory sweeps -> {outputRoot}";
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

        /// <summary>True when the texture is stored in a BLOCK-COMPRESSED format.
        ///
        /// This matters because a mod author leaving a texture uncompressed is usually a
        /// decision rather than an oversight: DFU slices classic UI art with GetPixels sub-rects,
        /// and a block format constrains what rectangles are addressable at all - DFU says so
        /// itself in SpellIconCollection, which refuses an atlas that is "compressed with a
        /// block-based format but icons are not multiple of 4". DREAM's author left TALK02I0 and
        /// TALK03I0 as RGBA32 while compressing almost everything else; compressing them anyway
        /// broke the talk window.
        ///
        /// The list is of COMPRESSED families, so an unfamiliar format is treated as
        /// uncompressed. That is the safe direction: the cost of being wrong is size, and the
        /// cost of the other error is a broken window.</summary>
        public static bool IsCompressedFormat(TextureFormat format)
        {
            switch (format)
            {
                case TextureFormat.DXT1: case TextureFormat.DXT1Crunched:
                case TextureFormat.DXT5: case TextureFormat.DXT5Crunched:
                case TextureFormat.BC4: case TextureFormat.BC5:
                case TextureFormat.BC6H: case TextureFormat.BC7:
                case TextureFormat.ETC_RGB4: case TextureFormat.ETC2_RGB:
                case TextureFormat.ETC2_RGBA1: case TextureFormat.ETC2_RGBA8:
                case TextureFormat.ETC_RGB4Crunched: case TextureFormat.ETC2_RGBA8Crunched:
                case TextureFormat.EAC_R: case TextureFormat.EAC_R_SIGNED:
                case TextureFormat.EAC_RG: case TextureFormat.EAC_RG_SIGNED:
                case TextureFormat.PVRTC_RGB2: case TextureFormat.PVRTC_RGBA2:
                case TextureFormat.PVRTC_RGB4: case TextureFormat.PVRTC_RGBA4:
                case TextureFormat.ASTC_4x4: case TextureFormat.ASTC_5x5:
                case TextureFormat.ASTC_6x6: case TextureFormat.ASTC_8x8:
                case TextureFormat.ASTC_10x10: case TextureFormat.ASTC_12x12:
                case TextureFormat.ASTC_HDR_4x4: case TextureFormat.ASTC_HDR_5x5:
                case TextureFormat.ASTC_HDR_6x6: case TextureFormat.ASTC_HDR_8x8:
                case TextureFormat.ASTC_HDR_10x10: case TextureFormat.ASTC_HDR_12x12:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>Name of the file that carries the author's Read/Write Enabled flags from the
        /// extraction to the import.
        ///
        /// It starts with a dot on purpose: Unity's AssetDatabase ignores dot-prefixed files
        /// entirely, so this never becomes an asset, never gets a .meta, and can never end up
        /// inside a rebuilt bundle. It is a note the extractor leaves for the postprocessor, not
        /// content.</summary>
        public const string ReadableSidecarName = ".readable-textures.txt";

        /// <summary>Records which extracted textures came from a source the author had marked
        /// Read/Write Enabled.
        ///
        /// WHY THIS EXISTS AT ALL. MobileConvertedModImporter used to force isReadable=false on
        /// every converted texture, to save the CPU-side copy. That broke DFU on a device:
        /// TextureReplacement.TryImportTexture only LOGS when a non-readable texture reaches a
        /// caller that needs pixels and hands it over anyway, and ImageReader then calls
        /// GetPixels32 on it - which throws, every frame, inside the UI draw loop. DFU says whose
        /// call this is in its own remark (TextureReplacement.cs): "It is up to mod authors to
        /// ensure that textures from asset bundles have `Read/Write Enabled` flag set when
        /// required." The author's flag is therefore ground truth, and 202 of the 330 textures in
        /// DREAM's hud &amp; menu module have it set.
        ///
        /// WHY A SIDECAR rather than fixing the flag up after import. The postprocessor cannot
        /// see the source bundle - by import time it is gone - so the information has to be
        /// carried. Re-importing each texture a second time with the corrected flag would work
        /// and would double the ASTC compression bill for every texture in a multi-gigabyte
        /// pack, which is the most expensive part of a conversion. A static map in memory would
        /// be cheaper still and would not survive the reimport that a cache invalidation or a
        /// GetVersion bump causes, silently reverting the fix later. A file next to the assets
        /// survives both and is inspectable by hand.</summary>
        static void WriteReadableSidecar(string outputRoot, Dictionary<string, string> flags,
            ExtractReport report)
        {
            var lines = new List<string>();
            foreach (var entry in flags)
            {
                string rel = RelativeToRoot(entry.Key, outputRoot);
                if (rel != null)
                    lines.Add(entry.Value + "\t" + rel);
            }
            lines.Sort(StringComparer.Ordinal);   // stable file across identical conversions
            TryWriteFile(Path.Combine(outputRoot, ReadableSidecarName),
                System.Text.Encoding.UTF8.GetBytes(string.Join("\n", lines.ToArray())),
                outputRoot, "the readable-texture list", report);
        }

        /// <summary>An extracted asset's path relative to its extraction root, with forward
        /// slashes - the spelling the sidecar stores and the importer looks up.</summary>
        public static string RelativeToRoot(string path, string root)
        {
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(root))
                return null;
            string full, fullRoot;
            try
            {
                full = Path.GetFullPath(path).Replace('\\', '/');
                fullRoot = Path.GetFullPath(root).Replace('\\', '/');
            }
            catch (Exception) { return null; }
            if (!fullRoot.EndsWith("/", StringComparison.Ordinal))
                fullRoot += "/";
            if (full.Length <= fullRoot.Length
                || !full.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
                return null;
            return full.Substring(fullRoot.Length);
        }

        /// <summary>The key an asset is sliced on: its output path WITHOUT the extension,
        /// lowercased.
        ///
        /// Not its position in the list, and the difference is a bug the self-test caught. Two
        /// bundle assets can resolve to ONE output file - a foo.tga and a foo.png collapse once
        /// the texture extension is rewritten - and a whole conversion resolves that by letting
        /// the first one win and counting the other as a collision. Sliced by position, those two
        /// can land in different slices, where each has its own claim table and BOTH survive: the
        /// module then ships two mods that provide the same short name, and load order decides
        /// which the game sees. The union stopped matching a single pass, which is the one
        /// property slicing must not lose.
        ///
        /// Keying on the extensionless output path puts every asset that could possibly collide
        /// in the same slice - a rewrite only ever changes the extension - so the existing
        /// first-one-wins rule resolves them exactly as it would have in one pass.</summary>
        public static string SliceKeyOf(string outPath)
        {
            string path = (outPath ?? string.Empty).Replace('\\', '/');
            int dot = path.LastIndexOf('.');
            int slash = path.LastIndexOf('/');
            if (dot > slash)
                path = path.Substring(0, dot);
            return path.ToLowerInvariant();
        }

        /// <summary>Which slice a key belongs to. MD5 rather than string.GetHashCode, because
        /// .NET randomises string hashes per process - slices computed in different Unity
        /// invocations would disagree about which assets they own, and the module would come out
        /// with holes and duplicates. This has to be stable across processes and machines.</summary>
        public static int SliceOf(string key, int chunkCount)
        {
            if (chunkCount <= 1)
                return 0;
            using (var md5 = System.Security.Cryptography.MD5.Create())
            {
                byte[] hash = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(key ?? string.Empty));
                uint value = (uint)(hash[0] | (hash[1] << 8) | (hash[2] << 16) | (hash[3] << 24));
                return (int)(value % (uint)chunkCount);
            }
        }

        public const string MinFreeVar = "DFU_MOD_MIN_FREE_GB";

        /// <summary>Free space, in GB, below which a conversion refuses to continue.
        ///
        /// 4GB is not a guess about what a slice needs - it is a guess about what a FAILURE
        /// costs. Running out mid-write does not produce a clean error: Unity dies with "Failed
        /// to write compressed chunk to the archive 'Temp/unitystream.unity3d'! Error: 14",
        /// which reads like a corrupt bundle, and it can leave a half-written .dfmod that looks
        /// installable. Stopping early with the word "disk" in the message is worth several GB
        /// of caution. Raise or lower it with DFU_MOD_MIN_FREE_GB.</summary>
        public const double DefaultMinFreeGb = 4;

        public static double MinFreeGb()
        {
            return ParsePositiveNumber(Environment.GetEnvironmentVariable(MinFreeVar),
                DefaultMinFreeGb, MinFreeVar);
        }

        /// <summary>Free bytes on the volume holding a path, or -1 when it cannot be
        /// determined - in which case the check declines to block rather than guessing.</summary>
        public static long FreeBytesFor(string path)
        {
            try
            {
                string full = Path.GetFullPath(string.IsNullOrEmpty(path) ? "." : path);
                string root = Path.GetPathRoot(full);
                if (string.IsNullOrEmpty(root))
                    return -1;
                return new DriveInfo(root).AvailableFreeSpace;
            }
            catch (Exception) { return -1; }
        }

        /// <summary>Stops the conversion while the message can still say "disk".</summary>
        static void RequireFreeSpace(string path, string what)
        {
            long free = FreeBytesFor(path);
            if (free < 0)
                return;   // unknown: not a reason to refuse to work

            double freeGb = free / (1024.0 * 1024 * 1024);
            double floor = MinFreeGb();
            if (freeGb >= floor)
                return;

            throw new IOException(string.Format(
                "Only {0:F1}GB free where the extraction goes, below the {1:F1}GB floor " +
                "({2}), {3}. THIS IS A DISK PROBLEM, not a bad mod: converting a large module " +
                "fills Unity's import cache (Library/Artifacts reached 25GB on an 800MB " +
                "module). Free space, or convert this module in slices with " +
                "DFU_MOD_CHUNK_COUNT - see README-iOS.md. Stopping now rather than failing " +
                "inside a Unity write, which reports a corrupt archive instead of a full disk.",
                freeGb, floor, MinFreeVar, what));
        }

        /// <summary>The file-stem a slice's manifest and bundle take: "dream - mobs (2 of 4)".
        /// The whole-module name when there is only one slice, so an unchunked conversion is
        /// byte-for-byte what it always was.</summary>
        public static string SliceName(string dfmodPath, int chunkIndex, int chunkCount)
        {
            string baseName = Path.GetFileNameWithoutExtension(dfmodPath);
            return chunkCount > 1
                ? baseName + " (" + (chunkIndex + 1) + " of " + chunkCount + ")"
                : baseName;
        }

        /// <summary>A GUID for one slice, derived from the module's own so it is stable across
        /// conversions and distinct between slices. Formatted as a GUID because that is what
        /// ModInfo.GUID is compared as.</summary>
        public static string DerivedGuid(string sourceGuid, int chunkIndex, int chunkCount)
        {
            string seed = (sourceGuid ?? string.Empty) + "#" + chunkIndex + "/" + chunkCount;
            using (var md5 = System.Security.Cryptography.MD5.Create())
                return new Guid(md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(seed))).ToString();
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
            // Nothing should reach here with no bytes - every producer either returns content or
            // throws. But this is the one place every write passes through, and the cost of a
            // recurrence differs enormously: as a named skip it costs one asset and says which,
            // whereas File.WriteAllBytes turns it into "ArgumentNullException: Value cannot be
            // null", an error about an argument that names nothing. That exact confusion cost a
            // whole module once (see TexturePng.Encode).
            if (bytes == null)
            {
                Skipped(report, "no-content");
                Debug.LogWarning($"[MobileModExtractor] {sourceName} produced no bytes to write " +
                    $"to '{path}'; skipping it rather than writing an empty file. This is a bug " +
                    "in whatever produced it, not a property of the mod - please report it.");
                return false;
            }

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
            // The fast path is an optimisation and is allowed to decline, in EITHER of the two
            // ways it can. It can throw - and it can RETURN NULL, which is what EncodeToPNG does
            // for a format it cannot serialise, silently and without complaint. That second case
            // is not exotic: EncodeToPNG handles only a few uncompressed layouts, so a texture
            // that is readable AND block-compressed hits it every time, and "readable and
            // block-compressed" is what a real mod's art actually is - 180 of the 330 textures in
            // DREAM's hud & menu module are readable BC7. Treating only the throw as a decline
            // let those 180 return null from here, which surfaced three layers away as
            // "ArgumentNullException: Value cannot be null" out of File.WriteAllBytes, blamed on
            // the write. The fixtures never caught it because they are RGB24/RGBA32, the one
            // family EncodeToPNG does handle.
            byte[] fast = null;
            if (src.isReadable)
            {
                try
                {
                    fast = perPixel == null
                        ? src.EncodeToPNG()
                        : EncodePixels(Transform(src.GetPixels32(), perPixel),
                                       src.width, src.height, linear);
                }
                catch (Exception) { /* fall through to GPU path */ }
            }
            if (fast != null && fast.Length > 0)
                return fast;
            // Graphics.Blit against the null device is a silent no-op: ReadPixels then returns
            // a uniform grey and the extraction looks entirely successful - right name, right
            // path, right size, no pixels. Corrupting a mod quietly is worse than not
            // converting it, so refuse instead of guessing.
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                throw new InvalidOperationException(
                    "Cannot decode texture '" + src.name + "' (format " + src.format +
                    ", isReadable " + src.isReadable + "): this texture cannot be encoded " +
                    "directly - a compressed one never can, whether or not it is readable - so " +
                    "it has to be decoded by a GPU blit, and this Unity process has no graphics " +
                    "device. Re-run the conversion WITHOUT the -nographics flag ('-batchmode' " +
                    "on its own is right; see README-iOS.md).");

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
                // This method must never hand back null. A null here would travel silently to
                // the write and arrive as "Value cannot be null" with no texture named in it -
                // an error about an argument, three layers from the texture that caused it.
                // tmp is RGBA32, which EncodeToPNG does handle, so this should be unreachable;
                // if it ever is reached, say WHICH texture and in what state.
                if (png == null || png.Length == 0)
                    throw new InvalidOperationException(
                        "Could not encode texture '" + src.name + "' to PNG even after a GPU " +
                        "blit (source format " + src.format + ", graphicsFormat " +
                        src.graphicsFormat + ", isReadable " + src.isReadable + ", " +
                        src.width + "x" + src.height + ").");
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

        /// <summary>The cap applied to classic UI art, which is to say none: 16384 is Unity's
        /// maximum, so nothing is ever downscaled. This is not a memory oversight. UI art is
        /// drawn at 1:1 and DFU computes GetPixels rects from its dimensions, so a clamp does not
        /// cost quality, it changes arithmetic - see IsClassicUiArt.</summary>
        public const int MaxUiTextureSize = 16384;

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

        // One parsed sidecar per converted mod, remembered with the file's stamp so an edit or a
        // re-conversion is picked up without an editor restart. Import runs per asset and there
        // are thousands of them; re-reading the file each time would be the expensive way to get
        // the same answer.
        static readonly Dictionary<string, KeyValuePair<string, Dictionary<string, string>>> readableCache =
            new Dictionary<string, KeyValuePair<string, Dictionary<string, string>>>(StringComparer.Ordinal);
        static readonly HashSet<string> warnedMissingSidecar = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>True when the mod author had Read/Write Enabled set on the texture this
        /// asset was extracted from.
        ///
        /// A readable texture keeps a CPU-side copy, so it costs roughly double - and that is
        /// exactly why this is not our decision to make. DFU's own contract
        /// (TextureReplacement.cs) puts it on the mod author: "It is up to mod authors to ensure
        /// that textures from asset bundles have `Read/Write Enabled` flag set when required."
        /// Overriding it saved memory and froze the game.
        ///
        /// A missing sidecar means the extraction predates this mechanism, and there is no
        /// honest default: false can crash the game and true can double a multi-gigabyte pack.
        /// So it keeps the memory-cheap answer and says so ONCE per mod, naming the folder,
        /// because "re-convert this mod" is the actual fix and a silent guess is what caused
        /// this bug in the first place.</summary>
        public static bool SourceWasReadable(string assetPath)
        {
            return SourceFlags(assetPath).IndexOf('R') >= 0;
        }

        /// <summary>True when the author left the source texture UNCOMPRESSED.</summary>
        public static bool SourceWasUncompressed(string assetPath)
        {
            return SourceFlags(assetPath).IndexOf('U') >= 0;
        }

        /// <summary>The author's recorded choices for one extracted texture, as flag letters
        /// ("R" readable, "U" uncompressed), or empty when nothing is known.</summary>
        static string SourceFlags(string assetPath)
        {
            string path = (assetPath ?? string.Empty).Replace('\\', '/');
            if (!path.StartsWith(Root, StringComparison.Ordinal))
                return string.Empty;
            int slash = path.IndexOf('/', Root.Length);
            if (slash < 0)
                return string.Empty;

            string modRoot = path.Substring(0, slash);
            string relative = path.Substring(slash + 1);
            string sidecar = modRoot + "/" + MobileModExtractor.ReadableSidecarName;

            string stamp;
            try
            {
                var info = new FileInfo(sidecar);
                stamp = info.Exists ? info.LastWriteTimeUtc.Ticks + ":" + info.Length : null;
            }
            catch (Exception) { stamp = null; }

            if (stamp == null)
            {
                if (warnedMissingSidecar.Add(modRoot))
                    Debug.LogWarning($"[MobileConvertedModPolicy] no {MobileModExtractor.ReadableSidecarName} " +
                        $"in '{modRoot}', so the mod author's Read/Write Enabled flags are not " +
                        "known; importing its textures non-readable, which is the memory-cheap " +
                        "answer and the one that can make DFU throw on UI art. Re-convert this " +
                        "mod to restore the flags.");
                return string.Empty;
            }

            KeyValuePair<string, Dictionary<string, string>> cached;
            if (!readableCache.TryGetValue(modRoot, out cached) || cached.Key != stamp)
            {
                var set = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                try
                {
                    foreach (string line in File.ReadAllLines(sidecar))
                    {
                        string entry = line.Trim();
                        if (entry.Length == 0)
                            continue;
                        // "<flags>\t<path>". A line with no tab is the ORIGINAL format, which
                        // recorded readability alone - read it as "R" rather than discarding an
                        // older extraction's flags outright.
                        int tab = entry.IndexOf('\t');
                        if (tab < 0)
                            set[entry] = "R";
                        else
                            set[entry.Substring(tab + 1)] = entry.Substring(0, tab);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[MobileConvertedModPolicy] could not read '{sidecar}': " +
                        $"{ex.GetType().Name}. Importing this mod's textures non-readable.");
                }
                cached = new KeyValuePair<string, Dictionary<string, string>>(stamp, set);
                readableCache[modRoot] = cached;
            }

            string found;
            return cached.Value.TryGetValue(relative, out found) ? found : string.Empty;
        }

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
        /// <summary>ASTC block size for CLASSIC UI ART, as opposed to the tunable world-texture
        /// one. 4x4 rather than 6x6, and not for quality: DFU addresses this art with GetPixels
        /// SUB-RECTS whose coordinates it computes itself, and a block format constrains which
        /// rectangles are addressable. DFU states the assumption in SpellIconCollection, which
        /// refuses an atlas "compressed with a block-based format but icons are not multiple of
        /// 4". 4x4 is the only block size that cannot introduce an alignment the classic 320x200
        /// arithmetic does not already satisfy. It costs 8 bits/pixel against 6x6's 3.56, which
        /// is the price of the UI working.</summary>
        public const TextureImporterFormat UiFormat = TextureImporterFormat.ASTC_4x4;

        /// <summary>True when this asset is classic UI art - the same set the no-mipmap rule
        /// picks out, and for a related reason. That rule says this art is drawn at 1:1; this one
        /// says DFU also does PIXEL-EXACT ARITHMETIC on it, so its dimensions and its format are
        /// a contract rather than a preference.
        ///
        /// DaggerfallTalkWindow is the worked example: it slices its background with
        /// GetPixels((int)(4 * (width / 320f)), ...) - classic 320x200 coordinates scaled by the
        /// REPLACEMENT texture's own width. DREAM's TALK01I0 is 1920x1200, exactly 6x the classic
        /// canvas, so every one of those rects lands on an integer. Clamped to 1024 it becomes
        /// 3.2x, every rect origin and size truncates, and the window comes up with blank panels
        /// and dead buttons.</summary>
        public static bool IsClassicUiArt(string assetPath, string[] noMipMarkers)
        {
            return !ShouldMipmap(assetPath, noMipMarkers);
        }

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
    /// THERE ARE TWO POLICIES HERE, and the split is the important part. Classic UI art
    /// (.IMG/.CIF/.RCI names and DFU's Img/CifRci folders) has a PIXEL-EXACT CONTRACT with DFU's
    /// own code: DaggerfallTalkWindow slices its background with GetPixels rects computed as
    /// classic 320x200 coordinates scaled by the replacement texture's own width, and
    /// SpellIconCollection refuses a block-compressed atlas whose icons are not a multiple of 4.
    /// So for that art the author's dimensions and format are preserved - no size clamp, no
    /// compression where the source had none, and ASTC 4x4 where a compressed source must be
    /// re-encoded because iOS cannot decode BC7. World textures have no such contract and keep
    /// the memory-optimised policy (1024 cap, ASTC 6x6) - as does everything else, EXCEPT a
    /// texture the author left both uncompressed AND readable, which is them saying "code reads
    /// pixels out of this" in two independent ways. That is where the memory actually is:
    /// UI art is a few hundred images, world textures are gigabytes. DO NOT RE-OPTIMISE THE UI
    /// PATH - twice now a saving that looked free has broken a window instead.
    ///
    /// isReadable follows the MOD AUTHOR's Read/Write Enabled flag, and does not force anything.
    /// Dropping the CPU-side mirror looks like pure waste avoided - it is a second copy of every
    /// texture, so a readable one costs roughly double - but forcing it off froze the game on a
    /// device: DFU hands a non-readable texture to callers that need pixels with only a log, and
    /// ImageReader's GetPixels32 then throws every frame inside the UI draw loop. DFU's own
    /// remark says whose call it is ("It is up to mod authors to ensure that textures from asset
    /// bundles have `Read/Write Enabled` flag set when required"), so the memory it costs is the
    /// author's decision and not this converter's. See MobileModExtractor.ReadableSidecarName. npotScale None
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
        public override uint GetVersion() { return 4; }

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
            // THE AUTHOR'S FLAG, NOT OURS. This used to be a flat isReadable=false to save the
            // CPU-side copy, and that broke DFU on a device: a non-readable texture handed to a
            // caller that needs pixels only produces a LOG from TryImportTexture, which returns
            // it anyway, and ImageReader then calls GetPixels32 on it - throwing every frame
            // inside the UI draw loop. The visible symptom is a frozen UI smeared with cursor
            // trails, which looks like a hang and is not one. See ReadableSidecar.
            importer.isReadable = MobileConvertedModPolicy.SourceWasReadable(assetPath);

            // THE SPLIT. Classic UI art has a pixel-exact contract with DFU's own code and world
            // textures do not, so they get different policies - and the memory that matters is
            // in the world textures anyway. See IsClassicUiArt.
            string[] markers = MobileConvertedModPolicy.NoMipMarkers();
            bool uiArt = MobileConvertedModPolicy.IsClassicUiArt(assetPath, markers);
            bool uncompressedSource = MobileConvertedModPolicy.SourceWasUncompressed(assetPath);

            // A texture the author left UNCOMPRESSED *and* marked READABLE is one they expected
            // code to read pixels out of - two independent signals, and neither is the default.
            // It gets the UI treatment even without a classic name, because the name rule is a
            // heuristic and this is the author saying it outright. Found by measurement, not
            // theory: DREAM's hud & menu has exactly two such textures outside the .IMG/.CIF/.RCI
            // set - "cursor" and "renameSaveButtonBackgroundColor", the second of which says in
            // its own name that something samples its pixels - and preserving both costs 0.06MB.
            bool pixelContract = uiArt || (uncompressedSource
                && MobileConvertedModPolicy.SourceWasReadable(assetPath));
            bool keepUncompressed = pixelContract && uncompressedSource;

            importer.textureCompression = keepUncompressed
                ? TextureImporterCompression.Uncompressed
                : TextureImporterCompression.Compressed;
            // No size clamp on UI art: its dimensions ARE the contract. DREAM sizes its talk
            // window art at exactly 6x the classic 320x200 canvas, and clamping it to 1024 turns
            // that into 3.2x and truncates every rect DaggerfallTalkWindow computes.
            importer.maxTextureSize = pixelContract
                ? MobileConvertedModPolicy.MaxUiTextureSize
                : MobileConvertedModPolicy.MaxTextureSize();
            importer.mipmapEnabled = MobileConvertedModPolicy.MipmapsAllowed()
                && MobileConvertedModPolicy.ShouldMipmap(assetPath, markers);
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
            ios.maxTextureSize = importer.maxTextureSize;
            ios.compressionQuality = MobileConvertedModPolicy.CompressionQuality();
            if (keepUncompressed)
            {
                // The author left this uncompressed and DFU reads sub-rects out of it; naming
                // RGBA32 explicitly is what stops the platform choosing a block format anyway.
                ios.textureCompression = TextureImporterCompression.Uncompressed;
                ios.format = TextureImporterFormat.RGBA32;
            }
            else
            {
                ios.textureCompression = TextureImporterCompression.Compressed;
                // BC7/DXT cannot be decoded by iOS, so a compressed source still has to become
                // ASTC - but UI art takes the 4x4 block, which cannot break the alignment maths.
                ios.format = pixelContract
                    ? MobileConvertedModPolicy.UiFormat
                    : MobileConvertedModPolicy.IosFormat();
            }
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
