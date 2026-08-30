// Project:         Daggerfall Unity
// Copyright:       Copyright (C) 2009-2023 Daggerfall Workshop
// Web Site:        http://www.dfworkshop.net
// License:         MIT License (http://www.opensource.org/licenses/mit-license.php)
// Source Code:     https://github.com/Interkarma/daggerfall-unity
// Original Author: TheLacus
// Contributors:
// 
// Notes:
//

using System;
using System.Collections;
using System.IO;
using UnityEngine;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Game.Mobile;

namespace DaggerfallWorkshop.Utility.AssetInjection
{
    /// <summary>
    /// Handles import and injection of custom sounds and songs with the purpose of providing modding support.
    /// Sound files are imported from mod bundles with load order or loaded directly from disk.
    /// </summary>
    public static class SoundReplacement
    {
        #region Fields & Properties

        static readonly string soundPath = Path.Combine(Application.streamingAssetsPath, "Sound");

        /// <summary>
        /// Path to custom sounds and songs on disk.
        /// </summary>
        public static string SoundPath
        {
            get { return soundPath; }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Seek sound from mods.
        /// </summary>
        /// <param name="sound">Sound clip to seek.</param>
        /// <param name="audioClip">Audioclip with imported sound data.</param>
        /// <returns>True if sound is found.</returns>
        public static bool TryImportSound(SoundClips sound, out AudioClip audioClip)
        {
            return TryImportAudioClip(sound.ToString(), ".wav", false, out audioClip);
        }

        /// <summary>
        /// Seek song from mods.
        /// </summary>
        /// <param name="song">Song to seek.</param>
        /// <param name="audioClip">Audioclip with imported sound data.</param>
        /// <returns>True if song is found.</returns>
        public static bool TryImportSong(SongFiles song, out AudioClip audioClip)
        {
            return TryImportAudioClip(song.ToString(), ".ogg", true, out audioClip);
        }

        /// <summary>
        /// Seek midi song from mods.
        /// </summary>
        /// <param name="filename">Name of song to seek including .mid extension.</param>
        /// <param name="songBytes">Midi data as a byte array.</param>
        /// <returns>True if song is found.</returns>
        public static bool TryImportMidiSong(string filename, out byte[] songBytes)
        {
            return TryGetAudioBytes("song_" + filename, out songBytes);
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Import sound data from modding locations as an audio clip.
        /// </summary>
        private static bool TryImportAudioClip(string name, string extension, bool streaming, out AudioClip audioClip)
        {
            if (DaggerfallUnity.Settings.AssetInjection)
            {
                // Seek from loose files
                // MOBILE: user content from the app's Documents folder (see MobileContentPath).
                string path = MobileContentPath.Override(Path.Combine(soundPath, name + extension));
                if (File.Exists(path))
                {
#if UNITY_IOS && !UNITY_EDITOR
                    // MEASURED ON DEVICE: the legacy WWW("file://") path below hands back a
                    // clip with 0 channels, 0 Hz and 0 samples on iOS - and TryImport* still
                    // returns true, so the engine plays that silence instead of falling back
                    // to the original audio. Silently losing sound is worse than not
                    // replacing it, so iOS decodes WAV itself and declines everything else.
                    if (string.Equals(extension, ".wav", StringComparison.OrdinalIgnoreCase))
                    {
                        if (TryDecodeWavFromDisk(path, name, out audioClip))
                            return true;
                    }
                    else if (string.Equals(extension, ".ogg", StringComparison.OrdinalIgnoreCase))
                    {
                        // Preload-then-swap. Returning a clip that is still loading would be
                        // unsafe here: DaggerfallSongPlayer assigns it and then waits on
                        // clip.loadState, so a clip that never finishes leaves the game in
                        // permanent silence - the same silent failure the legacy WWW path
                        // caused. Instead the first request declines (MIDI plays, which is the
                        // authentic track anyway) and kicks off a background load; once it is
                        // ready the next request for that song returns it.
                        if (TryGetPreloadedSong(path, name, out audioClip))
                            return true;
                    }
                    else
                    {
                        Debug.LogWarning("[SoundReplacement] " + extension + " replacement is not " +
                                         "supported on iOS yet; using the original audio for " + name);
                    }

                    audioClip = null;
                    return false;
#else
                    // WWW is removed in Unity 6, so this is UnityWebRequest now - the same
                    // API the iOS ogg preloader above already uses. WWW's old semantics were
                    // "return a clip that finishes loading in the background"; a UWR clip only
                    // exists after the request completes, so this waits for it. The wait is
                    // bounded and cheap: it is a file:// read on desktop, which completes in
                    // milliseconds, and the timeout means a truncated file fails with a log
                    // line rather than hanging the game.
                    string url = "file://" + path;
                    AudioType audioType = AudioType.UNKNOWN;
                    if (string.Equals(extension, ".ogg", StringComparison.OrdinalIgnoreCase))
                        audioType = AudioType.OGGVORBIS;
                    else if (string.Equals(extension, ".wav", StringComparison.OrdinalIgnoreCase))
                        audioType = AudioType.WAV;
                    else if (string.Equals(extension, ".mp3", StringComparison.OrdinalIgnoreCase))
                        audioType = AudioType.MPEG;

                    using (var request = UnityEngine.Networking.UnityWebRequestMultimedia.GetAudioClip(url, audioType))
                    {
                        var handler = (UnityEngine.Networking.DownloadHandlerAudioClip)request.downloadHandler;
                        handler.streamAudio = streaming;

                        request.SendWebRequest();

                        var started = DateTime.UtcNow;
                        while (!request.isDone)
                        {
                            if ((DateTime.UtcNow - started).TotalSeconds > 10)
                            {
                                Debug.LogErrorFormat("Timed out loading audioclip: {0}", path);
                                audioClip = null;
                                return false;
                            }
                            System.Threading.Thread.Sleep(1);
                        }

                        if (request.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
                        {
                            Debug.LogErrorFormat("Failed to load audioclip: {0}", request.error);
                            audioClip = null;
                            return false;
                        }

                        audioClip = UnityEngine.Networking.DownloadHandlerAudioClip.GetContent(request);
                        return audioClip != null;
                    }
#endif
                }

                // Seek from mods
                if (ModManager.Instance != null && ModManager.Instance.TryGetAsset(name, false, out audioClip))
                {
                    if (audioClip.preloadAudioData || audioClip.LoadAudioData())
                        return true;

                    Debug.LogErrorFormat("Failed to load audiodata for audioclip {0}", name);
                }
            }

            audioClip = null;
            return false;
        }

        /// <summary>
        /// Import midi data from modding locations as a byte array.
        /// </summary>
        private static bool TryGetAudioBytes(string name, out byte[] songBytes)
        {
            if (DaggerfallUnity.Settings.AssetInjection)
            {
                // Seek from loose files
                // MOBILE: user content from the app's Documents folder (see MobileContentPath).
                string path = MobileContentPath.Override(Path.Combine(soundPath, name));
                if (File.Exists(path))
                {
                    songBytes = File.ReadAllBytes(path);
                    return true;
                }

                // Seek from mods
                if (ModManager.Instance != null)
                {
                    TextAsset textAsset;
                    if (ModManager.Instance.TryGetAsset(name, false, out textAsset))
                    {
                        songBytes = textAsset.bytes;
                        return true;
                    }
                }
            }

            songBytes = null;
            return false;
        }

        /// <summary>
        /// Load audio data from WWW in background.
        /// </summary>
#if UNITY_IOS && !UNITY_EDITOR
        // Songs decoded in the background, keyed by song name.
        static readonly System.Collections.Generic.Dictionary<string, AudioClip> preloadedSongs =
            new System.Collections.Generic.Dictionary<string, AudioClip>();
        static readonly System.Collections.Generic.HashSet<string> songLoadsInFlight =
            new System.Collections.Generic.HashSet<string>();

        /// <summary>
        /// Return a previously decoded song if one is ready, otherwise start decoding it and
        /// report failure so the caller keeps playing the original track.
        ///
        /// UNTESTED ON DEVICE - there is no ogg encoder on the build machine to produce a test
        /// file. Written so that every failure path falls back to the original music rather
        /// than to silence, which bounds the risk if it does not work.
        /// </summary>
        static bool TryGetPreloadedSong(string path, string name, out AudioClip clip)
        {
            if (preloadedSongs.TryGetValue(name, out clip) && clip != null &&
                clip.loadState == AudioDataLoadState.Loaded)
                return true;

            clip = null;

            if (!songLoadsInFlight.Contains(name) && DaggerfallUnity.Instance != null)
            {
                songLoadsInFlight.Add(name);
                DaggerfallUnity.Instance.StartCoroutine(PreloadSong(path, name));
            }

            return false;
        }

        static IEnumerator PreloadSong(string path, string name)
        {
            using (UnityEngine.Networking.UnityWebRequest request =
                       UnityEngine.Networking.UnityWebRequestMultimedia.GetAudioClip(
                           "file://" + path, AudioType.OGGVORBIS))
            {
                yield return request.SendWebRequest();

                if (request.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
                {
                    AudioClip loaded =
                        UnityEngine.Networking.DownloadHandlerAudioClip.GetContent(request);

                    if (loaded != null && loaded.samples > 0)
                    {
                        loaded.name = name;
                        preloadedSongs[name] = loaded;
                        Debug.Log("[SoundReplacement] decoded replacement song " + name);
                    }
                    else
                    {
                        Debug.LogWarning("[SoundReplacement] decoded an empty clip for " + name +
                                         "; keeping the original music");
                    }
                }
                else
                {
                    Debug.LogWarning("[SoundReplacement] could not decode " + name + ": " +
                                     request.error + "; keeping the original music");
                }
            }

            songLoadsInFlight.Remove(name);
        }
#endif

        /// <summary>
        /// Decode a RIFF/WAVE file into an AudioClip synchronously.
        ///
        /// Done by hand rather than through UnityWebRequest because the caller's contract is
        /// synchronous - it must return a usable clip immediately - and because for a local
        /// file there is nothing for a web request to do. Supports 8/16/24/32-bit PCM and
        /// 32-bit float, which covers what a WAV in a mod folder will realistically be.
        /// </summary>
        public static bool TryDecodeWavFromDisk(string path, string name, out AudioClip audioClip)
        {
            audioClip = null;

            try
            {
                byte[] data = File.ReadAllBytes(path);
                if (data.Length < 44 ||
                    data[0] != 'R' || data[1] != 'I' || data[2] != 'F' || data[3] != 'F' ||
                    data[8] != 'W' || data[9] != 'A' || data[10] != 'V' || data[11] != 'E')
                {
                    Debug.LogError("[SoundReplacement] not a RIFF/WAVE file: " + path);
                    return false;
                }

                int channels = 0, sampleRate = 0, bits = 0, format = 1;
                int dataOffset = -1, dataLength = 0;

                // Walk the chunk list rather than assuming a 44-byte header: real files carry
                // LIST/fact chunks and the data chunk is not always where you expect.
                int pos = 12;
                while (pos + 8 <= data.Length)
                {
                    string id = System.Text.Encoding.ASCII.GetString(data, pos, 4);
                    int size = BitConverter.ToInt32(data, pos + 4);
                    int body = pos + 8;
                    if (size < 0 || body + size > data.Length)
                        size = data.Length - body;

                    if (id == "fmt ")
                    {
                        format = BitConverter.ToInt16(data, body);
                        channels = BitConverter.ToInt16(data, body + 2);
                        sampleRate = BitConverter.ToInt32(data, body + 4);
                        bits = BitConverter.ToInt16(data, body + 14);
                    }
                    else if (id == "data")
                    {
                        dataOffset = body;
                        dataLength = size;
                    }

                    pos = body + size + (size % 2);   // chunks are word aligned
                }

                if (channels <= 0 || sampleRate <= 0 || dataOffset < 0 || dataLength <= 0)
                {
                    Debug.LogError("[SoundReplacement] unusable WAV header in " + path);
                    return false;
                }

                int bytesPerSample = bits / 8;
                if (bytesPerSample <= 0)
                {
                    Debug.LogError("[SoundReplacement] unsupported bit depth " + bits + " in " + path);
                    return false;
                }

                int totalSamples = dataLength / bytesPerSample;
                float[] samples = new float[totalSamples];

                for (int i = 0; i < totalSamples; i++)
                {
                    int o = dataOffset + i * bytesPerSample;
                    if (format == 3 && bits == 32)
                        samples[i] = BitConverter.ToSingle(data, o);
                    else if (bits == 8)
                        samples[i] = (data[o] - 128) / 128f;          // 8-bit PCM is unsigned
                    else if (bits == 16)
                        samples[i] = BitConverter.ToInt16(data, o) / 32768f;
                    else if (bits == 24)
                        samples[i] = ((data[o] | (data[o + 1] << 8) | ((sbyte)data[o + 2] << 16))) / 8388608f;
                    else if (bits == 32)
                        samples[i] = BitConverter.ToInt32(data, o) / 2147483648f;
                    else
                    {
                        Debug.LogError("[SoundReplacement] unsupported WAV bit depth " + bits + " in " + path);
                        return false;
                    }
                }

                audioClip = AudioClip.Create(name, totalSamples / channels, channels, sampleRate, false);
                if (!audioClip.SetData(samples, 0))
                {
                    Debug.LogError("[SoundReplacement] SetData failed for " + path);
                    audioClip = null;
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError("[SoundReplacement] WAV decode failed for " + path + ": " + ex.Message);
                audioClip = null;
                return false;
            }
        }

        #endregion
    }
}
