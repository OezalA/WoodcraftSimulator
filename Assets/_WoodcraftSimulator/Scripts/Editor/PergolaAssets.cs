using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using WoodcraftSimulator.Construction;
using WoodcraftSimulator.Jobs;
using WoodcraftSimulator.Materials;

namespace WoodcraftSimulator.Editor
{
    /// <summary>
    /// Phase 11 (D-049): a free-standing pergola 3 × 3 m beside the terrace. Bottom up: four concrete pier blocks on
    /// prepared spots, each with a steel U post base anchored in it; four posts 100 × 100 × 2200 (cut from 2400) set
    /// into the bases, plumbed with the spirit level and held by temporary braces; two beams on edge (3000 × 145 × 45)
    /// on the post tops; five rafters on edge (3000 × 120 × 45) on the beams. Beams and rafters are fixed with steel
    /// angle brackets. Dimensions follow common garden-pergola practice; a simplified game structure, not a certified
    /// design (no load calculation). Origin = ground at the pergola centre.
    /// </summary>
    public static class PergolaAssets
    {
        public const string BlueprintId = "pergola-3000x3000";
        public const string JobId = "pergola-job";
        public const string PostAnchorId = "post-anchor";

        public static readonly float[] PostX = { -1.25f, 1.25f };
        public static readonly float[] PostZ = { -1.25f, 1.25f };
        public static readonly float[] RafterX = { -1.4f, -0.7f, 0f, 0.7f, 1.4f };
        public static readonly string[] BeamIds = { "beam-front", "beam-back" };

        /// <summary>Top of the concrete pier blocks above the ground (m).</summary>
        public const float PierTop = 0.2f;

        /// <summary>Steel U post base: inner width (= post), flange height, sheet thickness (m).</summary>
        public const float AnchorInner = 0.1f;
        public const float AnchorFlangeHeight = 0.15f;
        public const float Sheet = 0.003f;

        /// <summary>The post stands on the base plate.</summary>
        public const float PostBottom = PierTop + 0.005f;
        public const float PostLength = 2.2f;
        public const float PostTop = PostBottom + PostLength;
        public const float BeamTop = PostTop + 0.145f;

        /// <summary>Leg of the angle brackets (m), as on the terrace (D-048).</summary>
        public const float AngleLeg = TerraceAssets.AngleLeg;

        /// <summary>Post <paramref name="index"/> 1..4: front left, front right, back left, back right.</summary>
        public static Vector3 PostPosition(int index) => new Vector3(PostX[index % 2], 0f, PostZ[index / 2]);

        /// <summary>The beam over post <paramref name="index"/>.</summary>
        public static int BeamOf(int post) => post / 2;

        /// <summary>Angle brackets sit on the side of the upper member facing the pergola centre (+1 or −1).</summary>
        public static float Inward(float coordinate) => coordinate > 0f ? -1f : 1f;

        /// <summary>Steel U post base anchored in the pier block; the post stands in it and is screwed through a flange.</summary>
        public static FastenerDefinition PostAnchor()
        {
            FastenerDefinition fastener = BlueprintAssets.GetOrCreateFastener("PostAnchor", PostAnchorId,
                "U post base with screws", 5f, 40f, FastenerKind.Connector);
            var serialized = new SerializedObject(fastener);
            SerializedProperty turkish = serialized.FindProperty("displayNameTurkish");
            if (string.IsNullOrEmpty(turkish.stringValue))
            {
                turkish.stringValue = "Direk ayağı (vidalarıyla)";
                serialized.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssetIfDirty(fastener);
            }

            return fastener;
        }

        public static BlueprintDefinition Pergola()
        {
            string path = $"{BlueprintAssets.Folder}/Pergola3000x3000.asset";
            // Rewritten on every call (data defined here only), like the terrace (D-048).
            var definition = AssetDatabase.LoadAssetAtPath<BlueprintDefinition>(path);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<BlueprintDefinition>();
                AssetDatabase.CreateAsset(definition, path);
            }

            const float gap = 0.001f;
            Vector3 upright = new Vector3(-90f, 0f, 0f); // length (local Z) pointing up
            Vector3 onEdgeAlongX = Quaternion.LookRotation(Vector3.right, Vector3.forward).eulerAngles;
            Vector3 onEdgeAlongZ = Quaternion.LookRotation(Vector3.forward, Vector3.right).eulerAngles;

            var slots = new List<(string id, string name, string nameTr, Vector3 size, Vector3 position, Vector3 euler,
                string[] needs, bool plumb)>();
            for (int i = 0; i < 4; i++)
            {
                Vector3 p = PostPosition(i);
                slots.Add(($"pier-{i + 1}", $"Pier block {i + 1}", $"Beton ayak {i + 1}", new Vector3(200f, 200f, 200f),
                    new Vector3(p.x, PierTop / 2f + gap, p.z), Vector3.zero, new string[0], false));
            }

            for (int i = 0; i < 4; i++)
            {
                Vector3 p = PostPosition(i);
                slots.Add(($"post-{i + 1}", $"Post {i + 1}", $"Direk {i + 1}", new Vector3(2200f, 100f, 100f),
                    new Vector3(p.x, PostBottom + PostLength / 2f, p.z), upright, new[] { $"pier-{i + 1}" }, true));
            }

