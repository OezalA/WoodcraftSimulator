using System.Collections.Generic;
using UnityEngine;
using WoodcraftSimulator.Handling;
using WoodcraftSimulator.Measurement;
using WoodcraftSimulator.Persistence;

namespace WoodcraftSimulator.Materials
{
    /// <summary>
    /// Creates timber boards at runtime with the same setup the scene builder gives them (kinematic body, carryable,
    /// materials, persistent id), e.g. when a save is loaded. Also resolves species definitions by id.
    /// </summary>
    public sealed class TimberBoardFactory : MonoBehaviour
    {
        [Tooltip("All species a saved board may refer to (by id).")]
        [SerializeField] private WoodSpeciesDefinition[] species = System.Array.Empty<WoodSpeciesDefinition>();

        [SerializeField] private Material lumberMaterial;
        [SerializeField] private Material markMaterial;

        [Tooltip("Parent of created boards (keeps the hierarchy tidy).")]
        [SerializeField] private Transform container;

        private Dictionary<string, WoodSpeciesDefinition> speciesById;

        public bool IsKnownSpecies(string id) => Lookup().ContainsKey(id ?? string.Empty);

        public bool TryGetSpecies(string id, out WoodSpeciesDefinition definition) =>
            Lookup().TryGetValue(id ?? string.Empty, out definition);

        /// <summary>Creates a resting board for a piece with its marks.</summary>
        public TimberBoard Create(string persistentId, WoodSpeciesDefinition speciesDefinition, TimberPiece piece,
            BoardMarks marks, Vector3 position, Quaternion rotation)
        {
            var board = new GameObject($"{piece.Species.DisplayName}_{piece.Dimensions.Length.FormatMillimeters()}");
            // Inactive while configuring: Awake (TimberBoard.Rebuild) runs on activation, with the species set.
            board.SetActive(false);
            board.transform.SetParent(container, false);
            board.transform.SetPositionAndRotation(position, rotation);

            board.AddComponent<MeshFilter>();
            board.AddComponent<MeshRenderer>().sharedMaterial = lumberMaterial;
            board.AddComponent<BoxCollider>();

            var body = board.AddComponent<Rigidbody>();
            // Resting objects are kinematic (D-019), like the builder's boards.
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            board.AddComponent<Carryable>();
            var timberBoard = board.AddComponent<TimberBoard>();
            timberBoard.Initialize(speciesDefinition, markMaterial);
            board.AddComponent<PersistentId>().Set(persistentId);

            board.SetActive(true);
            timberBoard.SetPiece(piece, marks);
            return timberBoard;
        }

        private Dictionary<string, WoodSpeciesDefinition> Lookup()
        {
            if (speciesById == null)
            {
                speciesById = new Dictionary<string, WoodSpeciesDefinition>();
                foreach (WoodSpeciesDefinition definition in species)
                {
                    if (definition != null)
                    {
                        speciesById[definition.Id] = definition;
                    }
                }
            }

            return speciesById;
        }
    }
}
