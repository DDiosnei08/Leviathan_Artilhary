using System;
using System.Linq;
using ProjectLeviathan.ProceduralTerrain;
using ProjectLeviathan.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class IntegratedBattlefieldSceneBuilder
{
    public const string ScenePath = "Assets/Scenes/Demos/Base_Provisoria_Integrada.unity";
    const string ArtilleryScene = "Assets/Scenes/Tests/Test_ArtilleryControls/Test_ArtilleryControls.unity";
    const string CommandScene = "Assets/Scenes/Tests/Test_ControllerPlayer/Test_controlsPlayer.unity";
    const string SettingsPath = "Assets/Settings/Terrain/BaseProvisoriaTerrain.asset";
    static readonly Vector3[] Footprint =
    {
        new Vector3(-22, 0, -14), new Vector3(18, 0, -14),
        new Vector3(-22, 0, 18), new Vector3(18, 0, 18), Vector3.zero
    };

    [MenuItem("Leviathan/Create Integrated Battlefield Scene")]
    public static void CreateFromMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        BuildScene();
        Selection.activeGameObject = Object.FindAnyObjectByType<IntegratedBattlefieldRig>().gameObject;
        SceneView.lastActiveSceneView?.LookAt(Selection.activeTransform.position, Quaternion.Euler(25, 35, 0), 55f);
    }

    // Used by Unity batch mode in an isolated copy of the project.
    public static void BuildBatch()
    {
        BuildScene();
        ValidateScene();
        CaptureOverview();
        Debug.Log("INTEGRATED_BATTLEFIELD_OK " + ScenePath);
    }

    const string PlayVerificationKey = "Leviathan.IntegratedBattlefield.PlayVerification";
    static int playStage;
    static double stageStarted;
    static double verificationStarted;
    static bool runtimeError;
    static Quaternion needleBeforeAim;

    public static void VerifyPlayBatch()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        SessionState.SetBool(PlayVerificationKey, true);
        ResumePlayVerification();
        EditorApplication.EnterPlaymode();
    }

    [InitializeOnLoadMethod]
    static void ResumePlayVerification()
    {
        if (!SessionState.GetBool(PlayVerificationKey, false)) return;
        playStage = 0;
        runtimeError = false;
        verificationStarted = EditorApplication.timeSinceStartup;
        Application.logMessageReceived -= TrackRuntimeErrors;
        EditorApplication.update -= VerifyPlayFrame;
        Application.logMessageReceived += TrackRuntimeErrors;
        EditorApplication.update += VerifyPlayFrame;
    }

    static void TrackRuntimeErrors(string message, string trace, LogType type)
    {
        if (trace.Contains("UnityEditor.Search.")) return;
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) runtimeError = true;
    }

    static void VerifyPlayFrame()
    {
        try
        {
            if (EditorApplication.timeSinceStartup - verificationStarted > 60)
                throw new InvalidOperationException("Play verification timed out.");
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            IntegratedBattlefieldRig rig = Object.FindAnyObjectByType<IntegratedBattlefieldRig>();
            if (rig == null || !rig.terrain.HasGeneratedTerrain) return;
            double now = EditorApplication.timeSinceStartup;
            if (playStage == 0)
            {
                rig.SetView(IntegratedBattlefieldRig.ViewMode.Player);
                stageStarted = now;
                playStage = 1;
            }
            else if (playStage == 1 && now - stageStarted > 1)
            {
                if (!rig.player.GetComponent<CharacterController>().isGrounded)
                    throw new InvalidOperationException("The player did not land on the base during Play.");
                if (Object.FindObjectsByType<TerrainWorldController>().Length != 1)
                    throw new InvalidOperationException("A duplicate terrain appeared during Play.");
                rig.SetView(IntegratedBattlefieldRig.ViewMode.Operator);
                ArtilleryGaugeDriver gauges = rig.artillery.GetComponentInChildren<ArtilleryGaugeDriver>();
                if (gauges.azimuthNeedleMesh.parent.name != "Azimuth_Runtime_Pivot")
                    throw new InvalidOperationException("The imported azimuth needle has no runtime pivot.");
                needleBeforeAim = gauges.azimuthNeedleMesh.parent.localRotation;
                rig.artillery.AddAzimuth(25);
                stageStarted = now;
                playStage = 2;
            }
            else if (playStage == 2 && now - stageStarted > 0.4)
            {
                if (rig.handwheels.Any(w => !w.enabled)) throw new InvalidOperationException("Operator handwheels are disabled.");
                ArtilleryGaugeDriver gauges = rig.artillery.GetComponentInChildren<ArtilleryGaugeDriver>();
                if (Mathf.Abs(Quaternion.Angle(needleBeforeAim, gauges.azimuthNeedleMesh.parent.localRotation) - 25) > 0.1f)
                    throw new InvalidOperationException("The imported gauge needle did not track artillery aim.");
                CaptureCamera(rig.operatorCamera, "BaseProvisoriaOperator.png");
                rig.artillery.ResetAim();
                rig.SetView(IntegratedBattlefieldRig.ViewMode.Overview);
                stageStarted = now;
                playStage = 3;
            }
            else if (playStage == 3 && now - stageStarted > 0.4)
            {
                ValidateTerrainPalette(rig.terrain.terrainSettings);
                foreach (TerrainChunk chunk in rig.terrain.TerrainChunks.Values)
                {
                    if (chunk.Coordinate != Vector2Int.zero) continue;
                    Material material = chunk.MeshRenderer.sharedMaterial;
                    Texture2D texture = material.GetTexture("_BaseMap") as Texture2D;
                    if (texture == null) throw new InvalidOperationException("Terrain biome color texture is missing.");
                    Debug.Log($"Terrain preview material: {material.shader.name}, color texture {texture.width}x{texture.height}, center {texture.GetPixel(texture.width / 2, texture.height / 2)}, renderer {chunk.MeshRenderer.enabled}.");
                }
                CaptureOverview();
                if (runtimeError) throw new InvalidOperationException("Unity reported an error during Play verification.");
                Debug.Log("INTEGRATED_BATTLEFIELD_PLAY_OK: player grounded, one terrain, operator handwheels enabled, runtime previews captured.");
                FinishPlayVerification(0);
            }
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            FinishPlayVerification(1);
        }
    }

    static void FinishPlayVerification(int exitCode)
    {
        SessionState.SetBool(PlayVerificationKey, false);
        EditorApplication.update -= VerifyPlayFrame;
        Application.logMessageReceived -= TrackRuntimeErrors;
        EditorApplication.Exit(exitCode);
    }

    public static void BuildScene()
    {
        Scene scene = EditorSceneManager.OpenScene(ArtilleryScene, OpenSceneMode.Single);
        GameObject gun = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "ArtilleryTestGenerated");
        if (gun == null) throw new InvalidOperationException("ArtilleryTestGenerated was not found in the artillery scene.");
        ArtilleryTestController controller = gun.GetComponent<ArtilleryTestController>();
        Camera operatorCamera = gun.GetComponentsInChildren<Camera>(true).First();
        foreach (Canvas canvas in gun.GetComponentsInChildren<Canvas>(true)) Object.DestroyImmediate(canvas.gameObject);

        foreach (GameObject other in scene.GetRootGameObjects().Where(g => g != gun).ToArray())
        {
            if (other.GetComponentInChildren<ArtilleryGaugeDriver>(true) != null)
                other.transform.SetParent(gun.transform, true);
            else
                Object.DestroyImmediate(other);
        }
        foreach (Light light in gun.GetComponentsInChildren<Light>(true)) Object.DestroyImmediate(light.gameObject);
        RemoveChild(gun.transform, "Ground");
        RemoveChild(gun.transform, "StagingPad");

        Scene source = EditorSceneManager.OpenScene(CommandScene, OpenSceneMode.Additive);
        GameObject sourceRoom = source.GetRootGameObjects().First(g => g.name == "CommandRoomGenerated");
        GameObject room = Object.Instantiate(sourceRoom);
        SceneManager.MoveGameObjectToScene(room, scene);
        EditorSceneManager.CloseScene(source, true);
        SceneManager.SetActiveScene(scene);

        TerrainSettings settings = CreateSettings();
        Vector3 site = FindSite(settings, out float minimum, out float maximum);
        GameObject baseRoot = new GameObject("Base Provisoria");
        baseRoot.transform.position = new Vector3(site.x, maximum + 0.35f, site.z);
        gun.name = "ArtilleryBattery";
        gun.transform.SetParent(baseRoot.transform, false);
        gun.transform.localPosition = new Vector3(5, 0, 5);
        gun.transform.localRotation = Quaternion.identity;
        room.name = "CommandPost";
        room.transform.SetParent(baseRoot.transform, false);
        room.transform.localPosition = new Vector3(-13, 0, -1);
        room.transform.localRotation = Quaternion.identity;

        foreach (Light light in room.GetComponentsInChildren<Light>(true)) Object.DestroyImmediate(light.gameObject);
        foreach (Canvas canvas in room.GetComponentsInChildren<Canvas>(true)) Object.DestroyImmediate(canvas.gameObject);
        foreach (CommandRoomCameraFocusZone zone in room.GetComponentsInChildren<CommandRoomCameraFocusZone>(true))
            zone.enabled = false;
        foreach (HandwheelInteraction wheel in room.GetComponentsInChildren<HandwheelInteraction>(true))
            Object.DestroyImmediate(wheel);
        foreach (ArtilleryCommandLever lever in room.GetComponentsInChildren<ArtilleryCommandLever>(true))
            lever.artilleryController = controller;
        foreach (ArtilleryGaugeDriver gauges in room.GetComponentsInChildren<ArtilleryGaugeDriver>(true))
            gauges.artilleryController = controller;
        foreach (ArtilleryTestController virtualController in room.GetComponentsInChildren<ArtilleryTestController>(true))
            Object.DestroyImmediate(virtualController.gameObject);

        LeviathanPlayerController player = room.GetComponentInChildren<LeviathanPlayerController>(true);
        if (player == null) throw new InvalidOperationException("The command scene has no player.");
        player.transform.SetParent(baseRoot.transform, false);
        player.transform.localPosition = new Vector3(5, 1.05f, -8);
        player.transform.localRotation = Quaternion.identity;
        foreach (Renderer renderer in player.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
        Camera playerCamera = player.GetComponentInChildren<Camera>(true);
        playerCamera.transform.localPosition = new Vector3(0, 0.62f, 0);
        playerCamera.transform.localRotation = Quaternion.identity;
        player.cameraPivot = playerCamera.transform;
        player.enabled = false;
        CommandRoomInteractor interactor = player.GetComponent<CommandRoomInteractor>();
        interactor.viewCamera = playerCamera;
        interactor.enabled = false;

        float foundationDepth = Mathf.Max(0.65f, maximum - minimum + 0.7f);
        Material concrete = MaterialAsset("BaseConcrete", new Color(0.27f, 0.29f, 0.28f));
        Material metal = MaterialAsset("BaseMetal", new Color(0.14f, 0.18f, 0.19f));
        Material sandbag = MaterialAsset("BaseSandbags", new Color(0.43f, 0.45f, 0.31f));
        Material crate = MaterialAsset("BaseSupplies", new Color(0.25f, 0.34f, 0.29f));
        Material trim = MaterialAsset("BaseSafety", new Color(0.85f, 0.66f, 0.15f));
        PlaceGaugeConsole(gun.transform, metal);
        Part("Foundation", baseRoot.transform, new Vector3(-2, -foundationDepth * 0.5f, 2),
            new Vector3(40, foundationDepth, 32), concrete);
        CreatePerimeter(baseRoot.transform, sandbag);
        for (int i = 0; i < 5; i++)
            Part("SupplyCrate_" + i, baseRoot.transform, new Vector3(-19 + i % 2 * 1.4f, 0.5f + i / 4 * 1.02f, 12 + i % 3 * 1.4f),
                new Vector3(1.2f, 1, 1.2f), crate);
        Part("BatteryPad", baseRoot.transform, new Vector3(5, 0.015f, 7), new Vector3(9, 0.03f, 12), metal);
        for (int i = 0; i < 6; i++)
            Part("WalkwayMarker_" + i, baseRoot.transform, new Vector3(-7 + i * 2f, 0.025f, -6),
                new Vector3(0.35f, 0.05f, 1.2f), trim);
        CreateAccessRamp(baseRoot.transform, settings, site, concrete);
        CreateFlag(baseRoot.transform, metal, trim);
        CreateRoomLight(room.transform);
        CreateSun();

        GameObject overviewObject = new GameObject("OverviewCamera");
        overviewObject.transform.SetParent(baseRoot.transform, false);
        overviewObject.transform.localPosition = new Vector3(39, 26, -43);
        overviewObject.transform.LookAt(baseRoot.transform.TransformPoint(new Vector3(-2, 1, 3)));
        Camera overview = overviewObject.AddComponent<Camera>();
        overview.fieldOfView = 55f;
        foreach (Camera camera in new[] { overview, playerCamera, operatorCamera })
        {
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 10000f;
            camera.clearFlags = CameraClearFlags.Skybox;
            if (camera.GetComponent<AudioListener>() == null) camera.gameObject.AddComponent<AudioListener>();
        }

        GameObject terrainObject = new GameObject("Procedural Battlefield");
        WorldWeatherProvider weather = terrainObject.AddComponent<WorldWeatherProvider>();
        TerrainWorldController terrain = terrainObject.AddComponent<TerrainWorldController>();
        terrain.terrainSettings = settings;
        terrain.terrainMaterial = settings.terrainMaterial;
        terrain.viewer = overview.transform;
        terrain.weatherProvider = weather;
        terrain.useFiniteMap = true;
        terrain.previewInEditor = true;
        terrain.maxChunkGenerationsPerFrame = 2;
        terrain.RegenerateTerrain();

        IntegratedBattlefieldRig rig = baseRoot.AddComponent<IntegratedBattlefieldRig>();
        rig.terrain = terrain;
        rig.artillery = controller;
        rig.operatorStation = operatorCamera.transform;
        rig.player = player;
        rig.interactor = interactor;
        rig.overviewCamera = overview;
        rig.playerCamera = playerCamera;
        rig.operatorCamera = operatorCamera;
        rig.operatorInteractor = operatorCamera.GetComponent<CommandRoomInteractor>();
        if (rig.operatorInteractor == null) rig.operatorInteractor = operatorCamera.gameObject.AddComponent<CommandRoomInteractor>();
        rig.operatorInteractor.viewCamera = operatorCamera;
        rig.operatorInteractor.useScreenCenter = false;
        rig.operatorInteractor.interactDistance = 3.5f;
        rig.handwheels = gun.GetComponentsInChildren<HandwheelInteraction>(true);
        foreach (HandwheelInteraction wheel in rig.handwheels)
        {
            wheel.playerCamera = operatorCamera;
            wheel.controller = controller;
        }
        foreach (ArtilleryGaugeDriver gauges in gun.GetComponentsInChildren<ArtilleryGaugeDriver>(true))
            gauges.artilleryController = controller;
        Transform map = room.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "MapSurface");
        if (map != null) PhysicalInstrumentBuilder.CreateMapControls(map, controller);
        rig.SetView(IntegratedBattlefieldRig.ViewMode.Overview);
        controller.ResetAim();

        if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new InvalidOperationException("Scene could not be saved.");
        AssetDatabase.SaveAssets();
        Debug.Log($"Integrated base at {baseRoot.transform.position}; terrain variation {maximum - minimum:0.00} m; {rig.handwheels.Length} handwheels.");
    }

    static TerrainSettings CreateSettings()
    {
        TerrainSettings settings = AssetDatabase.LoadAssetAtPath<TerrainSettings>(SettingsPath);
        if (settings == null)
        {
            settings = Object.Instantiate(ProceduralBattlefieldSetup.LoadTerrainSettings());
            settings.name = "BaseProvisoriaTerrain";
            // A compact preview of the same world keeps this integration scene quick to load.
            settings.finiteMapChunksPerSide = 3;
            settings.finiteMapFullLoadMaxChunksPerSide = 3;
            settings.finiteMapStreamingRadiusChunks = 1;
            settings.finiteMapEditorPreviewRadiusChunks = 1;
            AssetDatabase.CreateAsset(settings, SettingsPath);
        }
        settings.Validate();
        return settings;
    }

    static Vector3 FindSite(TerrainSettings settings, out float minimum, out float maximum)
    {
        HeightMap map = HeightMapGenerator.GenerateHeightMap(settings, settings.chunkResolution, settings.chunkResolution, Vector2.zero);
        Vector3 best = new Vector3(600, 0, 600);
        float bestScore = float.PositiveInfinity;
        minimum = maximum = 0;
        float margin = Mathf.Min(100f, settings.chunkSize * 0.2f);
        for (float z = margin; z <= settings.chunkSize - margin; z += 40)
        for (float x = margin; x <= settings.chunkSize - margin; x += 40)
        {
            Vector3 candidate = new Vector3(x, 0, z);
            float low = float.PositiveInfinity, high = float.NegativeInfinity;
            foreach (Vector3 probe in Footprint)
            {
                float h = Sample(map, settings, candidate + probe);
                low = Mathf.Min(low, h);
                high = Mathf.Max(high, h);
            }
            if (low < settings.heightMultiplier * Mathf.Max(settings.lakeThreshold, settings.riverThreshold) + 2f) continue;
            float score = high - low + Vector3.Distance(candidate, new Vector3(600, 0, 600)) * 0.0005f;
            if (score >= bestScore) continue;
            bestScore = score;
            best = candidate;
            minimum = low;
            maximum = high;
        }
        if (float.IsInfinity(bestScore)) throw new InvalidOperationException("No dry site found. Check terrain water thresholds.");
        return best;
    }

    static float Sample(HeightMap map, TerrainSettings settings, Vector3 p)
    {
        float x = Mathf.Clamp(p.x / settings.CellSize, 0, map.Width - 1);
        float z = Mathf.Clamp((settings.chunkSize - p.z) / settings.CellSize, 0, map.Height - 1);
        int x0 = Mathf.FloorToInt(x), z0 = Mathf.FloorToInt(z);
        int x1 = Mathf.Min(x0 + 1, map.Width - 1), z1 = Mathf.Min(z0 + 1, map.Height - 1);
        return Mathf.Lerp(Mathf.Lerp(map.WorldHeights[x0, z0], map.WorldHeights[x1, z0], x - x0),
            Mathf.Lerp(map.WorldHeights[x0, z1], map.WorldHeights[x1, z1], x - x0), z - z0);
    }

    static void CreatePerimeter(Transform parent, Material material)
    {
        for (int row = 0; row < 2; row++)
        for (int i = 0; i < 19; i++)
        {
            float x = -20 + i * 2f + row * 0.3f;
            Part($"RearSandbag_{row}_{i}", parent, new Vector3(x, 0.25f + row * 0.42f, 17), new Vector3(1.85f, 0.45f, 0.8f), material);
            if (x > 1 && x < 9) continue;
            Part($"FrontSandbag_{row}_{i}", parent, new Vector3(x, 0.25f + row * 0.42f, -13), new Vector3(1.85f, 0.45f, 0.8f), material);
        }
    }

    static void PlaceGaugeConsole(Transform gun, Material metal)
    {
        ArtilleryGaugeDriver gauges = gun.GetComponentInChildren<ArtilleryGaugeDriver>(true);
        if (gauges == null) throw new InvalidOperationException("The imported artillery gauge set is missing.");
        Renderer[] renderers = gauges.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) throw new InvalidOperationException("The gauge set has no model renderers.");
        Bounds bounds = renderers[0].bounds;
        foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
        gauges.transform.localScale *= 1.5f / Mathf.Max(0.01f, bounds.size.x);
        bounds = renderers[0].bounds;
        foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
        gauges.transform.position += gun.TransformPoint(new Vector3(0, 1.35f, -3.7f)) - bounds.center;
        Part("InstrumentConsole", gun, new Vector3(0, 0.55f, -3.7f), new Vector3(2, 1.1f, 0.7f), metal);
    }

    static void CreateAccessRamp(Transform parent, TerrainSettings settings, Vector3 site, Material material)
    {
        Vector3 end = site + new Vector3(5, 0, -26);
        float ground = ProceduralBattlefieldSetup.SampleHeight(settings, end);
        float drop = Mathf.Max(0, parent.position.y - ground);
        GameObject ramp = Part("AccessRamp", parent, new Vector3(5, -drop * 0.5f - 0.08f, -20),
            new Vector3(6, 0.16f, Mathf.Sqrt(144 + drop * drop)), material);
        ramp.transform.localRotation = Quaternion.Euler(-Mathf.Atan2(drop, 12) * Mathf.Rad2Deg, 0, 0);
    }

    static void CreateFlag(Transform parent, Material metal, Material flag)
    {
        Part("SignalPole", parent, new Vector3(15, 3.5f, 14), new Vector3(0.1f, 7, 0.1f), metal);
        Part("BatteryFlag", parent, new Vector3(16.2f, 6.3f, 14), new Vector3(2.4f, 1.1f, 0.035f), flag);
    }

    static void CreateRoomLight(Transform room)
    {
        GameObject lamp = new GameObject("CommandPostLamp");
        lamp.transform.SetParent(room, false);
        lamp.transform.localPosition = new Vector3(0, 2.65f, 0);
        Light light = lamp.AddComponent<Light>();
        light.type = LightType.Point;
        light.range = 12;
        light.intensity = 3;
        light.color = new Color(0.92f, 0.96f, 0.88f);
    }

    static void CreateSun()
    {
        GameObject sun = new GameObject("BattlefieldSun");
        sun.transform.rotation = Quaternion.Euler(48, -35, 0);
        Light light = sun.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.4f;
        light.shadows = LightShadows.Soft;
        RenderSettings.sun = light;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.55f, 0.64f, 0.72f);
        RenderSettings.ambientEquatorColor = new Color(0.42f, 0.46f, 0.43f);
        RenderSettings.ambientGroundColor = new Color(0.24f, 0.25f, 0.22f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.66f, 0.72f, 0.75f);
        RenderSettings.fogDensity = 0.00022f;
    }

    static GameObject Part(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
    {
        GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        obj.name = name;
        obj.transform.SetParent(parent, false);
        obj.transform.localPosition = position;
        obj.transform.localScale = scale;
        obj.GetComponent<Renderer>().sharedMaterial = material;
        return obj;
    }

    static Material MaterialAsset(string name, Color color)
    {
        string path = "Assets/Materials/Base/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;
        material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.name = name;
        material.color = color;
        material.SetFloat("_Smoothness", 0.2f);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    static void RemoveChild(Transform root, string name)
    {
        Transform child = root.Find(name);
        if (child != null) Object.DestroyImmediate(child.gameObject);
    }

    static void ValidateScene()
    {
        IntegratedBattlefieldRig rig = Object.FindAnyObjectByType<IntegratedBattlefieldRig>();
        if (rig == null || rig.artillery == null || rig.player == null || rig.terrain == null ||
            rig.operatorCamera == null || rig.playerCamera == null || rig.overviewCamera == null || rig.handwheels.Length < 2)
            throw new InvalidOperationException("Integrated scene references are incomplete.");
        if (Object.FindObjectsByType<ArtilleryTestController>().Length != 1)
            throw new InvalidOperationException("The integrated scene must have exactly one artillery controller.");
        ArtilleryGaugeDriver gauges = rig.artillery.GetComponentInChildren<ArtilleryGaugeDriver>(true);
        if (gauges == null || gauges.artilleryController != rig.artillery || gauges.azimuthNeedleMesh == null ||
            gauges.elevationNeedleMesh == null || gauges.azimuthCenter == null || gauges.elevationCenter == null)
            throw new InvalidOperationException("Imported gauge references are incomplete.");
        foreach (ArtilleryCommandLever lever in Object.FindObjectsByType<ArtilleryCommandLever>())
            if (lever.artilleryController != rig.artillery) throw new InvalidOperationException("Unbound command lever.");
        if (!rig.terrain.HasGeneratedTerrain) throw new InvalidOperationException("No terrain mesh was generated.");
        ValidateTerrainPalette(rig.terrain.terrainSettings);
        Physics.SyncTransforms();
        Vector3 deck = rig.transform.position;
        Collider foundation = rig.transform.Find("Foundation").GetComponent<Collider>();
        if (!foundation.Raycast(new Ray(deck + new Vector3(5, 2, -8), Vector3.down), out RaycastHit hit, 4))
            throw new InvalidOperationException("The player spawn has no base floor collider.");
        rig.artillery.AddAzimuth(25);
        rig.artillery.AddElevation(10);
        if (Quaternion.Angle(rig.artillery.azimuthPivot.localRotation, Quaternion.Euler(0, 25, 0)) > 0.01f ||
            Mathf.Abs(rig.artillery.Elevation - 25) > 0.01f)
            throw new InvalidOperationException("Artillery aim did not reach the real gun pivots.");
        rig.artillery.ResetAim();
        foreach (IntegratedBattlefieldRig.ViewMode mode in Enum.GetValues(typeof(IntegratedBattlefieldRig.ViewMode)))
        {
            rig.SetView(mode);
            if (Object.FindObjectsByType<Camera>().Count(c => c.enabled) != 1 ||
                Object.FindObjectsByType<AudioListener>().Count(l => l.enabled) != 1)
                throw new InvalidOperationException("Camera or audio listener selection is ambiguous.");
        }
        rig.SetView(IntegratedBattlefieldRig.ViewMode.Overview);
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), ScenePath);
    }

    static void ValidateTerrainPalette(TerrainSettings settings)
    {
        HeightMap flat = new HeightMap(new float[,] { { 0.3f } }, biomes: new float[,] { { 0.3f } },
            slopes: new float[,] { { 0 } }, moisture: new float[,] { { 0.5f } });
        Color actual = TextureGenerator.BuildColorMap(flat, settings, Vector2.zero)[0];
        Color expected = settings.GetBiomeColor(0.3f);
        if (Mathf.Abs(actual.r - expected.r) + Mathf.Abs(actual.g - expected.g) + Mathf.Abs(actual.b - expected.b) > 0.01f)
            throw new InvalidOperationException("Flat, temperate terrain lost its biome color.");
        flat.Slopes[0, 0] = 50;
        Color rocky = TextureGenerator.BuildColorMap(flat, settings, Vector2.zero)[0];
        Color rockyExpected = Color.Lerp(expected, settings.surfacePalette.rockyColor, 0.75f);
        if (Mathf.Abs(rocky.r - rockyExpected.r) + Mathf.Abs(rocky.g - rockyExpected.g) + Mathf.Abs(rocky.b - rockyExpected.b) > 0.01f)
            throw new InvalidOperationException("Steep terrain did not receive the rock palette.");
    }

    static void CaptureOverview()
    {
        IntegratedBattlefieldRig rig = Object.FindAnyObjectByType<IntegratedBattlefieldRig>();
        CaptureCamera(rig.overviewCamera, "BaseProvisoriaOverview.png");
    }

    static void CaptureCamera(Camera camera, string filename)
    {
        RenderTexture target = new RenderTexture(1600, 900, 24);
        Texture2D image = new Texture2D(1600, 900, TextureFormat.RGB24, false);
        RenderTexture previous = RenderTexture.active;
        Canvas[] overlays = Object.FindObjectsByType<Canvas>().Where(c => c.renderMode == RenderMode.ScreenSpaceOverlay).ToArray();
        try
        {
            camera.targetTexture = target;
            foreach (Canvas canvas in overlays)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 0.5f;
            }
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
            image.Apply();
            System.IO.Directory.CreateDirectory("Artifacts/Integration/Captures");
            System.IO.File.WriteAllBytes("Artifacts/Integration/Captures/" + filename, image.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = null;
            foreach (Canvas canvas in overlays)
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.worldCamera = null;
            }
            RenderTexture.active = previous;
            target.Release();
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(image);
        }
    }
}
