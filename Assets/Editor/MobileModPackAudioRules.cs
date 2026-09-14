// Project:         Daggerfall Unity - iOS Touch Input Layer
// License:         MIT License
//
// MOBILE: how a FETCHED mod pack's audio imports for iOS. The sibling of
// MobileModPackTextureRules, and it exists for the same reason that one does: the settings live in
// an AssetPostprocessor, which only runs inside an import and therefore cannot be tested, so the
// decisions are lifted out into pure functions that MobileSelfTest can pin without Unity importing
// anything.
//
// THE HOLE THIS FILLS. The conversion side of the pipeline already has a complete audio policy -
// MobileConvertedModImporter.OnPreprocessAudio in MobileModExtractor.cs sets Vorbis, a quality and
// a load type. But it is scoped by InScope() to MobileConvertedModPolicy.Root, which is
// "Assets/Game/Mods/Converted/". Mods fetched from source by tools/bundled-mods/fetch.py land at
// "Assets/Game/Mods/<Name>/" and were never covered, because until the atmosphere round no fetched
// pack had any audio in it. Better Ambience (50 WAV) and Immersive Footsteps (210 MP3) do, and
// without a rule here they import at Unity's defaults - PCM, DecompressOnLoad, preloadAudioData on -
// and go into the bundle as decoded samples. That is the whole reason this file exists.
//
// TWO CLASSES, AND THE THRESHOLD IS A DURATION WEARING A SIZE.
//   Sfx - a short clip that is triggered and must be audible on the frame it was asked for.
//         Vorbis, CompressedInMemory, forced to mono, audio data preloaded. A footstep that has to
//         fault its samples in has already missed its step.
//   Bed - a long ambient or musical clip that plays under everything.
//         Vorbis, Streaming, left in the author's channel count, NOT preloaded. A resident 30 s
//         stereo bed is about 5 MB of PCM; streaming costs one decode buffer.
//
// The extractor's own StreamingThresholdBytes (2 MB) is measured on a file THAT CONVERTER WROTE,
// always uncompressed 16-bit PCM, so one number is one duration there. Here the files are whatever
// the author committed - 44.1 kHz stereo WAV in one pack, 22 kHz mono MP3 in the next - so a single
// byte count would mean ten seconds in one pack and four minutes in another. Hence a threshold per
// source extension, each chosen to land near ten seconds:
//
//   .wav   1 MB  ~ 11.9 s of 44.1 kHz mono Int16, ~5.9 s of 44.1 kHz stereo
//   .mp3   160 KB ~ 13 s at 96 kbps, ~10 s at 128 kbps
//   .ogg   160 KB  same arithmetic; Vorbis and MP3 sit at comparable bitrates for this material
//
// Every clip in the four mods the atmosphere round ships is well under its threshold and imports as
// Sfx: Better Ambience's longest is 0.79 s / 140 KB (AmbientRaining.wav, the one real bed, is
// excluded by mods.json because no source file in that repo references it), Immersive Footsteps'
// longest is 0.91 s / 11 KB, First-Person Lighting's two are 0.25 s / 7 KB. The Bed branch is
// therefore exercised only by MobileSelfTest today. That is said out loud rather than left to be
// discovered, exactly as MobileConvertedModPolicy.LoadTypeForSize says it of its own streaming half.
//
// Read by MobileModPackAudioImporter in MobileModBuilder.cs; pinned as pure rules by
// MobileSelfTest.TestPackAudioRules.
//
// Place in Assets/Editor/

namespace DaggerfallWorkshop.Game.Mobile.EditorTools
{
    public static class MobileModPackAudioRules
    {
        /// <summary>What a clip is for. See the file header.</summary>
        public enum Class { Sfx, Bed }

        // Mod folder names under Assets/Game/Mods/ (the mods.json entry name). Only these are
        // touched: a pack with no audio must not have its import cache invalidated by this rule.
        static readonly string[] audioMods = { "BetterAmbience", "ImmersiveFootsteps", "FirstPersonLighting" };

