using System.Collections.Generic;
using ProjectLeviathan.ProceduralTerrain;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectLeviathan.Vegetation
{
    [ExecuteAlways,DefaultExecutionOrder(170)]
    public sealed class WorldSceneryManager : MonoBehaviour
    {
        public TerrainWorldController terrain;
        public WorldSceneryConfiguration configuration;
        public Transform player;
        public int InstanceCount {get;private set;}
        public int VisibleCount {get;private set;}
        public ulong DistributionHash
        {
            get {ulong hash=0;foreach(var tile in tiles.Values) if(tile.instances!=null) foreach(var item in tile.instances)
                hash+=Hash(unchecked((uint)(Mathf.RoundToInt(item.position.x*100)*73856093^Mathf.RoundToInt(item.position.z*100)*19349663^item.prototype)));return hash;}
        }
        public Vector3 NearestRock(Vector3 from)
        {
            Vector3 result=from;float best=float.MaxValue;
            foreach(var tile in tiles.Values) if(tile.instances!=null) foreach(var item in tile.instances)
                if(configuration.prototypes[item.prototype].role==SceneryRole.Rock && (item.position-from).sqrMagnitude<best)
                {result=item.position;best=(item.position-from).sqrMagnitude;}
            return result;
        }
        readonly Dictionary<TerrainChunk,Tile> tiles=new Dictionary<TerrainChunk,Tile>();
        readonly Plane[] planes=new Plane[6];
        Matrix4x4[][] batches;
        int[] batchCounts;
        Material material;
        Material[] prototypeMaterials;
        readonly List<Material> ownedMaterials=new List<Material>();
        Transform colliderRoot;
        MeshCollider[] colliders;
        CollisionCandidate[] collisionCandidates;
        float nextCollisionUpdate;
        struct CollisionCandidate {public Instance instance;public float distanceSquared;}
        struct Instance {public Vector3 position;public Quaternion rotation;public float size;public int prototype;public Vector2 grid;}
        sealed class Tile {public TerrainChunk chunk;public bool dirty=true;public Instance[] instances;}

        void OnEnable()
        {
            if(terrain==null || configuration==null || configuration.material==null || configuration.prototypes==null) return;
            material=new Material(configuration.material) {enableInstancing=true,hideFlags=HideFlags.HideAndDontSave};
            ownedMaterials.Add(material);
            prototypeMaterials=new Material[configuration.prototypes.Length];
            for(int i=0;i<prototypeMaterials.Length;i++)
            {
                var source=configuration.prototypes[i]?.material;
                if(source==null || source==configuration.material) {prototypeMaterials[i]=material;continue;}
                for(int previous=0;previous<i;previous++)
                    if(configuration.prototypes[previous]?.material==source) {prototypeMaterials[i]=prototypeMaterials[previous];break;}
                if(prototypeMaterials[i]!=null) continue;
                prototypeMaterials[i]=new Material(source) {enableInstancing=true,hideFlags=HideFlags.HideAndDontSave};
                ownedMaterials.Add(prototypeMaterials[i]);
            }
            batches=new Matrix4x4[configuration.prototypes.Length*2][];batchCounts=new int[batches.Length];
            for(int i=0;i<batches.Length;i++) batches[i]=new Matrix4x4[1023];
            terrain.ChunkCreated+=Attach;foreach(var chunk in terrain.TerrainChunks.Values) Attach(chunk);
            RenderPipelineManager.beginCameraRendering+=Render;
        }
        void Attach(TerrainChunk chunk)
        {if(tiles.ContainsKey(chunk)) return;tiles.Add(chunk,new Tile {chunk=chunk});chunk.DataChanged+=Changed;chunk.Disposing+=Detach;}
        void Changed(TerrainChunk chunk) {if(tiles.TryGetValue(chunk,out var tile)) tile.dirty=true;}
        void Detach(TerrainChunk chunk) {chunk.DataChanged-=Changed;chunk.Disposing-=Detach;tiles.Remove(chunk);}
        void Update()
        {
            if(material==null) {OnEnable();return;}
            int budget=configuration.buildsPerFrame;InstanceCount=0;
            foreach(var tile in tiles.Values)
            {
                if(tile.dirty && budget>0 && tile.chunk.WorldFeaturesReady && tile.chunk.GeneratedData!=null) {Build(tile);budget--;}
                if(tile.instances!=null) InstanceCount+=tile.instances.Length;
            }
            if(Application.IsPlaying(gameObject) && Time.time>=nextCollisionUpdate) UpdateColliders();
        }
        void UpdateColliders()
        {
            nextCollisionUpdate=Time.time+.5f;
            if(player==null || configuration.collisionDistance<=0 || configuration.maximumColliders<=0)
            {
                DisableColliders();
                return;
            }
            int capacity=Mathf.Clamp(configuration.maximumColliders,1,128);
            if(colliders!=null && colliders.Length!=capacity)
            {
                DisableColliders();
                Destroy(colliderRoot.gameObject);
                colliderRoot=null;
                colliders=null;
            }
            if(colliderRoot==null)
            {
                colliderRoot=new GameObject("Colisoes de Cenario").transform;colliderRoot.SetParent(transform,false);
                colliderRoot.gameObject.hideFlags=HideFlags.DontSave;colliders=new MeshCollider[capacity];
                collisionCandidates=new CollisionCandidate[capacity];
                for(int i=0;i<colliders.Length;i++)
                {var go=new GameObject("Collider "+i);go.transform.SetParent(colliderRoot,false);colliders[i]=go.AddComponent<MeshCollider>();colliders[i].enabled=false;}
            }
            int count=0;float radius=configuration.collisionDistance*configuration.collisionDistance;
            // Keep the nearest bounded set, independently of chunk/dictionary traversal order.
            foreach(var tile in tiles.Values) if(tile.instances!=null && tile.chunk.GameObject!=null && tile.chunk.GameObject.activeInHierarchy) foreach(var item in tile.instances)
            {
                float distance=(item.position-player.position).sqrMagnitude;
                if(distance>radius || (count==capacity && distance>=collisionCandidates[count-1].distanceSquared)) continue;
                int insert=Mathf.Min(count,capacity-1);
                while(insert>0 && distance<collisionCandidates[insert-1].distanceSquared)
                {
                    collisionCandidates[insert]=collisionCandidates[insert-1];
                    insert--;
                }
                collisionCandidates[insert]=new CollisionCandidate {instance=item,distanceSquared=distance};
                count=Mathf.Min(count+1,capacity);
            }
            for(int i=0;i<count;i++)
            {
                var item=collisionCandidates[i].instance;
                var collider=colliders[i];
                Mesh mesh=configuration.prototypes[item.prototype].mesh;
                Vector3 scale=Vector3.one*item.size;
                if(collider.enabled && collider.sharedMesh==mesh && collider.transform.position==item.position &&
                    collider.transform.rotation==item.rotation && collider.transform.localScale==scale) continue;
                collider.enabled=false;
                if(collider.sharedMesh!=mesh) collider.sharedMesh=mesh;
                collider.transform.SetPositionAndRotation(item.position,item.rotation);
                collider.transform.localScale=scale;
                collider.enabled=true;
            }
            for(int i=count;i<colliders.Length;i++) colliders[i].enabled=false;
        }
        void DisableColliders()
        {if(colliders!=null) foreach(var collider in colliders) if(collider!=null) collider.enabled=false;}
        void Build(Tile tile)
        {
            var c=configuration;var chunk=tile.chunk;var map=chunk.GeneratedData;float extent=chunk.Settings.chunkSize;
            var instances=new List<Instance>(c.maximumInstancesPerChunk);
            int cells=Mathf.Max(1,Mathf.FloorToInt(extent/c.rockSpacing));float spacing=extent/cells;
            for(int z=0;z<cells;z++) for(int x=0;x<cells;x++)
            {
                uint hash=CellHash(chunk,x,z,cells);
                float px=(x+.15f+Random(hash+1)*.7f)*spacing,pz=(z+.15f+Random(hash+2)*.7f)*spacing;
                Vector3 world=chunk.GameObject.transform.TransformPoint(new Vector3(px,0,pz));
                float cluster=Mathf.PerlinNoise(world.x/c.clusterScale+chunk.Settings.noiseSettings.seed%7919,world.z/c.clusterScale+123);
                float density=c.rockDensity*Mathf.Lerp(.12f,1,Mathf.SmoothStep(0,1,Mathf.InverseLerp(.35f,.7f,cluster)));
                if(Random(hash)>density) continue;
                Vector2 grid=new Vector2(px/extent*(map.Width-1),(1-pz/extent)*(map.Height-1));
                if(!Valid(map,grid,extent,c.maximumRockSlope,5,true)) continue;
                int prototype=Pick(SceneryRole.Rock,hash);if(prototype<0) continue;
                Add(instances,chunk,grid,prototype,hash,Quaternion.Euler(Random(hash+5)*12,Random(hash+6)*360,Random(hash+7)*12));
            }
            // Understory follows broad world-space forest patches, with occasional fallen timber.
            for(int z=0;z<cells;z++) for(int x=0;x<cells;x++)
            {
                uint hash=CellHash(chunk,x,z,cells)^0x3ea924u;
                float px=(x+.1f+Random(hash+1)*.8f)*spacing,pz=(z+.1f+Random(hash+2)*.8f)*spacing;
                Vector3 world=chunk.GameObject.transform.TransformPoint(new Vector3(px,0,pz));
                float grove=Mathf.PerlinNoise(world.x/220+chunk.Settings.noiseSettings.seed%10000,world.z/220+911);
                if(grove<.43f || Random(hash)>.5f) continue;
                Vector2 grid=new Vector2(px/extent*(map.Width-1),(1-pz/extent)*(map.Height-1));
                if(!Valid(map,grid,extent,7,8,true)) continue;
                int prototype=Pick(Random(hash+4)<.12f ? SceneryRole.FallenLog : SceneryRole.Shrub,hash);
                if(prototype>=0) Add(instances,chunk,grid,prototype,hash,Quaternion.Euler(0,Random(hash+6)*360,0));
            }
            if(c.roadsideFurniture)
            {
                int step=Mathf.Max(3,Mathf.RoundToInt(c.roadsideSpacing/chunk.Settings.CellSize));
                for(int y=step;y<map.Height-step;y+=step) for(int x=step;x<map.Width-step;x+=step)
                {
                    if(map.Roads[x,y]<.1f) continue;
                    uint hash=CellHash(chunk,x,y,map.Width);
                    // Road-mask gradients identify an outward offset, leaving the carriageway clear.
                    Vector2 outward=new Vector2(map.Roads[x-1,y]-map.Roads[x+1,y],map.Roads[x,y-1]-map.Roads[x,y+1]);
                    if(outward.sqrMagnitude<.001f) outward=new Vector2(1,0);
                    outward.Normalize();Vector2 grid=new Vector2(x,y)+outward*(12/chunk.Settings.CellSize);
                    if(!Valid(map,grid,extent,7,12,true)) continue;
                    int prototype=Pick(Random(hash)>.25f ? SceneryRole.Lamp : SceneryRole.Barrier,hash);
                    if(prototype<0) continue;
                    Add(instances,chunk,grid,prototype,hash,Quaternion.Euler(0,Mathf.Atan2(outward.x,-outward.y)*Mathf.Rad2Deg+90,0));
                }
            }
            // A small service perimeter is tied to the single base, not a second base prefab.
            var site=map.BaseSite;
            if(site.valid)
            {
                for(int i=0;i<12;i++)
                {
                    float angle=(site.yaw+i*30)*Mathf.Deg2Rad;
                    Vector2 world=site.center+new Vector2(Mathf.Sin(angle),Mathf.Cos(angle))*46;
                    Vector2 grid=new Vector2((world.x-map.WorldOrigin.x)/extent*(map.Width-1),(1-(world.y-map.WorldOrigin.y)/extent)*(map.Height-1));
                    if(grid.x<1 || grid.y<1 || grid.x>map.Width-2 || grid.y>map.Height-2 || !Valid(map,grid,extent,5,5,true)) continue;
                    uint hash=Hash(unchecked((uint)(site.seed+i*9173)));
                    int prototype=Pick(i%3==0 ? SceneryRole.Lamp : i%3==1 ? SceneryRole.Barrier : SceneryRole.Crate,hash);
                    if(prototype>=0) Add(instances,chunk,grid,prototype,hash,Quaternion.Euler(0,site.yaw+i*30,0));
                }
            }
            tile.instances=instances.ToArray();tile.dirty=false;
        }
        static bool Valid(HeightMap map,Vector2 grid,float extent,float slope,float margin,bool avoidRoads)
        {
            int x=Mathf.RoundToInt(grid.x),y=Mathf.RoundToInt(grid.y);
            if(x<1 || y<1 || x>=map.Width-1 || y>=map.Height-1) return false;
            if(map.Slopes[x,y]>slope || map.Water[x,y]>.01f || map.PoiMask[x,y]>.01f || (avoidRoads && map.Roads[x,y]>.01f)) return false;
            return !map.BaseSite.IsExcluded(map.WorldOrigin.x+grid.x/(map.Width-1)*extent,map.WorldOrigin.y+(1-grid.y/(map.Height-1))*extent,margin);
        }
        int Pick(SceneryRole role,uint hash)
        {
            int count=0;foreach(var p in configuration.prototypes) if(p!=null && p.role==role && p.mesh!=null) count++;
            if(count==0) return -1;int selected=(int)(hash%(uint)count);
            for(int i=0;i<configuration.prototypes.Length;i++) if(configuration.prototypes[i]!=null && configuration.prototypes[i].role==role && configuration.prototypes[i].mesh!=null && selected--==0) return i;
            return -1;
        }
        void Add(List<Instance> instances,TerrainChunk chunk,Vector2 grid,int prototype,uint hash,Quaternion rotation)
        {
            if(instances.Count>=configuration.maximumInstancesPerChunk) return;
            var map=chunk.GeneratedData;float size=Mathf.Lerp(configuration.prototypes[prototype].sizeRange.x,configuration.prototypes[prototype].sizeRange.y,Random(hash+9));
            Vector3 local=new Vector3(grid.x/(map.Width-1)*chunk.Settings.chunkSize,0,(1-grid.y/(map.Height-1))*chunk.Settings.chunkSize);
            local.y=Height(map,grid,Stride(chunk))-(configuration.prototypes[prototype].role==SceneryRole.Rock ? size*.08f : .01f);
            instances.Add(new Instance {position=chunk.GameObject.transform.TransformPoint(local),rotation=rotation,size=size,prototype=prototype,grid=grid});
        }
        static int Stride(TerrainChunk chunk) {foreach(var lod in chunk.Settings.lods) if(lod.lod==chunk.AppliedLod) return lod.GetSimplificationLevel();return 1;}
        static float Height(HeightMap map,Vector2 grid,int stride)
        {
            int x=Mathf.Min((int)(grid.x/stride)*stride,map.Width-2),y=Mathf.Min((int)(grid.y/stride)*stride,map.Height-2);
            int xx=Mathf.Min(x+stride,map.Width-1),yy=Mathf.Min(y+stride,map.Height-1);float u=(grid.x-x)/(xx-x),v=(grid.y-y)/(yy-y);var h=map.WorldHeights;
            return u+v<=1 ? h[x,y]*(1-u-v)+h[xx,y]*u+h[x,yy]*v : h[xx,yy]*(u+v-1)+h[xx,y]*(1-v)+h[x,yy]*(1-u);
        }
        public void RenderForValidation(Camera camera) => Render(default,camera);
        public int ValidateForValidation(out float maximumRootError,out int[] roles)
        {
            int invalid=0;maximumRootError=0;roles=new int[6];
            foreach(var tile in tiles.Values) if(tile.instances!=null) foreach(var item in tile.instances)
            {
                var role=configuration.prototypes[item.prototype].role;roles[(int)role]++;
                var map=tile.chunk.GeneratedData;if(!Valid(map,item.grid,tile.chunk.Settings.chunkSize,role==SceneryRole.Rock ? configuration.maximumRockSlope : 7,5,true)) invalid++;
                float root=Height(map,item.grid,Stride(tile.chunk))-(role==SceneryRole.Rock ? item.size*.08f : .01f);
                maximumRootError=Mathf.Max(maximumRootError,Mathf.Abs(tile.chunk.GameObject.transform.InverseTransformPoint(item.position).y-root));
            }
            return invalid;
        }
        void Render(ScriptableRenderContext context,Camera camera)
        {
            if(material==null || camera==null || !SystemInfo.supportsInstancing || (camera.cameraType!=CameraType.Game && camera.cameraType!=CameraType.SceneView)) return;
            GeometryUtility.CalculateFrustumPlanes(camera,planes);System.Array.Clear(batchCounts,0,batchCounts.Length);VisibleCount=0;
            foreach(var tile in tiles.Values) if(tile.instances!=null && tile.chunk.GameObject!=null && tile.chunk.GameObject.activeInHierarchy)
                foreach(var item in tile.instances)
                {
                    float distance=(item.position-camera.transform.position).magnitude;if(distance>configuration.drawDistance) continue;
                    bool visible=true;for(int p=0;p<6;p++) if(planes[p].GetDistanceToPoint(item.position+Vector3.up*item.size*.5f)<-item.size) {visible=false;break;}
                    if(!visible) continue;int group=item.prototype*2+(distance<configuration.shadowDistance ? 1 : 0);
                    batches[group][batchCounts[group]++]=Matrix4x4.TRS(item.position,item.rotation,Vector3.one*item.size);VisibleCount++;
                    if(batchCounts[group]==1023) Flush(group,camera);
                }
            for(int group=0;group<batchCounts.Length;group++) Flush(group,camera);
        }
        void Flush(int group,Camera camera)
        {
            int count=batchCounts[group];if(count==0) return;
            Graphics.DrawMeshInstanced(configuration.prototypes[group/2].mesh,0,prototypeMaterials[group/2],batches[group],count,null,
                group%2==1 ? ShadowCastingMode.On : ShadowCastingMode.Off,true,gameObject.layer,camera,LightProbeUsage.Off);batchCounts[group]=0;
        }
        static uint CellHash(TerrainChunk chunk,int x,int y,int cells) => Hash(unchecked((uint)(chunk.Settings.noiseSettings.seed^(chunk.Coordinate.x*cells+x)*73856093^(chunk.Coordinate.y*cells+y)*19349663)));
        static uint Hash(uint x) {unchecked {x^=x>>16;x*=0x7feb352d;x^=x>>15;x*=0x846ca68b;return x^(x>>16);}}
        static float Random(uint x) => (Hash(x)&0xffffff)/16777216f;
        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering-=Render;if(terrain!=null) terrain.ChunkCreated-=Attach;
            foreach(var tile in tiles.Values) {tile.chunk.DataChanged-=Changed;tile.chunk.Disposing-=Detach;}
            tiles.Clear();InstanceCount=VisibleCount=0;
            DisableColliders();
            if(colliderRoot!=null) {if(Application.IsPlaying(gameObject)) Destroy(colliderRoot.gameObject);else DestroyImmediate(colliderRoot.gameObject);colliderRoot=null;colliders=null;}
            foreach(var owned in ownedMaterials) if(owned!=null) {if(Application.isPlaying) Destroy(owned);else DestroyImmediate(owned);}
            ownedMaterials.Clear();material=null;prototypeMaterials=null;
            batches=null;batchCounts=null;collisionCandidates=null;
        }
        void OnDestroy() => OnDisable();
    }
}
