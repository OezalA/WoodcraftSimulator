using UnityEditor;
using UnityEngine;
using WoodcraftSimulator.Construction;

namespace WoodcraftSimulator.Editor
{
    /// <summary>
    /// Creates the blueprint assets if they don't exist yet (folder created by the scene builder).
    /// Existing assets are never overwritten: once created, the asset is the source of truth.
    /// </summary>
    public static class BlueprintAssets
    {
        public const string Folder = "Assets/_WoodcraftSimulator/ScriptableObjects/Blueprints";
        public const string FastenerFolder = "Assets/_WoodcraftSimulator/ScriptableObjects/Fasteners";
        public const string WoodScrewId = "wood-screw-5x80";

        /// <summary>Generic 5 × 80 mm wood screw (dimensions only, no product data; D-032).</summary>
        public static FastenerDefinition WoodScrew5x80()
        {
            string path = $"{FastenerFolder}/WoodScrew5x80.asset";
            var definition = AssetDatabase.LoadAssetAtPath<FastenerDefinition>(path);
            if (definition != null)
            {
                return definition;
            }

            definition = ScriptableObject.CreateInstance<FastenerDefinition>();
            AssetDatabase.CreateAsset(definition, path);
            var serialized = new SerializedObject(definition);
            serialized.FindProperty("id").stringValue = WoodScrewId;
            serialized.FindProperty("displayName").stringValue = "Wood screw 5 × 80 mm";
            serialized.FindProperty("diameterMillimeters").floatValue = 5f;
            serialized.FindProperty("lengthMillimeters").floatValue = 80f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssetIfDirty(definition);
            return definition;
        }

        /// <summary>
        /// Phase 5 frame (D-032): 600 × 300 mm outside, 95 mm deep, lying on the table with the boards on edge.
        /// Rails 600 mm along X at the front/back, sides 210 mm (300 − 2 × 45) along Z between them (butt joints),
        /// two 5 × 80 mm screws per corner through the rail into the end of the side.
        /// Origin = table top at the frame center.
        /// </summary>
        public static BlueprintDefinition Frame600x300()
        {
            string path = $"{Folder}/Frame600x300.asset";
            var definition = AssetDatabase.LoadAssetAtPath<BlueprintDefinition>(path);
            if (definition != null)
            {
                return definition;
            }

            definition = ScriptableObject.CreateInstance<BlueprintDefinition>();
            AssetDatabase.CreateAsset(definition, path);

            const float thickness = 0.045f;
            const float width = 0.095f;
            const float outerX = 0.6f;
            const float outerZ = 0.3f;
            const float gap = 0.001f;
            float centerY = width / 2f + gap;
            float railZ = outerZ / 2f - thickness / 2f;   // 0.1275
            float sideX = outerX / 2f - thickness / 2f;   // 0.2775

            // Board axes: length = local Z, width = local X, thickness = local Y. On edge: width vertical.
            Vector3 railEuler = Quaternion.LookRotation(Vector3.right, Vector3.forward).eulerAngles;
            Vector3 sideEuler = Quaternion.LookRotation(Vector3.forward, Vector3.right).eulerAngles;

            var serialized = new SerializedObject(definition);
            serialized.FindProperty("id").stringValue = "frame-600x300";
            serialized.FindProperty("displayName").stringValue = "Frame 600 × 300 mm";

            SerializedProperty slots = serialized.FindProperty("slots");
            slots.arraySize = 4;
            SetSlot(slots.GetArrayElementAtIndex(0), "front", "Front rail", 600f, new Vector3(0f, centerY, -railZ), railEuler);
            SetSlot(slots.GetArrayElementAtIndex(1), "back", "Back rail", 600f, new Vector3(0f, centerY, railZ), railEuler);
            SetSlot(slots.GetArrayElementAtIndex(2), "left", "Left side", 210f, new Vector3(-sideX, centerY, 0f), sideEuler);
            SetSlot(slots.GetArrayElementAtIndex(3), "right", "Right side", 210f, new Vector3(sideX, centerY, 0f), sideEuler);

            // Screws enter the outer face of the rail, at the side's center line, 25 mm above/below the middle.
            SerializedProperty connections = serialized.FindProperty("connections");
            connections.arraySize = 4;
            SetCorner(connections.GetArrayElementAtIndex(0), "front-left", "front", "left", -sideX, -outerZ / 2f, centerY);
            SetCorner(connections.GetArrayElementAtIndex(1), "front-right", "front", "right", sideX, -outerZ / 2f, centerY);
            SetCorner(connections.GetArrayElementAtIndex(2), "back-left", "back", "left", -sideX, outerZ / 2f, centerY);
            SetCorner(connections.GetArrayElementAtIndex(3), "back-right", "back", "right", sideX, outerZ / 2f, centerY);

            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssetIfDirty(definition);
            return definition;
        }

        private static void SetSlot(SerializedProperty slot, string id, string displayName, float lengthMm,
            Vector3 position, Vector3 euler)
        {
            slot.FindPropertyRelative("id").stringValue = id;
            slot.FindPropertyRelative("displayName").stringValue = displayName;
            slot.FindPropertyRelative("lengthMillimeters").floatValue = lengthMm;
            slot.FindPropertyRelative("widthMillimeters").floatValue = 95f;
            slot.FindPropertyRelative("thicknessMillimeters").floatValue = 45f;
            slot.FindPropertyRelative("localPosition").vector3Value = position;
            slot.FindPropertyRelative("localEulerAngles").vector3Value = euler;
            slot.FindPropertyRelative("prerequisites").arraySize = 0;
        }

        private static void SetCorner(SerializedProperty connection, string id, string rail, string side,
            float x, float outerFaceZ, float centerY)
        {
            connection.FindPropertyRelative("id").stringValue = id;
            connection.FindPropertyRelative("partA").stringValue = rail;
            connection.FindPropertyRelative("partB").stringValue = side;
            connection.FindPropertyRelative("fastenerId").stringValue = WoodScrewId;

            Vector3 inward = new Vector3(0f, 0f, -Mathf.Sign(outerFaceZ));
            SerializedProperty points = connection.FindPropertyRelative("points");
            points.arraySize = 2;
            for (int i = 0; i < 2; i++)
            {
                SerializedProperty point = points.GetArrayElementAtIndex(i);
                point.FindPropertyRelative("localPosition").vector3Value =
                    new Vector3(x, centerY + (i == 0 ? 0.025f : -0.025f), outerFaceZ);
                point.FindPropertyRelative("localDirection").vector3Value = inward;
            }
        }
    }
}
