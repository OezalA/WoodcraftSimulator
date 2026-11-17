using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using WoodcraftSimulator.Construction;
using WoodcraftSimulator.Core;
using WoodcraftSimulator.Cutting;
using WoodcraftSimulator.Economy;
using WoodcraftSimulator.Handling;
using WoodcraftSimulator.Interaction;
using WoodcraftSimulator.Inventory;
using WoodcraftSimulator.Jobs;
using WoodcraftSimulator.Materials;
using WoodcraftSimulator.Persistence;
using WoodcraftSimulator.Player;
using WoodcraftSimulator.Tools;
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
        // Timber rack on the left wall: 3 shelves (tops above the floor, 0.6 m apart so the lower shelves stay
        // visible and reachable), 0.45 m deep, 2.6 m long.
        private const float RackDepth = 0.45f;
        private const float RackShelfLength = 2.6f;
        private const float RackShelfThickness = 0.03f;
        private const float RackUprightSize = 0.08f;
        private const float RackArmHeight = 0.04f;
        private const float RackFrontOfUprightsX = -InteriorWidth / 2f + RackUprightSize;
        private static readonly float[] RackShelfTops = { 0.30f, 0.90f, 1.50f };
        private static readonly float[] RackUprightZ = { -1.2f, 0f, 1.2f };

        // Minimum size (m) of a small item's grab collider in every direction (visual can be smaller).
        private const float MinGrabSize = 0.05f;

        private const float PlayerHeight = 1.8f;
        private const float PlayerRadius = 0.3f;
        private const float EyeHeight = 1.65f;

        [MenuItem("WoodcraftSimulator/Build Test Workshop Scene")]
        public static void Build()
        {
            // Scenes can't be created or saved in Play Mode (Unity throws InvalidOperationException).
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Stop Play Mode first",
                    "The workshop scene can only be rebuilt outside Play Mode. Press Stop, then run this command again.",
                    "OK");
                return;
            }

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
            EnsureFolder(ItemAssets.Folder);
            EnsureFolder(SawAssets.Folder);
            EnsureFolder(BlueprintAssets.Folder);
            EnsureFolder(BlueprintAssets.FastenerFolder);
            EnsureFolder(JobAssets.Folder);
            EnsureFolder(EconomyAssets.Folder);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            ConfigureDefaultObjects(scene);

            var materials = new GreyboxMaterials();
            BuildEnvironment(materials, WoodSpeciesAssets.EnsureDefaults());
            GameObject player = BuildPlayer(materials.PlacementPreview);
            PanelSettings panelSettings = GetOrCreatePanelSettings(theme);
            BuildHud(player.GetComponent<PlayerInteractor>(), player.GetComponent<PlayerCarrier>(), hudLayout, panelSettings);
            SaveGameService saveService = BuildSaveSystem(player, materials, WoodSpeciesAssets.EnsureDefaults());
            BuildPauseSystem(player, pauseMenuLayout, panelSettings, saveService);
            AddPlayerSounds(player);
            TurkishNames.Apply();

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
            BuildTimberRack(workshop, materials.Steel, materials.Workbench);
            BuildTimber(workshop, materials, species);
            BuildBenchTools(workshop, materials);
            BuildWorkbenchLamp(workshop, materials.Wall);
            BuildAssemblyTable(workshop, materials);
            BuildTerraceSite(environment, materials);
            BuildPergolaSite(environment, materials);
            BuildJobBoard(workshop, materials, species);
            BuildToolWall(workshop, materials);
            BuildSawhorses(workshop, materials.Workbench, new Vector3(-1.6f, FloorTop, 0f));
            BuildOffcutBins(workshop, materials, species);
            BuildApprentice(workshop, materials);

            // The apprentice's walkable area, built when the scene starts (D-052).
            environment.gameObject.AddComponent<RuntimeNavMesh>();
        }

        /// <summary>
        /// Job board (Phase 6, D-039) on the back wall left of the workbench: a 70 × 50 cm board with one A4 sheet per
        /// job. Accepted jobs deliver their stock flat on the floor in front of the timber rack.
        /// The board factory is assigned later (it lives on the save system).
        /// </summary>
        private static void BuildJobBoard(Transform parent, GreyboxMaterials materials,
            IReadOnlyDictionary<string, WoodSpeciesDefinition> species)
        {
            float wallZ = InteriorDepth / 2f;
            float centerY = FloorTop + 1.5f;
            const float centerX = -1.9f;
            GameObject board = CreateBox("JobBoard", parent, new Vector3(centerX, centerY, wallZ - 0.01f),
                new Vector3(1.6f, 0.5f, 0.02f), materials.Workbench);
            Object.DestroyImmediate(board.GetComponent<BoxCollider>()); // the sheets are what you aim at

            var jobBoard = board.AddComponent<JobBoard>();
            JobDefinition[] jobs =
            {
                JobAssets.FrameJob(species), JobAssets.ShelfJob(species), ToteAssets.ToteJob(species), TerraceAssets.TerraceJob(species),
                PergolaAssets.PergolaJob(species)
            };
            for (int i = 0; i < jobs.Length; i++)
            {
                // A4 portrait (210 × 297 mm) pinned on the board; the sheet is a child of the workshop, not the board,
                // so its scale is its real size.
                float x = centerX + (i - (jobs.Length - 1) / 2f) * 0.3f;
                var sheetSize = new Vector3(0.21f, 0.297f, 0.002f);
                GameObject sheet = CreateBox($"JobSheet_{jobs[i].Id}", parent, new Vector3(x, centerY, wallZ - 0.021f),
                    sheetSize, materials.Paper);
                EnlargeGrabCollider(sheet, sheetSize);
                var jobSheet = sheet.AddComponent<JobSheet>();
                SetReference(jobSheet, "board", jobBoard);
                var serializedSheet = new SerializedObject(jobSheet);
                serializedSheet.FindProperty("jobIndex").intValue = i;
                serializedSheet.ApplyModifiedPropertiesWithoutUndo();
            }

            EconomyAssets.EnsurePay(jobs[0], 35f);
            EconomyAssets.EnsurePay(jobs[1], 60f);

            // Stock delivery: on the floor in front of the timber rack, boards along Z, side by side towards +X.
            // Its board factory is assigned later (it lives on the save system).
            var deliveryObject = new GameObject("StockDelivery");
            deliveryObject.transform.SetParent(parent, false);
            deliveryObject.transform.localPosition = new Vector3(RackFrontOfUprightsX + RackDepth + 0.25f, FloorTop, 0f);
            var delivery = deliveryObject.AddComponent<StockDelivery>();

            WorkshopEconomy economy = BuildEconomy(parent, materials, species, delivery);

            SetReferences(jobBoard, "jobs", jobs);
            SetReference(jobBoard, "station", Object.FindFirstObjectByType<AssemblyStation>());
            SetReference(jobBoard, "cutList", Object.FindFirstObjectByType<CutList>());
            SetReference(jobBoard, "waste", Object.FindFirstObjectByType<WorkshopWaste>());
            SetReference(jobBoard, "economy", economy);
            var outdoorStations = new List<Object>();
            foreach (string sitePath in new[] { "Environment/TerraceSite", "Environment/PergolaSite" })
            {
                GameObject outdoorSite = GameObject.Find(sitePath);
                if (outdoorSite != null)
                {
                    outdoorStations.Add(outdoorSite.GetComponent<AssemblyStation>());
                }
            }

            SetReferences(jobBoard, "otherStations", outdoorStations);
            SetReference(jobBoard, "saw", SawAssets.HandSaw());
            SetReferences(jobBoard, "fasteners", Fasteners());
        }

        /// <summary>
        /// Money, fastener stock and the order board (D-040): the board hangs on the back wall right of the workbench,
        /// one A4 sheet per catalog item; E on a sheet buys one. A new workshop starts with €100, 200 screws 4 × 40
        /// and 50 screws 5 × 80.
        /// </summary>
        private static WorkshopEconomy BuildEconomy(Transform parent, GreyboxMaterials materials,
            IReadOnlyDictionary<string, WoodSpeciesDefinition> species, StockDelivery delivery)
        {
            MaterialCatalogDefinition catalog = EconomyAssets.Catalog(species);
            var economyObject = new GameObject("Economy");
            economyObject.transform.SetParent(parent, false);
            var economy = economyObject.AddComponent<WorkshopEconomy>();
            SetReference(economy, "catalog", catalog);
            SetReference(economy, "delivery", delivery);
            GameObject outdoor = GameObject.Find("Environment/OutdoorDelivery");
            SetReference(economy, "outdoorDelivery", outdoor != null ? outdoor.GetComponent<StockDelivery>() : null);
            SetReferences(economy, "fasteners", Fasteners());
            var serializedEconomy = new SerializedObject(economy);
            serializedEconomy.FindProperty("startingFundsEuros").floatValue = 100f;
            SerializedProperty starting = serializedEconomy.FindProperty("startingFasteners");
            starting.arraySize = 4;
            starting.GetArrayElementAtIndex(0).FindPropertyRelative("fastener").objectReferenceValue = BlueprintAssets.WoodScrew4x40();
            starting.GetArrayElementAtIndex(0).FindPropertyRelative("count").intValue = 200;
            starting.GetArrayElementAtIndex(1).FindPropertyRelative("fastener").objectReferenceValue = BlueprintAssets.WoodScrew5x80();
            starting.GetArrayElementAtIndex(1).FindPropertyRelative("count").intValue = 50;
            starting.GetArrayElementAtIndex(2).FindPropertyRelative("fastener").objectReferenceValue = BlueprintAssets.Nail2_8x50();
            starting.GetArrayElementAtIndex(2).FindPropertyRelative("count").intValue = 100;
            starting.GetArrayElementAtIndex(3).FindPropertyRelative("fastener").objectReferenceValue = BlueprintAssets.Dowel8x40();
            starting.GetArrayElementAtIndex(3).FindPropertyRelative("count").intValue = 24;
            serializedEconomy.ApplyModifiedPropertiesWithoutUndo();

            float wallZ = InteriorDepth / 2f;
            // Up to seven sheets per row, material rows on top, equipment upgrades in the last row (D-044, D-046).
            const int columns = 7;
            const float rowStep = 0.33f;
            const float centerX = 2.75f; // right of the light switch (x 1.4)
            float topY = FloorTop + 1.85f;
            int count = catalog.Boards.Count + catalog.FastenerBoxes.Count;
            int rows = (count + columns - 1) / columns;
            float bottomY = topY - rows * rowStep;
            GameObject board = CreateBox("OrderBoard", parent,
                new Vector3(centerX, (topY + bottomY) / 2f, wallZ - 0.01f),
                new Vector3(0.3f * columns + 0.1f, (rows + 1) * rowStep + 0.05f, 0.02f), materials.Workbench);
            Object.DestroyImmediate(board.GetComponent<BoxCollider>());
            var sheetSize = new Vector3(0.21f, 0.297f, 0.002f);
            for (int i = 0; i < count; i++)
            {
                float x = centerX + (i % columns - (columns - 1) / 2f) * 0.3f;
                float y = topY - i / columns * rowStep;
                GameObject sheet = CreateBox($"OrderSheet_{i}", parent, new Vector3(x, y, wallZ - 0.021f),
                    sheetSize, materials.Paper);
                EnlargeGrabCollider(sheet, sheetSize);
                var orderSheet = sheet.AddComponent<OrderSheet>();
                SetReference(orderSheet, "economy", economy);
                var serializedSheet = new SerializedObject(orderSheet);
                serializedSheet.FindProperty("itemIndex").intValue = i;
                serializedSheet.ApplyModifiedPropertiesWithoutUndo();
            }

            BuildUpgrades(parent, materials, economy, centerX, bottomY, wallZ);

            // The drill takes its screws from the stock.
            DrillDriverTool drill = Object.FindFirstObjectByType<DrillDriverTool>();
            if (drill != null)
            {
                SetReference(drill, "economy", economy);
            }

            return economy;
        }


        /// <summary>
        /// Tool wall (Phase 8a, D-044): a 1.2 × 0.6 m board on the back wall above the workbench with one place per
        /// tool (a painted outline). With the tool on the belt, E on its outline hangs it there, upright.
        /// </summary>
        private static void BuildToolWall(Transform parent, GreyboxMaterials materials)
        {
            float wallZ = InteriorDepth / 2f;
            float centerY = FloorTop + 1.5f;
            GameObject board = CreateBox("ToolWall", parent, new Vector3(0f, centerY, wallZ - 0.01f),
                new Vector3(2.0f, 0.6f, 0.02f), materials.Workbench);
            Object.DestroyImmediate(board.GetComponent<BoxCollider>()); // the outlines are what you aim at

            // Item, x on the board, size of the item box (x = width, y = thickness, z = length) as built on the bench.
            AddToolPlace(parent, materials, ItemAssets.TapeMeasure(), -0.72f, new Vector3(0.07f, 0.035f, 0.07f), wallZ, centerY);
            AddToolPlace(parent, materials, ItemAssets.CarpenterPencil(), -0.58f, new Vector3(0.012f, 0.007f, 0.175f), wallZ, centerY);
            AddToolPlace(parent, materials, ItemAssets.HandSaw(), -0.4f, new Vector3(0.12f, 0.004f, 0.5f), wallZ, centerY);
            AddToolPlace(parent, materials, ItemAssets.DrillDriver(), -0.12f, new Vector3(0.2f, 0.07f, 0.16f), wallZ, centerY);

            // Phase 9 tools (D-045) start on the wall.
            var economy = Object.FindFirstObjectByType<WorkshopEconomy>();
            var hammerSize = new Vector3(0.035f, 0.03f, 0.33f);
            ToolHook hammerPlace = AddToolPlace(parent, materials, ItemAssets.Hammer(), 0.15f, hammerSize, wallZ, centerY);
            GameObject hammer = CreateHangingItem("Hammer", parent, ItemAssets.Hammer(), hammerSize, hammerPlace,
                materials.Steel, new Vector3(0.2f, -0.22f, 0.42f), new Vector3(0f, -10f, 0f));
            var hammerTool = hammer.AddComponent<HandFastenTool>();
            SetFastenKind(hammerTool, FastenerKind.Nail, 1.2f, economy);

            var glueSize = new Vector3(0.06f, 0.06f, 0.18f);
            ToolHook gluePlace = AddToolPlace(parent, materials, ItemAssets.GlueBottle(), 0.32f, glueSize, wallZ, centerY);
            GameObject glue = CreateHangingItem("GlueBottle", parent, ItemAssets.GlueBottle(), glueSize, gluePlace,
                materials.ToolYellow, new Vector3(0.18f, -0.18f, 0.38f), new Vector3(-60f, 0f, 0f));
            var glueTool = glue.AddComponent<HandFastenTool>();
            SetFastenKind(glueTool, FastenerKind.Dowel, 0.8f, economy);

            var sandingSize = new Vector3(0.07f, 0.03f, 0.12f);
            ToolHook sandingPlace = AddToolPlace(parent, materials, ItemAssets.SandingBlock(), 0.5f, sandingSize, wallZ, centerY);
            GameObject sanding = CreateHangingItem("SandingBlock", parent, ItemAssets.SandingBlock(), sandingSize,
                sandingPlace, materials.Paper, new Vector3(0.18f, -0.2f, 0.4f), new Vector3(0f, 0f, 0f));
            SetSurfaceResult(sanding.AddComponent<SurfaceTool>(), BoardSurface.Sanded);

            var brushSize = new Vector3(0.04f, 0.015f, 0.22f);
            ToolHook brushPlace = AddToolPlace(parent, materials, ItemAssets.OilBrush(), 0.68f, brushSize, wallZ, centerY);
            GameObject brush = CreateHangingItem("OilBrush", parent, ItemAssets.OilBrush(), brushSize, brushPlace,
                materials.Workbench, new Vector3(0.18f, -0.2f, 0.4f), new Vector3(20f, 0f, 0f));
            SetSurfaceResult(brush.AddComponent<SurfaceTool>(), BoardSurface.Oiled);

            // Phase 11 (D-049): a 400 mm spirit level for plumbing posts.
            var levelSize = new Vector3(0.045f, 0.025f, 0.4f);
            ToolHook levelPlace = AddToolPlace(parent, materials, ItemAssets.SpiritLevel(), 0.86f, levelSize, wallZ, centerY);
            GameObject level = CreateHangingItem("SpiritLevel", parent, ItemAssets.SpiritLevel(), levelSize, levelPlace,
                materials.ToolYellow, new Vector3(0.2f, -0.22f, 0.45f), new Vector3(0f, -10f, 0f));
            level.AddComponent<SpiritLevelTool>();
        }

        /// <summary>A pickup item that starts hanging on its tool-wall place.</summary>
        private static GameObject CreateHangingItem(string name, Transform parent, ItemDefinition definition, Vector3 size,
            ToolHook place, Material material, Vector3 handPosition, Vector3 handRotation)
        {
            GameObject item = CreatePickupItem(name, parent, definition, size, Vector3.zero, 0f, material,
                handPosition, handRotation);
            Transform hang = new SerializedObject(place).FindProperty("hangPoint").objectReferenceValue as Transform;
            if (hang != null)
            {
                item.transform.SetPositionAndRotation(hang.position, hang.rotation);
            }

            return item;
        }

        private static void SetSurfaceResult(SurfaceTool tool, BoardSurface result)
        {
            var serialized = new SerializedObject(tool);
            serialized.FindProperty("result").enumValueIndex = (int)result;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetFastenKind(HandFastenTool tool, FastenerKind kind, float seconds, WorkshopEconomy economy)
        {
            var serialized = new SerializedObject(tool);
            serialized.FindProperty("kind").enumValueIndex = (int)kind;
            serialized.FindProperty("seconds").floatValue = seconds;
            serialized.FindProperty("economy").objectReferenceValue = economy;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static ToolHook AddToolPlace(Transform parent, GreyboxMaterials materials, ItemDefinition item, float x,
            Vector3 itemSize, float wallZ, float centerY)
        {
            // Outline: slightly larger than the tool's face, flat on the board.
            var outlineSize = new Vector3(itemSize.x + 0.02f, itemSize.z + 0.02f, 0.002f);
            GameObject outline = CreateBox($"ToolPlace_{item.Id}", parent, new Vector3(x, centerY, wallZ - 0.021f),
                outlineSize, materials.Paper);
            EnlargeGrabCollider(outline, outlineSize);
            var hook = outline.AddComponent<ToolHook>();

            // The tool hangs upright in front of its outline: length vertical, thickness out of the wall.
            var hang = new GameObject("HangPoint").transform;
            hang.SetParent(parent, false);
            hang.localPosition = new Vector3(x, centerY, wallZ - 0.022f - itemSize.y / 2f - 0.003f);
            hang.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            hang.name = $"HangPoint_{item.Id}";

            SetReference(hook, "item", item);
            SetReference(hook, "hangPoint", hang);
            return hook;
        }

        /// <summary>
        /// Equipment upgrades (Phase 8b, D-044), one sheet each in the order board's lower row: pull saw (faster
        /// cuts), brushless drill (faster pilot holes and screws), LED lamp (brighter bench light).
        /// </summary>
        private static void BuildUpgrades(Transform parent, GreyboxMaterials materials, WorkshopEconomy economy,
            float centerX, float rowY, float wallZ)
        {
            var upgrades = economy.gameObject.AddComponent<WorkshopUpgrades>();
            var serialized = new SerializedObject(upgrades);
            serialized.FindProperty("economy").objectReferenceValue = economy;
            SerializedProperty list = serialized.FindProperty("upgrades");
            list.arraySize = 3;
            SetUpgrade(list.GetArrayElementAtIndex(0), WorkshopUpgrades.PullSaw, "Japanese pull saw", "Japon çekme testere",
                "cuts 70 % faster", "%70 daha hızlı keser", 45f);
            SetUpgrade(list.GetArrayElementAtIndex(1), WorkshopUpgrades.BrushlessDrill, "Brushless drill-driver",
                "Fırçasız şarjlı matkap", "drills and drives 60 % faster", "%60 daha hızlı deler ve vidalar", 80f);
            SetUpgrade(list.GetArrayElementAtIndex(2), WorkshopUpgrades.LedLamp, "LED workshop lamp", "LED atölye lambası",
                "brighter bench light", "tezgahta daha parlak ışık", 30f);
            Light lamp = GameObject.Find("Environment/Workshop/WorkbenchLamp")?.GetComponent<Light>();
            SerializedProperty lamps = serialized.FindProperty("lamps");
            lamps.arraySize = lamp != null ? 1 : 0;
            if (lamp != null)
            {
                lamps.GetArrayElementAtIndex(0).objectReferenceValue = lamp;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();

            var sheetSize = new Vector3(0.21f, 0.297f, 0.002f);
            for (int i = 0; i < 3; i++)
            {
                float x = centerX + (i - 1f) * 0.3f;
                GameObject sheet = CreateBox($"UpgradeSheet_{i}", parent, new Vector3(x, rowY, wallZ - 0.021f),
                    sheetSize, materials.Paper);
                EnlargeGrabCollider(sheet, sheetSize);
                var upgradeSheet = sheet.AddComponent<UpgradeSheet>();
                SetReference(upgradeSheet, "upgrades", upgrades);
                var serializedSheet = new SerializedObject(upgradeSheet);
                serializedSheet.FindProperty("upgradeIndex").intValue = i;
                serializedSheet.ApplyModifiedPropertiesWithoutUndo();
            }

            HandSawTool saw = Object.FindFirstObjectByType<HandSawTool>();
            if (saw != null)
            {
                SetReference(saw, "upgrades", upgrades);
            }

            DrillDriverTool drill = Object.FindFirstObjectByType<DrillDriverTool>();
            if (drill != null)
            {
                SetReference(drill, "upgrades", upgrades);
            }
        }

        private static void SetUpgrade(SerializedProperty entry, string id, string name, string nameTurkish,
            string effect, string effectTurkish, float price)
        {
            entry.FindPropertyRelative("id").stringValue = id;
            entry.FindPropertyRelative("displayName").stringValue = name;
            entry.FindPropertyRelative("displayNameTurkish").stringValue = nameTurkish;
            entry.FindPropertyRelative("effect").stringValue = effect;
            entry.FindPropertyRelative("effectTurkish").stringValue = effectTurkish;
            entry.FindPropertyRelative("priceEuros").floatValue = price;
        }

        /// <summary>
        /// Two sawhorses (Phase 8c, D-044) between the timber rack and the workbench: 0.65 m high, beams across the
        /// room's Z axis 1.2 m apart, so a long board laid along Z rests on both. Boards are sawn at work height only
        /// (bench, sawhorses, table), not on the floor; the saw is told where the floor is.
        /// </summary>
        private static void BuildSawhorses(Transform parent, Material wood, Vector3 baseCenter, bool setSawFloor = true)
        {
            const float height = 0.65f;
            const float beamLength = 0.9f;
            const float beamWidth = 0.09f;
            const float beamThickness = 0.06f;
            const float legSize = 0.045f;
            const float centerX = 0f;
            var horses = new GameObject("Sawhorses").transform;
            horses.SetParent(parent, false);
            horses.localPosition = baseCenter;
            foreach (float z in new[] { -0.6f, 0.6f })
            {
                CreateBox("SawhorseBeam", horses, new Vector3(centerX, height - beamThickness / 2f, z),
                    new Vector3(beamLength, beamThickness, beamWidth), wood);
                float legHeight = height - beamThickness;
                foreach (float dx in new[] { -beamLength / 2f + 0.06f, beamLength / 2f - 0.06f })
                {
                    foreach (float dz in new[] { -0.12f, 0.12f })
                    {
                        CreateBox("SawhorseLeg", horses, new Vector3(centerX + dx, legHeight / 2f, z + dz),
                            new Vector3(legSize, legHeight, legSize), wood);
                    }
                }
            }

            HandSawTool saw = Object.FindFirstObjectByType<HandSawTool>();
            if (saw != null && setSawFloor)
            {
                var serialized = new SerializedObject(saw);
                serialized.FindProperty("floorY").floatValue = FloorTop;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        /// <summary>Placeholder feedback sounds around the player (Phase 8d, D-044); tool sounds live on the tools.</summary>
        private static void AddPlayerSounds(GameObject player)
        {
            var sounds = player.AddComponent<PlayerSounds>();
            SetReference(sounds, "carrier", player.GetComponent<PlayerCarrier>());
            SetReference(sounds, "toolBelt", player.GetComponent<PlayerToolBelt>());
            SetReference(sounds, "economy", Object.FindFirstObjectByType<WorkshopEconomy>());
        }

        /// <summary>
        /// Outdoor terrace site (Phase 10, D-046) on the ground in front of the workshop door: an assembly station
        /// for the terrace blueprint (origin = ground at the terrace centre), a thin site floor to aim at, a corner
        /// stake to set out the outline (string line), six pier spots to dig and gravel, a delivery area for terrace
        /// material and two sawhorses to cut it.
        /// </summary>
        private static void BuildTerraceSite(Transform environment, GreyboxMaterials materials)
        {
            var siteObject = new GameObject("TerraceSite");
            siteObject.transform.SetParent(environment, false);
            siteObject.transform.localPosition = new Vector3(0f, 0f, -6f);
            Transform site = siteObject.transform;

            // Thin site floor (child of the station): aiming at it offers the pier slots.
            CreateBox("SiteFloor", site, new Vector3(0f, 0.005f, 0f), new Vector3(2.8f, 0.01f, 2.2f), materials.Concrete);

            var preparation = siteObject.AddComponent<SitePreparation>();
            var spots = new Object[6];
            var bracketVisuals = new List<Object>();
            var bracketSlots = new List<string>();
            for (int i = 0; i < 6; i++)
            {
                Vector3 p = TerraceAssets.PierPosition(i);
                GameObject spot = CreateBox($"PierSpot_{i + 1}", site, new Vector3(p.x, 0.02f, p.z),
                    new Vector3(0.3f, 0.02f, 0.3f), materials.PencilMark);
                GameObject stake = CreateBox("Stake", spot.transform, new Vector3(0f, 6f, 0f), new Vector3(0.1f, 12f, 0.1f),
                    materials.PencilRed);
                Object.DestroyImmediate(stake.GetComponent<BoxCollider>());
                GameObject gravel = CreateBox("Gravel", spot.transform, new Vector3(0f, 0.5f, 0f), new Vector3(1.1f, 1f, 1.1f),
                    materials.Steel);
                Object.DestroyImmediate(gravel.GetComponent<BoxCollider>());
                gravel.SetActive(false);
                bracketVisuals.Add(BuildBeamBracket(site, new Vector3(p.x, TerraceAssets.PierTop, p.z), materials.Steel));
                bracketSlots.Add($"pier-{i + 1}");
                var siteSpot = spot.AddComponent<SiteSpot>();
                spot.AddComponent<PersistentId>().Set($"PierSpot_{i + 1}");
                var serializedSpot = new SerializedObject(siteSpot);
                serializedSpot.FindProperty("site").objectReferenceValue = preparation;
                serializedSpot.FindProperty("slotId").stringValue = $"pier-{i + 1}";
                serializedSpot.FindProperty("stakeVisual").objectReferenceValue = stake;
                serializedSpot.FindProperty("gravelVisual").objectReferenceValue = gravel;
                serializedSpot.ApplyModifiedPropertiesWithoutUndo();
                spots[i] = siteSpot;
            }

            // Corner stake and the string line around the outline (2.4 × 1.8 m).
            GameObject marker = CreateBox("CornerStake", site, new Vector3(-1.25f, 0.25f, -0.95f), new Vector3(0.05f, 0.5f, 0.05f),
                materials.PencilRed);
            EnlargeGrabCollider(marker, marker.transform.localScale);
            SetReference(marker.AddComponent<SiteMarker>(), "site", preparation); // default text: the terrace
            var lineObject = new GameObject("StringLine");
            lineObject.transform.SetParent(site, false);
            var line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = 4;
            line.SetPositions(new[]
            {
                new Vector3(-1.2f, 0.3f, -0.9f), new Vector3(1.2f, 0.3f, -0.9f), new Vector3(1.2f, 0.3f, 0.9f),
                new Vector3(-1.2f, 0.3f, 0.9f)
            });
            line.widthMultiplier = 0.006f;
            line.sharedMaterial = materials.CutGuide;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.enabled = false;
            SetReferences(preparation, "spots", spots);
            SetReference(preparation, "stringLine", line);

            // Angle brackets joist-to-beam (D-048), shown once the joist is set; one per joist and beam.
            for (int j = 0; j < TerraceAssets.JoistX.Length; j++)
            {
                foreach (float beamZ in TerraceAssets.BeamZ)
                {
                    bracketVisuals.Add(BuildAngleBracket(site, j, beamZ, materials.Steel));
                    bracketSlots.Add($"joist-{j + 1}");
                }
            }

            SetReferences(preparation, "bracketVisuals", bracketVisuals);
            var serializedPreparation = new SerializedObject(preparation);
            SerializedProperty slotsProperty = serializedPreparation.FindProperty("bracketSlots");
            slotsProperty.arraySize = bracketSlots.Count;
            for (int i = 0; i < bracketSlots.Count; i++)
            {
                slotsProperty.GetArrayElementAtIndex(i).stringValue = bracketSlots[i];
            }

            serializedPreparation.ApplyModifiedPropertiesWithoutUndo();

            var station = siteObject.AddComponent<AssemblyStation>();
            siteObject.AddComponent<PersistentId>().Set("TerraceSite");
            SetReference(station, "blueprint", TerraceAssets.Terrace());
            SetReference(station, "ghostMaterial", materials.BlueprintGhost);
            SetReference(station, "pointMarkerMaterial", materials.CutGuide);
            SetReference(station, "pilotPointMaterial", materials.PilotPoint);
            SetReference(station, "screwHeadMaterial", materials.Steel);
            SetReference(station, "holeMaterial", materials.PencilMark);
            SetReference(station, "waste", Object.FindFirstObjectByType<WorkshopWaste>());
            SetReferences(station, "knownBlueprints", new Object[] { TerraceAssets.Terrace() });
            SetReferences(station, "fasteners", Fasteners());
            var serializedStation = new SerializedObject(station);
            serializedStation.FindProperty("snapDistance").floatValue = 0.15f;
            serializedStation.ApplyModifiedPropertiesWithoutUndo();

            // Delivery area for terrace material: on the ground right of the site, boards along Z.
            var deliveryObject = new GameObject("OutdoorDelivery");
            deliveryObject.transform.SetParent(environment, false);
            deliveryObject.transform.localPosition = new Vector3(1.9f, 0f, -6f);
            deliveryObject.AddComponent<StockDelivery>();

            // Sawhorses left of the site, for cutting joists to length outside.
            BuildSawhorses(environment, materials.Workbench, new Vector3(-2.4f, 0f, -6f), setSawFloor: false);
        }

        /// <summary>
        /// Outdoor pergola site (Phase 11, D-049) right of the terrace: an assembly station for the pergola blueprint
        /// (origin = ground at the pergola centre), a thin site floor, a corner stake and string line, four pier spots,
        /// the steel post bases and angle brackets (shown with their parts), sawhorses to cut the posts, and the
        /// stepladder. Material comes to the outdoor delivery area beside the terrace.
        /// </summary>
        private static void BuildPergolaSite(Transform environment, GreyboxMaterials materials)
        {
            var siteObject = new GameObject("PergolaSite");
            siteObject.transform.SetParent(environment, false);
            siteObject.transform.localPosition = PergolaSitePosition;
            Transform site = siteObject.transform;

            CreateBox("SiteFloor", site, new Vector3(0f, 0.005f, 0f), new Vector3(3.4f, 0.01f, 3.4f), materials.Concrete);

            var preparation = siteObject.AddComponent<SitePreparation>();
            var spots = new Object[4];
            var bracketVisuals = new List<Object>();
            var bracketSlots = new List<string>();
            for (int i = 0; i < 4; i++)
            {
                Vector3 p = PergolaAssets.PostPosition(i);
                GameObject spot = CreateBox($"PierSpot_{i + 1}", site, new Vector3(p.x, 0.02f, p.z),
                    new Vector3(0.3f, 0.02f, 0.3f), materials.PencilMark);
                GameObject stake = CreateBox("Stake", spot.transform, new Vector3(0f, 6f, 0f), new Vector3(0.1f, 12f, 0.1f),
                    materials.PencilRed);
                Object.DestroyImmediate(stake.GetComponent<BoxCollider>());
                GameObject gravel = CreateBox("Gravel", spot.transform, new Vector3(0f, 0.5f, 0f), new Vector3(1.1f, 1f, 1.1f),
                    materials.Steel);
                Object.DestroyImmediate(gravel.GetComponent<BoxCollider>());
                gravel.SetActive(false);

                // Steel U post base, anchored in the pier block: shown with the block (D-049).
                bracketVisuals.Add(BuildBeamBracket(site, new Vector3(p.x, PergolaAssets.PierTop, p.z), materials.Steel,
                    PergolaAssets.AnchorInner, PergolaAssets.AnchorFlangeHeight));
                bracketSlots.Add($"pier-{i + 1}");

                var siteSpot = spot.AddComponent<SiteSpot>();
                spot.AddComponent<PersistentId>().Set($"PergolaPierSpot_{i + 1}");
                var serializedSpot = new SerializedObject(siteSpot);
                serializedSpot.FindProperty("site").objectReferenceValue = preparation;
                serializedSpot.FindProperty("slotId").stringValue = $"pier-{i + 1}";
                serializedSpot.FindProperty("stakeVisual").objectReferenceValue = stake;
                serializedSpot.FindProperty("gravelVisual").objectReferenceValue = gravel;
                serializedSpot.ApplyModifiedPropertiesWithoutUndo();
                spots[i] = siteSpot;

                // Angle bracket post-to-beam: foot on the post top inside the beam, shown with the beam.
                float side = PergolaAssets.Inward(p.z);
                bracketVisuals.Add(BuildAngleBracket(site, $"AngleBracket_post_{i + 1}",
                    new Vector3(p.x, PergolaAssets.PostTop, p.z + side * 0.0225f), Vector3.forward * side, materials.Steel));
                bracketSlots.Add(PergolaAssets.BeamIds[PergolaAssets.BeamOf(i)]);
            }

            for (int r = 0; r < PergolaAssets.RafterX.Length; r++)
            {
                float side = PergolaAssets.RafterSide(r);
                foreach (float beamZ in PergolaAssets.PostZ)
                {
                    bracketVisuals.Add(BuildAngleBracket(site, $"AngleBracket_rafter_{r + 1}",
                        new Vector3(PergolaAssets.RafterX[r] + side * 0.0225f, PergolaAssets.BeamTop, beamZ),
                        Vector3.right * side, materials.Steel));
                    bracketSlots.Add($"rafter-{r + 1}");
                }
            }

            // Corner stake and the string line through the post centres (2.5 × 2.5 m).
            GameObject marker = CreateBox("CornerStake", site, new Vector3(-1.55f, 0.25f, -1.55f), new Vector3(0.05f, 0.5f, 0.05f),
                materials.PencilRed);
            EnlargeGrabCollider(marker, marker.transform.localScale);
            var siteMarker = marker.AddComponent<SiteMarker>();
            SetReference(siteMarker, "site", preparation);
            var serializedMarker = new SerializedObject(siteMarker);
            serializedMarker.FindProperty("outline").stringValue = "the pergola (3 × 3 m)";
            serializedMarker.FindProperty("outlineTurkish").stringValue = "pergolayı (3 × 3 m)";
            serializedMarker.ApplyModifiedPropertiesWithoutUndo();
            var lineObject = new GameObject("StringLine");
            lineObject.transform.SetParent(site, false);
            var line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = 4;
            line.SetPositions(new[]
            {
                new Vector3(-1.25f, 0.3f, -1.25f), new Vector3(1.25f, 0.3f, -1.25f), new Vector3(1.25f, 0.3f, 1.25f),
                new Vector3(-1.25f, 0.3f, 1.25f)
            });
            line.widthMultiplier = 0.006f;
            line.sharedMaterial = materials.CutGuide;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.enabled = false;

            var serializedPreparation = new SerializedObject(preparation);
            SetArray(serializedPreparation.FindProperty("spots"), spots);
            serializedPreparation.FindProperty("stringLine").objectReferenceValue = line;
            SetArray(serializedPreparation.FindProperty("bracketVisuals"), bracketVisuals);
            SerializedProperty slotsProperty = serializedPreparation.FindProperty("bracketSlots");
            slotsProperty.arraySize = bracketSlots.Count;
            for (int i = 0; i < bracketSlots.Count; i++)
            {
                slotsProperty.GetArrayElementAtIndex(i).stringValue = bracketSlots[i];
            }

            (string prefix, string name, string nameTr)[] stages =
            {
                ("pier", "foundation (pier blocks)", "temel (beton ayaklar)"),
                ("post", "posts (plumb and brace each)", "direkler (her birini şakülüne getir, destekle)"),
                ("beam", "beams", "kirişler"),
                ("rafter", "rafters", "aşıklar")
            };
            SerializedProperty stagesProperty = serializedPreparation.FindProperty("stages");
            stagesProperty.arraySize = stages.Length;
            for (int s = 0; s < stages.Length; s++)
            {
                SerializedProperty stage = stagesProperty.GetArrayElementAtIndex(s);
                stage.FindPropertyRelative("slotPrefix").stringValue = stages[s].prefix;
                stage.FindPropertyRelative("name").stringValue = stages[s].name;
                stage.FindPropertyRelative("nameTurkish").stringValue = stages[s].nameTr;
            }

            serializedPreparation.ApplyModifiedPropertiesWithoutUndo();

            var station = siteObject.AddComponent<AssemblyStation>();
            siteObject.AddComponent<PersistentId>().Set("PergolaSite");
            SetReference(station, "blueprint", PergolaAssets.Pergola());
            SetReference(station, "ghostMaterial", materials.BlueprintGhost);
            SetReference(station, "pointMarkerMaterial", materials.CutGuide);
            SetReference(station, "pilotPointMaterial", materials.PilotPoint);
            SetReference(station, "screwHeadMaterial", materials.Steel);
            SetReference(station, "holeMaterial", materials.PencilMark);
            SetReference(station, "braceMaterial", materials.Lumber);
            SetReference(station, "waste", Object.FindFirstObjectByType<WorkshopWaste>());
            SetReferences(station, "knownBlueprints", new Object[] { PergolaAssets.Pergola() });
            SetReferences(station, "fasteners", Fasteners());
            var serializedStation = new SerializedObject(station);
            serializedStation.FindProperty("snapDistance").floatValue = 0.15f;
            serializedStation.ApplyModifiedPropertiesWithoutUndo();

            // Sawhorses to cut the posts, between the delivery area and the pergola.
            BuildSawhorses(environment, materials.Workbench, PergolaSitePosition + new Vector3(-2.6f, 0f, -2.6f), setSawFloor: false);
            BuildStepladder(environment, materials, PergolaSitePosition + new Vector3(-2.0f, 0f, 1.6f));
        }

        private static readonly Vector3 PergolaSitePosition = new Vector3(6.5f, 0f, -7f);

        private static void SetArray(SerializedProperty property, IReadOnlyList<Object> values)
        {
            property.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
        }

        /// <summary>
        /// Portable A-frame stepladder (D-049, D-050), 2.0 m high with its top standing step at 1.45 m (the rails above it
        /// are the hand hold); the steps face −Z.
        /// One box collider is its footprint (carried and placed like a board); rails, legs and steps are visual only.
        /// </summary>
        private static void BuildStepladder(Transform parent, GreyboxMaterials materials, Vector3 groundPosition)
        {
            const float width = 0.5f;
            const float height = 2.0f;
            const float depth = 1.0f;
            var ladder = new GameObject("Stepladder");
            ladder.transform.SetParent(parent, false);
            ladder.transform.localPosition = groundPosition + Vector3.up * 0.001f;

            var footprint = ladder.AddComponent<BoxCollider>();
            footprint.size = new Vector3(width, height, depth);
            footprint.center = new Vector3(0f, height / 2f, 0f);
            var body = ladder.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.mass = 7f;
            ladder.AddComponent<Carryable>();
            ladder.AddComponent<Stepladder>();
            ladder.AddComponent<PersistentId>().Set("Stepladder");

            var visuals = new List<GameObject>();
            foreach (float x in new[] { -width / 2f + 0.03f, width / 2f - 0.03f })
            {
                // Front rails (steps side) and rear legs meet under the top cap.
                visuals.Add(CreateStrut("Rail", ladder.transform, new Vector3(x, 0f, -depth / 2f), new Vector3(x, height - 0.04f, -0.06f),
                    new Vector2(0.025f, 0.06f), materials.Steel));
                visuals.Add(CreateStrut("Leg", ladder.transform, new Vector3(x, 0f, depth / 2f), new Vector3(x, height - 0.04f, 0.06f),
                    new Vector2(0.025f, 0.04f), materials.Steel));
            }

            foreach (float y in new[] { 0.37f, 0.73f, 1.09f, 1.45f })
            {
                float z = -depth / 2f + (depth / 2f - 0.06f) * (y / (height - 0.04f));
                visuals.Add(CreateBox("Step", ladder.transform, new Vector3(0f, y - 0.0125f, z), new Vector3(width - 0.06f, 0.025f, 0.12f),
                    materials.ToolYellow));
            }

            visuals.Add(CreateBox("TopCap", ladder.transform, new Vector3(0f, height - 0.02f, 0f), new Vector3(width, 0.04f, 0.2f),
                materials.ToolYellow));
            foreach (GameObject visual in visuals)
            {
                Object.DestroyImmediate(visual.GetComponent<BoxCollider>());
            }
        }

        /// <summary>
        /// Offcut bins (D-052): one per timber species, in a row along the front wall right of the door. Each is a
        /// plastic bin with a band in the colour of its species; a pile inside grows with the pieces; the apprentice
        /// stands at its service point (in front, room side) to put a piece in. The board factory is assigned later.
        /// </summary>
        private static void BuildOffcutBins(Transform parent, GreyboxMaterials materials,
            IReadOnlyDictionary<string, WoodSpeciesDefinition> species)
        {
            const float width = 0.38f;
            const float height = 0.5f;
            const float depth = 0.45f;
            const float wall = 0.015f;
            var timber = new List<WoodSpeciesDefinition>();
            foreach (WoodSpeciesDefinition definition in species.Values)
            {
                if (definition != null && definition.ToDomain().IsTimber)
                {
                    timber.Add(definition);
                }
            }

            timber.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            var bins = new GameObject("OffcutBins").transform;
            bins.SetParent(parent, false);
            float z = -InteriorDepth / 2f + depth / 2f + 0.03f;
            for (int i = 0; i < timber.Count; i++)
            {
                WoodSpeciesDefinition definition = timber[i];
                var bin = new GameObject($"OffcutBin_{definition.Id}");
                bin.transform.SetParent(bins, false);
                bin.transform.localPosition = new Vector3(1.0f + i * 0.44f, FloorTop, z);

                var footprint = bin.AddComponent<BoxCollider>();
                footprint.size = new Vector3(width, height, depth);
                footprint.center = new Vector3(0f, height / 2f, 0f);

                Material colour = materials.Colored($"Offcut_{definition.Id}", definition.GreyboxColor);
                var parts = new List<GameObject>
                {
                    CreateBox("Bottom", bin.transform, new Vector3(0f, wall / 2f, 0f), new Vector3(width, wall, depth), materials.BinPlastic),
                    CreateBox("Front", bin.transform, new Vector3(0f, height / 2f, depth / 2f - wall / 2f), new Vector3(width, height, wall), materials.BinPlastic),
                    CreateBox("Back", bin.transform, new Vector3(0f, height / 2f, -depth / 2f + wall / 2f), new Vector3(width, height, wall), materials.BinPlastic),
                    CreateBox("Left", bin.transform, new Vector3(-width / 2f + wall / 2f, height / 2f, 0f), new Vector3(wall, height, depth), materials.BinPlastic),
                    CreateBox("Right", bin.transform, new Vector3(width / 2f - wall / 2f, height / 2f, 0f), new Vector3(wall, height, depth), materials.BinPlastic),
                    // Species band on the front (room side), so the bins are told apart at a glance.
                    CreateBox("Band", bin.transform, new Vector3(0f, height - 0.07f, depth / 2f + 0.002f), new Vector3(width - 0.04f, 0.08f, 0.004f), colour)
                };
                GameObject pile = CreateBox("Pile", bin.transform, new Vector3(0f, 0.04f, 0f),
                    new Vector3(width - 2f * wall - 0.02f, 0.02f, depth - 2f * wall - 0.02f), colour);
                parts.Add(pile);
                foreach (GameObject part in parts)
                {
                    Object.DestroyImmediate(part.GetComponent<BoxCollider>());
                }

                var servicePoint = new GameObject("ServicePoint").transform;
                servicePoint.SetParent(bin.transform, false);
                servicePoint.localPosition = new Vector3(0f, 0f, depth / 2f + 0.45f);

                var offcutBin = bin.AddComponent<OffcutBin>();
                SetReference(offcutBin, "species", definition);
                SetReference(offcutBin, "pile", pile.transform);
                SetReference(offcutBin, "servicePoint", servicePoint);
                bin.AddComponent<PersistentId>().Set($"OffcutBin_{definition.Id}");
            }
        }

        /// <summary>
        /// The apprentice (D-052): a greybox figure (overalls, head, cap) standing near the offcut bins. One capsule
        /// collider (aimed at for E, blocks like a person); its NavMesh agent starts off and is switched on once the
        /// runtime walkable area exists. Carries a piece at hip height along his forward direction.
        /// </summary>
        private static void BuildApprentice(Transform parent, GreyboxMaterials materials)
        {
            var root = new GameObject("Apprentice");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = new Vector3(2.4f, FloorTop, -1.7f);
            root.transform.localRotation = Quaternion.LookRotation(new Vector3(-1f, 0f, 1f));

            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0f, 0.85f, 0f);
            capsule.height = 1.7f;
            capsule.radius = 0.25f;
            var body = root.AddComponent<Rigidbody>();
            body.isKinematic = true;

            var agent = root.AddComponent<UnityEngine.AI.NavMeshAgent>();
            agent.radius = 0.3f;
            agent.height = 1.7f;
            agent.speed = 2.0f;
            agent.angularSpeed = 400f;
            agent.acceleration = 8f;
            agent.stoppingDistance = 0.3f;
            agent.enabled = false;

            var visuals = new List<GameObject>
            {
                CreateBox("Legs", root.transform, new Vector3(0f, 0.42f, 0f), new Vector3(0.32f, 0.84f, 0.2f), materials.Overalls),
                CreateBox("Torso", root.transform, new Vector3(0f, 1.15f, 0f), new Vector3(0.42f, 0.62f, 0.24f), materials.Overalls),
                CreateBox("ArmLeft", root.transform, new Vector3(-0.27f, 1.08f, 0f), new Vector3(0.1f, 0.6f, 0.12f), materials.Overalls),
                CreateBox("ArmRight", root.transform, new Vector3(0.27f, 1.08f, 0f), new Vector3(0.1f, 0.6f, 0.12f), materials.Overalls),
                CreateBox("Cap", root.transform, new Vector3(0f, 1.71f, 0.02f), new Vector3(0.22f, 0.06f, 0.26f), materials.ToolYellow)
            };
            GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            head.transform.SetParent(root.transform, false);
            head.transform.localPosition = new Vector3(0f, 1.58f, 0f);
            head.transform.localScale = Vector3.one * 0.22f;
            head.GetComponent<Renderer>().sharedMaterial = materials.Skin;
            Object.DestroyImmediate(head.GetComponent<SphereCollider>());
            foreach (GameObject visual in visuals)
            {
                Object.DestroyImmediate(visual.GetComponent<BoxCollider>());
            }

            var carryPoint = new GameObject("CarryPoint").transform;
            carryPoint.SetParent(root.transform, false);
            carryPoint.localPosition = new Vector3(0.38f, 0.95f, 0f);

            var apprentice = root.AddComponent<Apprentice>();
            SetReference(apprentice, "carryPoint", carryPoint);
            SetReference(apprentice, "jobBoard", Object.FindFirstObjectByType<JobBoard>());
            SetReference(apprentice, "economy", Object.FindFirstObjectByType<WorkshopEconomy>());
        }

        /// <summary>A box from <paramref name="from"/> to <paramref name="to"/> with the given cross-section (x, y).</summary>
        private static GameObject CreateStrut(string name, Transform parent, Vector3 from, Vector3 to, Vector2 section, Material material)
        {
            Vector3 along = to - from;
            GameObject strut = CreateBox(name, parent, (from + to) / 2f, new Vector3(section.x, section.y, along.magnitude), material);
            strut.transform.localRotation = Quaternion.LookRotation(along, Vector3.Cross(along, Vector3.right));
            return strut;
        }

        /// <summary>
        /// Steel U beam bracket on top of a pier block (D-048): a base plate and two side flanges 45 mm apart, open
        /// along X so the beam on edge sits in it. Visual only (no collider); shown while the block is set.
        /// </summary>
        private static GameObject BuildBeamBracket(Transform site, Vector3 basePosition, Material steel,
            float inner = 0.045f, float flangeHeight = TerraceAssets.BracketFlangeHeight)
        {
            const float length = 0.1f;
            const float sheet = TerraceAssets.BracketSheet;
            var bracket = new GameObject("UBracket").transform;
            bracket.SetParent(site, false);
            bracket.localPosition = basePosition;

            var parts = new[]
            {
                CreateBox("BasePlate", bracket, new Vector3(0f, sheet / 2f, 0f), new Vector3(length, sheet, inner + 2f * sheet), steel),
                CreateBox("Flange", bracket, new Vector3(0f, flangeHeight / 2f, -(inner + sheet) / 2f),
                    new Vector3(length, flangeHeight, sheet), steel),
                CreateBox("Flange", bracket, new Vector3(0f, flangeHeight / 2f, (inner + sheet) / 2f),
                    new Vector3(length, flangeHeight, sheet), steel)
            };
            foreach (GameObject part in parts)
            {
                Object.DestroyImmediate(part.GetComponent<BoxCollider>());
            }

            bracket.gameObject.SetActive(false);
            return bracket.gameObject;
        }

        /// <summary>
        /// Steel angle bracket joist-to-beam (D-048): the foot lies on the beam top beside the joist, the upright leg
        /// against the joist's side towards the terrace centre (the screw point is on it). Visual only (no collider).
        /// </summary>
        private static GameObject BuildAngleBracket(Transform site, int joist, float beamZ, Material steel)
        {
            float side = TerraceAssets.AngleBracketSide(joist);
            // Origin: the joist's side face at the beam top, on the beam's centre line.
            return BuildAngleBracket(site, $"AngleBracket_{joist + 1}",
                new Vector3(TerraceAssets.JoistX[joist] + side * 0.0225f, TerraceAssets.BeamTop, beamZ), Vector3.right * side, steel);
        }

        /// <summary>
        /// Steel angle bracket (D-048): <paramref name="origin"/> is the upper member's side face at the lower member's
        /// top; the foot lies on the lower member towards <paramref name="side"/> (horizontal unit vector), the upright
        /// leg stands against the upper member. Visual only (no collider), hidden until its part is set.
        /// </summary>
        private static GameObject BuildAngleBracket(Transform site, string name, Vector3 origin, Vector3 side, Material steel)
        {
            const float leg = TerraceAssets.AngleLeg;
            const float sheet = TerraceAssets.BracketSheet;
            var bracket = new GameObject(name).transform;
            bracket.SetParent(site, false);
            bracket.localPosition = origin;
            // Local X = side (LookRotation: right = up × forward).
            bracket.localRotation = Quaternion.LookRotation(Vector3.Cross(side, Vector3.up), Vector3.up);

            var parts = new[]
            {
                CreateBox("Foot", bracket, new Vector3(leg / 2f, sheet / 2f, 0f), new Vector3(leg, sheet, leg), steel),
                CreateBox("Upright", bracket, new Vector3(sheet / 2f, leg / 2f, 0f), new Vector3(sheet, leg, leg), steel)
            };
            foreach (GameObject part in parts)
            {
                Object.DestroyImmediate(part.GetComponent<BoxCollider>());
            }

            bracket.gameObject.SetActive(false);
            return bracket.gameObject;
        }

        private static Object[] Fasteners() => new Object[]
        {
            BlueprintAssets.WoodScrew5x80(), BlueprintAssets.WoodScrew4x40(), BlueprintAssets.Nail2_8x50(), BlueprintAssets.Dowel8x40(),
            TerraceAssets.DeckScrew(), TerraceAssets.JoistHanger(), TerraceAssets.PostBase(), PergolaAssets.PostAnchor()
        };

        private static void SetReferences(Object target, string propertyName, IReadOnlyList<Object> values)
        {
            var serializedObject = new SerializedObject(target);
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            property.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Small items at the front of the workbench: tape measure, carpenter's pencil, cut list and hand saw
        /// (placeholder boxes with real-world sizes).
        /// </summary>
        private static void BuildBenchTools(Transform parent, GreyboxMaterials materials)
        {
            var tools = new GameObject("Tools").transform;
            tools.SetParent(parent, false);

            // Records the sawdust of every cut (shared by all saws).
            var waste = new GameObject("WorkshopWaste").AddComponent<WorkshopWaste>();
            waste.transform.SetParent(parent, false);

            float benchTop = FloorTop + 0.9f;
            float frontZ = InteriorDepth / 2f - 0.5f - 0.3f; // 10 cm behind the bench's front edge

            // Tape measure housing ≈ 70 × 70 × 35 mm, lying on its side; held upright in the hand.
            GameObject tape = CreatePickupItem("TapeMeasure", tools, ItemAssets.TapeMeasure(), new Vector3(0.07f, 0.035f, 0.07f),
                new Vector3(-0.6f, benchTop, frontZ), 0f, materials.ToolYellow,
                handPosition: new Vector3(0.15f, -0.12f, 0.36f), handRotation: new Vector3(0f, -20f, 90f));
            var tapeTool = tape.AddComponent<TapeMeasureTool>();
            SetReference(tapeTool, "blade", CreateTapeBlade(tape, materials.TapeBlade));

            // Cut list: an A5 sheet (148 × 210 mm) at the front-left of the bench with the target dimension.
            GameObject note = CreateBox("CutList", tools, new Vector3(-0.85f, benchTop + 0.002f, frontZ + 0.02f),
                new Vector3(0.148f, 0.002f, 0.21f), materials.Paper);
            EnlargeGrabCollider(note, note.transform.localScale);
            var cutList = note.AddComponent<CutList>();
            note.AddComponent<PersistentId>().Set("CutList");

            // Carpenter's pencil ≈ 175 × 12 × 7 mm (flat), lying across the bench; held pointing forward.
            GameObject pencil = CreatePickupItem("CarpenterPencil", tools, ItemAssets.CarpenterPencil(),
                new Vector3(0.012f, 0.007f, 0.175f), new Vector3(-0.35f, benchTop, frontZ), 90f, materials.PencilRed,
                handPosition: new Vector3(0.12f, -0.1f, 0.3f), handRotation: new Vector3(10f, -15f, 0f));
            var pencilTool = pencil.AddComponent<PencilTool>();
            SetReference(pencilTool, "blade", CreateTapeBlade(pencil, materials.TapeBlade));
            SetReference(pencilTool, "markPreview", CreateLine(pencil, "MarkPreview", materials.MarkPreview, 0.0015f));
            SetReference(pencilTool, "cutList", cutList);

            // Hand saw: placeholder box for a ~500 mm blade (120 mm tall, 4 mm thick for visibility), lying flat on the
            // bench; held upright, pointing forward and slightly down.
            GameObject saw = CreatePickupItem("HandSaw", tools, ItemAssets.HandSaw(), new Vector3(0.12f, 0.004f, 0.5f),
                new Vector3(0f, benchTop, frontZ - 0.03f), 90f, materials.Steel,
                handPosition: new Vector3(0.2f, -0.24f, 0.5f), handRotation: new Vector3(15f, -5f, 90f));
            var sawTool = saw.AddComponent<HandSawTool>();
            SetReference(sawTool, "definition", SawAssets.HandSaw());
            SetReference(sawTool, "waste", waste);
            SetReference(sawTool, "cutList", cutList);
            SetReference(sawTool, "cutGuide", CreateLine(saw, "CutGuide", materials.CutGuide, 0.003f));
        }

        /// <summary>
        /// The extended tape blade drawn by a tool (LineRenderer on a child: one Renderer per GameObject). The pencil
        /// has its own, standing for the tape laid along the board while marking.
        /// </summary>
        private static LineRenderer CreateTapeBlade(GameObject tool, Material bladeMaterial) =>
            CreateLine(tool, "TapeBlade", bladeMaterial, 0.016f); // ~16 mm wide blade (real tapes: 16–32 mm)

        /// <summary>A camera-facing 2-point line on a child object (hidden until the tool shows it).</summary>
        private static LineRenderer CreateLine(GameObject tool, string name, Material material, float width)
        {
            var bladeObject = new GameObject(name);
            bladeObject.transform.SetParent(tool.transform, false);

            var blade = bladeObject.AddComponent<LineRenderer>();
            blade.useWorldSpace = true;
            blade.positionCount = 2;
            blade.widthMultiplier = width;
            blade.alignment = LineAlignment.View; // always faces the camera, independent of the hand's rotation
            blade.sharedMaterial = material;
            blade.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            blade.receiveShadows = false;
            blade.enabled = false;
            return blade;
        }

        /// <summary>
        /// Grab area larger than the visual: at least MinGrabSize in every direction, so a 7 mm pencil or a sheet of
        /// paper is easy to aim at. BoxCollider.size is in local units, i.e. relative to the scaled cube.
        /// </summary>
        private static void EnlargeGrabCollider(GameObject box, Vector3 size)
        {
            box.GetComponent<BoxCollider>().size = new Vector3(
                Mathf.Max(1f, MinGrabSize / size.x),
                Mathf.Max(1f, MinGrabSize / size.y),
                Mathf.Max(1f, MinGrabSize / size.z));
        }

        private static GameObject CreatePickupItem(string name, Transform parent, ItemDefinition definition, Vector3 size,
            Vector3 surfacePosition, float yawDegrees, Material material, Vector3 handPosition, Vector3 handRotation)
        {
            // Placeholder geometry: a scaled cube is acceptable for small props (unlike timber, D-016).
            GameObject item = CreateBox(name, parent, surfacePosition + Vector3.up * (size.y / 2f + 0.001f), size, material);
            item.transform.localRotation = Quaternion.Euler(0f, yawDegrees, 0f);

            EnlargeGrabCollider(item, size);

            var pickup = item.AddComponent<PickupItem>();
            item.AddComponent<PersistentId>().Set(name);
            var serialized = new SerializedObject(pickup);
            serialized.FindProperty("definition").objectReferenceValue = definition;
            serialized.FindProperty("handPosition").vector3Value = handPosition;
            serialized.FindProperty("handRotation").vector3Value = handRotation;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return item;
        }

        /// <summary>
        /// Assembly table (1.2 × 0.8 m, 0.75 m high) on the right side of the room with the Phase 5 frame blueprint.
        /// The <see cref="AssemblyStation"/> sits on the root, whose origin is the table top center: the table's
        /// colliders are its children, so aiming at the table finds the station (D-033).
        /// </summary>
        private static void BuildAssemblyTable(Transform parent, GreyboxMaterials materials)
        {
            const float length = 1.2f;
            const float depth = 1.0f;
            const float surfaceHeight = 0.75f;
            const float topThickness = 0.04f;
            const float legSize = 0.06f;
            const float legInset = 0.06f;

            var table = new GameObject("AssemblyTable").transform;
            table.SetParent(parent, false);
            table.localPosition = new Vector3(2.6f, FloorTop + surfaceHeight, 0.5f);

            CreateBox("Top", table, new Vector3(0f, -topThickness / 2f, 0f),
                new Vector3(length, topThickness, depth), materials.Workbench);

            float legHeight = surfaceHeight - topThickness;
            float legX = length / 2f - legInset;
            float legZ = depth / 2f - legInset;
            for (int i = 0; i < 4; i++)
            {
                float x = (i % 2 == 0) ? -legX : legX;
                float z = (i < 2) ? -legZ : legZ;
                CreateBox($"Leg_{i + 1}", table, new Vector3(x, -topThickness - legHeight / 2f, z),
                    new Vector3(legSize, legHeight, legSize), materials.Workbench);
            }

            var station = table.gameObject.AddComponent<AssemblyStation>();
            table.gameObject.AddComponent<PersistentId>().Set("AssemblyTable");
            SetReference(station, "blueprint", BlueprintAssets.Frame600x300());
            SetReference(station, "ghostMaterial", materials.BlueprintGhost);
            SetReference(station, "pointMarkerMaterial", materials.CutGuide);
            SetReference(station, "pilotPointMaterial", materials.PilotPoint);
            SetReference(station, "screwHeadMaterial", materials.Steel);
            SetReference(station, "holeMaterial", materials.PencilMark);
            SetReference(station, "waste", Object.FindFirstObjectByType<WorkshopWaste>());
            SetReferences(station, "knownBlueprints",
                new Object[] { BlueprintAssets.Frame600x300(), BlueprintAssets.WallShelf(), ToteAssets.ToolTote() });
            SetReferences(station, "fasteners", Fasteners());

            // Cordless drill-driver lying on the table beside the frame (placeholder box: 200 mm tall body,
            // 70 mm wide, 160 mm long), held upright and pointing forward. Not a child of the station, so aiming at
            // it is aiming at a pickup, not at the assembly.
            GameObject drill = CreatePickupItem("DrillDriver", parent, ItemAssets.DrillDriver(),
                new Vector3(0.2f, 0.07f, 0.16f), table.localPosition + new Vector3(0.45f, 0f, -0.27f), 0f,
                materials.DrillTeal, handPosition: new Vector3(0.2f, -0.2f, 0.42f), handRotation: new Vector3(0f, -8f, 90f));
            var drillTool = drill.AddComponent<DrillDriverTool>();
            SetReferences(drillTool, "fasteners", Fasteners());
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
            switchObject.AddComponent<PersistentId>().Set("LightSwitch");
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
            const float depth = 1.0f;
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

        /// <summary>
        /// Wall-mounted timber rack along the left wall: steel uprights and arms carrying continuous shelves
        /// (continuous so a 2.4 m board can be aimed at anywhere, not only at a 5 cm arm). Placement onto the
        /// shelves uses the normal carry/placement system; the uprights act as obstacles.
        /// </summary>
        private static void BuildTimberRack(Transform parent, Material steel, Material shelf)
        {
            var rack = new GameObject("TimberRack").transform;
            rack.SetParent(parent, false);
            rack.gameObject.AddComponent<KeepPiecesHere>(); // the apprentice leaves timber on the rack alone (D-052)

            float wallX = -InteriorWidth / 2f;
            float shelfCenterX = RackFrontOfUprightsX + RackDepth / 2f;
            float uprightHeight = RackShelfTops[RackShelfTops.Length - 1] + 0.25f;

            for (int u = 0; u < RackUprightZ.Length; u++)
            {
                CreateBox($"Upright_{u + 1}", rack,
                    new Vector3(wallX + RackUprightSize / 2f, FloorTop + uprightHeight / 2f, RackUprightZ[u]),
                    new Vector3(RackUprightSize, uprightHeight, RackUprightSize), steel);
            }

            for (int level = 0; level < RackShelfTops.Length; level++)
            {
                float top = FloorTop + RackShelfTops[level];
                CreateBox($"Shelf_{level + 1}", rack,
                    new Vector3(shelfCenterX, top - RackShelfThickness / 2f, 0f),
                    new Vector3(RackDepth, RackShelfThickness, RackShelfLength), shelf);

                for (int u = 0; u < RackUprightZ.Length; u++)
                {
                    CreateBox($"Arm_{level + 1}_{u + 1}", rack,
                        new Vector3(shelfCenterX, top - RackShelfThickness - RackArmHeight / 2f, RackUprightZ[u]),
                        new Vector3(RackDepth, RackArmHeight, 0.05f), steel);
                }
            }
        }

        private static void BuildTimber(Transform parent, GreyboxMaterials materials,
            IReadOnlyDictionary<string, WoodSpeciesDefinition> species)
        {
            var timber = new GameObject("Timber").transform;
            timber.SetParent(parent, false);

            // Long construction timber stored on the rack (length along Z). Top shelf is left empty for the player.
            float lowerShelf = FloorTop + RackShelfTops[0];
            for (int i = 0; i < 3; i++)
            {
                CreateBoard($"Spruce_2400x95x45_{i + 1}", timber, species["spruce"], 2400f, 95f, 45f,
                    new Vector3(RackFrontOfUprightsX + 0.07f + i * 0.13f, lowerShelf, 0f), 0f, materials.Lumber, materials.PencilMark);
            }

            float middleShelf = FloorTop + RackShelfTops[1];
            CreateBoard("Larch_2000x145x28", timber, species["larch"], 2000f, 145f, 28f,
                new Vector3(RackFrontOfUprightsX + 0.10f, middleShelf, 0f), 0f, materials.Lumber, materials.PencilMark);
            CreateBoard("DouglasFir_2400x145x45", timber, species["douglas-fir"], 2400f, 145f, 45f,
                new Vector3(RackFrontOfUprightsX + 0.30f, middleShelf, 0f), 0f, materials.Lumber, materials.PencilMark);

            // Smaller boards on the workbench (length along X, yaw 90°). Bench surface: FloorTop + 0.9 m.
            float benchTop = FloorTop + 0.9f;
            float benchZ = InteriorDepth / 2f - 0.5f;
            CreateBoard("Pine_1000x200x18", timber, species["pine"], 1000f, 200f, 18f,
                new Vector3(-0.45f, benchTop, benchZ - 0.05f), 90f, materials.Lumber, materials.PencilMark);
            CreateBoard("Oak_600x120x22", timber, species["oak"], 600f, 120f, 22f,
                new Vector3(0.5f, benchTop, benchZ - 0.2f), 90f, materials.Lumber, materials.PencilMark);
            CreateBoard("Walnut_400x100x25", timber, species["walnut"], 400f, 100f, 25f,
                new Vector3(0.5f, benchTop, benchZ - 0.02f), 90f, materials.Lumber, materials.PencilMark);
            CreateBoard("Beech_500x45x45", timber, species["beech"], 500f, 45f, 45f,
                new Vector3(0.5f, benchTop, benchZ + 0.12f), 90f, materials.Lumber, materials.PencilMark);
        }

        /// <summary>
        /// Creates a <see cref="TimberBoard"/> resting on a surface. <paramref name="surfacePosition"/> is the point
        /// on the supporting surface below the board's center.
        /// </summary>
        private static void CreateBoard(string name, Transform parent, WoodSpeciesDefinition species,
            float lengthMm, float widthMm, float thicknessMm, Vector3 surfacePosition, float yawDegrees, Material material,
            Material markMaterial)
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
            // Resting objects are kinematic: they stay exactly where they were placed (D-019).
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            board.AddComponent<Carryable>();
            // Stable save id = the builder's board name (D-036).
            board.AddComponent<PersistentId>().Set(name);
            var timberBoard = board.AddComponent<TimberBoard>();
            var serialized = new SerializedObject(timberBoard);
            serialized.FindProperty("species").objectReferenceValue = species;
            serialized.FindProperty("lengthMillimeters").floatValue = lengthMm;
            serialized.FindProperty("widthMillimeters").floatValue = widthMm;
            serialized.FindProperty("thicknessMillimeters").floatValue = thicknessMm;
            serialized.FindProperty("markMaterial").objectReferenceValue = markMaterial;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            // Generate mesh/collider now so the board is visible in the Editor; Awake regenerates at runtime.
            timberBoard.Rebuild();
        }

        private static GameObject BuildPlayer(Material placementPreviewMaterial)
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

            var carrier = player.AddComponent<PlayerCarrier>();
            SetReference(carrier, "cameraTransform", cameraObject.transform);
            SetReference(carrier, "interactor", interactor);
            SetReference(carrier, "previewMaterial", placementPreviewMaterial);

            var toolBelt = player.AddComponent<PlayerToolBelt>();
            SetReference(toolBelt, "cameraTransform", cameraObject.transform);
            SetReference(toolBelt, "carrier", carrier);

            return player;
        }

        private static void BuildHud(PlayerInteractor interactor, PlayerCarrier carrier, VisualTreeAsset layout,
            PanelSettings panelSettings)
        {
            var hudObject = new GameObject("HUD");

            var document = hudObject.AddComponent<UIDocument>();
            document.panelSettings = panelSettings;
            document.visualTreeAsset = layout;

            var hud = hudObject.AddComponent<GameplayHud>();
            SetReference(hud, "interactor", interactor);
            SetReference(hud, "carrier", carrier);
            SetReference(hud, "toolBelt", carrier.GetComponent<PlayerToolBelt>());

            // Completion reports of every assembly station in the scene.
            AssemblyStation[] stations = Object.FindObjectsByType<AssemblyStation>(FindObjectsSortMode.None);
            var serializedHud = new SerializedObject(hud);
            SerializedProperty stationsProperty = serializedHud.FindProperty("stations");
            stationsProperty.arraySize = stations.Length;
            for (int i = 0; i < stations.Length; i++)
            {
                stationsProperty.GetArrayElementAtIndex(i).objectReferenceValue = stations[i];
            }

            serializedHud.ApplyModifiedPropertiesWithoutUndo();

            // Job sheet and results (same UI document).
            var jobBoard = Object.FindFirstObjectByType<JobBoard>();
            SetReference(hud, "jobBoard", jobBoard);
            if (jobBoard != null)
            {
                var jobPanel = hudObject.AddComponent<JobPanel>();
                SetReference(jobPanel, "jobBoard", jobBoard);
                SetReference(jobPanel, "interactor", interactor);
            }

            // Contents of the offcut bin nearby (D-052).
            var binPanel = hudObject.AddComponent<OffcutBinPanel>();
            SetReference(binPanel, "interactor", interactor);

            // Money and inventory (D-040).
            var economy = Object.FindFirstObjectByType<WorkshopEconomy>();
            if (economy != null)
            {
                var economyPanel = hudObject.AddComponent<EconomyPanel>();
                SetReference(economyPanel, "economy", economy);
            }
        }

        /// <summary>
        /// Save system (D-036): a board factory (knows every species and the board materials, used to recreate
        /// saved boards) and the save service that loads on start and saves on quit.
        /// </summary>
        private static SaveGameService BuildSaveSystem(GameObject player, GreyboxMaterials materials,
            IReadOnlyDictionary<string, WoodSpeciesDefinition> species)
        {
            var saveObject = new GameObject("SaveSystem");

            var factory = saveObject.AddComponent<TimberBoardFactory>();
            var serializedFactory = new SerializedObject(factory);
            SerializedProperty speciesProperty = serializedFactory.FindProperty("species");
            speciesProperty.arraySize = species.Count;
            int index = 0;
            foreach (WoodSpeciesDefinition definition in species.Values)
            {
                speciesProperty.GetArrayElementAtIndex(index++).objectReferenceValue = definition;
            }

            serializedFactory.FindProperty("lumberMaterial").objectReferenceValue = materials.Lumber;
            serializedFactory.FindProperty("markMaterial").objectReferenceValue = materials.PencilMark;
            GameObject timber = GameObject.Find("Environment/Workshop/Timber");
            serializedFactory.FindProperty("container").objectReferenceValue = timber != null ? timber.transform : null;
            serializedFactory.ApplyModifiedPropertiesWithoutUndo();

            var service = saveObject.AddComponent<SaveGameService>();
            SetReference(service, "boardFactory", factory);
            SetReference(service, "player", player.GetComponent<PlayerController>());
            SetReference(service, "toolBelt", player.GetComponent<PlayerToolBelt>());
            SetReference(service, "waste", Object.FindFirstObjectByType<WorkshopWaste>());

            var jobBoard = Object.FindFirstObjectByType<JobBoard>();
            SetReference(service, "jobBoard", jobBoard);
            foreach (StockDelivery delivery in Object.FindObjectsByType<StockDelivery>(FindObjectsSortMode.None))
            {
                SetReference(delivery, "boardFactory", factory);
            }

            foreach (OffcutBin bin in Object.FindObjectsByType<OffcutBin>(FindObjectsSortMode.None))
            {
                SetReference(bin, "boardFactory", factory);
            }

            SetReference(service, "economy", Object.FindFirstObjectByType<WorkshopEconomy>());
            SetReference(service, "upgrades", Object.FindFirstObjectByType<WorkshopUpgrades>());
            SetReferences(service, "sites", Object.FindObjectsByType<SitePreparation>(FindObjectsSortMode.None));

            return service;
        }

        private static void BuildPauseSystem(GameObject player, VisualTreeAsset layout, PanelSettings panelSettings,
            SaveGameService saveService)
        {
            var playerController = player.GetComponent<PlayerController>();
            var interactor = player.GetComponent<PlayerInteractor>();
            var carrier = player.GetComponent<PlayerCarrier>();

            // Kept separate from the player and from any future GameManager (master prompt §6.1).
            var pauseObject = new GameObject("PauseController");
            var pauseController = pauseObject.AddComponent<PauseController>();

            var serializedPause = new SerializedObject(pauseController);
            SerializedProperty disabledProperty = serializedPause.FindProperty("disableWhilePaused");
            Behaviour[] gameplayComponents = { playerController, interactor, carrier, player.GetComponent<PlayerToolBelt>() };
            disabledProperty.arraySize = gameplayComponents.Length;
            for (int i = 0; i < gameplayComponents.Length; i++)
            {
                disabledProperty.GetArrayElementAtIndex(i).objectReferenceValue = gameplayComponents[i];
            }
            serializedPause.ApplyModifiedPropertiesWithoutUndo();

            var menuObject = new GameObject("PauseMenu");
            var document = menuObject.AddComponent<UIDocument>();
            document.panelSettings = panelSettings;
            document.visualTreeAsset = layout;
            document.sortingOrder = 10; // drawn above the HUD (same panel, higher order wins)

            var pauseMenu = menuObject.AddComponent<PauseMenu>();
            SetReference(pauseMenu, "pauseController", pauseController);
            SetReference(pauseMenu, "playerController", playerController);
            SetReference(pauseMenu, "saveService", saveService);
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
            public Material Steel { get; } = GetOrCreate("Steel", new Color(0.36f, 0.38f, 0.42f));
            public Material ToolYellow { get; } = GetOrCreate("ToolYellow", new Color(0.95f, 0.78f, 0.10f));
            public Material PencilRed { get; } = GetOrCreate("PencilRed", new Color(0.75f, 0.15f, 0.12f));
            public Material TapeBlade { get; } = GetOrCreateUnlit("TapeBlade", new Color(1f, 0.85f, 0.15f));
            public Material PencilMark { get; } = GetOrCreateUnlit("PencilMark", new Color(0.12f, 0.12f, 0.13f));
            public Material CutGuide { get; } = GetOrCreateUnlit("CutGuide", new Color(1f, 0.45f, 0.1f));
            public Material MarkPreview { get; } = GetOrCreateUnlit("MarkPreview", new Color(0.2f, 0.5f, 1f));
            public Material PilotPoint { get; } = GetOrCreateUnlit("PilotPoint", new Color(0.9f, 0.1f, 0.15f));
            public Material DrillTeal { get; } = GetOrCreate("DrillTeal", new Color(0.1f, 0.55f, 0.5f));
            public Material Paper { get; } = GetOrCreate("Paper", new Color(0.93f, 0.93f, 0.90f));

            /// <summary>Opaque URP Unlit: flat color, unaffected by lighting (good for thin overlays like a tape blade).</summary>
            private static Material GetOrCreateUnlit(string name, Color color)
            {
                string path = $"{MaterialFolder}/{name}.mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);

                if (material == null)
                {
                    material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                    AssetDatabase.CreateAsset(material, path);
                }

                material.SetColor("_BaseColor", color);
                EditorUtility.SetDirty(material);
                return material;
            }
            public Material Lumber { get; } = GetOrCreate("Lumber", new Color(0.86f, 0.73f, 0.53f));
            public Material BlueprintGhost { get; } = GetOrCreateTransparentUnlit("BlueprintGhost",
                new Color(0.45f, 0.75f, 1f, 0.25f));
            public Material PlacementPreview { get; } = GetOrCreateTransparentUnlit("PlacementPreview",
                new Color(0.3f, 1f, 0.4f, 0.35f));

            /// <summary>
            /// URP Unlit set up as alpha-blended transparent. These are the properties/keywords the URP material
            /// inspector would set when choosing Surface Type = Transparent, Blending Mode = Alpha.
            /// </summary>
            private static Material GetOrCreateTransparentUnlit(string name, Color color)
            {
                string path = $"{MaterialFolder}/{name}.mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);

                if (material == null)
                {
                    material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                    AssetDatabase.CreateAsset(material, path);
                }

                material.SetFloat("_Surface", 1f); // Transparent
                material.SetFloat("_Blend", 0f);   // Alpha
                material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
                material.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite", 0f);
                material.SetOverrideTag("RenderType", "Transparent");
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                material.SetColor("_BaseColor", color);
                EditorUtility.SetDirty(material);
                return material;
            }

            public Material BinPlastic { get; } = GetOrCreate("BinPlastic", new Color(0.22f, 0.24f, 0.27f));
            public Material Overalls { get; } = GetOrCreate("ApprenticeOveralls", new Color(0.16f, 0.28f, 0.52f));
            public Material Skin { get; } = GetOrCreate("ApprenticeSkin", new Color(0.87f, 0.70f, 0.56f));

            /// <summary>A plain lit material of this colour (created once, then reused).</summary>
            public Material Colored(string name, Color color) => GetOrCreate(name, color);

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
