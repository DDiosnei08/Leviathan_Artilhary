using UnityEngine;

namespace ProjectLeviathan.ProceduralTerrain
{
    public class TerrainDebug : MonoBehaviour
    {
        public TerrainWorldController terrainWorld;
        public bool drawChunkBounds = true;
        public bool drawLod = true;
        public bool drawHeightMap = false;

        void OnDrawGizmos()
        {
            if (terrainWorld == null)
            {
                return;
            }

            if (drawChunkBounds)
            {
                Gizmos.color = Color.green;
                foreach (var chunk in terrainWorld.GetType().GetField("terrainChunks", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(terrainWorld) as System.Collections.IEnumerable)
                {
                    var chunkObj = chunk.GetType().GetProperty("GameObject").GetValue(chunk) as GameObject;
                    if (chunkObj != null)
                    {
                        Vector3 localCenter = new Vector3(
                            terrainWorld.terrainSettings.chunkSize * 0.5f,
                            0f,
                            terrainWorld.terrainSettings.chunkSize * 0.5f);
                        Gizmos.DrawWireCube(
                            chunkObj.transform.TransformPoint(localCenter),
                            new Vector3(terrainWorld.terrainSettings.chunkSize, 3f, terrainWorld.terrainSettings.chunkSize));
                    }
                }
            }
        }
    }
}
