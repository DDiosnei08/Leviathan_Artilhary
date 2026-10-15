using System;
using ProjectLeviathan.ProceduralTerrain;
using UnityEngine;

namespace ProjectLeviathan.Base
{
    [DefaultExecutionOrder(-200)]
    [ExecuteAlways]
    public sealed class BaseSpawner : MonoBehaviour
    {
        public ProceduralHeightSource heightSource;
        public GameObject basePrefab;
        public BaseSiteSettings settings = new BaseSiteSettings();
        public bool generateOnAwake = true;
        [Tooltip("Editor-only preview, hidden while the single runtime base is active.")]
        public GameObject previewInstance;
        public BaseSiteData previewSite;
        public Transform player;
        [Min(0)] public float activateDistance;
        public BaseSiteData Site { get; private set; }
        public GameObject Instance { get; private set; }
        public event Action<BaseSpawner> Generated;
        TerrainSettings originalSettings, ownedSettings;
        float nextCheck;
        int attemptedSeed;
        bool attempted;
        public Vector3 FoundationOffset {get;private set;}
        public void ConfigureFootprint()
        {
            if(basePrefab==null) return;
            foreach(var filter in basePrefab.GetComponentsInChildren<MeshFilter>(true))
            {
                if(filter.name!="Fosso_Piso" || filter.sharedMesh==null) continue;
                var meshBounds=filter.sharedMesh.bounds;var local=new Bounds();bool first=true;
                for(int i=0;i<8;i++)
                {
                    Vector3 corner=meshBounds.center+Vector3.Scale(meshBounds.extents,new Vector3((i&1)==0 ? -1 : 1,(i&2)==0 ? -1 : 1,(i&4)==0 ? -1 : 1));
                    Vector3 point=basePrefab.transform.InverseTransformPoint(filter.transform.TransformPoint(corner));
                    if(first) {local=new Bounds(point,Vector3.zero);first=false;}else local.Encapsulate(point);
                }
                FoundationOffset=new Vector3(local.center.x,0,local.center.z);
                float radius=Mathf.Max(local.extents.x,local.extents.z);
                settings.flatRadius=Mathf.Max(settings.flatRadius,radius+4);
                settings.checkRadius=Mathf.Max(settings.checkRadius,radius+7);
                settings.exclusionRadius=Mathf.Max(settings.exclusionRadius,radius+8);
                settings.carveRadius=Mathf.Max(.1f,radius-.25f);
                settings.blendRadius=0;break;
            }
        }
        public Vector3 RootPosition(BaseSiteData site) => new Vector3(site.center.x,site.groundHeight,site.center.y)-Quaternion.Euler(0,site.yaw,0)*FoundationOffset;
#if UNITY_EDITOR
        public bool RebuildEditorPreview()
        {
            if(Application.isPlaying || heightSource==null || heightSource.terrain==null || previewInstance==null) return false;
            var terrain=heightSource.terrain;var source=terrain.terrainSettings;if(source==null) return false;
            ConfigureFootprint();settings.seed=source.noiseSettings.seed;
            var generation=source.CaptureGenerationSettings(false);
            settings.waterLevel=generation.EvaluateHeight(generation.lakeThreshold)*generation.heightMultiplier;
            settings.maxAltitude=generation.heightMultiplier*2;settings.constrainToMap=terrain.useFiniteMap;
            int radius=source.finiteMapChunksPerSide/2;settings.minimumWorldPosition=Vector2.one*(-radius*source.chunkSize);
            settings.maximumWorldPosition=Vector2.one*((radius+1)*source.chunkSize);
            heightSource.ResetCache();var next=BaseSiteSolver.Solve(heightSource,settings);previewSite=next;
            source.RuntimeBaseSite=next;previewInstance.SetActive(next.valid);
            if(next.valid)
            {
                previewInstance.transform.SetPositionAndRotation(RootPosition(next),Quaternion.Euler(0,next.yaw,0));
                var chart=previewInstance.GetComponentInChildren<BasePhysicalMap>();if(chart!=null) chart.SetWorld(source,next);
                var assembly=previewInstance.GetComponent<BaseAssembly>();
                foreach(var rig in FindObjectsByType<BaseGameRig>(FindObjectsSortMode.None)) if(rig.spawner==this && assembly!=null)
                {
                    rig.player.transform.SetPositionAndRotation(assembly.playerSpawn.position,assembly.playerSpawn.rotation);
                    rig.overviewCamera.transform.position=previewInstance.transform.TransformPoint(new Vector3(-47,32,-45));
                    rig.overviewCamera.transform.LookAt(previewInstance.transform.position+Vector3.up*3);
                    terrain.viewer=rig.overviewCamera.transform;
                }
            }
            terrain.RegenerateTerrain();UnityEditor.EditorUtility.SetDirty(this);
            if(!next.valid) Debug.LogWarning("[BaseSpawner] No naturally supported dry site. Preview hidden; enlarge the search area.",this);
            return next.valid;
        }
#endif
#if UNITY_EDITOR
        [SerializeField,HideInInspector] TerrainSettings editorTerrainAsset;
        static int editorRestoreFrames;
        [UnityEditor.InitializeOnLoadMethod]
        static void RegisterEditorCleanup()
        {
            UnityEditor.EditorApplication.playModeStateChanged-=RestoreAfterPlay;
            UnityEditor.EditorApplication.playModeStateChanged+=RestoreAfterPlay;
        }
        static void RestoreAfterPlay(UnityEditor.PlayModeStateChange state)
        {
            if(state==UnityEditor.PlayModeStateChange.ExitingEditMode)
            {
                UnityEditor.EditorApplication.update-=CompleteEditorRestoration;
                editorRestoreFrames=0;
                foreach(var spawner in FindObjectsByType<BaseSpawner>())
                {
                    if(spawner.heightSource==null || spawner.heightSource.terrain==null) continue;
                    var asset=spawner.heightSource.terrain.terrainSettings;
                    if(asset==null || !UnityEditor.EditorUtility.IsPersistent(asset)) continue;
                    spawner.editorTerrainAsset=asset;
                    UnityEditor.SessionState.SetString(EditorTerrainKey(spawner),
                        UnityEditor.AssetDatabase.AssetPathToGUID(UnityEditor.AssetDatabase.GetAssetPath(asset)));
                }
                return;
            }
            if(state!=UnityEditor.PlayModeStateChange.EnteredEditMode) return;
            RestoreEditorReferences();
            editorRestoreFrames=3;
            UnityEditor.EditorApplication.update-=CompleteEditorRestoration;
            UnityEditor.EditorApplication.update+=CompleteEditorRestoration;
        }
        static void CompleteEditorRestoration()
        {
            if(!UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode) RestoreEditorReferences();
            if(--editorRestoreFrames<=0 || UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
                UnityEditor.EditorApplication.update-=CompleteEditorRestoration;
        }
        static string EditorTerrainKey(BaseSpawner spawner) => "Leviathan.BaseSpawner.TerrainAsset."+
            UnityEditor.GlobalObjectId.GetGlobalObjectIdSlow(spawner);
        static void RestoreEditorReferences()
        {
            foreach(var spawner in FindObjectsByType<BaseSpawner>(FindObjectsSortMode.None))
            {
                if(spawner.originalSettings!=null) spawner.CleanupRuntime();
                // No-scene-reload restoration can overwrite managed references after OnDisable.
                // A session GUID captured before Play survives both serialization and domain reload.
                string guid=UnityEditor.SessionState.GetString(EditorTerrainKey(spawner),string.Empty);
                if(!string.IsNullOrEmpty(guid))
                {
                    var asset=UnityEditor.AssetDatabase.LoadAssetAtPath<TerrainSettings>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
                    if(asset!=null) spawner.editorTerrainAsset=asset;
                }
                if(spawner.editorTerrainAsset!=null && spawner.heightSource!=null && spawner.heightSource.terrain!=null)
                {
                    spawner.heightSource.terrain.terrainSettings=spawner.editorTerrainAsset;
                    spawner.editorTerrainAsset.RuntimeBaseSite=spawner.previewSite;
                }
            }
        }
#endif
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void PreparePlaySession()
        {
            // ExecuteAlways callbacks are not guaranteed to run again with both reload options off.
            foreach(var spawner in FindObjectsByType<BaseSpawner>(FindObjectsSortMode.None))
                if(Application.IsPlaying(spawner.gameObject)) spawner.InitializeRuntime();
        }
        void Awake() => InitializeRuntime();
        void Start() => InitializeRuntime();
        void InitializeRuntime()
        {
            if (!Application.isPlaying || ownedSettings!=null) return;
#if UNITY_EDITOR
            // ExecuteAlways can receive an enable callback while the Editor is leaving Play.
            if(!UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode) return;
#endif
            if (heightSource != null && heightSource.terrain != null && heightSource.terrain.terrainSettings != null)
            {
                originalSettings=heightSource.terrain.terrainSettings;
#if UNITY_EDITOR
                if(UnityEditor.EditorUtility.IsPersistent(originalSettings)) editorTerrainAsset=originalSettings;
#endif
                ownedSettings=Instantiate(originalSettings); ownedSettings.name=originalSettings.name+" (Runtime)";
                ownedSettings.hideFlags=HideFlags.DontSave;
                heightSource.terrain.terrainSettings=ownedSettings;
            }
            if (previewInstance!=null) previewInstance.SetActive(false);
            if (generateOnAwake) Generate();
        }
        void OnEnable()
        {
            if (Application.isPlaying) { InitializeRuntime(); return; }
#if UNITY_EDITOR
            if(heightSource!=null && heightSource.terrain!=null && UnityEditor.EditorUtility.IsPersistent(heightSource.terrain.terrainSettings))
                editorTerrainAsset=heightSource.terrain.terrainSettings;
#endif
            if (!Application.isPlaying && heightSource!=null && heightSource.terrain!=null && heightSource.terrain.terrainSettings!=null)
                heightSource.terrain.terrainSettings.RuntimeBaseSite=previewSite;
        }
        [ContextMenu("Gerar base unica (Play)")]
        public void Generate()
        {
            if(!Application.isPlaying)
            { Debug.LogWarning("[BaseSpawner] Use Leviathan > Base Giratoria > Criar ou reconstruir cena for an editor preview.",this);return; }
            if (heightSource == null || heightSource.terrain == null || heightSource.terrain.terrainSettings == null || basePrefab == null)
            { Debug.LogError("[BaseSpawner] Configure terrain, height source and prefab.",this); return; }
            if (BaseSiteRegistry.HasSite && Instance==null && BaseSiteRegistry.Site.valid)
            {
                // Registry ownership also guards multiple spawners accidentally added to the scene.
                if (!BaseSiteRegistry.Set(BaseSiteRegistry.Site,this)) { Debug.LogError("[BaseSpawner] Another spawner already owns the base.",this); return; }
            }
            var terrain=heightSource.terrain;
            ConfigureFootprint();
            settings.seed=terrain.terrainSettings.noiseSettings.seed;
            settings.constrainToMap=terrain.useFiniteMap;
            int radius=terrain.terrainSettings.finiteMapChunksPerSide/2;
            settings.minimumWorldPosition=Vector2.one*(-radius*terrain.terrainSettings.chunkSize);
            settings.maximumWorldPosition=Vector2.one*((radius+1)*terrain.terrainSettings.chunkSize);
            attempted=true;attemptedSeed=settings.seed;
            var natural=terrain.terrainSettings.CaptureGenerationSettings(false);
            settings.waterLevel=natural.EvaluateHeight(natural.lakeThreshold)*natural.heightMultiplier;
            settings.maxAltitude=Mathf.Max(settings.waterLevel+100,natural.heightMultiplier*2);
            heightSource.ResetCache();
            BaseSiteData next=BaseSiteSolver.Solve(heightSource,settings);
            if (!next.valid)
            {
                BaseSiteRegistry.Clear(this);Site=default;terrain.terrainSettings.RuntimeBaseSite=default;
                if(Instance!=null) {Instance.SetActive(false);Destroy(Instance);Instance=null;}
                if(terrain.GeneratedChunkCount>0) terrain.RegenerateTerrain();
                Generated?.Invoke(this);
                Debug.LogError("[BaseSpawner] No dry base site was found. Adjust the search area or terrain settings.",this);
                return;
            }
            if (!BaseSiteRegistry.Set(next,this)) { Debug.LogError("[BaseSpawner] Another spawner already owns the base.",this); return; }
            bool same=Site.valid && Site.seed==next.seed && Site.center==next.center && Mathf.Approximately(Site.groundHeight,next.groundHeight) && Mathf.Approximately(Site.yaw,next.yaw);
            Site=next; terrain.terrainSettings.RuntimeBaseSite=Site;
            if (!same && Instance!=null)
            {
                Instance.SetActive(false);
                if (Application.isPlaying) Destroy(Instance); else DestroyImmediate(Instance);
                Instance=null;
            }
            if (Instance==null) Instance=Instantiate(basePrefab);
            Instance.name="BaseGiratoria_Unica";
            Instance.transform.SetPositionAndRotation(RootPosition(Site),Quaternion.Euler(0,Site.yaw,0));
            Instance.SetActive(true);
            var chart=Instance.GetComponentInChildren<BasePhysicalMap>();
            if(chart!=null) chart.SetWorld(terrain.terrainSettings,Site);
            if (terrain.GeneratedChunkCount>0) terrain.RegenerateTerrain();
            Generated?.Invoke(this);
        }
        void Update()
        {
            if (!Application.isPlaying) { if(originalSettings!=null) CleanupRuntime(); return; }
            // ExecuteAlways.Awake may already have run in Edit mode when scene reload is disabled.
            InitializeRuntime();
            if (ownedSettings != null && (!Site.valid || ownedSettings.noiseSettings.seed != Site.seed) &&
                (!attempted || attemptedSeed!=ownedSettings.noiseSettings.seed)) Generate();
            if (Instance==null || player==null || activateDistance<=0 || Time.time<nextCheck) return;
            nextCheck=Time.time+.5f;
            bool active=(player.position-Instance.transform.position).sqrMagnitude<activateDistance*activateDistance;
            if (Instance.activeSelf!=active) Instance.SetActive(active);
        }
        void OnDisable() { if(originalSettings!=null) CleanupRuntime(); }
        void OnDestroy() => CleanupRuntime();
        void CleanupRuntime()
        {
            BaseSiteRegistry.Clear(this);
            if (heightSource!=null && heightSource.terrain!=null && originalSettings!=null)
            {
                heightSource.terrain.terrainSettings=originalSettings;
                originalSettings.RuntimeBaseSite=previewSite;
            }
            if(Instance!=null)
            {
                Instance.SetActive(false);
                if(Application.isPlaying) Destroy(Instance);else DestroyImmediate(Instance);
            }
            if (ownedSettings!=null) { if(Application.isPlaying) Destroy(ownedSettings);else DestroyImmediate(ownedSettings); }
            Instance=null; ownedSettings=null;originalSettings=null;Site=default;attempted=false;
            if(previewInstance!=null) previewInstance.SetActive(true);
        }
        void OnDrawGizmosSelected()
        {
            if (!Site.valid) return;
            Vector3 center=new Vector3(Site.center.x,Site.groundHeight,Site.center.y);
            Gizmos.color=Color.cyan; Circle(center,Site.InfluenceRadius);
            Gizmos.color=Color.yellow; Circle(center,Site.flatRadius);
            Gizmos.color=Color.red; Circle(center,Site.carveRadius);
        }
        static void Circle(Vector3 center,float radius)
        {
            Vector3 previous=center+Vector3.right*radius;
            for(int i=1;i<=64;i++) { float a=i*Mathf.PI*2/64; Vector3 point=center+new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius); Gizmos.DrawLine(previous,point); previous=point; }
        }
    }
}
