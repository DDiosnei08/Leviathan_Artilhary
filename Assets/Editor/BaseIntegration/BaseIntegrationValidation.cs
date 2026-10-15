using System;
using System.IO;
using System.Text;
using ProjectLeviathan.Base;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

public static class BaseIntegrationValidation
{
    const string Key="Leviathan.BaseIntegration.Validation";
    static int step;
    static double started, changed;
    static bool failed;
    static BaseGameRig rig;
    static BaseSpawner spawner;
    static BaseAssembly assembly;
    static int originalSeed, leftStock, rightStock;
    static BaseSiteData initialSite;
    static StringBuilder report;
    static int siteChanges;
    sealed class FlatSource : IHeightSource
    {
        readonly float height;
        public FlatSource(float height) { this.height=height; }
        public float GetBaseHeight(float x,float z) => height;
    }
    static void SiteChanged(BaseSiteData data) => siteChanges++;
    [MenuItem("Leviathan/Base Giratoria/Validar integracao em Play")]
    public static void ValidateBatch()
    {
        EditorSceneManager.OpenScene(BaseIntegrationBuilder.ScenePath);
        SessionState.SetBool(Key,true); Resume(); EditorApplication.EnterPlaymode();
        EditorApplication.isPaused=false;
    }
    [InitializeOnLoadMethod]
    static void Resume()
    {
        if(!SessionState.GetBool(Key,false)) return;
        step=0;started=changed=EditorApplication.timeSinceStartup;failed=false;report=new StringBuilder();
        EditorApplication.update-=Tick;EditorApplication.update+=Tick;
        Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;
    }
    static void Log(string message,string trace,LogType type)
    {
        if(trace.Contains("UnityEditor.Search.")) return;
        if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert) {failed=true;report.AppendLine("ERROR "+message);}
    }
    static void Check(bool condition,string label)
    { if(!condition) throw new InvalidOperationException(label); report.AppendLine("PASS "+label); }
    static void Tick()
    {
        try
        {
            double now=EditorApplication.timeSinceStartup;
            if(now-started>150) throw new InvalidOperationException("Play validation timeout at step "+step);
            if(!EditorApplication.isPlaying)
            {
                if(step==13 && !EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    // Teardown time is not Edit-mode settling time. Allow delayed restoration
                    // after Unity reapplies serialized fields with scene reload disabled.
                    step=15;changed=now;
                }
                else if(step==15 && now-changed>1)
                {
                    rig=Object.FindAnyObjectByType<BaseGameRig>();spawner=rig.spawner;
                    Check(spawner.previewInstance!=null && spawner.previewInstance.activeSelf,"Exit Play restores the editor preview");
                    var restored=rig.terrain.terrainSettings;
                    Check(restored!=null && !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(restored)),"Exit Play restores the terrain asset, not a runtime clone");
                    step=14;changed=now;EditorApplication.EnterPlaymode();
                }
                return;
            }
            Application.runInBackground=true;
            if(step==0 && rig==null) Debug.Log("BASE_VALIDATION_PLAY_ENTERED");
            if(step==0)
            {
                rig=Object.FindAnyObjectByType<BaseGameRig>(); spawner=rig.spawner;
                if(spawner.Instance==null) return;
                assembly=spawner.Instance.GetComponent<BaseAssembly>(); originalSeed=spawner.Site.seed;initialSite=spawner.Site;
                siteChanges=0;BaseSiteRegistry.OnSiteChanged+=SiteChanged;
                changed=now;step=1;return;
            }
            if(now-changed<1) return;
            switch(step)
            {
                case 1:
                    Vector2Int baseChunk=new Vector2Int(Mathf.FloorToInt(spawner.Site.center.x/rig.terrain.terrainSettings.chunkSize),Mathf.FloorToInt(spawner.Site.center.y/rig.terrain.terrainSettings.chunkSize));
                    if(!rig.terrain.TryGetTerrainChunk(baseChunk,out var readyChunk) || !readyChunk.HasGeneratedMesh || !readyChunk.WorldFeaturesReady) return;
                    Check(!failed,"No Unity errors during startup");
                    Check(siteChanges==0,"Site registry emits no per-frame regeneration events");
                    var drySettings=new BaseSiteSettings { seed=77,candidates=32,waterLevel=10 };
                    var dryA=BaseSiteSolver.Solve(new FlatSource(300),drySettings);
                    var dryB=BaseSiteSolver.Solve(new FlatSource(300),drySettings);
                    Check(dryA.valid && dryA.center==dryB.center && dryA.yaw==dryB.yaw,"Flat-height solver is deterministic");
                    Check(!BaseSiteSolver.Solve(new FlatSource(0),drySettings).valid,"Submerged terrain rejects a floating base");
                    drySettings.constrainToMap=true;drySettings.minimumWorldPosition=Vector2.zero;drySettings.maximumWorldPosition=Vector2.one*1200;
                    drySettings.searchCenter=Vector2.one*10000;
                    Check(!BaseSiteSolver.Solve(new FlatSource(300),drySettings).valid,"Base-site solver rejects positions outside the finite map");
                    Check(Object.FindObjectsByType<BaseAssembly>(FindObjectsSortMode.None).Length==1,"Exactly one base for the seed");
                    Check(Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Length==0,"No HUD or Canvas");
                    Check(assembly.left.IsConfigured && assembly.right.IsConfigured,"Both imported cannon hierarchies connected");
                    Check(assembly.left.ShotDirection.y>0 && assembly.right.ShotDirection.y>0,"Positive elevation aims above the horizon");
                    Check(rig.terrain.GeneratedChunkCount>0,"Terrain streaming generated chunks");
                    ValidatePit(); changed=now;step=12;break;
                case 12:
                    if(now-changed<2) return;
                    Capture(rig.overviewCamera,"01_Base_No_Terreno");
                    rig.SetView(BaseGameRig.View.LeftCannon); changed=now;step=2;break;
                case 2:
                    Capture(rig.stationCamera,"02_Instrumentos_Canhao");
                    var wheels=assembly.GetComponentsInChildren<BaseHandwheelInteraction>();
                    foreach(var wheel in wheels) if(wheel.elevation && wheel.cannon==assembly.left)
                    { float before=assembly.left.Elevation;wheel.RotateBy(90);Check(assembly.left.Elevation>before,"Physical wheel drives imported elevation pivot"); }
                    var buttons=assembly.GetComponentsInChildren<PhysicalPushButton>();
                    foreach(var button in buttons) if(button.name=="Botao_MUNICAO" && button.GetComponentInParent<BaseGaugeDriver>().cannon==assembly.left)
                    {
                        Press(button);Check(assembly.left.selectedAmmo==1,"Mouse raycast selects ammunition through physical button");
                        Press(button);Check(assembly.left.selectedAmmo==0,"Physical button cycles back to HE");
                    }
                    rig.SetView(BaseGameRig.View.Map);changed=now;step=3;break;
                case 3:
                    Capture(rig.stationCamera,"03_Mapa_Fisico");
                    leftStock=assembly.left.CurrentAmmo.stock;rightStock=assembly.right.CurrentAmmo.stock;
                    Check(!assembly.left.Fire(),"Empty cannon rejects firing");
                    Check(assembly.left.RequestLoad() && assembly.right.RequestLoad(),"Independent simultaneous loading");
                    Check(!assembly.left.RequestLoad(),"Busy cannon rejects duplicate loading");
                    assembly.left.SelectAmmo(1); Check(assembly.left.selectedAmmo==0,"Ammunition selection locked during loading");
                    changed=now;step=4;break;
                case 4:
                    if(assembly.left.State!=CannonState.Loaded || assembly.right.State!=CannonState.Loaded) return;
                    Check(assembly.left.CurrentAmmo.stock==leftStock-1 && assembly.right.CurrentAmmo.stock==rightStock-1,"Each loaded shell consumes one stock unit");
                    assembly.left.AdjustElevation(10);assembly.right.AdjustElevation(20);
                    Check(Mathf.Abs(assembly.left.Elevation-15)<.01f && Mathf.Abs(assembly.right.Elevation-25)<.01f,"Independent elevations on the same heading");
                    Check(Mathf.Abs(assembly.left.ShotDirection.y-Mathf.Sin(15*Mathf.Deg2Rad))<.005f,"FBX elevation axis calibrated");
                    assembly.platform.QueueRotation(4);changed=now;step=5;break;
                case 5:
                    if(now-changed<1.2) return;
                    Check(!assembly.left.Fire(),"Moving base rejects a shot");
                    step=6;break;
                case 6:
                    if(assembly.platform.IsMoving) return;
                    Check(Mathf.Abs(Mathf.DeltaAngle(initialSite.yaw+4,assembly.platform.Heading))<.05f,"Platform yaw uses Unity vertical axis and stops at target");
                    Check(assembly.left.Fire() && assembly.right.Fire(),"Both cannons fire simultaneously while stationary");
                    changed=now;step=7;break;
                case 7:
                    if(assembly.left.State!=CannonState.Empty || assembly.right.State!=CannonState.Empty) return;
                    Check(assembly.left.CurrentAmmo.stock==leftStock-1,"Firing does not consume a second shell");
                    Check(assembly.left.recoilPivot.localPosition.sqrMagnitude<.000001f,"Recoil returns to imported rest pose");
                    Check(assembly.left.RequestLoad(),"Cannon can reload after recovery");
                    assembly.left.enabled=false;Check(assembly.left.CurrentAmmo.stock==leftStock-1,"Canceled loading refunds the reserved shell");assembly.left.enabled=true;
                    spawner.Generate();changed=now;step=8;break;
                case 8:
                    Check(spawner.Site.center==initialSite.center && Mathf.Abs(spawner.Site.groundHeight-initialSite.groundHeight)<.001f,"Regeneration repeats deterministic site");
                    Check(Object.FindObjectsByType<BaseAssembly>(FindObjectsSortMode.None).Length==1,"Regeneration retains one base");
                    rig.terrain.terrainSettings.noiseSettings.seed=12345;changed=now;step=9;break;
                case 9:
                    if(spawner.Site.seed!=12345) return;
                    Check(spawner.Site.center!=initialSite.center,"Changing terrain seed relocates base");
                    Check(Object.FindObjectsByType<BaseAssembly>(FindObjectsSortMode.None).Length==1,"Seed change creates one replacement base");
                    rig.terrain.terrainSettings.noiseSettings.seed=9173;changed=now;step=10;break;
                case 10:
                    if(spawner.Site.seed!=9173) return;
                    Check(Object.FindObjectsByType<BaseAssembly>(FindObjectsSortMode.None).Length==1,"Third seed remains unique");
                    rig.terrain.terrainSettings.noiseSettings.seed=originalSeed;changed=now;step=11;break;
                case 11:
                    if(spawner.Site.seed!=originalSeed) return;
                    Check(spawner.Site.center==initialSite.center,"Returning to original seed restores original position");
                    rig.SetView(BaseGameRig.View.Overview);Capture(rig.overviewCamera,"04_Base_Apos_Regeneracao");
                    Check(!failed,"No Unity exceptions during load, fire, rotation and regeneration");
                    step=13;changed=now;EditorApplication.ExitPlaymode();break;
                case 14:
                    rig=Object.FindAnyObjectByType<BaseGameRig>();spawner=rig.spawner;
                    if(spawner.Instance==null || now-changed<2) return;
                    Check(Object.FindObjectsByType<BaseAssembly>(FindObjectsSortMode.None).Length==1,"A second Play session still creates exactly one active base");
                    Check(spawner.Site.center==initialSite.center,"A second Play session restores the deterministic site");
                    Check(rig.overviewCamera!=null && rig.playerCamera!=null && rig.stationCamera!=null,"Cameras survive exiting Play without scene reload");
                    Check(!failed,"Repeated Play session has no gameplay errors");Finish(0);break;
            }
        }
        catch(Exception e) { report.AppendLine("FAIL "+e);Debug.LogException(e);Finish(1); }
    }
    static void ValidatePit()
    {
        int hits=0;var site=spawner.Site;
        foreach(var chunk in rig.terrain.TerrainChunks.Values)
        {
            if(!chunk.HasGeneratedMesh) continue;
            var map=chunk.GeneratedData; Check(map.BaseSite.seed==site.seed && map.BaseSite.valid,"Chunk uses immutable base-site snapshot "+chunk.Coordinate);
            for(int i=0;i<12;i++)
            {
                float a=i*Mathf.PI/6;Vector3 p=new Vector3(site.center.x+Mathf.Cos(a)*20.8f,site.groundHeight+20,site.center.y+Mathf.Sin(a)*20.8f);
                if(chunk.MeshCollider.Raycast(new Ray(p,Vector3.down),out var hit,50)) hits++;
            }
        }
        Check(hits==0,"The geometric pit opening does not cover the FBX floor");
    }
    static void Press(PhysicalPushButton button)
    {
        Vector3 screen=rig.stationCamera.WorldToScreenPoint(button.buttonVisual.position);
        Check(rig.stationInteractor.TryBeginInteraction(new Vector2(screen.x,screen.y)),"Physical button collider is reachable: "+button.name);
        rig.stationInteractor.CancelInteraction();
    }
    public static void Capture(Camera camera,string name)
    {
        Directory.CreateDirectory("Artifacts/BaseIntegration/Captures");
        var rt=new RenderTexture(1600,900,24);rt.Create();var old=camera.targetTexture;var active=RenderTexture.active;
        camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
        var image=new Texture2D(1600,900,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1600,900),0,0);image.Apply();
        File.WriteAllBytes("Artifacts/BaseIntegration/Captures/"+name+".png",image.EncodeToPNG());
        camera.targetTexture=old;RenderTexture.active=active;rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(image);
    }
    static void Finish(int code)
    {
        SessionState.SetBool(Key,false);EditorApplication.update-=Tick;Application.logMessageReceived-=Log;
        BaseSiteRegistry.OnSiteChanged-=SiteChanged;
        Directory.CreateDirectory("Artifacts/BaseIntegration");File.WriteAllText("Artifacts/BaseIntegration/PlayValidation.txt",report.ToString());
        Debug.Log("BASE_INTEGRATION_VALIDATION_EXIT="+code);
        EditorApplication.Exit(code);
    }
}
