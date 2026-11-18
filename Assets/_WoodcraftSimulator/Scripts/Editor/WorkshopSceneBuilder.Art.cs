using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using WoodcraftSimulator.Core;
using WoodcraftSimulator.Materials;

namespace WoodcraftSimulator.Editor
{
    /// <summary>
    /// Phase 13b (D-058, D-059): the CC0 textures on the greybox. Every look falls back to the plain colour when its
    /// texture set isn't in the project (<see cref="ArtLibrary"/>).
    /// </summary>
    public static partial class WorkshopSceneBuilder
    {
        private const string TexturedFolder = "Assets/_WoodcraftSimulator/Art/Materials/Textured";

        /// <summary>A texture set for a greybox material: metres one texture covers, smoothness.</summary>
        private static readonly Dictionary<string, (string set, float metres, float smoothness)> GreyboxArt =
            new Dictionary<string, (string, float, float)>
            {
                { "Concrete", ("Concrete034", 2f, 0.08f) },
                { "Wall", ("PaintedPlaster017", 2f, 0.05f) },
                { "Workbench", ("Wood048", 1f, 0.2f) },
                { "Steel", ("Metal049A", 0.5f, 0.35f) },
                { "Gravel", ("Gravel043", 1f, 0.05f) },
                { "IslandGrass", ("Grass004", 2f, 0.05f) },
                { "Sand", ("Ground080", 2f, 0.05f) }
            };

        /// <summary>Wood (and the non-timber pieces) by species id: texture set, grain upright in the texture, smoothness.</summary>
        private static readonly Dictionary<string, (string set, bool grainOnV, float smoothness)> SpeciesArt =
            new Dictionary<string, (string, bool, float)>
            {
                { "spruce", ("Wood022", false, 0.12f) },
                { "pine", ("Wood050", false, 0.12f) },
                { "larch", ("Wood052", false, 0.14f) },
                { "douglas-fir", ("Wood081", true, 0.14f) },
                { "oak", ("Wood049", false, 0.15f) },
                { "beech", ("Wood034", false, 0.15f) },
                { "walnut", ("Wood067", false, 0.18f) },
                { "concrete", ("Concrete034", false, 0.06f) }
            };

        /// <summary>Textures the greybox materials that have a set; the rest keep their colour.</summary>
        private static void ApplyArt(GreyboxMaterials materials)
        {
            foreach (KeyValuePair<string, (string set, float metres, float smoothness)> entry in GreyboxArt)
            {
                if (!ArtLibrary.Has(entry.Value.set))
                {
                    continue; // not in the project: the material keeps its greybox colour
                }

                Material material = materials.Colored(entry.Key, Color.grey);
                ArtLibrary.TryApply(material, entry.Value.set, Vector2.one * entry.Value.metres, entry.Value.smoothness, Color.white);
            }

            // The ground plane's UVs span its 40 m: tile the grass every 3 m.
            Material ground = materials.Ground;
            if (ArtLibrary.TryApply(ground, "Grass004", Vector2.one * 3f, 0.05f, Color.white))
            {
                ground.SetTextureScale("_BaseMap", Vector2.one * (40f / 3f));
            }
        }

        /// <summary>
        /// Each species gets its board material: its wood texture, or none (boards keep the greybox colour). Glass
        /// panels get a see-through material either way.
        /// </summary>
        private static void ApplySpeciesArt(IReadOnlyDictionary<string, WoodSpeciesDefinition> species)
        {
            EnsureFolder(TexturedFolder);
            foreach (WoodSpeciesDefinition definition in species.Values)
            {
                Material material = null;
                bool grainOnV = false;
                if (definition.Id == "glass")
                {
                    material = GlassMaterial();
                }
                else if (SpeciesArt.TryGetValue(definition.Id, out (string set, bool grainOnV, float smoothness) art) &&
                         ArtLibrary.Has(art.set))
                {
                    material = TexturedMaterial($"Species_{definition.Id}");
                    ArtLibrary.TryApply(material, art.set, Vector2.one, art.smoothness, Color.white);
                    grainOnV = art.grainOnV;
                }

                var serialized = new SerializedObject(definition);
                serialized.FindProperty("boardMaterial").objectReferenceValue = material;
                serialized.FindProperty("grainAlongTextureV").boolValue = grainOnV;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssetIfDirty(definition);
            }
        }

        private static Material TexturedMaterial(string name)
        {
            string path = $"{TexturedFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }

            return material;
        }

        /// <summary>Toughened glass panels (D-055): a light blue tint, see-through, glossy.</summary>
        private static Material GlassMaterial()
        {
            Material glass = TexturedMaterial("Species_glass");
            glass.SetFloat("_Surface", 1f); // transparent
            glass.SetFloat("_Blend", 0f); // alpha
            glass.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            glass.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            glass.SetFloat("_ZWrite", 0f);
            glass.SetOverrideTag("RenderType", "Transparent");
            glass.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            glass.renderQueue = (int)RenderQueue.Transparent;
            glass.SetColor("_BaseColor", new Color(0.72f, 0.88f, 0.92f, 0.28f));
            glass.SetFloat("_Smoothness", 0.95f);
            EditorUtility.SetDirty(glass);
            return glass;
        }

