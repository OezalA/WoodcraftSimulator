using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using WoodcraftSimulator.Core;
using WoodcraftSimulator.Interaction;
using WoodcraftSimulator.Materials;
using WoodcraftSimulator.Player;
using WoodcraftSimulator.UI;
using WoodcraftSimulator.Workshop;

namespace WoodcraftSimulator.Editor
{
    /// <summary>
    /// Generates the greybox test workshop scene from code so it is reproducible.
    /// Units: 1 Unity unit = 1 meter. Real-world reference sizes are noted inline.
    /// </summary>
    public static class WorkshopSceneBuilder
    {
        private const string ScenePath = "Assets/_WoodcraftSimulator/Scenes/Workshop/Workshop.unity";
        private const string MaterialFolder = "Assets/_WoodcraftSimulator/Art/Materials/Greybox";
        private const string UiFolder = "Assets/_WoodcraftSimulator/UI";
        private const string HudUxmlPath = UiFolder + "/Hud.uxml";
        private const string PauseMenuUxmlPath = UiFolder + "/PauseMenu.uxml";
        private const string ThemePath = UiFolder + "/DefaultRuntimeTheme.tss";
        private const string PanelSettingsPath = UiFolder + "/GameplayPanelSettings.asset";

        // Workshop interior: 8 m x 6 m, 3 m walls, 10 cm concrete slab on top of the ground.
        private const float FloorTop = 0.10f;
        private const float InteriorWidth = 8f;
        private const float InteriorDepth = 6f;
        private const float WallHeight = 3f;
        private const float WallThickness = 0.2f;
        private const float DoorWidth = 1.2f;
        private const float DoorHeight = 2.1f;

        // Player: 1.80 m tall capsule, eyes at 1.65 m.
        private const float PlayerHeight = 1.8f;
        private const float PlayerRadius = 0.3f;
        private const float EyeHeight = 1.65f;

        [MenuItem("WoodcraftSimulator/Build Test Workshop Scene")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            if (File.Exists(ScenePath) &&
                !EditorUtility.DisplayDialog("Workshop scene exists",
                    $"{ScenePath} already exists. Rebuild and overwrite it?", "Rebuild", "Cancel"))
            {
                return;
            }

            // Load UI assets first so a missing file aborts before the current scene is replaced.
            var hudLayout = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(HudUxmlPath);
            var pauseMenuLayout = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(PauseMenuUxmlPath);
            var theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);
            if (hudLayout == null || pauseMenuLayout == null || theme == null)
            {
                Debug.LogError($"Workshop scene not built: missing {HudUxmlPath}, {PauseMenuUxmlPath} or {ThemePath}.");
                return;
            }

            EnsureFolder(Path.GetDirectoryName(ScenePath).Replace('\\', '/'));
            EnsureFolder(MaterialFolder);
            EnsureFolder(WoodSpeciesAssets.Folder);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            ConfigureDefaultObjects(scene);

            var materials = new GreyboxMaterials();
            BuildEnvironment(materials, WoodSpeciesAssets.EnsureDefaults());
            GameObject player = BuildPlayer();
            PanelSettings panelSettings = GetOrCreatePanelSettings(theme);
            BuildHud(player.GetComponent<PlayerInteractor>(), hudLayout, panelSettings);
            BuildPauseSystem(player, pauseMenuLayout, panelSettings);

            // Intentionally does not touch Build Settings (ProjectSettings): add the scene there manually when needed.
            EditorSceneManager.SaveScene(scene, ScenePath);

            Selection.activeGameObject = player;
            Debug.Log($"Workshop scene built and saved to {ScenePath}.");
        }

