using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using ProjectLeviathan.Splines;
using ProjectLeviathan.Base;
using Object = UnityEngine.Object;

/// <summary>Batch-only QA of saved assets. Does not apply setup presets or save scenes.</summary>
public static class ProjectQaValidation
{
    const string Output = "Artifacts/QA/";
    static readonly StringBuilder Report = new StringBuilder();
    static int failures;

    static void Check(bool ok, string label)
    {
        Report.AppendLine((ok ? "PASS " : "FAIL ") + label);
        if (!ok) failures++;
    }

    public static void AuditBatch()
    {
        Directory.CreateDirectory(Output);
        try
        {
            ShaderUtil.allowAsyncCompilation = false;
            Report.Clear(); failures = 0;
            Report.AppendLine("INFO Unity=" + Application.unityVersion + " GPU=" + SystemInfo.graphicsDeviceName);
            var assets = AssetDatabase.GetAllAssetPaths().Where(p => p.StartsWith("Assets/", StringComparison.Ordinal) && !AssetDatabase.IsValidFolder(p)).ToArray();
            var guidPattern = new Regex(@"guid: ([0-9a-f]{32})");
            int references = 0, broken = 0;
            foreach (string path in assets)
            {
                string extension = Path.GetExtension(path).ToLowerInvariant();
                if (extension != ".unity" && extension != ".prefab" && extension != ".asset" && extension != ".mat") continue;
                string content = File.ReadAllText(path);
                if (!content.StartsWith("%YAML", StringComparison.Ordinal)) continue;
                foreach (Match match in guidPattern.Matches(content))
                {
                    string guid = match.Groups[1].Value;
                    if (guid.StartsWith("0000000000000000", StringComparison.Ordinal)) continue;
                    references++;
                    if (string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(guid)))
                    { Report.AppendLine("FAIL Unresolved GUID " + guid + " in " + path); broken++; failures++; }
                }
            }
            Check(broken == 0, "Serialized external GUIDs resolve: " + references + " references");
            foreach (var scene in EditorBuildSettings.scenes)
                Check(File.Exists(scene.path) && AssetDatabase.AssetPathToGUID(scene.path) == scene.guid.ToString(), "Build scene reference: " + scene.path);
            int missing = 0;
            foreach (string path in assets.Where(p => p.EndsWith(".prefab", StringComparison.Ordinal)))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) { Check(false, "Prefab loads: " + path); continue; }
                int count = MissingScripts(prefab);
                if (count != 0) { missing += count; Check(false, "Missing scripts in " + path + ": " + count); }
            }
            Check(missing == 0, "All prefab hierarchies have resolved scripts");
            foreach (string path in assets.Where(p => p.EndsWith(".unity", StringComparison.Ordinal)))
            {
                var scene = EditorSceneManager.OpenScene(path);
                Check(scene.GetRootGameObjects().Sum(MissingScripts) == 0, "Scene scripts resolve: " + path);
                foreach (var spline in Object.FindObjectsByType<SurfaceSplineMesh>())
                {
                    spline.Rebuild();
                    Check(spline.LastError == null && spline.GeneratedMesh != null && spline.GeneratedMesh.GetIndexCount(0) > 0,
                        "Saved spline rebuilds without recreating demo: " + spline.name);
                }
            }
            int materials = 0;
            foreach (string path in assets.Where(p => p.EndsWith(".mat", StringComparison.Ordinal)))
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null || material.shader == null || material.shader.name == "Hidden/InternalErrorShader" || !material.shader.isSupported)
                    Check(false, "Material shader valid: " + path);
                materials++;
            }
            Report.AppendLine("INFO Materials inspected=" + materials);
            foreach (string path in assets.Where(p => p.StartsWith("Assets/Shaders/", StringComparison.Ordinal)))
            {
                if (path.EndsWith(".shader", StringComparison.Ordinal))
                {
                    var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
                    Check(shader != null && shader.isSupported, "Shader supported: " + path);
                    if (shader != null) foreach (var message in ShaderUtil.GetShaderMessages(shader))
                        if (message.severity.ToString() == "Error") Check(false, path + ": " + message.message);
                }
                if (path.EndsWith(".compute", StringComparison.Ordinal))
                {
                    var compute = AssetDatabase.LoadAssetAtPath<ComputeShader>(path);
                    Check(compute != null && SystemInfo.supportsComputeShaders, "Compute supported: " + path);
                    if (compute != null) foreach (var message in ShaderUtil.GetComputeShaderMessages(compute))
                        if (message.severity.ToString() == "Error") Check(false, path + ": " + message.message);
                }
            }
        }
        catch (Exception e) { Check(false, e.ToString()); Debug.LogException(e); }
        File.WriteAllText(Output + "AssetAudit.txt", Report.ToString());
        Debug.Log("QA_ASSET_AUDIT failures=" + failures);
        EditorApplication.Exit(failures == 0 ? 0 : 1);
    }

    static int MissingScripts(GameObject root) => root.GetComponentsInChildren<Transform>(true)
        .Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));

    public static void SeedBatch()
    {
        Directory.CreateDirectory(Output); Report.Clear(); failures = 0;
        try
        {
            EditorSceneManager.OpenScene(BaseIntegrationBuilder.ScenePath);
            var spawner = Object.FindAnyObjectByType<BaseSpawner>();
            var source = spawner.heightSource;
            var terrain = source.terrain;
            var original = terrain.terrainSettings;
            var copy = Object.Instantiate(original);
            var settings = JsonUtility.FromJson<BaseSiteSettings>(JsonUtility.ToJson(spawner.settings));
            terrain.terrainSettings = copy;
            try
            {
                foreach (int seed in new[] { 0, 1, 2, 3, 4, 5, 7, 42, 100, 999, 12345, 9173, -1, -42, int.MinValue, int.MaxValue })
                {
                    copy.noiseSettings.seed = seed; source.ResetCache(); settings.seed = seed;
                    var first = BaseSiteSolver.Solve(source, settings);
                    var again = BaseSiteSolver.Solve(source, settings);
                    Check(first.valid, "Natural supported base site exists for seed=" + seed);
                    Check(first.valid == again.valid && first.center == again.center && first.groundHeight == again.groundHeight && first.yaw == again.yaw,
                        "Deterministic base solve seed=" + seed);
                    if (!first.valid) continue;
                    float min = float.PositiveInfinity, max = float.NegativeInfinity; bool dry = true;
                    for (int ring = 0; ring <= 8; ring++) for (int q = 0; q < 128; q++)
                    {
                        float a = q * Mathf.PI / 64, r = settings.checkRadius * ring / 8f;
                        float x = first.center.x + Mathf.Cos(a) * r, z = first.center.y + Mathf.Sin(a) * r;
                        float h = source.GetBaseHeight(x, z); min = Mathf.Min(min, h); max = Mathf.Max(max, h); dry &= source.IsDry(x, z);
                    }
                    Check(dry && float.IsFinite(min) && max - min <= settings.maxHeightRange + .01f,
                        "Dense independent footprint support seed=" + seed + " range=" + (max - min));
                }
            }
            finally { terrain.terrainSettings = original; source.ResetCache(); Object.DestroyImmediate(copy); }
        }
        catch (Exception e) { Check(false, e.ToString()); Debug.LogException(e); }
        File.WriteAllText(Output + "Seeds.txt", Report.ToString());
        EditorApplication.Exit(failures == 0 ? 0 : 1);
    }

    public static void BuildBatch()
    {
        Directory.CreateDirectory(Output);
        try
        {
            var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { BaseIntegrationBuilder.ScenePath,
                    "Assets/Scenes/Tests/Test_ArtilleryControls/Test_ArtilleryControls.unity", SurfaceSplineValidation.ScenePath },
                target = BuildTarget.StandaloneWindows64,
                locationPathName = Output + "Build/LeviathanQA.exe",
                options = BuildOptions.Development
            });
            var summary = result.summary;
            var report = new StringBuilder();
            report.AppendLine((summary.result == BuildResult.Succeeded ? "PASS " : "FAIL ") + "Windows development build: " + summary.result);
            report.AppendLine("INFO Bytes=" + summary.totalSize + " Duration=" + summary.totalTime + " Warnings=" + summary.totalWarnings + " Errors=" + summary.totalErrors);
            foreach (var step in result.steps) foreach (var message in step.messages)
                if (message.type == LogType.Error || message.type == LogType.Exception || message.type == LogType.Warning)
                    report.AppendLine(message.type + " " + message.content);
            File.WriteAllText(Output + "Build.txt", report.ToString());
            EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
        }
        catch (Exception e) { File.WriteAllText(Output + "Build.txt", "FAIL " + e); Debug.LogException(e); EditorApplication.Exit(1); }
    }

    public static void TuningBatch()
    {
        Report.Clear(); failures=0;
        var grass=AssetDatabase.LoadAssetAtPath<ProjectLeviathan.Grass.GrassConfiguration>("Assets/Settings/Grass/BaseGiratoriaGrass.asset");
        var field=typeof(ProjectLeviathan.Grass.GrassConfiguration).GetField("maximumResidentPatches");
        var slider=(RangeAttribute)Attribute.GetCustomAttribute(field,typeof(RangeAttribute));
        Check(slider==null || grass.maximumResidentPatches<=slider.max,
            "Inspector residency range supports configured grass budget: value="+grass.maximumResidentPatches+" sliderMax="+slider?.max);
        Directory.CreateDirectory(Output);File.WriteAllText(Output+"Tuning.txt",Report.ToString());
        EditorApplication.Exit(failures==0 ? 0 : 1);
    }
}
