// Project:         Daggerfall Unity - iOS Touch Input Layer
// License:         MIT License
//
// Editor utility: constructs the entire touch HUD, wires every reference and
// button action, and reports what it did. Replaces the manual canvas setup.
//
// Menu: Tools > Daggerfall Mobile > Build Touch HUD
//
// Safe to re-run: it deletes any previously generated MobileCanvas/MobileInput
// objects first, so iterating on layout is one menu click.
//
// Place in Assets/Editor/

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEditor;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Mobile;

namespace DaggerfallWorkshop.Game.Mobile.EditorTools
{
    public static class MobileHudBuilder
    {
        const string canvasName = "MobileCanvas";
        const string controllerName = "MobileInput";

        // Reference resolution the layout numbers below are authored against.
        static readonly Vector2 referenceResolution = new Vector2(1920f, 1080f);

        [MenuItem("Tools/Daggerfall Mobile/Build Touch HUD")]
        public static void Build()
        {
            // ---------------------------------------------------------- clean slate
            DestroyExisting(canvasName);
            DestroyExisting(controllerName);

            // ---------------------------------------------------------- event system
            EventSystem eventSystem = Object.FindObjectOfType<EventSystem>();
            if (eventSystem == null)
            {
                GameObject es = new GameObject("EventSystem",
                    typeof(EventSystem), typeof(StandaloneInputModule));
                Undo.RegisterCreatedObjectUndo(es, "Create EventSystem");
                eventSystem = es.GetComponent<EventSystem>();
            }

            // Phantom-mouse kill switch: iPadOS trackpads present a held mouse button and
            // mousePosition parks at the last touch, so UGUI's mouse path re-pressed
            // whatever was last tapped. Deny the mouse on touch devices.
            StandaloneInputModule module = eventSystem.GetComponent<StandaloneInputModule>();
            if (module != null)
            {
                MobileUGUIInput uguiInput = eventSystem.GetComponent<MobileUGUIInput>();
                if (uguiInput == null)
                    uguiInput = eventSystem.gameObject.AddComponent<MobileUGUIInput>();
                module.inputOverride = uguiInput;
            }

            // ---------------------------------------------------------- canvas
            GameObject canvasGo = new GameObject(canvasName,
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Undo.RegisterCreatedObjectUndo(canvasGo, "Create MobileCanvas");

            Canvas canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;

            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = referenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            // ---------------------------------------------------------- layers
            GameObject gameplayLayer = CreateFullScreenChild(canvasGo, "GameplayLayer");
            gameplayLayer.AddComponent<SafeAreaPanel>();
            CanvasGroup hudGroup = gameplayLayer.AddComponent<CanvasGroup>();

            GameObject menuLayer = CreateFullScreenChild(canvasGo, "MenuLayer");
            menuLayer.AddComponent<SafeAreaPanel>();

            // Order inside GameplayLayer IS raycast priority: first child is bottom-most,
            // so the look zone must be created before the joystick and buttons.

            // ---------------------------------------------------------- look zone
            GameObject lookZoneGo = CreateFullScreenChild(gameplayLayer, "LookZone");
            Image lookImage = lookZoneGo.AddComponent<Image>();
            lookImage.color = new Color(0f, 0f, 0f, 0f);   // invisible but still raycastable
            lookImage.raycastTarget = true;
            TouchLookZone lookZone = lookZoneGo.AddComponent<TouchLookZone>();

            // ---------------------------------------------------------- joystick
            GameObject joyGo = CreateChild(gameplayLayer, "MoveJoystick");
            RectTransform joyRect = (RectTransform)joyGo.transform;
            Anchor(joyRect, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0.5f, 0.5f));
            joyRect.anchoredPosition = new Vector2(260f, 240f);
            joyRect.sizeDelta = new Vector2(340f, 340f);
            Image joyBg = joyGo.AddComponent<Image>();
            joyBg.color = new Color(1f, 1f, 1f, 0.35f);
            joyBg.raycastTarget = true;

            GameObject knobGo = CreateChild(joyGo, "Knob");
            RectTransform knobRect = (RectTransform)knobGo.transform;
            Anchor(knobRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            knobRect.anchoredPosition = Vector2.zero;
            knobRect.sizeDelta = new Vector2(140f, 140f);
            Image knobImg = knobGo.AddComponent<Image>();
            knobImg.color = new Color(1f, 1f, 1f, 0.85f);
            // Critical: the knob must not eat drags aimed at the background.
            knobImg.raycastTarget = false;

            VirtualJoystick joystick = joyGo.AddComponent<VirtualJoystick>();
            joystick.handle = knobRect;
            // Left 40% x bottom 70% of the screen is movement territory: any grab there
            // snaps this stick under the thumb. Pairs with TouchLookZone.ignoreLeftFraction
            // so an off-ring grab can never yank the camera instead of walking.
            joystick.screenClaimRegion = new Rect(0f, 0f, 0.40f, 0.70f);

            // ---------------------------------------------------------- look joystick
            // Twin-stick: right stick turns the camera at a rate set by deflection.
            // The full-screen LookZone stays underneath for drag-look and swipe attacks
            // on any empty screen space.
            GameObject lookJoyGo = CreateChild(gameplayLayer, "LookJoystick");
            RectTransform lookJoyRect = (RectTransform)lookJoyGo.transform;
            Anchor(lookJoyRect, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0.5f));
            lookJoyRect.anchoredPosition = new Vector2(-260f, 240f);
            lookJoyRect.sizeDelta = new Vector2(340f, 340f);
            Image lookJoyBg = lookJoyGo.AddComponent<Image>();
            lookJoyBg.color = new Color(1f, 1f, 1f, 0.35f);
            lookJoyBg.raycastTarget = true;

            GameObject lookKnobGo = CreateChild(lookJoyGo, "LookKnob");
            RectTransform lookKnobRect = (RectTransform)lookKnobGo.transform;
            Anchor(lookKnobRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            lookKnobRect.anchoredPosition = Vector2.zero;
            lookKnobRect.sizeDelta = new Vector2(140f, 140f);
            Image lookKnobImg = lookKnobGo.AddComponent<Image>();
            lookKnobImg.color = new Color(1f, 1f, 1f, 0.85f);
            lookKnobImg.raycastTarget = false;

            VirtualJoystick lookJoystick = lookJoyGo.AddComponent<VirtualJoystick>();
            lookJoystick.handle = lookKnobRect;
            // Same territory treatment as the move stick - on device the exact-rect hit
            // test proved unreliable (left stick only worked once IT had a region), so
            // grabs in the lower-right quadrant snap this stick under the thumb. Upper
            // screen stays with the drag-look/swipe zone.
            lookJoystick.screenClaimRegion = new Rect(0.60f, 0f, 0.40f, 0.60f);

            // ---------------------------------------------------------- activate button
            GameObject activateGo = CreateActionButton(
                gameplayLayer, "ActivateButton", "ACTIVATE",
                InputManager.Actions.ActivateCenterObject, MobileActionButton.PressMode.Tap,
                new Vector2(1f, 0f), new Vector2(-60f, 470f), new Vector2(220f, 220f));

            // ---------------------------------------------------------- primary bank
            // Only what is used moment to moment stays on screen. Ten always-visible
            // buttons crowded the look/swipe area, and map/status/rest are occasional.
            // Every action icon is its OWN layout element - no GridLayoutGroup. A grid made
            // the icons one draggable block (device request: move each icon on its own) and
            // its reflow shuffled the survivors whenever classic mode hid one. The container
            // is a full-screen pass-through rect kept only for hierarchy tidiness.
            GameObject bankGo = CreateFullScreenChild(gameplayLayer, "PrimaryBank");

            var primary = new List<(string label, InputManager.Actions action, MobileActionButton.PressMode mode)>
            {
                ("WEAPON", InputManager.Actions.ReadyWeapon, MobileActionButton.PressMode.Tap),
                ("SPELL",  InputManager.Actions.CastSpell,   MobileActionButton.PressMode.Tap),
                ("JUMP",   InputManager.Actions.Jump,        MobileActionButton.PressMode.Tap),
                ("CROUCH", InputManager.Actions.Crouch,      MobileActionButton.PressMode.Toggle),
            };
            var primaryRects = new Dictionary<string, RectTransform>();
            foreach (var entry in primary)
            {
                GameObject b = CreateActionButton(bankGo, entry.label + "Button", entry.label,
                    entry.action, entry.mode,
                    new Vector2(1f, 0f), Vector2.zero, new Vector2(150f, 150f));
                primaryRects[entry.label] = (RectTransform)b.transform;
            }

            // ---------------------------------------------------------- menu drawer
            GameObject menuToggleGo = CreateChild(gameplayLayer, "MenuToggle");
            RectTransform menuToggleRect = (RectTransform)menuToggleGo.transform;
            Anchor(menuToggleRect, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f));
            menuToggleRect.anchoredPosition = new Vector2(-720f, 60f);
            menuToggleRect.sizeDelta = new Vector2(150f, 150f);
            Image menuToggleImg = menuToggleGo.AddComponent<Image>();
            menuToggleImg.color = new Color(1f, 1f, 1f, 0.30f);
            Button menuToggleButton = menuToggleGo.AddComponent<Button>();
            AddLabel(menuToggleGo, "MENU");

            // Secondary buttons live in a full-screen pass-through container so the drawer
            // can still show/hide them as one unit, while each icon stays an independent
            // layout element the player can drag on its own.
            GameObject drawerGo = CreateFullScreenChild(gameplayLayer, "SecondaryBank");

            MobileButtonDrawer drawer = gameplayLayer.AddComponent<MobileButtonDrawer>();
            drawer.panel = drawerGo;
            drawer.toggleGraphic = menuToggleImg;

            var secondary = new List<(string label, InputManager.Actions action)>
            {
                // Escape during gameplay opens the pause options window (Save/Load/Exit) -
                // without this the save menu is unreachable from the touch HUD.
                ("PAUSE",     InputManager.Actions.Escape),
                ("INVENTORY", InputManager.Actions.Inventory),
                // Distinct from STATUS (which is the smaller status popup): this is the
                // character sheet proper - skills, level, equipment - and it also carries
                // Daggerfall's own Logbook button, so it is the route to the quest journal
                // too. Touch had no way to reach either.
                ("SHEET",     InputManager.Actions.CharacterSheet),
                ("STATUS",    InputManager.Actions.Status),
                ("MAP",       InputManager.Actions.TravelMap),
                // The DUNGEON/interior automap - a different action and a different window
                // from the travel map above (dfuiOpenAutomap vs the travel map screen).
                // Touch had only the travel map, which left dungeon navigation blind in
                // fullscreen mode; the classic bar's map icon had been covering for it.
                ("AUTOMAP",   InputManager.Actions.AutoMap),
                ("REST",      InputManager.Actions.Rest),
            };
            var secondaryRects = new Dictionary<string, RectTransform>();
            foreach (var entry in secondary)
            {
                GameObject b = CreateActionButton(drawerGo, entry.label + "Button", entry.label,
                    entry.action, MobileActionButton.PressMode.Tap,
                    new Vector2(1f, 0f), Vector2.zero, new Vector2(150f, 150f));
                b.GetComponent<MobileActionButton>().ownerDrawer = drawer;
                secondaryRects[entry.label] = (RectTransform)b.transform;
            }

            // ------------------------------------------------ interaction mode cycle
            // Steal is how locks are picked and Info is how things are examined; touch could
            // reach neither, because the mode switch only existed on the classic bar and the
            // controller d-pad. Locked doors were simply unopenable by touch.
            // Lives in the action row, not the drawer: switching to Steal to pick a lock or
            // Info to examine something is a gameplay move made mid-scene, not a menu trip.
            GameObject modeGo = CreateActionButton(bankGo, "InteractionModeButton", "",
                InputManager.Actions.GrabMode, MobileActionButton.PressMode.Tap,
                new Vector2(1f, 0f), Vector2.zero, new Vector2(150f, 150f));
            MobileActionButton modeButton = modeGo.GetComponent<MobileActionButton>();
            modeButton.cyclesInteractionMode = true;

            // Wears Daggerfall's own mode art from MAIN01I0.IMG, so it always shows the
            // mode it will act on - and matches the classic bar exactly instead of
            // approximating it.
            modeGo.AddComponent<MobileGameArtIcon>().source =
                MobileGameArtIcon.ArtSource.InteractionMode;

            // The character sheet button wears the player's own paper-doll head, which is
            // what classic Daggerfall used as the character-sheet button.
            secondaryRects["SHEET"].gameObject.AddComponent<MobileGameArtIcon>().source =
                MobileGameArtIcon.ArtSource.PlayerPortrait;

            UnityEditor.Events.UnityEventTools.AddPersistentListener(
                menuToggleButton.onClick, drawer.Toggle);

            // ---------------------------------------------------------- combat toggle
            GameObject combatGo = CreateChild(gameplayLayer, "CombatToggle");
            RectTransform combatRect = (RectTransform)combatGo.transform;
            Anchor(combatRect, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f));
            combatRect.anchoredPosition = new Vector2(-300f, 480f);
            combatRect.sizeDelta = new Vector2(170f, 170f);
            Image combatImg = combatGo.AddComponent<Image>();
            combatImg.color = new Color(1f, 1f, 1f, 0.30f);
            Button combatButton = combatGo.AddComponent<Button>();
            AddLabel(combatGo, "COMBAT");

