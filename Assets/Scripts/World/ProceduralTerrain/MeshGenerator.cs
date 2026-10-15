using UnityEngine;

namespace ProjectLeviathan.ProceduralTerrain
{
    public static class MeshGenerator
    {
        public static MeshData GenerateTerrainMesh(
            float[,] heightMap,
            TerrainSettings settings,
            int lodLevel = 0,
            float[,] normalHeightMap = null)
        {
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<TerrainSettings>();
            }

            return GenerateTerrainMesh(
                heightMap,
                settings.CaptureGenerationSettings(),
                lodLevel,
                normalHeightMap);
        }

        public static MeshData GenerateTerrainMesh(
            float[,] heightMap,
            TerrainGenerationSettings settings,
            int lodLevel = 0,
            float[,] normalHeightMap = null,
            float[,] worldHeightMap = null,
            float[,] normalWorldHeightMap = null)
        {
            if (settings == null)
            {
                throw new System.ArgumentNullException(nameof(settings));
            }

            int width = heightMap.GetLength(0);
            int height = heightMap.GetLength(1);
            int simplificationIncrement = settings.GetSimplificationLevel(lodLevel);

            int verticesPerLineX = Mathf.CeilToInt((width - 1) / (float)simplificationIncrement) + 1;
            int verticesPerLineY = Mathf.CeilToInt((height - 1) / (float)simplificationIncrement) + 1;

            MeshData meshData = new MeshData(verticesPerLineX, verticesPerLineY);
            int vertexIndex = 0;
            float vertexSpacingX = settings.chunkSize / (float)(width - 1);
            float vertexSpacingZ = settings.chunkSize / (float)(height - 1);
            bool hasNormalHeightMap = normalHeightMap != null &&
                normalHeightMap.GetLength(0) == width + 2 &&
                normalHeightMap.GetLength(1) == height + 2;

            for (int y = 0; y < verticesPerLineY; y++)
            {
                for (int x = 0; x < verticesPerLineX; x++)
                {
                    int mapX = x == verticesPerLineX - 1 ? width - 1 : x * simplificationIncrement;
                    int mapY = y == verticesPerLineY - 1 ? height - 1 : y * simplificationIncrement;
                    float sampleHeight = Mathf.Clamp01(heightMap[mapX, mapY]);
                    float worldY = worldHeightMap != null ? worldHeightMap[mapX,mapY] : settings.EvaluateHeight(sampleHeight) * settings.heightMultiplier;
                    if (float.IsNaN(worldY) || float.IsInfinity(worldY))
                    {
                        throw new System.InvalidOperationException(
                            $"Terrain mesh generation produced a non-finite vertex height at ({mapX}, {mapY}). Check the terrain height curve.");
                    }

                    meshData.vertices[vertexIndex] = new Vector3(
                        mapX * vertexSpacingX,
                        worldY,
                        settings.chunkSize - mapY * vertexSpacingZ);
                    meshData.uvs[vertexIndex] = new Vector2(
                        mapX / (float)(width - 1),
                        mapY / (float)(height - 1));
                    if (hasNormalHeightMap)
                    {
                        float leftHeight = GetWorldHeight(normalHeightMap[mapX, mapY + 1], settings);
                        float rightHeight = GetWorldHeight(normalHeightMap[mapX + 2, mapY + 1], settings);
                        float upperHeight = GetWorldHeight(normalHeightMap[mapX + 1, mapY], settings);
                        float lowerHeight = GetWorldHeight(normalHeightMap[mapX + 1, mapY + 2], settings);
                        if (normalWorldHeightMap != null)
                        {
                            leftHeight = normalWorldHeightMap[mapX,mapY+1];
                            rightHeight = normalWorldHeightMap[mapX+2,mapY+1];
                            upperHeight = normalWorldHeightMap[mapX+1,mapY];
                            lowerHeight = normalWorldHeightMap[mapX+1,mapY+2];
                        }
                        meshData.normals[vertexIndex] = new Vector3(
                            (leftHeight - rightHeight) / (2f * vertexSpacingX),
                            1f,
                            (lowerHeight - upperHeight) / (2f * vertexSpacingZ)).normalized;
                    }

                    if (x < verticesPerLineX - 1 && y < verticesPerLineY - 1)
                    {
                        int a = vertexIndex;
                        int b = vertexIndex + 1;
                        int c = vertexIndex + verticesPerLineX;
                        int d = vertexIndex + verticesPerLineX + 1;

                        meshData.AddTriangle(a, b, c);
                        meshData.AddTriangle(b, d, c);
                    }

                    vertexIndex++;
                }
            }

            if (!hasNormalHeightMap)
            {
                meshData.RecalculateNormals();
            }

            return meshData;
        }

        static float GetWorldHeight(float normalizedHeight, TerrainGenerationSettings settings)
        {
            return settings.EvaluateHeight(Mathf.Clamp01(normalizedHeight)) * settings.heightMultiplier;
        }
    }
}