        /// <summary>
        /// The builder makes boxes from Unity's cube scaled to size, whose UVs stretch one texture over every face. Boxes
        /// with a textured material get a cube mesh with metre-based UVs for their scale instead (shared per size), so
        /// a wall, a floor and a sawhorse leg show the texture at the same physical scale.
        /// </summary>
        private static void UseMetreUVs(Scene scene)
        {
            var meshes = new Dictionary<Vector3Int, Mesh>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
                {
                    var renderer = filter.GetComponent<MeshRenderer>();
                    if (filter.sharedMesh == null || filter.sharedMesh.name != "Cube" || renderer == null ||
                        renderer.sharedMaterial == null || renderer.sharedMaterial.GetTexture("_BaseMap") == null)
                    {
                        continue;
                    }

                    Vector3 scale = filter.transform.lossyScale;
                    var key = new Vector3Int(Mathf.RoundToInt(Mathf.Abs(scale.x) * 1000f), Mathf.RoundToInt(Mathf.Abs(scale.y) * 1000f),
                        Mathf.RoundToInt(Mathf.Abs(scale.z) * 1000f));
                    if (!meshes.TryGetValue(key, out Mesh mesh))
                    {
                        mesh = BoxMeshBuilder.Create(Vector3.one, $"Box {key.x}x{key.y}x{key.z}mm", null,
                            new Vector3(key.x, key.y, key.z) / 1000f);
                        meshes[key] = mesh;
                    }

                    filter.sharedMesh = mesh;
                }
            }
        }

        /// <summary>
        /// The active scene's sky (Phase 13c, D-060): a CC0 panorama on Unity's panoramic skybox, at the given exposure;
        /// without the panorama the default procedural sky stays.
        /// </summary>
        private static void ApplySky(string panorama, float exposure, float rotation)
        {
            Texture2D sky = ArtLibrary.Sky(panorama);
            Shader shader = Shader.Find("Skybox/Panoramic");
            if (sky == null || shader == null)
            {
                return;
            }

            EnsureFolder(TexturedFolder);
            string path = $"{TexturedFolder}/Sky_{panorama}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = $"Sky_{panorama}" };
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetTexture("_MainTex", sky);
            material.SetFloat("_Exposure", exposure);
            material.SetFloat("_Rotation", rotation);
            material.SetFloat("_Mapping", 1f); // latitude-longitude layout
            material.SetFloat("_ImageType", 0f); // 360°
            EditorUtility.SetDirty(material);
            RenderSettings.skybox = material;
        }

        /// <summary>
        /// A reflection probe over a place (D-060): rendered once when the place loads (realtime, on awake), so glossy
        /// things (the sea, glass, steel, oiled wood) reflect its sky without a lighting bake.
        /// </summary>
        private static void AddReflectionProbe(Transform parent, Vector3 center, Vector3 size)
        {
            var probeObject = new GameObject("ReflectionProbe");
            probeObject.transform.SetParent(parent, false);
            probeObject.transform.localPosition = center;
            var probe = probeObject.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.OnAwake;
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            probe.size = size;
            probe.resolution = 128;
            probe.clearFlags = ReflectionProbeClearFlags.Skybox;
            probe.boxProjection = false;
        }

        /// <summary>
        /// Post-processing for every place (D-060): a global volume in the core scene (ACES tonemapping, a little bloom,
        /// slight contrast and saturation, a faint vignette) and post-processing plus SMAA on the player's camera.
        /// </summary>
        private static void AddPostProcessing(Scene core, Camera camera)
        {
            string path = "Assets/_WoodcraftSimulator/Art/Rendering/PostProcessing.asset";
            EnsureFolder("Assets/_WoodcraftSimulator/Art/Rendering");
            AssetDatabase.DeleteAsset(path); // rebuilt from here every time
            var profile = ScriptableObject.CreateInstance<UnityEngine.Rendering.VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);

            var tonemapping = profile.Add<UnityEngine.Rendering.Universal.Tonemapping>(true);
            tonemapping.mode.value = UnityEngine.Rendering.Universal.TonemappingMode.ACES;
            var bloom = profile.Add<UnityEngine.Rendering.Universal.Bloom>(true);
            bloom.intensity.value = 0.25f;
            bloom.threshold.value = 1.1f;
            var color = profile.Add<UnityEngine.Rendering.Universal.ColorAdjustments>(true);
            color.postExposure.value = 0.15f;
            color.contrast.value = 8f;
            color.saturation.value = 6f;
            var vignette = profile.Add<UnityEngine.Rendering.Universal.Vignette>(true);
            vignette.intensity.value = 0.15f;
            foreach (UnityEngine.Rendering.VolumeComponent component in profile.components)
            {
                component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
                AssetDatabase.AddObjectToAsset(component, profile);
            }

            AssetDatabase.SaveAssets();

            var volumeObject = new GameObject("PostProcessing");
            SceneManager.MoveGameObjectToScene(volumeObject, core);
            var volume = volumeObject.AddComponent<UnityEngine.Rendering.Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;

            if (!camera.TryGetComponent(out UnityEngine.Rendering.Universal.UniversalAdditionalCameraData cameraData))
            {
                cameraData = camera.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            }

            cameraData.renderPostProcessing = true;
            cameraData.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        }

        /// <summary>A CC0 terrain layer when the set is there (normal map too), else the generated plain one.</summary>
        private static TerrainLayer TerrainLayerFor(string name, string set, Color fallback, float tileMetres)
        {
            if (!ArtLibrary.Has(set))
            {
                return TerrainLayerAsset(name, fallback);
            }

            string path = $"{TerrainFolder}/{name}_CC0.terrainlayer";
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
            if (layer == null)
            {
                layer = new TerrainLayer();
                AssetDatabase.CreateAsset(layer, path);
            }

            var probe = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            ArtLibrary.TryApply(probe, set, Vector2.one, 0.1f, Color.white);
            layer.diffuseTexture = probe.GetTexture("_BaseMap") as Texture2D;
            layer.normalMapTexture = probe.GetTexture("_BumpMap") as Texture2D;
            layer.tileSize = Vector2.one * tileMetres;
            Object.DestroyImmediate(probe);
            EditorUtility.SetDirty(layer);
            return layer;
        }
    }
}
