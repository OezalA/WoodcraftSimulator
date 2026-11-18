using System.IO;
using UnityEditor;
using UnityEngine;

namespace WoodcraftSimulator.Editor
{
    /// <summary>
    /// CC0 art in the repository (Phase 13a, D-058): texture sets under <see cref="TextureFolder"/>, one folder per set,
    /// as downloaded (ambientCG "Wood049_1K-JPG_Color.jpg" or Poly Haven "oak_veneer_01_diff_1k.jpg" naming). The
    /// builder asks <see cref="TryApply"/> for a set; when it isn't there the material keeps its greybox colour, so the
    /// game works without any art. Sources and licences: CREDITS.md in <see cref="Root"/>.
    /// </summary>
    public static class ArtLibrary
    {
        public const string Root = "Assets/_WoodcraftSimulator/Art/ThirdParty/CC0";
        public const string TextureFolder = Root + "/Textures";
        public const string HdriFolder = Root + "/Hdri";

        /// <summary>A sky panorama (Poly Haven "name_2k.hdr"), or null when it isn't in the project.</summary>
        public static Texture2D Sky(string name) =>
            AssetDatabase.LoadAssetAtPath<Texture2D>($"{HdriFolder}/{name}_2k.hdr");

        // File name parts that identify a map (ambientCG, Poly Haven).
        private static readonly string[] ColorKeys = { "_Color", "_diff", "_albedo", "_basecolor" };
        private static readonly string[] NormalKeys = { "_NormalGL", "_nor_gl" };
        private static readonly string[] OcclusionKeys = { "_AmbientOcclusion", "_ao" };
        internal static readonly string[] LinearKeys = { "_Roughness", "_rough", "_AmbientOcclusion", "_ao", "_Displacement", "_disp", "_Metalness", "_metal" };

        /// <summary>Whether a texture set has been added (its folder exists with a colour map).</summary>
        public static bool Has(string set) => Find(set, ColorKeys) != null;

        /// <summary>
        /// Puts a texture set on a URP Lit material: colour (white tint, the texture carries the colour), normal and
        /// occlusion maps, the tiling (repeats per metre of the meshes' metre-based UVs) and a smoothness. False (nothing
        /// changed) when the set isn't there.
        /// </summary>
        public static bool TryApply(Material material, string set, Vector2 metresPerTile, float smoothness, Color tint)
        {
            Texture2D color = Find(set, ColorKeys);
            if (material == null || color == null)
            {
                return false;
            }

            material.SetTexture("_BaseMap", color);
            material.SetColor("_BaseColor", tint);
            Vector2 tiling = new Vector2(1f / Mathf.Max(0.01f, metresPerTile.x), 1f / Mathf.Max(0.01f, metresPerTile.y));
            material.SetTextureScale("_BaseMap", tiling);

            Texture2D normal = Find(set, NormalKeys);
            material.SetTexture("_BumpMap", normal);
            if (normal != null)
            {
                material.EnableKeyword("_NORMALMAP");
            }
            else
            {
                material.DisableKeyword("_NORMALMAP");
            }

            Texture2D occlusion = Find(set, OcclusionKeys);
            material.SetTexture("_OcclusionMap", occlusion);
            if (occlusion != null)
            {
                material.EnableKeyword("_OCCLUSIONMAP");
            }

            material.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(material);
            return true;
        }

        private static Texture2D Find(string set, string[] keys)
        {
            string folder = $"{TextureFolder}/{set}";
            if (!AssetDatabase.IsValidFolder(folder))
            {
                return null;
            }

            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string file = Path.GetFileNameWithoutExtension(path);
                foreach (string key in keys)
                {
                    if (file.IndexOf(key, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    }
                }
            }

            return null;
        }
    }

    /// <summary>
    /// Import settings for the CC0 textures (D-058): normal maps as normal maps, data maps (roughness, occlusion,
    /// height) linear, everything at most 1024 px (owner: 1K, keeps the LFS-stored repository small).
    /// </summary>
    public sealed class ArtTextureImport : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            // Sky panoramas (Phase 13c): 2K (owner), no mipmaps (no seams at the horizon), wrap around, not up and down.
            if (assetPath.StartsWith(ArtLibrary.HdriFolder))
            {
                var sky = (TextureImporter)assetImporter;
                sky.maxTextureSize = 2048;
                sky.mipmapEnabled = false;
                sky.wrapModeU = TextureWrapMode.Repeat;
                sky.wrapModeV = TextureWrapMode.Clamp;
                return;
            }

            if (!assetPath.StartsWith(ArtLibrary.TextureFolder))
            {
                return;
            }

            var importer = (TextureImporter)assetImporter;
            string file = Path.GetFileNameWithoutExtension(assetPath);
            importer.maxTextureSize = 1024;
            if (file.IndexOf("_NormalGL", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                file.IndexOf("_nor_gl", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                importer.textureType = TextureImporterType.NormalMap;
                return;
            }

            foreach (string key in ArtLibrary.LinearKeys)
            {
                if (file.IndexOf(key, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    importer.sRGBTexture = false;
                    return;
                }
            }
        }
    }
}
