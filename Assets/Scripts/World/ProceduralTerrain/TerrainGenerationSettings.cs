using System.Collections.Generic;
using UnityEngine;

namespace ProjectLeviathan.ProceduralTerrain
{
    public sealed class TerrainGenerationSettings
    {
        const int HeightCurveSamples = 256;
        readonly float[] heightCurveSamples;
        readonly Dictionary<int, int> lodSimplificationByLevel;

        public float chunkSize { get; }
        public int chunkResolution { get; }
        public float heightMultiplier { get; }
        public bool generateIsland { get; }
        public float falloffA { get; }
        public float falloffB { get; }
        public NoiseSettings noiseSettings { get; }
        public List<TerrainNoiseLayerSettings> terrainNoiseLayers { get; }
        public float flatnessBias { get; }
        public float mountainBias { get; }
        public float reliefContrast { get; }
        public TerrainFlatZoneSettings flatZones { get; }
        public TerrainErosionSettings erosion { get; }
        public float biomeNoiseScale { get; }
        public float moistureNoiseScale { get; }
        public float temperatureNoiseScale { get; }
        public float temperatureAltitudeInfluence { get; }
        public float biomeNoiseWeight { get; }
        public int heightSmoothingIterations { get; }
        public float heightSmoothingStrength { get; }
        public float lakeThreshold { get; }
        public float riverThreshold { get; }
        public int riverCount { get; }

        public ProjectLeviathan.Base.BaseSiteData BaseSite { get; }

        internal TerrainGenerationSettings(TerrainSettings source, bool includeBaseSite = true)
        {
            BaseSite = includeBaseSite ? source.RuntimeBaseSite : default;
            erosion=source.erosion.Copy();
            chunkSize = source.chunkSize;
            chunkResolution = source.chunkResolution;
            heightMultiplier = source.heightMultiplier;
            generateIsland = source.generateIsland;
            falloffA = source.falloffA;
            falloffB = source.falloffB;
            noiseSettings = new NoiseSettings
            {
                seed = source.noiseSettings.seed,
                scale = source.noiseSettings.scale,
                offset = source.noiseSettings.offset,
                octaves = source.noiseSettings.octaves,
                persistence = source.noiseSettings.persistence,
                lacunarity = source.noiseSettings.lacunarity,
                normalize = source.noiseSettings.normalize
            };
            terrainNoiseLayers = new List<TerrainNoiseLayerSettings>(source.terrainNoiseLayers.Count);
            for (int i = 0; i < source.terrainNoiseLayers.Count; i++)
            {
                TerrainNoiseLayerSettings layer = source.terrainNoiseLayers[i];
                terrainNoiseLayers.Add(new TerrainNoiseLayerSettings
                {
                    name = layer.name,
                    scaleMeters = layer.scaleMeters,
                    strength = layer.strength,
                    octaves = layer.octaves,
                    persistence = layer.persistence,
                    lacunarity = layer.lacunarity
                });
            }

            flatnessBias = source.flatnessBias;
            mountainBias = source.mountainBias;
            reliefContrast = source.reliefContrast;
            flatZones = new TerrainFlatZoneSettings
            {
                enabled = source.flatZones.enabled,
                scaleMeters = source.flatZones.scaleMeters,
                octaves = source.flatZones.octaves,
                threshold = source.flatZones.threshold,
                blendWidth = source.flatZones.blendWidth,
                levelScaleMeters = source.flatZones.levelScaleMeters,
                minimumLevel = source.flatZones.minimumLevel,
                maximumLevel = source.flatZones.maximumLevel
            };
            biomeNoiseScale = source.biomeNoiseScale;
            moistureNoiseScale = source.moistureNoiseScale;
            temperatureNoiseScale = source.temperatureNoiseScale;
            temperatureAltitudeInfluence = source.temperatureAltitudeInfluence;
            biomeNoiseWeight = source.biomeNoiseWeight;
            heightSmoothingIterations = source.heightSmoothingIterations;
            heightSmoothingStrength = source.heightSmoothingStrength;
            lakeThreshold = source.lakeThreshold;
            riverThreshold = source.riverThreshold;
            riverCount = source.riverCount;
            heightCurveSamples = new float[HeightCurveSamples + 1];
            for (int i = 0; i <= HeightCurveSamples; i++)
            {
                heightCurveSamples[i] = source.heightCurve.Evaluate(i / (float)HeightCurveSamples);
            }

            lodSimplificationByLevel = new Dictionary<int, int>();
            for (int i = 0; i < source.lods.Count; i++)
            {
                LODInfo lod = source.lods[i];
                if (lod != null && !lodSimplificationByLevel.ContainsKey(lod.lod))
                {
                    lodSimplificationByLevel[lod.lod] = Mathf.Max(1, lod.meshResolution);
                }
            }
        }

        public float GetCellSize(int resolution)
        {
            return chunkSize / (Mathf.Max(2, resolution) - 1);
        }

        public float EvaluateHeight(float normalizedHeight)
        {
            float position = Mathf.Clamp01(normalizedHeight) * HeightCurveSamples;
            int lower = Mathf.Min(Mathf.FloorToInt(position), HeightCurveSamples - 1);
            return Mathf.Lerp(heightCurveSamples[lower], heightCurveSamples[lower + 1], position - lower);
        }

        public int GetSimplificationLevel(int lodLevel)
        {
            return lodSimplificationByLevel.TryGetValue(lodLevel, out int simplification)
                ? simplification
                : Mathf.Max(1, lodLevel > 0 ? lodLevel * 2 : 1);
        }
    }
}
