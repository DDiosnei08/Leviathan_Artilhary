using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using ProjectLeviathan.Base;
using ProjectLeviathan.ProceduralTerrain;
using ProjectLeviathan.Vegetation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

public static class DieselpunkVisualValidation
{
    const string Key = "Leviathan.Dieselpunk.Validation";
    const string Output = "Artifacts/DieselpunkPolish/";
    static StringBuilder report;
    static int step;
    static double started, changed;
    static bool errors;
    static BaseGameRig rig;
    static ulong sceneryHash;
    static Camera waterCamera;
    static Texture2D waterFrame;
    static GameObject fixture;

    public static void ApplyAndValidateBatch()
    {
        try
        {
            EditorSceneManager.OpenScene(BaseIntegrationBuilder.ScenePath);
            var before = Object.FindAnyObjectByType<BaseGameRig>();
            ulong geometry = GeometryFingerprint(before.terrain.terrainSettings);
            Vector2 site = before.spawner.previewSite.center;
            int seed = before.terrain.terrainSettings.noiseSettings.seed;
            DieselpunkVisualSetup.ApplyBatch();
            before = Object.FindAnyObjectByType<BaseGameRig>();
            if (geometry != GeometryFingerprint(before.terrain.terrainSettings) || site != before.spawner.previewSite.center ||
                seed != before.terrain.terrainSettings.noiseSettings.seed)
                throw new InvalidOperationException("Visual setup changed terrain geometry, seed or base placement.");
            Directory.CreateDirectory(Output);
            File.WriteAllText(Output + "SetupValidation.txt", "PASS Visual setup preserves sampled natural heights, world seed and editor base placement.\nSeed=" + seed + "\nHeight fingerprint=" + geometry + "\nBase center=" + site);
            ValidateBatch();
        }
        catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
    }

    static ulong GeometryFingerprint(TerrainSettings settings)
    {
        var map = HeightMapGenerator.GenerateHeightMap(settings.CaptureGenerationSettings(false), 25, 25, Vector2.zero);
        ulong hash = 14695981039346656037;
        unchecked { foreach (float height in map.WorldHeights) hash = (hash ^ (uint)height.GetHashCode()) * 1099511628211; }
        return hash;
    }

    public static void ValidateBatch()
    {
        EditorSceneManager.OpenScene(BaseIntegrationBuilder.ScenePath);
        SessionState.SetBool(Key, true); Resume(); EditorApplication.EnterPlaymode(); EditorApplication.isPaused = false;
    }