            // NO RETICLE OF OUR OWN. Daggerfall already draws a crosshair (HUDCrosshair,
            // controlled by the Crosshair setting), and ours was a second one - visibly a
            // separate square BELOW the real cross, because HUDCrosshair centres itself in
            // the viewport ABOVE the classic bar while a UGUI overlay element centres on
            // the whole screen. Two crosshairs, and the wrong one was untextured.

            // ---------------------------------------------------------- menu back button
            CreateActionButton(menuLayer, "MenuBackButton", "BACK",
                InputManager.Actions.Escape, MobileActionButton.PressMode.Tap,
                new Vector2(0f, 1f), new Vector2(120f, -90f), new Vector2(160f, 110f));

            // ---------------------------------------------------------- settings panel
            // No gear on the HUD any more: Mobile Settings is a button in the pause menu
            // (MobilePauseOptionsWindow), reachable by touch, mouse, keyboard and pad alike.
            // Parented to the canvas, not GameplayLayer, so it stays visible while the
            // gameplay HUD is hidden behind the classic menus it is opened from.
            GameObject panelHost = CreateFullScreenChild(canvasGo, "SettingsHost");
            MobileSettingsPanel settingsPanel = panelHost.AddComponent<MobileSettingsPanel>();
            MobileLayoutEditor layoutEditor = panelHost.AddComponent<MobileLayoutEditor>();

