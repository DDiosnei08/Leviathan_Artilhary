using System;
using System.Collections.Generic;
using ProjectLeviathan.ProceduralTerrain;
using UnityEngine;

namespace ProjectLeviathan.Grass
{
    public sealed class GrassTile : IDisposable
    {
        public readonly TerrainChunk terrain;
        internal readonly Dictionary<Vector2Int, GrassPatch> patches = new Dictionary<Vector2Int, GrassPatch>(128);
        internal Texture2D heights, normalDensity;
        internal ComputeBuffer exclusions;
        internal int exclusionCount;
        internal bool dirty = true;
        internal HeightMap uploadedMap;
        internal readonly GrassManager owner;
        readonly float[] height;
        readonly Color[] mask;
        readonly List<Vector4> exclusionData = new List<Vector4>(1024);
        readonly List<Renderer> renderers = new List<Renderer>(512);
        readonly TerrainGenerationSettings generation;
        uint uploadedHash;
        internal int appliedLod = -1;
        internal Vector2 lodBlend, lodFrom;
        internal float lodStarted;


        public int ResidentPatches => patches.Count;
        public long TextureBytes => heights != null ? heights.width * heights.height * 20L : 0;

        internal GrassTile(TerrainChunk terrain, GrassManager owner)
        {
            this.terrain = terrain; this.owner = owner;
            int n = terrain.Settings.chunkResolution;
            generation = terrain.Settings.CaptureGenerationSettings();
            height = new float[n * n]; mask = new Color[n * n];
            heights = new Texture2D(n, n, TextureFormat.RFloat, false, true) { name = "Grass heights", wrapMode = TextureWrapMode.Clamp };
            normalDensity = new Texture2D(n, n, TextureFormat.RGBAFloat, false, true) { name = "Grass normals and mask", wrapMode = TextureWrapMode.Clamp };
            exclusions = new ComputeBuffer(1024, 16);
            terrain.DataChanged += Changed;
            terrain.Disposing += Disposing;
        }
        void Changed(TerrainChunk chunk)
        {
            // Repeated noise generation during a mesh-only LOD change must not reset grass.
            dirty = uploadedMap == null || SurfaceHash(chunk.GeneratedData) != uploadedHash;
        }
        static uint SurfaceHash(HeightMap map)
        {
            if (map == null) return 0;
            uint hash = 2166136261;
            unchecked
            {
                for (int y = 0; y < map.Height; y++) for (int x = 0; x < map.Width; x++)
                {
                    hash = (hash ^ (uint)map.Values[x,y].GetHashCode()) * 16777619;
                    if(map.WorldHeights!=null) hash=(hash^(uint)map.WorldHeights[x,y].GetHashCode())*16777619;
                    hash = (hash ^ (uint)map.Water[x,y].GetHashCode()) * 16777619;
                    hash = (hash ^ (uint)map.Roads[x,y].GetHashCode()) * 16777619;
                    hash = (hash ^ (uint)map.PoiMask[x,y].GetHashCode()) * 16777619;
                    hash = (hash ^ (uint)map.Biomes[x,y].GetHashCode()) * 16777619;
                }
            }
            return hash;
        }
        void Disposing(TerrainChunk chunk) { owner.DetachChunk(chunk); }

