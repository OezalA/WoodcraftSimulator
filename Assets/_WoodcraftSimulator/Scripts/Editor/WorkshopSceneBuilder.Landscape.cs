using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace WoodcraftSimulator.Editor
{
    /// <summary>
    /// Generated landscapes of the customer places (Phase 13c revision, D-063): Unity terrains with CC0 texture layers
    /// blended by height and slope, grass tufts (a generated texture) and simple generated conifers as terrain trees.
    /// No downloaded models: Poly Haven's CC0 trees have millions of polygons, too heavy for a game. Everything is
    /// deterministic (fixed noise offsets), so a rebuild writes the same terrain data.
    /// </summary>
    public static partial class WorkshopSceneBuilder
    {
        /// <summary>
        /// The yard is laid 2 cm under y 0: terrain heights are stored in steps (~5 cm over the mountain's 1.5 km), and
        /// the yard must never cover the 1 cm site floor (D-046) that lies on y 0.
        /// </summary>
        private const float YardBias = 0.02f;

        // ---- Mountain house ----

        private const float MountainSize = 5000f;
        private const float MountainBase = -450f;   // lowest point (the valley floor) relative to the yard
        private const float MountainHeight = 1500f; // height range of the terrain data

        /// <summary>
        /// The mountain house's landscape: the yard (flat, y 0) on a shoulder high above a valley, a mountain rising
        /// behind the chalet; in front of the terrace (towards −Z) and to the sides the ground falls ~400 m into a
        /// forested valley, across it, 1.2–2.3 km away, a range of ridged, snow-capped peaks up to ~850 m above the yard
        /// (about 15° above the horizon: "snowy mountains in the distance", D-055). 5 × 5 km.
        /// </summary>
        private static void BuildMountainLandscape(Transform parent, GreyboxMaterials materials)
        {
            const int resolution = 513; // ~9.8 m per sample (the yard is flat, the shapes are large)
            var data = new TerrainData { heightmapResolution = resolution };
            data.size = new Vector3(MountainSize, MountainHeight, MountainSize);

            var heights = new float[resolution, resolution];
            for (int iz = 0; iz < resolution; iz++)
            {
                for (int ix = 0; ix < resolution; ix++)
                {
                    float x = (ix / (resolution - 1f) - 0.5f) * MountainSize;
                    float z = (iz / (resolution - 1f) - 0.5f) * MountainSize;
                    heights[iz, ix] = Mathf.Clamp01((MountainGround(x, z) - YardBias - MountainBase) / MountainHeight);
                }
            }

            data.SetHeights(0, 0, heights);

            TerrainLayer grass = TerrainLayerFor("MountainGrass", "Grass004", new Color(0.34f, 0.44f, 0.24f), 3f);
            TerrainLayer rock = TerrainLayerFor("MountainRock", "Rock058", new Color(0.45f, 0.44f, 0.42f), 14f);
            TerrainLayer snow = TerrainLayerFor("MountainSnow", "Snow010A", new Color(0.95f, 0.96f, 0.98f), 8f);
            data.terrainLayers = new[] { grass, rock, snow };
            PaintLayers(data, 512, MountainBase, (x, z, y, slope) =>
            {
                // Snow above a wavy snow line (not on cliffs), rock on steep slopes and high up, grass below.
                float snowLine = 250f + 80f * (Fbm(x / 400f + 11f, z / 400f + 3f, 3) - 0.5f) * 2f;
                float snowWeight = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(snowLine - 40f, snowLine + 40f, y)) *
                                   (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(50f, 62f, slope)));
                float rockWeight = Mathf.Max(Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(40f, 52f, slope)),
                    0.6f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(150f, 280f, y)));
                rockWeight *= 1f - snowWeight;
                return new Vector3(Mathf.Max(0f, 1f - snowWeight - rockWeight), rockWeight, snowWeight);
            });

            AddGrassTufts(data, 512, 14, (x, z, weights) =>
            {
                float near = 1f - Mathf.InverseLerp(120f, 220f, Mathf.Sqrt(x * x + z * z));
                return weights.x * near * KeepClearOfSite(x, z);
            });

            PlaceConifers(data, materials, MountainSize, MountainBase, 11f, (x, z, y, slope) =>
            {
                float distance = Mathf.Sqrt(x * x + z * z);
                if (distance < 60f || distance > 2400f || slope > 38f)
                {
                    return 0f;
                }

                // Up to a wavy tree line, in patches with clearings, thinner far away (the haze hides it); in front of the
                // terrace only trees whose tops stay below the eyes (the view stays open).
                float treeLine = 60f + 40f * (Fbm(x / 300f + 41f, z / 300f + 17f, 2) - 0.5f) * 2f;
                if (y > treeLine || (z < 0f && distance < 450f && y > -24f))
                {
                    return 0f;
                }

                float patches = Mathf.InverseLerp(0.40f, 0.60f, Fbm(x / 260f + 71f, z / 260f + 29f, 3));
                float far = 1f - 0.5f * Mathf.InverseLerp(800f, 2400f, distance);
                return patches * far * (1f - Mathf.InverseLerp(treeLine - 60f, treeLine, y));
            });

            CreateTerrain("MountainTerrain", data, parent, new Vector3(-MountainSize / 2f, MountainBase, -MountainSize / 2f), 3000f);
        }

        /// <summary>The mountain's ground height (m, relative to the yard) at a point, the chalet at the origin.</summary>
        private static float MountainGround(float x, float z)
        {
            float distance = Mathf.Sqrt(x * x + z * z);
            if (distance < 18f)
            {
                return 0f; // the yard
            }

            // Warped coordinates: no ridge runs straight.
            float wx = x + 400f * (Fbm(x / 1600f + 3.1f, z / 1600f + 7.7f, 2) - 0.5f) * 2f;
            float wz = z + 400f * (Fbm(x / 1600f + 19.3f, z / 1600f + 5.2f, 2) - 0.5f) * 2f;
            float ridges = Ridged(wx / 1100f + 50f, wz / 1100f + 50f, 5);
            float detail = (Fbm(x / 70f + 200f, z / 70f + 200f, 3) - 0.5f) * 2f;

            // In front and at the sides: down into the valley (falling away soon after the yard, so the valley shows),
            // then up to the range across it.
            float t = Mathf.InverseLerp(18f, 950f, distance);
            float valley = -400f * (0.5f * Mathf.SmoothStep(0f, 1f, t) + 0.5f * (1f - (1f - t) * (1f - t)));
            float range = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1150f, 2250f, distance)) * (520f + 720f * ridges);
            float frontHeight = valley + range;

            // Behind: the mountain the chalet stands on.
            float behindHeight = -20f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(18f, 120f, distance)) +
                                 Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(120f, 1500f, distance)) * (240f + 560f * ridges);

            float front = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.8f, 0.1f, -z / distance));
            float height = Mathf.Lerp(behindHeight, frontHeight, front);
            height += detail * (4f * Mathf.InverseLerp(18f, 120f, distance) + 22f * Mathf.InverseLerp(200f, 1500f, distance));
            return height;
        }

        // ---- Island house ----

        private const float IslandSize = 600f;
        private const float IslandBase = -9f;
        private const float IslandHeight = 40f;

        /// <summary>
        /// The island's landscape: a flat yard (y 0) in a grassy island ~60 m across with low dunes, a ~12 m sandy beach all
        /// round running into the sea (its water plane at −0.45 m), three islets in the distance. 600 × 600 m.
        /// </summary>
        private static void BuildIslandLandscape(Transform parent, GreyboxMaterials materials)
        {
            const int resolution = 257; // ~2.3 m per sample
            var data = new TerrainData { heightmapResolution = resolution };
            data.size = new Vector3(IslandSize, IslandHeight, IslandSize);

            var heights = new float[resolution, resolution];
            for (int iz = 0; iz < resolution; iz++)
            {
                for (int ix = 0; ix < resolution; ix++)
                {
                    float x = (ix / (resolution - 1f) - 0.5f) * IslandSize;
                    float z = (iz / (resolution - 1f) - 0.5f) * IslandSize;
                    heights[iz, ix] = Mathf.Clamp01((IslandGround(x, z) - YardBias - IslandBase) / IslandHeight);
                }
            }

            data.SetHeights(0, 0, heights);

            TerrainLayer grass = TerrainLayerFor("IslandGrass", "Grass004", new Color(0.40f, 0.55f, 0.28f), 3f);
            TerrainLayer sand = TerrainLayerFor("IslandSand", "Ground080", new Color(0.86f, 0.80f, 0.62f), 4f);
            TerrainLayer rock = TerrainLayerFor("IslandRock", "Rock058", new Color(0.50f, 0.48f, 0.44f), 8f);
            data.terrainLayers = new[] { grass, sand, rock };
            PaintLayers(data, 512, IslandBase, (x, z, y, slope) =>
            {
                // A beach ~12 m wide along the shore (a ragged edge), the islets sandy near the water, rock on their
                // steep sides, grass elsewhere (the yard included).
                float ragged = 2.5f * (Fbm(x / 6f + 5f, z / 6f + 8f, 3) - 0.5f) * 2f;
                float shore = IslandShore(x, z);
                float distance = Mathf.Sqrt(x * x + z * z);
                float beach = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(shore - 16f + ragged, shore - 12f + ragged, distance));
                float isletBeach = distance > shore + 10f ? 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.3f, 1.2f, y + ragged * 0.2f)) : 0f;
                float sandWeight = Mathf.Max(beach * (distance <= shore + 10f ? 1f : 0f), isletBeach);
                float rockWeight = (1f - sandWeight) * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(22f, 34f, slope));
                return new Vector3(Mathf.Max(0f, 1f - sandWeight - rockWeight), sandWeight, rockWeight);
            });

            AddGrassTufts(data, 512, 3, (x, z, weights) => weights.x * KeepClearOfSite(x, z));
            CreateTerrain("IslandTerrain", data, parent, new Vector3(-IslandSize / 2f, IslandBase, -IslandSize / 2f), 1000f);
        }

        /// <summary>Islets in the distance: centre x, centre z, radius, height (m).</summary>
        private static readonly Vector4[] Islets =
        {
            new Vector4(-140f, -170f, 42f, 9f), new Vector4(90f, -215f, 58f, 14f), new Vector4(215f, -60f, 36f, 7f)
        };

        /// <summary>The island's wavy shore line: its distance from the house (~37–55 m) in the direction of a point.</summary>
        private static float IslandShore(float x, float z)
        {
            float angle = Mathf.Atan2(z, x);
            return 46f + 9f * (Fbm(Mathf.Cos(angle) * 1.6f + 30f, Mathf.Sin(angle) * 1.6f + 30f, 3) - 0.5f) * 2f;
        }

        /// <summary>The island's ground height (m, relative to the yard) at a point, the house at the origin.</summary>
        private static float IslandGround(float x, float z)
        {
            float distance = Mathf.Sqrt(x * x + z * z);
            if (distance < 24f)
            {
                return 0f; // the yard
            }

            float shore = IslandShore(x, z);
            float land = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(shore - 4f, shore + 8f, distance)); // water line ~shore + 1 m
            float dunes = Mathf.Max(0.1f, (Fbm(x / 13f + 50f, z / 13f + 50f, 3) - 0.5f) * 1.8f + 0.35f); // no pools inland
            // The dunes flatten into a low beach towards the shore.
            float onLand = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(24f, 34f, distance)) *
                           Mathf.Lerp(dunes, 0.2f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(shore - 20f, shore - 12f, distance)));
            float seabed = -2.2f - 5f * Mathf.InverseLerp(shore + 5f, shore + 120f, distance);
            float height = Mathf.Lerp(seabed, onLand, land);

            foreach (Vector4 islet in Islets)
            {
                float dx = x - islet.x;
                float dz = z - islet.y;
                float share = 1f - Mathf.Sqrt(dx * dx + dz * dz) / islet.z;
                if (share > 0f)
                {
                    float bumps = 0.75f + 0.5f * Fbm(x / 18f + islet.x, z / 18f + islet.y, 3);
                    height = Mathf.Max(height, -7f + (islet.w + 7f) * Mathf.SmoothStep(0f, 1f, share) * bumps);
                }
            }

            return height;
        }

        /// <summary>
        /// 0 where the terrace site, the house and the van stand (no grass through the deck or the van), else 1.
        /// Both places use the same layout (D-055): site at the origin, house at z 6.5, van at (−7, −1).
        /// </summary>
        private static float KeepClearOfSite(float x, float z) =>
            Mathf.Min(OutsideBox(x, z, 0f, 0f, 4.6f, 2.6f), // the site with the sawhorses and the delivery spot
                Mathf.Min(OutsideBox(x, z, 0f, 6.5f, 4.5f, 3f), OutsideBox(x, z, -7f, -1f, 1.6f, 3f)));

        /// <summary>0 inside a box (centre, half sizes), rising to 1 within 1.5 m outside it.</summary>
        private static float OutsideBox(float x, float z, float centreX, float centreZ, float halfX, float halfZ) =>
            Mathf.InverseLerp(0f, 1.5f, Mathf.Max(Mathf.Abs(x - centreX) - halfX, Mathf.Abs(z - centreZ) - halfZ));

        // ---- Terrain building blocks ----

        /// <summary>Paints the (up to three) layers from a rule (x, z, height, slope in degrees) → weights.</summary>
        private static void PaintLayers(TerrainData data, int resolution, float baseY, System.Func<float, float, float, float, Vector3> rule)
        {
            data.alphamapResolution = resolution;
            int count = data.terrainLayers.Length;
            var alphas = new float[resolution, resolution, count];
            Vector3 size = data.size;
            for (int iz = 0; iz < resolution; iz++)
            {
                for (int ix = 0; ix < resolution; ix++)
                {
                    float nx = ix / (resolution - 1f);
                    float nz = iz / (resolution - 1f);
                    float x = (nx - 0.5f) * size.x;
                    float z = (nz - 0.5f) * size.z;
                    float y = data.GetInterpolatedHeight(nx, nz) + baseY;
                    Vector3 weights = rule(x, z, y, data.GetSteepness(nx, nz));
                    float sum = Mathf.Max(0.0001f, weights.x + weights.y + weights.z);
                    for (int layer = 0; layer < count && layer < 3; layer++)
                    {
                        alphas[iz, ix, layer] = weights[layer] / sum;
                    }
                }
            }

            data.SetAlphamaps(0, 0, alphas);
        }


        /// <summary>
        /// Grass tufts (camera-facing quads with a generated texture, waving in the wind) up to
        /// <paramref name="maxPerCell"/> per detail cell, scaled by a rule (x, z, layer weights) → 0..1.
        /// </summary>
        private static void AddGrassTufts(TerrainData data, int resolution, int maxPerCell,
            System.Func<float, float, Vector3, float> density)
        {
            data.SetDetailResolution(resolution, 16);
            data.SetDetailScatterMode(DetailScatterMode.InstanceCountMode);
            data.detailPrototypes = new[]
            {
                new DetailPrototype
                {
                    prototypeTexture = GrassTuftTexture(),
                    usePrototypeMesh = false,
                    renderMode = DetailRenderMode.GrassBillboard,
                    minWidth = 0.45f, maxWidth = 0.8f,
                    minHeight = 0.25f, maxHeight = 0.55f,
                    noiseSpread = 0.35f,
                    healthyColor = new Color(0.78f, 0.86f, 0.62f),
                    dryColor = new Color(0.86f, 0.80f, 0.55f)
                }
            };
            data.wavingGrassStrength = 0.25f;
            data.wavingGrassAmount = 0.25f;
            data.wavingGrassSpeed = 0.35f;
            data.wavingGrassTint = new Color(0.9f, 0.95f, 0.85f);

            float[,,] alphas = data.GetAlphamaps(0, 0, data.alphamapResolution, data.alphamapResolution);
            int layers = alphas.GetLength(2);
            var map = new int[resolution, resolution];
            Vector3 size = data.size;
            for (int iz = 0; iz < resolution; iz++)
            {
                for (int ix = 0; ix < resolution; ix++)
                {
                    float nx = (ix + 0.5f) / resolution;
                    float nz = (iz + 0.5f) / resolution;
                    int ax = Mathf.Min(data.alphamapResolution - 1, (int)(nx * data.alphamapResolution));
                    int az = Mathf.Min(data.alphamapResolution - 1, (int)(nz * data.alphamapResolution));
                    var weights = new Vector3(alphas[az, ax, 0], layers > 1 ? alphas[az, ax, 1] : 0f, layers > 2 ? alphas[az, ax, 2] : 0f);
                    float share = Mathf.Clamp01(density((nx - 0.5f) * size.x, (nz - 0.5f) * size.z, weights));
                    // A little patchiness: denser and thinner spots.
                    share *= 0.55f + 0.45f * Fbm(ix * 0.09f + 300f, iz * 0.09f + 300f, 2) * 2f;
                    map[iz, ix] = Mathf.Clamp(Mathf.RoundToInt(share * maxPerCell), 0, maxPerCell);
                }
            }

            data.SetDetailLayer(0, 0, 0, map);
        }

        /// <summary>
        /// Conifers as terrain trees (three generated shapes): candidates on a jittered grid, kept by a rule
        /// (x, z, height, slope) → probability. Instanced, no colliders (the trees stand away from where one walks).
        /// </summary>
        private static void PlaceConifers(TerrainData data, GreyboxMaterials materials, float terrainSize, float baseY, float spacing,
            System.Func<float, float, float, float, float> rule)
        {
            data.treePrototypes = new[]
            {
                new TreePrototype { prefab = ConiferPrefab("ConiferSpruce", materials, 4, 0.22f, new Color(0.13f, 0.22f, 0.12f)) },
                new TreePrototype { prefab = ConiferPrefab("ConiferFir", materials, 3, 0.30f, new Color(0.16f, 0.26f, 0.13f)) },
                new TreePrototype { prefab = ConiferPrefab("ConiferDark", materials, 4, 0.26f, new Color(0.10f, 0.18f, 0.11f)) }
            };

            var random = new System.Random(1955);
            var trees = new List<TreeInstance>();
            int steps = Mathf.FloorToInt(terrainSize / spacing);
            for (int iz = 0; iz < steps; iz++)
            {
                for (int ix = 0; ix < steps; ix++)
                {
                    float nx = (ix + 0.15f + 0.7f * (float)random.NextDouble()) / steps;
                    float nz = (iz + 0.15f + 0.7f * (float)random.NextDouble()) / steps;
                    float keep = (float)random.NextDouble();
                    float x = (nx - 0.5f) * terrainSize;
                    float z = (nz - 0.5f) * terrainSize;
                    float y = data.GetInterpolatedHeight(nx, nz) + baseY;
                    if (keep >= rule(x, z, y, data.GetSteepness(nx, nz)))
                    {
                        continue;
                    }

                    float scale = 0.7f + 0.6f * (float)random.NextDouble();
                    trees.Add(new TreeInstance
                    {
                        position = new Vector3(nx, 0f, nz),
                        prototypeIndex = random.Next(3),
                        widthScale = scale * (0.85f + 0.3f * (float)random.NextDouble()),
                        heightScale = scale,
                        rotation = (float)(random.NextDouble() * Mathf.PI * 2f),
                        color = Color.white,
                        lightmapColor = Color.white
                    });
                }
            }

            data.SetTreeInstances(trees.ToArray(), true);
        }

        /// <summary>Saves the terrain data and makes the terrain object (URP terrain material, distances).</summary>
        private static void CreateTerrain(string name, TerrainData data, Transform parent, Vector3 position, float treeDistance)
        {
            string dataPath = $"{TerrainFolder}/{name}.asset";
            AssetDatabase.DeleteAsset(dataPath);
            AssetDatabase.CreateAsset(data, dataPath);

            GameObject terrainObject = Terrain.CreateTerrainGameObject(data);
            terrainObject.name = "Terrain";
            var terrain = terrainObject.GetComponent<Terrain>();
            terrain.heightmapPixelError = 3f;
            terrain.basemapDistance = 400f;
            terrain.detailObjectDistance = 90f;
            terrain.treeDistance = treeDistance;
            terrain.treeBillboardDistance = treeDistance; // plain meshes all the way (they have no billboard)
            terrain.treeMaximumFullLODCount = 100000;

            // URP's terrain shader (without a material the terrain may render pink in URP).
            Shader terrainShader = Shader.Find("Universal Render Pipeline/Terrain/Lit");
            if (terrainShader != null)
            {
                string materialPath = $"{TerrainFolder}/{name}.mat";
                var terrainMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (terrainMaterial == null)
                {
                    terrainMaterial = new Material(terrainShader) { name = name };
                    AssetDatabase.CreateAsset(terrainMaterial, materialPath);
                }

                terrain.materialTemplate = terrainMaterial;
            }

            terrainObject.transform.SetParent(parent, false);
            terrainObject.transform.localPosition = position;
        }

        /// <summary>
        /// A grass tuft texture, 128 px: a dozen tapering, slightly bent blades, darker at the root, on transparency.
        /// Generated once and kept as an asset.
        /// </summary>
        private static Texture2D GrassTuftTexture()
        {
            string path = $"{TerrainFolder}/GrassTuft.asset";
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture != null)
            {
                return texture;
            }

            const int size = 128;
            texture = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = "GrassTuft", wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[size * size];
            var random = new System.Random(7);
            for (int blade = 0; blade < 16; blade++)
            {
                float root = 14f + (float)random.NextDouble() * 100f;
                float tall = 55f + (float)random.NextDouble() * 70f;
                float lean = ((float)random.NextDouble() - 0.5f) * 40f;
                float width = 3f + (float)random.NextDouble() * 3f;
                float shade = 0.85f + 0.3f * (float)random.NextDouble();
                for (int y = 0; y < size && y < tall; y++)
                {
                    float t = y / tall;
                    float centre = root + lean * t * t;
                    float half = width * (1f - t) * 0.5f + 0.3f;
                    var colour = Color.Lerp(new Color(0.20f, 0.30f, 0.10f), new Color(0.50f, 0.66f, 0.26f), t) * shade;
                    colour.a = 1f;
                    for (int x = Mathf.Max(0, Mathf.FloorToInt(centre - half)); x <= Mathf.Min(size - 1, Mathf.CeilToInt(centre + half)); x++)
                    {
                        pixels[y * size + x] = colour;
                    }
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            AssetDatabase.CreateAsset(texture, path);
            return texture;
        }

        /// <summary>
        /// A simple conifer ~14 m tall (a trunk and stacked cones, 7 sides, ~30 triangles: thousands are cheap) saved
        /// as a mesh and a prefab for the terrain's trees; materials with GPU instancing.
        /// </summary>
        private static GameObject ConiferPrefab(string name, GreyboxMaterials materials, int tiers, float widthRatio, Color needles)
        {
            const int sides = 7;
            const float height = 14f;
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var trunk = new List<int>();
            var crown = new List<int>();

            // Trunk: an open five-sided prism up to where the lowest cone starts.
            const int trunkSides = 5;
            const float trunkRadius = 0.25f;
            const float trunkTop = 3f;
            for (int i = 0; i < trunkSides; i++)
            {
                float a0 = i * Mathf.PI * 2f / trunkSides;
                float a1 = (i + 1) * Mathf.PI * 2f / trunkSides;
                var d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0));
                var d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
                int start = vertices.Count;
                vertices.AddRange(new[] { d0 * trunkRadius, d1 * trunkRadius, d1 * trunkRadius + Vector3.up * trunkTop, d0 * trunkRadius + Vector3.up * trunkTop });
                normals.AddRange(new[] { d0, d1, d1, d0 });
                trunk.AddRange(new[] { start, start + 2, start + 1, start, start + 3, start + 2 });
            }

            // Crown: cones from wide and low to narrow and high, overlapping.
            for (int tier = 0; tier < tiers; tier++)
            {
                float t = tier / (float)tiers;
                float baseY = 2f + t * (height - 4f);
                float topY = Mathf.Min(height, baseY + (height - 2f) / tiers * 1.9f);
                float radius = height * widthRatio * (1f - t * 0.75f);
                float slope = radius / (topY - baseY);
                for (int i = 0; i < sides; i++)
                {
                    float a0 = (i + tier * 0.5f) * Mathf.PI * 2f / sides;
                    float a1 = (i + 1 + tier * 0.5f) * Mathf.PI * 2f / sides;
                    var d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0));
                    var d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
                    var dm = (d0 + d1).normalized;
                    int start = vertices.Count;
                    vertices.AddRange(new[] { d0 * radius + Vector3.up * baseY, d1 * radius + Vector3.up * baseY, Vector3.up * topY });
                    normals.AddRange(new[] { (d0 + Vector3.up * slope).normalized, (d1 + Vector3.up * slope).normalized, (dm + Vector3.up * slope).normalized });
                    crown.AddRange(new[] { start, start + 2, start + 1 });
                }
            }

            string meshPath = $"{TerrainFolder}/{name}.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (mesh == null)
            {
                mesh = new Mesh { name = name };
                AssetDatabase.CreateAsset(mesh, meshPath);
            }

            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(trunk, 0);
            mesh.SetTriangles(crown, 1);
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);

            Material bark = materials.Colored("ConiferBark", new Color(0.24f, 0.17f, 0.11f));
            Material leaves = materials.Colored(name + "Needles", needles);
            bark.enableInstancing = true;
            leaves.enableInstancing = true;

            var tree = new GameObject(name);
            tree.AddComponent<MeshFilter>().sharedMesh = mesh;
            tree.AddComponent<MeshRenderer>().sharedMaterials = new[] { bark, leaves };
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(tree, $"{TerrainFolder}/{name}.prefab");
            Object.DestroyImmediate(tree);
            return prefab;
        }

        // ---- Noise ----

        /// <summary>Fractal Perlin noise, about 0..1 (Unity's Perlin; keep coordinates positive, it mirrors at 0).</summary>
        private static float Fbm(float x, float z, int octaves)
        {
            float sum = 0f;
            float amplitude = 0.5f;
            float norm = 0f;
            for (int i = 0; i < octaves; i++)
            {
                sum += Mathf.PerlinNoise(x + i * 17.31f, z + i * 31.77f) * amplitude;
                norm += amplitude;
                x *= 2.03f;
                z *= 2.03f;
                amplitude *= 0.5f;
            }

            return sum / norm;
        }

        /// <summary>Ridged multifractal noise, 0..1: sharp crests (mountain ridges), smoother valleys.</summary>
        private static float Ridged(float x, float z, int octaves)
        {
            float sum = 0f;
            float amplitude = 0.5f;
            float norm = 0f;
            float weight = 1f;
            for (int i = 0; i < octaves; i++)
            {
                float n = 1f - Mathf.Abs(Mathf.PerlinNoise(x + i * 13.7f, z + i * 29.3f) * 2f - 1f);
                n *= n * weight;
                weight = Mathf.Clamp01(n * 1.6f);
                sum += n * amplitude;
                norm += amplitude;
                x *= 2.07f;
                z *= 2.07f;
                amplitude *= 0.5f;
            }

            return sum / norm;
        }
    }
}
