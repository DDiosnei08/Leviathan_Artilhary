using System;
using System.Collections.Generic;
using System.Linq;
using ProjectLeviathan.ProceduralTerrain;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class CartoonShaderSceneBuilder
{
    public const string ScenePath = "Assets/Scenes/Demos/Base_Cartum_Terreno_Agua.unity";
    const string DemoSettingsPath = "Assets/Settings/Terrain/CartoonShaderDemo.asset";
    const string VerificationKey = "Leviathan.CartoonShaders.VerifyPlay";
    static double started, stageStarted;
    static int stage;
    static bool failed;
    static bool waterWarmupDone;
    static Texture2D waterFrame;

    [MenuItem("Leviathan/Create Cartoon Shader Demo")]
    public static void CreateFromMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        BuildBatch();
    }

    public static void BuildBatch()
    {
        ValidateWaterGenerator();
        Scene scene = EditorSceneManager.OpenScene(IntegratedBattlefieldSceneBuilder.ScenePath, OpenSceneMode.Single);
        IntegratedBattlefieldRig rig = Object.FindAnyObjectByType<IntegratedBattlefieldRig>();
        if (rig == null) throw new InvalidOperationException("The integrated base rig was not found.");
        TerrainSettings settings = AssetDatabase.LoadAssetAtPath<TerrainSettings>(DemoSettingsPath);
        if (settings == null)
        {
            settings = Object.Instantiate(rig.terrain.terrainSettings);
            settings.name = "CartoonShaderDemo";
            settings.finiteMapChunksPerSide = 17;
            settings.finiteMapFullLoadMaxChunksPerSide = 3;
            settings.finiteMapStreamingRadiusChunks = 1;
            settings.finiteMapEditorPreviewRadiusChunks = 1;
            AssetDatabase.CreateAsset(settings, DemoSettingsPath);
        }
        settings.terrainMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Terrain/TerrainCartoon.mat");
        settings.waterMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Water/WaterCartoon.mat");
        settings.generateWaterSurfaces = true;
        settings.Validate();
        EditorUtility.SetDirty(settings);
        rig.terrain.terrainSettings = settings;
        rig.terrain.terrainMaterial = settings.terrainMaterial;
        ReanchorBase(rig, settings);
        rig.terrain.RegenerateTerrain();
        CartoonShaderShowcase showcase = rig.GetComponent<CartoonShaderShowcase>();
        if (showcase == null) showcase = rig.gameObject.AddComponent<CartoonShaderShowcase>();
        showcase.rig = rig;
        FindShore(settings, out Vector3 focus, out Vector3 intoWater);
        showcase.waterFocusPosition = focus + intoWater * 8;
        Vector3 camera = focus + intoWater * 60 + Vector3.up * 55;
        camera.y = Mathf.Max(camera.y, ProceduralBattlefieldSetup.SampleHeight(settings, camera) + 15);
        showcase.waterCameraPosition = camera;
        rig.SetView(IntegratedBattlefieldRig.ViewMode.Overview);
        if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new InvalidOperationException("Could not save shader demo scene.");
        AssetDatabase.SaveAssets();
        CheckShaders();
        Debug.Log($"CARTOON_SHADER_BUILD_OK: preserved base at {rig.transform.position}; water shore {focus}.");
    }

    static void ReanchorBase(IntegratedBattlefieldRig rig, TerrainSettings settings)
    {
        Transform foundation = rig.transform.Find("Foundation");
        if (foundation == null) throw new InvalidOperationException("Base foundation is missing.");
        Dictionary<Vector2Int, HeightMap> samples = new Dictionary<Vector2Int, HeightMap>();
        float Sample(Vector3 p)
        {
            Vector2Int coordinate = new Vector2Int(Mathf.FloorToInt(p.x / settings.chunkSize), Mathf.FloorToInt(p.z / settings.chunkSize));
            if (!samples.TryGetValue(coordinate, out HeightMap map))
            {
                map = HeightMapGenerator.GenerateHeightMap(settings, settings.chunkResolution, settings.chunkResolution, settings.GetChunkWorldOrigin(coordinate));
                samples.Add(coordinate, map);
            }
            Vector2 origin = settings.GetChunkWorldOrigin(coordinate);
            float x = Mathf.Clamp((p.x - origin.x) / settings.CellSize, 0, map.Width - 1);
            float y = Mathf.Clamp((settings.chunkSize - (p.z - origin.y)) / settings.CellSize, 0, map.Height - 1);
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            int x1 = Mathf.Min(x0 + 1, map.Width - 1), y1 = Mathf.Min(y0 + 1, map.Height - 1);
            return Mathf.Lerp(Mathf.Lerp(map.WorldHeights[x0, y0], map.WorldHeights[x1, y0], x - x0),
                Mathf.Lerp(map.WorldHeights[x0, y1], map.WorldHeights[x1, y1], x - x0), y - y0);
        }
        float low = float.PositiveInfinity, high = float.NegativeInfinity;
        Vector3 center = foundation.localPosition;
        for (int z = 0; z < 5; z++)
        for (int x = 0; x < 5; x++)
        {
            Vector3 point = rig.transform.TransformPoint(new Vector3(center.x + (x / 4f - 0.5f) * foundation.localScale.x,
                0, center.z + (z / 4f - 0.5f) * foundation.localScale.z));
            float height = Sample(point);
            low = Mathf.Min(low, height);
            high = Mathf.Max(high, height);
        }
        float depth = Mathf.Max(0.65f, high - low + 0.7f);
        Vector3 basePosition = rig.transform.position;
        basePosition.y = high + 0.35f;
        rig.transform.position = basePosition;
        Vector3 scale = foundation.localScale;
        scale.y = depth;
        foundation.localScale = scale;
        center.y = -depth * 0.5f;
        foundation.localPosition = center;
        Transform ramp = rig.transform.Find("AccessRamp");
        if (ramp != null)
        {
            float drop = Mathf.Max(0, basePosition.y - Sample(rig.transform.TransformPoint(new Vector3(5, 0, -26))));
            ramp.localPosition = new Vector3(5, -drop * 0.5f - 0.08f, -20);
            ramp.localScale = new Vector3(6, 0.16f, Mathf.Sqrt(144 + drop * drop));
            ramp.localRotation = Quaternion.Euler(-Mathf.Atan2(drop, 12) * Mathf.Rad2Deg, 0, 0);
        }
    }

    static void FindShore(TerrainSettings settings, out Vector3 focus, out Vector3 intoWater)
    {
        int radius = settings.finiteMapChunksPerSide / 2;
        for (int ring = 0; ring <= radius; ring++)
        for (int z = -ring; z <= ring; z++)
        for (int x = -ring; x <= ring; x++)
        {
            if (Mathf.Max(Mathf.Abs(x), Mathf.Abs(z)) != ring) continue;
            Vector2 origin = settings.GetChunkWorldOrigin(new Vector2Int(x, z));
            HeightMap coarse = HeightMapGenerator.GenerateHeightMap(settings, 33, 33, origin);
            bool hasLake = false;
            for (int j = 1; j < 32 && !hasLake; j++)
            for (int i = 1; i < 32 && !hasLake; i++)
                hasLake = coarse.Water[i, j] > 0.5f && coarse.Values[i, j] < settings.lakeThreshold - 0.003f;
            if (!hasLake) continue;
            HeightMap map = HeightMapGenerator.GenerateHeightMap(settings, settings.chunkResolution, settings.chunkResolution, origin);
            Vector2Int[] directions = { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };
            float cell = settings.CellSize;
            for (int j = 1; j < map.Height - 1; j++)
            for (int i = 1; i < map.Width - 1; i++)
            {
                if (map.Water[i, j] < 0.5f || map.Values[i, j] >= settings.lakeThreshold) continue;
                foreach (Vector2Int direction in directions)
                {
                    int nx = i + direction.x, ny = j + direction.y;
                    if (map.Values[nx, ny] <= settings.lakeThreshold) continue;
                    float t = Mathf.InverseLerp(map.Values[i, j], map.Values[nx, ny], settings.lakeThreshold);
                    Vector3 wet = new Vector3(origin.x + i * cell, 0, origin.y + settings.chunkSize - j * cell);
                    Vector3 dry = new Vector3(origin.x + nx * cell, 0, origin.y + settings.chunkSize - ny * cell);
                    focus = Vector3.Lerp(wet, dry, t);
                    focus.y = settings.CaptureGenerationSettings().EvaluateHeight(settings.lakeThreshold) * settings.heightMultiplier + settings.waterSurfaceOffset;
                    intoWater = (wet - dry).normalized;
                    return;
                }
            }
        }
        throw new InvalidOperationException("No procedural lake shore found inside the demo map. No artificial water plane was created.");
    }

    static void CheckShaders()
    {
        foreach (string name in new[] { "Leviathan/Cartoon Terrain", "Leviathan/Cartoon Water" })
        {
            Shader shader = Shader.Find(name);
            if (shader == null || !shader.isSupported) throw new InvalidOperationException("Unsupported shader: " + name);
            foreach (var message in ShaderUtil.GetShaderMessages(shader))
                if (message.severity.ToString() == "Error") throw new InvalidOperationException(name + ": " + message.message);
        }
    }

    static void ValidateWaterGenerator()
    {
        TerrainSettings settings = ScriptableObject.CreateInstance<TerrainSettings>();
        settings.chunkSize = 20;
        settings.chunkResolution = 3;
        settings.heightMultiplier = 10;
        settings.lakeThreshold = 0.13f;
        settings.Validate();
        HeightMap map = new HeightMap(new float[3, 3], water: new float[3, 3], worldHeights: new float[3, 3]);
        for (int y = 0; y < 3; y++)
        for (int x = 0; x < 3; x++)
        {
            map.Values[x, y] = 0.2f;
            map.WorldHeights[x, y] = 2;
        }
        if (TerrainWaterMeshGenerator.Generate(map, settings) != null)
            throw new InvalidOperationException("Water was generated over a dry height map.");
        map.Values[1, 1] = 0.1f;
        map.WorldHeights[1, 1] = 1;
        map.Water[1, 1] = 1;
        Mesh clipped = TerrainWaterMeshGenerator.Generate(map, settings);
        if (clipped == null || clipped.vertexCount == 0) throw new InvalidOperationException("A wet sample produced no lake surface.");
        foreach (Vector3 vertex in clipped.vertices)
        {
            if (vertex.x < 6.99f || vertex.x > 13.01f || vertex.z < 6.99f || vertex.z > 13.01f)
                throw new InvalidOperationException("Lake shore triangles cover dry samples.");
            if (Mathf.Abs(vertex.y - 1.38f) > 0.001f) throw new InvalidOperationException("Lake level is not constant.");
        }
        if (clipped.normals.Any(n => n.y < 0.99f)) throw new InvalidOperationException("Lake normals point downwards.");
        Object.DestroyImmediate(clipped);
        map.Values[1, 1] = 0.18f;
        map.WorldHeights[1, 1] = 1.8f;
        Mesh river = TerrainWaterMeshGenerator.Generate(map, settings);
        if (river == null) throw new InvalidOperationException("The river mask produced no geometry.");
        Object.DestroyImmediate(river);
        for (int y = 0; y < 3; y++)
        for (int x = 0; x < 3; x++)
        {
            map.Values[x, y] = 0.1f;
            map.WorldHeights[x, y] = 1;
            map.Water[x, y] = 1;
        }
        Mesh left = TerrainWaterMeshGenerator.Generate(map, settings);
        Mesh right = TerrainWaterMeshGenerator.Generate(map, settings);
        List<Vector3> leftEdge = left.vertices.Where(v => Mathf.Abs(v.x - 20) < 0.001f).OrderBy(v => v.z).ToList();
        List<Vector3> rightEdge = right.vertices.Where(v => Mathf.Abs(v.x) < 0.001f).OrderBy(v => v.z).ToList();
        if (leftEdge.Count != rightEdge.Count || leftEdge.Count == 0) throw new InvalidOperationException("Water chunk edges do not match.");
        for (int i = 0; i < leftEdge.Count; i++)
            if (Vector3.Distance(leftEdge[i], rightEdge[i] + Vector3.right * 20) > 0.001f)
                throw new InvalidOperationException("Water has a height seam between chunks.");
        Object.DestroyImmediate(left);
        Object.DestroyImmediate(right);
        settings.generateWaterSurfaces = false;
        if (TerrainWaterMeshGenerator.Generate(map, settings) != null) throw new InvalidOperationException("Disabled water still generated a mesh.");
        Object.DestroyImmediate(settings);
        Debug.Log("CARTOON_WATER_GEOMETRY_OK: dry mask, clipped lake, river mask, upward normals, matching chunk edges, disabled water.");
    }

    public static void VerifyPlayBatch()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        SessionState.SetBool(VerificationKey, true);
        ResumeVerification();
        EditorApplication.EnterPlaymode();
    }

    [InitializeOnLoadMethod]
    static void ResumeVerification()
    {
        if (!SessionState.GetBool(VerificationKey, false)) return;
        stage = 0;
        failed = false;
        waterWarmupDone = false;
        started = EditorApplication.timeSinceStartup;
        EditorApplication.update -= VerifyFrame;
        EditorApplication.update += VerifyFrame;
        Application.logMessageReceived -= TrackErrors;
        Application.logMessageReceived += TrackErrors;
    }

    static void TrackErrors(string message, string trace, LogType type)
    {
        if (trace.Contains("UnityEditor.Search.")) return;
        if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert) failed = true;
    }

    static void VerifyFrame()
    {
        try
        {
            double now = EditorApplication.timeSinceStartup;
            if (now - started > 120) throw new InvalidOperationException("Shader verification timed out.");
            if (!EditorApplication.isPlaying) return;
            CartoonShaderShowcase showcase = Object.FindAnyObjectByType<CartoonShaderShowcase>();
            if (showcase == null || !showcase.rig.terrain.HasGeneratedTerrain) return;
            IntegratedBattlefieldRig rig = showcase.rig;
            if (stage == 0)
            {
                stageStarted = now;
                stage = 1;
            }
            else if (stage == 1 && now - stageStarted > 1)
            {
                if (rig.terrain.TerrainChunks.Values.Any(c => !c.HasGeneratedMesh || c.GeneratedData == null)) return;
                CheckShaders();
                foreach (TerrainChunk chunk in rig.terrain.TerrainChunks.Values)
                {
                    Material material = chunk.MeshRenderer.sharedMaterial;
                    if (material.shader.name != "Leviathan/Cartoon Terrain" || material.GetTexture("_BaseMap") == null)
                        throw new InvalidOperationException($"Terrain {chunk.Coordinate} lost the biome texture or cartoon shader: {material.shader.name}, texture={material.GetTexture("_BaseMap")}, generated={chunk.HasGeneratedMesh}.");
                }
                Texture2D image = Capture(rig.overviewCamera, "CartoonBase.png");
                float ground = ProceduralBattlefieldSetup.SampleHeight(rig.terrain.terrainSettings, rig.overviewCamera.transform.position);
                if (rig.overviewCamera.transform.position.y <= ground)
                    throw new InvalidOperationException("The overview camera is buried in the current terrain.");
                int colored = image.GetPixels32().Count(p => p.g > p.r * 1.1f && p.g > p.b * 1.1f || p.r > p.b * 1.2f);
                if (colored < 20000) throw new InvalidOperationException("Biome colors are not visible in the overview.");
                Debug.Log($"CARTOON_TERRAIN_RENDER_OK: {colored} biome-colored pixels; camera above the current ground.");
                Object.DestroyImmediate(image);
                rig.SetView(IntegratedBattlefieldRig.ViewMode.Player);
                stageStarted = now;
                stage = 2;
            }
            else if (stage == 2 && now - stageStarted > 1)
            {
                if (!rig.player.GetComponent<CharacterController>().isGrounded)
                    throw new InvalidOperationException("Player no longer stands on the base.");
                rig.artillery.AddAzimuth(25);
                rig.artillery.AddElevation(10);
                if (Mathf.Abs(rig.artillery.Elevation - 25) > 0.01f) throw new InvalidOperationException("Artillery controls no longer work.");
                rig.artillery.ResetAim();
                showcase.ShowWater();
                stageStarted = now;
                stage = 3;
            }
            else if (stage == 3 && now - stageStarted > 1)
            {
                if (rig.terrain.TerrainChunks.Values.Any(c => !c.HasGeneratedMesh || c.GeneratedData == null)) return;
                VerifyWaterChunks(rig.terrain);
                if (!waterWarmupDone)
                {
                    Object.DestroyImmediate(Capture(rig.overviewCamera, "CartoonWater_Warmup.png"));
                    waterWarmupDone = true;
                    stageStarted = now;
                    return;
                }
                waterFrame = Capture(rig.overviewCamera, "CartoonWater_A.png");
                // A cold GPU Resident Drawer cache may need more than one camera render.
                if (waterFrame.GetPixels32().Count(p => p.b > p.r * 1.25f && p.g > p.r * 1.25f) < 5000)
                {
                    Object.DestroyImmediate(waterFrame);
                    waterFrame = null;
                    stageStarted = now;
                    return;
                }
                stageStarted = now;
                stage = 4;
            }
            else if (stage == 4 && now - stageStarted > 1)
            {
                Texture2D next = Capture(rig.overviewCamera, "CartoonWater_B.png");
                Color32[] a = waterFrame.GetPixels32(), b = next.GetPixels32();
                int changed = 0, bluePixels = 0, firstBluePixels = 0;
                for (int i = 0; i < a.Length; i++)
                {
                    bool firstWater = a[i].b > a[i].r * 1.25f && a[i].g > a[i].r * 1.25f;
                    bool secondWater = b[i].b > b[i].r * 1.25f && b[i].g > b[i].r * 1.25f;
                    if (firstWater) firstBluePixels++;
                    if (secondWater) bluePixels++;
                    if (firstWater && secondWater && Mathf.Abs(a[i].r - b[i].r) + Mathf.Abs(a[i].g - b[i].g) + Mathf.Abs(a[i].b - b[i].b) > 15) changed++;
                }
                if (changed < 1000 || bluePixels < 5000 || firstBluePixels < 5000)
                    throw new InvalidOperationException($"Water pixels are blank or static: changed={changed}, blue A={firstBluePixels}, blue B={bluePixels}.");
                Debug.Log($"CARTOON_WATER_ANIMATION_OK: {changed} changing pixels, {bluePixels} blue/turquoise pixels.");
                Object.DestroyImmediate(waterFrame);
                Object.DestroyImmediate(next);
                rig.terrain.RegenerateTerrain();
                stageStarted = now;
                stage = 5;
            }
            else if (stage == 5 && now - stageStarted > 0.5)
            {
                if (rig.terrain.TerrainChunks.Values.Any(c => !c.HasGeneratedMesh || c.GeneratedData == null)) return;
                VerifyWaterChunks(rig.terrain);
                CheckShaders();
                showcase.ShowBase();
                stageStarted = now;
                stage = 6;
            }
            else if (stage == 6 && now - stageStarted > 0.5)
            {
                if (rig.terrain.TerrainChunks.Values.Any(c => !c.HasGeneratedMesh || c.GeneratedData == null)) return;
                if (!rig.terrain.HasGeneratedTerrain || Object.FindObjectsByType<TerrainWorldController>().Length != 1)
                    throw new InvalidOperationException("Terrain regeneration failed or created a duplicate world.");
                if (failed) throw new InvalidOperationException("Unity reported errors during shader validation.");
                Debug.Log("CARTOON_SHADERS_PLAY_OK: shader rendering, biome textures, base player/artillery, animated water, regeneration and cleanup.");
                Finish(0);
            }
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            Finish(1);
        }
    }

    static void VerifyWaterChunks(TerrainWorldController terrain)
    {
        int meshes = 0;
        foreach (TerrainChunk chunk in terrain.TerrainChunks.Values)
        {
            if (chunk.GeneratedWaterMesh == null) continue;
            meshes++;
            if (chunk.WaterRenderer == null || chunk.WaterRenderer.sharedMaterial.shader.name != "Leviathan/Cartoon Water")
                throw new InvalidOperationException("Water material was lost during generation.");
            if (chunk.GameObject.GetComponentsInChildren<MeshRenderer>().Count(r => r.name == "Generated Water Surface") != 1)
                throw new InvalidOperationException("Duplicate water surfaces after regeneration.");
        }
        if (meshes == 0) throw new InvalidOperationException("No water geometry generated at the lake view.");
        Debug.Log($"CARTOON_WATER_CHUNKS_OK: {meshes} water meshes in {terrain.GeneratedChunkCount} chunks.");
    }

    static Texture2D Capture(Camera camera, string filename)
    {
        RenderTexture target = new RenderTexture(1600, 900, 24);
        Texture2D image = new Texture2D(1600, 900, TextureFormat.RGB24, false);
        RenderTexture previous = RenderTexture.active;
        try
        {
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
            image.Apply();
            int magenta = image.GetPixels32().Count(p => p.r > 220 && p.b > 220 && p.g < 40);
            if (magenta > 100) throw new InvalidOperationException("A shader error material is visible in " + filename);
            System.IO.Directory.CreateDirectory("Artifacts/Shaders/Captures");
            System.IO.File.WriteAllBytes("Artifacts/Shaders/Captures/" + filename, image.EncodeToPNG());
            return image;
        }
        finally
        {
            camera.targetTexture = null;
            RenderTexture.active = previous;
            target.Release();
            Object.DestroyImmediate(target);
        }
    }

    static void Finish(int exitCode)
    {
        SessionState.SetBool(VerificationKey, false);
        EditorApplication.update -= VerifyFrame;
        Application.logMessageReceived -= TrackErrors;
        EditorApplication.Exit(exitCode);
    }
}
