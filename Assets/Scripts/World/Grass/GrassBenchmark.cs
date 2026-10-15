using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ProjectLeviathan.Grass
{
    // Enabled only in an explicit standalone benchmark, never part of the player HUD.
    public sealed class GrassBenchmark : MonoBehaviour
    {
        public GrassManager manager;
        const int Blocks = 8, Samples = 300, Warmup = 120;
        readonly FrameTiming[] timings = new FrameTiming[1];
        readonly double[] gpu = new double[Blocks * Samples];
        readonly double[] cpu = new double[Blocks * Samples];
        int block, frame, count;
        ulong lastTimestamp;
        double started;
        string output;
        long maximumGrassGc;
        bool baselineSmoke;
        void Start()
        {
            string[] args = Environment.GetCommandLineArgs();
            int flag = Array.IndexOf(args, "-grass-benchmark");
            baselineSmoke = Array.IndexOf(args, "-grass-baseline-smoke") >= 0;
            if (flag < 0 && !baselineSmoke) { enabled = false; return; }
            int path = Array.IndexOf(args, "-grass-output");
            output = path >= 0 && path + 1 < args.Length ? args[path + 1] : Path.Combine(Application.persistentDataPath, "GrassBenchmark");
            Directory.CreateDirectory(output);
            Application.runInBackground = true; QualitySettings.vSyncCount = 0; Application.targetFrameRate = -1;
            manager.configuration.collectGpuStatistics = false;
            manager.rendering = !baselineSmoke;
            FindAnyObjectByType<GrassShowcase>().ShowGrass();
            started = Time.realtimeSinceStartupAsDouble;
            Debug.Log("GRASS_BENCHMARK_START: " + SystemInfo.graphicsDeviceName + ", " + Screen.width + "x" + Screen.height + ", threading=" + SystemInfo.renderingThreadingMode);
        }
        void Update()
        {
            if (Time.realtimeSinceStartupAsDouble - started > 180)
            { Debug.LogError("No usable GPU frame timings before benchmark timeout."); Application.Quit(2); enabled = false; return; }
            frame++;
            if (baselineSmoke && frame >= 240)
            { Debug.Log("GRASS_BASELINE_SMOKE_OK: 240 frames with grass rendering disabled."); Application.Quit(0); enabled = false; return; }
            FrameTimingManager.CaptureFrameTimings();
            uint available = FrameTimingManager.GetLatestTimings(1, timings);
            if (frame <= Warmup || available == 0 || timings[0].gpuFrameTime <= 0 ||
                (timings[0].frameStartTimestamp != 0 && lastTimestamp == timings[0].frameStartTimestamp)) return;
            if (baselineSmoke) return;
            lastTimestamp = timings[0].frameStartTimestamp;
            int index = block * Samples + count;
            maximumGrassGc = Math.Max(maximumGrassGc, manager.LastUpdateAllocatedBytes);
            gpu[index] = timings[0].gpuFrameTime; cpu[index] = timings[0].cpuFrameTime;
            count++;
            if (count == 50 && block < 2) ScreenCapture.CaptureScreenshot(Path.Combine(output, block == 0 ? "Standalone_On.png" : "Standalone_Off.png"));
            if (count < Samples) return;
            Debug.Log("GRASS_BENCHMARK_BLOCK: " + block + ", grass=" + (block % 2 == 0) + ", patches=" + manager.ResidentPatches);
            block++;
            if (block == Blocks) { Finish(); enabled = false; return; }
            manager.rendering = block % 2 == 0; frame = 0; count = 0;
        }
        void Finish()
        {
            Directory.CreateDirectory(output);
            StringBuilder csv = new StringBuilder("block,grass,gpu_ms,cpu_ms\n");
            double[] on = new double[Blocks / 2 * Samples], off = new double[Blocks / 2 * Samples];
            int a = 0, b = 0;
            for (int i = 0; i < gpu.Length; i++)
            {
                bool grass = i / Samples % 2 == 0;
                if (grass) on[a++] = gpu[i]; else off[b++] = gpu[i];
                csv.Append(i / Samples).Append(',').Append(grass ? 1 : 0).Append(',')
                    .Append(gpu[i].ToString("F6", CultureInfo.InvariantCulture)).Append(',').Append(cpu[i].ToString("F6", CultureInfo.InvariantCulture)).Append('\n');
            }
            Array.Sort(on); Array.Sort(off);
            var report = new Report
            {
                unity = Application.unityVersion, gpu = SystemInfo.graphicsDeviceName, api = SystemInfo.graphicsDeviceType.ToString(),
                threading = SystemInfo.renderingThreadingMode.ToString(),
                indirectApi = manager.UsingGraphicsJobsFallback ? "DrawMeshInstancedIndirect compatibility" : "RenderMeshIndirect",
                renderScale = GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp ? urp.renderScale : 1,
                density = manager.configuration.bladesPerSquareMeter, residentPatches = manager.ResidentPatches,
                width = Screen.width, height = Screen.height, samplesPerState = on.Length,
                gpuOnMedianMs = on[on.Length / 2], gpuOffMedianMs = off[off.Length / 2],
                incrementalMedianMs = on[on.Length / 2] - off[off.Length / 2],
                gpuOnP95Ms = on[(int)(on.Length * 0.95)], gpuOffP95Ms = off[(int)(off.Length * 0.95)],
                nearShadows = manager.configuration.castNearShadows,
                bufferBytes = manager.BufferBytes,
                maximumGrassUpdateGcBytes = maximumGrassGc,
                note = "Whole-frame A/B estimate, not an isolated GPU timestamp for grass passes. Other open GPU workloads can affect it."
            };
            File.WriteAllText(Path.Combine(output,"Benchmark.csv"), csv.ToString());
            File.WriteAllText(Path.Combine(output,"Benchmark.json"), JsonUtility.ToJson(report,true));
            Debug.Log("GRASS_BENCHMARK_DONE: estimated GPU increment=" + report.incrementalMedianMs.ToString("F3") + " ms");
            Application.Quit(0);
        }
        [Serializable]
        sealed class Report
        {
            public string unity, gpu, api, threading, indirectApi, note;
            public int width, height, samplesPerState;
            public int residentPatches;
            public float renderScale, density;
            public double gpuOnMedianMs, gpuOffMedianMs, incrementalMedianMs, gpuOnP95Ms, gpuOffP95Ms;
            public bool nearShadows;
            public long bufferBytes;
            public long maximumGrassUpdateGcBytes;
        }
    }
}
