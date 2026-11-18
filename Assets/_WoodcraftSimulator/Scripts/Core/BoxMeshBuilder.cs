using UnityEngine;

namespace WoodcraftSimulator.Core
{
    /// <summary>
    /// Builds an axis-aligned box mesh of an exact size, centered on the origin.
    /// 4 vertices per face so each face has flat normals and its own UVs.
    /// UVs are in meters along the face, so textures tile at a physical scale (1 UV unit = 1 m).
    /// For a board (Phase 13b) <c>lengthOnU</c> chooses where its length (local Z) lies in the texture on all four long
    /// faces, so the wood grain runs along the board on every side: true = along U (the texture's width, most wood
    /// textures), false = along V (a texture with the grain upright).
    /// </summary>
    public static class BoxMeshBuilder
    {
        private static readonly Vector3[] FaceNormals =
        {
            Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back
        };

        public static Mesh Create(Vector3 size, string name) => Create(size, name, null);

        /// <param name="lengthOnU">null = the plain mapping; true / false = a board's length along U / V.</param>
        public static Mesh Create(Vector3 size, string name, bool? lengthOnU) => Create(size, name, lengthOnU, size);

        /// <summary>
        /// A box of <paramref name="size"/> whose UVs are those of a box of <paramref name="uvSize"/>: a unit cube that is
        /// scaled to its real size in the scene gets metre-based UVs (the scene builder, Phase 13b).
        /// </summary>
        public static Mesh Create(Vector3 size, string name, bool? lengthOnU, Vector3 uvSize)
        {
            Vector3 uvHalf = uvSize * 0.5f;
            var vertices = new Vector3[24];
            var normals = new Vector3[24];
            var uvs = new Vector2[24];
            var triangles = new int[36];
            Vector3 half = size * 0.5f;

            for (int face = 0; face < FaceNormals.Length; face++)
            {
                Vector3 normal = FaceNormals[face];
                // Any axis perpendicular to the normal; v is chosen so the face winds clockwise when
                // seen from outside (Unity's front-face convention).
                Vector3 u = Mathf.Abs(normal.z) > 0.5f ? Vector3.right : Vector3.forward;
                if (Mathf.Abs(normal.y) > 0.5f)
                {
                    u = Vector3.right;
                }

                if (lengthOnU.HasValue && Mathf.Abs(normal.z) < 0.5f)
                {
                    // Top, bottom and the two sides: the length (Z) on U, or on V (then U runs across).
                    u = lengthOnU.Value ? Vector3.forward : (Mathf.Abs(normal.y) > 0.5f ? Vector3.right : Vector3.up);
                }

                Vector3 v = Vector3.Cross(u, normal);

                float halfU = Mathf.Abs(Vector3.Dot(u, half));
                float halfV = Mathf.Abs(Vector3.Dot(v, half));
                Vector3 center = Vector3.Scale(normal, half);
                float uvU = Mathf.Abs(Vector3.Dot(u, uvHalf)) * 2f;
                float uvV = Mathf.Abs(Vector3.Dot(v, uvHalf)) * 2f;

                int first = face * 4;
                SetVertex(first + 0, center - u * halfU - v * halfV, 0f, 0f);
                SetVertex(first + 1, center - u * halfU + v * halfV, 0f, uvV);
                SetVertex(first + 2, center + u * halfU + v * halfV, uvU, uvV);
                SetVertex(first + 3, center + u * halfU - v * halfV, uvU, 0f);

                int t = face * 6;
                triangles[t + 0] = first + 0;
                triangles[t + 1] = first + 1;
                triangles[t + 2] = first + 2;
                triangles[t + 3] = first + 0;
                triangles[t + 4] = first + 2;
                triangles[t + 5] = first + 3;

                void SetVertex(int index, Vector3 position, float uvU, float uvV)
                {
                    vertices[index] = position;
                    normals[index] = normal;
                    uvs[index] = new Vector2(uvU, uvV);
                }
            }

            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
