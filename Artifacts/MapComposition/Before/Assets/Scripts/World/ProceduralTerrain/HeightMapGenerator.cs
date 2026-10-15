using System.Collections.Generic;
using UnityEngine;

namespace ProjectLeviathan.ProceduralTerrain
{
    public static class HeightMapGenerator
    {
        public static HeightMap GenerateHeightMap(
            TerrainSettings settings,
            int width,
            int height,
            Vector2 chunkWorldOrigin)
        {
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<TerrainSettings>();
            }

            return GenerateHeightMap(settings.CaptureGenerationSettings(), width, height, chunkWorldOrigin);
        }

        public static HeightMap GenerateHeightMap(
            TerrainGenerationSettings settings,
            int width,
            int height,
            Vector2 chunkWorldOrigin)
        {
            if (settings == null)
            {
                throw new System.ArgumentNullException(nameof(settings));
            }

            if (width <= 0)
            {
                throw new System.ArgumentOutOfRangeException(
                    nameof(width),
                    "Height map width must be greater than zero.");
            }

            if (height <= 0)
            {
                throw new System.ArgumentOutOfRangeException(
                    nameof(height),
                    "Height map height must be greater than zero.");
            }

            int erosionPadding=settings.erosion.enabled ? settings.erosion.thermalIterations+1 : 0;
            int padding = settings.heightSmoothingIterations + erosionPadding + 1;
            int paddedWidth = width + padding * 2;
            int paddedHeight = height + padding * 2;
            float cellSize = settings.GetCellSize(width);
            Vector2 paddedWorldOrigin = new Vector2(
                chunkWorldOrigin.x - padding * cellSize,
                chunkWorldOrigin.y + settings.chunkSize + padding * cellSize);
            Vector2 sampleStep = new Vector2(cellSize, -cellSize);
            float[,] noiseMap = Noise.GenerateNoiseMap(
                paddedWidth,
                paddedHeight,
                settings.noiseSettings.scale,
                settings.noiseSettings.octaves,
                settings.noiseSettings.persistence,
                settings.noiseSettings.lacunarity,
                settings.noiseSettings.seed,
                paddedWorldOrigin + settings.noiseSettings.offset,
                sampleStep,
                settings.noiseSettings.normalize);
            List<float[,]> detailNoiseMaps = new List<float[,]>();
            for (int i = 0; i < settings.terrainNoiseLayers.Count; i++)
            {
                TerrainNoiseLayerSettings layer = settings.terrainNoiseLayers[i];
                detailNoiseMaps.Add(Noise.GenerateNoiseMap(
                    paddedWidth,
                    paddedHeight,
                    layer.scaleMeters,
                    layer.octaves,
                    layer.persistence,
                    layer.lacunarity,
                    unchecked(settings.noiseSettings.seed + 104729 * (i + 1)),
                    paddedWorldOrigin + settings.noiseSettings.offset,
                    sampleStep,
                    true));
            }

            // Zonas planas: duas máscaras de ruído de baixa frequência, amostradas em coordenadas de mundo
            // (mesma origem das demais), portanto contínuas entre chunks.
            TerrainFlatZoneSettings flatZones = settings.flatZones;
            bool useFlatZones = flatZones != null && flatZones.enabled;
            float[,] flatMaskNoise = null;
            float[,] flatLevelNoise = null;
            if (useFlatZones)
            {
                flatMaskNoise = Noise.GenerateNoiseMap(
                    paddedWidth,
                    paddedHeight,
                    flatZones.scaleMeters,
                    flatZones.octaves,
                    0.5f,
                    2f,
                    unchecked(settings.noiseSettings.seed + 21001),
                    paddedWorldOrigin + settings.noiseSettings.offset,
                    sampleStep,
                    true);
                flatLevelNoise = Noise.GenerateNoiseMap(
                    paddedWidth,
                    paddedHeight,
                    flatZones.levelScaleMeters,
                    2,
                    0.5f,
                    2f,
                    unchecked(settings.noiseSettings.seed + 21011),
                    paddedWorldOrigin + settings.noiseSettings.offset,
                    sampleStep,
                    true);
            }

            float[,] paddedHeights = new float[paddedWidth, paddedHeight];
            float[,] protectedPlains = new float[paddedWidth,paddedHeight];
            float maximumNoiseAmplitude = settings.noiseSettings.GetMaximumAmplitude();
            bool useFalloff = settings.generateIsland;
            float[,] paddedFalloff = null;
            if (useFalloff)
            {
                int smoothingPadding = padding - 1;
                float[,] smoothingFalloff = FalloffGenerator.GenerateFalloffMap(
                    width + smoothingPadding * 2,
                    height + smoothingPadding * 2,
                    settings.falloffA,
                    settings.falloffB);
                paddedFalloff = AddClampedBorder(smoothingFalloff);
            }
            float[,] biomeNoise = Noise.GenerateNoiseMap(
                paddedWidth,
                paddedHeight,
                settings.biomeNoiseScale,
                2,
                0.5f,
                2f,
                settings.noiseSettings.seed + 7919,
                paddedWorldOrigin + settings.noiseSettings.offset,
                sampleStep,
                false);
            float[,] moistureNoise = Noise.GenerateNoiseMap(
                paddedWidth,
                paddedHeight,
                settings.moistureNoiseScale,
                3,
                0.5f,
                2f,
                unchecked(settings.noiseSettings.seed + 15485863),
                paddedWorldOrigin + settings.noiseSettings.offset,
                sampleStep,
                true);
            float[,] temperatureNoise = Noise.GenerateNoiseMap(
                paddedWidth,
                paddedHeight,
                settings.temperatureNoiseScale,
                2,
                0.5f,
                2f,
                unchecked(settings.noiseSettings.seed + 32452843),
                paddedWorldOrigin + settings.noiseSettings.offset,
                sampleStep,
                true);
            float biomeNoiseAmplitude = 1f + 0.5f;
            float[,] biomes = new float[width, height];
            float[,] moisture = new float[width, height];
            float[,] temperature = new float[width, height];

            for (int y = 0; y < paddedHeight; y++)
            {
                for (int x = 0; x < paddedWidth; x++)
                {
                    float value = noiseMap[x, y];
                    if (float.IsNaN(value) || float.IsInfinity(value))
                    {
                        throw new System.InvalidOperationException(
                            $"Noise generation produced a non-finite value at ({x}, {y}). Check the terrain noise settings.");
                    }

                    if (!settings.noiseSettings.normalize)
                    {
                        value = Mathf.InverseLerp(
                            -maximumNoiseAmplitude,
                            maximumNoiseAmplitude,
                            value);
                    }

                    for (int layerIndex = 0; layerIndex < detailNoiseMaps.Count; layerIndex++)
                    {
                        TerrainNoiseLayerSettings layer = settings.terrainNoiseLayers[layerIndex];
                        value += (detailNoiseMaps[layerIndex][x, y] - 0.5f) * layer.strength;
                    }

                    value = Mathf.Clamp01(value);
                    if (settings.reliefContrast != 1f)
                    {
                        // O ruído normalizado fica concentrado perto de 0,5; o contraste espalha os valores
                        // e dá amplitude real de montanhas e vales.
                        value = Mathf.Clamp01(0.5f + (value - 0.5f) * settings.reliefContrast);
                    }

                    if (useFalloff)
                    {
                        value = Mathf.Clamp01(value - paddedFalloff[x, y]);
                    }

                    value = Mathf.Lerp(value, value * value, Mathf.Clamp01(settings.flatnessBias));
                    float mountainAmount = Mathf.Clamp01(settings.mountainBias) * Mathf.InverseLerp(0.62f, 1f, value);
                    value = Mathf.Lerp(value, Mathf.Pow(value, 0.72f), mountainAmount);
                    if (useFlatZones)
                    {
                        float mask = Mathf.SmoothStep(
                            0f,
                            1f,
                            Mathf.InverseLerp(
                                flatZones.threshold,
                                flatZones.threshold + flatZones.blendWidth,
                                flatMaskNoise[x, y]));
                        float plainLevel = Mathf.Lerp(
                            flatZones.minimumLevel,
                            flatZones.maximumLevel,
                            flatLevelNoise[x, y]);
                        value = Mathf.Lerp(value, plainLevel, mask);
                        protectedPlains[x,y]=mask;
                    }

                    paddedHeights[x, y] = value;

                    if (x >= padding && x < padding + width && y >= padding && y < padding + height)
                    {
                        int mapX = x - padding;
                        int mapY = y - padding;
                        float normalizedMoisture = moistureNoise[x, y];
                        float normalizedTemperature = Mathf.Clamp01(
                            temperatureNoise[x, y] -
                            paddedHeights[x, y] * settings.temperatureAltitudeInfluence);
                        float perlinBiome = Mathf.InverseLerp(
                            -biomeNoiseAmplitude,
                            biomeNoiseAmplitude,
                            biomeNoise[x, y]);
                        float worldX = chunkWorldOrigin.x + (x - padding) * cellSize + settings.noiseSettings.offset.x;
                        float worldZ = chunkWorldOrigin.y + settings.chunkSize - (y - padding) * cellSize +
                            settings.noiseSettings.offset.y;
                        float whiteNoise = GetWhiteNoise(
                            worldX,
                            worldZ,
                            settings.noiseSettings.seed + 7919);
                        float regionalBiome = Mathf.Lerp(perlinBiome, whiteNoise, settings.biomeNoiseWeight);
                        biomes[mapX, mapY] = Mathf.Clamp01(
                            regionalBiome * 0.55f + normalizedMoisture * 0.3f + normalizedTemperature * 0.15f);
                        moisture[mapX, mapY] = normalizedMoisture;
                        temperature[mapX, mapY] = normalizedTemperature;
                    }
                }
            }

            float[,] smoothedHeights = settings.heightSmoothingIterations > 0 && settings.heightSmoothingStrength > 0f
                ? SmoothHeights(paddedHeights, settings.heightSmoothingIterations, settings.heightSmoothingStrength)
                : paddedHeights;
            float[,] heights = ExtractCenter(smoothedHeights, width, height, padding);
            float[,] normalHeights = ExtractCenter(smoothedHeights, width + 2, height + 2, padding - 1);
            float[,] worldHeights = new float[width, height];
            float[,] paddedWorld = new float[paddedWidth,paddedHeight];
            for (int y = 0; y < paddedHeight; y++) for (int x = 0; x < paddedWidth; x++)
            {
                paddedWorld[x,y]=settings.EvaluateHeight(smoothedHeights[x,y])*settings.heightMultiplier;
            }
            paddedWorld=SimpleTerrainErosion.Apply(paddedWorld,protectedPlains,settings.erosion,paddedWorldOrigin,sampleStep,settings.noiseSettings.seed);
            float[,] normalWorldHeights=ExtractCenter(paddedWorld,width+2,height+2,padding-1);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    worldHeights[x, y] = normalWorldHeights[x + 1, y + 1];
                }
            }

            float[,] falloff = useFalloff
                ? ExtractCenter(paddedFalloff, width, height, padding)
                : null;
            float[,] slopes = GenerateSlopeMap(normalWorldHeights, width, height, cellSize);
            float[,] water = new float[width, height];
            float[,] navigation = new float[width, height];
            float[,] poiSuitability = new float[width, height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float elevation = heights[x, y];
                    float slope = slopes[x, y];
                    float actualHeight=worldHeights[x,y];
                    float lakeHeight=settings.EvaluateHeight(settings.lakeThreshold)*settings.heightMultiplier;
                    float riverHeight=settings.EvaluateHeight(settings.riverThreshold)*settings.heightMultiplier;
                    bool lake = actualHeight <= lakeHeight;
                    bool river = settings.riverCount > 0 &&
                        actualHeight > lakeHeight &&
                        actualHeight < riverHeight &&
                        moisture[x, y] > 0.72f &&
                        slope < 5f;
                    water[x, y] = lake || river ? 1f : 0f;
                    navigation[x, y] = water[x, y] > 0f ? 0f :
                        slope < 5f ? 3f :
                        slope < 12f ? 2f :
                        slope < 25f ? 1f : 0f;
                    poiSuitability[x, y] = water[x, y] > 0f
                        ? 0f
                        : navigation[x, y] / 3f * Mathf.Clamp01(1f - slope / 45f);
                    if (settings.BaseSite.IsExcluded(chunkWorldOrigin.x + x * cellSize,
                        chunkWorldOrigin.y + settings.chunkSize - y * cellSize,
                        0))
                    {
                        water[x,y] = navigation[x,y] = poiSuitability[x,y] = 0;
                    }
                }
            }

            return new HeightMap(
                heights,
                falloff,
                biomes,
                normalHeights,
                slopes,
                moisture,
                temperature,
                water,
                navigation,
                poiSuitability,
                worldHeights: worldHeights,
                normalWorldHeights: normalWorldHeights,
                baseSite: settings.BaseSite,
                worldOrigin: chunkWorldOrigin);
        }

        static float[,] GenerateSlopeMap(
            float[,] normalHeights,
            int width,
            int height,
            float cellSize)
        {
            float[,] slopes = new float[width, height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float deltaX = (normalHeights[x + 2, y + 1] - normalHeights[x, y + 1]) / (2f * cellSize);
                    float deltaZ = (normalHeights[x + 1, y + 2] - normalHeights[x + 1, y]) / (2f * cellSize);
                    slopes[x, y] = Mathf.Atan(Mathf.Sqrt(deltaX * deltaX + deltaZ * deltaZ)) * Mathf.Rad2Deg;
                }
            }

            return slopes;
        }

        static float[,] SmoothHeights(float[,] source, int iterations, float strength)
        {
            int width = source.GetLength(0);
            int height = source.GetLength(1);
            float[,] current = source;

            for (int iteration = 0; iteration < iterations; iteration++)
            {
                float[,] next = new float[width, height];
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        float sum = 0f;
                        float weightSum = 0f;
                        for (int sampleY = -1; sampleY <= 1; sampleY++)
                        {
                            int neighborY = Mathf.Clamp(y + sampleY, 0, height - 1);
                            for (int sampleX = -1; sampleX <= 1; sampleX++)
                            {
                                int neighborX = Mathf.Clamp(x + sampleX, 0, width - 1);
                                float weight = sampleX == 0 && sampleY == 0 ? 4f :
                                    sampleX == 0 || sampleY == 0 ? 2f : 1f;
                                sum += current[neighborX, neighborY] * weight;
                                weightSum += weight;
                            }
                        }

                        float average = sum / weightSum;
                        next[x, y] = Mathf.Lerp(current[x, y], average, strength);
                    }
                }

                current = next;
            }

            return current;
        }

        static float[,] ExtractCenter(float[,] source, int width, int height, int padding)
        {
            float[,] result = new float[width, height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    result[x, y] = source[x + padding, y + padding];
                }
            }

            return result;
        }

        static float[,] AddClampedBorder(float[,] source)
        {
            int width = source.GetLength(0);
            int height = source.GetLength(1);
            float[,] result = new float[width + 2, height + 2];

            for (int y = 0; y < result.GetLength(1); y++)
            {
                int sourceY = Mathf.Clamp(y - 1, 0, height - 1);
                for (int x = 0; x < result.GetLength(0); x++)
                {
                    int sourceX = Mathf.Clamp(x - 1, 0, width - 1);
                    result[x, y] = source[sourceX, sourceY];
                }
            }

            return result;
        }

        static float GetWhiteNoise(float x, float y, int seed)
        {
            unchecked
            {
                uint hash = (uint)seed;
                hash ^= (uint)Mathf.RoundToInt(x) * 0x8da6b343u;
                hash ^= (uint)Mathf.RoundToInt(y) * 0xd8163841u;
                hash ^= hash >> 13;
                hash *= 0x85ebca6bu;
                hash ^= hash >> 16;
                return (hash & 0x00ffffffu) / 16777215f;
            }
        }
    }
}