        private static void ConfigureDefaultObjects(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                // The player brings its own camera.
                if (root.TryGetComponent(out Camera _))
                {
                    Object.DestroyImmediate(root);
                }
                else if (root.TryGetComponent(out Light light) && light.type == LightType.Directional)
                {
                    root.name = "Sun";
                    root.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
                    light.shadows = LightShadows.Soft;
                }
            }
        }

        private static void BuildEnvironment(GreyboxMaterials materials,
            IReadOnlyDictionary<string, WoodSpeciesDefinition> species)
        {
            var environment = new GameObject("Environment").transform;

            // Ground: Unity's Plane primitive is 10 m x 10 m at scale 1 -> 40 m x 40 m.
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(environment, false);
            ground.transform.localScale = new Vector3(4f, 1f, 4f);
            ground.GetComponent<Renderer>().sharedMaterial = materials.Ground;

            var workshop = new GameObject("Workshop").transform;
            workshop.SetParent(environment, false);

            CreateBox("Floor", workshop,
                new Vector3(0f, FloorTop / 2f, 0f),
                new Vector3(InteriorWidth + 2f * WallThickness, FloorTop, InteriorDepth + 2f * WallThickness),
                materials.Concrete);

            BuildWalls(workshop, materials.Wall);
            BuildWorkbench(workshop, materials.Workbench);
            BuildTimber(workshop, materials.Lumber, species);
            BuildWorkbenchLamp(workshop, materials.Wall);
        }

        private static void BuildWorkbenchLamp(Transform parent, Material switchMaterial)
        {
            // Spot light hanging above the workbench, controlled by a switch on the back wall.
            var lampObject = new GameObject("WorkbenchLamp");
            lampObject.transform.SetParent(parent, false);
            lampObject.transform.localPosition = new Vector3(0f, FloorTop + 2.6f, InteriorDepth / 2f - 0.5f);
            lampObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // pointing straight down

            var lamp = lampObject.AddComponent<Light>();
            lamp.type = LightType.Spot;
            lamp.color = new Color(1f, 0.93f, 0.8f); // warm white
            lamp.intensity = 8f;
            lamp.range = 5f;
            lamp.spotAngle = 80f;
            lamp.shadows = LightShadows.Soft;

            // Switch: 8 x 12 cm plate at 1.2 m height on the back wall, right of the workbench.
            GameObject switchObject = CreateBox("LightSwitch", parent,
                new Vector3(1.4f, FloorTop + 1.2f, InteriorDepth / 2f - 0.015f),
                new Vector3(0.08f, 0.12f, 0.03f), switchMaterial);

            var lightSwitch = switchObject.AddComponent<LightSwitch>();
            var serializedSwitch = new SerializedObject(lightSwitch);
            SerializedProperty lightsProperty = serializedSwitch.FindProperty("lights");
            lightsProperty.arraySize = 1;
            lightsProperty.GetArrayElementAtIndex(0).objectReferenceValue = lamp;
            serializedSwitch.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildWalls(Transform parent, Material material)
        {
            var walls = new GameObject("Walls").transform;
            walls.SetParent(parent, false);

            float wallCenterY = FloorTop + WallHeight / 2f;
            float outerWidth = InteriorWidth + 2f * WallThickness;
            float backZ = InteriorDepth / 2f + WallThickness / 2f;
            float sideX = InteriorWidth / 2f + WallThickness / 2f;

            CreateBox("Wall_Back", walls, new Vector3(0f, wallCenterY, backZ),
                new Vector3(outerWidth, WallHeight, WallThickness), material);
            CreateBox("Wall_Left", walls, new Vector3(-sideX, wallCenterY, 0f),
                new Vector3(WallThickness, WallHeight, InteriorDepth), material);
            CreateBox("Wall_Right", walls, new Vector3(sideX, wallCenterY, 0f),
                new Vector3(WallThickness, WallHeight, InteriorDepth), material);

            // Front wall with a door opening in the middle.
            float segmentWidth = (outerWidth - DoorWidth) / 2f;
            float segmentX = DoorWidth / 2f + segmentWidth / 2f;
            CreateBox("Wall_Front_L", walls, new Vector3(-segmentX, wallCenterY, -backZ),
                new Vector3(segmentWidth, WallHeight, WallThickness), material);
            CreateBox("Wall_Front_R", walls, new Vector3(segmentX, wallCenterY, -backZ),
                new Vector3(segmentWidth, WallHeight, WallThickness), material);

            float lintelHeight = WallHeight - DoorHeight;
            CreateBox("Wall_Front_Lintel", walls,
                new Vector3(0f, FloorTop + DoorHeight + lintelHeight / 2f, -backZ),
                new Vector3(DoorWidth, lintelHeight, WallThickness), material);
        }

        private static void BuildWorkbench(Transform parent, Material material)
        {
            // Typical workbench: 2.0 m x 0.8 m, work surface 0.90 m above the floor, 60 mm top.
            const float length = 2.0f;
            const float depth = 0.8f;
            const float surfaceHeight = 0.9f;
            const float topThickness = 0.06f;
            const float legSize = 0.07f;
            const float legInset = 0.08f;

            var bench = new GameObject("Workbench").transform;
            bench.SetParent(parent, false);
            // Against the back wall, leaving a 10 cm gap.
            bench.localPosition = new Vector3(0f, FloorTop, InteriorDepth / 2f - depth / 2f - 0.1f);

            CreateBox("Top", bench, new Vector3(0f, surfaceHeight - topThickness / 2f, 0f),
                new Vector3(length, topThickness, depth), material);

            float legHeight = surfaceHeight - topThickness;
            float legX = length / 2f - legInset;
            float legZ = depth / 2f - legInset;
            for (int i = 0; i < 4; i++)
            {
                float x = (i % 2 == 0) ? -legX : legX;
                float z = (i < 2) ? -legZ : legZ;
                CreateBox($"Leg_{i + 1}", bench, new Vector3(x, legHeight / 2f, z),
                    new Vector3(legSize, legHeight, legSize), material);
            }
        }

        private static void BuildTimber(Transform parent, Material material,
            IReadOnlyDictionary<string, WoodSpeciesDefinition> species)
        {
            var timber = new GameObject("Timber").transform;
            timber.SetParent(parent, false);

            // Construction timber stacked on the floor along the left wall (length along Z).
            float wallX = -InteriorWidth / 2f;
            for (int i = 0; i < 3; i++)
            {
                CreateBoard($"Spruce_2400x95x45_{i + 1}", timber, species["spruce"], 2400f, 95f, 45f,
                    new Vector3(wallX + 0.4f, FloorTop + i * 0.046f, 0f), 0f, material);
            }

            CreateBoard("Larch_2000x145x28", timber, species["larch"], 2000f, 145f, 28f,
                new Vector3(wallX + 0.7f, FloorTop, 0f), 0f, material);
            CreateBoard("DouglasFir_2400x145x45", timber, species["douglas-fir"], 2400f, 145f, 45f,
                new Vector3(wallX + 1.0f, FloorTop, 0f), 0f, material);

            // Smaller boards on the workbench (length along X, yaw 90°). Bench surface: FloorTop + 0.9 m.
            float benchTop = FloorTop + 0.9f;
            float benchZ = InteriorDepth / 2f - 0.5f;
            CreateBoard("Pine_1000x200x18", timber, species["pine"], 1000f, 200f, 18f,
                new Vector3(-0.45f, benchTop, benchZ - 0.05f), 90f, material);
            CreateBoard("Oak_600x120x22", timber, species["oak"], 600f, 120f, 22f,
                new Vector3(0.5f, benchTop, benchZ - 0.2f), 90f, material);
            CreateBoard("Walnut_400x100x25", timber, species["walnut"], 400f, 100f, 25f,
                new Vector3(0.5f, benchTop, benchZ - 0.02f), 90f, material);
            CreateBoard("Beech_500x45x45", timber, species["beech"], 500f, 45f, 45f,
                new Vector3(0.5f, benchTop, benchZ + 0.12f), 90f, material);
        }

        /// <summary>
        /// Creates a <see cref="TimberBoard"/> resting on a surface. <paramref name="surfacePosition"/> is the point
        /// on the supporting surface below the board's center.
        /// </summary>
        private static void CreateBoard(string name, Transform parent, WoodSpeciesDefinition species,
            float lengthMm, float widthMm, float thicknessMm, Vector3 surfacePosition, float yawDegrees, Material material)
        {
            var board = new GameObject(name);
            board.transform.SetParent(parent, false);
            // +1 mm so the physics engine doesn't start with overlapping colliders.
            board.transform.localPosition = surfacePosition + Vector3.up * (thicknessMm / 2000f + 0.001f);
            board.transform.localRotation = Quaternion.Euler(0f, yawDegrees, 0f);

            board.AddComponent<MeshFilter>();
            board.AddComponent<MeshRenderer>().sharedMaterial = material;
            board.AddComponent<BoxCollider>();

            var body = board.AddComponent<Rigidbody>();
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            var timberBoard = board.AddComponent<TimberBoard>();
            var serialized = new SerializedObject(timberBoard);
            serialized.FindProperty("species").objectReferenceValue = species;
            serialized.FindProperty("lengthMillimeters").floatValue = lengthMm;
            serialized.FindProperty("widthMillimeters").floatValue = widthMm;
            serialized.FindProperty("thicknessMillimeters").floatValue = thicknessMm;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            // Generate mesh/collider now so the board is visible in the Editor; Awake regenerates at runtime.
            timberBoard.Rebuild();
        }

        private static GameObject BuildPlayer()
        {
            var player = new GameObject("Player");
            // Origin at the feet, standing in the workshop facing the workbench (+Z).
            player.transform.position = new Vector3(0f, FloorTop + 0.05f, -1.5f);

            var characterController = player.AddComponent<CharacterController>();
            characterController.height = PlayerHeight;
            characterController.radius = PlayerRadius;
            characterController.center = new Vector3(0f, PlayerHeight / 2f, 0f);
            characterController.skinWidth = 0.03f;
            characterController.stepOffset = 0.3f;
            characterController.slopeLimit = 45f;

            var cameraObject = new GameObject("PlayerCamera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(player.transform, false);
            cameraObject.transform.localPosition = new Vector3(0f, EyeHeight, 0f);

            var camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 70f;
            // Small near plane so close-up work (measuring, marking) doesn't clip into objects.
            camera.nearClipPlane = 0.03f;
            camera.farClipPlane = 500f;
            cameraObject.AddComponent<AudioListener>();

            var controller = player.AddComponent<PlayerController>();
            // Private [SerializeField] fields are set through SerializedObject, exactly like the Inspector does.
            var serializedController = new SerializedObject(controller);
            serializedController.FindProperty("cameraTransform").objectReferenceValue = cameraObject.transform;
            serializedController.ApplyModifiedPropertiesWithoutUndo();

            var interactor = player.AddComponent<PlayerInteractor>();
            SetReference(interactor, "cameraTransform", cameraObject.transform);

            return player;
        }

        private static void BuildHud(PlayerInteractor interactor, VisualTreeAsset layout, PanelSettings panelSettings)
        {
            var hudObject = new GameObject("HUD");

            var document = hudObject.AddComponent<UIDocument>();
            document.panelSettings = panelSettings;
            document.visualTreeAsset = layout;

            var hud = hudObject.AddComponent<GameplayHud>();
            SetReference(hud, "interactor", interactor);
        }

        private static void BuildPauseSystem(GameObject player, VisualTreeAsset layout, PanelSettings panelSettings)
        {
            var playerController = player.GetComponent<PlayerController>();
            var interactor = player.GetComponent<PlayerInteractor>();

            // Kept separate from the player and from any future GameManager (master prompt §6.1).
            var pauseObject = new GameObject("PauseController");
            var pauseController = pauseObject.AddComponent<PauseController>();

            var serializedPause = new SerializedObject(pauseController);
            SerializedProperty disabledProperty = serializedPause.FindProperty("disableWhilePaused");
            disabledProperty.arraySize = 2;
            disabledProperty.GetArrayElementAtIndex(0).objectReferenceValue = playerController;
            disabledProperty.GetArrayElementAtIndex(1).objectReferenceValue = interactor;
            serializedPause.ApplyModifiedPropertiesWithoutUndo();

            var menuObject = new GameObject("PauseMenu");
            var document = menuObject.AddComponent<UIDocument>();
            document.panelSettings = panelSettings;
            document.visualTreeAsset = layout;
            document.sortingOrder = 10; // drawn above the HUD (same panel, higher order wins)

            var pauseMenu = menuObject.AddComponent<PauseMenu>();
            SetReference(pauseMenu, "pauseController", pauseController);
            SetReference(pauseMenu, "playerController", playerController);
        }

        private static PanelSettings GetOrCreatePanelSettings(ThemeStyleSheet theme)
        {
            var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (panelSettings == null)
            {
                panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(panelSettings, PanelSettingsPath);
            }

            // Same idea as uGUI's CanvasScaler: design at 1080p, scale with the window.
            panelSettings.themeStyleSheet = theme;
            panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panelSettings.referenceResolution = new Vector2Int(1920, 1080);
            panelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panelSettings.match = 0.5f;
            EditorUtility.SetDirty(panelSettings);
            AssetDatabase.SaveAssetIfDirty(panelSettings);
            return panelSettings;
        }

        /// <summary>Assigns a private [SerializeField] reference the same way the Inspector does.</summary>
        private static void SetReference(Object target, string propertyName, Object value)
        {
            var serializedObject = new SerializedObject(target);
            serializedObject.FindProperty(propertyName).objectReferenceValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject CreateBox(string name, Transform parent, Vector3 localPosition,
            Vector3 localScale, Material material)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.localPosition = localPosition;
            box.transform.localScale = localScale;
            box.GetComponent<Renderer>().sharedMaterial = material;
            return box;
        }

        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            string parent = Path.GetDirectoryName(folderPath).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folderPath));
        }

        /// <summary>Flat-colored URP Lit materials, saved as assets so the scene can reference them.</summary>
        private sealed class GreyboxMaterials
        {
            public Material Ground { get; } = GetOrCreate("Ground", new Color(0.36f, 0.42f, 0.30f));
            public Material Concrete { get; } = GetOrCreate("Concrete", new Color(0.55f, 0.55f, 0.53f));
            public Material Wall { get; } = GetOrCreate("Wall", new Color(0.82f, 0.80f, 0.74f));
            public Material Workbench { get; } = GetOrCreate("Workbench", new Color(0.60f, 0.43f, 0.27f));
            public Material Lumber { get; } = GetOrCreate("Lumber", new Color(0.86f, 0.73f, 0.53f));

            private static Material GetOrCreate(string name, Color color)
            {
                string path = $"{MaterialFolder}/{name}.mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);

                if (material == null)
                {
                    material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    AssetDatabase.CreateAsset(material, path);
                }

                material.SetColor("_BaseColor", color);
                material.SetFloat("_Smoothness", 0.15f);
                EditorUtility.SetDirty(material);
                return material;
            }
        }
    }
}
