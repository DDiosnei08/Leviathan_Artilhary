using UnityEngine;

namespace ProjectLeviathan.ProceduralTerrain
{
    [System.Serializable]
    public class TerrainBiomePreset
    {
        public string name = "Biome";
        public float minHeight = 0f;
        public float maxHeight = 1f;
        public Color color = Color.white;
        public Material material;

        public bool Contains(float height)
        {
            return height >= minHeight && height <= maxHeight;
        }
    }
}
