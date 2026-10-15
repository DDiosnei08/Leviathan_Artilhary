using System;
using System.IO;
using ProjectLeviathan.Base;
using ProjectLeviathan.Grass;
using ProjectLeviathan.ProceduralTerrain;
using ProjectLeviathan.Vegetation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

public static class DieselpunkVisualSetup
{
    public const string GradePath = "Assets/Settings/Volumes/DieselpunkGrade.asset";
    public const string SurfaceShader = "Leviathan/Dieselpunk Surface";
    const string EnvironmentMaterials = "Assets/Materials/Environment/";

    [MenuItem("Leviathan/Visual/Aplicar acabamento dieselpunk")]
    static void Menu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        ApplyBatch();
    }

    public static void ApplyBatch()
    {
        AssetDatabase.Refresh();
        var scene = EditorSceneManager.OpenScene(BaseIntegrationBuilder.ScenePath);
        var rig = Object.FindAnyObjectByType<BaseGameRig>();
        if (rig == null) throw new InvalidOperationException("Open the integrated base scene first.");
        var settings = rig.terrain.terrainSettings;
        ApplyTerrain(settings);
        ApplyVegetation(rig.grass.configuration);
        ApplyScenery();
        ApplyBaseMaterials();
        ApplyAtmosphere(rig);
        settings.RuntimeBaseSite = rig.spawner.previewSite;
        rig.terrain.RegenerateTerrain();
        foreach (var trees in Object.FindObjectsByType<ProceduralTreeManager>()) { trees.enabled = false; trees.enabled = true; }
        foreach (var scenery in Object.FindObjectsByType<WorldSceneryManager>()) { scenery.enabled = false; scenery.enabled = true; }
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(scene);
        Debug.Log("DIESELPUNK_VISUAL_SETUP_OK: materials, world palette and atmosphere applied without changing terrain geometry.");
    }

    static Material Load(string path) => AssetDatabase.LoadAssetAtPath<Material>(path) ??
        throw new InvalidOperationException("Missing material: " + path);

    static void ApplyTerrain(TerrainSettings settings)
    {
        Color[] palette = {
            new Color(.12f,.25f,.27f), new Color(.32f,.35f,.28f), new Color(.35f,.43f,.36f),
            new Color(.48f,.46f,.33f), new Color(.38f,.42f,.45f), new Color(.64f,.69f,.69f)
        };
        for (int i = 0; i < settings.biomePresets.Count; i++) settings.biomePresets[i].color = palette[Mathf.Min(i, palette.Length - 1)];
        for (int i = 0; i < settings.terrainTypes.Count; i++) settings.terrainTypes[i].color = palette[Mathf.Min(i, palette.Length - 1)];
        var p = settings.surfacePalette;
        p.dryColor = new Color(.49f,.44f,.34f); p.wetColor = new Color(.22f,.33f,.30f);
        p.coldColor = new Color(.57f,.65f,.66f); p.rockyColor = new Color(.34f,.39f,.43f);
        settings.oceanColor = palette[0];
        settings.roadMaterial = SurfaceMaterial("IndustrialRoad", new Color(.30f,.29f,.25f), 0, .1f, 0, .12f);
        settings.roadMaterial.SetFloat("_WeatherScale", .3f);
        EditorUtility.SetDirty(settings.roadMaterial); EditorUtility.SetDirty(settings);
        var terrain = Load("Assets/Materials/Terrain/TerrainCartoon.mat");
        terrain.SetFloat("_ColorIntensity", 1.02f); terrain.SetFloat("_ShadowContrast", .62f);
        terrain.SetFloat("_BandSoftness", .13f); terrain.SetFloat("_DetailScale", .13f); terrain.SetFloat("_DetailStrength", .085f);
        terrain.SetFloat("_PatchScale", .011f); terrain.SetFloat("_PatchStrength", .22f); terrain.SetFloat("_SlopeStrength", .27f);
        terrain.SetColor("_MineralTint", new Color(.47f,.42f,.34f)); terrain.SetColor("_SlopeTint", new Color(.36f,.42f,.46f));
        terrain.SetColor("_ShadowTint", new Color(.45f,.56f,.61f)); EditorUtility.SetDirty(terrain);
        var water = Load("Assets/Materials/Water/WaterCartoon.mat");
        water.SetColor("_ShallowColor", new Color(.16f,.38f,.38f)); water.SetColor("_DeepColor", new Color(.065f,.18f,.24f));
        water.SetColor("_PatternColor", new Color(.48f,.67f,.67f)); water.SetColor("_FoamColor", new Color(.78f,.84f,.77f));
        water.SetColor("_SkyReflectionColor", new Color(.48f,.60f,.64f)); water.SetColor("_ShadowTint", new Color(.43f,.56f,.61f));
        water.SetFloat("_PatternStrength", .19f); water.SetFloat("_FoamStrength", .52f); water.SetFloat("_WaveHeight", .075f);
        water.SetFloat("_FresnelStrength", .22f); water.SetFloat("_SpecularStrength", .32f); EditorUtility.SetDirty(water);
    }

    static void ApplyVegetation(GrassConfiguration grass)
    {
        grass.rootColor = new Color(.14f,.25f,.20f); grass.tipColor = new Color(.48f,.55f,.36f);
        grass.translucency = .26f; grass.specular = .045f; EditorUtility.SetDirty(grass);
        var trees = Load("Assets/Materials/Vegetation/TreeCartoon.mat");
        trees.SetFloat("_Saturation", .74f); trees.SetFloat("_LeafTintStrength", .25f); trees.SetFloat("_ColorVariation", .12f);
        trees.SetColor("_LeafTint", new Color(.78f,.87f,.78f)); trees.SetColor("_ShadowTint", new Color(.42f,.54f,.55f));
        EditorUtility.SetDirty(trees);
    }

    static Material SurfaceMaterial(string name, Color color, float metal, float smoothness, float wear, float grime)
    {
        string path = EnvironmentMaterials + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            var shader = Shader.Find(SurfaceShader) ?? throw new InvalidOperationException("Missing dieselpunk surface shader.");
            material = new Material(shader); AssetDatabase.CreateAsset(material, path);
        }
        StyleSurface(material, color, metal, smoothness, wear, grime);
        return material;
    }

    static void StyleSurface(Material material, Color color, float metal, float smoothness, float wear, float grime)
    {
        material.shader = Shader.Find(SurfaceShader); material.shaderKeywords = Array.Empty<string>(); material.enableInstancing = true;
        material.SetColor("_BaseColor", color); material.SetFloat("_Metallic", metal); material.SetFloat("_Smoothness", smoothness);
        material.SetFloat("_WearAmount", wear); material.SetFloat("_GrimeStrength", grime); material.SetFloat("_GrainStrength", .065f);
        material.SetFloat("_WeatherScale", 1.7f); material.SetFloat("_Saturation", .82f);
        material.SetColor("_WearColor", new Color(.37f,.25f,.18f)); material.SetColor("_GrimeColor", new Color(.41f,.39f,.32f));
        EditorUtility.SetDirty(material);
    }

    static void ApplyScenery()
    {
        var rocks = Load(EnvironmentMaterials + "CuratedProps.mat"); StyleSurface(rocks, Color.white, 0, .12f, 0, .12f);
        var pole = SurfaceMaterial("UtilityPole", Color.white, .45f, .32f, .055f, .13f);
        pole.SetFloat("_VertexMetalMask", 1);
        var concrete = SurfaceMaterial("ConcreteBarrier", new Color(.86f,.93f,.97f), 0, .09f, 0, .13f);
        var fabric = SurfaceMaterial("SandbagFabric", new Color(.88f,.87f,.73f), 0, .06f, 0, .17f);
        var wood = SurfaceMaterial("SupplyWood", new Color(.86f,.85f,.78f), 0, .14f, 0, .12f);
        var configuration = AssetDatabase.LoadAssetAtPath<WorldSceneryConfiguration>(AssetCurationSetup.ConfigPath);
        foreach (var prototype in configuration.prototypes)
        {
            if (prototype == null) continue;
            prototype.material = prototype.role == SceneryRole.Lamp ? pole : prototype.role == SceneryRole.Crate ? wood :
                prototype.role == SceneryRole.Barrier ? (prototype.label.Contains("Sandbag") ? fabric : concrete) : rocks;
        }
        foreach (var material in new[] { rocks, pole, concrete, fabric, wood })
        { material.SetFloat("_VertexColorStrength", 1); EditorUtility.SetDirty(material); }
        EditorUtility.SetDirty(configuration);
    }

    static void ApplyBaseMaterials()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets/Materials/BaseGiratoria" }))
        {
            var material = Load(AssetDatabase.GUIDToAssetPath(guid));
            string name = material.name;
            Color color; float metal = .2f, smoothness = .32f, wear = .055f, grime = .1f;
            switch (name)
            {
                case "FBX_Verde_Militar": color = new Color(.27f,.35f,.32f); break;
                case "FBX_Canhao_Verde_Escuro": color = new Color(.15f,.25f,.25f); metal = .3f; break;
                case "FBX_Aco_escuro": color = new Color(.19f,.24f,.27f); metal = .7f; wear = .09f; break;
                case "FBX_Aco_oxidado": color = new Color(.39f,.23f,.16f); metal = .3f; smoothness = .19f; wear = .15f; break;
                case "FBX_Concreto": color = new Color(.48f,.51f,.51f); metal = 0; smoothness = .08f; wear = 0; grime = .15f; break;
                case "FBX_Latao": case "FBX_Projetil_Latao": color = new Color(.63f,.49f,.25f); metal = .75f; smoothness = .43f; wear = .025f; break;
                case "FBX_Amarelo_Seguranca": color = new Color(.88f,.65f,.2f); metal = .05f; wear = .06f; break;
                case "Volantes": color = new Color(.38f,.46f,.43f); metal = .55f; wear = .06f; break;
                case "Mesa": color = new Color(.30f,.38f,.36f); break;
                case "Instrumentos": color = new Color(.19f,.29f,.29f); break;
                case "Botoes": case "Aro": color = new Color(.20f,.27f,.28f); metal = .4f; wear = .025f; break;
                default: continue; // Keep paper, map texture, ink, needles and semantic signal colors legible.
            }
            StyleSurface(material, color, metal, smoothness, wear, grime);
            if (name.Contains("Latao")) material.SetColor("_WearColor", new Color(.37f,.38f,.23f));
            EditorUtility.SetDirty(material);
        }
    }

    static void ApplyAtmosphere(BaseGameRig rig)
    {
        var sun = GameObject.Find("Sol").GetComponent<Light>();
        sun.transform.rotation = Quaternion.Euler(38, -32, 0); sun.color = new Color(1, .95f, .85f); sun.intensity = 1.35f;
        sun.shadows = LightShadows.Soft; sun.shadowStrength = .82f; RenderSettings.sun = sun;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(.51f,.61f,.66f);
        RenderSettings.ambientEquatorColor = new Color(.43f,.47f,.45f); RenderSettings.ambientGroundColor = new Color(.25f,.27f,.26f);
        RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(.65f,.71f,.71f); RenderSettings.fogStartDistance = 650; RenderSettings.fogEndDistance = 2800;
        const string skyPath = EnvironmentMaterials + "DieselpunkSky.mat";
        var sky = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
        if (sky == null) { sky = new Material(Shader.Find("Skybox/Procedural")); AssetDatabase.CreateAsset(sky, skyPath); }
        sky.SetColor("_SkyTint", new Color(.52f,.61f,.64f)); sky.SetColor("_GroundColor", new Color(.29f,.32f,.32f));
        sky.SetFloat("_Exposure", 1.15f); sky.SetFloat("_AtmosphereThickness", 1.2f); sky.SetFloat("_SunSize", .025f);
        RenderSettings.skybox = sky; EditorUtility.SetDirty(sky);
        var grade = AssetDatabase.LoadAssetAtPath<VolumeProfile>(GradePath);
        if (grade == null) { grade = ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(grade, GradePath); }
        if (!grade.TryGet<Tonemapping>(out var tone)) { tone = grade.Add<Tonemapping>(); AssetDatabase.AddObjectToAsset(tone, grade); }
        tone.mode.Override(TonemappingMode.Neutral);
        if (!grade.TryGet<ColorAdjustments>(out var color)) { color = grade.Add<ColorAdjustments>(); AssetDatabase.AddObjectToAsset(color, grade); }
        color.postExposure.Override(.12f); color.contrast.Override(8); color.saturation.Override(-4);
        EditorUtility.SetDirty(tone); EditorUtility.SetDirty(color); EditorUtility.SetDirty(grade);
        var volume = GameObject.Find("Atmosfera Dieselpunk")?.GetComponent<Volume>();
        if (volume == null) volume = new GameObject("Atmosfera Dieselpunk").AddComponent<Volume>();
        volume.isGlobal = true; volume.priority = 2; volume.sharedProfile = grade;
        foreach (var camera in new[] { rig.overviewCamera, rig.playerCamera, rig.stationCamera })
        {
            camera.clearFlags = CameraClearFlags.Skybox; camera.backgroundColor = RenderSettings.fogColor;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        }
        DynamicGI.UpdateEnvironment();
    }
}
