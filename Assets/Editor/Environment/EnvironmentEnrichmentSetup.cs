using System;
using System.IO;
using ProjectLeviathan.Base;
using ProjectLeviathan.Grass;
using ProjectLeviathan.ProceduralTerrain;
using ProjectLeviathan.Vegetation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

public static class EnvironmentEnrichmentSetup
{
    public const string TreesPath="Assets/Settings/Vegetation/ProceduralTrees.asset";
    public const string GrassPath="Assets/Settings/Grass/BaseGiratoriaGrass.asset";
    [MenuItem("Leviathan/Environment/Enriquecer cena atual")]
    static void Menu()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        ApplyBatch();
    }
    public static void ApplyBatch()
    {
        AssetDatabase.Refresh();
        var scene=EditorSceneManager.OpenScene(BaseIntegrationBuilder.ScenePath);
        var rig=Object.FindAnyObjectByType<BaseGameRig>();var terrain=rig.terrain;var spawner=rig.spawner;
        var settings=terrain.terrainSettings;
        settings.erosion.enabled=true;settings.erosion.channelDepth=7;settings.erosion.channelScale=140;
        settings.erosion.channelWidth=.12f;settings.erosion.thermalIterations=1;settings.erosion.thermalStrength=.18f;
        settings.heightSmoothingStrength=.22f;
        foreach(var layer in settings.terrainNoiseLayers) if(layer.name=="Surface Detail")
        {layer.scaleMeters=100;layer.strength=.014f;layer.octaves=3;}
        settings.surfacePalette.dryColor=new Color(.42f,.48f,.28f);
        foreach(var biome in settings.biomePresets) if(biome.name.Contains("Fields")) biome.color=new Color(.4f,.51f,.29f);
        foreach(var biome in settings.terrainTypes) if(biome.name=="Fields") biome.color=new Color(.4f,.51f,.29f);
        settings.Validate();EditorUtility.SetDirty(settings);

        var s=spawner.settings;s.maxHeightRange=1;s.maxSlopeDeg=3;s.checkRadius=30;s.flatRadius=28;s.blendRadius=0;
        s.carveRadius=21.9f;s.exclusionRadius=30;s.prominenceWeight=0;s.candidates=1200;s.gridSteps=13;
        s.minDistance=120;s.maxDistance=1800;s.preferredDistance=600;
        s.seed=settings.noiseSettings.seed;var captured=settings.CaptureGenerationSettings(false);
        s.waterLevel=captured.EvaluateHeight(captured.lakeThreshold)*captured.heightMultiplier;s.maxAltitude=captured.heightMultiplier*2;
        s.constrainToMap=terrain.useFiniteMap;
        int radius=settings.finiteMapChunksPerSide/2;s.minimumWorldPosition=Vector2.one*(-radius*settings.chunkSize);
        s.maximumWorldPosition=Vector2.one*((radius+1)*settings.chunkSize);
        spawner.heightSource.ResetCache();var site=BaseSiteSolver.Solve(spawner.heightSource,s);
        if(!site.valid) throw new InvalidOperationException("No naturally flat, dry base site. Increase the search radius, not the slope tolerance.");
        spawner.previewSite=site;settings.RuntimeBaseSite=site;
        var preview=spawner.previewInstance;
        preview.transform.SetPositionAndRotation(new Vector3(site.center.x,site.groundHeight,site.center.y),Quaternion.Euler(0,site.yaw,0));
        var assembly=preview.GetComponent<BaseAssembly>();
        rig.player.transform.SetPositionAndRotation(assembly.playerSpawn.position,assembly.playerSpawn.rotation);
        rig.overviewCamera.transform.position=preview.transform.TransformPoint(new Vector3(-43,31,-42));
        rig.overviewCamera.transform.LookAt(preview.transform.position+Vector3.up*4);
        preview.GetComponentInChildren<BasePhysicalMap>().SetWorld(settings,site);

        Directory.CreateDirectory("Assets/Settings/Vegetation");Directory.CreateDirectory("Assets/Models/Vegetation");Directory.CreateDirectory("Assets/Materials/Vegetation");AssetDatabase.Refresh();
        var trees=AssetDatabase.LoadAssetAtPath<TreeConfiguration>(TreesPath);
        if(trees==null) {trees=ScriptableObject.CreateInstance<TreeConfiguration>();AssetDatabase.CreateAsset(trees,TreesPath);}
        trees.broadleafNear=MeshAsset("Broadleaf_LOD0",false,false);trees.broadleafFar=MeshAsset("Broadleaf_LOD1",false,true);
        trees.pineNear=MeshAsset("Pine_LOD0",true,false);trees.pineFar=MeshAsset("Pine_LOD1",true,true);
        const string materialPath="Assets/Materials/Vegetation/TreeCartoon.mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if(material==null) {material=new Material(Shader.Find("Leviathan/Cartoon Trees")) {enableInstancing=true};AssetDatabase.CreateAsset(material,materialPath);}
        trees.material=material;EditorUtility.SetDirty(trees);
        var manager=Object.FindAnyObjectByType<ProceduralTreeManager>();
        if(manager==null) manager=new GameObject("Arvores Procedurais").AddComponent<ProceduralTreeManager>();
        manager.enabled=false;manager.terrain=terrain;manager.configuration=trees;manager.enabled=true;

        var grass=AssetDatabase.LoadAssetAtPath<GrassConfiguration>(GrassPath);
        if(grass==null) {grass=Object.Instantiate(rig.grass.configuration);grass.name="BaseGiratoriaGrass";AssetDatabase.CreateAsset(grass,GrassPath);}
        grass.bladesPerSquareMeter=9;grass.heightRange=new Vector2(.55f,.95f);grass.widthRange=new Vector2(.045f,.075f);
        grass.nearDistance=28;grass.middleDistance=65;grass.farDistance=120;grass.maximumResidentPatches=80;
        grass.middleDensity=.65f;grass.farDensity=.22f;grass.generationsPerFrame=3;
        grass.maximumSlope=32;grass.biomeRange=new Vector2(.08f,.9f);grass.biomeColorInfluence=.18f;
        rig.grass.configuration=grass;EditorUtility.SetDirty(grass);
        terrain.RegenerateTerrain();EditorUtility.SetDirty(spawner);EditorUtility.SetDirty(manager);EditorUtility.SetDirty(rig.grass);
        EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        Directory.CreateDirectory("Artifacts/EnvironmentEnrichment");
        File.WriteAllText("Artifacts/EnvironmentEnrichment/Setup.txt","Seed="+site.seed+"\nNatural site="+site.center+"\nGround="+site.groundHeight+"\nRange limit="+s.maxHeightRange+"\nGrass blades/m2="+grass.bladesPerSquareMeter);
        Debug.Log("ENVIRONMENT_ENRICHMENT_SETUP_OK "+site.center);
    }
    static Mesh MeshAsset(string name,bool pine,bool far)
    {
        string path="Assets/Models/Vegetation/"+name+".asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(mesh!=null) return mesh;
        mesh=TreeMeshFactory.Create(pine,far);AssetDatabase.CreateAsset(mesh,path);return mesh;
    }
}
