using System;
using System.IO;
using System.Text;
using ProjectLeviathan.Base;
using ProjectLeviathan.ProceduralTerrain;
using ProjectLeviathan.Vegetation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

public static class EnvironmentEnrichmentValidation
{
    const string Key="Leviathan.Environment.Validation";
    const string Output="Artifacts/EnvironmentEnrichment";
    static StringBuilder report;
    static double started,changed;
    static int step;
    static bool error;
    static BaseGameRig rig;
    static ProceduralTreeManager trees;
    static ulong treeHash;
    static int treeCount;

    [MenuItem("Leviathan/Environment/Validar ambiente em Play")]
    public static void ValidateBatch()
    {
        EditorSceneManager.OpenScene(BaseIntegrationBuilder.ScenePath);
        SessionState.SetBool(Key,true);Resume();
        try {ValidateHeights();EditorApplication.EnterPlaymode();EditorApplication.isPaused=false;}
        catch(Exception e) {report.AppendLine("FAIL "+e);Finish(1);}
    }
    [InitializeOnLoadMethod]
    static void Resume()
    {
        if(!SessionState.GetBool(Key,false)) return;
        report=new StringBuilder();started=changed=EditorApplication.timeSinceStartup;step=0;error=false;
        EditorApplication.update-=Tick;EditorApplication.update+=Tick;
        Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;
    }
    static void Log(string message,string trace,LogType type)
    {if(!trace.Contains("UnityEditor.Search.") && (type==LogType.Error || type==LogType.Exception || type==LogType.Assert)) {error=true;report.AppendLine("ERROR "+message);}}
    static void Check(bool condition,string label)
    {if(!condition) throw new InvalidOperationException(label);report.AppendLine("PASS "+label);}
    static void ValidateHeights()
    {
        rig=Object.FindAnyObjectByType<BaseGameRig>();var s=rig.terrain.terrainSettings;
        int n=s.chunkResolution;var natural=s.CaptureGenerationSettings(false);
        var a=HeightMapGenerator.GenerateHeightMap(natural,n,n,Vector2.zero);
        var b=HeightMapGenerator.GenerateHeightMap(natural,n,n,new Vector2(s.chunkSize,0));
        var c=HeightMapGenerator.GenerateHeightMap(natural,n,n,new Vector2(0,s.chunkSize));
        var repeat=HeightMapGenerator.GenerateHeightMap(natural,n,n,Vector2.zero);
        float seam=0,normalSeam=0,determinism=0;
        for(int i=0;i<n;i++) {seam=Mathf.Max(seam,Mathf.Abs(a.WorldHeights[n-1,i]-b.WorldHeights[0,i]));seam=Mathf.Max(seam,Mathf.Abs(a.WorldHeights[i,0]-c.WorldHeights[i,n-1]));}
        for(int i=0;i<n;i++) for(int offset=-1;offset<=1;offset++)
        {
            normalSeam=Mathf.Max(normalSeam,Mathf.Abs(a.NormalWorldHeights[n+offset,i+1]-b.NormalWorldHeights[1+offset,i+1]));
            normalSeam=Mathf.Max(normalSeam,Mathf.Abs(a.NormalWorldHeights[i+1,1+offset]-c.NormalWorldHeights[i+1,n+offset]));
        }
        for(int y=0;y<n;y++) for(int x=0;x<n;x++) determinism=Mathf.Max(determinism,Mathf.Abs(a.WorldHeights[x,y]-repeat.WorldHeights[x,y]));
        Check(seam<.01f,"Eroded chunk borders match on both axes; max error="+seam);
        Check(normalSeam<.01f,"Normal sampling halos match across eroded chunk borders; max error="+normalSeam);
        Check(determinism==0,"Erosion generation repeats exactly for the same seed");
        var site=rig.spawner.previewSite;Check(site.valid,"The scene has a valid naturally flat base site");
        Vector2 origin=new Vector2(Mathf.Floor(site.center.x/s.chunkSize)*s.chunkSize,Mathf.Floor(site.center.y/s.chunkSize)*s.chunkSize);
        s.RuntimeBaseSite=site;
        var withSite=HeightMapGenerator.GenerateHeightMap(s.CaptureGenerationSettings(),n,n,origin);
        var withoutSite=HeightMapGenerator.GenerateHeightMap(s.CaptureGenerationSettings(false),n,n,origin);
        float deformation=0;
        for(int y=0;y<n;y++) for(int x=0;x<n;x++) deformation=Mathf.Max(deformation,Mathf.Abs(withSite.WorldHeights[x,y]-withoutSite.WorldHeights[x,y]));
        Check(deformation==0,"Placing the base does not change any natural height sample");
        bool enabled=s.erosion.enabled;s.erosion.enabled=false;
        var original=HeightMapGenerator.GenerateHeightMap(s.CaptureGenerationSettings(false),n,n,Vector2.zero);s.erosion.enabled=enabled;
        float incision=0;for(int y=0;y<n;y++) for(int x=0;x<n;x++) incision=Mathf.Max(incision,Mathf.Abs(a.WorldHeights[x,y]-original.WorldHeights[x,y]));
        Check(incision>.1f,"Erosion adds measurable surface relief; maximum change="+incision);
        var spawner=rig.spawner;spawner.heightSource.ResetCache();float low=float.MaxValue,high=float.MinValue;
        float radius=spawner.settings.checkRadius;
        for(int y=-12;y<=12;y++) for(int x=-12;x<=12;x++)
        {Vector2 offset=new Vector2(x,y)*radius/12;if(offset.sqrMagnitude>radius*radius) continue;float h=spawner.heightSource.GetBaseHeight(site.center.x+offset.x,site.center.y+offset.y);low=Mathf.Min(low,h);high=Mathf.Max(high,h);}
        Check(high-low<=spawner.settings.maxHeightRange+.05f,"The full base footprint fits natural flat ground; range="+(high-low));
        Check(low>spawner.settings.waterLevel+spawner.settings.minAboveWater,"Base footprint is dry");
        var grass=rig.grass.configuration;
        Check(grass.bladesPerSquareMeter>=5 && grass.heightRange.x>0 && grass.heightRange.y>=grass.heightRange.x &&
            grass.meadowVariation>0 && grass.meadowVariation<=1,"Dense grass with valid meadow height variation is connected");
        Check(Shader.Find("Leviathan/Cartoon Trees")!=null,"URP tree shader imported");
    }
    static void Tick()
    {
        try
        {
            double now=EditorApplication.timeSinceStartup;
            if(now-started>180) throw new InvalidOperationException("Environment validation timeout at step "+step);
            if(!EditorApplication.isPlaying) return;
            Application.runInBackground=true;
            rig=Object.FindAnyObjectByType<BaseGameRig>();trees=Object.FindAnyObjectByType<ProceduralTreeManager>();
            if(rig.spawner.Instance==null || trees==null || trees.TreeCount==0) return;
            if(step==0) {changed=now;step=1;return;}
            if(now-changed<10) return;
            if(step==1)
            {
                Check(!error,"Play startup has no runtime or shader errors");
                Check(trees.TreeCount>100,"Living trees generated across terrain chunks; count="+trees.TreeCount);
                Check(Object.FindObjectsByType<BaseAssembly>(FindObjectsSortMode.None).Length==1,"Exactly one base remains active");
                Check(Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Length==0,"Environment adds no HUD or Canvas");
                rig.SetView(BaseGameRig.View.Overview);Capture("01_Base_Terreno_Natural",rig.overviewCamera);
                Vector3 target=trees.FirstTreePosition;
                rig.overviewCamera.transform.position=target+new Vector3(-18,5,-22);
                rig.overviewCamera.transform.LookAt(target+Vector3.up*4);
                rig.grass.renderCamera=rig.overviewCamera;
                rig.grass.player=null;
                changed=now;step=2;return;
            }
            if(step==2)
            {
                Capture("02_Grama_Arvores",rig.overviewCamera);
                trees.RenderForValidation(rig.overviewCamera);
                Check(trees.VisibleTrees>0,"Tree frustum/distance culling retains visible trees");
                int invalid=trees.ValidatePlacementForValidation(out float treeRootError);
                Check(invalid==0,"Trees avoid water, roads, POIs and the base footprint");
                Check(treeRootError<.06f,"Tree trunks follow their terrain LOD; max error="+treeRootError);
                rig.grass.RenderForValidation();var counts=rig.grass.ReadVisibleCountsForValidation();
                uint blades=0;foreach(var count in counts) blades+=count;
                Check(blades>1000,"GPU grass culling retains a populated field; visible blades="+blades);
                float rootError=rig.grass.ValidateRootsForValidation();Check(rootError<.06f,"Grass roots follow rendered terrain; max error="+rootError);
                report.AppendLine("INFO Grass buffer bytes="+rig.grass.BufferBytes+"; CPU Update allocation="+rig.grass.LastUpdateAllocatedBytes);
                treeHash=trees.DistributionHash;treeCount=trees.TreeCount;
                trees.enabled=false;trees.enabled=true;changed=now;step=3;return;
            }
            if(step==3)
            {
                Check(trees.DistributionHash==treeHash && trees.TreeCount==treeCount,"Tree regeneration preserves seeded positions and density");
                Check(!error,"Rendering and regeneration have no Unity errors");Finish(0);
            }
        }
        catch(Exception e) {report.AppendLine("FAIL "+e);Debug.LogException(e);Finish(1);}
    }
    static void Capture(string name,Camera camera)
    {
        Directory.CreateDirectory(Output+"/Captures");
        var rt=new RenderTexture(1600,900,24);rt.Create();var old=camera.targetTexture;var active=RenderTexture.active;
        camera.targetTexture=rt;rig.grass.RenderForValidation();trees.RenderForValidation(camera);camera.Render();RenderTexture.active=rt;
        var image=new Texture2D(1600,900,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1600,900),0,0);image.Apply();
        int magenta=0;var pixels=image.GetPixels32();foreach(var p in pixels) if(p.r>240 && p.b>240 && p.g<30) magenta++;
        Check(magenta<100,"Screenshot "+name+" has no magenta error materials");
        File.WriteAllBytes(Output+"/Captures/"+name+".png",image.EncodeToPNG());
        camera.targetTexture=old;RenderTexture.active=active;rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(image);
    }
    static void Finish(int code)
    {
        SessionState.SetBool(Key,false);EditorApplication.update-=Tick;Application.logMessageReceived-=Log;
        Directory.CreateDirectory(Output);File.WriteAllText(Output+"/Validation.txt",report.ToString());
        Debug.Log("ENVIRONMENT_VALIDATION_EXIT="+code);EditorApplication.Exit(code);
    }
}