            // ---------------------------------------------------------- controller
            GameObject controllerGo = new GameObject(controllerName);
            Undo.RegisterCreatedObjectUndo(controllerGo, "Create MobileInput");

            MobileInputController controller = controllerGo.AddComponent<MobileInputController>();
            VirtualMouseCursor cursor = controllerGo.AddComponent<VirtualMouseCursor>();

            // Lives on the controller object so it survives with it and is drawn whether or
            // not the touch HUD is visible - a probe that vanished the moment a controller
            // connected would be useless.
            //
            // Armed by SERIALIZING the field, not by a scripting define. A
            // PlayerSettings.SetScriptingDefineSymbolsForGroup call from one batchmode
            // session did not reach the player script compilation in the next one - the
            // define landed in ProjectSettings.asset and was still ignored, silently
            // producing a probe build with the probe switched off. A serialized field is
            // plain scene data: it cannot be quietly dropped, and it can be verified by
            // reading the scene file back.
            MobileControllerProbe probe = controllerGo.AddComponent<MobileControllerProbe>();
            probe.active = System.Environment.GetEnvironmentVariable("DFU_IOS_PROBE") == "1";
            if (probe.active)
                Debug.LogWarning("[MobileHudBuilder] *** CONTROLLER PROBE ARMED (DFU_IOS_PROBE=1) *** " +
                                 "this scene is a diagnostic build - do NOT commit it or ship it.");

