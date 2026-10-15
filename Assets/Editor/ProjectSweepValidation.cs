using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using ProjectLeviathan.Base;
using ProjectLeviathan.ProceduralTerrain;
using ProjectLeviathan.Vegetation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class ProjectSweepValidation
{
    const string Output = "Artifacts/ProjectSweep/";
    const string SessionKey = "Leviathan.ProjectSweep.Validation";
    static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly StringBuilder report = new StringBuilder();
    static double started, ready;
    static bool errors;

    // Dependency absence is a review candidate, never an automatic deletion decision.
    public static void AuditBatch()
    {
        try { Audit(); EditorApplication.Exit(0); }
        catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
    }

    [MenuItem("Tools/Leviathan/Validation/Write Asset Dependency Audit")]
    public static void Audit()
    {
        Directory.CreateDirectory(Output);
        string[] paths = AssetDatabase.GetAllAssetPaths().Where(p => p.StartsWith("Assets/", StringComparison.Ordinal) &&
            !AssetDatabase.IsValidFolder(p)).OrderBy(p => p).ToArray();
        var referencedBy = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (string path in paths)
            foreach (string dependency in AssetDatabase.GetDependencies(path, false))
            {
                if (dependency == path) continue;
                if (!referencedBy.TryGetValue(dependency, out var references))
                    referencedBy[dependency] = references = new List<string>();
                references.Add(path);
            }
        var rootPaths = new HashSet<string>(StringComparer.Ordinal) {
            BaseIntegrationBuilder.ScenePath,
            "Assets/Scenes/Tests/Test_ArtilleryControls/Test_ArtilleryControls.unity"
        };
        foreach (string settingsPath in Directory.GetFiles("ProjectSettings", "*.asset"))
            foreach (Match match in Regex.Matches(File.ReadAllText(settingsPath), @"guid: ([0-9a-f]{32})"))
            {
                string path = AssetDatabase.GUIDToAssetPath(match.Groups[1].Value);
                if (!string.IsNullOrEmpty(path)) rootPaths.Add(path);
            }
        var roots = new HashSet<string>(AssetDatabase.GetDependencies(rootPaths.ToArray(), true), StringComparer.Ordinal);
        var audit = new StringBuilder("Asset\tScene dependency\tDirect referrer count\tDirect referrers\n");
        foreach (string path in paths)
        {
            referencedBy.TryGetValue(path, out var references);
            audit.Append(path).Append('\t').Append(roots.Contains(path)).Append('\t')
                .Append(references?.Count ?? 0).Append('\t')
                .AppendLine(references == null ? "" : string.Join(";", references));
        }
        File.WriteAllText(Output + "AssetDependencies.tsv", audit.ToString());
        File.WriteAllLines(Output + "ReviewCandidates.txt", paths.Where(p => !roots.Contains(p) && !referencedBy.ContainsKey(p)));
        Debug.Log($"PROJECT_SWEEP_AUDIT: {paths.Length} assets, {roots.Count} dependencies of scenes and ProjectSettings.");
    }

    public static void ValidateBatch()
    {
        try
        {
            Audit();
            ProjectOrganizationValidator.ValidateAssets();
            EditorSceneManager.OpenScene(BaseIntegrationBuilder.ScenePath);
            SessionState.SetBool(SessionKey, true);
            Resume();
            EditorApplication.EnterPlaymode();
            EditorApplication.isPaused = false;
        }
        catch (Exception exception) { report.AppendLine("FAIL " + exception); Finish(1); }
    }

    [InitializeOnLoadMethod]
    static void Resume()
    {
        if (!SessionState.GetBool(SessionKey, false)) return;
        report.Clear(); started = EditorApplication.timeSinceStartup; ready = 0; errors = false;
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
        Application.logMessageReceived -= Log; Application.logMessageReceived += Log;
    }

    static void Log(string message, string trace, LogType type)
    {
        if (trace.Contains("UnityEditor.Search.")) return;
        if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
        errors = true; report.AppendLine("ERROR " + message);
    }

    static void Check(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException(label);
        report.AppendLine("PASS " + label);
    }

    static Action Method(object target, string name) => (Action)Delegate.CreateDelegate(typeof(Action), target,
        target.GetType().GetMethod(name, Private));

    static long Allocation(Action action, int iterations)
    {
        for (int i = 0; i < 10; i++) action();
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < iterations; i++) action();
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    static void Tick()
    {
        try
        {
            double now = EditorApplication.timeSinceStartup;
            if (now - started > 150) throw new InvalidOperationException("Project sweep Play validation timeout.");
            if (!EditorApplication.isPlaying) return;
            Application.runInBackground = true;
            var rig = Object.FindAnyObjectByType<BaseGameRig>();
            var scenery = Object.FindAnyObjectByType<WorldSceneryManager>();
            if (rig == null || rig.spawner.Instance == null || scenery == null || scenery.InstanceCount == 0) return;
            if (ready == 0) ready = now;
            if (now - ready < 15) return;
            Check(!errors, "Integrated scene starts without Unity errors");
            Check(Object.FindObjectsByType<BaseAssembly>().Length == 1, "Exactly one base in Play");
            Check(Object.FindObjectsByType<Canvas>().Length == 0, "No overlay UI");
            ValidateTerrain(rig.terrain);
            ValidateColliders(scenery);
            ValidateCapture(rig.overviewCamera);
            Check(!errors, "No Unity errors during regression tests");
            Finish(0);
        }
        catch (Exception exception) { report.AppendLine("FAIL " + exception); Finish(1); }
    }

    static void ValidateTerrain(TerrainWorldController terrain)
    {
        long settingsBytes = Allocation(terrain.terrainSettings.Validate, 1000);
        Check(settingsBytes == 0, "TerrainSettings.Validate: " + settingsBytes + " allocated bytes / 1000 calls after warmup");
        long terrainBytes = Allocation(Method(terrain, "Update"), 100);
        Check(terrainBytes == 0, "Steady-state terrain Update: " + terrainBytes + " allocated bytes / 100 calls");
        Action triangles = () => { _ = terrain.GeneratedTriangleCount; };
        Check(Allocation(triangles, 100) == 0, "Terrain triangle statistics do not copy mesh index arrays");
        int expected = terrain.TerrainChunks.Values.Where(c => c.MeshFilter.sharedMesh != null)
            .Sum(c => c.MeshFilter.sharedMesh.triangles.Length / 3);
        Check(terrain.GeneratedTriangleCount == expected, "Triangle statistics retain the correct count");
        var settings = Object.Instantiate(terrain.terrainSettings);
        try
        {
            Check(settings.poiDefinitions.Count > 0, "POI validation regression has a definition to duplicate");
            settings.poiDefinitions.Add(settings.poiDefinitions[0]);
            bool rejected = false;
            try { settings.Validate(); } catch (InvalidOperationException) { rejected = true; }
            Check(rejected, "Duplicate POI identifiers are still rejected");
        }
        finally { Object.Destroy(settings); }
    }

    static MeshCollider[] ActiveColliders(WorldSceneryManager scenery) =>
        scenery.GetComponentsInChildren<MeshCollider>().Where(c => c.enabled).ToArray();

    static void ValidateCapture(Camera camera)
    {
        string path = Output + "CaptureSmoke/Frame.png";
        DownloadedAssetSurvey.Capture(camera, path, 320, 180);
        Check(File.Exists(path), "Capture creates its output directory in a fresh project");
        RenderTexture previous = camera.targetTexture;
        RenderTexture active = RenderTexture.active;
        bool failed = false;
        try { DownloadedAssetSurvey.Capture(camera, Output, 64, 64); }
        catch (IOException) { failed = true; }
        catch (UnauthorizedAccessException) { failed = true; }
        Check(failed, "Capture test exercises a real output-file failure");
        Check(camera.targetTexture == previous && RenderTexture.active == active,
            "Camera and active render target are restored after capture failure");
    }

    static void ValidateColliders(WorldSceneryManager scenery)
    {
        var configuration = scenery.configuration;
        float distance = configuration.collisionDistance;
        int capacity = configuration.maximumColliders;
        Transform player = scenery.player;
        var probe = new GameObject("ProjectSweep collider test probe");
        Action refresh = Method(scenery, "UpdateColliders");
        try
        {
            probe.transform.position = scenery.NearestRock(player.position);
            scenery.player = probe.transform;
            configuration.collisionDistance = 500;
            configuration.maximumColliders = 8;
            refresh();
            var active = ActiveColliders(scenery);
            Check(active.Length == 8, "Collider pool respects a reduced live capacity");
            var distances = new List<float>();
            var tiles = (IDictionary)typeof(WorldSceneryManager).GetField("tiles", Private).GetValue(scenery);
            foreach (DictionaryEntry pair in tiles)
            {
                object tile = pair.Value;
                var chunk = (TerrainChunk)tile.GetType().GetField("chunk").GetValue(tile);
                if (!chunk.GameObject.activeInHierarchy) continue;
                var instances = (Array)tile.GetType().GetField("instances").GetValue(tile);
                if (instances == null) continue;
                foreach (object instance in instances)
                {
                    Vector3 position = (Vector3)instance.GetType().GetField("position").GetValue(instance);
                    float d = (position - probe.transform.position).sqrMagnitude;
                    if (d <= 500 * 500) distances.Add(d);
                }
            }
            distances.Sort();
            float[] actual = active.Select(c => (c.transform.position - probe.transform.position).sqrMagnitude).OrderBy(d => d).ToArray();
            Check(actual.Where((d, i) => Mathf.Abs(d - distances[i]) > .01f).Count() == 0,
                "Pool selects the nearest objects, not the first streamed chunk");
            Check(Allocation(refresh, 100) == 0, "Stable collider refresh: zero managed allocation / 100 calls");
            configuration.collisionDistance = 0; refresh();
            Check(ActiveColliders(scenery).Length == 0, "Zero collision distance disables existing colliders");
            configuration.collisionDistance = 500; refresh(); scenery.player = null; refresh();
            Check(ActiveColliders(scenery).Length == 0, "Missing player clears stale colliders");
            scenery.player = probe.transform; configuration.maximumColliders = 0; refresh();
            Check(ActiveColliders(scenery).Length == 0, "Zero collider budget disables collision safely");
            configuration.maximumColliders = 8; refresh();
            scenery.enabled = false;
            Check(ActiveColliders(scenery).Length == 0, "Disabling scenery disables its colliders immediately");
            scenery.enabled = true;
        }
        finally
        {
            configuration.collisionDistance = distance; configuration.maximumColliders = capacity;
            scenery.player = player; Object.Destroy(probe);
        }
    }

    static void Finish(int code)
    {
        SessionState.SetBool(SessionKey, false);
        EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
        Directory.CreateDirectory(Output);
        File.WriteAllText(Output + "Validation.txt", report.ToString());
        Debug.Log("PROJECT_SWEEP_VALIDATION_EXIT=" + code);
        EditorApplication.Exit(code);
    }
}
