using System;
using UnityEngine;

namespace WoodcraftSimulator.Persistence
{
    /// <summary>
    /// Stable identity of a scene object in the save file (D-036). Objects placed by the scene builder get a fixed,
    /// readable id; objects created at runtime (e.g. the halves of a cut) get a new unique id.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PersistentId : MonoBehaviour
    {
        [SerializeField] private string id;

        public string Id => id;

        /// <summary>Gives this object a new unique id (a copy made with Instantiate must not keep the original's).</summary>
        public void AssignNew() => id = Guid.NewGuid().ToString("N");

        public void Set(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Persistent id must not be empty.", nameof(value));
            }

            id = value;
        }

        private void Awake()
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                AssignNew();
            }
        }
    }
}
