using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using WoodcraftSimulator.Handling;

namespace WoodcraftSimulator.Workshop
{
    /// <summary>
    /// Builds the walkable area for the <see cref="Apprentice"/> when the scene starts (D-052), from the colliders of
    /// the fixed scene: floor, ground, walls, benches. Movable objects (boards, the stepladder, anything
    /// <see cref="Carryable"/>), the player and the apprentice are left out, so they never block a path that later
    /// opens. Built at runtime with Unity's built-in AI module (no baked asset to keep in the repository).
    /// </summary>
    public sealed class RuntimeNavMesh : MonoBehaviour
    {
        [Tooltip("Size of the area (centered on this object), in meters.")]
        [SerializeField] private Vector3 size = new Vector3(40f, 10f, 40f);

        [SerializeField, Min(0.1f)] private float agentRadius = 0.3f;
        [SerializeField, Min(0.5f)] private float agentHeight = 1.7f;

        [Tooltip("Highest step the apprentice walks up (the workshop floor edge, not onto a sawhorse or bench).")]
        [SerializeField, Min(0.05f)] private float agentClimb = 0.25f;

        private NavMeshDataInstance instance;

        /// <summary>True once the walkable area exists.</summary>
        public static bool IsReady { get; private set; }

        // Awake: before the apprentice's Start places its agent on it.
        private void Awake()
        {
            var bounds = new Bounds(transform.position, size);
            var sources = new List<NavMeshBuildSource>();
            NavMeshBuilder.CollectSources(bounds, Physics.DefaultRaycastLayers, NavMeshCollectGeometry.PhysicsColliders, 0,
                new List<NavMeshBuildMarkup>(), sources);
            sources.RemoveAll(IsMovable);

            NavMeshBuildSettings settings = NavMesh.GetSettingsByID(0);
            settings.agentRadius = agentRadius;
            settings.agentHeight = agentHeight;
            settings.agentClimb = agentClimb;

            NavMeshData data = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
            if (data == null)
            {
                Debug.LogWarning($"[{nameof(RuntimeNavMesh)}] No walkable area could be built.", this);
                return;
            }

            instance = NavMesh.AddNavMeshData(data);
            IsReady = instance.valid;
        }

        private void OnDestroy()
        {
            if (instance.valid)
            {
                instance.Remove();
            }

            IsReady = false;
        }

        private static bool IsMovable(NavMeshBuildSource source)
        {
            Component component = source.component;
            if (component == null)
            {
                return false;
            }

            return (component is Collider collider && collider.isTrigger) ||
                   component.GetComponentInParent<Carryable>() != null ||
                   component.GetComponentInParent<CharacterController>() != null ||
                   component.GetComponentInParent<Apprentice>() != null;
        }
    }
}
