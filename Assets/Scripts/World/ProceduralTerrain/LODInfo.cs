using UnityEngine;

namespace ProjectLeviathan.ProceduralTerrain
{
    [System.Serializable]
    public class LODInfo
    {
        public int lod = 0;
        public float visibleDistance = 200f;
        public int meshResolution = 1;
        public bool useForRendering = true;

        public int GetSimplificationLevel()
        {
            return Mathf.Max(1, meshResolution);
        }
    }
}
