using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ProjectLeviathan.Base;
using ProjectLeviathan.Splines;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Copied only into the isolated QA build, never installed in the game project.
public sealed class QaPlayerSmokeProbe : MonoBehaviour
{
    readonly StringBuilder report = new StringBuilder();
    readonly List<float> frameMs = new List<float>(4096);
    string output;
    float started, changed;
    int stage, frame, viewIndex, failures;
    BaseGameRig rig;
    readonly BaseGameRig.View[] views = { BaseGameRig.View.LeftCannon, BaseGameRig.View.RightCannon, BaseGameRig.View.Map, BaseGameRig.View.Player };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        var args = Environment.GetCommandLineArgs();
        int flag = Array.IndexOf(args, "-leviathan-qa-output");
        if (flag < 0 || flag + 1 >= args.Length) return;
        var root = new GameObject("QA standalone probe");
        DontDestroyOnLoad(root);
        var probe = root.AddComponent<QaPlayerSmokeProbe>();
        probe.output = args[flag + 1];
    }
    void Start()
    {
        Directory.CreateDirectory(output);
        Application.runInBackground = true;
        QualitySettings.vSyncCount = 0; Application.targetFrameRate = -1;
        started = changed = Time.realtimeSinceStartup;
        Application.logMessageReceived += Log;
        report.AppendLine("INFO Unity=" + Application.unityVersion + " GPU=" + SystemInfo.graphicsDeviceName +
            " API=" + SystemInfo.graphicsDeviceType + " Size=" + Screen.width + "x" + Screen.height + " Development=" + Debug.isDebugBuild);
    }
    void Log(string message, string trace, LogType type)
    {
        if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
        failures++; report.AppendLine("ERROR " + message + "\n" + trace);
    }
    void Check(bool ok, string label)
    {
        report.AppendLine((ok ? "PASS " : "FAIL ") + label);
        if (!ok) failures++;
    }
    bool Ready()
    {
        if (rig == null || rig.spawner.Instance == null || rig.terrain.TerrainChunks.Count == 0) return false;
        foreach (var chunk in rig.terrain.TerrainChunks.Values) if (!chunk.HasGeneratedMesh || !chunk.WorldFeaturesReady) return false;
        return true;
    }
    void Update()
    {
        try { Tick(); }
        catch (Exception e) { Check(false, e.ToString()); Finish(); }
    }
    void Tick()
    {
        float now = Time.realtimeSinceStartup;
        if (now - started > 240) { Check(false, "Standalone smoke timeout at stage " + stage); Finish(); return; }
        if (stage == 0)
        {
            rig = Object.FindAnyObjectByType<BaseGameRig>();
            if (!Ready() || now - changed < 12) return;
            Check(Object.FindObjectsByType<BaseAssembly>().Length == 1, "Build starts with one base");
            Check(Object.FindObjectsByType<Canvas>().Length == 0, "Build starts without gameplay Canvas");
            Check(rig.grass.ResidentPatches > 0, "Grass buffers generated in standalone");
            Check(rig.spawner.Instance.GetComponent<BaseAssembly>().left.IsConfigured, "Imported cannon bindings survive build");
            rig.SetView(BaseGameRig.View.Overview);
            Capture("01_BuildOverview"); stage = 1; changed = now; frame = 0; return;
        }
        if (stage == 1)
        {
            if (++frame > 60) frameMs.Add(Time.unscaledDeltaTime * 1000);
            if (now - changed < 15 || frameMs.Count < 120) return;
            frameMs.Sort();
            report.AppendLine("INFO Hidden development-player total-frame samples=" + frameMs.Count +
                " median_ms=" + frameMs[frameMs.Count / 2] + " p95_ms=" + frameMs[(int)(frameMs.Count * .95f)] +
                " allocated_bytes=" + Profiler.GetTotalAllocatedMemoryLong());
            report.AppendLine("INFO These are CPU/wall frame intervals, not GPU timings or isolated grass cost.");
            rig.SetView(views[viewIndex]); stage = 2; changed = now; return;
        }
        if (stage == 2)
        {
            if (now - changed < 3) return;
            Check(Camera.main != null && Camera.main.enabled && rig.grass.renderCamera == Camera.main, "Active camera and grass follow " + views[viewIndex]);
            Check(Object.FindObjectsByType<AudioListener>().Count(l => l.isActiveAndEnabled) == 1, "Exactly one audio listener in " + views[viewIndex]);
            if (views[viewIndex] == BaseGameRig.View.Player)
                Check(rig.player.GetComponent<CharacterController>().isGrounded, "Operator settles on a physical floor in player view");
            Capture("02_" + views[viewIndex]);
            if (++viewIndex < views.Length) { rig.SetView(views[viewIndex]); changed = now; return; }
            SceneManager.LoadScene(1); stage = 3; changed = now; return;
        }
        if (stage == 3)
        {
            if (now - changed < 4) return;
            var controller = Object.FindAnyObjectByType<ArtilleryTestController>();
            Check(controller != null, "Artillery test scene loads in standalone");
            if (controller != null)
            {
                controller.AddElevation(999); Check(controller.Elevation == controller.maxElevation, "Artillery upper stop");
                controller.AddElevation(-999); Check(controller.Elevation == controller.minElevation, "Artillery lower stop");
                controller.AddAzimuth(-1); Check(controller.Azimuth >= 0 && controller.Azimuth < 360, "Azimuth wraps negative input");
                controller.ResetAim(); Check(controller.Elevation == 15 && controller.Azimuth == 0, "Artillery reset");
                var wheels = Object.FindObjectsByType<HandwheelInteraction>();
                Check(wheels.Length >= 2, "Artillery test contains physical wheels");
                foreach (var wheel in wheels)
                {
                    float before = wheel.controlType == HandwheelInteraction.ControlType.Azimuth ? controller.Azimuth : controller.Elevation;
                    wheel.RotateBy(10);
                    float after = wheel.controlType == HandwheelInteraction.ControlType.Azimuth ? controller.Azimuth : controller.Elevation;
                    Check(before != after, "Handwheel affects controller: " + wheel.name);
                }
            }
            Capture("03_ArtilleryTest"); SceneManager.LoadScene(2); stage = 4; changed = now; return;
        }
        if (stage == 4)
        {
            if (now - changed < 4) return;
            var splines = Object.FindObjectsByType<SurfaceSplineMesh>();
            Check(splines.Length == 3, "Saved spline scene loads all three surfaces in build");
            foreach (var spline in splines) Check(spline.GeneratedMesh != null && spline.GeneratedMesh.GetIndexCount(0) > 0, "Standalone projected mesh: " + spline.name);
            Check(Object.FindAnyObjectByType<BaseAssembly>() == null, "Previous gameplay base is released on scene change");
            Capture("04_Spline"); SceneManager.LoadScene(0); stage = 5; changed = now; return;
        }
        if (stage == 5)
        {
            rig = Object.FindAnyObjectByType<BaseGameRig>();
            if (!Ready() || now - changed < 12) return;
            Check(Object.FindObjectsByType<BaseAssembly>().Length == 1, "Returning to game scene creates one base");
            Check(rig.grass.ResidentPatches > 0, "Grass rebuilds after scene unload and reload");
            Check(failures == 0, "No Unity exceptions or assertions across standalone scene transitions");
            Finish();
        }
    }
    void Capture(string name)
    {
        var camera = Camera.main;
        if (camera == null) { Check(false, "Capture camera: " + name); return; }
        var target = new RenderTexture(1280, 720, 24);
        var previous = camera.targetTexture; var active = RenderTexture.active;
        var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        try
        {
            target.Create(); camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply();
            var pixels = image.GetPixels32(); int pink = 0, lit = 0;
            foreach (var p in pixels) { if (p.r > 220 && p.b > 220 && p.g < 40) pink++; if (p.r + p.g + p.b > 25) lit++; }
            Check(lit > pixels.Length / 10 && pink < pixels.Length / 100, "Nonblank render without error-magenta: " + name + " pink=" + pink);
            File.WriteAllBytes(Path.Combine(output, name + ".png"), image.EncodeToPNG());
        }
        finally { camera.targetTexture = previous; RenderTexture.active = active; target.Release(); Destroy(target); Destroy(image); }
    }
    void Finish()
    {
        enabled = false; Application.logMessageReceived -= Log;
        File.WriteAllText(Path.Combine(output, "Standalone.txt"), report.ToString());
        Debug.Log("QA_STANDALONE_EXIT=" + (failures == 0 ? 0 : 1));
        Application.Quit(failures == 0 ? 0 : 1);
    }
}
