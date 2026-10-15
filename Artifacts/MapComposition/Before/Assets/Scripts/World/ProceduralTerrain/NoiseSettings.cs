using UnityEngine;
using System;

namespace ProjectLeviathan.ProceduralTerrain
{
    [System.Serializable]
    public class TerrainNoiseLayerSettings
    {
        public string name = "Noise Layer";
        [Min(1f)] public float scaleMeters = 1800f;
        [Range(0f, 1f)] public float strength = 0.25f;
        [Range(1, 8)] public int octaves = 2;
        [Range(0.01f, 1f)] public float persistence = 0.5f;
        [Range(1f, 5f)] public float lacunarity = 2f;
    }

    [System.Serializable]
    public class NoiseSettings
    {
        [Header("Seed and sampling")]
        public int seed = 1337;
        [Tooltip("Noise feature scale in world meters, independent of mesh resolution.")]
        public float scale = 281.25f;
        [Tooltip("World-space sampling offset in meters.")]
        public Vector2 offset = Vector2.zero;

        [Header("Fractal noise")]
        [Range(1, 8)]
        public int octaves = 4;
        [Range(0.01f, 1f)]
        public float persistence = 0.5f;
        [Range(1.1f, 5f)]
        public float lacunarity = 2f;

        [Header("Normalization")]
        public bool normalize = true;

        public void Validate()
        {
            if (float.IsNaN(scale) || float.IsInfinity(scale))
            {
                throw new ArgumentOutOfRangeException(nameof(scale), "Noise scale must be a finite number.");
            }

            if (float.IsNaN(persistence) || float.IsInfinity(persistence))
            {
                throw new ArgumentOutOfRangeException(nameof(persistence), "Noise persistence must be a finite number.");
            }

            if (float.IsNaN(lacunarity) || float.IsInfinity(lacunarity))
            {
                throw new ArgumentOutOfRangeException(nameof(lacunarity), "Noise lacunarity must be a finite number.");
            }

            if (float.IsNaN(offset.x) || float.IsInfinity(offset.x) ||
                float.IsNaN(offset.y) || float.IsInfinity(offset.y))
            {
                throw new ArgumentOutOfRangeException(nameof(offset), "Noise offset must contain finite numbers.");
            }

            if (scale <= 0.0001f)
            {
                scale = 0.0001f;
            }

            if (octaves < 1)
            {
                octaves = 1;
            }

            if (persistence <= 0f)
            {
                persistence = 0.0001f;
            }
            else
            {
                persistence = Mathf.Clamp01(persistence);
            }

            if (lacunarity < 1f)
            {
                lacunarity = 1f;
            }
            else
            {
                lacunarity = Mathf.Min(lacunarity, 5f);
            }

            octaves = Mathf.Clamp(octaves, 1, 8);
        }

        public float GetMaximumAmplitude()
        {
            float maximum = 0f;
            float amplitude = 1f;

            for (int i = 0; i < octaves; i++)
            {
                maximum += amplitude;
                amplitude *= persistence;
            }

            return maximum;
        }
    }
}