            for (int b = 0; b < 2; b++)
            {
                slots.Add((BeamIds[b], b == 0 ? "Front beam" : "Back beam", b == 0 ? "Ön kiriş" : "Arka kiriş",
                    new Vector3(3000f, 145f, 45f), new Vector3(0f, PostTop + 0.0725f + gap, PostZ[b]), onEdgeAlongX,
                    new[] { $"post-{b * 2 + 1}", $"post-{b * 2 + 2}" }, false));
            }

            for (int r = 0; r < RafterX.Length; r++)
            {
                slots.Add(($"rafter-{r + 1}", $"Rafter {r + 1}", $"Aşık {r + 1}", new Vector3(3000f, 120f, 45f),
                    new Vector3(RafterX[r], BeamTop + 0.06f + gap, 0f), onEdgeAlongZ, BeamIds, false));
            }

            var serialized = new SerializedObject(definition);
            serialized.FindProperty("id").stringValue = BlueprintId;
            serialized.FindProperty("displayName").stringValue = "Pergola 3 × 3 m";
            serialized.FindProperty("displayNameTurkish").stringValue = "Pergola 3 × 3 m";
            serialized.FindProperty("accuracyMillimeters").floatValue = 2f;
            serialized.FindProperty("fitMillimeters").floatValue = 8f;
            SerializedProperty slotsProperty = serialized.FindProperty("slots");
            slotsProperty.arraySize = slots.Count;
            for (int i = 0; i < slots.Count; i++)
            {
                var s = slots[i];
                SerializedProperty slot = slotsProperty.GetArrayElementAtIndex(i);
                slot.FindPropertyRelative("id").stringValue = s.id;
                slot.FindPropertyRelative("displayName").stringValue = s.name;
                slot.FindPropertyRelative("displayNameTurkish").stringValue = s.nameTr;
                slot.FindPropertyRelative("lengthMillimeters").floatValue = s.size.x;
                slot.FindPropertyRelative("widthMillimeters").floatValue = s.size.y;
                slot.FindPropertyRelative("thicknessMillimeters").floatValue = s.size.z;
                slot.FindPropertyRelative("localPosition").vector3Value = s.position;
                slot.FindPropertyRelative("localEulerAngles").vector3Value = s.euler;
                slot.FindPropertyRelative("requiresPlumb").boolValue = s.plumb;
                SerializedProperty needs = slot.FindPropertyRelative("prerequisites");
                needs.arraySize = s.needs.Length;
                for (int n = 0; n < s.needs.Length; n++)
                {
                    needs.GetArrayElementAtIndex(n).stringValue = s.needs[n];
                }
            }

            var connections = new List<(string id, string a, string b, string fastener, Vector3 point, Vector3 direction)>();
            for (int i = 0; i < 4; i++)
            {
                // U post base: screwed through its outer flange (the outside face of the post) into the post.
                Vector3 p = PostPosition(i);
                float outward = -Inward(p.z);
                connections.Add(($"post-{i + 1}-base", $"post-{i + 1}", $"pier-{i + 1}", PostAnchorId,
                    new Vector3(p.x, PostBottom + AnchorFlangeHeight / 2f, p.z + outward * (AnchorInner / 2f + Sheet)),
                    Vector3.forward * -outward));
            }

            for (int i = 0; i < 4; i++)
            {
                // Angle bracket: foot on the post top beside the beam (towards the inside), upright leg screwed into
                // the beam's side.
                Vector3 p = PostPosition(i);
                float side = Inward(p.z);
                connections.Add(($"post-{i + 1}-beam", BeamIds[BeamOf(i)], $"post-{i + 1}", TerraceAssets.JoistHangerId,
                    new Vector3(p.x, PostTop + AngleLeg / 2f, p.z + side * (0.0225f + Sheet)), Vector3.forward * -side));
            }

            for (int r = 0; r < RafterX.Length; r++)
            {
                for (int b = 0; b < 2; b++)
                {
                    float side = RafterSide(r);
                    connections.Add(($"rafter-{r + 1}-{BeamIds[b]}", $"rafter-{r + 1}", BeamIds[b], TerraceAssets.JoistHangerId,
                        new Vector3(RafterX[r] + side * (0.0225f + Sheet), BeamTop + AngleLeg / 2f, PostZ[b]),
                        Vector3.right * -side));
                }
            }

            SerializedProperty connectionsProperty = serialized.FindProperty("connections");
            connectionsProperty.arraySize = connections.Count;
            for (int i = 0; i < connections.Count; i++)
            {
                var c = connections[i];
                SerializedProperty connection = connectionsProperty.GetArrayElementAtIndex(i);
                connection.FindPropertyRelative("id").stringValue = c.id;
                connection.FindPropertyRelative("partA").stringValue = c.a;
                connection.FindPropertyRelative("partB").stringValue = c.b;
                connection.FindPropertyRelative("fastenerId").stringValue = c.fastener;
                SerializedProperty points = connection.FindPropertyRelative("points");
                points.arraySize = 1;
                points.GetArrayElementAtIndex(0).FindPropertyRelative("localPosition").vector3Value = c.point;
                points.GetArrayElementAtIndex(0).FindPropertyRelative("localDirection").vector3Value = c.direction;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssetIfDirty(definition);
            return definition;
        }