            controller.moveJoystick = joystick;
            controller.lookJoystick = lookJoystick;
            controller.lookZone = lookZone;
            controller.virtualMouse = cursor;
            controller.gameplayLayer = gameplayLayer;
            controller.menuLayer = menuLayer;
            controller.combatToggleGraphic = combatImg;

            // ---------------------------------------------------------- inch-based layout
            // Sizes in PHYSICAL inches so controls stay thumb-sized from an iPhone mini to
            // a 13in iPad. Apple's minimum touch target is 44pt (~0.29in); these clear it.
            //
            // These defaults are Ikram's device-tuned layout (2026-08-23 screenshot,
            // reconstructed by pixel measurement at 264ppi): the action bank runs along the
            // bottom centre with COMBAT at its left and MENU at its right, ACTIVATE sits
            // between the row and the look stick, and the drawer opens as a compact column
            // tucked against the right edge. Controls are a step smaller than the original
            // guesses - play showed the big sizes crowded the look/swipe area - but every
            // target still clears 0.45in. Margins are from each element's own anchor corner;
            // note action buttons pivot at that corner while the sticks pivot at centre.
            MobileHudLayout layout = gameplayLayer.AddComponent<MobileHudLayout>();
            layout.canvas = canvas;
            layout.elements = new[]
            {
                // Classic-mode values are Ikram's device-tuned minimal layout
                // (2026-08-23 screenshot, reconstructed by pixel measurement): sticks
                // smaller and tucked into the corners above the bar, ACTIVATE with CROUCH
                // and JUMP by the right thumb, MENU beside them, TUNE and MAP stacked at
                // the top right. Everything else defaults hidden there - the bar itself
                // provides inventory, map, rest, pause, weapon; SPELL, STATUS and COMBAT
                // are a Hide/Show away for players who want them.
                Elem("Joystick",  joyRect,      1.10f, 0f, new Vector2(1.35f, 1.30f),
                     classicMarginIn: new Vector2(0.78f, 0.76f), classicWidthIn: 0.80f),
                Elem("Knob",      knobRect,     0.45f, 0f, Vector2.zero, false,
                     classicWidthIn: 0.33f),
                Elem("LookJoystick", lookJoyRect, 1.10f, 0f, new Vector2(1.80f, 1.15f),
                     classicMarginIn: new Vector2(0.58f, 0.78f), classicWidthIn: 0.80f),
                Elem("LookKnob",  lookKnobRect, 0.45f, 0f, Vector2.zero, false,
                     classicWidthIn: 0.33f),
                Elem("Activate",  (RectTransform)activateGo.transform, 0.80f, 0f, new Vector2(2.40f, 0.65f),
                     classicMarginIn: new Vector2(2.64f, 0.62f), classicWidthIn: 0.50f),
                Elem("Combat",    combatRect,   0.55f, 0f, new Vector2(5.95f, 0.10f),
                     classicHidden: true),
                Elem("MenuToggle", menuToggleRect, 0.50f, 0f, new Vector2(2.60f, 0.10f),
                     classicMarginIn: new Vector2(3.27f, 0.00f), classicWidthIn: 0.40f),
                // One element per icon so each is draggable, hideable and scalable on its
                // own. Defaults reproduce the former grid rows exactly: the bottom-centre
                // action row steps 0.57in (0.50 cell + 0.07 gap) leftward from WEAPON, the
                // drawer column steps 0.53in (0.48 + 0.05) upward from PAUSE.
                Elem("Weapon",    primaryRects["WEAPON"],   0.50f, 0f, new Vector2(3.35f, 0.10f),
                     classicHidden: true),
                Elem("Spell",     primaryRects["SPELL"],    0.50f, 0f, new Vector2(3.92f, 0.10f),
                     classicHidden: true),
                Elem("Jump",      primaryRects["JUMP"],     0.50f, 0f, new Vector2(4.49f, 0.10f),
                     classicMarginIn: new Vector2(2.07f, 0.05f), classicWidthIn: 0.45f),
                Elem("Crouch",    primaryRects["CROUCH"],   0.50f, 0f, new Vector2(5.06f, 0.10f),
                     classicMarginIn: new Vector2(2.68f, 0.00f), classicWidthIn: 0.45f),
                // Interaction mode sits with the action row. Hidden by default in classic
                // mode - the bar carries its own mode switcher.
                Elem("Mode",      (RectTransform)modeGo.transform, 0.50f, 0f,
                     new Vector2(5.63f, 0.10f), classicHidden: true),
                Elem("Pause",     secondaryRects["PAUSE"],     0.48f, 0f, new Vector2(0.20f, 1.75f),
                     classicHidden: true),
                Elem("Inventory", secondaryRects["INVENTORY"], 0.48f, 0f, new Vector2(0.20f, 2.28f),
                     classicHidden: true),
                // The bar opens the character sheet by its portrait, so this is redundant
                // there - but it is the only route to the sheet (and its Logbook) otherwise.
                Elem("Sheet",     secondaryRects["SHEET"],     0.48f, 0f, new Vector2(0.20f, 2.81f),
                     classicHidden: true),
                Elem("Status",    secondaryRects["STATUS"],    0.48f, 0f, new Vector2(0.20f, 3.34f),
                     classicHidden: true),
                Elem("Map",       secondaryRects["MAP"],       0.48f, 0f, new Vector2(0.20f, 3.87f),
                     classicMarginIn: new Vector2(0.26f, 3.61f), classicWidthIn: 0.40f),
                // Sits directly above the travel map: two map buttons together is the
                // discoverable arrangement. Hidden in classic mode, where the bar's own
                // map icon already opens the automap indoors.
                Elem("Automap",   secondaryRects["AUTOMAP"],   0.48f, 0f, new Vector2(0.20f, 4.40f),
                     classicHidden: true),
                Elem("Rest",      secondaryRects["REST"],      0.48f, 0f, new Vector2(0.20f, 4.93f),
                     classicHidden: true),
            };

