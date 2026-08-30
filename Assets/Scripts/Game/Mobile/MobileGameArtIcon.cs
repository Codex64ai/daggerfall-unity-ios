// Project:         Daggerfall Unity - iOS Touch Input Layer
// License:         MIT License
//
// Notes:
//   Dresses a touch button in Daggerfall's OWN artwork, loaded from the player's arena2 at
//   runtime, instead of a hand-drawn imitation.
//
//   Two buttons needed icons that had to look native: the character sheet, and the
//   interaction-mode switch. Both already exist as classic art:
//
//     PlayerPortrait   - the paper-doll head, exactly what HUDLarge shows in the bar and
//                        what classic Daggerfall used as the character-sheet button.
//     InteractionMode  - the four mode icons in MAIN01I0.IMG (steal / grab / info / talk),
//                        the same subrects HUDLarge draws, so the button always SHOWS the
//                        mode it will act on.
//
//   Better than drawing new icons in two ways: the style match is exact rather than
//   approximate, and nothing is redistributed - the art is read from the player's own game
//   files, the same as every other texture in the game.
//
//   The portrait also tracks the player: race, gender, face index and any racial override
//   (vampirism, lycanthropy) all change it, so it is refreshed rather than cached forever.
//

using UnityEngine;
using UnityEngine.UI;
using DaggerfallConnect;
using DaggerfallConnect.Arena2;
using DaggerfallConnect.Utility;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.MagicAndEffects.MagicEffects;
using DaggerfallWorkshop.Utility;

namespace DaggerfallWorkshop.Game.Mobile
{
    [RequireComponent(typeof(Image))]
    public class MobileGameArtIcon : MonoBehaviour
    {
        public enum ArtSource
        {
            PlayerPortrait,
            InteractionMode,
        }

        const string interactionModesFilename = "MAIN01I0.IMG";

        // Same subrects HUDLarge uses, against the same native size.
        static readonly Rect stealSubrect = new Rect(0, 0, 47, 23);
        static readonly Rect talkSubrect = new Rect(0, 23, 47, 23);
        static readonly Rect grabSubrect = new Rect(0, 46, 47, 23);
        static readonly Rect infoSubrect = new Rect(0, 69, 47, 23);
        static readonly DFSize interactionModesSize = new DFSize(47, 92);

        public ArtSource source = ArtSource.PlayerPortrait;

        [Tooltip("Custom mode icons in enum order: Steal, Grab, Info, Talk. Assigned by " +
                 "MobileIconImporter from btn_mode_*.png. When present these are used " +
                 "instead of the classic MAIN01I0.IMG art, whose stone background does not " +
                 "belong on a transparent HUD button. Leave empty to fall back to the " +
                 "classic art.")]
        public Sprite[] modeSprites;

        [Tooltip("Seconds between refreshes. The portrait changes with racial overrides and " +
                 "the mode icon with the current mode, so neither can be loaded once and kept.")]
        public float refreshInterval = 0.25f;

        Image image;
        float nextRefresh;

        // Interaction-mode art is cut once and kept - it comes from a fixed IMG.
        static Texture2D stealTexture, grabTexture, infoTexture, talkTexture;
        static bool modeArtLoaded;

        // Portrait art is keyed so a changed face reloads but an unchanged one does not.
        Texture2D portraitTexture;
        string portraitKey;

        void Awake()
        {
            image = GetComponent<Image>();
        }

        void OnEnable()
        {
            nextRefresh = 0f;
        }

        void Update()
        {
            if (Time.unscaledTime < nextRefresh)
                return;

            nextRefresh = Time.unscaledTime + refreshInterval;
            Refresh();
        }

