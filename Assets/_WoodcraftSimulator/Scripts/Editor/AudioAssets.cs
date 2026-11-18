using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using WoodcraftSimulator.Audio;

namespace WoodcraftSimulator.Editor
{
    /// <summary>
    /// The sound library (Phase 13d, D-061), filled from the CC0 sound files in the project by their names (Kenney's
    /// "footstep_wood_000.ogg", "impactGlass_light_002.ogg", "click_003.ogg" ...). Rebuilt on every scene build; a
    /// sound that isn't there leaves its list empty (the generated placeholder plays).
    /// </summary>
    public static class AudioAssets
    {
        public const string AudioFolder = ArtLibrary.Root + "/Audio";
        private const string LibraryPath = "Assets/_WoodcraftSimulator/ScriptableObjects/Audio/SoundLibrary.asset";

        public static SoundLibrary Library()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_WoodcraftSimulator/ScriptableObjects/Audio"))
            {
                AssetDatabase.CreateFolder("Assets/_WoodcraftSimulator/ScriptableObjects", "Audio");
            }

            var library = AssetDatabase.LoadAssetAtPath<SoundLibrary>(LibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<SoundLibrary>();
                AssetDatabase.CreateAsset(library, LibraryPath);
            }

            library.footstepsConcrete = Clips("footstep_concrete_");
            library.footstepsWood = Clips("footstep_wood_");
            library.footstepsGrass = Clips("footstep_grass_");
            library.footstepsSnow = Clips("footstep_snow_");
            library.impactWood = Concat(Clips("impactWood_"), Clips("impactPlank_"));
            library.impactMetal = Clips("impactMetal_light_");
            library.impactGlass = Clips("impactGlass_light_");
            library.uiClick = Clips("click_");
            library.uiConfirm = Clips("confirmation_");
            library.uiError = Clips("error_");
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssetIfDirty(library);
            return library;
        }

        private static AudioClip[] Clips(string prefix)
        {
            var clips = new List<AudioClip>();
            if (!AssetDatabase.IsValidFolder(AudioFolder))
            {
                return clips.ToArray();
            }

            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { AudioFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileName(path).StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase))
                {
                    clips.Add(AssetDatabase.LoadAssetAtPath<AudioClip>(path));
                }
            }

            clips.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return clips.ToArray();
        }

        private static AudioClip[] Concat(AudioClip[] a, AudioClip[] b)
        {
            var all = new List<AudioClip>(a);
            all.AddRange(b);
            return all.ToArray();
        }
    }
}