        /// <summary>Side (+1 = +X) of rafter <paramref name="index"/> carrying its angle brackets: towards the centre.</summary>
        public static float RafterSide(int index) => RafterX[index] > 0f ? -1f : 1f;

        public static JobDefinition PergolaJob(IReadOnlyDictionary<string, WoodSpeciesDefinition> species)
        {
            string path = $"{JobAssets.Folder}/Job_Pergola.asset";
            var definition = AssetDatabase.LoadAssetAtPath<JobDefinition>(path);
            if (definition != null)
            {
                return definition;
            }

            definition = ScriptableObject.CreateInstance<JobDefinition>();
            AssetDatabase.CreateAsset(definition, path);
            var serialized = new SerializedObject(definition);
            serialized.FindProperty("id").stringValue = JobId;
            serialized.FindProperty("displayName").stringValue = "Garden pergola";
            serialized.FindProperty("displayNameTurkish").stringValue = "Bahçe pergolası";
            serialized.FindProperty("description").stringValue =
                "A free-standing pergola 3 × 3 m beside the terrace: pier blocks, four plumb posts, two beams, five rafters.";
            serialized.FindProperty("descriptionTurkish").stringValue =
                "Terasın yanında serbest duran 3 × 3 m pergola: beton ayaklar, şakülünde dört direk, iki kiriş, beş aşık.";
            serialized.FindProperty("blueprint").objectReferenceValue = Pergola();
            serialized.FindProperty("stockSpecies").objectReferenceValue = species["spruce"];
            serialized.FindProperty("stockLengthMillimeters").floatValue = 3000f;
            serialized.FindProperty("stockWidthMillimeters").floatValue = 120f;
            serialized.FindProperty("stockThicknessMillimeters").floatValue = 45f;
            serialized.FindProperty("payEuros").floatValue = 850f;
            serialized.FindProperty("depositShare").floatValue = 0.5f;

            SerializedProperty extras = serialized.FindProperty("extraStocks");
            extras.arraySize = 3;
            SetExtra(extras.GetArrayElementAtIndex(0), species["spruce"], 3000f, 145f, 45f);
            SetExtra(extras.GetArrayElementAtIndex(1), species["spruce"], 2400f, 100f, 100f);
            SetExtra(extras.GetArrayElementAtIndex(2), species["concrete"], 200f, 200f, 200f);

            SetStrings(serialized.FindProperty("tips"), new[]
            {
                "Outside, right of the terrace. Set out the site at the corner stake, then dig and gravel the four pier spots.",
                "Cut the posts to 2200 mm on the sawhorses. A post never goes in exactly vertical: hold the spirit level against two neighbouring faces, push it upright with the wheel, then nail a temporary brace (left mouse button).",
                "Only braced posts can be fastened or carry a beam. Beams and rafters are higher than you can reach: carry the stepladder there, climb it with Space.",
                "The customer inspects each stage before the next (E on the corner stake): every joint fastened, no screw head standing out. Passing every first inspection pays a bonus.",
                "The customer paid half as a deposit for the material; the rest comes on delivery."
            });
            SetStrings(serialized.FindProperty("tipsTurkish"), new[]
            {
                "Dışarıda, terasın sağında. Köşe kazığında alanı işaretle, sonra dört ayak yerini kaz ve çakıl dök.",
                "Direkleri sehpalarda 2200 mm kes. Direk hiçbir zaman tam dik oturmaz: su terazisini iki komşu yüze tut, tekerlekle düzelt, sonra geçici destek çak (sol tık).",
                "Sadece destekli direk sabitlenebilir ve kiriş taşır. Kirişler ve aşıklar uzanamayacağın yükseklikte: merdiveni oraya taşı, Space ile çık.",
                "Müşteri her aşamayı bir sonrakinden önce denetler (köşe kazığında E): her bağlantı sabit, taşan vida başı yok. Her aşama ilk denetimde geçerse prim var.",
                "Müşteri malzeme için yarısını avans olarak ödedi; kalanı teslimde gelir."
            });
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssetIfDirty(definition);
            return definition;
        }

        private static void SetExtra(SerializedProperty extra, WoodSpeciesDefinition species, float length, float width,
            float thickness)
        {
            extra.FindPropertyRelative("species").objectReferenceValue = species;
            extra.FindPropertyRelative("lengthMillimeters").floatValue = length;
            extra.FindPropertyRelative("widthMillimeters").floatValue = width;
            extra.FindPropertyRelative("thicknessMillimeters").floatValue = thickness;
        }

        private static void SetStrings(SerializedProperty array, IReadOnlyList<string> values)
        {
            array.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++)
            {
                array.GetArrayElementAtIndex(i).stringValue = values[i];
            }
        }
    }
}
