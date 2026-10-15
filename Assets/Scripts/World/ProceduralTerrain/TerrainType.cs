using UnityEngine;

namespace ProjectLeviathan.ProceduralTerrain
{
    [System.Serializable]
    public class TerrainType
    {
        public string name = "Terrain";
        public float minHeight = 0f;
        public float maxHeight = 1f;
        public Color color = Color.white;
        public Material material;
    }
}
