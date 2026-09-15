// MOBILE PORT - source: github.com/joshcamas/daggerfall-unity-mods @ c59e2aa9085734df3aefd19b5dd8c0b49cfcaade
// File BetterAmbience/BetterFootsteps/BetterFootstepsMod.cs, copied for iOS except lines marked MOBILE.
// Upstream licence (repo-root LICENSE): MIT, Copyright (c) 2020 Josh Steinhauer.
// The LICENSE lives at the repo ROOT, outside BetterAmbience/, so it is not in the fetched
// tree; it is reproduced verbatim in THIRD-PARTY.md.

using UnityEngine;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Utility.ModSupport.ModSettings;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Utility;

namespace SpellcastStudios.BetterFootsteps
{
    public class BetterFootstepsMod : MonoBehaviour
    {
        private static Mod mod;

        private ModSettings settings;
        private BetterFootstepsComponent footstepsComponent;

        private float armorSoundVolume;
        private float footstepSoundVolume;
        private bool enableCustomFootsteps;

        // MOBILE: [Invoke] and the mod's own Init are gone - BetterAmbiencePort creates this component
        // and hands it the Mod, because on iOS the code is compiled into the app and MobilePortedMods
        // decides what runs. SetMod is called before the component is enabled, so Start() still finds it.
        public static void SetMod(Mod m) { mod = m; }

        private void Start()
        {
            //Heck hack
            if (!mod.GetSettings().GetValue<bool>("Better Footsteps", "enable"))
                return;

            // MOBILE: one line the log reader trusts. There is no audio in the simulator, so this is
            // the only evidence a sim pass can show that the mod took over footsteps at all.
            Debug.Log("[BetterAmbience] footsteps: replacing the built-in player footsteps");

            // MOBILE: past the "enable" setting and about to silence PlayerFootsteps, so this module
            // really is the one holding the player's footsteps. Immersive Footsteps reads this flag
            // instead of looking the mod up by GUID, so its compatibility box only appears when there
            // genuinely are two sets of footsteps running. BetterAmbiencePort's Bootstrap does not
            // even attach this component when Immersive Footsteps is installed, so with both mods on
            // the flag stays false and no box appears.
            BetterAmbiencePort.FootstepsActive = true;

            DisableBuiltInFootsteps();

            if (footstepsComponent == null)
                footstepsComponent = GameManager.Instance.PlayerObject.AddComponent<BetterFootstepsComponentPlayer>();

            ApplyFootsteps(footstepsComponent);

            mod.IsReady = true;

            LoadSettings(mod.GetSettings());

            //GameManager.OnEncounter += UpdateEnemyFootsteps;
            //PlayerEnterExit.OnTransitionDungeonInterior += OnTransitionDungeonInterior;
        }

        // MOBILE: UpdateEnemyFootsteps / ShouldEnemyHaveFootsteps and the two subclasses they fed
        // (BetterFootstepsComponentEnemy, BetterFootstepsComponentNPC) are NOT ported. Upstream never
        // ran them: both subscriptions above are commented out in the source, so no enemy or NPC has
        // ever carried a footstep component in this mod, on desktop or here. Porting them would have
        // meant one MonoBehaviour.Update per live entity; porting the dead code faithfully would have
        // meant the same cost for nothing. Player-only is what the mod actually is.

        private void LoadSettings(ModSettings settings)
        {
            enableCustomFootsteps = settings.GetValue<bool>("Better Footsteps", "enable");

            armorSoundVolume = settings.GetValue<float>("Better Footsteps", "armorVolume");
            footstepSoundVolume = settings.GetValue<float>("Better Footsteps", "footstepVolume");

            footstepsComponent.FootstepsArmor.SetVolume(0.4f * armorSoundVolume);
            footstepsComponent.FootstepSoundDungeon.SetVolume(0.3f * footstepSoundVolume);
            footstepsComponent.FootstepSoundBuilding.SetVolume(0.4f * footstepSoundVolume);
            footstepsComponent.FootstepSoundShallow.SetVolume(0.4f * footstepSoundVolume);
            footstepsComponent.FootstepSoundOutside.SetVolume(0.15f * footstepSoundVolume);
            footstepsComponent.FootstepSoundSubmerged.SetVolume(footstepSoundVolume);
            footstepsComponent.FootstepSoundSnow.SetVolume(footstepSoundVolume);
        }

        private void ApplyFootsteps(BetterFootstepsComponent footsteps)
        {
            footsteps.FootstepSoundBuilding.AddAudioClips(SoundUtility.TryImportAudioClips("sfx_footstep_wood", "wav", 4, false));
            footsteps.FootstepSoundBuilding.SetPitchRange(0.8f, 1.2f);

            footsteps.FootstepSoundDungeon.AddAudioClips(SoundUtility.TryImportAudioClips("sfx_footstep_stone", "wav", 4, false));
            footsteps.FootstepSoundDungeon.SetPitchRange(0.8f, 1.2f);

            footsteps.FootstepsArmor.AddAudioClips(SoundUtility.TryImportAudioClips("sfx_footstep_armor_light", "wav", 11, false));
            footsteps.FootstepsArmor.SetPitchRange(1f, 1.2f);

            footsteps.FootstepSoundOutside.AddAudioClips(SoundUtility.TryImportAudioClips("sfx_footstep_crunchy-grass", "wav", 4, false));
            footsteps.FootstepSoundOutside.SetPitchRange(0.8f, 1.2f);

            footsteps.FootstepSoundShallow.AddAudioClips(SoundUtility.TryImportAudioClips("sfx_footstep_water", "wav", 6, false));
            footsteps.FootstepSoundShallow.SetPitchRange(0.8f, 1.2f);

            footsteps.FootstepSoundSubmerged.AddSoundClip(SoundClips.SplashSmall);
            footsteps.FootstepSoundSubmerged.SetPitchRange(0.8f, 1.2f);

            footsteps.FootstepSoundSnow.AddSoundClip(SoundClips.PlayerFootstepSnow1);
            footsteps.FootstepSoundSnow.AddSoundClip(SoundClips.PlayerFootstepSnow2);
            footsteps.FootstepSoundSnow.SetPitchRange(0.8f, 1.2f);
        }

        private void DisableBuiltInFootsteps()
        {
            var oldFootsteps = GameManager.Instance.PlayerObject.GetComponent<PlayerFootsteps>();

            oldFootsteps.FootstepSoundBuilding1 = SoundClips.None;
            oldFootsteps.FootstepSoundBuilding2 = SoundClips.None;
            oldFootsteps.FootstepSoundDungeon1 = SoundClips.None;
            oldFootsteps.FootstepSoundDungeon1 = SoundClips.None;
            oldFootsteps.FootstepSoundOutside1 = SoundClips.None;
            oldFootsteps.FootstepSoundOutside1 = SoundClips.None;
            oldFootsteps.FootstepSoundShallow = SoundClips.None;
            oldFootsteps.FootstepSoundSnow1 = SoundClips.None;
            oldFootsteps.FootstepSoundSnow2 = SoundClips.None;
            oldFootsteps.FootstepSoundSubmerged = SoundClips.None;

        }

    }
}