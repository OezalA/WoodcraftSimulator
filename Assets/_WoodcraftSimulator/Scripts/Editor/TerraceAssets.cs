using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using WoodcraftSimulator.Construction;
using WoodcraftSimulator.Jobs;
using WoodcraftSimulator.Materials;

namespace WoodcraftSimulator.Editor
{
    /// <summary>
    /// Phase 10 (D-046): a small ground-level terrace 2.4 × 1.8 m built outside the workshop. Layers, bottom up:
    /// six concrete pier blocks on prepared gravel spots, two beams on edge (2400 × 145 × 45) fixed to the piers with
    /// steel U beam brackets anchored in the blocks (D-048), five joists (1800 × 120 × 45) on the beams fixed with
    /// steel angle brackets (D-048), twelve larch deck boards (2400 × 145 × 28, 5 mm gaps) screwed to the outer and middle joists. Origin = ground at the terrace centre.
    /// </summary>
    public static class TerraceAssets
    {
        public const string BlueprintId = "terrace-2400x1800";
        public const string JobId = "terrace-job";
        public const string DeckScrewId = "deck-screw-5x60";
        public const string JoistHangerId = "joist-hanger";
        public const string PostBaseId = "post-base-plate";

        public static readonly float[] PierX = { -1.1f, 0f, 1.1f };
        public static readonly float[] BeamZ = { -0.8f, 0.8f };
        public static readonly float[] JoistX = { -1.1775f, -0.59f, 0f, 0.59f, 1.1775f };

        public static FastenerDefinition DeckScrew() => BlueprintAssets.GetOrCreateFastener("DeckScrew5x60", DeckScrewId,
            "Deck screw 5 × 60 mm", 5f, 60f, FastenerKind.Screw);

        /// <summary>Top of the concrete pier blocks above the ground (m).</summary>
        public const float PierTop = 0.2f;

        /// <summary>Steel U beam bracket on each pier block (D-048): flange height and sheet thickness (m).</summary>
        public const float BracketFlangeHeight = 0.1f;
        public const float BracketSheet = 0.003f;

        /// <summary>Top of the beams above the ground (m): the joists sit on it.</summary>
        public const float BeamTop = PierTop + 0.145f;

        /// <summary>Leg length and width of the angle bracket fixing a joist to a beam (m, D-048).</summary>
        public const float AngleLeg = 0.04f;

        /// <summary>
        /// Side (+1 = +X, −1 = −X) of joist <paramref name="joist"/> that carries its angle brackets: towards the
        /// terrace centre, so the bracket's foot on the beam never sticks out past the beam end.
        /// </summary>
        public static float AngleBracketSide(int joist) => JoistX[joist] < 0f ? 1f : -1f;

        /// <summary>
        /// The joist-to-beam connector: a steel angle bracket, one leg on the beam top beside the joist, the other
        /// screwed to the joist's side (D-048). Id kept from D-046 ("joist hanger") so saves stay valid.
        /// </summary>
        public static FastenerDefinition JoistHanger()
        {
            FastenerDefinition fastener = BlueprintAssets.GetOrCreateFastener("JoistHanger", JoistHangerId,
                "Angle bracket with screws", 5f, 40f, FastenerKind.Connector);
            return Rename(fastener, "Angle bracket with screws", "Köşe bağlantı demiri (vidalarıyla)");
        }

        private static FastenerDefinition Rename(FastenerDefinition fastener, string name, string nameTurkish)
        {
            var serialized = new SerializedObject(fastener);
            serialized.FindProperty("displayName").stringValue = name;
            serialized.FindProperty("displayNameTurkish").stringValue = nameTurkish;
            if (serialized.ApplyModifiedPropertiesWithoutUndo())
            {
                AssetDatabase.SaveAssetIfDirty(fastener);
            }

            return fastener;
        }

        /// <summary>
        /// The pier-to-beam connector: a steel U beam bracket anchored in the pier block; the beam sits in it and is
        /// screwed through the flanges (D-048, owner feedback). Id kept from D-046 so saves stay valid.
        /// </summary>
        public static FastenerDefinition PostBase()
        {
            FastenerDefinition fastener = BlueprintAssets.GetOrCreateFastener("PostBasePlate", PostBaseId,
                "U beam bracket with screws", 5f, 40f, FastenerKind.Connector);
            return Rename(fastener, "U beam bracket with screws", "U kiriş ayağı (vidalarıyla)");
        }