    [InitializeOnLoadMethod]
    static void Resume()
    {
        if (!SessionState.GetBool(Key, false)) return;
        report = new StringBuilder(); step = 0; errors = false; started = changed = EditorApplication.timeSinceStartup;
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
        Application.logMessageReceived -= Log; Application.logMessageReceived += Log;
    }
    static void Log(string message, string trace, LogType type)
    {
        if (trace.Contains("UnityEditor.Search.")) return;
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) { errors = true; report.AppendLine("ERROR " + message); }
    }
    static void Check(bool condition, string label)
    { if (!condition) throw new InvalidOperationException(label); report.AppendLine("PASS " + label); }

    static void CheckShaders()
    {
        foreach (string name in new[] { DieselpunkVisualSetup.SurfaceShader, "Leviathan/Cartoon Terrain", "Leviathan/Cartoon Water", "Leviathan/Cartoon Trees", "Leviathan/Procedural Grass" })
        {
            Shader shader = Shader.Find(name);
            Check(shader != null && shader.isSupported, "Supported shader: " + name);
            foreach (var message in ShaderUtil.GetShaderMessages(shader))
                if (message.severity.ToString() == "Error") throw new InvalidOperationException(name + ": " + message.message);
        }
    }

    static void Tick()
    {
        try
        {
            double now = EditorApplication.timeSinceStartup;
            if (now - started > 180) throw new InvalidOperationException("Visual validation timeout at stage " + step);
            if (!EditorApplication.isPlaying) return;
            Application.runInBackground = true;
            rig = Object.FindAnyObjectByType<BaseGameRig>();
            var scenery = Object.FindAnyObjectByType<WorldSceneryManager>();
            var trees = Object.FindAnyObjectByType<ProceduralTreeManager>();
            if (rig.spawner.Instance == null || scenery.InstanceCount == 0 || trees.TreeCount == 0) return;
            if (step == 0) { step = 1; changed = now; return; }
            if (now - changed < 6) return;
            switch (step)
            {
                case 1:
                    Check(!errors, "Polished scene starts without gameplay or shader errors"); CheckShaders();
                    Check(Object.FindObjectsByType<BaseAssembly>().Length == 1, "One base, with no extra gameplay objects");
                    Check(Object.FindObjectsByType<Canvas>().Length == 0, "No player overlays introduced");
                    foreach (var chunk in rig.terrain.TerrainChunks.Values)
                        if (chunk.HasGeneratedMesh)
                            Check(chunk.BiomeTexture != null && chunk.MeshRenderer.sharedMaterial.GetTexture("_BaseMap") == chunk.BiomeTexture,
                                "Biome texture remains assigned on " + chunk.Coordinate);
                    Check(scenery.configuration.prototypes.All(p => p.material != null && p.material.enableInstancing), "All scenery archetypes have instanced material overrides");
                    Check(rig.grass.ResidentPatches>0 && rig.grass.GeneratedBlades>0, "GPU grass remains generated with unchanged density; blades="+rig.grass.GeneratedBlades);
                    Check(Load("Assets/Materials/BaseGiratoria/Mapa.mat").shader.name != DieselpunkVisualSetup.SurfaceShader, "Map and paper are excluded from weathering");
                    foreach (var camera in new[] { rig.overviewCamera, rig.playerCamera, rig.stationCamera })
                        Check(camera.GetUniversalAdditionalCameraData().renderPostProcessing, "Restrained color grade enabled: " + camera.name);
                    Capture("01_Base_Dieselpunk", rig.overviewCamera);
                    rig.SetView(BaseGameRig.View.LeftCannon); step = 2; changed = now; break;
                case 2:
                    Capture("02_Instrumentos", rig.stationCamera);
                    var assembly = rig.spawner.Instance.GetComponent<BaseAssembly>();
                    foreach (var wheel in assembly.GetComponentsInChildren<BaseHandwheelInteraction>())
                        if (wheel.elevation && wheel.cannon == assembly.left)
                        { float before = assembly.left.Elevation; wheel.RotateBy(30); Check(assembly.left.Elevation > before, "Physical handwheel works with polished materials"); break; }
                    rig.SetView(BaseGameRig.View.Map); step = 3; changed = now; break;
                case 3:
                    Capture("03_Mapa_Fisico", rig.stationCamera);
                    Check(rig.spawner.Instance.GetComponentInChildren<BasePhysicalMap>().surface != null, "Physical map retains its renderer");
                    rig.SetView(BaseGameRig.View.Overview);
                    Vector3 rock = scenery.NearestRock(rig.spawner.Instance.transform.position);
                    rig.overviewCamera.transform.position = rock + new Vector3(-24, 7, -28);
                    rig.overviewCamera.transform.LookAt(rock + Vector3.up * 2);
                    step = 4; changed = now; break;
                case 4:
                    Capture("04_Paisagem", rig.overviewCamera);
                    Vector3 point = FindWater(out bool found);
                    if (found)
                    {
                        rig.overviewCamera.transform.position = point + new Vector3(-20, 15, -20);
                        rig.overviewCamera.transform.LookAt(point); report.AppendLine("INFO Water capture uses a generated terrain surface.");
                    }
                    else report.AppendLine("INFO No water in the loaded window; animation is checked on an isolated shader fixture.");
                    sceneryHash = scenery.DistributionHash; CreateWaterFixture(); step = 5; changed = now; break;
                case 5:
                    if (rig.terrain.TerrainChunks.Values.Any(c => c.GeneratedWaterMesh != null)) Capture("05_Agua_No_Mundo", rig.overviewCamera);
                    waterFrame = Read(waterCamera); step = 6; changed = now; break;
                case 6:
                    var next = Read(waterCamera); var a = waterFrame.GetPixels32(); var b = next.GetPixels32(); long difference = 0;
                    for (int i = 0; i < a.Length; i++) difference += Math.Abs(a[i].r - b[i].r) + Math.Abs(a[i].g - b[i].g) + Math.Abs(a[i].b - b[i].b);
                    Check(difference > a.Length, "Water shader animates in an isolated GPU render; average channel delta=" + difference / (a.Length * 3f));
                    Object.Destroy(waterFrame); waterFrame = null; Object.Destroy(next);
                    Object.Destroy(fixture.GetComponent<MeshFilter>().sharedMesh); Object.Destroy(fixture); fixture = null;
                    sceneryHash = scenery.DistributionHash; scenery.enabled = false;
                    var owned = (System.Collections.IList)typeof(WorldSceneryManager).GetField("ownedMaterials", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(scenery);
                    Check(owned.Count == 0, "Disabling scenery releases all material overrides");
                    scenery.enabled = true; step = 7; changed = now; break;
                case 7:
                    Check(scenery.DistributionHash == sceneryHash, "Material overrides preserve deterministic scenery regeneration");
                    CheckShaders(); Check(!errors, "Rendering, controls, water animation and regeneration have no gameplay errors"); Finish(0); break;
            }
        }
        catch (Exception exception) { report.AppendLine("FAIL " + exception); Debug.LogException(exception); Finish(1); }
    }

    static Material Load(string path) => AssetDatabase.LoadAssetAtPath<Material>(path);
    static Vector3 FindWater(out bool found)
    {
        float best = 0; Vector3 result = Vector3.zero; found = false;
        foreach (var chunk in rig.terrain.TerrainChunks.Values)
        {
            Mesh mesh = chunk.GeneratedWaterMesh; if (mesh == null) continue;
            var vertices = mesh.vertices; var indices = mesh.triangles;
            for (int i = 0; i < indices.Length; i += 3)
            {
                Vector3 a = vertices[indices[i]], b = vertices[indices[i + 1]], c = vertices[indices[i + 2]];
                float area = Vector3.Cross(b - a, c - a).sqrMagnitude;
                if (area <= best) continue;
                best = area; result = chunk.GameObject.transform.TransformPoint((a + b + c) / 3); found = true;
            }
        }
        return result;
    }

    static void CreateWaterFixture()
    {
        fixture = new GameObject("Temporary water render fixture"); fixture.layer = 31; fixture.transform.position = new Vector3(14000, 4000, 14000);
        var mesh = new Mesh(); const int n = 25; var vertices = new Vector3[n * n]; var data = new Vector3[n * n]; var triangles = new int[(n - 1) * (n - 1) * 6];
        for (int y = 0; y < n; y++) for (int x = 0; x < n; x++) { vertices[y * n + x] = new Vector3(x * 1.25f, 0, y * 1.25f); data[y * n + x] = new Vector3(4, 0, 0); }
        int at = 0;
        for (int y = 0; y < n - 1; y++) for (int x = 0; x < n - 1; x++)
        { int i = y * n + x; triangles[at++] = i; triangles[at++] = i + n; triangles[at++] = i + 1; triangles[at++] = i + 1; triangles[at++] = i + n; triangles[at++] = i + n + 1; }
        mesh.vertices = vertices; mesh.SetUVs(1, data); mesh.triangles = triangles; mesh.RecalculateNormals(); mesh.RecalculateBounds();
        fixture.AddComponent<MeshFilter>().sharedMesh = mesh;
        fixture.AddComponent<MeshRenderer>().sharedMaterial = Load("Assets/Materials/Water/WaterCartoon.mat");
        var cameraObject = new GameObject("Water fixture camera"); cameraObject.transform.SetParent(fixture.transform, false);
        waterCamera = cameraObject.AddComponent<Camera>(); waterCamera.enabled = false; waterCamera.cullingMask = 1 << 31;
        waterCamera.orthographic = true; waterCamera.orthographicSize = 9; waterCamera.transform.localPosition = new Vector3(15, 22, 15); waterCamera.transform.localRotation = Quaternion.Euler(90, 0, 0);
        waterCamera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
    }

    static Texture2D Read(Camera camera)
    {
        string path = Output + "Captures/WaterFixture.png"; DownloadedAssetSurvey.Capture(camera, path, 320, 320);
        var texture = new Texture2D(2, 2); texture.LoadImage(File.ReadAllBytes(path)); return texture;
    }
    static void Capture(string name, Camera camera)
    {
        // Indirect draws queued in LateUpdate must be resubmitted for explicit Camera.Render.
        rig.grass.RenderForValidation();
        Object.FindAnyObjectByType<ProceduralTreeManager>().RenderForValidation(camera);
        Object.FindAnyObjectByType<WorldSceneryManager>().RenderForValidation(camera);
        string path = Output + "Captures/" + name + ".png"; DownloadedAssetSurvey.Capture(camera, path);
        var texture = new Texture2D(2, 2); texture.LoadImage(File.ReadAllBytes(path)); var pixels = texture.GetPixels32();
        int magenta = 0; foreach (var p in pixels) if (p.r > 220 && p.b > 220 && p.g < 60) magenta++;
        Check(magenta < pixels.Length * .001f, "No error materials in " + name);
        Check(pixels.Max(p => p.r) - pixels.Min(p => p.r) > 35, "Image has visible tonal contrast: " + name);
        Object.Destroy(texture);
    }
    static void Finish(int code)
    {
        SessionState.SetBool(Key, false); EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
        if(waterFrame!=null) Object.Destroy(waterFrame);
        if(fixture!=null) {Object.Destroy(fixture.GetComponent<MeshFilter>().sharedMesh);Object.Destroy(fixture);}
        Directory.CreateDirectory(Output); File.WriteAllText(Output + "Validation.txt", report.ToString());
        Debug.Log("DIESELPUNK_VISUAL_VALIDATION_EXIT=" + code); EditorApplication.Exit(code);
    }
}
