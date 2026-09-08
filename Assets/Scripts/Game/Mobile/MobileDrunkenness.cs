// Project:         Daggerfall Unity - iOS Touch Input Layer
// License:         MIT License
//
// This port's own drunkenness: a 0-100 level that tavern drinks raise (less for a hardy
// character), that fades with time and faster in sleep, and that has three stages measured
// against the character's endurance - tipsy (a little slow), drunk (slow, clumsy, tiring, the
// view sways) and blind drunk (you pass out: hours lost, the level halved, a headache that keeps
// the penalties for an hour, and maybe a lighter purse if you dropped in a tavern's common room).
// Climates & Calories reads and saves the level through MobileTavernWindow.drunk and calls Tick()
// once per effect tick; nothing in here is taken from that mod.

using System;
using UnityEngine;
using DaggerfallConnect;
using DaggerfallWorkshop.Game.Entity;

namespace DaggerfallWorkshop.Game.Mobile
{
    public static class MobileDrunkenness
    {
        public enum Stage { Sober, Tipsy, Drunk, BlindDrunk }

        public const int Max = 100;
        public const int SoberPerTick = 2;
        public const int SoberPerTickAsleep = 6;
        public const int WaterRelief = 5;
        public const int HangoverMinutes = 60;

        /// <summary>The level, 0 (sober) to 100. Saved by Climates & Calories as its Drunk value.</summary>
        public static int Level
        {
            get { return level; }
            set { level = Mathf.Clamp(value, 0, Max); }
        }
        static int level;
        static uint hangoverUntil;          // classic game minutes; 0 = none
        static int flavourCooldown;

        // Engine tavern menu rows: ale, beer, mead, wine, then food.
        static readonly int[] alcoholByMenuIndex = { 8, 10, 15, 20 };

        /// <summary>Pure: what a drink from menu row <paramref name="menuIndex"/> adds. Endurance 100 halves it; never below 3 for a drink, 0 for food.</summary>
        public static int AlcoholFor(int menuIndex, int endurance)
        {
            if (menuIndex < 0 || menuIndex >= alcoholByMenuIndex.Length) return 0;
            float scale = 1f - Mathf.Clamp(endurance, 0, 100) / 200f;
            return Mathf.Max(3, Mathf.RoundToInt(alcoholByMenuIndex[menuIndex] * scale));
        }

        /// <summary>Pure: stage for a level against endurance. Thresholds: a third, two thirds, and endurance itself (capped at 95 so it is always reachable).</summary>
        public static Stage StageFor(int level, int endurance)
        {
            endurance = Mathf.Clamp(endurance, 10, 100);
            if (level > Mathf.Min(endurance, 95)) return Stage.BlindDrunk;
            if (level > endurance * 2 / 3) return Stage.Drunk;
            if (level > endurance / 3) return Stage.Tipsy;
            return Stage.Sober;
        }

        /// <summary>Pure: the level after one tick of sobering up.</summary>
        public static int Sobered(int level, bool asleep)
        {
            return Mathf.Max(0, level - (asleep ? SoberPerTickAsleep : SoberPerTick));
        }

        /// <summary>Pure: stat penalties for a stage - speed, then agility.</summary>
        public static void Penalties(Stage stage, out int speed, out int agility)
        {
            switch (stage)
            {
                case Stage.Tipsy: speed = -5; agility = 0; break;
                case Stage.Drunk: case Stage.BlindDrunk: speed = -15; agility = -10; break;
                default: speed = 0; agility = 0; break;
            }
        }

        /// <summary>Pure: gold lost when passing out in a common room - a tenth of the purse, at most 40, never more than is carried.</summary>
        public static int RobberyLoss(int gold)
        {
            return Mathf.Min(gold, Mathf.Min(40, gold / 10));
        }

        /// <summary>Pure: hours slept off when passing out.</summary>
        public static int PassOutHours(int roll0to3) { return 4 + Mathf.Clamp(roll0to3, 0, 3); }

        static readonly string[] tipsyLines = { "The ale sits warm in you.", "Your feet find the floor a little late.", "You hum a tune you do not remember learning." };
        static readonly string[] drunkLines = { "The room leans when you do not.", "Your hands are not quite where you left them.", "Everything is slightly too loud." };

