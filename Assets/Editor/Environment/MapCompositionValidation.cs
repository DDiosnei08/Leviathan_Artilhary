using System;
using System.IO;
using System.Text;
using ProjectLeviathan.Base;
using ProjectLeviathan.ProceduralTerrain;
using ProjectLeviathan.Vegetation;
using ProjectLeviathan.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

public static class MapCompositionValidation
{
    const string Key="Leviathan.MapComposition.Validation";
    const string Output="Artifacts/MapComposition/";
    static StringBuilder report;
    static double started,changed;
    static int step,warmFrames;
    static bool errors;
    static BaseGameRig rig;
    static LayeredSkyController sky;
    static ProceduralTreeManager trees;
    static WorldSceneryManager scenery;
    static ulong treesHash,sceneryHash;
    static Vector2 baseCenter;
    static Vector3 savedCamera;

    public static void ApplyAndValidateBatch()
    {
        ShaderUtil.allowAsyncCompilation=false;
        MapCompositionSetup.ApplyBatch();
        SessionState.SetBool(Key,true);Resume();EditorApplication.EnterPlaymode();EditorApplication.isPaused=false;
    }
    // QA must exercise saved assets without silently rebuilding presets first.
    public static void ValidateBatch()
    {
        ShaderUtil.allowAsyncCompilation=false;
        EditorSceneManager.OpenScene(BaseIntegrationBuilder.ScenePath);
        SessionState.SetBool(Key,true);Resume();EditorApplication.EnterPlaymode();EditorApplication.isPaused=false;
    }
    [InitializeOnLoadMethod] static void Resume()
    {
        if(!SessionState.GetBool(Key,false))return;
        report=new StringBuilder();step=warmFrames=0;errors=false;started=changed=EditorApplication.timeSinceStartup;
        EditorApplication.update-=Tick;EditorApplication.update+=Tick;Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;
    }
    static void Log(string message,string trace,LogType type)
    {
        if(trace.Contains("UnityEditor.Search."))return;
        if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert){errors=true;report.AppendLine("ERROR "+message);}
    }
    static void Check(bool pass,string message)
    {if(!pass)throw new InvalidOperationException(message);report.AppendLine("PASS "+message);}
    static bool Ready()
    {
        if(rig==null || rig.spawner.Instance==null || rig.terrain.TerrainChunks.Count<9)return false;
        foreach(var chunk in rig.terrain.TerrainChunks.Values)if(!chunk.HasGeneratedMesh || !chunk.WorldFeaturesReady)return false;
        return true;
    }
    static void Tick()
    {
        try
        {
            double now=EditorApplication.timeSinceStartup;
            if(now-started>420)throw new InvalidOperationException("Map validation timeout at "+step);
            if(!EditorApplication.isPlaying)return;
            Application.runInBackground=true;
            if(step==0)
            {
                rig=Object.FindAnyObjectByType<BaseGameRig>();sky=Object.FindAnyObjectByType<LayeredSkyController>();
                trees=Object.FindAnyObjectByType<ProceduralTreeManager>();scenery=Object.FindAnyObjectByType<WorldSceneryManager>();
                if(rig==null || rig.spawner.Instance==null)return;
                sky.cycleTime=false;sky.updateEnvironment=false;sky.SetTimeOfDay(10);
                rig.SetView(BaseGameRig.View.Overview);step=1;changed=now;return;
            }
            if(!Ready()){warmFrames=0;return;}
            if(++warmFrames<140 || now-changed<8)return;
            if(step==1)
            {
                Check(!errors,"World starts without Unity errors");Check(Object.FindObjectsByType<BaseAssembly>().Length==1,"Exactly one base per seed");
                Check(Object.FindObjectsByType<Canvas>().Length==0,"No gameplay HUD added");
                baseCenter=rig.spawner.Site.center;treesHash=trees.DistributionHash;sceneryHash=scenery.DistributionHash;
                Check(trees.TreeCount>100 && scenery.InstanceCount>50,"Populated trees and scenery: "+trees.TreeCount+" / "+scenery.InstanceCount);
                Check(trees.ValidatePlacementForValidation(out float treeError)==0 && treeError<.01f,"Trees stay outside water/roads/base and follow terrain LOD");
                Check(scenery.ValidateForValidation(out float propError,out var roles)==0 && propError<.01f,"Scenery remains supported and outside forbidden areas");
                Check(roles[4]>0 && roles[5]>0,"Shrubs and fallen timber are generated: "+roles[4]+" / "+roles[5]);
                ValidateRoads();
                int poi=0;foreach(var chunk in rig.terrain.TerrainChunks.Values)poi+=chunk.GeneratedPOIVisualCount;
                Check(poi>0,"Industrial reference points are generated: "+poi);
                Check(rig.spawner.Instance.GetComponentsInChildren<Light>(true).Length>=3,"Base has physical service lights");
                var shaders=new[]{"Leviathan/Spline Track","Leviathan/Industrial Mist","Leviathan/Cartoon Terrain"};
                foreach(string name in shaders){var shader=Shader.Find(name);Check(shader!=null && shader.isSupported,name+" supported");foreach(var m in ShaderUtil.GetShaderMessages(shader))if(m.severity.ToString()=="Error")throw new InvalidOperationException(m.message);}
                Capture("01_Base_Estradas");savedCamera=rig.overviewCamera.transform.position;
                rig.overviewCamera.transform.position=rig.spawner.Instance.transform.position+new Vector3(-110,70,-140);rig.overviewCamera.transform.LookAt(rig.spawner.Instance.transform.position+Vector3.up*4);
                Capture("02_Entorno");sky.SetTimeOfDay(18.5f);step=2;changed=now;warmFrames=0;return;
            }
            if(step==2)
            {
                Capture("03_Entardecer");sky.SetTimeOfDay(10);
                foreach(var chunk in rig.terrain.TerrainChunks.Values)
                {
                    var root=chunk.GameObject.transform.Find("Generated Points of Interest");
                    if(root!=null && root.childCount>0)
                    {
                        Vector3 p=root.GetChild(0).position;rig.overviewCamera.transform.position=p+new Vector3(-43,26,-48);rig.overviewCamera.transform.LookAt(p+Vector3.up*8);break;
                    }
                }
                step=3;changed=now;warmFrames=0;return;
            }
            if(step==3)
            {
                Capture("04_Referencia_Industrial");
                rig.overviewCamera.transform.position=savedCamera;rig.overviewCamera.transform.LookAt(rig.spawner.Instance.transform.position+Vector3.up*4);
                rig.terrain.RegenerateTerrain();step=4;changed=now;warmFrames=0;return;
            }
            if(step==4)
            {
                Check(trees.DistributionHash==treesHash,"Tree distribution repeats after regeneration");
                Check(scenery.DistributionHash==sceneryHash,"Scenery distribution repeats after regeneration");
                Check(rig.spawner.Site.center==baseCenter,"Regeneration preserves the deterministic base site");
                ValidateRoads();Check(!errors,"Streaming, LOD, effects and regeneration complete without Unity errors");
                Finish(0);
            }
        }
        catch(Exception e){report.AppendLine("FAIL "+e);Debug.LogException(e);Finish(1);}
    }
    static void ValidateRoads()
    {
        int meshes=0,vertices=0;float maxError=0,nearest=float.PositiveInfinity;
        foreach(var chunk in rig.terrain.TerrainChunks.Values)
        {
            if(!chunk.HasGeneratedRoadMesh)continue;meshes++;var mesh=chunk.GeneratedRoadMesh;int stride=1;
            for(int y=0;y<chunk.GeneratedData.Height;y++)for(int x=0;x<chunk.GeneratedData.Width;x++)
                if(chunk.GeneratedData.Roads[x,y]>.5f && chunk.GeneratedData.PoiMask[x,y]>.01f)throw new InvalidOperationException("Road crosses an industrial footprint");
            foreach(var lod in chunk.Settings.lods)if(lod.lod==chunk.AppliedLod)stride=lod.GetSimplificationLevel();
            var v=mesh.vertices;var n=mesh.normals;vertices+=v.Length;
            // Only indexed vertices are rendered; omitted boundary samples are intentionally unused.
            var used=new bool[v.Length];foreach(int i in mesh.triangles)used[i]=true;
            for(int i=0;i<v.Length;i++)if(used[i])
            {
                Vector3 p=v[i]-n[i]*Mathf.Max(.04f,chunk.Settings.roadSurfaceLift);
                if(!TerrainRoadMeshGenerator.SampleSurface(chunk.GeneratedData,chunk.Settings,stride,p,out Vector3 hit,out _))throw new InvalidOperationException("Road outside valid terrain at "+chunk.Coordinate+" local="+p.ToString("F6")+" vertex="+i);
                maxError=Mathf.Max(maxError,Vector3.Distance(p,hit));
                Vector3 world=chunk.GameObject.transform.TransformPoint(p);nearest=Mathf.Min(nearest,Vector2.Distance(new Vector2(world.x,world.z),rig.spawner.Site.center));
            }
        }
        Check(meshes>0 && vertices>100,"Spline roads rendered: "+meshes+" meshes, "+vertices+" vertices");
        Check(maxError<.02f,"Roads follow actual LOD triangles; max contact error="+maxError);
        Check(nearest<80 && nearest>rig.spawner.Site.carveRadius,"Supply route reaches the base perimeter without entering the pit: "+nearest+"m");
    }
    static void Capture(string name)
    {rig.grass.RenderForValidation();trees.RenderForValidation(rig.overviewCamera);scenery.RenderForValidation(rig.overviewCamera);DownloadedAssetSurvey.Capture(rig.overviewCamera,Output+"Captures/"+name+".png");}
    static void Finish(int code)
    {
        SessionState.SetBool(Key,false);EditorApplication.update-=Tick;Application.logMessageReceived-=Log;
        Directory.CreateDirectory(Output);File.WriteAllText(Output+"MapValidation.txt",report.ToString());Debug.Log("MAP_COMPOSITION_VALIDATION_EXIT="+code+"\n"+report);EditorApplication.Exit(code);
    }
}