        void Refresh()
        {
            if (image == null || !GameManager.HasInstance)
                return;

            // Custom mode art wins: the classic icons carry the bar's stone background,
            // which reads as a panel behind a button that should be a bare glyph.
            if (source == ArtSource.InteractionMode && modeSprites != null && modeSprites.Length >= 4)
            {
                Sprite want = modeSprites[ModeIndex()];
                if (want != null)
                {
                    if (image.sprite != want)
                    {
                        image.sprite = want;
                        image.color = Color.white;
                    }
                    return;
                }
            }

            Texture2D art = (source == ArtSource.InteractionMode)
                ? GetInteractionModeTexture()
                : GetPortraitTexture();

            if (art == null)
                return;

            // Only rebuild the sprite when the texture actually changed - Sprite.Create
            // allocates, and this runs on a timer.
            if (image.sprite != null && image.sprite.texture == art)
                return;

            image.sprite = Sprite.Create(art, new Rect(0, 0, art.width, art.height),
                                         new Vector2(0.5f, 0.5f));
            image.color = Color.white;      // the placeholder tint would darken real art
        }

        /// <summary>Index into modeSprites, matching PlayerActivateModes' own order.</summary>
        static int ModeIndex()
        {
            switch (GameManager.Instance.PlayerActivate.CurrentMode)
            {
                case PlayerActivateModes.Steal: return 0;
                case PlayerActivateModes.Grab: return 1;
                case PlayerActivateModes.Info: return 2;
                case PlayerActivateModes.Talk: return 3;
            }
            return 1;
        }

        static Texture2D GetInteractionModeTexture()
        {
            if (!modeArtLoaded)
            {
                try
                {
                    Texture2D sheet = ImageReader.GetTexture(interactionModesFilename);
                    if (sheet == null)
                        return null;

                    stealTexture = ImageReader.GetSubTexture(sheet, stealSubrect, interactionModesSize);
                    talkTexture = ImageReader.GetSubTexture(sheet, talkSubrect, interactionModesSize);
                    grabTexture = ImageReader.GetSubTexture(sheet, grabSubrect, interactionModesSize);
                    infoTexture = ImageReader.GetSubTexture(sheet, infoSubrect, interactionModesSize);
                    modeArtLoaded = true;
                }
                catch (System.Exception ex)
                {
                    modeArtLoaded = true;       // do not retry every tick
                    Debug.LogWarning("[MobileGameArtIcon] could not load mode art: " + ex.Message);
                    return null;
                }
            }

            switch (GameManager.Instance.PlayerActivate.CurrentMode)
            {
                case PlayerActivateModes.Steal: return stealTexture;
                case PlayerActivateModes.Grab: return grabTexture;
                case PlayerActivateModes.Info: return infoTexture;
                case PlayerActivateModes.Talk: return talkTexture;
            }

            return grabTexture;
        }

        Texture2D GetPortraitTexture()
        {
            PlayerEntity player = GameManager.Instance.PlayerEntity;
            if (player == null || player.RaceTemplate == null)
                return null;

            try
            {
                // Racial overrides (vampire, werewolf) replace the head entirely.
                RacialOverrideEffect racialOverride =
                    GameManager.Instance.PlayerEffectManager.GetRacialOverrideEffect();

                ImageData head;
                string key;

                if (racialOverride != null &&
                    racialOverride.GetCustomHeadImageData(player, out head))
                {
                    key = "override:" + racialOverride.GetType().Name;
                }
                else
                {
                    key = string.Format("{0}:{1}:{2}", player.RaceTemplate.ID, player.Gender,
                                        player.FaceIndex);

                    if (key == portraitKey && portraitTexture != null)
                        return portraitTexture;

                    head = ImageReader.GetImageData(
                        player.Gender == Genders.Female
                            ? player.RaceTemplate.PaperDollHeadsFemale
                            : player.RaceTemplate.PaperDollHeadsMale,
                        player.FaceIndex, 0, true);
                }

                if (key == portraitKey && portraitTexture != null)
                    return portraitTexture;

                portraitKey = key;
                portraitTexture = head.texture;
                return portraitTexture;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[MobileGameArtIcon] could not load portrait: " + ex.Message);
                return null;
            }
        }
    }
}
