// Project:         Daggerfall Unity - iOS Touch Layer
// License:         MIT License
//
// MOBILE 2026-09-14: AMBIENCE. Two native touches that share one host object because they share one
// audience - the player wearing headphones on a train, for whom the game is mostly sound.
//
//   REVERB ([Enhancements] AudioReverb). Daggerfall Unity plays every sound dry: a footstep in a
//   stone dungeon and the same footstep in a tavern are bit-identical. One AudioReverbFilter on the
//   AudioListener, switched on PlayerEnterExit transitions, costs nothing measurable and is the
//   single largest "this sounds like a place" change available. The filter sits on the LISTENER
//   rather than per-source so it applies to everything - engine, mods, and the five optional audio
//   mods of this round alike - without any of them knowing it exists.
//
//   LIGHTNING ([Enhancements] LightningFlash). Storm weather already plays three thunder clips
//   (StormLightningShort, StormLightningThunder, StormThunderRoll) and DFU's own visual half of it
//   has been dead since 2015 - AmbientEffectsPlayer's header says so ("NOTE: Lightning sky effects
//   are deprecated for now"), and its PlayLightningEffect flag is false on every prefab. So the
//   thunder arrives with nothing to have caused it. This flashes the screen white FIRST and then
//   lets the clip play after a delay drawn from a random strike distance, which is the whole of
//   what makes lightning read as distance rather than as a sound effect.
//
// WHY THE DELAY NEEDS AN ENGINE HOOK. AmbientEffectsPlayer raises OnPlayEffect at the moment the
// clip starts, so a listener on that event can only ever flash WITH the thunder or after it -
// exactly the reading this feature exists to remove. The hook (AmbientEffectsPlayer.MobileStormDelay)
// is asked for a delay BEFORE the clip plays, does the flash on the way past, and returns 0 for
// every case it does not own - the switch off, indoors, a non-storm clip, or too soon after the
// last flash - in which case the engine plays the clip exactly as it always did. One branch, one
// `return 0`, no behaviour change when the feature is off.
//
// WHY THE FLASH IS AN IMGUI OVERLAY. It has to survive four compositing paths this port already
// has: plain rendering, retro mode's 320x200/640x400 chain, the CRT presenter at native
// resolution, and the 4:3 aspect viewport. A camera clear or an image effect lives inside those
// chains and has to be re-reasoned for each; OnGUI runs once, after every camera in the frame has
// presented, over the final composite. GUI.depth -100 puts it in front of DaggerfallUI (depth 0),
// which is right: lightning whitens the HUD along with everything else. It draws on Repaint only
// and draws nothing at all when the alpha is zero, so the cost off-storm is one branch per frame.

using DaggerfallConnect;
using DaggerfallWorkshop.Game.Utility;
using UnityEngine;

namespace DaggerfallWorkshop.Game.Mobile
{
    public static class MobileAmbience
    {
        #region Reverb

        /// <summary>MOBILE: the three kinds of space Daggerfall has, as far as sound is concerned.</summary>
        public enum Space
        {
            Exterior,
            BuildingInterior,
            Dungeon,
        }

        /// <summary>
        /// MOBILE pure: the space-to-preset map, and the whole of the reverb design.
        /// <para>
        /// Exterior is Off, not Generic - outdoors has no early reflections to model and Unity's
        /// Generic preset would put the player in a small room under the sky. Building interiors
        /// are Room: Daggerfall's interiors are houses, shops and taverns, none of them large.
        /// Dungeons split three ways because they are the only case where the difference is audible
        /// and the block type is free to read: the natural ones (caves, mines, nests) are Cave,
        /// castle blocks are Hallway - long, hard, straight - and every built dungeon is
        /// StoneCorridor, which is what the rest of them are.
        /// </para>
        /// </summary>
        public static AudioReverbPreset PresetFor(Space space, DFRegion.DungeonTypes dungeonType, bool castle)
        {
            switch (space)
            {
                case Space.BuildingInterior:
                    return AudioReverbPreset.Room;

                case Space.Dungeon:
                    if (castle)
                        return AudioReverbPreset.Hallway;
                    return IsNaturalDungeon(dungeonType) ? AudioReverbPreset.Cave : AudioReverbPreset.StoneCorridor;

                default:
                    return AudioReverbPreset.Off;
            }
        }

