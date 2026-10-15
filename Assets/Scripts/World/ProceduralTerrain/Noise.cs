using UnityEngine;

namespace ProjectLeviathan.ProceduralTerrain
{
    public static class Noise
    {
        static readonly System.Collections.Generic.Dictionary<long, Vector2[]> octaveCache = new System.Collections.Generic.Dictionary<long, Vector2[]>();
        static readonly object octaveCacheLock = new object();

        public static float[,] GenerateNoiseMap(int width, int height, NoiseSettings settings, Vector2 offset)
        {
            if (settings == null)
            {
                settings = new NoiseSettings();
            }

            settings.Validate();
            return GenerateNoiseMap(
                width,
                height,
                settings.scale,
                settings.octaves,
                settings.persistence,
                settings.lacunarity,
                settings.seed,
                offset + settings.offset,
                Vector2.one,
                settings.normalize);
        }

        public static float[,] GenerateNoiseMap(
            int mapWidth,
            int mapHeight,
            float scale,
            int octaves,
            float persistence,
            float lacunarity,
            int seed,
            Vector2 worldOrigin,
            bool normalize = true)
        {
            return GenerateNoiseMap(
                mapWidth,
                mapHeight,
                scale,
                octaves,
                persistence,
                lacunarity,
                seed,
                worldOrigin,
                Vector2.one,
                normalize);
        }

        public static float[,] GenerateNoiseMap(
            int mapWidth,
            int mapHeight,
            float scale,
            int octaves,
            float persistence,
            float lacunarity,
            int seed,
            Vector2 worldOrigin,
            Vector2 sampleStep,
            bool normalize = true)
        {
            float[,] noiseMap = new float[mapWidth, mapHeight];
            if (mapWidth <= 0 || mapHeight <= 0)
            {
                return noiseMap;
            }

            scale = Mathf.Max(0.0001f, scale);
            Vector2[] octaveOffsets = GetOctaveOffsets(seed, octaves);
            float maxPossibleHeight = 0f;
            float amplitude = 1f;

            for (int i = 0; i < octaves; i++)
            {
                maxPossibleHeight += amplitude;
                amplitude *= persistence;
            }

            for (int y = 0; y < mapHeight; y++)
            {
                for (int x = 0; x < mapWidth; x++)
                {
                    float amplitudeValue = 1f;
                    float frequency = 1f;
                    float noiseHeight = 0f;

                    for (int octave = 0; octave < octaves; octave++)
                    {
                        float sampleX = ((worldOrigin.x + x * sampleStep.x + octaveOffsets[octave].x) / scale) * frequency;
                        float sampleY = ((worldOrigin.y + y * sampleStep.y + octaveOffsets[octave].y) / scale) * frequency;

                        float perlinValue = PerlinNoise(sampleX, sampleY) * 2f - 1f;
                        noiseHeight += perlinValue * amplitudeValue;

                        amplitudeValue *= persistence;
                        frequency *= lacunarity;
                    }

                    noiseMap[x, y] = noiseHeight;
                }
            }

            if (!normalize)
            {
                return noiseMap;
            }

            for (int y = 0; y < mapHeight; y++)
            {
                for (int x = 0; x < mapWidth; x++)
                {
                    float value = noiseMap[x, y];
                    noiseMap[x, y] = Mathf.InverseLerp(-maxPossibleHeight, maxPossibleHeight, value);
                }
            }

            return noiseMap;
        }

        static float PerlinNoise(float x, float y)
        {
            int cellX = (int)System.Math.Floor(x);
            int cellY = (int)System.Math.Floor(y);
            float localX = x - cellX;
            float localY = y - cellY;
            float fadeX = Fade(localX);
            float fadeY = Fade(localY);

            float bottom = Lerp(
                Gradient(Hash(cellX, cellY), localX, localY),
                Gradient(Hash(cellX + 1, cellY), localX - 1f, localY),
                fadeX);
            float top = Lerp(
                Gradient(Hash(cellX, cellY + 1), localX, localY - 1f),
                Gradient(Hash(cellX + 1, cellY + 1), localX - 1f, localY - 1f),
                fadeX);
            return Mathf.Clamp01(0.5f + Lerp(bottom, top, fadeY) * 0.35355339f);
        }

        static float Gradient(uint hash, float x, float y)
        {
            switch (hash & 7u)
            {
                case 0: return x + y;
                case 1: return -x + y;
                case 2: return x - y;
                case 3: return -x - y;
                case 4: return x;
                case 5: return -x;
                case 6: return y;
                default: return -y;
            }
        }

        static uint Hash(int x, int y)
        {
            unchecked
            {
                uint hash = (uint)x * 0x8da6b343u ^ (uint)y * 0xd8163841u;
                hash ^= hash >> 13;
                hash *= 0x85ebca6bu;
                hash ^= hash >> 16;
                return hash;
            }
        }

        static float Fade(float value)
        {
            return value * value * value * (value * (value * 6f - 15f) + 10f);
        }

        static float Lerp(float from, float to, float amount)
        {
            return from + (to - from) * amount;
        }

        static Vector2[] GetOctaveOffsets(int seed, int octaves)
        {
            long cacheKey = ((long)seed << 32) | (uint)octaves;
            lock (octaveCacheLock)
            {
                if (octaveCache.TryGetValue(cacheKey, out Vector2[] cached))
                {
                    return cached;
                }

                System.Random random = new System.Random(seed);
                Vector2[] offsets = new Vector2[octaves];

                for (int i = 0; i < octaves; i++)
                {
                    offsets[i] = new Vector2(
                        random.Next(-100000, 100000),
                        random.Next(-100000, 100000));
                }

                octaveCache[cacheKey] = offsets;
                return offsets;
            }
        }
    }
}
