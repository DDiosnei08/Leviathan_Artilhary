using UnityEngine;

namespace ProjectLeviathan.ProceduralTerrain
{
    public class MeshData
    {
        public Vector3[] vertices;
        public Vector3[] normals;
        public Vector2[] uvs;
        public int[] triangles;
        private int triangleIndex;

        public MeshData(int width, int height)
        {
            vertices = new Vector3[width * height];
            normals = new Vector3[width * height];
            uvs = new Vector2[width * height];
            triangles = new int[(width - 1) * (height - 1) * 6];
        }

        public void AddTriangle(int a, int b, int c)
        {
            triangles[triangleIndex] = a;
            triangles[triangleIndex + 1] = b;
            triangles[triangleIndex + 2] = c;
            triangleIndex += 3;
        }

        public void RecalculateNormals()
        {
            for (int i = 0; i < normals.Length; i++)
            {
                normals[i] = Vector3.up;
            }

            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector3 a = vertices[triangles[i]];
                Vector3 b = vertices[triangles[i + 1]];
                Vector3 c = vertices[triangles[i + 2]];
                Vector3 normal = Vector3.Cross(b - a, c - a).normalized;

                normals[triangles[i]] += normal;
                normals[triangles[i + 1]] += normal;
                normals[triangles[i + 2]] += normal;
            }

            for (int i = 0; i < normals.Length; i++)
            {
                normals[i].Normalize();
            }
        }

        public Mesh CreateMesh()
        {
            Mesh mesh = new Mesh();
            mesh.indexFormat = vertices.Length > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.uv = uvs;
            mesh.normals = normals;
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