        /// <summary>
        /// MOBILE pure: the dungeon types that are holes in the ground rather than buildings. The
        /// nests and the volcanic caves are here for the same reason the caves are - Daggerfall
        /// builds all of them out of the natural cave block set, so they sound like caves.
        /// </summary>
        public static bool IsNaturalDungeon(DFRegion.DungeonTypes dungeonType)
        {
            switch (dungeonType)
            {
                case DFRegion.DungeonTypes.NaturalCave:
                case DFRegion.DungeonTypes.Mine:
                case DFRegion.DungeonTypes.VolcanicCaves:
                case DFRegion.DungeonTypes.SpiderNest:
                case DFRegion.DungeonTypes.ScorpionNest:
                case DFRegion.DungeonTypes.HarpyNest:
                    return true;
                default:
                    return false;
            }
        }

        #endregion

        #region Lightning

        /// <summary>MOBILE: nearest strike, in seconds of thunder delay. Roughly 100 m of air.</summary>
        public const float MinThunderDelay = 0.3f;

        /// <summary>MOBILE: farthest strike still worth flashing for - about 1.4 km.</summary>
        public const float MaxThunderDelay = 4.0f;

        /// <summary>MOBILE: how long the whole flash lasts, rise and decay together.</summary>
        public const float FlashDuration = 0.22f;

        /// <summary>MOBILE: the rise. Short enough to read as instant, long enough not to strobe.</summary>
        public const float FlashRise = 0.03f;

        /// <summary>MOBILE: peak whiteness of the NEAREST strike. Not 1.0 - a full white frame on an
        /// OLED iPad at night is painful, and the HUD underneath should still be legible through it.</summary>
        public const float MaxFlashAlpha = 0.6f;

        /// <summary>MOBILE: peak whiteness of the FARTHEST strike - a glow on the horizon, not a hit.</summary>
        public const float MinFlashAlpha = 0.15f;

        /// <summary>MOBILE: the rate limit. AmbientEffectsPlayer's own storm interval is 4-35 s, so
        /// this normally never bites; it is here so that a mod or a debug command driving the
        /// ambient player harder cannot turn the screen into a strobe.</summary>
        public const float MinFlashInterval = 3.0f;

        /// <summary>MOBILE pure: the three clips that are lightning. Everything else the ambient
        /// player might hand us plays untouched.</summary>
        public static bool IsStormClip(SoundClips clip)
        {
            return clip == SoundClips.StormLightningShort
                || clip == SoundClips.StormLightningThunder
                || clip == SoundClips.StormThunderRoll;
        }

        /// <summary>
        /// MOBILE pure: seconds between the flash and the thunder, from a 0..1 roll standing for how
        /// far away the strike is. Linear, because the delay IS the distance (sound covers about
        /// 340 m a second) - so a uniform roll gives uniformly distributed strikes across the sky
        /// rather than bunching them near or far.
        /// </summary>
        public static float DelayForDistance(float roll01)
        {
            if (float.IsNaN(roll01))
                roll01 = 0.5f;
            return Mathf.Lerp(MinThunderDelay, MaxThunderDelay, Mathf.Clamp01(roll01));
        }

        /// <summary>
        /// MOBILE pure: how bright that strike flashes. The far ones are dim - the one thing that
        /// stops every flash reading as a hit directly overhead. Derived from the delay rather than
        /// from the roll so that the two can never disagree about which strike this is.
        /// </summary>
        public static float PeakAlphaForDelay(float delay)
        {
            float t = Mathf.InverseLerp(MinThunderDelay, MaxThunderDelay, delay);
            return Mathf.Lerp(MaxFlashAlpha, MinFlashAlpha, t);
        }