        /// <summary>The mod names this importer applies to, for verification.</summary>
        // MOBILE: AsReadOnly, not the array itself - same reason as MobileModPackTextureRules.RawDataMods:
        // an IReadOnlyList&lt;string&gt; that IS the live array can be cast back to string[] and mutated.
        // Exposed so MobileSelfTest can check every name against tools/bundled-mods/mods.json, because
        // nothing else ties this literal to the fetched folder name and a rename there would quietly
        // send 262 clips back to uncompressed PCM with nothing in any log to say so.
        public static System.Collections.Generic.IReadOnlyList<string> AudioMods
        {
            get { return System.Array.AsReadOnly(audioMods); }
        }

        /// <summary>
        /// Vorbis quality for a fetched pack's audio. 0.5, where the converted-mod policy uses 0.7.
        /// The two are answering different questions: the converter re-encodes art that arrived as
        /// somebody's finished desktop bundle and cannot be re-authored, so it errs high; these clips
        /// are footsteps and one-shot effects at a fraction of a second, played through a phone
        /// speaker or a pair of earbuds, where the top of the scale spends bandwidth on detail nothing
        /// in the chain can resolve. The atmosphere spec asks for "~0.5" and this is it.
        /// </summary>
        public const float VorbisQuality = 0.5f;

        /// <summary>Bed threshold for a source .wav. See the file header for the arithmetic.</summary>
        public const long WavBedThresholdBytes = 1024 * 1024;

        /// <summary>Bed threshold for a compressed source (.mp3/.ogg). See the file header.</summary>
        public const long CompressedBedThresholdBytes = 160 * 1024;

        /// <summary>True when this asset is inside a pack this importer owns.</summary>
        public static bool Applies(string assetPath)
        {
            string p = Normalize(assetPath);
            foreach (string mod in audioMods)
                if (p.StartsWith("Assets/Game/Mods/" + mod + "/", System.StringComparison.Ordinal))
                    return true;
            return false;
        }

        /// <summary>The bed threshold for a path, by its source extension. See the file header.</summary>
        public static long BedThresholdBytes(string assetPath)
        {
            string p = Normalize(assetPath);
            if (p.EndsWith(".wav", System.StringComparison.OrdinalIgnoreCase))
                return WavBedThresholdBytes;
            return CompressedBedThresholdBytes;
        }

        /// <summary>
        /// Sfx or Bed, from the path (which picks the threshold) and the file's size on disk.
        /// Pure, so the self-test can walk a table of sizes without an import or a file.
        /// </summary>
        public static Class For(string assetPath, long fileBytes)
        {
            return fileBytes > BedThresholdBytes(assetPath) ? Class.Bed : Class.Sfx;
        }

        /// <summary>
        /// Sfx sit compressed in memory; beds stream. Neither is safe as a blanket rule - a streamed
        /// sound effect misses the frame it was triggered on, and a resident bed is megabytes the
        /// device does not get back - which is the same split, and the same reasoning, as
        /// MobileConvertedModPolicy.LoadTypeForSize.
        /// </summary>
        public static UnityEngine.AudioClipLoadType LoadType(Class cls)
        {
            return cls == Class.Bed
                ? UnityEngine.AudioClipLoadType.Streaming
                : UnityEngine.AudioClipLoadType.CompressedInMemory;
        }

        /// <summary>
        /// Sfx are forced to mono; beds keep the author's channels. 23 of Better Ambience's 51 clips
        /// are 44.1 kHz STEREO footsteps - two identical-sounding channels for a sound the port then
        /// positions in 3D anyway (the mod's own AudioSource sets spatialBlend = 1, and Unity pans a
        /// spatialised clip from its mono downmix), so the second channel is bytes and decode time
        /// spent on something that cannot be heard. A bed is the one place stereo earns its size.
        /// </summary>
        public static bool ForceToMono(Class cls)
        {
            return cls == Class.Sfx;
        }

        /// <summary>
        /// Sfx preload their audio data; beds do not. This is the flag, not the load type: DFU's own
        /// SoundReplacement reads a mod clip as "if (audioClip.preloadAudioData || audioClip.LoadAudioData())",
        /// so a clip that arrives without samples costs a synchronous load at the moment it is first
        /// needed - which for a footstep is the moment it is already late.
        /// </summary>
        public static bool PreloadAudioData(Class cls)
        {
            return cls == Class.Sfx;
        }

        static string Normalize(string assetPath)
        {
            return (assetPath ?? "").Replace('\\', '/');
        }
    }
}