            settingsPanel.controller = controller;
            settingsPanel.layout = layout;
            settingsPanel.lookZone = lookZone;
            settingsPanel.joystick = joystick;
            settingsPanel.hudGroup = hudGroup;
            settingsPanel.layoutEditor = layoutEditor;
            layoutEditor.layout = layout;
            layoutEditor.canvas = canvas;
            layoutEditor.gameplayLayer = gameplayLayer;

            // Hidden until MENU is pressed. Done last so children exist first.
            drawerGo.SetActive(false);

            // Auto-assign the cursor texture. Without it InputManager.OnGUI skips
            // drawing entirely and the menu cursor is invisible while still being
            // functional - which reads as "the menus are broken".
            AssignCursorTexture(controller);

            // Wire the combat toggle now that the controller exists.
            UnityEditor.Events.UnityEventTools.AddPersistentListener(
                combatButton.onClick, controller.ToggleCombatMode);

            // Menu layer starts hidden; the controller reactivates it when a window opens.
            menuLayer.SetActive(false);

            Selection.activeGameObject = controllerGo;
            EditorUtility.SetDirty(controllerGo);
            EditorUtility.SetDirty(canvasGo);

            Debug.Log(
                "[MobileHudBuilder] Built touch HUD.\n" +
                "  MobileCanvas (sort order 50, 1920x1080 reference)\n" +
                "    GameplayLayer: LookZone, MoveJoystick, ActivateButton, CombatToggle\n" +
                "      PrimaryBank (" + primary.Count + " always visible)\n" +
                "      MenuToggle -> SecondaryBank (" + (secondary.Count + 1) + " incl. TUNE, hidden)\n" +
                "    MenuLayer (inactive): MenuBackButton\n" +
                "  MobileInput: MobileInputController + VirtualMouseCursor, all references wired.\n" +
                "\n" +
                "  cursorTexture: " + (controller.cursorTexture != null
                    ? controller.cursorTexture.name + " (assigned automatically)"
                    : "*** NOT ASSIGNED - menu cursor will be invisible ***") + "\n" +
                "\n" +
                "Artwork is applied by MobileIconImporter, which ApplyAll runs next.");
        }

