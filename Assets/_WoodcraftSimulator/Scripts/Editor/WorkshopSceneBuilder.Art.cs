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
