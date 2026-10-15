using UnityEngine;

namespace ProjectLeviathan.Grass
{
    [CreateAssetMenu(menuName = "Leviathan/Grass Configuration")]
    public sealed class GrassConfiguration : ScriptableObject
    {
        public ComputeShader compute;
        public Material material;
        [Header("Residency and budgets")]
        [Min(4f)] public float patchSize = 30f;
        [Range(0.1f, 16f)] public float bladesPerSquareMeter = 5f;
        [Range(4, 128)] public int maximumResidentPatches = 64;
        [Range(1, 8)] public int generationsPerFrame = 2;
        [Range(1, 4)] public int uploadsPerFrame = 1;
        [Min(1)] public int seed = 9173;
        [Header("Geometry and LOD")]
        public Vector2 heightRange = new Vector2(0.35f, 0.65f);
        public Vector2 widthRange = new Vector2(0.025f, 0.045f);
        public Vector2 bendRange = new Vector2(0.12f, 0.32f);
        public float nearDistance = 18f;
        public float middleDistance = 42f;
        public float farDistance = 90f;
        public float transitionWidth = 12f;
        [Range(0.1f, 1f)] public float middleDensity = 0.42f;
        [Range(0.02f, 0.5f)] public float farDensity = 0.12f;
        public Vector2 widerDistantBlades = new Vector2(1.55f, 2.5f);
        [Range(0.05f, 1f)] public float patchFadeSeconds = 0.25f;
        [Header("Eligibility")]
        public Vector2 normalizedHeightRange = new Vector2(0.17f, 0.78f);
        public Vector2 biomeRange = new Vector2(0.12f, 0.82f);
        [Range(0f, 60f)] public float maximumSlope = 28f;
        [Range(0f, 20f)] public float slopeFade = 10f;
        public bool excludeProps = true;
        [Header("Base grass edge (does not affect trees or scenery)")]
        [Tooltip("Clear ground outside the concrete foundation, in metres.")]
        [Min(0f)] public float baseClearance = 0.8f;
        [Tooltip("Distance over which grass density returns to normal, in metres.")]
        [Min(0.1f)] public float baseEdgeFade = 2f;
        [Tooltip("Outward variation of the edge; never encroaches on the foundation.")]
        [Min(0f)] public float baseEdgeVariation = 1.5f;
        [Min(0.001f)] public float baseEdgeNoiseScale = 0.18f;
        [Header("Appearance")]
        [Header("Meadow patches (world space)")]
        [Min(1)] public float meadowPatchSize = 38;
        [Range(0, 1)] public float meadowVariation = .75f;
        [Range(0, 1)] public float meadowMinimumDensity = .18f;
        public Color rootColor = new Color(0.13f, 0.26f, 0.09f);
        public Color tipColor = new Color(0.45f, 0.66f, 0.23f);
        [Range(0f, 1f)] public float biomeColorInfluence = 0.3f;
        [Range(0f, 1f)] public float translucency = 0.35f;
        [Range(0f, 0.5f)] public float specular = 0.08f;
        [Range(0f, 1f)] public float roundedNormals = 0.6f;
        [Range(0f, 1f)] public float viewThickening = 0.65f;
        [Header("Wind and interaction")]
        public bool useWorldWeather = true;
        public Vector2 windDirection = new Vector2(0.7f, 0.3f);
        public float windSpeed = 3f;
        [Range(0f, 1f)] public float windStrength = 0.25f;
        [Min(0.001f)] public float windNoiseScale = 0.08f;
        public bool playerInteraction = true;
        [Min(0.1f)] public float playerRadius = 1.3f;
        [Header("Shadows and diagnostics")]
        public bool castNearShadows = true;
        [Range(0f, 40f)] public float shadowDistance = 22f;
        public bool culling = true;
        public bool collectGpuStatistics = true;
        public bool drawGizmos;

        public int GridResolution => Mathf.Clamp(Mathf.CeilToInt(patchSize * Mathf.Sqrt(bladesPerSquareMeter)), 1, 128);
        public int CandidateCount => GridResolution * GridResolution;

        void OnValidate()
        {
            patchSize = Mathf.Max(4f, patchSize);
            heightRange.x = Mathf.Max(0.05f, heightRange.x);
            heightRange.y = Mathf.Max(heightRange.x, heightRange.y);
            widthRange.x = Mathf.Max(0.005f, widthRange.x);
            widthRange.y = Mathf.Max(widthRange.x, widthRange.y);
            baseClearance = Mathf.Max(0f, baseClearance);
            baseEdgeFade = Mathf.Max(0.1f, baseEdgeFade);
            baseEdgeVariation = Mathf.Max(0f, baseEdgeVariation);
            baseEdgeNoiseScale = Mathf.Max(0.001f, baseEdgeNoiseScale);
            middleDistance = Mathf.Max(nearDistance + 5f, middleDistance);
            farDistance = Mathf.Max(middleDistance + 10f, farDistance);
        }
    }
}
