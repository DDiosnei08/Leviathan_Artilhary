using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectLeviathan.ProceduralTerrain
{
    public static class TerrainWaterMeshGenerator
    {
        struct Sample
        {
            public int id;
            public float wetness;
            public Vector3 position;
            public Vector3 waterData;
        }

        sealed class SurfaceBuilder
        {
            readonly List<Vector3> vertices = new List<Vector3>();
            readonly List<Vector3> waterData = new List<Vector3>();
            readonly List<int> triangles = new List<int>();
            readonly Dictionary<ulong, int> vertexLookup = new Dictionary<ulong, int>();
            readonly int[] polygon = new int[4];
            int polygonCount;

            public void AddLayer(HeightMap map, TerrainSettings settings, bool river)
            {
                vertexLookup.Clear();
                TerrainGenerationSettings captured = settings.CaptureGenerationSettings();
                float lakeLevel = captured.EvaluateHeight(settings.lakeThreshold) * settings.heightMultiplier;
                float cellX = settings.chunkSize / (map.Width - 1);
                float cellZ = settings.chunkSize / (map.Height - 1);
                Sample[] samples = new Sample[map.Width * map.Height];
                for (int y = 0; y < map.Height; y++)
                for (int x = 0; x < map.Width; x++)
                {
                    int id = y * map.Width + x;
                    float bed = map.WorldHeights != null ? map.WorldHeights[x, y] :
                        captured.EvaluateHeight(map.Values[x, y]) * settings.heightMultiplier;
                    bool wet = map.Water[x, y] > 0.5f;
                    bool riverWet = wet && map.Values[x, y] > settings.lakeThreshold;
                    float level = river ? bed + (riverWet ? settings.riverWaterDepth : 0f) : lakeLevel;
                    float depth = Mathf.Max(0, level - bed);
                    float lakeSign = Mathf.Min(settings.lakeThreshold - map.Values[x, y], (lakeLevel - bed) / Mathf.Max(1, settings.heightMultiplier));
                    samples[id] = new Sample
                    {
                        id = id,
                        wetness = river ? (riverWet ? 0.5f : -0.5f) :
                            (wet && !riverWet ? lakeSign : -Mathf.Max(0.000001f, Mathf.Abs(lakeSign))),
                        position = new Vector3(x * cellX, level + settings.waterSurfaceOffset, settings.chunkSize - y * cellZ),
                        // UV1: bed depth in meters, river edge mask, lake/river selector.
                        waterData = new Vector3(depth, river ? (riverWet ? 0 : 2) : 0, river ? 1 : 0)
                    };
                }
                for (int y = 0; y < map.Height - 1; y++)
                for (int x = 0; x < map.Width - 1; x++)
                {
                    int a = y * map.Width + x, b = a + 1, c = a + map.Width, d = c + 1;
                    ClipTriangle(samples[a], samples[b], samples[c]);
                    ClipTriangle(samples[b], samples[d], samples[c]);
                }
            }

            void ClipTriangle(Sample a, Sample b, Sample c)
            {
                if (a.wetness < 0 && b.wetness < 0 && c.wetness < 0) return;
                polygonCount = 0;
                AddEdge(a, b);
                AddEdge(b, c);
                AddEdge(c, a);
                for (int i = 1; i < polygonCount - 1; i++)
                {
                    Vector3 ab = vertices[polygon[i]] - vertices[polygon[0]];
                    Vector3 ac = vertices[polygon[i + 1]] - vertices[polygon[0]];
                    if (Vector3.Cross(ab, ac).sqrMagnitude < 0.000001f) continue;
                    triangles.Add(polygon[0]);
                    triangles.Add(polygon[i]);
                    triangles.Add(polygon[i + 1]);
                }
            }

            void AddEdge(Sample a, Sample b)
            {
                if (a.wetness >= 0) polygon[polygonCount++] = GetVertex(a, a, 0);
                if ((a.wetness >= 0) == (b.wetness >= 0)) return;
                float t = a.wetness / (a.wetness - b.wetness);
                polygon[polygonCount++] = GetVertex(a, b, t);
            }

            int GetVertex(Sample a, Sample b, float t)
            {
                ulong key = ((ulong)(uint)Mathf.Min(a.id, b.id) << 32) | (uint)Mathf.Max(a.id, b.id);
                if (vertexLookup.TryGetValue(key, out int existing)) return existing;
                int index = vertices.Count;
                vertices.Add(Vector3.LerpUnclamped(a.position, b.position, t));
                waterData.Add(Vector3.LerpUnclamped(a.waterData, b.waterData, t));
                vertexLookup.Add(key, index);
                return index;
            }

            public Mesh Build(float waveHeight)
            {
                if (triangles.Count == 0) return null;
                Mesh mesh = new Mesh { name = "Generated Cartoon Water" };
                if (vertices.Count > 65535) mesh.indexFormat = IndexFormat.UInt32;
                mesh.SetVertices(vertices);
                mesh.SetUVs(1, waterData);
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                Bounds bounds = mesh.bounds;
                bounds.Expand(new Vector3(0, Mathf.Max(0.01f, waveHeight * 3), 0));
                mesh.bounds = bounds;
                return mesh;
            }
        }

        public static Mesh Generate(HeightMap map, TerrainSettings settings)
        {
            if (map == null || settings == null || !settings.generateWaterSurfaces || map.Width < 2 || map.Height < 2) return null;
            SurfaceBuilder builder = new SurfaceBuilder();
            builder.AddLayer(map, settings, false);
            builder.AddLayer(map, settings, true);
            float waveHeight = settings.waterMaterial != null && settings.waterMaterial.HasProperty("_WaveHeight")
                ? settings.waterMaterial.GetFloat("_WaveHeight") : 0.3f;
            return builder.Build(Mathf.Max(0.3f, waveHeight));
        }
    }
}