        [MenuItem("Tools/Daggerfall Mobile/Remove Touch HUD")]
        public static void Remove()
        {
            DestroyExisting(canvasName);
            DestroyExisting(controllerName);
            Debug.Log("[MobileHudBuilder] Removed touch HUD objects.");
        }

        #region Helpers

        /// <summary>
        /// Loads the generated arrow cursor and forces pixel-art-friendly import
        /// settings: point filtering and no compression, so a 32x32 cursor stays crisp
        /// instead of turning into a blurry DXT smear.
        /// </summary>
        static void AssignCursorTexture(MobileInputController controller)
        {
            const string cursorPath = "Assets/DaggerfallMobile/UI/cursor_arrow.png";

            TextureImporter importer = AssetImporter.GetAtPath(cursorPath) as TextureImporter;
            if (importer != null)
            {
                TextureImporterSettings settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.filterMode = FilterMode.Point;
                settings.wrapMode = TextureWrapMode.Clamp;
                settings.mipmapEnabled = false;
                settings.alphaIsTransparency = true;
                importer.SetTextureSettings(settings);
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }

            Texture2D cursor = AssetDatabase.LoadAssetAtPath<Texture2D>(cursorPath);
            if (cursor != null)
            {
                controller.cursorTexture = cursor;
                controller.cursorWidth = cursor.width;
                controller.cursorHeight = cursor.height;
            }
            else
            {
                Debug.LogWarning("[MobileHudBuilder] cursor texture not found at " + cursorPath +
                                 " - assign MobileInputController.cursorTexture by hand or the menu cursor stays invisible.");
            }
        }

