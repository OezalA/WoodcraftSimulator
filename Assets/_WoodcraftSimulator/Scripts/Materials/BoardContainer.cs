using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WoodcraftSimulator.Materials
{
    /// <summary>
    /// Parent for the boards of a content scene (the workshop's "Timber"), so created boards keep the hierarchy tidy
    /// (D-056: the factory in the core scene can't hold a reference into a content scene).
    /// </summary>
    public sealed class BoardContainer : MonoBehaviour
    {
        private static readonly List<BoardContainer> Loaded = new List<BoardContainer>();

        private void OnEnable() => Loaded.Add(this);

        private void OnDisable() => Loaded.Remove(this);

        /// <summary>The container of the active scene (the place the player is at), or null.</summary>
        public static Transform InActiveScene()
        {
            Scene active = SceneManager.GetActiveScene();
            BoardContainer found = Loaded.Find(each => each != null && each.gameObject.scene == active);
            return found != null ? found.transform : null;
        }
    }
}
