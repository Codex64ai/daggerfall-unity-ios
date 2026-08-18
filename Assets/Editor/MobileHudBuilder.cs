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
            if (Object.FindObjectOfType<EventSystem>() == null)
            {
                GameObject es = new GameObject("EventSystem",
                    typeof(EventSystem), typeof(StandaloneInputModule));
                Undo.RegisterCreatedObjectUndo(es, "Create EventSystem");
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

            // ---------------------------------------------------------- activate button
            GameObject activateGo = CreateActionButton(
                gameplayLayer, "ActivateButton", "ACTIVATE",
                InputManager.Actions.ActivateCenterObject, MobileActionButton.PressMode.Tap,
                new Vector2(1f, 0f), new Vector2(-60f, 470f), new Vector2(220f, 220f));

            // ---------------------------------------------------------- primary bank
            // Only what is used moment to moment stays on screen. Ten always-visible
            // buttons crowded the look/swipe area, and map/status/rest are occasional.
            GameObject bankGo = CreateChild(gameplayLayer, "PrimaryBank");
            RectTransform bankRect = (RectTransform)bankGo.transform;
            Anchor(bankRect, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f));
            bankRect.anchoredPosition = new Vector2(-40f, 40f);
            bankRect.sizeDelta = new Vector2(660f, 160f);

            GridLayoutGroup grid = bankGo.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(150f, 150f);
            grid.spacing = new Vector2(14f, 14f);
            grid.startCorner = GridLayoutGroup.Corner.LowerRight;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.LowerRight;
            grid.constraint = GridLayoutGroup.Constraint.FixedRowCount;
            grid.constraintCount = 1;

            var primary = new List<(string label, InputManager.Actions action, MobileActionButton.PressMode mode)>
            {
                ("WEAPON", InputManager.Actions.ReadyWeapon, MobileActionButton.PressMode.Tap),
                ("SPELL",  InputManager.Actions.CastSpell,   MobileActionButton.PressMode.Tap),
                ("JUMP",   InputManager.Actions.Jump,        MobileActionButton.PressMode.Tap),
                ("CROUCH", InputManager.Actions.Crouch,      MobileActionButton.PressMode.Toggle),
            };
            foreach (var entry in primary)
            {
                CreateActionButton(bankGo, entry.label + "Button", entry.label,
                    entry.action, entry.mode,
                    new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(150f, 150f));
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

            // Secondary buttons stack upward from the MENU button.
            GameObject drawerGo = CreateChild(gameplayLayer, "SecondaryBank");
            RectTransform drawerRect = (RectTransform)drawerGo.transform;
            Anchor(drawerRect, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f));
            drawerRect.anchoredPosition = new Vector2(-720f, 230f);
            drawerRect.sizeDelta = new Vector2(160f, 830f);

            GridLayoutGroup drawerGrid = drawerGo.AddComponent<GridLayoutGroup>();
            drawerGrid.cellSize = new Vector2(150f, 150f);
            drawerGrid.spacing = new Vector2(14f, 14f);
            drawerGrid.startCorner = GridLayoutGroup.Corner.LowerRight;
            drawerGrid.startAxis = GridLayoutGroup.Axis.Vertical;
            drawerGrid.childAlignment = TextAnchor.LowerCenter;
            drawerGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            drawerGrid.constraintCount = 1;

            MobileButtonDrawer drawer = gameplayLayer.AddComponent<MobileButtonDrawer>();
            drawer.panel = drawerGo;
            drawer.toggleGraphic = menuToggleImg;

            var secondary = new List<(string label, InputManager.Actions action)>
            {
                ("INVENTORY", InputManager.Actions.Inventory),
                ("STATUS",    InputManager.Actions.Status),
                ("MAP",       InputManager.Actions.TravelMap),
                ("REST",      InputManager.Actions.Rest),
            };
            foreach (var entry in secondary)
            {
                GameObject b = CreateActionButton(drawerGo, entry.label + "Button", entry.label,
                    entry.action, MobileActionButton.PressMode.Tap,
                    new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(150f, 150f));
                b.GetComponent<MobileActionButton>().ownerDrawer = drawer;
            }

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

            // ---------------------------------------------------------- reticle
            GameObject reticleGo = CreateChild(gameplayLayer, "Reticle");
            RectTransform reticleRect = (RectTransform)reticleGo.transform;
            Anchor(reticleRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            reticleRect.anchoredPosition = Vector2.zero;
            reticleRect.sizeDelta = new Vector2(10f, 10f);
            Image reticleImg = reticleGo.AddComponent<Image>();
            reticleImg.color = new Color(1f, 1f, 1f, 0.55f);
            reticleImg.raycastTarget = false;   // must never absorb a look drag

            // ---------------------------------------------------------- menu back button
            CreateActionButton(menuLayer, "MenuBackButton", "BACK",
                InputManager.Actions.Escape, MobileActionButton.PressMode.Tap,
                new Vector2(0f, 1f), new Vector2(120f, -90f), new Vector2(160f, 110f));

            // ---------------------------------------------------------- settings gear
            // Inside the drawer: settings are not a moment-to-moment control.
            GameObject gearGo = CreateChild(drawerGo, "SettingsGear");
            RectTransform gearRect = (RectTransform)gearGo.transform;
            gearRect.sizeDelta = new Vector2(150f, 150f);
            Image gearImg = gearGo.AddComponent<Image>();
            gearImg.color = new Color(1f, 1f, 1f, 0.28f);
            Button gearButton = gearGo.AddComponent<Button>();
            AddLabel(gearGo, "TUNE");

            // ---------------------------------------------------------- tuning panel
            // Parented to the canvas, not GameplayLayer, so it stays reachable when the
            // gameplay HUD is hidden behind a classic menu.
            GameObject panelHost = CreateFullScreenChild(canvasGo, "SettingsHost");
            MobileSettingsPanel settingsPanel = panelHost.AddComponent<MobileSettingsPanel>();

            // ---------------------------------------------------------- controller
            GameObject controllerGo = new GameObject(controllerName);
            Undo.RegisterCreatedObjectUndo(controllerGo, "Create MobileInput");

            MobileInputController controller = controllerGo.AddComponent<MobileInputController>();
            VirtualMouseCursor cursor = controllerGo.AddComponent<VirtualMouseCursor>();

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
            MobileHudLayout layout = gameplayLayer.AddComponent<MobileHudLayout>();
            layout.canvas = canvas;
            layout.elements = new[]
            {
                Elem("Joystick",  joyRect,      1.60f, 0f, new Vector2(1.00f, 0.95f)),
                Elem("Knob",      knobRect,     0.66f, 0f, Vector2.zero, false),
                Elem("LookJoystick", lookJoyRect, 1.60f, 0f, new Vector2(1.00f, 0.95f)),
                Elem("LookKnob",  lookKnobRect, 0.66f, 0f, Vector2.zero, false),
                Elem("Activate",  (RectTransform)activateGo.transform, 1.00f, 0f, new Vector2(2.30f, 0.40f)),
                Elem("Combat",    combatRect,   0.72f, 0f, new Vector2(2.45f, 1.60f)),
                Elem("MenuToggle", menuToggleRect, 0.62f, 0f, new Vector2(3.55f, 0.40f)),
                Bank("PrimaryBank",   bankRect,   new Vector2(0.35f, 2.05f), 0.62f, 0.07f),
                Bank("SecondaryBank", drawerRect, new Vector2(3.55f, 1.15f), 0.62f, 0.07f),
            };

            settingsPanel.controller = controller;
            settingsPanel.layout = layout;
            settingsPanel.lookZone = lookZone;
            settingsPanel.joystick = joystick;
            settingsPanel.hudGroup = hudGroup;

            UnityEditor.Events.UnityEventTools.AddPersistentListener(
                gearButton.onClick, settingsPanel.Toggle);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(
                gearButton.onClick, drawer.Close);

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
                "    GameplayLayer: LookZone, MoveJoystick, ActivateButton, CombatToggle, Reticle\n" +
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
                                            float heightIn, Vector2 marginIn, bool position = true)
        {
            return new MobileHudLayout.Element
            {
                name = name,
                target = rt,
                widthInches = widthIn,
                heightInches = heightIn,
                marginInches = marginIn,
                applySize = true,
                applyPosition = position,
            };
        }

        static MobileHudLayout.Element Bank(string name, RectTransform rt, Vector2 marginIn,
                                            float cellIn, float spacingIn)
        {
            return new MobileHudLayout.Element
            {
                name = name,
                target = rt,
                widthInches = 0f,          // grid drives its own children
                marginInches = marginIn,
                applySize = false,
                applyPosition = true,
                gridCellInches = cellIn,
                gridSpacingInches = spacingIn,
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
