using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using WoodcraftSimulator.Construction;
using WoodcraftSimulator.Jobs;
using WoodcraftSimulator.Materials;

namespace WoodcraftSimulator.Editor
{
    /// <summary>
    /// Phase 9 project (D-045): an open tool tote that combines every new process: ripped parts (sides 100 mm, handle
    /// 45 mm), a doweled and glued base, nailed sides, screwed handle, and a sanded and oiled finish.
    /// 436 × 145 × 250 mm outside, 18 mm spruce, lying on the assembly table as it stands.
    /// </summary>
    public static class ToteAssets
    {
        public const string BlueprintId = "tool-tote";
        public const string JobId = "tool-tote-job";

        public static BlueprintDefinition ToolTote()
        {
            string path = $"{BlueprintAssets.Folder}/ToolTote.asset";
            var definition = AssetDatabase.LoadAssetAtPath<BlueprintDefinition>(path);
            if (definition != null)
            {
                return definition;
            }

            definition = ScriptableObject.CreateInstance<BlueprintDefinition>();
            AssetDatabase.CreateAsset(definition, path);

            const float t = 0.018f;
            const float gap = 0.001f;
            // Board axes: length = local Z, width = X, thickness = Y.
            Vector3 lyingAlongX = Quaternion.LookRotation(Vector3.right, Vector3.up).eulerAngles;      // base
            Vector3 standingAcrossX = Quaternion.LookRotation(Vector3.up, Vector3.right).eulerAngles;  // ends
            Vector3 onEdgeAlongX = Quaternion.LookRotation(Vector3.right, Vector3.forward).eulerAngles; // sides, handle

            var serialized = new SerializedObject(definition);
            serialized.FindProperty("id").stringValue = BlueprintId;
            serialized.FindProperty("displayName").stringValue = "Tool tote 436 × 145 × 250 mm";
            serialized.FindProperty("displayNameTurkish").stringValue = "Alet sandığı 436 × 145 × 250 mm";

            SerializedProperty slots = serialized.FindProperty("slots");
            slots.arraySize = 6;
            SetSlot(slots.GetArrayElementAtIndex(0), "base", "Base", "Taban", 400f, 145f, new Vector3(0f, t / 2f + gap, 0f), lyingAlongX);
            SetSlot(slots.GetArrayElementAtIndex(1), "end-left", "Left end", "Sol baş", 250f, 145f,
                new Vector3(-0.209f, 0.125f + gap, 0f), standingAcrossX, "base");
            SetSlot(slots.GetArrayElementAtIndex(2), "end-right", "Right end", "Sağ baş", 250f, 145f,
                new Vector3(0.209f, 0.125f + gap, 0f), standingAcrossX, "base");
            SetSlot(slots.GetArrayElementAtIndex(3), "side-front", "Front side", "Ön yan", 436f, 100f,
                new Vector3(0f, 0.05f + gap, -0.0815f), onEdgeAlongX, "end-left", "end-right");
            SetSlot(slots.GetArrayElementAtIndex(4), "side-back", "Back side", "Arka yan", 436f, 100f,
                new Vector3(0f, 0.05f + gap, 0.0815f), onEdgeAlongX, "end-left", "end-right");
            SetSlot(slots.GetArrayElementAtIndex(5), "handle", "Handle", "Sap", 400f, 45f,
                new Vector3(0f, 0.2275f + gap, 0f), onEdgeAlongX, "end-left", "end-right");

            var connections = new List<(string id, string a, string b, string fastener, Vector3[] points, Vector3 direction)>
            {
                // Ends doweled to the base: two dowels through each end into the end grain of the base.
                ("end-left-base", "end-left", "base", BlueprintAssets.DowelId,
                    new[] { new Vector3(-0.218f, t / 2f + gap, -0.04f), new Vector3(-0.218f, t / 2f + gap, 0.04f) }, Vector3.right),
                ("end-right-base", "end-right", "base", BlueprintAssets.DowelId,
                    new[] { new Vector3(0.218f, t / 2f + gap, -0.04f), new Vector3(0.218f, t / 2f + gap, 0.04f) }, Vector3.left),
                // Sides nailed to the ends (9 mm from the sides' ends: pre-drill) and to the base edge.
                ("front-left", "side-front", "end-left", BlueprintAssets.NailId,
                    new[] { new Vector3(-0.209f, 0.03f, -0.0905f), new Vector3(-0.209f, 0.075f, -0.0905f) }, Vector3.forward),
                ("front-right", "side-front", "end-right", BlueprintAssets.NailId,
                    new[] { new Vector3(0.209f, 0.03f, -0.0905f), new Vector3(0.209f, 0.075f, -0.0905f) }, Vector3.forward),
                ("back-left", "side-back", "end-left", BlueprintAssets.NailId,
                    new[] { new Vector3(-0.209f, 0.03f, 0.0905f), new Vector3(-0.209f, 0.075f, 0.0905f) }, Vector3.back),
                ("back-right", "side-back", "end-right", BlueprintAssets.NailId,
                    new[] { new Vector3(0.209f, 0.03f, 0.0905f), new Vector3(0.209f, 0.075f, 0.0905f) }, Vector3.back),
                ("front-base", "side-front", "base", BlueprintAssets.NailId,
                    new[] { new Vector3(-0.1f, t / 2f + gap, -0.0905f), new Vector3(0.1f, t / 2f + gap, -0.0905f) }, Vector3.forward),
                ("back-base", "side-back", "base", BlueprintAssets.NailId,
                    new[] { new Vector3(-0.1f, t / 2f + gap, 0.0905f), new Vector3(0.1f, t / 2f + gap, 0.0905f) }, Vector3.back),
                // Handle screwed through the ends (22.5 mm from their top ends: pre-drill).
                ("left-handle", "end-left", "handle", BlueprintAssets.ShelfScrewId,
                    new[] { new Vector3(-0.218f, 0.2275f + gap, 0f) }, Vector3.right),
                ("right-handle", "end-right", "handle", BlueprintAssets.ShelfScrewId,
                    new[] { new Vector3(0.218f, 0.2275f + gap, 0f) }, Vector3.left)
            };

            SerializedProperty connectionsProperty = serialized.FindProperty("connections");
            connectionsProperty.arraySize = connections.Count;
            for (int i = 0; i < connections.Count; i++)
            {
                var spec = connections[i];
                SerializedProperty connection = connectionsProperty.GetArrayElementAtIndex(i);
                connection.FindPropertyRelative("id").stringValue = spec.id;
                connection.FindPropertyRelative("partA").stringValue = spec.a;
                connection.FindPropertyRelative("partB").stringValue = spec.b;
                connection.FindPropertyRelative("fastenerId").stringValue = spec.fastener;
                SerializedProperty points = connection.FindPropertyRelative("points");
                points.arraySize = spec.points.Length;
                for (int p = 0; p < spec.points.Length; p++)
                {
                    points.GetArrayElementAtIndex(p).FindPropertyRelative("localPosition").vector3Value = spec.points[p];
                    points.GetArrayElementAtIndex(p).FindPropertyRelative("localDirection").vector3Value = spec.direction;
                }
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssetIfDirty(definition);
            return definition;
        }

        public static JobDefinition ToteJob(IReadOnlyDictionary<string, WoodSpeciesDefinition> species)
        {
            string path = $"{JobAssets.Folder}/Job_ToolTote.asset";
            var definition = AssetDatabase.LoadAssetAtPath<JobDefinition>(path);
            if (definition != null)
            {
                return definition;
            }

            definition = ScriptableObject.CreateInstance<JobDefinition>();
            AssetDatabase.CreateAsset(definition, path);
            var serialized = new SerializedObject(definition);
            serialized.FindProperty("id").stringValue = JobId;
            serialized.FindProperty("displayName").stringValue = "Tool tote";
            serialized.FindProperty("displayNameTurkish").stringValue = "Alet sandığı";
            serialized.FindProperty("description").stringValue =
                "An open tool tote from 18 mm spruce: doweled and glued base, nailed sides, screwed handle, sanded and oiled.";
            serialized.FindProperty("descriptionTurkish").stringValue =
                "18 mm ladinden açık alet sandığı: kavelalı ve tutkallı taban, çivili yanlar, vidalı sap; zımparalı ve yağlı.";
            serialized.FindProperty("blueprint").objectReferenceValue = ToolTote();
            serialized.FindProperty("stockSpecies").objectReferenceValue = species["spruce"];
            serialized.FindProperty("stockLengthMillimeters").floatValue = 2000f;
            serialized.FindProperty("stockWidthMillimeters").floatValue = 145f;
            serialized.FindProperty("stockThicknessMillimeters").floatValue = 18f;
            serialized.FindProperty("payEuros").floatValue = 90f;
            serialized.FindProperty("requiresFinish").boolValue = true;
            SetStrings(serialized.FindProperty("tips"), new[]
            {
                "Rip the sides (100 mm) and the handle (45 mm): saw in hand, Q for rip mode, the wheel sets the width.",
                "Ends to base: drill the dowel holes, glue the dowels in, then let the clamped joint dry.",
                "The nails near the ends of the sides are red points: pre-drill them, or the sides split.",
                "Sand, then oil every part for the full pay."
            });
            SetStrings(serialized.FindProperty("tipsTurkish"), new[]
            {
                "Yanları (100 mm) ve sapı (45 mm) boyuna kes: testere elde, Q boyuna kesim modu, tekerlek genişliği ayarlar.",
                "Başlar tabana: kavela deliklerini aç, kavelaları tutkalla tak, işkencedeki bağlantının kurumasını bekle.",
                "Yanların uçlarına yakın çiviler kırmızı noktalar: önce delik aç, yoksa yanlar çatlar.",
                "Tam ücret için her parçayı önce zımparala, sonra yağla."
            });
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssetIfDirty(definition);
            return definition;
        }

        private static void SetSlot(SerializedProperty slot, string id, string name, string nameTurkish, float lengthMm,
            float widthMm, Vector3 position, Vector3 euler, params string[] prerequisites)
        {
            slot.FindPropertyRelative("id").stringValue = id;
            slot.FindPropertyRelative("displayName").stringValue = name;
            slot.FindPropertyRelative("displayNameTurkish").stringValue = nameTurkish;
            slot.FindPropertyRelative("lengthMillimeters").floatValue = lengthMm;
            slot.FindPropertyRelative("widthMillimeters").floatValue = widthMm;
            slot.FindPropertyRelative("thicknessMillimeters").floatValue = 18f;
            slot.FindPropertyRelative("localPosition").vector3Value = position;
            slot.FindPropertyRelative("localEulerAngles").vector3Value = euler;
            SetStrings(slot.FindPropertyRelative("prerequisites"), prerequisites);
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
