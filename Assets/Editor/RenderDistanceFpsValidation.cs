using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Text;
using ProjectLeviathan.Base;
using ProjectLeviathan.Diagnostics;
using ProjectLeviathan.Grass;
using ProjectLeviathan.Vegetation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;

public static class RenderDistanceFpsValidation
{
    const string SessionKey = "Leviathan.RenderDistanceFps.Validation";
    const string Output = "Artifacts/RenderDistanceFps/";
    static readonly BindingFlags Fields = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    static StringBuilder report;
    static double started, changed;
    static int step, sampleCount, seed, readyFrame;
    static bool errors;
    static BaseGameRig rig;
    static TestFpsCounter counter;
    static Keyboard keyboard;
    static Vector2 baseCenter;
    static float previousTimeScale;
    struct Blade { public Vector3 position; public uint shape, normal, bend, color, seed; }

    // Dedicated batch Editor only: exits Unity after the tests, without saving Play changes.
    public static void ValidateBatch()
    {
        EditorSceneManager.OpenScene(BaseIntegrationBuilder.ScenePath);
        SessionState.SetBool(SessionKey, true); Resume(); EditorApplication.EnterPlaymode(); EditorApplication.isPaused = false;
    }
    [InitializeOnLoadMethod]
    static void Resume()
    {
        if (!SessionState.GetBool(SessionKey, false)) return;
        report = new StringBuilder(); step = 0; errors = false; started = changed = EditorApplication.timeSinceStartup;
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
            if (now - started > 240) throw new InvalidOperationException("Distance/FPS test timeout at step " + step);
            if (!EditorApplication.isPlaying) return;
            Application.runInBackground = true;
            if (step == 0)
            {
                rig = Object.FindAnyObjectByType<BaseGameRig>(); counter = Object.FindAnyObjectByType<TestFpsCounter>();
                previousTimeScale = Time.timeScale; step = 1; changed = now; return;
            }
            double delay = step == 1 || step == 8 ? 15 : step == 6 || step == 7 ? 8 : 1;
            if (now - changed < delay) return;
            switch (step)
            {
                case 1:
                    foreach (var chunk in rig.terrain.TerrainChunks.Values) if (!chunk.HasGeneratedMesh || !chunk.WorldFeaturesReady) return;
                    Check(counter != null && counter.enabled && counter.visible && counter.developmentOnly, "FPS counter is integrated and restricted to test builds");
                    Check(counter.SampleCount > 0 && counter.FramesPerSecond > 0 && float.IsFinite(counter.FrameMilliseconds), "FPS and frame time are sampled");
                    Check(Mathf.Abs(counter.FramesPerSecond * counter.FrameMilliseconds - 1000) < .01f, "FPS and ms use the same averaged frame interval");
                    var label = (GUIContent)Field(counter, "label");
                    Check(label.text.StartsWith("FPS: ") && label.text.EndsWith(" ms") && !label.text.Contains("--"), "Display label contains measured values");
                    Check(rig.terrain.terrainSettings.finiteMapStreamingRadiusChunks == 2 && rig.terrain.TerrainChunks.Count == 25, "Terrain streams a 5 x 5 window");
                    Check(rig.grass.configuration.farDistance == 240 && rig.grass.configuration.maximumResidentPatches == 320, "Grass distance and bounded residency increased together");
                    Check(rig.grass.ResidentPatches > 80 && rig.grass.ResidentPatches <= 320, "Extended coverage is not capped by the old 80-patch budget: " + rig.grass.ResidentPatches);
                    Check(Object.FindAnyObjectByType<ProceduralTreeManager>().configuration.farDistance == 1400, "Trees render to 1.4 km");
                    Check(Object.FindAnyObjectByType<WorldSceneryManager>().configuration.drawDistance == 900, "Scenery renders to 900 m");
                    Check(rig.playerCamera.farClipPlane >= 4000 && rig.overviewCamera.farClipPlane >= 4000 && rig.stationCamera.farClipPlane >= 4000, "Camera far planes accommodate the extended world");
                    CheckExtendedGrass(); rig.grass.ValidateRootsForValidation();
                    Check(Object.FindObjectsByType<Canvas>().Length == 0, "FPS diagnostic introduces no Canvas or gameplay HUD");
                    Capture("01_Visao_Geral");
                    baseCenter = rig.spawner.Site.center; seed = rig.terrain.terrainSettings.noiseSettings.seed;
                    sampleCount = counter.SampleCount; Time.timeScale = 0; step = 2; break;
                case 2:
                    Check(counter.SampleCount > sampleCount && counter.FramesPerSecond > 0, "FPS uses unscaled time and continues while gameplay time is paused");
                    Time.timeScale = previousTimeScale;
                    keyboard = InputSystem.AddDevice<Keyboard>(); keyboard.MakeCurrent();
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F8)); step = 3; break;
                case 3:
                    Check(!counter.visible, "F8 hides the diagnostic");
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState()); step = 4; break;
                case 4:
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F8)); step = 5; break;
                case 5:
                    Check(counter.visible, "F8 shows the diagnostic again");
                    InputSystem.RemoveDevice(keyboard); keyboard = null;
                    rig.SetView(BaseGameRig.View.LeftCannon); step = 6; break;
                case 6:
                    Check(counter.visible && counter.FramesPerSecond > 0, "Counter remains active across camera changes");
                    Check(rig.grass.renderCamera == rig.stationCamera, "Grass follows the station camera frustum");
                    Capture("02_Instrumentos");
                    rig.SetView(BaseGameRig.View.Player); rig.player.enabled = false;
                    var assembly = rig.spawner.Instance.GetComponent<BaseAssembly>();
                    rig.playerCamera.transform.position = assembly.leftStation.position + Vector3.up * 3;
                    rig.playerCamera.transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(assembly.left.ShotDirection, Vector3.up).normalized - Vector3.up * .08f);
                    step = 7; break;
                case 7:
                    CheckExtendedGrass(150); Capture("03_Visao_Conves");
                    rig.SetView(BaseGameRig.View.Overview); rig.terrain.RegenerateTerrain(); step = 8; break;
                case 8:
                    foreach (var chunk in rig.terrain.TerrainChunks.Values) if (!chunk.HasGeneratedMesh || !chunk.WorldFeaturesReady) return;
                    Check(baseCenter == rig.spawner.Site.center && seed == rig.terrain.terrainSettings.noiseSettings.seed, "Regeneration preserves world seed and base location");
                    Check(Object.FindObjectsByType<BaseAssembly>().Length == 1, "One base remains after regeneration");
                    readyFrame = Time.frameCount; step = 9; break;
                case 9:
                    // Terrain readiness precedes the budgeted grass generation; wait in rendered frames.
                    if (Time.frameCount - readyFrame < Mathf.CeilToInt((float)rig.grass.configuration.maximumResidentPatches / rig.grass.configuration.generationsPerFrame) + 2) return;
                    rig.grass.ValidateRootsForValidation(); CheckExtendedGrass();
                    Check(!errors, "Extended scene, FPS input and regeneration run without errors");
                    report.AppendLine("INFO Offscreen camera captures do not include the IMGUI FPS overlay.");
                    report.AppendLine("INFO Grass buffer pool bytes=" + rig.grass.BufferBytes + "; texture bytes=" + rig.grass.TextureBytes);
                    Finish(0); return;
            }
            changed = now;
        }
        catch (Exception exception) { report.AppendLine("FAIL " + exception); Debug.LogException(exception); Finish(1); }
    }
    static object Field(object value, string name) => value.GetType().GetField(name, Fields).GetValue(value);
    static void CheckExtendedGrass(float minimumVisibleDistance = 200)
    {
        int beyondOldDistance = 0; float maximum = 0;
        foreach (DictionaryEntry entry in (IDictionary)Field(rig.grass, "tiles"))
        {
            var tile = (GrassTile)entry.Value;
            foreach (DictionaryEntry patchEntry in (IDictionary)Field(tile, "patches"))
            {
                object buffers = Field(patchEntry.Value, "buffers");
                var source = (ComputeBuffer)Field(buffers, "blades");
                var blades = new Blade[source.count]; source.GetData(blades);
                var commands = new GraphicsBuffer.IndirectDrawIndexedArgs[4];
                ((GraphicsBuffer)Field(buffers, "args")).GetData(commands);
                var visible = (ComputeBuffer[])Field(buffers, "visible");
                var indices = new uint[source.count];
                for (int lod = 0; lod < 3; lod++)
                {
                    int count = (int)commands[lod].instanceCount; if (count == 0) continue;
                    visible[lod].GetData(indices, 0, 0, count);
                    for (int i = 0; i < count; i++)
                    {
                        Vector3 root = tile.terrain.GameObject.transform.TransformPoint(blades[indices[i]].position);
                        float distance = Vector3.Distance(root, rig.grass.renderCamera.transform.position);
                        maximum = Mathf.Max(maximum, distance); if (distance > 130) beyondOldDistance++;
                    }
                }
            }
        }
        Check(beyondOldDistance > 500 && maximum > minimumVisibleDistance && maximum <= 240.01f,
            "GPU culling draws grass beyond old range and respects 240 m: distant=" + beyondOldDistance + ", max=" + maximum);
    }
    static void Capture(string name)
    {
        var camera = rig.CurrentView == BaseGameRig.View.Overview ? rig.overviewCamera :
            rig.CurrentView == BaseGameRig.View.Player ? rig.playerCamera : rig.stationCamera;
        rig.grass.RenderForValidation();
        Object.FindAnyObjectByType<ProceduralTreeManager>().RenderForValidation(camera);
        Object.FindAnyObjectByType<WorldSceneryManager>().RenderForValidation(camera);
        DownloadedAssetSurvey.Capture(camera, Output + "Captures/" + name + ".png");
    }
    static void Finish(int code)
    {
        Time.timeScale = previousTimeScale; if (keyboard != null) InputSystem.RemoveDevice(keyboard);
        SessionState.SetBool(SessionKey, false); EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
        Directory.CreateDirectory(Output); File.WriteAllText(Output + "Validation.txt", report.ToString());
        Debug.Log("RENDER_DISTANCE_FPS_VALIDATION_EXIT=" + code); EditorApplication.Exit(code);
    }
}
