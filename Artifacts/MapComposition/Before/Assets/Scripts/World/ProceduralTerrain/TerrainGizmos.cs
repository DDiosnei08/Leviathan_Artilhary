using UnityEngine;

namespace ProjectLeviathan.ProceduralTerrain
{
    public class TerrainGizmos : MonoBehaviour
    {
        public TerrainWorldController worldController;
        public bool drawChunkBounds = true;
        public bool drawLodLabels = true;
        public bool drawHeightPreview = false;
        public bool drawPOIMarkers = true;
        public Color chunkColor = Color.green;
        public Color lodColor = Color.yellow;
        public Color poiColor = new Color(0.8f, 0.56f, 0.2f);

        void OnDrawGizmos()
        {
            if (worldController == null)
            {
                return;
            }

            foreach (TerrainChunk chunk in worldController.TerrainChunks.Values)
            {
                if (chunk == null || chunk.GameObject == null)
                {
                    continue;
                }

                if (drawChunkBounds)
                {
                    Gizmos.color = chunkColor;
                    Vector3 center = chunk.CenterWorldPosition;
                    Vector3 size = new Vector3(worldController.terrainSettings.chunkSize, 3f, worldController.terrainSettings.chunkSize);
                    Gizmos.DrawWireCube(center, size);
                }

                if (drawLodLabels)
                {
#if UNITY_EDITOR
                    Vector3 labelPosition = chunk.CenterWorldPosition + Vector3.up * 7f;
                    UnityEditor.Handles.Label(labelPosition, chunk.DebugLabel);
#endif
                }
            }

            if (drawPOIMarkers)
            {
                foreach (TerrainWorldPOI poi in worldController.GeneratedPOIs)
                {
                    Vector3 position = worldController.transform.TransformPoint(poi.TerrainPosition);
                    Gizmos.color = poiColor;
                    Gizmos.DrawSphere(position + Vector3.up * 4f, 6f);
#if UNITY_EDITOR
                    UnityEditor.Handles.Label(position + Vector3.up * 12f, poi.DisplayName);
#endif
                }
            }
        }
    }
}
