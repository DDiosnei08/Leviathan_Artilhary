using System.Collections.Generic;
using ProjectLeviathan.ProceduralTerrain;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectLeviathan.Vegetation
{
    [ExecuteAlways,DefaultExecutionOrder(160)]
    public sealed class ProceduralTreeManager : MonoBehaviour
    {
        public TerrainWorldController terrain;
        public TreeConfiguration configuration;
        public int TreeCount {get;private set;}
        public int VisibleTrees {get;private set;}
        readonly Dictionary<TerrainChunk,Tile> tiles=new Dictionary<TerrainChunk,Tile>();
        readonly Plane[] planes=new Plane[6];
        readonly Matrix4x4[][] batches=CreateBatches();
        readonly int[] counts=new int[8];
        Material material;
        struct Tree {public Vector3 position;public float height,yaw,width;public int species;}
        sealed class Tile {public TerrainChunk chunk;public Tree[] trees;public bool dirty=true;}
        static Matrix4x4[][] CreateBatches()
        {var result=new Matrix4x4[8][];for(int i=0;i<result.Length;i++) result[i]=new Matrix4x4[1023];return result;}
        public ulong DistributionHash
        {
            get {ulong result=0;foreach(var tile in tiles.Values) if(tile.trees!=null) foreach(var tree in tile.trees)
                result+=Hash(unchecked((uint)(Mathf.RoundToInt(tree.position.x*100)*73856093^Mathf.RoundToInt(tree.position.z*100)*19349663)));return result;}
        }
        public Vector3 FirstTreePosition
        {
            get {foreach(var tile in tiles.Values) if(tile.trees!=null && tile.trees.Length>0) return tile.trees[0].position;return Vector3.zero;}
        }
        void OnEnable()
        {
            if(terrain==null || configuration==null || configuration.material==null) return;
            material=new Material(configuration.material) {enableInstancing=true,hideFlags=HideFlags.HideAndDontSave};
            terrain.ChunkCreated+=Attach;
            foreach(var chunk in terrain.TerrainChunks.Values) Attach(chunk);
            RenderPipelineManager.beginCameraRendering+=RenderCamera;
        }
        void Attach(TerrainChunk chunk)
        {
            if(tiles.ContainsKey(chunk)) return;
            tiles.Add(chunk,new Tile {chunk=chunk});chunk.DataChanged+=Changed;chunk.Disposing+=Detach;
        }
        void Changed(TerrainChunk chunk) {if(tiles.TryGetValue(chunk,out var tile)) tile.dirty=true;}
        void Detach(TerrainChunk chunk)
        {chunk.DataChanged-=Changed;chunk.Disposing-=Detach;tiles.Remove(chunk);}
        void Update()
        {
            if(material==null) {if(terrain!=null && configuration!=null && configuration.material!=null) OnEnable();return;}
            int budget=configuration.buildsPerFrame;TreeCount=0;
            foreach(var tile in tiles.Values)
            {
                if(tile.dirty && budget>0 && tile.chunk.GeneratedData!=null && tile.chunk.WorldFeaturesReady)
                {Build(tile);budget--;}
                if(tile.trees!=null) TreeCount+=tile.trees.Length;
            }
        }
        void Build(Tile tile)
        {
            var chunk=tile.chunk;var map=chunk.GeneratedData;var settings=chunk.Settings;var c=configuration;
            int cells=Mathf.Max(1,Mathf.FloorToInt(settings.chunkSize/c.spacing));float spacing=settings.chunkSize/cells;
            var result=new List<Tree>(Mathf.Min(c.maximumTreesPerChunk,cells*cells));
            int stride=1;foreach(var lod in settings.lods) if(lod.lod==chunk.AppliedLod) stride=lod.GetSimplificationLevel();
            int accepted=0;
            for(int z=0;z<cells;z++) for(int x=0;x<cells;x++)
            {
                uint hash=Hash(unchecked((uint)(settings.noiseSettings.seed^(chunk.Coordinate.x*cells+x)*73856093^(chunk.Coordinate.y*cells+z)*19349663)));
                if(Random(hash)>c.density) continue;
                float px=(x+.15f+Random(hash+1)*.7f)*spacing,pz=(z+.15f+Random(hash+2)*.7f)*spacing;
                Vector3 world=chunk.GameObject.transform.TransformPoint(new Vector3(px,0,pz));
                float grove=Mathf.PerlinNoise(world.x/c.groveScale+settings.noiseSettings.seed%10000,world.z/c.groveScale+911);
                if(grove<c.groveThreshold) continue;
                float gx=px/settings.chunkSize*(map.Width-1),gy=(1-pz/settings.chunkSize)*(map.Height-1);
                int mx=Mathf.Clamp(Mathf.RoundToInt(gx),0,map.Width-1),my=Mathf.Clamp(Mathf.RoundToInt(gy),0,map.Height-1);
                if(map.Values[mx,my]<c.normalizedAltitude.x || map.Values[mx,my]>c.normalizedAltitude.y ||
                    map.Slopes[mx,my]>c.maximumSlope || map.Water[mx,my]>.01f ||map.Roads[mx,my]>.01f ||map.PoiMask[mx,my]>.01f ||
                    map.BaseSite.IsExcluded(world.x,world.z,8)) continue;
                bool blocked=false;
                for(int dz=0;dz<=1;dz++) for(int dx=0;dx<=1;dx++)
                {
                    int sx=Mathf.Clamp(Mathf.FloorToInt(gx)+dx,0,map.Width-1),sy=Mathf.Clamp(Mathf.FloorToInt(gy)+dz,0,map.Height-1);
                    if(map.Water[sx,sy]>.01f || map.Roads[sx,sy]>.01f || map.PoiMask[sx,sy]>.01f) blocked=true;
                }
                if(blocked) continue;
                world.y=SurfaceHeight(map,gx,gy,stride)+chunk.GameObject.transform.position.y;
                var tree=new Tree {position=world,height=Mathf.Lerp(c.heights.x,c.heights.y,Random(hash+3)),
                    yaw=Random(hash+4)*360,width=Mathf.Lerp(.85f,1.15f,Random(hash+5)),species=Random(hash+6)>.6f ? 1 : 0};
                accepted++;
                // Reservoir sampling preserves coverage across the whole chunk when capped.
                if(result.Count<c.maximumTreesPerChunk) result.Add(tree);
                else {int slot=(int)(Random(hash+7)*accepted);if(slot<result.Count) result[slot]=tree;}
            }
            tile.trees=result.ToArray();tile.dirty=false;
        }
        static float SurfaceHeight(HeightMap map,float x,float y,int stride)
        {
            int x0=Mathf.Min((int)(x/stride)*stride,map.Width-2),y0=Mathf.Min((int)(y/stride)*stride,map.Height-2);
            int x1=Mathf.Min(x0+stride,map.Width-1),y1=Mathf.Min(y0+stride,map.Height-1);
            float fx=(x-x0)/(x1-x0),fy=(y-y0)/(y1-y0);var h=map.WorldHeights;
            return fx+fy<=1 ? h[x0,y0]*(1-fx-fy)+h[x1,y0]*fx+h[x0,y1]*fy : h[x1,y1]*(fx+fy-1)+h[x1,y0]*(1-fy)+h[x0,y1]*(1-fx);
        }
        public void RenderForValidation(Camera camera) => RenderCamera(default,camera);
        public int ValidatePlacementForValidation(out float maximumRootError)
        {
            int invalid=0;maximumRootError=0;
            foreach(var tile in tiles.Values)
            {
                if(tile.trees==null || tile.chunk.GeneratedData==null) continue;
                var chunk=tile.chunk;var map=chunk.GeneratedData;int stride=1;
                foreach(var lod in chunk.Settings.lods) if(lod.lod==chunk.AppliedLod) stride=lod.GetSimplificationLevel();
                foreach(var tree in tile.trees)
                {
                    Vector3 local=chunk.GameObject.transform.InverseTransformPoint(tree.position);
                    float x=local.x/chunk.Settings.chunkSize*(map.Width-1),y=(1-local.z/chunk.Settings.chunkSize)*(map.Height-1);
                    int mx=Mathf.Clamp(Mathf.RoundToInt(x),0,map.Width-1),my=Mathf.Clamp(Mathf.RoundToInt(y),0,map.Height-1);
                    if(map.Water[mx,my]>.01f || map.Roads[mx,my]>.01f || map.PoiMask[mx,my]>.01f ||
                        map.BaseSite.IsExcluded(tree.position.x,tree.position.z,8)) invalid++;
                    maximumRootError=Mathf.Max(maximumRootError,Mathf.Abs(local.y-SurfaceHeight(map,x,y,stride)));
                }
            }
            return invalid;
        }
        void RenderCamera(ScriptableRenderContext context,Camera camera)
        {
            if(material==null || camera==null || !SystemInfo.supportsInstancing ||
                (camera.cameraType!=CameraType.Game && camera.cameraType!=CameraType.SceneView)) return;
            var c=configuration;Vector2 wind=c.windDirection.normalized;
            material.SetVector("_TreeWind",new Vector4(wind.x,wind.y,c.windStrength,1));
            GeometryUtility.CalculateFrustumPlanes(camera,planes);System.Array.Clear(counts,0,counts.Length);VisibleTrees=0;
            foreach(var tile in tiles.Values)
            {
                if(tile.trees==null || tile.chunk.GameObject==null || !tile.chunk.GameObject.activeInHierarchy) continue;
                foreach(var tree in tile.trees)
                {
                    float distance=(tree.position-camera.transform.position).magnitude;
                    if(distance>c.farDistance) continue;
                    bool visible=true;Vector3 center=tree.position+Vector3.up*tree.height*.5f;
                    for(int p=0;p<6;p++) if(planes[p].GetDistanceToPoint(center)<-tree.height) {visible=false;break;}
                    if(!visible) continue;
                    int group=(tree.species*2+(distance>c.nearDistance ? 1 : 0))*2+(distance<=c.shadowDistance ? 1 : 0);
                    batches[group][counts[group]++]=Matrix4x4.TRS(tree.position,Quaternion.Euler(0,tree.yaw,0),new Vector3(tree.height*tree.width,tree.height,tree.height*tree.width));
                    VisibleTrees++;if(counts[group]==1023) Flush(group,camera);
                }
            }
            for(int group=0;group<counts.Length;group++) Flush(group,camera);
        }
        void Flush(int group,Camera camera)
        {
            int count=counts[group];if(count==0) return;
            int meshIndex=group/2;
            Mesh mesh=meshIndex==0 ? configuration.broadleafNear : meshIndex==1 ? configuration.broadleafFar : meshIndex==2 ? configuration.pineNear : configuration.pineFar;
            if(mesh!=null) Graphics.DrawMeshInstanced(mesh,0,material,batches[group],count,null,
                group%2==1 ? ShadowCastingMode.On : ShadowCastingMode.Off,true,gameObject.layer,camera,LightProbeUsage.Off);
            counts[group]=0;
        }
        static uint Hash(uint value) {unchecked {value^=value>>16;value*=0x7feb352d;value^=value>>15;value*=0x846ca68b;return value^(value>>16);}}
        static float Random(uint value) => (Hash(value)&0x00ffffff)/16777216f;
        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering-=RenderCamera;
            if(terrain!=null) terrain.ChunkCreated-=Attach;
            foreach(var tile in tiles.Values) {tile.chunk.DataChanged-=Changed;tile.chunk.Disposing-=Detach;}
            tiles.Clear();TreeCount=VisibleTrees=0;
            if(material!=null) {if(Application.isPlaying) Destroy(material);else DestroyImmediate(material);material=null;}
        }
        void OnDestroy() => OnDisable();
    }
}