        internal void Upload(GrassConfiguration config, GrassExclusionVolume[] volumes)
        {
            HeightMap map = terrain.GeneratedData;
            TerrainSettings settings = terrain.Settings;
            int n = map.Width;
            float cell = settings.chunkSize / (n - 1);
            if (n != heights.width) throw new InvalidOperationException("Regenerate the world after changing its chunk resolution.");
            float World(float v) => generation.EvaluateHeight(Mathf.Clamp01(v)) * settings.heightMultiplier;
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                int index = y * n + x;
                height[index] = map.WorldHeights != null ? map.WorldHeights[x, y] : World(map.Values[x, y]);
                bool padded = map.NormalHeights.GetLength(0) == n + 2;
                float l = World(padded ? map.NormalHeights[x, y + 1] : map.Values[Mathf.Max(x - 1, 0), y]);
                float r = World(padded ? map.NormalHeights[x + 2, y + 1] : map.Values[Mathf.Min(x + 1, n - 1), y]);
                float u = World(padded ? map.NormalHeights[x + 1, y] : map.Values[x, Mathf.Max(y - 1, 0)]);
                float d = World(padded ? map.NormalHeights[x + 1, y + 2] : map.Values[x, Mathf.Min(y + 1, n - 1)]);
                if(map.NormalWorldHeights!=null)
                {
                    l=map.NormalWorldHeights[x,y+1];r=map.NormalWorldHeights[x+2,y+1];
                    u=map.NormalWorldHeights[x+1,y];d=map.NormalWorldHeights[x+1,y+2];
                }
                Vector3 normal = new Vector3((l - r) / (2 * settings.CellSize), 1, (d - u) / (2 * settings.CellSize)).normalized;
                float slope = Vector3.Angle(normal, Vector3.up);
                float eligibility = 1 - Mathf.InverseLerp(config.maximumSlope - Mathf.Max(0.01f, config.slopeFade), config.maximumSlope, slope);
                if (map.Water[x, y] > 0.01f || map.Roads[x, y] > 0.01f || map.PoiMask[x, y] > 0.01f ||
                    map.Values[x, y] < config.normalizedHeightRange.x || map.Values[x, y] > config.normalizedHeightRange.y ||
                    map.Biomes[x, y] < config.biomeRange.x || map.Biomes[x, y] > config.biomeRange.y) eligibility = 0;
                if (map.BaseSite.IsExcluded(map.WorldOrigin.x + x * cell, map.WorldOrigin.y + settings.chunkSize - y * cell,
                    cell*.5f)) eligibility = 0;
                mask[index] = new Color(normal.x, normal.y, normal.z, eligibility);
            }
            heights.SetPixelData(height, 0); heights.Apply(false, false);
            normalDensity.SetPixels(mask); normalDensity.Apply(false, false);
            List<Vector4> bounds = exclusionData;
            bounds.Clear();
            foreach (GrassExclusionVolume volume in volumes)
                if (volume != null && volume.isActiveAndEnabled) AddBounds(bounds, volume.WorldBounds);
            if (config.excludeProps)
            {
                for (int i = 0; i < terrain.GameObject.transform.childCount; i++)
                {
                    Transform child = terrain.GameObject.transform.GetChild(i);
                    if (!child.name.Contains("Prop") && !child.name.Contains("Interest")) continue;
                    renderers.Clear(); child.GetComponentsInChildren(true, renderers);
                    foreach (Renderer renderer in renderers) AddBounds(bounds, renderer.bounds);
                }
            }
            exclusionCount = bounds.Count / 2;
            if (bounds.Count == 0) bounds.Add(Vector4.zero);
            exclusions.SetData(bounds);
            uploadedMap = map; uploadedHash = SurfaceHash(map); dirty = false;
        }
        static void AddBounds(List<Vector4> target, Bounds b)
        {
            if (target.Count >= 1024) return;
            b.Expand(0.5f); target.Add(b.min); target.Add(b.max);
        }
        void ReleaseTextures()
        {
            if (heights != null) UnityEngine.Object.Destroy(heights);
            if (normalDensity != null) UnityEngine.Object.Destroy(normalDensity);
            exclusions?.Release(); exclusions = null;
        }
        public void Dispose()
        {
            terrain.DataChanged -= Changed; terrain.Disposing -= Disposing;
            foreach (GrassPatch patch in patches.Values) owner.Return(patch);
            patches.Clear(); ReleaseTextures();
        }
    }

    internal sealed class GrassPatch
    {
        internal GrassBuffers buffers;
        internal Vector2 origin;
        internal Vector2Int global;
        internal Bounds bounds;
        internal float birth;
        internal float lastStatistics;
        internal bool generated;
    }
}
