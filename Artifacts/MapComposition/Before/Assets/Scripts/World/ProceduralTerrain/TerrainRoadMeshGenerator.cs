using System.Collections.Generic;
using UnityEngine;

namespace ProjectLeviathan.ProceduralTerrain
{
    public static class TerrainRoadMeshGenerator
    {
        public static Mesh Generate(HeightMap map, TerrainSettings settings)
        {
            if (map == null)
            {
                throw new System.ArgumentNullException(nameof(map));
            }

            if (settings == null)
            {
                throw new System.ArgumentNullException(nameof(settings));
            }

            int width = map.Width;
            int height = map.Height;
            float cellSizeX = settings.chunkSize / Mathf.Max(1, width - 1);
            float cellSizeZ = settings.chunkSize / Mathf.Max(1, height - 1);
            float roadWidthX = settings.roadWidthMeters;
            float roadWidthZ = settings.roadWidthMeters;
            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();
            List<Vector2> uvs = new List<Vector2>();

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (map.Roads[x, y] <= 0.5f || map.Water[x, y] > 0.5f || map.Navigation[x, y] <= 0f)
                    {
                        continue;
                    }

                    float centerX = x * cellSizeX;
                    float centerZ = settings.chunkSize - y * cellSizeZ;
                    float minX = Mathf.Max(0f, centerX - roadWidthX * 0.5f);
                    float maxX = Mathf.Min(settings.chunkSize, centerX + roadWidthX * 0.5f);
                    float minZ = Mathf.Max(0f, centerZ - roadWidthZ * 0.5f);
                    float maxZ = Mathf.Min(settings.chunkSize, centerZ + roadWidthZ * 0.5f);
                    if (maxX - minX < 0.01f || maxZ - minZ < 0.01f)
                    {
                        continue;
                    }

                    int firstVertex = vertices.Count;
                    vertices.Add(new Vector3(minX, GetHeight(map, settings, minX, maxZ), maxZ));
                    vertices.Add(new Vector3(maxX, GetHeight(map, settings, maxX, maxZ), maxZ));
                    vertices.Add(new Vector3(minX, GetHeight(map, settings, minX, minZ), minZ));
                    vertices.Add(new Vector3(maxX, GetHeight(map, settings, maxX, minZ), minZ));

                    float vMin = minZ / settings.chunkSize;
                    float vMax = maxZ / settings.chunkSize;
                    uvs.Add(new Vector2(minX / settings.chunkSize, vMax));
                    uvs.Add(new Vector2(maxX / settings.chunkSize, vMax));
                    uvs.Add(new Vector2(minX / settings.chunkSize, vMin));
                    uvs.Add(new Vector2(maxX / settings.chunkSize, vMin));

                    triangles.Add(firstVertex);
                    triangles.Add(firstVertex + 2);
                    triangles.Add(firstVertex + 1);
                    triangles.Add(firstVertex + 1);
                    triangles.Add(firstVertex + 2);
                    triangles.Add(firstVertex + 3);
                }
            }

            if (vertices.Count == 0)
            {
                return null;
            }

            Mesh mesh = new Mesh { name = "Generated Terrain Roads" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.SetUVs(0, uvs);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        static float GetHeight(HeightMap map, TerrainSettings settings, float worldX, float worldZ)
        {
            float cellSizeX = settings.chunkSize / Mathf.Max(1, map.Width - 1);
            float cellSizeZ = settings.chunkSize / Mathf.Max(1, map.Height - 1);
            float sampleX = Mathf.Clamp(worldX / cellSizeX, 0f, map.Width - 1);
            float sampleY = Mathf.Clamp((settings.chunkSize - worldZ) / cellSizeZ, 0f, map.Height - 1);
            int x0 = Mathf.FloorToInt(sampleX);
            int y0 = Mathf.FloorToInt(sampleY);
            int x1 = Mathf.Min(x0 + 1, map.Width - 1);
            int y1 = Mathf.Min(y0 + 1, map.Height - 1);
            float tx = sampleX - x0;
            float ty = sampleY - y0;

            float topLeft = GetSampleHeight(map, settings, x0, y0);
            float topRight = GetSampleHeight(map, settings, x1, y0);
            float bottomLeft = GetSampleHeight(map, settings, x0, y1);
            float bottomRight = GetSampleHeight(map, settings, x1, y1);
            float top = Mathf.Lerp(topLeft, topRight, tx);
            float bottom = Mathf.Lerp(bottomLeft, bottomRight, tx);
            return Mathf.Lerp(top, bottom, ty) + Mathf.Max(0.35f, settings.roadSurfaceLift);
        }

        static float GetSampleHeight(HeightMap map, TerrainSettings settings, int x, int y)
        {
            return map.WorldHeights != null
                ? map.WorldHeights[x, y]
                : settings.heightCurve.Evaluate(Mathf.Clamp01(map.Values[x, y])) * settings.heightMultiplier;
        }
    }
}
