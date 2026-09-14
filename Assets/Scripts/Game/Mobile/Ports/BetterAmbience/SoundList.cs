// MOBILE PORT - source: github.com/joshcamas/daggerfall-unity-mods @ c59e2aa9085734df3aefd19b5dd8c0b49cfcaade
// File BetterAmbience/Utility/SoundList.cs, copied for iOS except lines marked MOBILE.
// Upstream licence (repo-root LICENSE): MIT, Copyright (c) 2020 Josh Steinhauer.
// The LICENSE lives at the repo ROOT, outside BetterAmbience/, so it is not in the fetched
// tree; it is reproduced verbatim in THIRD-PARTY.md.

using UnityEngine;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Utility.ModSupport.ModSettings;
using DaggerfallWorkshop;
using System.IO;
using System.Collections.Generic;

namespace SpellcastStudios
{
    public class SoundList
    {
        private List<SoundClips> soundClips = new List<SoundClips>();
        private List<AudioClip> audioClips = new List<AudioClip>();

        private float pitchMin = 1;
        private float pitchMax = 1;
        private float volume = 1;

        public void SetVolume(float volume)
        {
            this.volume = volume;
        }

        public void SetPitchRange(float min, float max)
        {
            pitchMin = min;
            pitchMax = max;
        }

        public void PlayRandomClip(DaggerfallAudioSource source, AudioSource audioSource, float volume)
        {
            audioSource.pitch = Random.Range(pitchMin, pitchMax);
            audioSource.PlayOneShot(GetRandomClip(source), this.volume * volume);
        }

        public AudioClip GetRandomClip(DaggerfallAudioSource source)
        {
            int max = soundClips.Count + audioClips.Count;

            if (max == 0)
                return null;

            int rand = Random.Range(0, soundClips.Count + audioClips.Count);

            if (rand < soundClips.Count)
                return source.GetAudioClip((int)soundClips[rand]);
            else
                return audioClips[rand];
        }

        public void AddAudioClip(AudioClip clip)
        {
            audioClips.Add(clip);
        }

        public void AddAudioClips(List<AudioClip> clips)
        {
            audioClips.AddRange(clips);
        }

        public void AddSoundClip(SoundClips soundClip)
        {
            soundClips.Add(soundClip);
        }
    }

    public class SoundUtility
    {
        //Imports using my format
        public static List<AudioClip> TryImportAudioClips(string name, string extension, int count, bool streaming)
        {
           var audioClips = new List<AudioClip>();

            for(int i = 0; i < count; i++)
            {
                string cname = name + "_" + i.ToString().PadLeft(3, '0');
                // MOBILE: upstream passes "wav" without the dot; normalise so the loose-file branch matches.
                string ext = extension.StartsWith(".") ? extension : "." + extension;
                AudioClip clip = null;

                if (TryImportAudioClip(cname, ext, streaming, out clip))
                {
                    audioClips.Add(clip);
                }
            }

            return audioClips;
        }

        // MOBILE: upstream copied DFU's SoundReplacement.TryImportAudioClip and used UnityEngine.WWW,
        // which Unity removed in 2023.1 - this project is on 6000.3.23f1, so the original body does not
        // compile at all. It is also no longer worth copying: this port's SoundReplacement already does
        // the same two lookups (loose file under StreamingAssets/Sound via MobileContentPath, then the
        // enabled mod bundles by extensionless name) with UnityWebRequestMultimedia and a synchronous
        // WAV decoder. The bundle branch is the one that fires on iOS: better-ambience.dfmod carries all
        // 50 sfx_footstep_* clips and TryGetAsset finds them by name.
        public static bool TryImportAudioClip(string name, string extension, bool streaming, out AudioClip audioClip)
        {
            audioClip = null;
            if (!DaggerfallUnity.Settings.AssetInjection)
                return false;

            // Loose override first, exactly as SoundReplacement orders it.
            string path = DaggerfallWorkshop.Game.Mobile.MobileContentPath.Override(
                System.IO.Path.Combine(DaggerfallWorkshop.Utility.AssetInjection.SoundReplacement.SoundPath, name + extension));
            if (File.Exists(path)
                && string.Equals(extension, ".wav", System.StringComparison.OrdinalIgnoreCase)
                && DaggerfallWorkshop.Utility.AssetInjection.SoundReplacement.TryDecodeWavFromDisk(path, name, out audioClip))
                return true;

            // Then the bundles. preloadAudioData is false for every clip this port's audio importer
            // touches (see MobileModPackAudioRules), so ask for the samples the way DFU's own
            // SoundReplacement does before handing the clip out.
            if (ModManager.Instance != null && ModManager.Instance.TryGetAsset(name, false, out audioClip) && audioClip != null)
            {
                if (audioClip.preloadAudioData || audioClip.LoadAudioData())
                    return true;
                Debug.LogErrorFormat("[BetterAmbience] failed to load audiodata for audioclip {0}", name);
            }

            audioClip = null;
            return false;
        }

    }

}