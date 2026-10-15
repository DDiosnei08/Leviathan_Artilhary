using System.Collections.Generic;
using UnityEngine;

namespace ProjectLeviathan.ProceduralTerrain
{
    /// <summary>Natural terrain with a small geometric opening for the imported pit, without height deformation.</summary>
    public static class BaseTerrainMesh
    {
        public static MeshData Generate(HeightMap map, TerrainGenerationSettings s, int lod)
        {
            var site = map.BaseSite;
            Vector2 nearest = new Vector2(Mathf.Clamp(site.center.x,map.WorldOrigin.x,map.WorldOrigin.x+s.chunkSize),
                Mathf.Clamp(site.center.y,map.WorldOrigin.y,map.WorldOrigin.y+s.chunkSize));
            if (!site.valid || (nearest-site.center).sqrMagnitude > site.InfluenceRadius*site.InfluenceRadius)
                return MeshGenerator.GenerateTerrainMesh(map.Values,s,lod,map.NormalHeights,map.WorldHeights,map.NormalWorldHeights);

            float cell = s.chunkSize/(map.Width-1);
            int divisions = Mathf.Clamp(Mathf.CeilToInt(cell / .5f),1,32);
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var normals = new List<Vector3>();
            var triangles = new List<int>();
            for (int y=0;y<map.Height-1;y++) for (int x=0;x<map.Width-1;x++)
            {
                float wx=map.WorldOrigin.x+x*cell, wz=map.WorldOrigin.y+s.chunkSize-y*cell;
                Vector2 closest=new Vector2(Mathf.Clamp(site.center.x,wx,wx+cell),Mathf.Clamp(site.center.y,wz-cell,wz));
                float opening=site.carveRadius+cell;
                int n=(closest-site.center).sqrMagnitude<=opening*opening ? divisions : 1;
                int start=vertices.Count;
                for(int j=0;j<=n;j++) for(int i=0;i<=n;i++)
                {
                    float fx=i/(float)n, fy=j/(float)n;
                    // Fine edges remain on the original coarse triangles, without earthworks.
                    float natural=SampleWorld(map.WorldHeights,x,y,fx,fy);
                    float px=wx+fx*cell,pz=wz-fy*cell;
                    float h=site.ModifyHeight(px,pz,natural);
                    vertices.Add(new Vector3((x+fx)*cell,h,s.chunkSize-(y+fy)*cell));
                    uv.Add(new Vector2((x+fx)/(map.Width-1),(y+fy)/(map.Height-1)));
                    float l=site.ModifyHeight(px-.25f,pz,NaturalAt(map,s,px-.25f,pz));
                    float r=site.ModifyHeight(px+.25f,pz,NaturalAt(map,s,px+.25f,pz));
                    float u=site.ModifyHeight(px,pz+.25f,NaturalAt(map,s,px,pz+.25f));
                    float d=site.ModifyHeight(px,pz-.25f,NaturalAt(map,s,px,pz-.25f));
                    Vector3 normal = new Vector3((l-r)*2,1,(d-u)*2).normalized;
                    if ((new Vector2(px,pz)-site.center).sqrMagnitude >= site.InfluenceRadius*site.InfluenceRadius)
                    {
                        normal=Vector3.Lerp(Vector3.Lerp(GridNormal(map,x,y,cell),GridNormal(map,x+1,y,cell),fx),
                            Vector3.Lerp(GridNormal(map,x,y+1,cell),GridNormal(map,x+1,y+1,cell),fx),fy).normalized;
                    }
                    normals.Add(normal);
                }
                for(int j=0;j<n;j++) for(int i=0;i<n;i++)
                {
                    int a=start+j*(n+1)+i,b=a+1,c=a+n+1,e=c+1;
                        AddOutsideOpening(triangles,vertices,a,b,c,map.WorldOrigin,site.center,site.carveRadius);
                        AddOutsideOpening(triangles,vertices,b,e,c,map.WorldOrigin,site.center,site.carveRadius);
                }
            }
            var data=new MeshData(2,2) { vertices=vertices.ToArray(),normals=normals.ToArray(),uvs=uv.ToArray(),triangles=triangles.ToArray() };
            return data;
        }
        static void AddOutsideOpening(List<int> triangles,List<Vector3> vertices,int a,int b,int c,Vector2 origin,Vector2 center,float radius)
        {
            Vector3 centroid=(vertices[a]+vertices[b]+vertices[c])/3;
            Vector2 point=new Vector2(origin.x+centroid.x,origin.y+centroid.z);
            if((point-center).sqrMagnitude<radius*radius) return;
            triangles.Add(a);triangles.Add(b);triangles.Add(c);
        }
        static float SampleWorld(float[,] h,int x,int y,float fx,float fy)
        {
            return fx+fy<=1 ? h[x,y]*(1-fx-fy)+h[x+1,y]*fx+h[x,y+1]*fy
                : h[x+1,y+1]*(fx+fy-1)+h[x+1,y]*(1-fy)+h[x,y+1]*(1-fx);
        }
        static float NaturalAt(HeightMap map,TerrainGenerationSettings s,float wx,float wz)
        {
            float cell=s.chunkSize/(map.Width-1);
            float gx=Mathf.Clamp((wx-map.WorldOrigin.x)/cell+1,0,map.Width+1);
            float gy=Mathf.Clamp((map.WorldOrigin.y+s.chunkSize-wz)/cell+1,0,map.Height+1);
            int x=Mathf.Min((int)gx,map.Width),y=Mathf.Min((int)gy,map.Height);
            return SampleWorld(map.NormalWorldHeights,x,y,gx-x,gy-y);
        }
        static Vector3 GridNormal(HeightMap map,int x,int y,float cell)
        {
            var h=map.NormalWorldHeights;
            return new Vector3((h[x,y+1]-h[x+2,y+1])/(2*cell),1,(h[x+1,y+2]-h[x+1,y])/(2*cell)).normalized;
        }
    }
}
