namespace ProjectLeviathan.ProceduralTerrain
{
    public class HeightMap
    {
        public int Width { get; }
        public int Height { get; }
        public float[,] Values { get; }
        public float[,] Falloff { get; }
        public float[,] Biomes { get; }
        public float[,] NormalHeights { get; }
        public float[,] Slopes { get; }
        public float[,] Moisture { get; }
        public float[,] Temperature { get; }
        public float[,] Water { get; }
        public float[,] Navigation { get; }
        public float[,] PoiSuitability { get; }
        public float[,] PoiMask { get; }
        public float[,] Roads { get; }
        public float[,] WorldHeights { get; }
        public float[,] NormalWorldHeights { get; }
        public ProjectLeviathan.Base.BaseSiteData BaseSite { get; }
        public UnityEngine.Vector2 WorldOrigin { get; }

        public HeightMap(
            float[,] values,
            float[,] falloff = null,
            float[,] biomes = null,
            float[,] normalHeights = null,
            float[,] slopes = null,
            float[,] moisture = null,
            float[,] temperature = null,
            float[,] water = null,
            float[,] navigation = null,
            float[,] poiSuitability = null,
            float[,] poiMask = null,
            float[,] roads = null,
            float[,] worldHeights = null,
            float[,] normalWorldHeights = null,
            ProjectLeviathan.Base.BaseSiteData baseSite = default,
            UnityEngine.Vector2 worldOrigin = default)
        {
            Width = values == null ? 0 : values.GetLength(0);
            Height = values == null ? 0 : values.GetLength(1);
            Values = values ?? new float[0, 0];
            Falloff = falloff ?? new float[Width, Height];
            Biomes = biomes ?? new float[Width, Height];
            NormalHeights = normalHeights ?? Values;
            Slopes = slopes ?? new float[Width, Height];
            Moisture = moisture ?? new float[Width, Height];
            Temperature = temperature ?? new float[Width, Height];
            Water = water ?? new float[Width, Height];
            Navigation = navigation ?? new float[Width, Height];
            PoiSuitability = poiSuitability ?? new float[Width, Height];
            PoiMask = poiMask ?? new float[Width, Height];
            Roads = roads ?? new float[Width, Height];
            WorldHeights = worldHeights;
            NormalWorldHeights = normalWorldHeights;
            BaseSite = baseSite;
            WorldOrigin = worldOrigin;
        }
    }
}
