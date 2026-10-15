using System.Collections.Generic;
using ProjectLeviathan.ProceduralTerrain;
using UnityEngine;

namespace ProjectLeviathan.Base
{
    public sealed class ProceduralHeightSource : MonoBehaviour, IHeightSource, IBaseSiteSuitability
    {
        public TerrainWorldController terrain;
        readonly Dictionary<Vector2Int,HeightMap> cache = new Dictionary<Vector2Int,HeightMap>();
        TerrainGenerationSettings naturalSettings;
        public void ResetCache()
        { cache.Clear(); naturalSettings=terrain.terrainSettings.CaptureGenerationSettings(false); }
        HeightMap SampleMap(float x,float z,out float gx,out float gy)
        {
            if (naturalSettings==null) ResetCache();
            float size=naturalSettings.chunkSize;
            Vector2Int coordinate=new Vector2Int(Mathf.FloorToInt(x/size),Mathf.FloorToInt(z/size));
            if (!cache.TryGetValue(coordinate,out HeightMap map))
            {
                map=HeightMapGenerator.GenerateHeightMap(naturalSettings,naturalSettings.chunkResolution,naturalSettings.chunkResolution,new Vector2(coordinate.x*size,coordinate.y*size));
                cache.Add(coordinate,map);
            }
            gx=(x-coordinate.x*size)/size*(map.Width-1);gy=(1-(z-coordinate.y*size)/size)*(map.Height-1);
            return map;
        }
        public bool IsDry(float x,float z)
        {
            var map=SampleMap(x,z,out float gx,out float gy);int xx=Mathf.Min((int)gx,map.Width-2),yy=Mathf.Min((int)gy,map.Height-2);
            return map.Water[xx,yy]<.01f && map.Water[xx+1,yy]<.01f && map.Water[xx,yy+1]<.01f && map.Water[xx+1,yy+1]<.01f;
        }
        public float GetBaseHeight(float x,float z)
        {
            var map=SampleMap(x,z,out float gx,out float gy);
            int ix=Mathf.Min(Mathf.FloorToInt(gx),map.Width-2), iy=Mathf.Min(Mathf.FloorToInt(gy),map.Height-2);
            float fx=gx-ix,fy=gy-iy;
            return fx+fy<=1 ? map.WorldHeights[ix,iy]*(1-fx-fy)+map.WorldHeights[ix+1,iy]*fx+map.WorldHeights[ix,iy+1]*fy
                : map.WorldHeights[ix+1,iy+1]*(fx+fy-1)+map.WorldHeights[ix+1,iy]*(1-fy)+map.WorldHeights[ix,iy+1]*(1-fx);
        }
    }
}