        /// <summary>Called once per Climates & Calories tick: sober up, apply the stage's cost, pass out at the top.</summary>
        public static void Tick(PlayerEntity player)
        {
            if (player == null) return;
            uint now = DaggerfallUnity.Instance.WorldTime.DaggerfallDateTime.ToClassicDaggerfallTime();
            bool hungover = hangoverUntil != 0 && now < hangoverUntil;
            if (hangoverUntil != 0 && now >= hangoverUntil) hangoverUntil = 0;

            if (level <= 0 && !hungover) { MobileDrunkSway.Set(0f); return; }
            level = Sobered(level, player.IsResting);

            Stage stage = StageFor(level, player.Stats.LiveEndurance);
            if (hungover && stage < Stage.Tipsy) stage = Stage.Tipsy;      // the headache keeps the small penalty
            int speed, agility;
            Penalties(stage, out speed, out agility);
            if (speed != 0 || agility != 0)
            {
                int[] mods = new int[DaggerfallStats.Count];
                mods[(int)DFCareer.Stats.Speed] = speed;
                mods[(int)DFCareer.Stats.Agility] = agility;
                GameManager.Instance.PlayerEffectManager.MergeDirectStatMods(mods);
            }
            MobileDrunkSway.Set(stage >= Stage.Drunk ? 1.5f : 0f);

            if (stage == Stage.Drunk)
                player.DecreaseFatigue(3, true);
            if (stage == Stage.BlindDrunk && !player.IsResting)
            {
                PassOut(player, now);
                return;
            }
            if (--flavourCooldown <= 0 && stage >= Stage.Tipsy && !hungover)
            {
                flavourCooldown = stage == Stage.Drunk ? 6 : 12;
                string[] lines = stage == Stage.Drunk ? drunkLines : tipsyLines;
                DaggerfallUI.AddHUDText(lines[UnityEngine.Random.Range(0, lines.Length)], 2.5f);
            }
        }

        /// <summary>A drink of water takes the edge off.</summary>
        public static void Water() { Level -= WaterRelief; }

        static void PassOut(PlayerEntity player, uint now)
        {
            var fade = DaggerfallUI.Instance.FadeBehaviour;
            if (fade.FadeInProgress) return;
            int hours = PassOutHours(UnityEngine.Random.Range(0, 4));
            bool commonRoom = false;
            var enterExit = GameManager.Instance.PlayerEnterExit;
            if (enterExit.IsPlayerInsideBuilding && enterExit.BuildingType == DFLocation.BuildingTypes.Tavern)
            {
                int mapId = GameManager.Instance.PlayerGPS.CurrentLocation.MapTableData.MapId;
                commonRoom = player.GetRentedRoom(mapId, enterExit.Interior.EntryDoor.buildingKey) == null;
            }
            fade.SmashHUDToBlack();
            DaggerfallUnity.Instance.WorldTime.DaggerfallDateTime.RaiseTime(hours * 3600);
            level /= 2;
            player.SetFatigue(player.MaxFatigue, true);
            hangoverUntil = now + (uint)(hours * 60) + HangoverMinutes;
            ClimatesCalories.Sleep.sleepyCounter = Mathf.Max(0, ClimatesCalories.Sleep.sleepyCounter - 30);
            string text = "You pass out. " + hours + " hours later you come to, head pounding.";
            if (commonRoom && UnityEngine.Random.Range(0, 4) == 0)
            {
                int loss = RobberyLoss(player.GetGoldAmount());
                if (loss > 0) { player.DeductGoldAmount(loss); text += " Your purse is " + loss + " gold lighter."; }
            }
            fade.FadeHUDFromBlack(2f);
            DaggerfallUI.AddHUDText(text, 5f);
            Debug.Log("[Drunkenness] passed out for " + hours + " h" + (commonRoom ? " in a common room" : ""));
        }
    }

    /// <summary>A gentle roll of the view while drunk, applied after mouse-look each frame so it never accumulates.</summary>
    public class MobileDrunkSway : MonoBehaviour
    {
        static MobileDrunkSway instance;
        static float amplitude;

        public static void Set(float degrees)
        {
            amplitude = degrees;
            if (degrees > 0f && instance == null && GameManager.Instance.MainCamera != null)
                instance = GameManager.Instance.MainCamera.gameObject.AddComponent<MobileDrunkSway>();
        }

        void LateUpdate()
        {
            if (amplitude <= 0f || GameManager.IsGamePaused) return;
            float roll = Mathf.Sin(Time.time * 0.9f) * amplitude;
            float pitch = Mathf.Sin(Time.time * 0.6f + 1f) * amplitude * 0.4f;
            transform.Rotate(pitch, 0f, roll, Space.Self);
        }
    }
}
