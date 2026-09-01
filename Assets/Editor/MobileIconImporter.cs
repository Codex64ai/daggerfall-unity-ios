// Project:         Daggerfall Unity - iOS Touch Input Layer
// License:         MIT License
//
// Imports the touch HUD artwork and assigns it to the HUD it belongs to.
//
//   Menu: Tools > Daggerfall Mobile > Import Touch Icons
//
// Two jobs, both easy to get wrong by hand across 16 textures:
//
//   1. Import settings. Pixel art needs Point filtering and no compression. Unity's
//      defaults (bilinear + DXT) turn a crisp 224px icon into a blurry smear, and the
//      damage is invisible in the Project window - you only see it in game.
//   2. Assignment. Each sprite goes to a specific button, the joystick background and
//      knob, and the two combat-toggle states. Text labels are switched off, since the
//      artwork is wordless by design.
//
// Safe to re-run, and safe to run before the art exists - missing files are reported
// rather than silently skipped.
//
// Place in Assets/Editor/

using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;

namespace DaggerfallWorkshop.Game.Mobile.EditorTools
{
    public static class MobileIconImporter
    {
        const string artDir = "Assets/DaggerfallMobile/UI";

        /// <summary>sprite file name -> the GameObject in the HUD that should display it.
        /// Pair list rather than a dictionary: the joystick sprites map to BOTH sticks.</summary>
        static readonly KeyValuePair<string, string>[] targets =
        {
            new KeyValuePair<string, string>("btn_activate", "ActivateButton"),
            new KeyValuePair<string, string>("btn_jump", "JUMPButton"),
            new KeyValuePair<string, string>("btn_crouch", "CROUCHButton"),
            new KeyValuePair<string, string>("btn_weapon", "WEAPONButton"),
            new KeyValuePair<string, string>("btn_rest", "RESTButton"),
            new KeyValuePair<string, string>("btn_status", "STATUSButton"),
            new KeyValuePair<string, string>("btn_inventory", "INVENTORYButton"),
            new KeyValuePair<string, string>("btn_spell", "SPELLButton"),
            new KeyValuePair<string, string>("btn_map", "MAPButton"),
            new KeyValuePair<string, string>("btn_pause", "PAUSEButton"),
            new KeyValuePair<string, string>("btn_back", "MenuBackButton"),
            new KeyValuePair<string, string>("joystick_bg", "MoveJoystick"),
            new KeyValuePair<string, string>("joystick_knob", "Knob"),
            new KeyValuePair<string, string>("joystick_bg", "LookJoystick"),
            new KeyValuePair<string, string>("joystick_knob", "LookKnob"),
            new KeyValuePair<string, string>("btn_menu", "MenuToggle"),
            new KeyValuePair<string, string>("btn_sheet", "SHEETButton"),
            new KeyValuePair<string, string>("btn_automap", "AUTOMAPButton"),
            new KeyValuePair<string, string>("btn_transport", "TRANSPORTButton"),
            new KeyValuePair<string, string>("btn_usemagic", "USEMAGICButton"),
        };

        [MenuItem("Tools/Daggerfall Mobile/Import Touch Icons")]
        public static void ImportAndAssign()
        {
            var log = new StringBuilder();
            log.AppendLine("[MobileIconImporter]");

            int fixedUp = ApplyImportSettings(log);
            int assigned = AssignSprites(log);

            log.AppendLine();
            log.AppendLine(string.Format("import settings applied to {0} textures, {1} sprites assigned",
                                         fixedUp, assigned));
            Debug.Log(log.ToString());

            AssetDatabase.SaveAssets();
        }

        #region Import settings

        static int ApplyImportSettings(StringBuilder log)
        {
            if (!Directory.Exists(artDir))
            {
                log.AppendLine("  art folder missing: " + artDir);
                return 0;
            }

            int count = 0;
            foreach (string path in Directory.GetFiles(artDir, "*.png"))
            {
                string assetPath = path.Replace("\\", "/");
                TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (importer == null)
                    continue;

                bool cursor = Path.GetFileNameWithoutExtension(assetPath) == "cursor_arrow";

                // The cursor is drawn with GUI.DrawTexture in InputManager.OnGUI, not by a
                // UGUI Image, so it must stay a plain texture rather than become a Sprite.
                importer.textureType = cursor ? TextureImporterType.Default
                                              : TextureImporterType.Sprite;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.npotScale = TextureImporterNPOTScale.None;
                if (!cursor)
                    importer.spritePixelsPerUnit = 100f;

                TextureImporterSettings settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.filterMode = FilterMode.Point;      // crisp pixels, not bilinear mush
                settings.wrapMode = TextureWrapMode.Clamp;
                settings.mipmapEnabled = false;
                settings.alphaIsTransparency = true;
                importer.SetTextureSettings(settings);

                importer.SaveAndReimport();
                count++;
            }
            log.AppendLine("  Point filter + uncompressed applied to " + count + " textures");
            return count;
        }

