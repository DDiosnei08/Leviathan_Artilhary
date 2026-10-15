using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectLeviathan.ProceduralTerrain
{
    public static class RoadsideInfrastructure
    {
        public static void Build(TerrainChunk chunk, Transform root)
        {
            var settings=chunk.Settings;var map=chunk.GeneratedData;
            if(settings.roadsidePoleMesh==null || settings.roadsidePoleMaterial==null) return;
            var poles=new List<CombineInstance>();var wires=new List<Vector3>();var indices=new List<int>();
            var occupied=new List<Vector3>();int stride=1;
            foreach(var lod in settings.lods)if(lod.lod==chunk.AppliedLod)stride=lod.GetSimplificationLevel();
            foreach(var path in TerrainRoadMeshGenerator.ExtractPaths(map,settings))
            {
                float travelled=0;Vector3? previous=null;
                for(int i=1;i<path.Count;i++)
                {
                    travelled+=Vector3.Distance(path[i],path[i-1]);if(travelled<65)continue;travelled=0;
                    if(poles.Count>=40)break;
                    Vector3 tangent=(path[i]-path[i-1]).normalized;
                    Vector3 seed=path[i]+Vector3.Cross(Vector3.up,tangent)*10;
                    if(!TerrainRoadMeshGenerator.SampleSurface(map,settings,stride,seed,out Vector3 p,out Vector3 normal) || normal.y<.99f ||
                        map.BaseSite.IsExcluded(map.WorldOrigin.x+p.x,map.WorldOrigin.y+p.z,12)) {previous=null;continue;}
                    int mx=Mathf.Clamp(Mathf.RoundToInt(p.x/settings.CellSize),0,map.Width-1),my=Mathf.Clamp(Mathf.RoundToInt((settings.chunkSize-p.z)/settings.CellSize),0,map.Height-1);
                    if(map.PoiMask[mx,my]>.01f || map.Roads[mx,my]>.01f){previous=null;continue;}
                    bool crowded=false;foreach(var used in occupied)if((used-p).sqrMagnitude<45*45){crowded=true;break;}
                    if(crowded){previous=null;continue;}
                    occupied.Add(p);
                    Quaternion rotation=Quaternion.LookRotation(tangent);
                    poles.Add(new CombineInstance{mesh=settings.roadsidePoleMesh,transform=Matrix4x4.TRS(p,rotation,Vector3.one*9)});
                    Vector3 top=p+Vector3.up*8.5f;
                    bool clear=true;
                    if(previous.HasValue)for(int j=1;j<10;j++)
                    {
                        Vector3 probe=Vector3.Lerp(previous.Value,top,j/10f);
                        int x=Mathf.Clamp(Mathf.RoundToInt(probe.x/settings.CellSize),0,map.Width-1),y=Mathf.Clamp(Mathf.RoundToInt((settings.chunkSize-probe.z)/settings.CellSize),0,map.Height-1);
                        if(map.PoiMask[x,y]>.01f)clear=false;
                    }
                    if(clear && previous.HasValue && Vector3.Distance(previous.Value,top)<110)
                    {
                        Vector3 last=previous.Value;
                        for(int j=1;j<=10;j++)
                        {float t=j/10f;Vector3 next=Vector3.Lerp(previous.Value,top,t)-Vector3.up*(4*t*(1-t)*2);Cable(last,next,wires,indices);last=next;}
                    }
                    previous=top;
                }
            }
            if(poles.Count>0)
            {
                var mesh=new Mesh{name="Roadside poles",indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(poles.ToArray());
                Add(root,mesh,settings.roadsidePoleMaterial);
            }
            if(wires.Count>0 && settings.roadsideCableMaterial!=null)
            {
                var mesh=new Mesh{name="Suspended utility cables",indexFormat=IndexFormat.UInt32};mesh.SetVertices(wires);mesh.SetTriangles(indices,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
                Add(root,mesh,settings.roadsideCableMaterial);
            }
        }
        static void Cable(Vector3 a,Vector3 b,List<Vector3> vertices,List<int> triangles)
        {
            Vector3 direction=(b-a).normalized,right=Vector3.Cross(direction,Vector3.up).normalized*.055f;
            Vector3 up=Vector3.Cross(right.normalized,direction)*.055f;int start=vertices.Count;
            for(int j=0;j<2;j++)for(int i=0;i<4;i++){float angle=i*Mathf.PI*.5f;vertices.Add((j==0?a:b)+right*Mathf.Cos(angle)+up*Mathf.Sin(angle));}
            for(int i=0;i<4;i++){int k=(i+1)%4;triangles.AddRange(new[]{start+i,start+4+i,start+k,start+k,start+4+i,start+4+k});}
        }
        static void Add(Transform root,Mesh mesh,Material material)
        {
            var go=new GameObject(mesh.name);go.transform.SetParent(root,false);
            go.AddComponent<MeshFilter>().sharedMesh=mesh;var r=go.AddComponent<MeshRenderer>();r.sharedMaterial=material;r.shadowCastingMode=ShadowCastingMode.Off;
            go.AddComponent<GeneratedSurfaceMeshOwner>().mesh=mesh;
        }
    }
}
