using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using WoodcraftSimulator.Construction;
using WoodcraftSimulator.Cutting;
using WoodcraftSimulator.Inventory;
using WoodcraftSimulator.Jobs;
using WoodcraftSimulator.Materials;

namespace WoodcraftSimulator.Editor
{
    /// <summary>
    /// Fills the Turkish names of the data assets (D-041), by id. Only empty Turkish fields are written, so a name
    /// edited in the Inspector is kept. Runs with "Build Test Workshop Scene" and from its own menu item.
    /// </summary>
    public static class TurkishNames
    {
        private static readonly Dictionary<string, string> Species = new Dictionary<string, string>
        {
            { "spruce", "Ladin" }, { "pine", "Çam" }, { "oak", "Meşe" }, { "beech", "Kayın" },
            { "walnut", "Ceviz" }, { "larch", "Karaçam" }, { "douglas-fir", "Duglas" }
        };

        private static readonly Dictionary<string, string> Items = new Dictionary<string, string>
        {
            { "tape-measure", "Metre" }, { "drill-driver", "Şarjlı matkap" }, { "hand-saw", "El testeresi" },
            { "carpenter-pencil", "Marangoz kalemi" },
            { "hammer", "Çekiç" }, { "glue-bottle", "Ahşap tutkalı" }, { "sanding-block", "Zımpara takozu" },
            { "oil-brush", "Ahşap yağı ve fırça" }
        };

        private static readonly Dictionary<string, string> Fasteners = new Dictionary<string, string>
        {
            { "wood-screw-5x80", "Ahşap vidası 5 × 80 mm" }, { "wood-screw-4x40", "Ahşap vidası 4 × 40 mm" },
            { "nail-2.8x50", "Tel çivi 2,8 × 50 mm" }, { "dowel-8x40", "Ahşap kavela 8 × 40 mm" }
        };

        private static readonly Dictionary<string, string> Blueprints = new Dictionary<string, string>
        {
            { "frame-600x300", "Çerçeve 600 × 300 mm" }, { "wall-shelf-500x600", "Raf 500 × 600 mm" }
        };

        /// <summary>Slot ids mean the same part in every blueprint so far.</summary>
        private static readonly Dictionary<string, string> Slots = new Dictionary<string, string>
        {
            { "front", "Ön kayıt" }, { "back", "Arka kayıt" }, { "left", "Sol yan" }, { "right", "Sağ yan" },
            { "bottom", "Alt raf" }, { "middle", "Orta raf" }, { "top", "Üst raf" }
        };

        private sealed class JobTexts
        {
            public string Name;
            public string Description;
            public string[] Tips;
        }

        private static readonly Dictionary<string, JobTexts> Jobs = new Dictionary<string, JobTexts>
        {
            {
                "frame-600x300-job", new JobTexts
                {
                    Name = "Alıştırma çerçevesi",
                    Description = "Yapı kerestesinden 600 × 300 mm çerçeve: iki kayıt, aralarında iki yan, her köşede " +
                                  "iki adet 5 × 80 mm vida.",
                    Tips = new[]
                    {
                        "Metrenin kancasını ölçtüğün uca tak; testere payı çizginin artık tarafında kalsın.",
                        "Köşe vidaları kayıt uçlarına yakın (kırmızı noktalar): önce pilot delik aç (Q matkap ucuna geçer), yoksa kayıt çatlar."
                    }
                }
            },
            {
                "shelf-500x600-job", new JobTexts
                {
                    Name = "Küçük raf",
                    Description = "18 mm ladin tahtadan 500 genişlik, 600 yükseklik, 145 derinlikte ayaklı raf: iki yan, " +
                                  "aralarında üç raf, her bağlantıda iki adet 4 × 40 mm vida.",
                    Tips = new[]
                    {
                        "Üst ve alt raflar yan tahtaların ucuna 9 mm mesafeden vidalanır (kırmızı noktalar): önce bunlara pilot delik aç (Q).",
                        "Düşük torkla başla, vida kendiliğinden hizada durana kadar torku artır."
                    }
                }
            }
        };

        [MenuItem("WoodcraftSimulator/Apply Turkish Names")]
        public static void Apply()
        {
            foreach (WoodSpeciesDefinition asset in All<WoodSpeciesDefinition>())
            {
                FillName(asset, Species);
            }

            foreach (ItemDefinition asset in All<ItemDefinition>())
            {
                FillName(asset, Items);
            }

            foreach (SawDefinition asset in All<SawDefinition>())
            {
                FillName(asset, Items);
            }

            foreach (FastenerDefinition asset in All<FastenerDefinition>())
            {
                FillName(asset, Fasteners);
            }

            foreach (BlueprintDefinition asset in All<BlueprintDefinition>())
            {
                var serialized = new SerializedObject(asset);
                FillString(serialized, "displayNameTurkish", Blueprints, serialized.FindProperty("id").stringValue);
                SerializedProperty slots = serialized.FindProperty("slots");
                for (int i = 0; i < slots.arraySize; i++)
                {
                    SerializedProperty slot = slots.GetArrayElementAtIndex(i);
                    if (string.IsNullOrEmpty(slot.FindPropertyRelative("displayNameTurkish").stringValue) &&
                        Slots.TryGetValue(slot.FindPropertyRelative("id").stringValue, out string name))
                    {
                        slot.FindPropertyRelative("displayNameTurkish").stringValue = name;
                    }
                }

                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            foreach (JobDefinition asset in All<JobDefinition>())
            {
                var serialized = new SerializedObject(asset);
                if (!Jobs.TryGetValue(serialized.FindProperty("id").stringValue, out JobTexts text))
                {
                    continue;
                }

                SetIfEmpty(serialized.FindProperty("displayNameTurkish"), text.Name);
                SetIfEmpty(serialized.FindProperty("descriptionTurkish"), text.Description);
                SerializedProperty tips = serialized.FindProperty("tipsTurkish");
                if (tips.arraySize == 0)
                {
                    tips.arraySize = text.Tips.Length;
                    for (int i = 0; i < text.Tips.Length; i++)
                    {
                        tips.GetArrayElementAtIndex(i).stringValue = text.Tips[i];
                    }
                }

                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            AssetDatabase.SaveAssets();
        }

        private static void FillName(Object asset, IReadOnlyDictionary<string, string> names)
        {
            var serialized = new SerializedObject(asset);
            FillString(serialized, "displayNameTurkish", names, serialized.FindProperty("id").stringValue);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void FillString(SerializedObject serialized, string field, IReadOnlyDictionary<string, string> names,
            string id)
        {
            if (id != null && names.TryGetValue(id, out string name))
            {
                SetIfEmpty(serialized.FindProperty(field), name);
            }
        }

        private static void SetIfEmpty(SerializedProperty property, string value)
        {
            if (property != null && string.IsNullOrEmpty(property.stringValue))
            {
                property.stringValue = value;
            }
        }

        private static IEnumerable<T> All<T>() where T : Object
        {
            foreach (string guid in AssetDatabase.FindAssets($"t:{typeof(T).Name}"))
            {
                var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null)
                {
                    yield return asset;
                }
            }
        }
    }
}
