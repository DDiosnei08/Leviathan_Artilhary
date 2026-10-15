using System;
using System.IO;
using System.Text;
using ProjectLeviathan.Base;
using ProjectLeviathan.Vegetation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

public static class AssetCurationValidation
{
    const string Key="Leviathan.AssetCuration.Validation";
    static StringBuilder report;static double started,changed;static int step;static bool error;
    static BaseGameRig rig;static WorldSceneryManager scenery;static ProceduralTreeManager trees;static ulong hash;
    public static void ValidateBatch()
    {
        EditorSceneManager.OpenScene(BaseIntegrationBuilder.ScenePath);SessionState.SetBool(Key,true);Resume();
        try {ValidateSites();EditorApplication.EnterPlaymode();EditorApplication.isPaused=false;}
        catch(Exception e) {report.AppendLine("FAIL "+e);Finish(1);}
    }
    [InitializeOnLoadMethod]
    static void Resume()
    {
        if(!SessionState.GetBool(Key,false)) return;
        report=new StringBuilder();step=0;started=changed=EditorApplication.timeSinceStartup;error=false;
        EditorApplication.update-=Tick;EditorApplication.update+=Tick;Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;
    }
    static void Log(string message,string trace,LogType type)
    {if(!trace.Contains("UnityEditor.Search.") && (type==LogType.Error || type==LogType.Exception || type==LogType.Assert)) {error=true;report.AppendLine("ERROR "+message);}}
    static void Check(bool condition,string label) {if(!condition) throw new InvalidOperationException(label);report.AppendLine("PASS "+label);}
    static void ValidateSites()
    {
        rig=Object.FindAnyObjectByType<BaseGameRig>();var spawner=rig.spawner;var settings=rig.terrain.terrainSettings;int original=settings.noiseSettings.seed;
        foreach(int seed in new[]{original,12345,9173,77,2026,1,42,777,654321,9001})
        {
            settings.noiseSettings.seed=seed;spawner.settings.seed=seed;spawner.heightSource.ResetCache();spawner.ConfigureFootprint();
            var site=BaseSiteSolver.Solve(spawner.heightSource,spawner.settings);Check(site.valid,"Naturally supported base found for seed "+seed);
            float low=float.MaxValue,high=float.MinValue;
            for(int r=1;r<=5;r++) for(int i=0;i<128;i++)
            {float a=i*Mathf.PI/64,rad=spawner.settings.checkRadius*r/5;float h=spawner.heightSource.GetBaseHeight(site.center.x+Mathf.Cos(a)*rad,site.center.y+Mathf.Sin(a)*rad);low=Mathf.Min(low,h);high=Mathf.Max(high,h);}
            Check(high-low<=spawner.settings.maxHeightRange+.005f && low>spawner.settings.waterLevel+spawner.settings.minAboveWater,"Dry support footprint for seed "+seed+"; range="+(high-low));
            var again=BaseSiteSolver.Solve(spawner.heightSource,spawner.settings);Check(again.center==site.center,"Base placement deterministic for seed "+seed);
        }
        settings.noiseSettings.seed=12345;Check(spawner.RebuildEditorPreview(),"Editor preview relocates after changing terrain seed");
        Check(spawner.previewSite.seed==12345,"Editor preview and terrain seed stay synchronized");
        Check((rig.terrain.viewer.position-spawner.previewInstance.transform.position).magnitude<100,"Streaming window follows the relocated preview");
        settings.noiseSettings.seed=original;Check(spawner.RebuildEditorPreview(),"Editor preview returns to original seed");
        var configuration=AssetDatabase.LoadAssetAtPath<WorldSceneryConfiguration>(AssetCurationSetup.ConfigPath);
        Check(configuration!=null && configuration.prototypes.Length>=7,"Curated rock, pole, barrier, sandbag and crate prototypes connected");
        foreach(var prototype in configuration.prototypes)
        {
            var material=prototype.material!=null ? prototype.material : configuration.material;
            bool uniformMaterial=material!=null && material.HasProperty("_VertexColorStrength") && material.GetFloat("_VertexColorStrength")==0 &&
                (!material.HasProperty("_VertexMetalMask") || material.GetFloat("_VertexMetalMask")==0);
            Check(prototype.mesh!=null && (prototype.mesh.colors.Length==prototype.mesh.vertexCount || uniformMaterial),
                "Mesh supplies the color data required by its material: "+prototype.label);
        }
        var treeConfiguration=Object.FindAnyObjectByType<ProceduralTreeManager>().configuration;
        Check(treeConfiguration.broadleafFar.vertexCount<treeConfiguration.broadleafNear.vertexCount,"Downloaded oak has a reduced geometry LOD");
        Check(treeConfiguration.pineFar.vertexCount<treeConfiguration.pineNear.vertexCount,"Downloaded fir has a reduced geometry LOD");
    }
    static void Tick()
    {
        try
        {
            double now=EditorApplication.timeSinceStartup;if(now-started>240) throw new InvalidOperationException("Curation validation timeout at step "+step);
            if(!EditorApplication.isPlaying) return;Application.runInBackground=true;
            rig=Object.FindAnyObjectByType<BaseGameRig>();scenery=Object.FindAnyObjectByType<WorldSceneryManager>();trees=Object.FindAnyObjectByType<ProceduralTreeManager>();
            if(rig.spawner.Instance==null || scenery==null || scenery.InstanceCount==0 || trees.TreeCount==0) return;
            if(step==0) {step=1;changed=now;return;}if(now-changed<12) return;
            if(step==1)
            {
                Check(!error,"No Unity errors during curated-world startup");Check(Object.FindObjectsByType<BaseAssembly>(FindObjectsSortMode.None).Length==1,"Exactly one base in Play");
                Check(Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Length==0,"No overlay UI introduced");
                Check(scenery.InstanceCount>100,"Scenery generated; count="+scenery.InstanceCount);Check(trees.TreeCount>100,"Downloaded trees generated; count="+trees.TreeCount);
                int invalid=scenery.ValidateForValidation(out float rootError,out int[] roles);Check(invalid==0,"Scenery avoids water, roads and the base footprint");Check(rootError<.01f,"Scenery rests on current terrain LOD; root error="+rootError);
                if(scenery.configuration.roadsideFurniture)
                    Check(roles[0]>100 && roles[1]>0 && roles[2]>0 && roles[3]>0,"Rocks, poles, barriers and supply crates all appear");
                else
                {
                    int roadPoles=0,landmarks=0;
                    foreach(var chunk in rig.terrain.TerrainChunks.Values)
                    {
                        landmarks+=chunk.GeneratedPOIVisualCount;
                        foreach(var filter in chunk.GameObject.GetComponentsInChildren<MeshFilter>())
                            if(filter.sharedMesh!=null && filter.sharedMesh.name=="Roadside poles")roadPoles++;
                    }
                    Check(roles[0]>100 && roles[4]>0 && roles[5]>0,"Current scenery generates rocks, shrubs and fallen timber");
                    Check(roadPoles>0 && landmarks>0,"Spline infrastructure and industrial POIs replace legacy roadside furniture");
                }
                int activeColliders=0;foreach(var collider in scenery.GetComponentsInChildren<MeshCollider>()) if(collider.enabled && collider.sharedMesh!=null) activeColliders++;
                Check(activeColliders>0 && activeColliders<=scenery.configuration.maximumColliders,"Nearby props use a bounded collider pool; active="+activeColliders);
                report.AppendLine("INFO Role counts: rocks="+roles[0]+" poles="+roles[1]+" barriers="+roles[2]+" crates="+roles[3]);
                ValidateOpening();rig.SetView(BaseGameRig.View.Overview);Capture("01_Base_Integrada",rig.overviewCamera);
                var site=rig.spawner.Site;rig.overviewCamera.orthographic=true;rig.overviewCamera.orthographicSize=65;
                rig.overviewCamera.transform.SetPositionAndRotation(new Vector3(site.center.x,site.groundHeight+130,site.center.y),Quaternion.Euler(90,0,0));
                Capture("02_Base_Vista_Superior",rig.overviewCamera);rig.overviewCamera.orthographic=false;
                Vector3 point=scenery.NearestRock(rig.spawner.Instance.transform.position);rig.overviewCamera.transform.position=point+new Vector3(-23,5,-25);rig.overviewCamera.transform.LookAt(point+Vector3.up*2);
                rig.grass.renderCamera=rig.overviewCamera;rig.grass.player=null;changed=now;step=2;return;
            }
            if(step==2)
            {
                Capture("03_Bosque_Pedras",rig.overviewCamera);Check(!error,"Curated materials render without shader errors");
                hash=scenery.DistributionHash;scenery.enabled=false;scenery.enabled=true;changed=now;step=3;return;
            }
            if(step==3) {Check(scenery.DistributionHash==hash,"Scenery regeneration is deterministic");Check(!error,"Regeneration has no Unity errors");Finish(0);}
        }
        catch(Exception e) {report.AppendLine("FAIL "+e);Debug.LogException(e);Finish(1);}
    }
    static void ValidateOpening()
    {
        var site=rig.spawner.Site;int outside=0,inside=0;
        for(int z=-32;z<=32;z+=2) for(int x=-32;x<=32;x+=2)
        {
            float radius=Mathf.Sqrt(x*x+z*z);bool hit=false;var ray=new Ray(new Vector3(site.center.x+x,site.groundHeight+40,site.center.y+z),Vector3.down);
            foreach(var chunk in rig.terrain.TerrainChunks.Values) if(chunk.HasGeneratedMesh && chunk.MeshCollider.Raycast(ray,out var h,100)) hit=true;
            if(radius>site.carveRadius+1 && !hit) outside++;if(radius<site.carveRadius-1 && hit) inside++;
        }
        Check(outside==0 && inside==0,"Terrain opening is centered, bounded and does not cover the imported pit");
        foreach(var renderer in rig.spawner.Instance.GetComponentsInChildren<Renderer>()) if(renderer.name=="Fosso_Piso")
            Check(Vector2.Distance(new Vector2(renderer.bounds.center.x,renderer.bounds.center.z),site.center)<.01f,"Physical pit center matches the terrain opening");
    }
    static void Capture(string name,Camera camera)
    {
        rig.grass.RenderForValidation();trees.RenderForValidation(camera);scenery.RenderForValidation(camera);
        string path="Artifacts/AssetCuration/Captures/"+name+".png";DownloadedAssetSurvey.Capture(camera,path);
        var image=new Texture2D(2,2);image.LoadImage(File.ReadAllBytes(path));int magenta=0;foreach(var p in image.GetPixels32()) if(p.r>240 && p.b>240 && p.g<30) magenta++;
        Object.DestroyImmediate(image);Check(magenta<100,"No magenta materials in "+name);
    }
    static void Finish(int code)
    {
        SessionState.SetBool(Key,false);EditorApplication.update-=Tick;Application.logMessageReceived-=Log;
        Directory.CreateDirectory("Artifacts/AssetCuration");File.WriteAllText("Artifacts/AssetCuration/Validation.txt",report.ToString());
        Debug.Log("ASSET_CURATION_VALIDATION_EXIT="+code);EditorApplication.Exit(code);
    }
}
