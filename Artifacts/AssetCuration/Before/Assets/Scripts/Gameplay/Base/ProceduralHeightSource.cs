using System.Collections.Generic;
using ProjectLeviathan.ProceduralTerrain;
using UnityEngine;

namespace ProjectLeviathan.Base
{
    public sealed class ProceduralHeightSource : MonoBehaviour, IHeightSource
    {
        public TerrainWorldController terrain;
        readonly Dictionary<Vector2Int,HeightMap> cache = new Dictionary<Vector2Int,HeightMap>();
        TerrainGenerationSettings naturalSettings;
        public void ResetCache()
        { cache.Clear(); naturalSettings=terrain.terrainSettings.CaptureGenerationSettings(false); }
        public float GetBaseHeight(float x,float z)
        {
            if (naturalSettings==null) ResetCache();
            float size=naturalSettings.chunkSize;
            Vector2Int coordinate=new Vector2Int(Mathf.FloorToInt(x/size),Mathf.FloorToInt(z/size));
            if (!cache.TryGetValue(coordinate,out HeightMap map))
            {
                map=HeightMapGenerator.GenerateHeightMap(naturalSettings,naturalSettings.chunkResolution,naturalSettings.chunkResolution,new Vector2(coordinate.x*size,coordinate.y*size));
                cache.Add(coordinate,map);
            }
            float gx=(x-coordinate.x*size)/size*(map.Width-1), gy=(1-(z-coordinate.y*size)/size)*(map.Height-1);
            int ix=Mathf.Min(Mathf.FloorToInt(gx),map.Width-2), iy=Mathf.Min(Mathf.FloorToInt(gy),map.Height-2);
            float fx=gx-ix,fy=gy-iy;
            return fx+fy<=1 ? map.WorldHeights[ix,iy]*(1-fx-fy)+map.WorldHeights[ix+1,iy]*fx+map.WorldHeights[ix,iy+1]*fy
                : map.WorldHeights[ix+1,iy+1]*(fx+fy-1)+map.WorldHeights[ix+1,iy]*(1-fy)+map.WorldHeights[ix,iy+1]*(1-fx);
        }
    }
}
