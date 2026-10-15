using UnityEngine;

namespace ProjectLeviathan.World
{
    public readonly struct WorldWeatherSample
    {
        public Vector3 WindDirection { get; }
        public float WindSpeedMetersPerSecond { get; }
        public float VisibilityMeters { get; }
        public float RainIntensity { get; }

        public WorldWeatherSample(
            Vector3 windDirection,
            float windSpeedMetersPerSecond,
            float visibilityMeters,
            float rainIntensity)
        {
            WindDirection = windDirection;
            WindSpeedMetersPerSecond = windSpeedMetersPerSecond;
            VisibilityMeters = visibilityMeters;
            RainIntensity = rainIntensity;
        }
    }

    public sealed class WorldWeatherProvider : MonoBehaviour
    {
        [SerializeField] int seed = 12345;
        [SerializeField, Range(0f, 360f)] float prevailingWindDegrees = 225f;
        [SerializeField, Range(0f, 90f)] float windDirectionVariationDegrees = 18f;
        [SerializeField, Min(0f)] float windSpeedMetersPerSecond = 4f;
        [SerializeField, Min(0f)] float windSpeedVariation = 2f;
        [SerializeField, Min(100f)] float visibilityMeters = 12000f;
        [SerializeField, Range(0f, 1f)] float rainIntensity;
        [SerializeField, Min(1f)] float weatherCellSizeMeters = 5000f;

        public int ConditionsRevision { get; private set; }

        public WorldWeatherSample SampleAt(Vector3 worldPosition)
        {
            float sample = Mathf.PerlinNoise(
                (worldPosition.x + seed * 41.3f) / weatherCellSizeMeters,
                (worldPosition.z - seed * 17.9f) / weatherCellSizeMeters);
            float directionOffset = (sample - 0.5f) * 2f * windDirectionVariationDegrees;
            Vector3 direction = Quaternion.Euler(0f, prevailingWindDegrees + directionOffset, 0f) *
                Vector3.forward;
            float speed = Mathf.Max(0f, windSpeedMetersPerSecond + (sample - 0.5f) * 2f * windSpeedVariation);
            float visibility = Mathf.Lerp(visibilityMeters, visibilityMeters * 0.35f, rainIntensity);
            return new WorldWeatherSample(
                direction.normalized,
                speed,
                visibility,
                rainIntensity);
        }

        public void SetConditions(
            float windDirectionDegrees,
            float windSpeed,
            float visibility,
            float rain)
        {
            prevailingWindDegrees = Mathf.Repeat(windDirectionDegrees, 360f);
            windSpeedMetersPerSecond = Mathf.Max(0f, windSpeed);
            visibilityMeters = Mathf.Max(100f, visibility);
            rainIntensity = Mathf.Clamp01(rain);
            ConditionsRevision++;
        }

        void OnValidate()
        {
            prevailingWindDegrees = Mathf.Repeat(prevailingWindDegrees, 360f);
            windSpeedMetersPerSecond = Mathf.Max(0f, windSpeedMetersPerSecond);
            windSpeedVariation = Mathf.Max(0f, windSpeedVariation);
            visibilityMeters = Mathf.Max(100f, visibilityMeters);
            rainIntensity = Mathf.Clamp01(rainIntensity);
            weatherCellSizeMeters = Mathf.Max(1f, weatherCellSizeMeters);
            ConditionsRevision++;
        }
    }
}
