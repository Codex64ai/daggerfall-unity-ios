// Project:         Daggerfall Unity - iOS Touch Input Layer
// License:         MIT License
//
// MOBILE: Better Ambience (joshcamas/daggerfall-unity-mods @ c59e2aa9, MIT, (c) 2020 Josh Steinhauer)
// started by MobilePortedMods rather than by DFU's [Invoke] loader.
//
// Upstream is FIVE independent [Invoke] entry points in one .dfmod - Better Footsteps, Camera Shake,
// Foggy Dungeons, Better Rain and Dungeon Reverb - each creating its own GameObject at Start. This
// port has one, for three reasons: MobilePortedMods hands a mod ONE Init; a throw from any of the
// five used to cost the other four their start; and two of the five are not ported at all.
//
// WHAT IS NOT PORTED, and why:
//   ReverbMod.cs      - it adds an AudioReverbZone to the player with a Cave/Stoneroom/Quarry preset
//                       by a settings dial. This port already has MobileAmbience, whose AudioReverb
//                       switch puts an AudioReverbFilter on the AudioListener and picks a preset per
//                       SPACE (dungeon by type, building interior, exterior), not per dial. Two
//                       reverbs on one listener stack audibly, so shipping both would be a bug with
//                       a settings screen in front of it.
//   BetterRainMod.cs  - it raises the rain particle emission rate to 2000, and 4000 in a storm. The
//                       atmosphere spec puts weather particle rework out of scope, and that is a
//                       large per-frame bill on a phone for an effect nobody asked for.
//   DungeonSoundsMod.cs - an empty Unity template component: Start() and Update() with no body. It
//                       is in the author's manifest; there is nothing in it to run.
//   BetterFootstepsComponentEnemy.cs / ...NPC.cs - never attached by anything. The two lines that
//                       would have attached them are commented out in upstream's own source, so no
//                       enemy has ever carried one. See the note in BetterFootstepsMod.
//
// The bundle (better-ambience.dfmod) ships the 50 sfx_footstep_* clips and modsettings.json.
// AmbientRaining.wav is excluded by mods.json: it is 63% of the mod's bytes and no source file in
// the repo references it.
//
// Place in Assets/Scripts/Game/Mobile/Ports/BetterAmbience/

using UnityEngine;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Utility.ModSupport;

namespace SpellcastStudios
{
    public static class BetterAmbiencePort
    {
        /// <summary>The mod's own title, as it appears in the launcher and in the bundle manifest.</summary>
        public const string Title = "Better Ambience";

        /// <summary>True once Init created the driver. What MobilePortedMods' four-argument StartOne asks.</summary>
        public static bool Installed { get; private set; }

        static GameObject driver;

        /// <summary>
        /// The one Init. Creates a single GameObject carrying the three ported modules, in the order
        /// upstream's five [Invoke]s happened to run: footsteps, camera shake, dungeon fog. Each module
        /// still does its own work in its own Start, so a module that throws is contained by Unity's
        /// per-component Start rather than taking the others with it - which is more containment than
        /// upstream had, not less.
        /// </summary>
        public static void Init(InitParams initParams)
        {
            if (Installed)
                return;

            Mod mod = initParams.Mod;
            if (mod == null)
            {
                Debug.Log("[BetterAmbience] not available: no mod entry (the bundle carries the clips and the settings)");
                return;
            }

            BetterFootsteps.BetterFootstepsMod.SetMod(mod);
            CameraShake.CameraShakeMod.SetMod(mod);
            FoggyDungeons.FoggyDungeonsMod.SetMod(mod);

            driver = new GameObject("Better Ambience");
            Object.DontDestroyOnLoad(driver);
            driver.AddComponent<Bootstrap>();

            mod.IsReady = true;
            Installed = true;
            Debug.Log("[BetterAmbience] started: footsteps, camera shake, dungeon fog");
        }

        /// <summary>
        /// MOBILE: waits for the scene before adding the three modules. Upstream's [Invoke] fires at
        /// StateTypes.Start and its components then reach straight for GameManager.Instance.PlayerObject
        /// and .MainCamera in their own Start. MobilePortedMods starts the compiled-in mods at the title
        /// on a PLAYER build, where those are not guaranteed to exist yet - the same reason Dynamic
        /// Skies' start is deferred there. So the modules are added only once the two objects they need
        /// are in the scene. Costs one Update per frame until then, and none afterwards: the bootstrap
        /// destroys itself.
        /// </summary>
        class Bootstrap : MonoBehaviour
        {
            void Update()
            {
                if (!GameManager.HasInstance)
                    return;
                GameManager gm = GameManager.Instance;
                if (gm.PlayerObject == null || gm.MainCamera == null || gm.PlayerEntityBehaviour == null)
                    return;

                gameObject.AddComponent<BetterFootsteps.BetterFootstepsMod>();
                gameObject.AddComponent<CameraShake.CameraShakeMod>();
                gameObject.AddComponent<FoggyDungeons.FoggyDungeonsMod>();
                Destroy(this);
            }
        }
    }
}
