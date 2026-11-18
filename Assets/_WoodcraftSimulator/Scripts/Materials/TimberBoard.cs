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
        private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");

        // Pencil line visual: drawn wider than a real 0.5 mm line so it stays visible from a normal working
        // distance. Only the visual; the authoritative position is the domain PencilMark (0.1 mm).
        private const float MarkLineWidth = 0.0015f;
        private const float MarkLineHeight = 0.0003f;
        private const float MarkSurfaceOffset = 0.0005f;
        private const string MarksContainerName = "PencilMarks";

        [SerializeField] private WoodSpeciesDefinition species;

        [Header("Actual dimensions (mm)")]
        [SerializeField, Min(1f)] private float lengthMillimeters = 2400f;
        [SerializeField, Min(1f)] private float widthMillimeters = 95f;
        [SerializeField, Min(1f)] private float thicknessMillimeters = 45f;

        [Tooltip("Material of pencil lines drawn on this board.")]
        [SerializeField] private Material markMaterial;

        [Tooltip("Sanded / oiled (D-045). Serialized so that the pieces of a cut keep it.")]
        [SerializeField] private BoardSurface surface = BoardSurface.Raw;

        private static Mesh unitCube; // shared by all mark visuals, scaled per mark

        private Mesh generatedMesh;
        private MaterialPropertyBlock propertyBlock;
        private Transform marksContainer;

        /// <summary>Authoritative timber data. Null until <see cref="Rebuild"/> has run.</summary>
        public TimberPiece Piece { get; private set; }

        /// <summary>Pencil marks on this board (created together with <see cref="Piece"/>).</summary>
        public BoardMarks Marks { get; private set; }

        public WoodSpeciesDefinition SpeciesDefinition => species;

        public BoardSurface Surface => surface;

        /// <summary>Raised after any board was sanded or oiled (e.g. the job result counts finished parts).</summary>
        public static event System.Action AnySurfaceChanged;

        /// <summary>Sets the surface treatment and shows it (sanded: lighter, oiled: darker and warmer).</summary>
        public void SetSurface(BoardSurface value)
        {
            surface = value;
            ApplyColor();
            AnySurfaceChanged?.Invoke();
        }

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

        /// <summary>Recreates the domain piece from the serialized fields, and everything derived from it.</summary>
        public void Rebuild()
        {
            if (species == null)
            {
                Debug.LogError($"{nameof(TimberBoard)} '{name}': wood species is not assigned.", this);
                return;
            }

            var piece = new TimberPiece(species.ToDomain(),
                BoardDimensions.FromMillimeters(lengthMillimeters, widthMillimeters, thicknessMillimeters));
            Apply(piece, new BoardMarks(piece.Dimensions.Length));
        }

        /// <summary>
        /// Sets the static references of a board created at runtime (before its GameObject is activated, so that
        /// <c>Awake</c> already finds the species).
        /// </summary>
        public void Initialize(WoodSpeciesDefinition speciesDefinition, Material pencilMarkMaterial)
        {
            species = speciesDefinition;
            markMaterial = pencilMarkMaterial;
        }

        /// <summary>
        /// Makes this board represent another piece (e.g. one half of a cut) with its marks. The species asset stays;
        /// the serialized dimensions are updated so the Inspector shows the truth.
        /// </summary>
        public void SetPiece(TimberPiece piece, BoardMarks marks)
        {
            BoardDimensions dimensions = piece.Dimensions;
            lengthMillimeters = (float)dimensions.Length.Millimeters;
            widthMillimeters = (float)dimensions.Width.Millimeters;
            thicknessMillimeters = (float)dimensions.Thickness.Millimeters;
            Apply(piece, marks);
        }

        private void ApplyColor()
        {
            if (species == null)
            {
                return;
            }

            // Greybox: the species colour; textured (D-059): the texture carries the colour, the surface tints it. Sanded is
            // a little lighter and smoother, oiled warmer, darker and with a sheen.
            bool textured = species.BoardMaterial != null;
            // A textured material keeps its own tint (white for wood, the see-through blue of glass panels).
            Color color = textured ? species.BoardMaterial.GetColor(BaseColorId) : species.GreyboxColor;
            float smoothness = textured ? species.BoardMaterial.GetFloat(SmoothnessId) : 0.12f;
            if (surface == BoardSurface.Sanded)
            {
                color = textured ? color * new Color(1.06f, 1.05f, 1.03f, 1f) : Color.Lerp(color, Color.white, 0.15f);
                smoothness = 0.25f;
            }
            else if (surface == BoardSurface.Oiled)
            {
                color = textured ? color * new Color(0.86f, 0.74f, 0.6f, 1f) : new Color(color.r * 0.8f, color.g * 0.66f, color.b * 0.5f, color.a);
                smoothness = 0.55f;
            }

            // Per-object color without creating a material instance per board.
            propertyBlock ??= new MaterialPropertyBlock();
            var meshRenderer = GetComponent<MeshRenderer>();
            meshRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(BaseColorId, color);
            if (textured)
            {
                propertyBlock.SetFloat(SmoothnessId, smoothness);
            }
            meshRenderer.SetPropertyBlock(propertyBlock);
        }

        /// <summary>Adds a pencil line across the board: stored in <see cref="Marks"/> and drawn on its face.</summary>
        public void AddMark(PencilMark mark)
        {
            Marks.Add(mark); // validates that the mark lies on the board
            CreateMarkVisual(mark);
        }

        private void Apply(TimberPiece piece, BoardMarks marks)
        {
            Piece = piece;
            Marks = marks;

            BoardDimensions dimensions = Piece.Dimensions;
            var size = new Vector3(
                (float)dimensions.Width.Meters,
                (float)dimensions.Thickness.Meters,
                (float)dimensions.Length.Meters);

            Mesh previousMesh = generatedMesh;
            // With a wood texture the grain runs along the board on every long face (Phase 13b, D-059).
            bool textured = species != null && species.BoardMaterial != null;
            generatedMesh = BoxMeshBuilder.Create(size, $"Timber {dimensions}", textured ? !species.GrainAlongTextureV : (bool?)null);
            if (textured)
            {
                GetComponent<MeshRenderer>().sharedMaterial = species.BoardMaterial;
            }

            GetComponent<MeshFilter>().sharedMesh = generatedMesh;
            DestroyMesh(previousMesh);

            var boxCollider = GetComponent<BoxCollider>();
            boxCollider.center = Vector3.zero;
            boxCollider.size = size;

            GetComponent<Rigidbody>().mass = (float)Piece.MassKg;

            ApplyColor();

            RebuildMarkVisuals();
        }

        private void RebuildMarkVisuals()
        {
            // All mark visuals live under one child, so a copied board (Instantiate) can drop the copied ones.
            Transform existing = transform.Find(MarksContainerName);
            if (existing != null)
            {
                existing.SetParent(null);
                DestroyEverywhere(existing.gameObject);
            }

            marksContainer = null;
            foreach (PencilMark mark in Marks.All)
            {
                CreateMarkVisual(mark);
            }
        }

        private void CreateMarkVisual(PencilMark mark)
        {
            if (marksContainer == null)
            {
                marksContainer = new GameObject(MarksContainerName).transform;
                marksContainer.SetParent(transform, false);
            }

            var visual = new GameObject($"PencilMark {mark.PositionFromStart}");
            visual.transform.SetParent(marksContainer, false);

            BoardDimensions dimensions = Piece.Dimensions;
            float halfLength = (float)(dimensions.Length.Meters / 2.0);
            float faceSign = mark.Face == BoardFace.Top ? 1f : -1f;
            float y = faceSign * ((float)(dimensions.Thickness.Meters / 2.0) + MarkSurfaceOffset);
            visual.transform.localPosition = new Vector3(0f, y, (float)mark.PositionFromStart.Meters - halfLength);
            visual.transform.localScale = new Vector3((float)dimensions.Width.Meters, MarkLineHeight, MarkLineWidth);

            unitCube ??= BoxMeshBuilder.Create(Vector3.one, "Unit cube");
            visual.AddComponent<MeshFilter>().sharedMesh = unitCube;
            var meshRenderer = visual.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = markMaterial;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        private static void DestroyEverywhere(Object target)
        {
            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }

        private static void DestroyMesh(Mesh mesh)
        {
            if (mesh != null)
            {
                DestroyEverywhere(mesh);
            }
        }
    }
}
