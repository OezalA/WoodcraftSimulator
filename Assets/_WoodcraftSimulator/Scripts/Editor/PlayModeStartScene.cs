using UnityEditor;
using UnityEditor.SceneManagement;

namespace WoodcraftSimulator.Editor
{
    /// <summary>
    /// Play always starts from the core scene (D-056), whichever scene is open: the core scene's SceneFlow then loads
    /// the place the save says (the workshop in a new game). An Editor setting for this session, not a project
    /// setting; it is set again after every script reload.
    /// </summary>
    [InitializeOnLoad]
    public static class PlayModeStartScene
    {
        public const string CoreScenePath = "Assets/_WoodcraftSimulator/Scenes/Core/Core.unity";

        static PlayModeStartScene() => EditorApplication.delayCall += Apply;

        public static void Apply()
        {
            var core = AssetDatabase.LoadAssetAtPath<SceneAsset>(CoreScenePath);
            EditorSceneManager.playModeStartScene = core; // null (no core scene built yet) = the open scene as before
        }
    }
}
