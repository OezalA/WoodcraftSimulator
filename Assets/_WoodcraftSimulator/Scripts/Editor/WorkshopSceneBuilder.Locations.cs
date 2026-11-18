using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using WoodcraftSimulator.Materials;
using WoodcraftSimulator.Persistence;
using WoodcraftSimulator.World;

namespace WoodcraftSimulator.Editor
{
    /// <summary>
    /// Phase 12.5 (D-055/D-056): the van and the customer locations, each its own content scene. Greybox landscapes:
    /// the feeling of the view, not its beauty yet (Phase 13). Everything is generated here (no bought art, D-037).
    /// </summary>
    public static partial class WorkshopSceneBuilder
    {
        private const string TerrainFolder = "Assets/_WoodcraftSimulator/Art/Terrain";

        /// <summary>
        /// A greybox pickup van (D-056), 2 × 5 m, cab at the front (+Z), an open cargo bed behind it at 0.8 m. One trigger
        /// box over the bed is the cargo area; the body colliders are aimed at for E (drive). Returns where the player
        /// stands after arriving: beside the driver's door.
        /// </summary>
        private static Transform BuildVan(Transform parent, GreyboxMaterials materials, Vector3 position, float yaw)
        {
            var root = new GameObject("Van");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = position;
            root.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            Transform van = root.transform;

            Material body = materials.Colored("VanBody", new Color(0.86f, 0.87f, 0.88f));
            Material dark = materials.Colored("VanTyres", new Color(0.12f, 0.12f, 0.13f));
            Material glass = materials.Colored("VanGlass", new Color(0.35f, 0.45f, 0.55f));

            CreateBox("Cab", van, new Vector3(0f, 1.25f, 1.55f), new Vector3(1.9f, 1.7f, 1.7f), body);
            GameObject windscreen = CreateBox("Windscreen", van, new Vector3(0f, 1.6f, 2.41f), new Vector3(1.7f, 0.6f, 0.02f), glass);
            Object.DestroyImmediate(windscreen.GetComponent<BoxCollider>());
            CreateBox("BedFloor", van, new Vector3(0f, 0.75f, -0.85f), new Vector3(1.9f, 0.1f, 3.1f), body);
            CreateBox("BedSideLeft", van, new Vector3(-0.92f, 0.95f, -0.85f), new Vector3(0.06f, 0.3f, 3.1f), body);
            CreateBox("BedSideRight", van, new Vector3(0.92f, 0.95f, -0.85f), new Vector3(0.06f, 0.3f, 3.1f), body);
            CreateBox("BedTailgate", van, new Vector3(0f, 0.95f, -2.37f), new Vector3(1.9f, 0.3f, 0.06f), body);
            foreach (Vector3 wheel in new[]
                     {
                         new Vector3(-0.85f, 0.35f, 1.5f), new Vector3(0.85f, 0.35f, 1.5f),
                         new Vector3(-0.85f, 0.35f, -1.5f), new Vector3(0.85f, 0.35f, -1.5f)
                     })
            {
                GameObject tyre = CreateBox("Wheel", van, wheel, new Vector3(0.25f, 0.7f, 0.7f), dark);
                Object.DestroyImmediate(tyre.GetComponent<BoxCollider>());
            }

            // What lies in the bed travels along (a trigger: not aimed at, not in the way).
            var cargo = new GameObject("CargoBed");
            cargo.transform.SetParent(van, false);
            cargo.transform.localPosition = new Vector3(0f, 0.8f + 0.6f, -0.85f);
            var cargoBox = cargo.AddComponent<BoxCollider>();
            cargoBox.isTrigger = true;
            cargoBox.size = new Vector3(1.8f, 1.2f, 3.0f);

            var vanComponent = root.AddComponent<Van>();
            SetReference(vanComponent, "cargoArea", cargoBox);

            var arrival = new GameObject("ArrivalPoint").transform;
            arrival.SetParent(van, false);
            arrival.localPosition = new Vector3(1.8f, 0f, 1.4f);
            arrival.localRotation = Quaternion.Euler(0f, 90f, 0f);
            return arrival;
        }

