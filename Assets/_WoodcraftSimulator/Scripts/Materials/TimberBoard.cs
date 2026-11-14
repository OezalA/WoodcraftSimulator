using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Rendering;
using WoodcraftSimulator.Core;
using WoodcraftSimulator.Interaction;
using WoodcraftSimulator.Measurement;

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
    public sealed class TimberBoard : MonoBehaviour, IDescribable
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private WoodSpeciesDefinition species;

        [Header("Actual dimensions (mm)")]
        [SerializeField, Min(1f)] private float lengthMillimeters = 2400f;
        [SerializeField, Min(1f)] private float widthMillimeters = 95f;
        [SerializeField, Min(1f)] private float thicknessMillimeters = 45f;

        private Mesh generatedMesh;
        private MaterialPropertyBlock propertyBlock;

        [Tooltip("Material of pencil lines drawn on this board.")]
        [SerializeField] private Material markMaterial;

        // Pencil line visual: drawn wider than a real 0.5 mm line so it stays visible from a normal working
        // distance. Only the visual; the authoritative position is the domain PencilMark (0.1 mm).
        private const float MarkLineWidth = 0.0015f;
        private const float MarkLineHeight = 0.0003f;
        private const float MarkSurfaceOffset = 0.0005f;

        private static Mesh unitCube; // shared by all mark visuals, scaled per mark
        private readonly List<GameObject> markVisuals = new List<GameObject>();

        /// <summary>Pencil marks on this board (created together with <see cref="Piece"/>).</summary>
        public BoardMarks Marks { get; private set; }

        /// <summary>Authoritative timber data. Null until <see cref="Rebuild"/> has run.</summary>
        public TimberPiece Piece { get; private set; }

        public WoodSpeciesDefinition SpeciesDefinition => species;

        /// <summary>e.g. "Spruce 2400 × 95 × 45 mm (4.6 kg)". Invariant culture: always "4.6", never "4,6".</summary>
        public string Description => Piece == null
            ? name
            : $"{Piece} ({Piece.MassKg.ToString("0.0", CultureInfo.InvariantCulture)} kg)";

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
            Marks = new BoardMarks(Piece.Dimensions.Length);
            ClearMarkVisuals();

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

        /// <summary>
        /// Adds a pencil line across the board: stored in <see cref="Marks"/> (authoritative) and drawn on the face
        /// given by <paramref name="faceSign"/> (+1 = local +Y face, −1 = −Y face).
        /// </summary>
        public void AddMark(PencilMark mark, float faceSign)
        {
            Marks.Add(mark); // validates that the mark lies on the board

            var visual = new GameObject($"PencilMark {mark.PositionFromStart}");
            visual.transform.SetParent(transform, false);

            BoardDimensions dimensions = Piece.Dimensions;
            float halfLength = (float)(dimensions.Length.Meters / 2.0);
            float y = Mathf.Sign(faceSign) * ((float)(dimensions.Thickness.Meters / 2.0) + MarkSurfaceOffset);
            visual.transform.localPosition = new Vector3(0f, y, (float)mark.PositionFromStart.Meters - halfLength);
            visual.transform.localScale = new Vector3((float)dimensions.Width.Meters, MarkLineHeight, MarkLineWidth);

            unitCube ??= BoxMeshBuilder.Create(Vector3.one, "Unit cube");
            visual.AddComponent<MeshFilter>().sharedMesh = unitCube;
            var meshRenderer = visual.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = markMaterial;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            markVisuals.Add(visual);
        }

        private void ClearMarkVisuals()
        {
            foreach (GameObject visual in markVisuals)
            {
                if (visual != null)
                {
                    Destroy(visual);
                }
            }

            markVisuals.Clear();
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