        /// <summary>
        /// MOBILE pure: the flash envelope - alpha at <paramref name="elapsed"/> seconds in. A fast
        /// linear rise and a squared decay, which is what a discharge looks like: it arrives all at
        /// once and the afterglow fades. Zero before it starts and after it ends, so the driver can
        /// call it every frame and only draw when it is non-zero.
        /// </summary>
        public static float FlashAlpha(float elapsed, float peak)
        {
            if (float.IsNaN(elapsed) || elapsed < 0f || elapsed >= FlashDuration || peak <= 0f)
                return 0f;
            if (elapsed < FlashRise)
                return peak * (elapsed / FlashRise);
            float decay = 1f - (elapsed - FlashRise) / (FlashDuration - FlashRise);
            return peak * decay * decay;
        }

        /// <summary>
        /// MOBILE pure: the rate limit, as a function rather than a field so the self test can walk
        /// it. A negative <paramref name="lastFlashAt"/> means "never flashed".
        /// </summary>
        public static bool CanFlash(float lastFlashAt, float now)
        {
            return lastFlashAt < 0f || now - lastFlashAt >= MinFlashInterval;
        }

        #endregion

        #region Driver

        static Driver instance;

        /// <summary>
        /// MOBILE: one host for the life of the app - the transition and storm hooks are static and
        /// have to survive the swap between the title scene and the game, as MobileAutosave's does.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            if (instance != null)
                return;

            GameObject host = new GameObject("MobileAmbience");
            instance = host.AddComponent<Driver>();
            Object.DontDestroyOnLoad(host);
        }

        /// <summary>MOBILE: re-read the player's surroundings and push the reverb preset. Public so
        /// the settings panel's row can apply the switch without waiting for the next doorway.</summary>
        public static void ApplyReverbNow()
        {
            if (instance != null)
                instance.RefreshReverb();
        }

        public class Driver : MonoBehaviour
        {
            AudioReverbFilter filter;
            AudioReverbPreset appliedPreset = (AudioReverbPreset)(-1);

            float flashStartedAt = -1f;
            float flashPeak = 0f;
            float lastFlashAt = -1f;

            void OnEnable()
            {
                PlayerEnterExit.OnTransitionInterior += OnAnyTransition;
                PlayerEnterExit.OnTransitionExterior += OnAnyTransition;
                PlayerEnterExit.OnTransitionDungeonInterior += OnAnyTransition;
                PlayerEnterExit.OnTransitionDungeonExterior += OnAnyTransition;
                StartGameBehaviour.OnStartGame += OnStartGame;

                // The engine asks this for a delay before it plays a storm clip. Installed once and
                // left installed: it answers 0 for everything it does not own, including the switch
                // being off, so there is no state to tear down when the player turns it off.
                AmbientEffectsPlayer.MobileStormDelay = StormClipDelay;
            }

            void OnDisable()
            {
                PlayerEnterExit.OnTransitionInterior -= OnAnyTransition;
                PlayerEnterExit.OnTransitionExterior -= OnAnyTransition;
                PlayerEnterExit.OnTransitionDungeonInterior -= OnAnyTransition;
                PlayerEnterExit.OnTransitionDungeonExterior -= OnAnyTransition;
                StartGameBehaviour.OnStartGame -= OnStartGame;

                if (AmbientEffectsPlayer.MobileStormDelay == (System.Func<SoundClips, float>)StormClipDelay)
                    AmbientEffectsPlayer.MobileStormDelay = null;
            }

            void OnStartGame(object sender, System.EventArgs e)
            {
                RefreshReverb();
            }

            void OnAnyTransition(PlayerEnterExit.TransitionEventArgs args)
            {
                RefreshReverb();
            }

