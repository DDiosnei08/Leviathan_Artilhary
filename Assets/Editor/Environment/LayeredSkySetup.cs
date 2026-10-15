using System;
using System.IO;
using ProjectLeviathan.Base;
using ProjectLeviathan.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class LayeredSkySetup
{
    public const string MaterialPath = "Assets/Materials/Environment/LeviathanLayeredSky.mat";
    [MenuItem("Leviathan/Visual/Instalar ceu em camadas")]
    static void Menu()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
        ApplyBatch();
    }
    public static void ApplyBatch()
    {
        var scene=EditorSceneManager.OpenScene(BaseIntegrationBuilder.ScenePath);
        var rig=UnityEngine.Object.FindAnyObjectByType<BaseGameRig>();
        if(rig==null)throw new InvalidOperationException("Integrated base rig is missing.");
        var shader=Shader.Find("Leviathan/Layered Sky");
        if(shader==null || !shader.isSupported)throw new InvalidOperationException("Layered sky shader is unsupported.");
        foreach(var message in ShaderUtil.GetShaderMessages(shader))
            if(message.severity.ToString()=="Error")throw new InvalidOperationException(message.message);
        Directory.CreateDirectory(Path.GetDirectoryName(MaterialPath));
        var material=AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if(material==null) {material=new Material(shader){name="LeviathanLayeredSky"};AssetDatabase.CreateAsset(material,MaterialPath);}
        var sky=UnityEngine.Object.FindAnyObjectByType<LayeredSkyController>();
        if(sky==null)
        {
            var root=new GameObject("Ceu Procedural");root.SetActive(false);sky=root.AddComponent<LayeredSkyController>();
        }
        sky.enabled=false;
        sky.skyMaterial=material;sky.sun=GameObject.Find("Sol").GetComponent<Light>();sky.weather=rig.terrain.weatherProvider;
        if(sky.moon==null)
        {
            var moonObject=new GameObject("Lua");moonObject.transform.SetParent(sky.transform,false);
            sky.moon=moonObject.AddComponent<Light>();sky.moon.type=LightType.Directional;
            sky.moon.shadows=LightShadows.Soft;sky.moon.shadowStrength=.5f;sky.moon.intensity=0;sky.moon.enabled=false;
        }
        sky.timeOfDay=9.5f;sky.minutesPerDay=30;sky.cycleTime=true;sky.animateAtmosphere=true;
        sky.gameObject.SetActive(true);sky.enabled=true;sky.SetTimeOfDay(9.5f);
        foreach(var camera in new[] {rig.overviewCamera,rig.playerCamera,rig.stationCamera})camera.clearFlags=CameraClearFlags.Skybox;
        EditorUtility.SetDirty(sky);EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();
        if(!EditorSceneManager.SaveScene(scene))throw new InvalidOperationException("Could not save sky integration.");
        Debug.Log("LAYERED_SKY_SETUP_OK");
    }
    public static void ApplyAndValidateBatch()
    {
        try {ApplyBatch();LayeredSkyValidation.ValidateBatch();}
        catch(Exception exception) {Debug.LogException(exception);EditorApplication.Exit(1);}
    }
}
