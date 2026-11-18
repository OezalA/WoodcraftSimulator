using UnityEngine;
using WoodcraftSimulator.Materials;
using WoodcraftSimulator.Player;

namespace WoodcraftSimulator.Audio
{
    /// <summary>
    /// Footsteps (Phase 13d, D-061): one step per stride while walking on the ground (not on the ladder, not while
    /// loading a place), its sound chosen by what is under the feet: boards and wooden floors wood, the workshop floor
    /// concrete, grass, snow (on the mountain terrain where its snow layer dominates), beach sand.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerFootsteps : MonoBehaviour
    {
        [SerializeField] private SoundLibrary library;
        [SerializeField, Range(0f, 1f)] private float volume = 0.35f;

        [Tooltip("Metres between two steps (walking); shorter when crouching.")]
        [SerializeField, Min(0.2f)] private float stride = 0.65f;

        private static readonly RaycastHit[] Hits = new RaycastHit[8];

        private CharacterController controller;
        private PlayerController player;
        private AudioSource source;
        private Vector3 lastPosition;
        private float travelled;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            TryGetComponent(out player);
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            lastPosition = transform.position;
        }

        private void Update()
        {
            Vector3 position = transform.position;
            Vector3 moved = position - lastPosition;
            lastPosition = position;
            moved.y = 0f;

            bool walking = library != null && controller.enabled && controller.isGrounded && player != null && player.enabled &&
                           !player.IsOnLadder && moved.magnitude < 1f; // a teleport is not a step
            if (!walking)
            {
                travelled = 0f;
                return;
            }

            travelled += moved.magnitude;
            float length = player.IsCrouching ? stride * 0.7f : stride;
            if (travelled < length)
            {
                return;
            }

            travelled = 0f;
            FootstepSurface surface = SurfaceUnderFeet();
            AudioClip clip = SoundLibrary.Any(library.Footsteps(surface));
            if (clip != null)
            {
                source.pitch = (surface == FootstepSurface.Sand ? 1.25f : 1f) * Random.Range(0.93f, 1.07f);
                source.PlayOneShot(clip, volume * (player.IsCrouching ? 0.5f : 1f));
            }
        }

        private FootstepSurface SurfaceUnderFeet()
        {
            Vector3 origin = transform.position + Vector3.up * 0.3f;
            int count = Physics.RaycastNonAlloc(origin, Vector3.down, Hits, 1f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            Collider ground = null;
            Vector3 point = transform.position;
            for (int i = 0; i < count; i++)
            {
                if (Hits[i].collider.transform.IsChildOf(transform) || Hits[i].distance >= nearest)
                {
                    continue;
                }

                nearest = Hits[i].distance;
                ground = Hits[i].collider;
                point = Hits[i].point;
            }

            if (ground == null)
            {
                return FootstepSurface.Concrete;
            }

            if (ground.GetComponentInParent<TimberBoard>() != null)
            {
                return FootstepSurface.Wood;
            }

            if (ground is TerrainCollider && ground.TryGetComponent(out Terrain terrain))
            {
                return TerrainSurface(terrain, point);
            }

            var renderer = ground.GetComponent<Renderer>();
            string name = renderer != null && renderer.sharedMaterial != null ? renderer.sharedMaterial.name : ground.name;
            if (Contains(name, "Sand"))
            {
                return FootstepSurface.Sand;
            }

            if (Contains(name, "Snow"))
            {
                return FootstepSurface.Snow;
            }

            if (Contains(name, "Grass") || Contains(name, "Ground") || Contains(name, "Meadow"))
            {
                return FootstepSurface.Grass;
            }

            if (Contains(name, "Wood") || Contains(name, "Workbench") || Contains(name, "Species_") || Contains(name, "Lumber"))
            {
                return FootstepSurface.Wood;
            }

            return FootstepSurface.Concrete;
        }

        /// <summary>On the mountain terrain: snow where the snow layer is strongest, else grass (rock counts as grass).</summary>
        private static FootstepSurface TerrainSurface(Terrain terrain, Vector3 point)
        {
            TerrainData data = terrain.terrainData;
            Vector3 local = point - terrain.transform.position;
            int x = Mathf.Clamp(Mathf.RoundToInt(local.x / data.size.x * (data.alphamapWidth - 1)), 0, data.alphamapWidth - 1);
            int z = Mathf.Clamp(Mathf.RoundToInt(local.z / data.size.z * (data.alphamapHeight - 1)), 0, data.alphamapHeight - 1);
            float[,,] alphas = data.GetAlphamaps(x, z, 1, 1);
            int layers = alphas.GetLength(2);
            return layers >= 3 && alphas[0, 0, 2] > 0.5f ? FootstepSurface.Snow : FootstepSurface.Grass;
        }

        private static bool Contains(string text, string part) =>
            text.IndexOf(part, System.StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
