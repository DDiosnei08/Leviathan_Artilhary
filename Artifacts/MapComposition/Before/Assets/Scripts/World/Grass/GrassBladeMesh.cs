using UnityEngine;

namespace ProjectLeviathan.Grass
{
    public static class GrassBladeMesh
    {
        public static Mesh Create(int pairedRows)
        {
            int count = pairedRows * 2 + 1;
            Vector3[] vertices = new Vector3[count];
            Vector2[] uv = new Vector2[count];
            int[] indices = new int[(pairedRows - 1) * 6 + 3];
            int index = 0;
            for (int row = 0; row < pairedRows; row++)
            {
                float t = row / (float)pairedRows;
                for (int side = 0; side < 2; side++)
                {
                    vertices[row * 2 + side] = new Vector3(side == 0 ? -1f : 1f, t, 0);
                    uv[row * 2 + side] = new Vector2(side, t);
                }
                if (row == pairedRows - 1) continue;
                int a = row * 2, b = a + 1, c = a + 2, d = a + 3;
                indices[index++] = a; indices[index++] = c; indices[index++] = b;
                indices[index++] = b; indices[index++] = c; indices[index++] = d;
            }
            vertices[count - 1] = Vector3.up;
            uv[count - 1] = new Vector2(0.5f, 1f);
            indices[index++] = count - 3; indices[index++] = count - 1; indices[index] = count - 2;
            Mesh mesh = new Mesh { name = "Grass Blade " + count + " vertices", hideFlags = HideFlags.DontSave };
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = indices;
            mesh.bounds = new Bounds(Vector3.up * 0.5f, Vector3.one * 3f);
            mesh.UploadMeshData(true);
            return mesh;
        }
    }
}
