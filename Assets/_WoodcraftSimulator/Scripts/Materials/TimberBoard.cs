using UnityEngine;
using WoodcraftSimulator.Core;

namespace WoodcraftSimulator.Materials
{
    /// <summary>
    /// Scene representation of a <see cref="TimberPiece"/>.
    /// The domain piece is the source of truth: mesh, collider and rigidbody mass are generated from it,
    /// and the transform scale must stay (1, 1, 1).
    /// Axes: length = local Z, width = local X, thickness = local Y.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(BoxCollider))]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class TimberBoard : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private WoodSpeciesDefinition species;

        [Header("Actual dimensions (mm)")]
        [SerializeField, Min(1f)] private float lengthMillimeters = 2400f;
        [SerializeField, Min(1f)] private float widthMillimeters = 95f;
        [SerializeField, Min(1f)] private float thicknessMillimeters = 45f;

        private Mesh generatedMesh;
        private MaterialPropertyBlock propertyBlock;

        /// <summary>Authoritative timber data. Null until <see cref="Rebuild"/> has run.</summary>
        public TimberPiece Piece { get; private set; }

        public WoodSpeciesDefinition SpeciesDefinition => species;

        private void Awake() => Rebuild();

        private void OnDestroy()
        {
            // Generated meshes are not assets; destroy them explicitly to avoid leaks.
            if (generatedMesh != null)
            {
                Destroy(generatedMesh);
            }
        }

        /// <summary>Recreates the domain piece and everything derived from it (mesh, collider, mass, color).</summary>
        public void Rebuild()
        {
            if (species == null)
            {
                Debug.LogError($"{nameof(TimberBoard)} '{name}': wood species is not assigned.", this);
                return;
            }

            Piece = new TimberPiece(species.ToDomain(),
                BoardDimensions.FromMillimeters(lengthMillimeters, widthMillimeters, thicknessMillimeters));

            BoardDimensions dimensions = Piece.Dimensions;
            var size = new Vector3(
                (float)dimensions.Width.Meters,
                (float)dimensions.Thickness.Meters,
                (float)dimensions.Length.Meters);

            Mesh previousMesh = generatedMesh;
            generatedMesh = BoxMeshBuilder.Create(size, $"Timber {dimensions}");
            GetComponent<MeshFilter>().sharedMesh = generatedMesh;
            DestroyMesh(previousMesh);

            var boxCollider = GetComponent<BoxCollider>();
            boxCollider.center = Vector3.zero;
            boxCollider.size = size;

            GetComponent<Rigidbody>().mass = (float)Piece.MassKg;

            // Per-object color without creating a material instance per board.
            propertyBlock ??= new MaterialPropertyBlock();
            var meshRenderer = GetComponent<MeshRenderer>();
            meshRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(BaseColorId, species.GreyboxColor);
            meshRenderer.SetPropertyBlock(propertyBlock);
        }

        private static void DestroyMesh(Mesh mesh)
        {
            if (mesh == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(mesh);
            }
            else
            {
                DestroyImmediate(mesh);
            }
        }
    }
}