        #endregion

        #region Assignment

        /// <summary>
        /// Feed the four interaction-mode icons into MobileGameArtIcon.
        ///
        /// Separate from the name->object table because this is one component holding FOUR
        /// sprites, swapped at runtime to show the current mode. Order matches
        /// PlayerActivateModes: Steal, Grab, Info, Talk. If any are missing the component
        /// falls back to Daggerfall's own MAIN01I0.IMG art, so the button always works.
        /// </summary>
        static int AssignInteractionModeSprites(GameObject canvas, StringBuilder log)
        {
            Transform t = FindDeep(canvas.transform, "InteractionModeButton");
            if (t == null)
                return 0;

            MobileGameArtIcon icon = t.GetComponent<MobileGameArtIcon>();
            if (icon == null)
                return 0;

            string[] names = { "btn_mode_steal", "btn_mode_grab", "btn_mode_info", "btn_mode_talk" };
            Sprite[] sprites = new Sprite[names.Length];
            int found = 0;

            for (int i = 0; i < names.Length; i++)
            {
                sprites[i] = LoadSprite(names[i]);
                if (sprites[i] != null)
                    found++;
                else
                    log.AppendLine("  missing mode sprite: " + names[i] + ".png (classic art will be used)");
            }

            if (found < names.Length)
                return 0;

            icon.modeSprites = sprites;
            log.AppendLine("  interaction mode icons -> InteractionModeButton (4 sprites)");
            return 1;
        }

        static int AssignSprites(StringBuilder log)
        {
            GameObject canvas = GameObject.Find("MobileCanvas");
            if (canvas == null)
            {
                log.AppendLine("  MobileCanvas not found - run Build Touch HUD first, " +
                               "or open Assets/Scenes/DaggerfallUnityGame.unity");
                return 0;
            }

            int assigned = 0;

            assigned += AssignInteractionModeSprites(canvas, log);

            foreach (KeyValuePair<string, string> pair in targets)
            {
                Sprite sprite = LoadSprite(pair.Key);
                if (sprite == null)
                {
                    log.AppendLine("  missing sprite: " + pair.Key + ".png");
                    continue;
                }

                Transform t = FindDeep(canvas.transform, pair.Value);
                if (t == null)
                {
                    log.AppendLine("  target object not found: " + pair.Value);
                    continue;
                }

                Image image = t.GetComponent<Image>();
                if (image == null)
                {
                    log.AppendLine("  no Image on: " + pair.Value);
                    continue;
                }

                image.sprite = sprite;
                image.color = Color.white;       // artwork carries its own shading
                EditorUtility.SetDirty(image);
                HideLabel(t, log);
                assigned++;
            }

            assigned += AssignCombatToggle(canvas.transform, log);

            return assigned;
        }

        static int AssignCombatToggle(Transform root, StringBuilder log)
        {
            Sprite on = LoadSprite("btn_combat_on");
            Sprite off = LoadSprite("btn_combat_off");
            if (on == null || off == null)
            {
                log.AppendLine("  combat toggle sprites missing - keeping colour tinting");
                return 0;
            }

            MobileInputController controller = Object.FindObjectOfType<MobileInputController>();
            if (controller == null)
            {
                log.AppendLine("  MobileInputController not found - combat sprites not assigned");
                return 0;
            }

            controller.combatOnSprite = on;
            controller.combatOffSprite = off;

            Transform toggle = FindDeep(root, "CombatToggle");
            if (toggle != null)
            {
                Image image = toggle.GetComponent<Image>();
                if (image != null)
                {
                    image.sprite = off;
                    image.color = Color.white;
                    EditorUtility.SetDirty(image);
                }
                HideLabel(toggle, log);
            }

            EditorUtility.SetDirty(controller);
            log.AppendLine("  combat toggle now swaps sprites instead of tinting");
            return 2;
        }

        /// <summary>The artwork is wordless, so the placeholder text labels are redundant.</summary>
        static void HideLabel(Transform target, StringBuilder log)
        {
            Transform label = target.Find("Label");
            if (label != null && label.gameObject.activeSelf)
            {
                label.gameObject.SetActive(false);
                EditorUtility.SetDirty(label.gameObject);
            }
        }

        static Sprite LoadSprite(string name)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>(artDir + "/" + name + ".png");
        }

        static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name)
                return root;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindDeep(root.GetChild(i), name);
                if (found != null)
                    return found;
            }
            return null;
        }

        #endregion
    }
}
