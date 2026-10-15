using System;
using System.IO;
using ProjectLeviathan.Grass;
using ProjectLeviathan.ProceduralTerrain;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

public static class GrassValidation
{
    public const string ConfigPath = "Assets/Settings/Grass/ProceduralGrass.asset";
    public const string DemoPath = "Assets/Scenes/Demos/Base_Cartum_Grama.unity";
    const string Key = "Leviathan.Grass.Validation";
    static GrassManager manager;
    static Camera camera;
    static TerrainChunk fixedChunk;
    static TerrainSettings fixedSettings;
    static uint hash;
    static int step, phase;
    static double started, changed;
    static bool errors;
    static Texture2D windFrame;
    static long maximumGc;
    static int gcSamples;

    public static GrassConfiguration EnsureAssets()
    {
        Directory.CreateDirectory("Assets/Materials/Grass");
        Directory.CreateDirectory("Assets/Settings/Grass");
        AssetDatabase.Refresh();
        Shader shader = Shader.Find("Leviathan/Procedural Grass");
        if (shader == null) throw new InvalidOperationException("Grass shader missing.");
        string path = "Assets/Materials/Grass/ProceduralGrass.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null) { material = new Material(shader) { enableInstancing = true }; AssetDatabase.CreateAsset(material, path); }
        GrassConfiguration config = AssetDatabase.LoadAssetAtPath<GrassConfiguration>(ConfigPath);
        if (config == null) { config = ScriptableObject.CreateInstance<GrassConfiguration>(); AssetDatabase.CreateAsset(config, ConfigPath); }
        config.compute = AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/Shaders/Grass/ProceduralGrass.compute");
        config.material = material;
        EditorUtility.SetDirty(config); AssetDatabase.SaveAssets();
        CheckShaders();
        return config;
    }
    static void CheckShaders()
    {
        Shader shader = Shader.Find("Leviathan/Procedural Grass");
        if (shader == null || !shader.isSupported) throw new InvalidOperationException("Grass shader unsupported.");
        foreach (var message in ShaderUtil.GetShaderMessages(shader))
            if (message.severity.ToString() == "Error") throw new InvalidOperationException(message.message);
        var compute = AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/Shaders/Grass/ProceduralGrass.compute");
        foreach (var message in ShaderUtil.GetComputeShaderMessages(compute))
            if (message.severity.ToString() == "Error") throw new InvalidOperationException(message.message);
    }
    public static void Stage1Batch() { Begin(1); }
    public static void Stage2Batch() { Begin(2); }
    public static void Stage3Batch() { Begin(3); }
    public static void Stage4Batch() { Begin(4); }
    public static void Stage5Batch() { Begin(5); }
    static void Begin(int phase)
    {
        EnsureAssets();
        if (phase <= 2)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject blocker = new GameObject("Disable automatic terrain bootstrap");
            blocker.SetActive(false); blocker.AddComponent<TerrainWorldController>();
        }
        else { BuildDemoBatch(); EditorSceneManager.OpenScene(DemoPath); }
        SessionState.SetInt(Key, phase); Resume(); EditorApplication.EnterPlaymode();
    }
    [InitializeOnLoadMethod]
    static void Resume()
    {
        phase = SessionState.GetInt(Key, 0);
        if (phase == 0) return;
        step = 0; started = EditorApplication.timeSinceStartup; changed = started; errors = false;
        EditorApplication.update -= Update; EditorApplication.update += Update;
        Application.logMessageReceived -= Track; Application.logMessageReceived += Track;
    }
    static void Track(string message, string trace, LogType type)
    {
        if (trace.Contains("UnityEditor.Search.")) return;
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors = true;
    }
    static void CreateFixed()
    {
        fixedSettings = ScriptableObject.CreateInstance<TerrainSettings>();
        fixedSettings.Validate(); fixedSettings.chunkSize = 30; fixedSettings.chunkResolution = 9;
        fixedSettings.heightMultiplier = 1; fixedSettings.generateWaterSurfaces = false; fixedSettings.generateProceduralProps = false;
        fixedSettings.terrainMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        fixedSettings.terrainMaterial.color = new Color(0.22f, 0.28f, 0.16f);
        HeightMap map = new HeightMap(new float[9,9], biomes: new float[9,9], worldHeights: new float[9,9]);
        for (int y = 0; y < 9; y++) for (int x = 0; x < 9; x++) { map.Values[x,y] = 0.5f; map.Biomes[x,y] = 0.5f; map.WorldHeights[x,y] = 0.5f; }
        fixedChunk = new TerrainChunk(Vector2Int.zero, fixedSettings, new GameObject("Fixed grass terrain").transform);
        fixedChunk.ApplyGeneratedData(MeshGenerator.GenerateTerrainMesh(map.Values, fixedSettings), map);
        camera = new GameObject("Grass test camera").AddComponent<Camera>();
        camera.transform.position = new Vector3(15, 2.2f, 3); camera.transform.LookAt(new Vector3(15, 0.8f, 20));
        camera.nearClipPlane = 0.1f; camera.farClipPlane = 200; camera.backgroundColor = new Color(0.4f,0.6f,0.75f);
        Light light = new GameObject("Grass test sun").AddComponent<Light>();
        light.type = LightType.Directional; light.intensity = 1.2f; light.shadows = LightShadows.Soft;
        light.transform.rotation = Quaternion.Euler(45, -30, 0);
        GrassConfiguration config = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GrassConfiguration>(ConfigPath));
        config.culling = phase != 1; config.windStrength = 0; config.useWorldWeather = false;
        config.maximumResidentPatches = 4;
        GameObject root = new GameObject("Fixed grass patch"); root.SetActive(false);
        manager = root.AddComponent<GrassManager>(); manager.configuration = config; manager.renderCamera = camera;
        root.SetActive(true); manager.AttachChunk(fixedChunk);
    }
    static void Update()
    {
        try
        {
            double now = EditorApplication.timeSinceStartup;
            if (now - started > 180) throw new InvalidOperationException("Grass validation timeout at step " + step);
            if (!EditorApplication.isPlaying) return;
            if (step == 0)
            {
                if (phase <= 2) CreateFixed();
                else { manager = Object.FindAnyObjectByType<GrassManager>(); camera = manager.renderCamera; }
                step = 1; changed = now; return;
            }
            if (now - changed < (phase <= 2 ? 1 : 4)) return;
            if (phase <= 2) FixedStep(now); else IntegratedStep(now);
        }
        catch (Exception e) { Debug.LogException(e); Finish(1); }
    }
    static void FixedStep(double now)
    {
        uint[] counts = manager.ReadVisibleCountsForValidation();
        uint visible = counts[0] + counts[1] + counts[2];
        if (step == 1)
        {
            if (manager.ResidentPatches != 1 || visible < 1000) throw new InvalidOperationException("Fixed patch blank: " + visible);
            CheckShaders(); Object.DestroyImmediate(Capture(camera, "Stage" + phase + "_Fixed.png"));
            hash = manager.ReadDistributionHashForValidation();
            Debug.Log("GRASS_FIXED_OK: visible=" + visible + ", hash=" + hash + ", bytes=" + manager.BufferBytes);
            manager.Rebuild(); step = 2;
        }
        else if (step == 2)
        {
            if (hash != manager.ReadDistributionHashForValidation()) throw new InvalidOperationException("Grass distribution not deterministic.");
            Debug.Log("GRASS_DETERMINISM_OK");
            if (phase == 1) { Finish(errors ? 1 : 0); return; }
            camera.transform.position = new Vector3(15, 5, 3);
            camera.transform.rotation = Quaternion.Euler(-90,0,0); step = 3;
        }
        else if (step == 3)
        {
            if (visible != 0 || counts[3] == 0) throw new InvalidOperationException("Frustum/shadow culling invalid: visible=" + visible + " shadows=" + counts[3]);
            camera.transform.position = new Vector3(15, 150, 15); step = 4;
        }
        else if (step == 4)
        {
            if (visible != 0) throw new InvalidOperationException("Distance culling failed.");
            Debug.Log("GRASS_CULLING_OK: frustum, distance, off-camera near shadows.");
            Finish(errors ? 1 : 0); return;
        }
        changed = now;
    }
    static void IntegratedStep(double now)
    {
        if (manager.ResidentPatches == 0) return;
        if (step <= 3)
            foreach (TerrainChunk chunk in manager.terrain.TerrainChunks.Values) if (!chunk.HasGeneratedMesh) return;
        if (step == 1)
        {
            CheckShaders();
            var rig = Object.FindAnyObjectByType<IntegratedBattlefieldRig>();
            if (rig == null || rig.artillery == null) throw new InvalidOperationException("Base/cannon lost.");
            if (Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Length != 0) throw new InvalidOperationException("Unexpected player Canvas.");
            uint[] counts = manager.ReadVisibleCountsForValidation();
            Debug.Log("GRASS_INTEGRATION_COUNTS: " + string.Join(",", counts));
            Object.DestroyImmediate(Capture(manager.renderCamera, "Stage" + phase + "_Base.png"));
            manager.terrain.RegenerateTerrain(); step = 2;
        }
        else if (step == 2)
        {
            if (manager.ResidentPatches > manager.configuration.maximumResidentPatches) throw new InvalidOperationException("Residency budget exceeded.");
            Debug.Log("GRASS_TILES_REGENERATION_OK: tiles=" + manager.ActiveTiles + ", patches=" + manager.ResidentPatches);
            manager.enabled = false;
            if (manager.BufferBytes != 0) throw new InvalidOperationException("Buffers not released on disable.");
            manager.enabled = true; step = 3;
        }
        else if (step == 3)
        {
            Debug.Log("GRASS_REENABLE_OK");
            Object.DestroyImmediate(Capture(manager.renderCamera, "Stage" + phase + "_Reenabled.png"));
            if (phase >= 4)
            {
                Object.FindAnyObjectByType<GrassShowcase>().ShowGrass();
                step = 7;
            }
            else
            {
                manager.terrain.enabled = false;
                ApplyLod(1); step = 4;
            }
        }
        else if (step == 4)
        {
            Debug.Log("GRASS_ROOTS_LOD1_OK: maximum error=" + manager.ValidateRootsForValidation());
            ApplyLod(2); step = 5;
        }
        else if (step == 5)
        {
            Debug.Log("GRASS_ROOTS_LOD2_OK: maximum error=" + manager.ValidateRootsForValidation());
            ApplyLod(0); step = 6;
        }
        else if (step == 6)
        {
            Debug.Log("GRASS_ROOTS_LOD0_OK: maximum error=" + manager.ValidateRootsForValidation());
            manager.terrain.enabled = true;
            Finish(errors ? 1 : 0); return;
        }
        else if (step == 7)
        {
            Debug.Log("GRASS_MASKS_ROOTS_OK: maximum error=" + manager.ValidateRootsForValidation());
            if (phase == 5)
            {
                maximumGc = 0; gcSamples = 0; step = 9;
            }
            else { windFrame = Capture(manager.renderCamera, "Stage4_Grass_A.png"); step = 8; }
        }
        else if (step == 8)
        {
            Texture2D next = Capture(manager.renderCamera, "Stage4_Grass_B.png");
            Color32[] a = windFrame.GetPixels32(), b = next.GetPixels32();
            int changedPixels = 0;
            for (int i = 0; i < a.Length; i++)
                if (a[i].g > a[i].r * 1.1f && a[i].g > a[i].b &&
                    Mathf.Abs(a[i].r-b[i].r) + Mathf.Abs(a[i].g-b[i].g) + Mathf.Abs(a[i].b-b[i].b) > 12) changedPixels++;
            Object.DestroyImmediate(windFrame); Object.DestroyImmediate(next);
            if (changedPixels < 500) throw new InvalidOperationException("Grass wind is static: " + changedPixels);
            Debug.Log("GRASS_WIND_ANIMATION_OK: changed green pixels=" + changedPixels);
            manager.UseCompatibilityForValidation(); step = 11;
        }
        else if (step == 9)
        {
            maximumGc = Math.Max(maximumGc, manager.LastUpdateAllocatedBytes); gcSamples++;
            if (gcSamples < 120) return;
            Debug.Log("GRASS_UPDATE_GC: max=" + maximumGc + " bytes over " + gcSamples + " stable samples.");
            if (maximumGc != 0) throw new InvalidOperationException("Steady grass update allocates managed memory: " + maximumGc);
            Vector3 p = manager.renderCamera.transform.position;
            p.x += manager.terrain.terrainSettings.chunkSize;
            p.y = ProceduralBattlefieldSetup.SampleHeight(manager.terrain.terrainSettings, p) + 2;
            manager.renderCamera.transform.position = p;
            step = 10;
        }
        else if (step == 10)
        {
            foreach (TerrainChunk chunk in manager.terrain.TerrainChunks.Values) if (!chunk.HasGeneratedMesh) return;
            if (manager.ActiveTiles != manager.terrain.GeneratedChunkCount || manager.ResidentPatches > manager.configuration.maximumResidentPatches)
                throw new InvalidOperationException("Streaming tile count or pool residency invalid.");
            manager.ValidateRootsForValidation();
            Debug.Log("GRASS_STREAMING_OK: tiles=" + manager.ActiveTiles + ", leased patches=" + manager.ResidentPatches + ", buffer bytes=" + manager.BufferBytes);
            Finish(errors ? 1 : 0); return;
        }
        else if (step == 11)
        {
            Object.DestroyImmediate(Capture(manager.renderCamera, "Stage4_Compatibility.png"));
            Debug.Log("GRASS_COMPATIBILITY_RENDER_OK");
            Finish(errors ? 1 : 0); return;
        }
        changed = now;
    }
    static void ApplyLod(int lod)
    {
        foreach (TerrainChunk chunk in manager.terrain.TerrainChunks.Values)
        {
            float distance = lod == 0 ? 0 : lod == 1 ? 2000 : 3000;
            chunk.UpdateLod(chunk.CenterWorldPosition + Vector3.right * distance);
            HeightMap map = chunk.GeneratedData;
            chunk.ApplyGeneratedData(MeshGenerator.GenerateTerrainMesh(map.Values, chunk.Settings, chunk.CurrentLod, map.NormalHeights), map);
        }
    }
    [MenuItem("Leviathan/Grass/Create Integrated Demo")]
    public static void BuildDemoMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        BuildDemoBatch();
    }
    public static void BuildDemoBatch()
    {
        GrassConfiguration config = EnsureAssets();
        var scene = EditorSceneManager.OpenScene(CartoonShaderSceneBuilder.ScenePath);
        IntegratedBattlefieldRig rig = Object.FindAnyObjectByType<IntegratedBattlefieldRig>();
        rig.terrain.RegenerateTerrain();
        GrassManager grass = Object.FindAnyObjectByType<GrassManager>();
        if (grass == null) grass = new GameObject("Procedural Grass").AddComponent<GrassManager>();
        grass.configuration = config; grass.terrain = rig.terrain; grass.renderCamera = rig.overviewCamera; grass.player = rig.player.transform;
        Transform foundation = rig.transform.Find("Foundation");
        var exclusion = foundation.GetComponent<GrassExclusionVolume>();
        if (exclusion == null) exclusion = foundation.gameObject.AddComponent<GrassExclusionVolume>();
        exclusion.size = Vector3.one;
        grass.exclusionVolumes = new[] { exclusion };
        Transform ramp = rig.transform.Find("AccessRamp");
        if (ramp != null)
        {
            var rampExclusion = ramp.GetComponent<GrassExclusionVolume>();
            if (rampExclusion == null) rampExclusion = ramp.gameObject.AddComponent<GrassExclusionVolume>();
            rampExclusion.size = Vector3.one;
            grass.exclusionVolumes = new[] { exclusion, rampExclusion };
        }
        GrassShowcase showcase = rig.GetComponent<GrassShowcase>();
        if (showcase == null) showcase = rig.gameObject.AddComponent<GrassShowcase>();
        showcase.rig = rig; showcase.baseCameraPosition = rig.overviewCamera.transform.position;
        showcase.baseCameraRotation = rig.overviewCamera.transform.rotation;
        float best = float.PositiveInfinity;
        foreach (TerrainChunk chunk in rig.terrain.TerrainChunks.Values)
        {
            HeightMap map = chunk.GeneratedData;
            if (map == null) continue;
            for (int z = 2; z < map.Height - 2; z++) for (int x = 2; x < map.Width - 2; x++)
            {
                bool eligible = true;
                for (int dz = -1; dz <= 2; dz++) for (int dx = -1; dx <= 2; dx++)
                {
                    int xx = x + dx, zz = z + dz;
                    if (map.Water[xx,zz] > 0.01f || map.Roads[xx,zz] > 0.01f || map.PoiMask[xx,zz] > 0.01f ||
                        map.Slopes[xx,zz] > 18 || map.Values[xx,zz] < config.normalizedHeightRange.x || map.Values[xx,zz] > config.normalizedHeightRange.y ||
                        map.Biomes[xx,zz] < config.biomeRange.x || map.Biomes[xx,zz] > config.biomeRange.y) eligible = false;
                }
                if (!eligible) continue;
                float h = (map.WorldHeights[x+1,z] + map.WorldHeights[x,z+1]) * 0.5f;
                Vector3 p = chunk.GameObject.transform.TransformPoint(new Vector3((x+0.5f) * chunk.Settings.CellSize, h, chunk.Settings.chunkSize - (z+0.5f) * chunk.Settings.CellSize));
                float distance = Vector3.Distance(p, rig.transform.position);
                if (distance < 60 || distance >= best) continue;
                best = distance;
                showcase.grassCameraPosition = p + Vector3.up * 1.25f;
                float targetHeight = (map.WorldHeights[x+2,z] + map.WorldHeights[x+1,z+1]) * 0.5f;
                showcase.grassCameraTarget = p + new Vector3(chunk.Settings.CellSize, targetHeight - h + 0.45f, 0);
            }
        }
        if (float.IsPositiveInfinity(best)) throw new InvalidOperationException("No eligible grass viewpoint found in the loaded procedural world.");
        EditorSceneManager.SaveScene(scene, DemoPath); AssetDatabase.SaveAssets();
        Debug.Log("GRASS_DEMO_BUILT: " + DemoPath);
    }
    public static void BuildBenchmarkBatch()
    {
        BuildDemoBatch();
        var scene = EditorSceneManager.OpenScene(DemoPath);
        GrassBenchmark benchmark = new GameObject("Standalone grass benchmark").AddComponent<GrassBenchmark>();
        benchmark.manager = Object.FindAnyObjectByType<GrassManager>();
        const string scenePath = "Assets/Scenes/Tests/GrassBenchmark.unity";
        EditorSceneManager.SaveScene(scene, scenePath);
        Directory.CreateDirectory("Artifacts/Grass/Standalone");
        PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
        PlayerSettings.defaultScreenWidth = 1920; PlayerSettings.defaultScreenHeight = 1080;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed; PlayerSettings.runInBackground = true;
        PlayerSettings.enableFrameTimingStats = true;
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { scenePath }, locationPathName = "Artifacts/Grass/Standalone/GrassBenchmark.exe",
            target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development
        });
        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) throw new InvalidOperationException("Grass benchmark build failed.");
        Debug.Log("GRASS_BENCHMARK_BUILD_OK");
    }
    static Texture2D Capture(Camera camera, string name)
    {
        var target = new RenderTexture(1920,1080,24);
        var image = new Texture2D(1920,1080,TextureFormat.RGB24,false);
        RenderTexture previous = RenderTexture.active;
        camera.targetTexture = target;
        manager.RenderForValidation(); camera.Render();
        manager.RenderForValidation(); camera.Render();
        RenderTexture.active = target;
        image.ReadPixels(new Rect(0,0,1920,1080),0,0); image.Apply();
        int green = 0, magenta = 0;
        foreach (Color32 p in image.GetPixels32()) { if (p.g > p.r * 1.15f && p.g > p.b * 1.1f) green++; if (p.r > 220 && p.b > 220 && p.g < 30) magenta++; }
        Directory.CreateDirectory("Artifacts/Grass/Captures"); File.WriteAllBytes("Artifacts/Grass/Captures/" + name, image.EncodeToPNG());
        camera.targetTexture = null; RenderTexture.active = previous; target.Release(); Object.DestroyImmediate(target);
        if (magenta > 100 || green < 5000) throw new InvalidOperationException("Grass capture blank/error: green=" + green + ", magenta=" + magenta);
        Debug.Log("GRASS_CAPTURE_OK: " + name + ", green pixels=" + green);
        return image;
    }
    static void Finish(int code)
    {
        SessionState.SetInt(Key,0); EditorApplication.update -= Update; Application.logMessageReceived -= Track;
        Debug.Log("GRASS_STAGE_" + phase + "_" + (code == 0 ? "OK" : "FAILED"));
        EditorApplication.Exit(code);
    }
}