        public static Vector3 PierPosition(int index) => new Vector3(PierX[index % 3], 0f, BeamZ[index / 3]);

        public static BlueprintDefinition Terrace()
        {
            string path = $"{BlueprintAssets.Folder}/Terrace2400x1800.asset";
            // Rewritten on every call (data defined here only), so layout changes reach an existing asset (D-048).
            var definition = AssetDatabase.LoadAssetAtPath<BlueprintDefinition>(path);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<BlueprintDefinition>();
                AssetDatabase.CreateAsset(definition, path);
            }

            const float gap = 0.001f;
            const float pierTop = PierTop;
            const float beamTop = BeamTop;
            const float joistTop = beamTop + 0.12f;
            Vector3 onEdgeAlongX = Quaternion.LookRotation(Vector3.right, Vector3.forward).eulerAngles;
            Vector3 onEdgeAlongZ = Quaternion.LookRotation(Vector3.forward, Vector3.right).eulerAngles;
            Vector3 flatAlongX = Quaternion.LookRotation(Vector3.right, Vector3.up).eulerAngles;

            var slots = new List<(string id, string name, string nameTr, Vector3 size, Vector3 position, Vector3 euler, string[] needs)>();
            for (int i = 0; i < 6; i++)
            {
                Vector3 p = PierPosition(i);
                slots.Add(($"pier-{i + 1}", $"Pier block {i + 1}", $"Beton ayak {i + 1}", new Vector3(200f, 200f, 200f),
                    new Vector3(p.x, pierTop / 2f + gap, p.z), Vector3.zero, new string[0]));
            }

            string[] beamIds = { "beam-front", "beam-back" };
            for (int b = 0; b < 2; b++)
            {
                slots.Add((beamIds[b], b == 0 ? "Front beam" : "Back beam", b == 0 ? "Ön kiriş" : "Arka kiriş",
                    new Vector3(2400f, 145f, 45f), new Vector3(0f, pierTop + 0.0725f + gap, BeamZ[b]), onEdgeAlongX,
                    new[] { $"pier-{b * 3 + 1}", $"pier-{b * 3 + 2}", $"pier-{b * 3 + 3}" }));
            }

            for (int j = 0; j < JoistX.Length; j++)
            {
                slots.Add(($"joist-{j + 1}", $"Joist {j + 1}", $"Döşeme altı kirişi {j + 1}", new Vector3(1800f, 120f, 45f),
                    new Vector3(JoistX[j], beamTop + 0.06f + gap, 0f), onEdgeAlongZ, beamIds));
            }

            var joistIds = new string[JoistX.Length];
            for (int j = 0; j < joistIds.Length; j++)
            {
                joistIds[j] = $"joist-{j + 1}";
            }

            for (int d = 0; d < 12; d++)
            {
                slots.Add(($"deck-{d + 1}", $"Deck board {d + 1}", $"Döşeme tahtası {d + 1}", new Vector3(2400f, 145f, 28f),
                    new Vector3(0f, joistTop + 0.014f + gap, DeckZ(d)), flatAlongX, joistIds));
            }

            var serialized = new SerializedObject(definition);
            serialized.FindProperty("id").stringValue = BlueprintId;
            serialized.FindProperty("displayName").stringValue = "Terrace 2.4 × 1.8 m";
            serialized.FindProperty("displayNameTurkish").stringValue = "Teras 2,4 × 1,8 m";
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
                SerializedProperty needs = slot.FindPropertyRelative("prerequisites");
                needs.arraySize = s.needs.Length;
                for (int n = 0; n < s.needs.Length; n++)
                {
                    needs.GetArrayElementAtIndex(n).stringValue = s.needs[n];
                }
            }

            var connections = new List<(string id, string a, string b, string fastener, Vector3 point, Vector3 direction)>();
            for (int i = 0; i < 6; i++)
            {
                // U beam bracket anchored in the pier block (D-048): screwed through its outer flange into the beam.
                int b = i / 3;
                float flange = 0.0225f + BracketSheet;
                float face = BeamZ[b] + (b == 0 ? -flange : flange);
                connections.Add(($"pier-{i + 1}-plate", beamIds[b], $"pier-{i + 1}", PostBaseId,
                    new Vector3(PierX[i % 3], pierTop + BracketFlangeHeight / 2f, face), b == 0 ? Vector3.forward : Vector3.back));
            }