        static MobileHudLayout.Element Elem(string name, RectTransform rt, float widthIn,
                                            float heightIn, Vector2 marginIn, bool position = true,
                                            Vector2? classicMarginIn = null,
                                            float classicWidthIn = 0f,
                                            bool classicHidden = false)
        {
            return new MobileHudLayout.Element
            {
                name = name,
                target = rt,
                widthInches = widthIn,
                heightInches = heightIn,
                marginInches = marginIn,
                applySize = widthIn > 0f,
                applyPosition = position,
                classicMarginInches = classicMarginIn ?? marginIn,
                classicWidthInches = classicWidthIn,
                classicHidden = classicHidden,
            };
        }

        static void DestroyExisting(string name)
        {
            GameObject existing = GameObject.Find(name);
            while (existing != null)
            {
                Undo.DestroyObjectImmediate(existing);
                existing = GameObject.Find(name);
            }
        }

        static GameObject CreateChild(GameObject parent, string name)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent.transform, false);
            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
            return go;
        }

        static GameObject CreateFullScreenChild(GameObject parent, string name)
        {
            GameObject go = CreateChild(parent, name);
            RectTransform rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return go;
        }

        static void Anchor(RectTransform rect, Vector2 min, Vector2 max, Vector2 pivot)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.pivot = pivot;
        }

        static GameObject CreateActionButton(
            GameObject parent, string name, string label,
            InputManager.Actions action, MobileActionButton.PressMode mode,
            Vector2 anchor, Vector2 position, Vector2 size)
        {
            GameObject go = CreateChild(parent, name);
            RectTransform rect = (RectTransform)go.transform;
            Anchor(rect, anchor, anchor, anchor);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            Image image = go.AddComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0.30f);
            image.raycastTarget = true;

            MobileActionButton button = go.AddComponent<MobileActionButton>();
            button.action = action;
            button.pressMode = mode;
            button.tintTarget = image;

            if (!string.IsNullOrEmpty(label))
                AddLabel(go, label);
            return go;
        }

        static void AddLabel(GameObject parent, string text)
        {
            GameObject go = CreateChild(parent, "Label");
            RectTransform rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Text label = go.AddComponent<Text>();
            label.text = text;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.fontSize = 26;
            label.raycastTarget = false;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (label.font == null)
                label.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        #endregion
    }
}
