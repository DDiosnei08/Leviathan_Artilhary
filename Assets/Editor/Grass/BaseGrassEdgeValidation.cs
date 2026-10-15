using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Text;
using ProjectLeviathan.Base;
using ProjectLeviathan.Grass;
using ProjectLeviathan.ProceduralTerrain;
using ProjectLeviathan.Vegetation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Run only in a dedicated Editor session: these batch entry points exit Unity when finished.
public static class BaseGrassEdgeValidation
{
    const string Key = "Leviathan.BaseGrassEdge.Validation";
    const string Output = "Artifacts/GrassBaseEdge/";
    static readonly BindingFlags Fields = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    static StringBuilder report;
    static int mode, step;
    static double started, changed;
    static bool errors;
    static GrassManager manager;
    static TerrainSettings fixedSettings;
    static TerrainChunk[] fixedChunks;
    static BaseSiteData site;
    static BaseGameRig rig;
    static uint distribution;
    static int seed;
    static long maximumGc;
    static int gcFrames;

    struct Blade { public Vector3 position; public uint shape, normal, bend, color, seed; }

    public static void FixedBatch() { Begin(1); }
    public static void IntegratedBatch() { Begin(2); }
    static void Begin(int value)
    {
        if (value == 1)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var blocker = new GameObject("Disable automatic terrain bootstrap");
            blocker.SetActive(false); blocker.AddComponent<TerrainWorldController>();
        }
        else EditorSceneManager.OpenScene(BaseIntegrationBuilder.ScenePath);
        SessionState.SetInt(Key, value); Resume(); EditorApplication.EnterPlaymode(); EditorApplication.isPaused = false;
    }
    [InitializeOnLoadMethod]
    static void Resume()
    {
        mode = SessionState.GetInt(Key, 0); if (mode == 0) return;
        report = new StringBuilder(); errors = false; step = 0; maximumGc = 0; gcFrames = 0;
        started = changed = EditorApplication.timeSinceStartup;
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
        Application.logMessageReceived -= Log; Application.logMessageReceived += Log;
    }
    static void Log(string message, string trace, LogType type)
    {
        if (trace.Contains("UnityEditor.Search.")) return;
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
        { errors = true; report.AppendLine("ERROR " + message); }
    }
    static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); report.AppendLine("PASS " + message); }
    static void Tick()
    {
        try
        {
            double now = EditorApplication.timeSinceStartup;
            if (now - started > 180) throw new InvalidOperationException("Edge validation timeout at step " + step);
            if (!EditorApplication.isPlaying) return;
            Application.runInBackground = true;
            if (step == 0)
            {
                if (mode == 1) CreateFixed();
                else { rig = Object.FindAnyObjectByType<BaseGameRig>(); manager = rig.grass; }
                step = 1; changed = now; return;
            }
            if (now - changed < (mode == 1 ? 2 : 10)) return;
            if (mode == 1) FixedStep(now); else IntegratedStep(now);
        }
        catch (Exception exception) { report.AppendLine("FAIL " + exception); Debug.LogException(exception); Finish(1); }
    }
    static void CreateFixed()
    {
        fixedSettings = ScriptableObject.CreateInstance<TerrainSettings>(); fixedSettings.Validate();
        fixedSettings.chunkSize = 30; fixedSettings.chunkResolution = 4; fixedSettings.heightMultiplier = 1;
        fixedSettings.generateWaterSurfaces = fixedSettings.generateProceduralProps = false;
        fixedSettings.terrainMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        site = new BaseSiteData { valid = true, seed = 42, center = new Vector2(30, 15), carveRadius = 4.75f, exclusionRadius = 18 };
        fixedChunks = new TerrainChunk[2];
        for (int i = 0; i < 2; i++)
        {
            fixedChunks[i] = new TerrainChunk(new Vector2Int(i, 0), fixedSettings, new GameObject("Fixed edge tile " + i).transform);
            ApplyFixed(i);
        }
        var camera = new GameObject("Edge test camera").AddComponent<Camera>();
        camera.transform.position = new Vector3(30, 12, 15); camera.transform.rotation = Quaternion.Euler(90, 0, 0);
        var config = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GrassConfiguration>("Assets/Settings/Grass/BaseGiratoriaGrass.asset"));
        config.culling = false; config.windStrength = 0; config.useWorldWeather = false; config.maximumResidentPatches = 4;
        config.collectGpuStatistics = false;
        var root = new GameObject("Fixed edge grass"); root.SetActive(false);
        manager = root.AddComponent<GrassManager>(); manager.configuration = config; manager.renderCamera = camera;
        root.SetActive(true); foreach (var chunk in fixedChunks) manager.AttachChunk(chunk);
    }
    static void ApplyFixed(int i)
    {
        const int n = 4;
        var map = new HeightMap(new float[n, n], biomes: new float[n, n], worldHeights: new float[n, n],
            baseSite: site, worldOrigin: new Vector2(i * 30, 0));
        for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
        { map.Values[x, y] = map.Biomes[x, y] = map.WorldHeights[x, y] = 0.5f; }
        fixedChunks[i].ApplyGeneratedData(MeshGenerator.GenerateTerrainMesh(map.Values, fixedSettings), map);
    }
    static void FixedStep(double now)
    {
        if (step == 1)
        {
            Check(manager.ResidentPatches == 2, "Two adjacent fixed tiles generated, with 10 m terrain cells");
            CheckShader(); CheckDistribution(true);
            distribution = manager.ReadDistributionHashForValidation(); manager.Rebuild(); step = 2;
        }
        else if (step == 2)
        {
            Check(distribution == manager.ReadDistributionHashForValidation(), "GPU edge distribution is deterministic after rebuild");
            site.center += new Vector2(3, 0); for (int i = 0; i < 2; i++) ApplyFixed(i); step = 3;
        }
        else if (step == 3)
        {
            CheckDistribution(true);
            Check(distribution != manager.ReadDistributionHashForValidation(), "A relocated base invalidates grass even on identical flat terrain");
            site.valid = false; for (int i = 0; i < 2; i++) ApplyFixed(i); step = 4;
        }
        else
        {
            CheckDistribution(true);
            Check(!errors, "GPU generation has no compilation or runtime errors");
            manager.enabled = false;
            Check(manager.ActiveTiles == 0 && manager.BufferBytes == 0, "Disabling grass releases tile buffers");
            Finish(0); return;
        }
        changed = now;
    }
    static object Field(object value, string name) => value.GetType().GetField(name, Fields).GetValue(value);
    static void CheckDistribution(bool fixedTest)
    {
        int total = 0, near = 0, mismatches = 0, seamLeft = 0, seamRight = 0;
        float closest = float.PositiveInfinity;
        var tiles = (IDictionary)Field(manager, "tiles");
        foreach (DictionaryEntry entry in tiles)
        {
            var tile = (GrassTile)entry.Value;
            var map = (HeightMap)Field(tile, "uploadedMap");
            if (map == null) continue;
            var tileSite = map.BaseSite;
            float radius = tileSite.carveRadius + .25f + manager.configuration.baseClearance;
            foreach (DictionaryEntry item in (IDictionary)Field(tile, "patches"))
            {
                object patch = item.Value; if (!(bool)Field(patch, "generated")) continue;
                var buffer = (ComputeBuffer)Field(Field(patch, "buffers"), "blades");
                var blades = new Blade[buffer.count]; buffer.GetData(blades);
                var origin = (Vector2)Field(patch, "origin"); var global = (Vector2Int)Field(patch, "global");
                int grid = manager.configuration.GridResolution;
                for (int id = 0; id < blades.Length; id++)
                {
                    var blade = blades[id];
                    if (fixedTest)
                    {
                        uint s = Hash(unchecked((uint)(global.x * grid + id % grid)) ^ Hash(unchecked((uint)(global.y * grid + id / grid))) ^
                            unchecked((uint)(manager.configuration.seed ^ fixedSettings.noiseSettings.seed)));
                        var p = origin + new Vector2(id % grid + Mathf.Lerp(.06f, .94f, Random(s + 1)), id / grid + Mathf.Lerp(.06f, .94f, Random(s + 2))) * (30f / grid);
                        Vector3 world = tile.terrain.GameObject.transform.TransformPoint(new Vector3(p.x, .485f, p.y));
                        float density = Density(new Vector2(world.x, world.z), tileSite);
                        float random = Random(s + 3);
                        bool expected = density > 0 && random <= density;
                        if (expected != (blade.shape != 0) && Mathf.Abs(density - random) > .0001f) mismatches++;
                    }
                    if (blade.shape == 0) continue;
                    total++;
                    Vector3 root = tile.terrain.GameObject.transform.TransformPoint(blade.position);
                    if (!tileSite.valid) continue;
                    float distance = Vector2.Distance(new Vector2(root.x, root.z), tileSite.center);
                    CheckInside(distance >= radius - .001f, "Blade inside concrete clearance: " + distance);
                    closest = Mathf.Min(closest, distance);
                    if (distance < radius + 3.5f) near++;
                    if (fixedTest && Mathf.Abs(root.x - 30) < 1 && distance < 12)
                    { if (root.x < 30) seamLeft++; else seamRight++; }
                }
            }
        }
        Check(total > 1000, "Grass generated: " + total);
        if (fixedTest) Check(mismatches == 0, "Every candidate agrees with world-space edge math across both chunks; mismatches=" + mismatches);
        if (fixedTest && site.valid) Check(seamLeft > 40 && seamRight > 40, "No extra exclusion stripe on chunk seam: " + seamLeft + "/" + seamRight);
        if ((!fixedTest && rig.spawner.Site.valid) || (fixedTest && site.valid))
        {
            Check(near > 40, "Grass returns close to concrete instead of the old 30 m scenery exclusion; near blades=" + near);
            report.AppendLine("INFO Closest accepted root distance=" + closest);
        }
        if (fixedTest && !site.valid) Check(total == manager.configuration.CandidateCount * 2, "No base means no radial grass exclusion");
    }
    static void CheckInside(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    static uint Hash(uint value)
    {
        unchecked { uint state = value * 747796405u + 2891336453u; uint word = ((state >> (int)((state >> 28) + 4)) ^ state) * 277803737u; return (word >> 22) ^ word; }
    }
    static float Random(uint value) => (Hash(value) & 0xffffffu) / 16777216f;
    static float Noise(Vector2 p)
    {
        int x = Mathf.FloorToInt(p.x), y = Mathf.FloorToInt(p.y);
        float fx = p.x - x, fy = p.y - y; fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
        uint s = unchecked((uint)(manager.configuration.seed ^ fixedSettings.noiseSettings.seed)) ^ 0x6b8b4567u;
        float Sample(int a, int b) => Random(unchecked((uint)a) ^ Hash(unchecked((uint)b)) ^ s);
        return Mathf.Lerp(Mathf.Lerp(Sample(x, y), Sample(x + 1, y), fx), Mathf.Lerp(Sample(x, y + 1), Sample(x + 1, y + 1), fx), fy);
    }
    static float Density(Vector2 world, BaseSiteData data)
    {
        if (!data.valid) return 1;
        var c = manager.configuration;
        float edge = Vector2.Distance(world, data.center) - data.carveRadius - .25f - c.baseClearance;
        if (edge <= 0) return 0;
        if (edge >= c.baseEdgeFade + c.baseEdgeVariation) return 1;
        return Mathf.SmoothStep(0, 1, (edge - Noise(world * c.baseEdgeNoiseScale) * c.baseEdgeVariation) / c.baseEdgeFade);
    }
    static void IntegratedStep(double now)
    {
        if (rig.spawner.Instance == null || manager.ResidentPatches == 0) return;
        foreach (var chunk in rig.terrain.TerrainChunks.Values) if (!chunk.HasGeneratedMesh || !chunk.WorldFeaturesReady) return;
        if (step == 1)
        {
            CheckShader(); site = rig.spawner.Site; seed = rig.terrain.terrainSettings.noiseSettings.seed;
            Check(Object.FindObjectsByType<BaseAssembly>().Length == 1, "One integrated base remains");
            Check(site.exclusionRadius >= 30, "Scenery and tree exclusion radius is preserved");
            CheckDistribution(false); manager.ValidateRootsForValidation();
            Capture("01_Base_Grama_Proxima"); rig.terrain.RegenerateTerrain(); step = 2; changed = now;
        }
        else if (step == 2)
        {
            Check(site.center == rig.spawner.Site.center && seed == rig.terrain.terrainSettings.noiseSettings.seed, "Regeneration preserves seed and base position");
            CheckDistribution(false); manager.ValidateRootsForValidation();
            Check(Object.FindObjectsByType<Canvas>().Length == 0, "No overlays introduced");
            Capture("02_Apos_Regenerar"); step = 3; changed = now;
        }
        else
        {
            maximumGc = Math.Max(maximumGc, manager.LastUpdateAllocatedBytes); if (++gcFrames < 30) return;
            Check(maximumGc == 0, "Grass LateUpdate remains zero GC over 30 steady-state samples");
            Check(!errors, "Integrated Play and terrain regeneration have no errors"); Finish(0);
        }
    }
    static void CheckShader()
    {
        var compute = AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/Shaders/Grass/ProceduralGrass.compute");
        foreach (var message in ShaderUtil.GetComputeShaderMessages(compute))
            if (message.severity.ToString() == "Error") throw new InvalidOperationException(message.message);
        Check(SystemInfo.supportsComputeShaders, "Compute shader supported and compiles on " + SystemInfo.graphicsDeviceName);
    }
    static void Capture(string name)
    {
        manager.RenderForValidation();
        Object.FindAnyObjectByType<ProceduralTreeManager>().RenderForValidation(rig.overviewCamera);
        Object.FindAnyObjectByType<WorldSceneryManager>().RenderForValidation(rig.overviewCamera);
        DownloadedAssetSurvey.Capture(rig.overviewCamera, Output + "Captures/" + name + ".png");
    }
    static void Finish(int code)
    {
        SessionState.SetInt(Key, 0); EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
        Directory.CreateDirectory(Output); File.WriteAllText(Output + (mode == 1 ? "FixedValidation.txt" : "IntegratedValidation.txt"), report.ToString());
        Debug.Log("BASE_GRASS_EDGE_VALIDATION_EXIT=" + code); EditorApplication.Exit(code);
    }
}