            for (int j = 0; j < JoistX.Length; j++)
            {
                for (int b = 0; b < 2; b++)
                {
                    // Angle bracket (D-048): foot on the beam top beside the joist, upright leg screwed into the joist's
                    // side (the side towards the terrace centre).
                    float side = AngleBracketSide(j);
                    connections.Add(($"joist-{j + 1}-{beamIds[b]}", $"joist-{j + 1}", beamIds[b], JoistHangerId,
                        new Vector3(JoistX[j] + side * (0.0225f + BracketSheet), beamTop + AngleLeg / 2f, BeamZ[b]),
                        Vector3.right * -side));
                }
            }

            int[] screwedJoists = { 0, 2, 4 };
            for (int d = 0; d < 12; d++)
            {
                foreach (int j in screwedJoists)
                {
                    // One deck screw per crossing with the outer and middle joists; the ends are 22.5 mm from the board
                    // ends, so they need pilot holes (red points).
                    connections.Add(($"deck-{d + 1}-joist-{j + 1}", $"deck-{d + 1}", $"joist-{j + 1}", DeckScrewId,
                        new Vector3(JoistX[j], joistTop + 0.028f + gap, DeckZ(d)), Vector3.down));
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

        /// <summary>Centre of deck board <paramref name="index"/> along Z: 145 mm boards with 5 mm gaps.</summary>
        private static float DeckZ(int index) => -0.9f + 0.0725f + index * 0.15f;

        public static JobDefinition TerraceJob(IReadOnlyDictionary<string, WoodSpeciesDefinition> species)
        {
            string path = $"{JobAssets.Folder}/Job_Terrace.asset";
            var definition = AssetDatabase.LoadAssetAtPath<JobDefinition>(path);
            if (definition != null)
            {
                return definition;
            }

            definition = ScriptableObject.CreateInstance<JobDefinition>();
            AssetDatabase.CreateAsset(definition, path);
            var serialized = new SerializedObject(definition);
            serialized.FindProperty("id").stringValue = JobId;
            serialized.FindProperty("displayName").stringValue = "Small terrace";
            serialized.FindProperty("displayNameTurkish").stringValue = "Küçük teras";
            serialized.FindProperty("description").stringValue =
                "A ground-level terrace 2.4 × 1.8 m in front of the workshop: pier blocks, beams, joists, larch decking.";
            serialized.FindProperty("descriptionTurkish").stringValue =
                "Atölyenin önünde zemine yakın 2,4 × 1,8 m teras: beton ayaklar, kirişler, döşeme altı kirişleri, melez döşeme.";
            serialized.FindProperty("blueprint").objectReferenceValue = Terrace();
            serialized.FindProperty("stockSpecies").objectReferenceValue = species["larch"];
            serialized.FindProperty("stockLengthMillimeters").floatValue = 2400f;
            serialized.FindProperty("stockWidthMillimeters").floatValue = 145f;
            serialized.FindProperty("stockThicknessMillimeters").floatValue = 28f;
            serialized.FindProperty("payEuros").floatValue = 650f;
            serialized.FindProperty("depositShare").floatValue = 0.5f;

            SerializedProperty extras = serialized.FindProperty("extraStocks");
            extras.arraySize = 3;
            SetExtra(extras.GetArrayElementAtIndex(0), species["spruce"], 2400f, 145f, 45f);
            SetExtra(extras.GetArrayElementAtIndex(1), species["spruce"], 2400f, 120f, 45f);
            SetExtra(extras.GetArrayElementAtIndex(2), species["concrete"], 200f, 200f, 200f);

            SetStrings(serialized.FindProperty("tips"), new[]
            {
                "Outside, in front of the door. Set out the site at the corner stake, then dig and gravel each pier spot.",
                "Build bottom up: pier blocks, beams, joists (cut 1800 mm on the sawhorses outside), deck boards.",
                "Beams and joists are fixed with steel connectors (drill-driver); deck screws at the board ends need pilot holes.",
                "The customer paid half as a deposit for the material; the rest comes on delivery."
            });
            SetStrings(serialized.FindProperty("tipsTurkish"), new[]
            {
                "Dışarıda, kapının önünde. Köşe kazığında alanı işaretle, sonra her ayak yerini kaz ve çakıl dök.",
                "Aşağıdan yukarı kur: beton ayaklar, kirişler, döşeme altı kirişleri (dışarıdaki sehpalarda 1800 mm kes), döşeme tahtaları.",
                "Kirişler ve döşeme altı kirişleri çelik bağlantı elemanlarıyla sabitlenir (şarjlı matkap); tahta uçlarındaki vidalar pilot delik ister.",
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