            /// <summary>
            /// MOBILE: read where the player is and put the matching preset on the listener. Called
            /// on every transition and whenever the switch moves; cheap enough either way, and it
            /// short-circuits when the preset has not changed so re-entering the same dungeon block
            /// does not restart the filter.
            /// </summary>
            internal void RefreshReverb()
            {
                AudioReverbPreset preset = AudioReverbPreset.Off;
                Space space = Space.Exterior;
                DFRegion.DungeonTypes dungeonType = DFRegion.DungeonTypes.NoDungeon;

                if (DaggerfallUnity.Settings.AudioReverb && GameManager.HasInstance)
                {
                    PlayerEnterExit enterExit = GameManager.Instance.PlayerEnterExit;
                    if (enterExit != null)
                    {
                        bool castle = enterExit.IsPlayerInsideDungeonCastle;
                        if (enterExit.IsPlayerInsideDungeon)
                        {
                            space = Space.Dungeon;
                            if (enterExit.Dungeon != null)
                                dungeonType = enterExit.Dungeon.Summary.DungeonType;
                        }
                        else if (enterExit.IsPlayerInside)
                        {
                            space = Space.BuildingInterior;
                        }
                        preset = PresetFor(space, dungeonType, castle);
                    }
                }

                if (preset == appliedPreset)
                    return;
                appliedPreset = preset;

                if (filter == null)
                {
                    AudioListener listener = Object.FindObjectOfType<AudioListener>();
                    if (listener == null)
                        return;
                    filter = listener.GetComponent<AudioReverbFilter>();
                    if (filter == null)
                        filter = listener.gameObject.AddComponent<AudioReverbFilter>();
                }

                // Disabled rather than left on the Off preset: Off still runs the filter, and an
                // exterior that pays for a reverb that does nothing is the one cost nobody agreed to.
                filter.reverbPreset = preset;
                filter.enabled = preset != AudioReverbPreset.Off;

                Debug.Log(string.Format("[Ambience] reverb preset {0} ({1}{2})",
                    preset, space,
                    space == Space.Dungeon ? ", " + dungeonType : ""));
            }

            /// <summary>
            /// MOBILE: the engine hook. Returns the seconds the storm clip should be held back, and
            /// flashes the screen on the way past; 0 means "not ours, play it now" and is the answer
            /// for every case below, which is what keeps the feature off when it is off.
            /// </summary>
            internal float StormClipDelay(SoundClips clip)
            {
                if (!DaggerfallUnity.Settings.LightningFlash || !IsStormClip(clip))
                    return 0f;

                if (!GameManager.HasInstance)
                    return 0f;

                // No flash from inside: a dungeon has no sky, and a building's windows are painted on.
                PlayerEnterExit enterExit = GameManager.Instance.PlayerEnterExit;
                if (enterExit != null && (enterExit.IsPlayerInside || enterExit.IsPlayerInsideDungeon))
                    return 0f;

                if (!CanFlash(lastFlashAt, Time.time))
                    return 0f;

                float delay = DelayForDistance(Random.value);
                flashPeak = PeakAlphaForDelay(delay);
                flashStartedAt = Time.time;
                lastFlashAt = Time.time;

                Debug.Log(string.Format("[Ambience] lightning flash peak {0:0.00} ({1}) - thunder in {2:0.0} s",
                    flashPeak, clip, delay));
                return delay;
            }

            /// <summary>
            /// MOBILE: the flash itself. Repaint only - OnGUI runs at least twice a frame and
            /// drawing in the layout pass would double the alpha - and nothing at all while the
            /// envelope is zero, which is every frame that is not in the 0.22 s after a strike.
            /// </summary>
            void OnGUI()
            {
                if (Event.current == null || Event.current.type != EventType.Repaint || flashStartedAt < 0f)
                    return;

                float alpha = FlashAlpha(Time.time - flashStartedAt, flashPeak);
                if (alpha <= 0f)
                {
                    flashStartedAt = -1f;
                    return;
                }

                // Lower depth draws in front: DaggerfallUI is 0, so -100 is over the finished frame,
                // HUD included. The colour is restored because GUI.color is global to the IMGUI pass.
                Color was = GUI.color;
                GUI.depth = -100;
                GUI.color = new Color(1f, 1f, 1f, alpha);
                GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
                GUI.color = was;
            }
        }

        #endregion
    }
}