        /// <summary>A place marker for a content scene: its location and where the player arrives.</summary>
        private static void AddPlaceInfo(Scene scene, LocationDefinition location, Transform arrival)
        {
            var info = new GameObject("PlaceInfo").AddComponent<PlaceInfo>();
            SceneManager.MoveGameObjectToScene(info.gameObject, scene);
            SetReference(info, "location", location);
            SetReference(info, "arrivalPoint", arrival);
        }

        /// <summary>Builds and saves the customer locations' scenes (they are closed again afterwards).</summary>
        private static void BuildCustomerLocations(GreyboxMaterials materials)
        {
            EnsureFolder(LocationJobs.ScenesFolder);
            EnsureFolder(TerrainFolder);
            Scene previous = SceneManager.GetActiveScene();

            BuildLocationScene(LocationJobs.MountainScenePath, LocationJobs.Mountain(), materials, scene => BuildMountain(scene, materials));
            BuildLocationScene(LocationJobs.IslandScenePath, LocationJobs.Island(), materials, scene => BuildIsland(scene, materials));

            if (previous.IsValid())
            {
                SceneManager.SetActiveScene(previous);
            }
        }

        private static void BuildLocationScene(string path, LocationDefinition location, GreyboxMaterials materials,
            System.Func<Scene, Transform> build)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene); // render settings (fog, sky) belong to the active scene
            ConfigureDefaultObjects(scene);
            Transform arrival = build(scene);
            AddPlaceInfo(scene, location, arrival);
            UseMetreUVs(scene);
            EditorSceneManager.SaveScene(scene, path);
            EditorSceneManager.CloseScene(scene, true);
        }

        /// <summary>
        /// The mountain house (D-055): a flat yard high in the mountains, a chalet, the terrace site in front of it facing
        /// a ring of snowy peaks (generated terrain), cold light and haze.
        /// </summary>
        private static Transform BuildMountain(Scene scene, GreyboxMaterials materials)
        {
            var environment = new GameObject("Environment").transform;
            BuildMountainTerrain(environment);
            ApplySky("kloofendal_43d_clear_puresky", 1f, 0f); // clear, crisp mountain sky (D-060)
            AddReflectionProbe(environment, new Vector3(0f, 2f, 0f), new Vector3(80f, 30f, 80f));
            AddAmbience(environment, "Wind", 0.35f);

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.80f, 0.86f, 0.92f);
            RenderSettings.fogDensity = 0.0028f;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.75f, 0.82f, 0.92f);
            RenderSettings.ambientEquatorColor = new Color(0.62f, 0.66f, 0.70f);
            RenderSettings.ambientGroundColor = new Color(0.35f, 0.33f, 0.30f);
            SetSun(scene, new Vector3(28f, 150f, 0f), new Color(1f, 0.96f, 0.9f), 1.1f);

            BuildHouse(environment, materials, new Vector3(0f, 0f, 6.5f), materials.Colored("ChaletWood", new Color(0.45f, 0.30f, 0.18f)),
                materials.Colored("ChaletRoof", new Color(0.22f, 0.20f, 0.20f)), "Mountain house owner", "Dağ evi sahibi");

            BuildTerraceSite(environment, materials, new TerraceSite
            {
                Id = "MountainTerraceSite", Position = Vector3.zero, Blueprint = LocationJobs.MountainTerrace(),
                DeliveryId = LocationJobs.MountainId, DeliveryOffset = new Vector3(2.2f, 0f, 0f),
                SawhorsesOffset = new Vector3(-2.6f, 0f, 0f), Outline = "the terrace (2.4 × 1.8 m)", OutlineTurkish = "terası (2,4 × 1,8 m)"
            });

            return BuildVan(environment, materials, new Vector3(-7f, 0f, -1f), 0f);
        }

        /// <summary>
        /// A generated terrain (no bought art, D-037): a flat yard in the middle (at y 0), rolling meadows around it,
        /// then a ring of high peaks, highest in front of the terrace (towards −Z); grass, rock and snow by height and
        /// slope. 400 × 400 m, peaks up to ~160 m. Its data and layers are saved as assets.
        /// </summary>
        private static void BuildMountainTerrain(Transform parent)
        {
            const int resolution = 129;
            const float size = 400f;
            const float height = 160f;
            const float yard = 0.05f; // the yard's height (normalised): 8 m above the terrain's base

            var data = new TerrainData { heightmapResolution = resolution, size = new Vector3(size, height, size) };
            var heights = new float[resolution, resolution];
            for (int z = 0; z < resolution; z++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    float u = x / (resolution - 1f) - 0.5f;
                    float v = z / (resolution - 1f) - 0.5f;
                    float r = Mathf.Sqrt(u * u + v * v) * 2f;
                    float meadow = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.12f, 0.35f, r)) * 0.06f *
                                   (0.6f + Mathf.PerlinNoise(x * 0.11f, z * 0.11f));
                    float front = 1f + 0.7f * Mathf.Max(0f, -v * 2f); // higher peaks in the terrace's view
                    float ridge = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 0.95f, r)) * front *
                                  (0.45f + 0.45f * Mathf.PerlinNoise(x * 0.07f + 13f, z * 0.07f + 7f));
                    heights[z, x] = Mathf.Clamp01(yard + meadow + ridge);
                }
            }

            data.SetHeights(0, 0, heights);

            TerrainLayer grass = TerrainLayerFor("MountainGrass", "Grass004", new Color(0.34f, 0.44f, 0.24f), 3f);
            TerrainLayer rock = TerrainLayerFor("MountainRock", "Rock058", new Color(0.45f, 0.44f, 0.42f), 6f);
            TerrainLayer snow = TerrainLayerFor("MountainSnow", "Snow010A", new Color(0.95f, 0.96f, 0.98f), 4f);
            data.terrainLayers = new[] { grass, rock, snow };
            data.alphamapResolution = 128;
            var alphas = new float[128, 128, 3];
            for (int z = 0; z < 128; z++)
            {
                for (int x = 0; x < 128; x++)
                {
                    float nx = x / 127f;
                    float nz = z / 127f;
                    float h = data.GetInterpolatedHeight(nx, nz) / height;
                    float steep = data.GetSteepness(nx, nz) / 90f;
                    float snowWeight = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.30f, 0.42f, h));
                    float rockWeight = (1f - snowWeight) * Mathf.Clamp01(Mathf.InverseLerp(0.10f, 0.22f, h) + steep * 1.5f);
                    float grassWeight = Mathf.Max(0f, 1f - snowWeight - rockWeight);
                    alphas[z, x, 0] = grassWeight;
                    alphas[z, x, 1] = rockWeight;
                    alphas[z, x, 2] = snowWeight;
                }
            }

            data.SetAlphamaps(0, 0, alphas);
            string dataPath = $"{TerrainFolder}/MountainTerrain.asset";
            AssetDatabase.DeleteAsset(dataPath);
            AssetDatabase.CreateAsset(data, dataPath);

            GameObject terrain = Terrain.CreateTerrainGameObject(data);
            terrain.name = "Terrain";

            // URP's terrain shader (without a material the terrain may render pink in URP).
            Shader terrainShader = Shader.Find("Universal Render Pipeline/Terrain/Lit");
            if (terrainShader != null)
            {
                string materialPath = $"{TerrainFolder}/MountainTerrain.mat";
                var terrainMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (terrainMaterial == null)
                {
                    terrainMaterial = new Material(terrainShader) { name = "MountainTerrain" };
                    AssetDatabase.CreateAsset(terrainMaterial, materialPath);
                }

                terrain.GetComponent<Terrain>().materialTemplate = terrainMaterial;
            }
            terrain.transform.SetParent(parent, false);
            terrain.transform.localPosition = new Vector3(-size / 2f, -yard * height, -size / 2f);
        }

        /// <summary>A terrain layer with a small generated one-colour texture (a little noise so it isn't flat).</summary>
        private static TerrainLayer TerrainLayerAsset(string name, Color color)
        {
            string texturePath = $"{TerrainFolder}/{name}.asset";
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (texture == null)
            {
                texture = new Texture2D(16, 16, TextureFormat.RGBA32, true) { name = name };
                for (int y = 0; y < 16; y++)
                {
                    for (int x = 0; x < 16; x++)
                    {
                        float shade = 0.9f + 0.2f * Mathf.PerlinNoise(x * 0.7f, y * 0.7f);
                        texture.SetPixel(x, y, new Color(color.r * shade, color.g * shade, color.b * shade, 1f));
                    }
                }

                texture.Apply();
                AssetDatabase.CreateAsset(texture, texturePath);
            }

            string layerPath = $"{TerrainFolder}/{name}.terrainlayer";
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(layerPath);
            if (layer == null)
            {
                layer = new TerrainLayer { diffuseTexture = texture, tileSize = new Vector2(4f, 4f) };
                AssetDatabase.CreateAsset(layer, layerPath);
            }

            return layer;
        }

        /// <summary>
        /// The island house (D-055): a small island in a calm sea (a sand disc with a grass top, a water plane to the
        /// horizon, a few islets in the distance), a white house, the deck site in front of it facing the open sea.
        /// </summary>
        private static Transform BuildIsland(Scene scene, GreyboxMaterials materials)
        {
            var environment = new GameObject("Environment").transform;

            GameObject sea = GameObject.CreatePrimitive(PrimitiveType.Plane);
            sea.name = "Sea";
            sea.transform.SetParent(environment, false);
            sea.transform.localPosition = new Vector3(0f, -0.45f, 0f);
            sea.transform.localScale = new Vector3(80f, 1f, 80f); // 800 m: to the haze
            Material water = materials.Colored("Sea", new Color(0.06f, 0.30f, 0.42f));
            water.SetFloat("_Smoothness", 0.92f); // reflects the sky (the probe below, D-060)
            sea.GetComponent<Renderer>().sharedMaterial = water;
            ApplySky("rustig_koppie_puresky", 1f, 0f); // deep blue summer sky (D-060)
            AddReflectionProbe(environment, new Vector3(0f, 2f, 0f), new Vector3(80f, 30f, 80f));
            AddAmbience(environment, "Waves", 0.4f);

            Material sand = materials.Colored("Sand", new Color(0.86f, 0.80f, 0.62f));
            Material grass = materials.Colored("IslandGrass", new Color(0.40f, 0.55f, 0.28f));
            Disc("Beach", environment, 60f, -0.6f, 1.2f, sand);
            Disc("Meadow", environment, 44f, -0.59f, 1.2f, grass);
            foreach ((Vector3 position, float size) in new[]
                     {
                         (new Vector3(-140f, -6f, -170f), 60f), (new Vector3(90f, -8f, -210f), 80f), (new Vector3(210f, -5f, -60f), 50f)
                     })
            {
                GameObject islet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                islet.name = "Islet";
                islet.transform.SetParent(environment, false);
                islet.transform.localPosition = position;
                islet.transform.localScale = new Vector3(size, size * 0.25f, size);
                islet.GetComponent<Renderer>().sharedMaterial = grass;
                Object.DestroyImmediate(islet.GetComponent<SphereCollider>());
            }

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.78f, 0.88f, 0.95f);
            RenderSettings.fogDensity = 0.0022f;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.70f, 0.82f, 0.95f);
            RenderSettings.ambientEquatorColor = new Color(0.70f, 0.75f, 0.78f);
            RenderSettings.ambientGroundColor = new Color(0.45f, 0.42f, 0.35f);
            SetSun(scene, new Vector3(55f, 200f, 0f), new Color(1f, 0.95f, 0.85f), 1.3f);

            BuildHouse(environment, materials, new Vector3(0f, 0f, 6.5f), materials.Colored("WhiteWall", new Color(0.95f, 0.94f, 0.9f)),
                materials.Colored("TerracottaRoof", new Color(0.72f, 0.36f, 0.22f)), "Island house owner", "Ada evi sahibi");

            BuildTerraceSite(environment, materials, new TerraceSite
            {
                Id = "IslandDeckSite", Position = Vector3.zero, Blueprint = LocationJobs.IslandDeck(),
                DeliveryId = LocationJobs.IslandId, DeliveryOffset = new Vector3(2.2f, 0f, 0f),
                SawhorsesOffset = new Vector3(-2.6f, 0f, 0f), GlassRailing = true,
                Outline = "the deck (2.4 × 1.8 m)", OutlineTurkish = "verandayı (2,4 × 1,8 m)"
            });

            return BuildVan(environment, materials, new Vector3(-7f, 0f, -1f), 0f);
        }

        /// <summary>A flat disc to walk on (a cylinder with a mesh collider: the primitive's capsule would be round).</summary>
        private static void Disc(string name, Transform parent, float diameter, float y, float thickness, Material material)
        {
            GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = name;
            disc.transform.SetParent(parent, false);
            disc.transform.localPosition = new Vector3(0f, y, 0f);
            disc.transform.localScale = new Vector3(diameter, thickness / 2f, diameter);
            disc.GetComponent<Renderer>().sharedMaterial = material;
            Object.DestroyImmediate(disc.GetComponent<CapsuleCollider>());
            disc.AddComponent<MeshCollider>().sharedMesh = disc.GetComponent<MeshFilter>().sharedMesh;
        }

        /// <summary>
        /// A greybox house, 8 × 5 m, 3 m high with a flat roof slab, its front (towards −Z) with a door where the
        /// customer takes the finished job (<see cref="CustomerHandover"/>).
        /// </summary>
        private static void BuildHouse(Transform parent, GreyboxMaterials materials, Vector3 position, Material walls, Material roof,
            string owner, string ownerTurkish)
        {
            var house = new GameObject("House").transform;
            house.SetParent(parent, false);
            house.localPosition = position;
            CreateBox("Walls", house, new Vector3(0f, 1.5f, 0f), new Vector3(8f, 3f, 5f), walls);
            CreateBox("Roof", house, new Vector3(0f, 3.15f, 0f), new Vector3(8.6f, 0.3f, 5.6f), roof);
            GameObject window = CreateBox("Window", house, new Vector3(-2.2f, 1.6f, -2.51f), new Vector3(1.4f, 1f, 0.02f),
                materials.Colored("HouseWindow", new Color(0.35f, 0.45f, 0.55f)));
            Object.DestroyImmediate(window.GetComponent<BoxCollider>());

            GameObject door = CreateBox("Door", house, new Vector3(1.6f, 1.05f, -2.53f), new Vector3(0.95f, 2.1f, 0.06f),
                materials.Colored("HouseDoor", new Color(0.30f, 0.20f, 0.12f)));
            var handover = door.AddComponent<CustomerHandover>();
            var serialized = new SerializedObject(handover);
            serialized.FindProperty("customerName").stringValue = owner;
            serialized.FindProperty("customerNameTurkish").stringValue = ownerTurkish;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetSun(Scene scene, Vector3 euler, Color color, float intensity)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.TryGetComponent(out Light light) && light.type == LightType.Directional)
                {
                    root.transform.rotation = Quaternion.Euler(euler);
                    light.color = color;
                    light.intensity = intensity;
                }
            }
        }
    }
}
