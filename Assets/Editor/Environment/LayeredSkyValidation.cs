using System;
using System.IO;
using System.Text;
using ProjectLeviathan.Base;
using ProjectLeviathan.Vegetation;
using ProjectLeviathan.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class LayeredSkyValidation
{
    const string Key="Leviathan.LayeredSky.Validation";
    const string Output="Artifacts/LayeredSky/";
    static StringBuilder report;
    static double started,changed;
    static int step,seed;
    static bool errors;
    static BaseGameRig rig;
    static LayeredSkyController sky;
    static Camera fixture;
    static Material original,material;
    static Texture2D night,windFrame;
    static Vector2 baseCenter;
    static float lowCoverage,highCoverage;
    static float savedStartHour;

    // Dedicated batch Editor only; exits Unity when all GPU image tests finish.
    public static void ValidateBatch()
    {
        EditorSceneManager.OpenScene(BaseIntegrationBuilder.ScenePath);
        SessionState.SetBool(Key,true);Resume();EditorApplication.EnterPlaymode();EditorApplication.isPaused=false;
    }
    [InitializeOnLoadMethod] static void Resume()
    {
        if(!SessionState.GetBool(Key,false))return;
        report=new StringBuilder();step=0;errors=false;started=changed=EditorApplication.timeSinceStartup;
        EditorApplication.update-=Tick;EditorApplication.update+=Tick;
        Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;
    }
    static void Log(string message,string trace,LogType type)
    {
        if(trace.Contains("UnityEditor.Search."))return;
        if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert){errors=true;report.AppendLine("ERROR "+message);}
    }
    static void Check(bool condition,string message)
    {if(!condition)throw new InvalidOperationException(message);report.AppendLine("PASS "+message);}
    static void Tick()
    {
        try
        {
            double now=EditorApplication.timeSinceStartup;
            if(now-started>240)throw new InvalidOperationException("Sky validation timeout at "+step);
            if(!EditorApplication.isPlaying)return;
            Application.runInBackground=true;
            if(step==0)
            {
                rig=Object.FindAnyObjectByType<BaseGameRig>();sky=Object.FindAnyObjectByType<LayeredSkyController>();
                Check(sky!=null && sky.sun!=null && sky.moon!=null,"Sky, sun and moon are integrated");
                savedStartHour=sky.timeOfDay;
                sky.cycleTime=false;sky.animateAtmosphere=false;sky.updateEnvironment=false;
                original=sky.skyMaterial;material=new Material(original);sky.skyMaterial=material;
                lowCoverage=material.GetFloat("_CloudLowCoverage");highCoverage=material.GetFloat("_CloudHighCoverage");
                fixture=new GameObject("Temporary sky-only camera").AddComponent<Camera>();fixture.enabled=false;
                fixture.clearFlags=CameraClearFlags.Skybox;fixture.cullingMask=0;fixture.fieldOfView=85;
                fixture.transform.rotation=Quaternion.Euler(-18,0,0);step=1;changed=now;return;
            }
            if(now-changed<(step==1 ? 12 : step==7 || step==10 ? 5 : 1.5))return;
            if(rig.spawner.Instance==null)return;
            switch(step)
            {
                case 1:
                    foreach(var chunk in rig.terrain.TerrainChunks.Values)if(!chunk.HasGeneratedMesh || !chunk.WorldFeaturesReady)return;
                    CheckShader();baseCenter=rig.spawner.Site.center;seed=rig.terrain.terrainSettings.noiseSettings.seed;
                    fixture.transform.position=rig.spawner.Instance.transform.position+Vector3.up*20;
                    rig.overviewCamera.transform.position=rig.spawner.Instance.transform.position+new Vector3(-70,25,-75);
                    rig.overviewCamera.transform.LookAt(rig.spawner.Instance.transform.position+Vector3.up*12);
                    sky.SetTimeOfDay(12);CaptureWorld("01_Dia");
                    Check(RenderSettings.sun==sky.sun && sky.sun.intensity>1 && !sky.moon.enabled,"Day uses sunlight without an extra active moon light");
                    step=2;break;
                case 2:
                    sky.SetTimeOfDay(6);CaptureWorld("02_Amanhecer");step=3;break;
                case 3:
                    sky.SetTimeOfDay(18);CaptureWorld("03_Entardecer");step=4;break;
                case 4:
                    sky.SetTimeOfDay(0);CaptureWorld("04_Noite");
                    Check(RenderSettings.sun==sky.moon && sky.moon.intensity>.2f && sky.sun.intensity<.001f,"Night uses moonlight as URP main light");
                    night=Read("05_Ceu_Noturno");
                    material.SetFloat("_StarBrightness",0);var withoutStars=Read("StarsDisabled");
                    int starPixels=ChangedPixels(night,withoutStars,30);
                    Check(starPixels>60,"Many GPU-rendered stars are visible through clouds; affected pixels="+starPixels);
                    material.SetFloat("_AuroraStrength",0);var withoutAurora=Read("AuroraDisabled");
                    int auroraPixels=ChangedPixels(withoutStars,withoutAurora,15);
                    Check(auroraPixels>100,"Colored aurora is rendered at night; affected pixels="+auroraPixels);
                    Object.Destroy(withoutStars);Object.Destroy(withoutAurora);Object.Destroy(night);night=null;
                    material.SetFloat("_StarBrightness",original.GetFloat("_StarBrightness"));material.SetFloat("_AuroraStrength",original.GetFloat("_AuroraStrength"));
                    sky.SetTimeOfDay(12);step=5;break;
                case 5:
                    material.SetFloat("_CloudHighCoverage",0);
                    var lowA=Read("LowLayer_A");fixture.transform.position+=Vector3.right*450;var lowB=Read("LowLayer_B");
                    Check(ChangedPixels(lowA,lowB,12)>200,"Lower cloud layer has world-space camera parallax");
                    Object.Destroy(lowA);Object.Destroy(lowB);fixture.transform.position-=Vector3.right*450;
                    material.SetFloat("_CloudLowCoverage",0);material.SetFloat("_CloudHighCoverage",highCoverage);
                    var highA=Read("HighLayer_A");fixture.transform.position+=Vector3.right*450;var highB=Read("HighLayer_B");
                    Check(ChangedPixels(highA,highB,12)>200,"Upper cloud layer has independent world-space camera parallax");
                    Object.Destroy(highA);Object.Destroy(highB);fixture.transform.position-=Vector3.right*450;
                    material.SetFloat("_CloudLowCoverage",lowCoverage);sky.animateAtmosphere=true;
                    windFrame=Read("CloudMotion_A");step=7;break;
                case 7:
                    var windNext=Read("CloudMotion_B");
                    Check(ChangedPixels(windFrame,windNext,6)>100,"Cloud layers animate with wind on the GPU");
                    Object.Destroy(windFrame);windFrame=null;Object.Destroy(windNext);sky.animateAtmosphere=false;
                    sky.SetTimeOfDay(0);material.SetFloat("_CloudOpacity",0);material.SetFloat("_StarBrightness",0);
                    sky.animateAtmosphere=true;windFrame=Read("AuroraMotion_A");step=10;break;
                case 10:
                    var auroraNext=Read("AuroraMotion_B");
                    Check(ChangedPixels(windFrame,auroraNext,6)>100,"Aurora curtains animate on the GPU independently of clouds and stars");
                    Object.Destroy(windFrame);windFrame=null;Object.Destroy(auroraNext);sky.animateAtmosphere=false;
                    material.SetFloat("_CloudOpacity",original.GetFloat("_CloudOpacity"));material.SetFloat("_StarBrightness",original.GetFloat("_StarBrightness"));
                    sky.SetTimeOfDay(23.99f);sky.minutesPerDay=1;sky.cycleTime=true;step=8;break;
                case 8:
                    Check(sky.CurrentHour<1,"Day/night clock wraps past midnight");sky.cycleTime=false;
                    Check(sky.timeOfDay==savedStartHour,"Runtime time progression does not overwrite saved start hour");
                    Check(baseCenter==rig.spawner.Site.center && seed==rig.terrain.terrainSettings.noiseSettings.seed,"Sky cycle preserves seed and base location");
                    Check(Object.FindObjectsByType<BaseAssembly>().Length==1,"One base remains");
                    Check(Object.FindObjectsByType<Canvas>().Length==0,"Sky introduces no Canvas or gameplay overlays");
                    sky.enabled=false;
                    Check(RenderSettings.skybox!=material,"Disabling the sky restores the previous environment");
                    sky.enabled=true;sky.SetTimeOfDay(9.5f);CheckShader();Check(!errors,"All sky phases, parallax, wind and cleanup run without errors");
                    Finish(0);return;
            }
            changed=now;
        }
        catch(Exception exception){report.AppendLine("FAIL "+exception);Debug.LogException(exception);Finish(1);}
    }
    static void CheckShader()
    {
        Shader shader=Shader.Find("Leviathan/Layered Sky");Check(shader!=null && shader.isSupported,"URP sky shader supported on "+SystemInfo.graphicsDeviceName);
        foreach(var message in ShaderUtil.GetShaderMessages(shader))if(message.severity.ToString()=="Error")throw new InvalidOperationException(message.message);
    }
    static int ChangedPixels(Texture2D a,Texture2D b,int threshold)
    {
        var x=a.GetPixels32();var y=b.GetPixels32();int changed=0;
        for(int i=0;i<x.Length;i++)if(Math.Abs(x[i].r-y[i].r)+Math.Abs(x[i].g-y[i].g)+Math.Abs(x[i].b-y[i].b)>threshold)changed++;
        return changed;
    }
    static Texture2D Read(string name)
    {
        string path=Output+"Captures/"+name+".png";DownloadedAssetSurvey.Capture(fixture,path,640,360);
        var texture=new Texture2D(2,2);texture.LoadImage(File.ReadAllBytes(path));return texture;
    }
    static void CaptureWorld(string name)
    {
        rig.grass.RenderForValidation();Object.FindAnyObjectByType<ProceduralTreeManager>().RenderForValidation(rig.overviewCamera);
        Object.FindAnyObjectByType<WorldSceneryManager>().RenderForValidation(rig.overviewCamera);
        string path=Output+"Captures/"+name+".png";DownloadedAssetSurvey.Capture(rig.overviewCamera,path);
        var texture=new Texture2D(2,2);texture.LoadImage(File.ReadAllBytes(path));
        int magenta=0;foreach(var p in texture.GetPixels32())if(p.r>230 && p.b>230 && p.g<50)magenta++;
        Check(magenta<texture.width*texture.height*.002f,"No error materials in "+name);Object.Destroy(texture);
    }
    static void Finish(int code)
    {
        SessionState.SetBool(Key,false);EditorApplication.update-=Tick;Application.logMessageReceived-=Log;
        if(sky!=null && original!=null){sky.skyMaterial=original;sky.SetTimeOfDay(9.5f);}
        if(fixture!=null)Object.Destroy(fixture.gameObject);if(material!=null)Object.Destroy(material);
        if(night!=null)Object.Destroy(night);if(windFrame!=null)Object.Destroy(windFrame);
        Directory.CreateDirectory(Output);File.WriteAllText(Output+"Validation.txt",report.ToString());
        Debug.Log("LAYERED_SKY_VALIDATION_EXIT="+code);EditorApplication.Exit(code);
    }
}
