using UnityEngine;
using ProjectLeviathan.World;

namespace ProjectLeviathan.ProceduralTerrain
{
    public static class TextureGenerator
    {
        public static Texture2D TextureFromHeightMap(float[,] heightMap)
        {
            int width = heightMap.GetLength(0);
            int height = heightMap.GetLength(1);
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            Color[] colors = new Color[width * height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float value = Mathf.Clamp01(heightMap[x, y]);
                    colors[y * width + x] = new Color(value, value, value);
                }
            }

            texture.SetPixels(colors);
            texture.Apply();
            return texture;
        }

        public static Texture2D TextureFromColorMap(Color[] colorMap, int width, int height)
        {
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.SetPixels(colorMap);
            texture.Apply();
            return texture;
        }

        public static Color[] BuildColorMap(float[,] heightMap, TerrainSettings settings)
        {
            return BuildColorMap(heightMap, null, settings, Vector2.zero);
        }

        public static Color[] BuildColorMap(HeightMap terrainData, TerrainSettings settings, Vector2 chunkWorldOrigin)
        {
            return BuildColorMap(terrainData, settings, chunkWorldOrigin, default);
        }

        public static Color[] BuildColorMap(
            HeightMap terrainData,
            TerrainSettings settings,
            Vector2 chunkWorldOrigin,
            WorldWeatherSample weather)
        {
            if (terrainData == null)
            {
                throw new System.ArgumentNullException(nameof(terrainData));
            }

            return BuildColorMap(
                terrainData.Values,
                terrainData.Biomes,
                terrainData.Slopes,
                terrainData.Water,
                terrainData.Moisture,
                terrainData.Temperature,
                terrainData.PoiMask,
                terrainData.Roads,
                settings,
                chunkWorldOrigin,
                weather.RainIntensity);
        }

        public static Color[] BuildColorMap(
            float[,] heightMap,
            float[,] biomeMap,
            TerrainSettings settings,
            Vector2 chunkWorldOrigin)
        {
            return BuildColorMap(
                heightMap,
                biomeMap,
                null,
                null,
                null,
                null,
                null,
                null,
                settings,
                chunkWorldOrigin,
                0f);
        }

        public static Color[] BuildColorMap(
            float[,] heightMap,
            float[,] biomeMap,
            float[,] slopeMap,
            float[,] waterMap,
            float[,] moistureMap,
            float[,] temperatureMap,
            float[,] poiMap,
            float[,] roadsMap,
            TerrainSettings settings,
            Vector2 chunkWorldOrigin,
            float rainIntensity)
        {
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<TerrainSettings>();
            }

            settings.Validate();
            TerrainSurfacePalette palette = settings.surfacePalette;
            int width = heightMap.GetLength(0);
            int height = heightMap.GetLength(1);
            Color[] colorMap = new Color[width * height];

            Color roadColor = new Color(0.42f, 0.35f, 0.25f); // Rich packed dirt / gravel road
            Color roadEdgeColor = new Color(0.32f, 0.28f, 0.20f);
            Color riverWaterColor = new Color(0.12f, 0.40f, 0.62f);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float sample = Mathf.Clamp01(heightMap[x, y]);
                    float biomeSample = biomeMap != null ? Mathf.Clamp01(biomeMap[x, y]) : sample;
                    float biomeClassification = Mathf.Lerp(biomeSample, sample, settings.biomeHeightInfluence);

                    // 1. Primary Biome Color
                    Color chosenColor = settings.useTerrainColors
                        ? settings.GetBiomeColor(biomeClassification)
                        : Color.white;

                    bool isWater = waterMap != null && waterMap[x, y] > 0.5f;
                    float moisture = moistureMap != null ? Mathf.Clamp01(moistureMap[x, y]) : 0.5f;
                    float temperature = temperatureMap != null ? Mathf.Clamp01(temperatureMap[x, y]) : 0.5f;
                    float slope = slopeMap != null ? slopeMap[x, y] : 0f;
                    float roadStrength = roadsMap != null ? Mathf.Clamp01(roadsMap[x, y]) : 0f;

                    // 2. Lake / Ocean below threshold
                    if (sample < settings.lakeThreshold)
                    {
                        chosenColor = settings.oceanColor;
                    }

                    // 3. Subtle climate / slope variation (without washing out base biomes)
                    if (!isWater && sample >= settings.lakeThreshold)
                    {
                        float dryBlend = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(
                            palette.dryMoistureThreshold - 0.15f,
                            palette.dryMoistureThreshold + 0.15f,
                            moisture));
                        float wetBlend = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(
                            palette.wetMoistureThreshold - 0.15f,
                            palette.wetMoistureThreshold + 0.15f,
                            moisture));
                        float rockyBlend = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(
                            palette.rockySlopeThreshold - 6f,
                            palette.rockySlopeThreshold + 6f,
                            slope));

                        float blendStrength = Mathf.Min(palette.climateBlendStrength, 0.45f);
                        chosenColor = Color.Lerp(chosenColor, palette.dryColor, dryBlend * blendStrength * 0.4f);
                        chosenColor = Color.Lerp(chosenColor, palette.wetColor, wetBlend * blendStrength * 0.45f);
                        chosenColor = Color.Lerp(chosenColor, palette.rockyColor, rockyBlend * 0.75f);
                    }

                    // 4. River / Water Stream
                    if (isWater && sample >= settings.lakeThreshold)
                    {
                        chosenColor = riverWaterColor;
                    }

                    // 5. Roads & Pathways (clearly drawn on terrain texture)
                    if (roadStrength > 0.1f && !isWater)
                    {
                        Color blendedRoad = Color.Lerp(roadEdgeColor, roadColor, roadStrength);
                        chosenColor = Color.Lerp(chosenColor, blendedRoad, Mathf.SmoothStep(0.1f, 0.9f, roadStrength) * 0.95f);
                    }

                    // 6. POI ground clearings
                    if (!isWater && poiMap != null && poiMap[x, y] > 0.5f && roadStrength <= 0.3f)
                    {
                        chosenColor = Color.Lerp(chosenColor, new Color(0.55f, 0.48f, 0.32f), 0.8f);
                    }

                    // 7. Rain Weather Effect
                    if (!isWater && rainIntensity > 0f)
                    {
                        float rain = Mathf.Clamp01(rainIntensity);
                        chosenColor = Color.Lerp(chosenColor, palette.wetColor, rain * palette.rainWetColorBlend * 0.5f);
                        chosenColor = Color.Lerp(chosenColor, Color.black, rain * palette.rainDarkening * 0.4f);
                    }

                    colorMap[y * width + x] = chosenColor;
                }
            }

            return colorMap;
        }
    }
}
