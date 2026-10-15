using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class PhysicalInteractionValidation
{
    const string Key = "Leviathan.PhysicalInteraction.Validation";
    static double started;
    static int stage;
    static bool failed;
    static Camera testCamera;
    static RenderTexture testTarget;

    public static void VerifyPlayBatch()
    {
        EditorSceneManager.OpenScene(CartoonShaderSceneBuilder.ScenePath);
        SessionState.SetBool(Key, true);
        Resume();
        EditorApplication.EnterPlaymode();
    }

    [InitializeOnLoadMethod]
    static void Resume()
    {
        if (!SessionState.GetBool(Key, false)) return;
        started = EditorApplication.timeSinceStartup;
        stage = 0;
        failed = false;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        Application.logMessageReceived -= TrackError;
        Application.logMessageReceived += TrackError;
    }

    static void TrackError(string message, string trace, LogType type)
    {
        if (trace.Contains("UnityEditor.Search.")) return;
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) failed = true;
    }

    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup - started > 120) throw new InvalidOperationException("Physical interaction validation timed out.");
            if (!EditorApplication.isPlaying) return;
            IntegratedBattlefieldRig rig = Object.FindAnyObjectByType<IntegratedBattlefieldRig>();
            if (rig == null || !rig.terrain.HasGeneratedTerrain || rig.terrain.TerrainChunks.Values.Any(c => !c.HasGeneratedMesh)) return;
            if (stage == 0)
            {
                rig.SetView(IntegratedBattlefieldRig.ViewMode.Player);
                RunControlTests(rig);
                rig.SetView(IntegratedBattlefieldRig.ViewMode.Operator);
                foreach (HandwheelInteraction wheel in rig.handwheels) Require(wheel.enabled, "Handwheel disabled in operator view.");
                Require(rig.operatorInteractor.enabled && !rig.interactor.enabled, "Wrong interaction owner in operator view.");
                foreach (HandwheelInteraction wheel in rig.handwheels)
                {
                    Vector2 point = rig.operatorCamera.WorldToScreenPoint(wheel.wheelCenter.position);
                    Require(rig.operatorInteractor.TryBeginInteraction(point) && rig.operatorInteractor.ActiveControl == wheel,
                        "The original operator camera cannot grab " + wheel.name);
                    rig.operatorInteractor.CancelInteraction();
                }
                rig.artillery.AddAzimuth(30);
                rig.artillery.AddElevation(10);
                stage = 1;
                started = EditorApplication.timeSinceStartup;
            }
            else if (stage == 1 && EditorApplication.timeSinceStartup - started > 1)
            {
                Require(Object.FindObjectsByType<Canvas>().Length == 0, "Gameplay still contains a Canvas.");
                foreach (ArtilleryGaugeDriver gauge in Object.FindObjectsByType<ArtilleryGaugeDriver>())
                {
                    Transform pivot = gauge.azimuthNeedleMesh.parent;
                    Require(pivot.name == "Azimuth_Runtime_Pivot", "The analog gauge needle lost its runtime pivot.");
                    Require(Quaternion.Angle(pivot.localRotation, Quaternion.AngleAxis(gauge.azimuthZeroOffset, gauge.azimuthAxis)) > 10f,
                        "The physical azimuth needle did not respond.");
                }
                Capture(rig.operatorCamera, "Operator_Warmup.png");
                stage = 2;
                started = EditorApplication.timeSinceStartup;
            }
            else if (stage == 2 && EditorApplication.timeSinceStartup - started > 0.5)
            {
                Capture(rig.operatorCamera, "Operator_NoHUD.png");
                Transform instruments = Object.FindAnyObjectByType<MechanicalDialIndicator>().transform;
                testCamera.gameObject.SetActive(true);
                testCamera.transform.position = instruments.position + Vector3.up * 1.9f + Vector3.back * 1.2f;
                testCamera.transform.LookAt(instruments.position);
                testCamera.fieldOfView = 62f;
                Capture(testCamera, "Map_Warmup.png");
                stage = 3;
                started = EditorApplication.timeSinceStartup;
            }
            else if (stage == 3 && EditorApplication.timeSinceStartup - started > 0.5)
            {
                Capture(testCamera, "Map_PhysicalControls.png");
                testCamera.gameObject.SetActive(false);
                rig.SetView(IntegratedBattlefieldRig.ViewMode.Player);
                Require(rig.interactor.enabled && !rig.operatorInteractor.enabled && rig.handwheels.All(w => w.enabled),
                    "First-person controls were not restored.");
                rig.artillery.ResetAim();
                Require(!failed, "Unity reported errors during physical interaction validation.");
                Debug.Log("PHYSICAL_INTERACTION_PLAY_OK: circular/linear/fine drag, capture, stops, buttons, lock ownership, cancellation, physical needles and zero Canvas.");
                Finish(0);
            }
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            Finish(1);
        }
    }

    static void RunControlTests(IntegratedBattlefieldRig rig)
    {
        Vector3 origin = Vector3.zero;
        GameObject cameraObject = new GameObject("Interaction Test Camera");
        testCamera = cameraObject.AddComponent<Camera>();
        // Batch mode has no normal Game View; use a stable viewport for pixel-based drag.
        testTarget = new RenderTexture(1600, 900, 24);
        testCamera.targetTexture = testTarget;
        testCamera.nearClipPlane = 0.01f;
        testCamera.transform.position = origin + Vector3.back * 2f;
        testCamera.transform.LookAt(origin);
        CommandRoomInteractor interactor = cameraObject.AddComponent<CommandRoomInteractor>();
        interactor.viewCamera = testCamera;
        interactor.playerController = rig.player;
        interactor.interactDistance = 3.5f;
        interactor.interactMask = 1 << 31;
        interactor.useScreenCenter = false;

        GameObject wheelObject = new GameObject("Interaction Test Wheel");
        wheelObject.transform.position = origin;
        wheelObject.layer = 31;
        BoxCollider collider = wheelObject.AddComponent<BoxCollider>();
        collider.size = new Vector3(1, 1, 0.1f);
        HandwheelInteraction wheel = wheelObject.AddComponent<HandwheelInteraction>();
        wheel.wheelVisual = new GameObject("Test Wheel Rotor").transform;
        wheel.wheelVisual.SetParent(wheelObject.transform, false);
        wheel.wheelCenter = wheelObject.transform;
        wheel.controller = rig.artillery;
        wheel.controlType = HandwheelInteraction.ControlType.Azimuth;
        wheel.deadZonePixels = 2f;
        wheel.maxAngleStep = 30f;
        Physics.SyncTransforms();
        rig.artillery.ResetAim();
        Vector2 ScreenAngle(float angle)
        {
            float rad = angle * Mathf.Deg2Rad;
            Vector3 p = testCamera.WorldToScreenPoint(origin + new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0) * 0.3f);
            return new Vector2(p.x, p.y);
        }
        Require(interactor.TryBeginInteraction(ScreenAngle(0)), "Could not capture the handwheel collider.");
        Require(rig.player.ControlsLocked, "Player/camera did not lock on grab.");
        for (int i = 1; i <= 3; i++) interactor.DragInteraction(ScreenAngle(i * 20) - interactor.PointerPosition, 0.016f);
        Require(Mathf.Abs(rig.artillery.Azimuth - 30f) < 0.1f, $"Circular drag: azimuth={rig.artillery.Azimuth}, rotation={wheel.Rotation}, captured={interactor.ActiveControl != null}, viewport={testCamera.pixelWidth}x{testCamera.pixelHeight}.");
        float before = rig.artillery.Azimuth;
        interactor.DragInteraction(ScreenAngle(80) - interactor.PointerPosition, 0.016f, true);
        Require(Mathf.Abs(rig.artillery.Azimuth - before - 2f) < 0.1f, "Fine adjustment ratio is incorrect.");
        interactor.DragInteraction(testCamera.WorldToScreenPoint(origin) - (Vector3)interactor.PointerPosition, 0.016f);
        before = rig.artillery.Azimuth;
        interactor.DragInteraction(ScreenAngle(160) - interactor.PointerPosition, 0.016f);
        Require(Mathf.Abs(rig.artillery.Azimuth - before) < 0.1f, "Re-entering the wheel dead zone caused a jump.");
        interactor.DragInteraction(new Vector2(800, 300), 0.016f);
        Require(interactor.ActiveControl == wheel, "Capture was lost when the pointer left the collider.");
        Require(!interactor.TryBeginInteraction(ScreenAngle(0)), "A second control stole the capture.");
        interactor.CancelInteraction();
        Require(!rig.player.ControlsLocked, "Player lock leaked after release.");

        wheel.controlType = HandwheelInteraction.ControlType.Elevation;
        wheel.dragMode = RotaryInstrumentInteraction.DragMode.Horizontal;
        rig.artillery.ResetAim();
        Require(interactor.TryBeginInteraction(ScreenAngle(0)), "Linear handwheel capture failed.");
        interactor.DragInteraction(new Vector2(20, 0), 0.016f);
        Require(Mathf.Abs(rig.artillery.Elevation - 23f) < 0.1f, "Linear drag did not change elevation.");
        interactor.CancelInteraction();
        rig.artillery.AddElevation(100f);
        Quaternion atStop = wheel.wheelVisual.localRotation;
        wheel.RotateBy(40f);
        Require(Quaternion.Angle(atStop, wheel.wheelVisual.localRotation) < 0.01f, "Handwheel spins past the artillery stop.");
        wheel.RotateBy(-20f);
        Require(Mathf.Abs(rig.artillery.Elevation - (rig.artillery.maxElevation - 10f)) < 0.1f, "Handwheel cannot reverse off its configured stop.");

        object anotherOwner = new object();
        rig.player.AcquireControlLock(anotherOwner);
        Require(interactor.TryBeginInteraction(ScreenAngle(0)), "Lock ownership capture failed.");
        interactor.CancelInteraction();
        Require(rig.player.ControlsLocked, "Interaction released another station's player lock.");
        rig.player.ReleaseControlLock(anotherOwner);
        Require(interactor.TryBeginInteraction(ScreenAngle(0)), "Disable cancellation capture failed.");
        wheel.enabled = false;
        interactor.DragInteraction(Vector2.one, 0.016f);
        Require(interactor.ActiveControl == null && !rig.player.ControlsLocked, "Disabled control kept mouse/player capture.");
        wheel.enabled = true;
        Require(interactor.TryBeginInteraction(ScreenAngle(0)), "Distance cancellation capture failed.");
        testCamera.transform.position += Vector3.back * 10f;
        interactor.DragInteraction(Vector2.one, 0.016f);
        Require(interactor.ActiveControl == null && !rig.player.ControlsLocked, "Interaction continued outside reach.");
        Object.DestroyImmediate(wheelObject);
        interactor.interactMask = ~0;

        RotaryInstrumentInteraction crank = Object.FindObjectsByType<RotaryInstrumentInteraction>().First(r => !(r is HandwheelInteraction));
        crank.RotateBy(90f);
        Require(Mathf.Abs(crank.Rotation - 90f) < 0.1f, "Map crank does not rotate.");
        PhysicalPushButton button = Object.FindObjectsByType<PhysicalPushButton>().First(b => b.name == "ResetMapButton");
        Vector3 rest = button.buttonVisual.localPosition;
        int pressed = 0, released = 0;
        button.onPressed.AddListener(() => pressed++);
        button.onReleased.AddListener(() => released++);
        testCamera.transform.position = button.transform.position + Vector3.up * 1.5f;
        testCamera.transform.LookAt(button.transform.position);
        Physics.SyncTransforms();
        Vector2 point = testCamera.WorldToScreenPoint(button.transform.position);
        Require(interactor.TryBeginInteraction(point), "Map button collider is unreachable.");
        Require(button.IsPressed && pressed == 1 && Mathf.Abs(crank.Rotation) < 0.1f &&
            Vector3.Distance(rest, button.buttonVisual.localPosition) > 0.01f, "Physical button did not press/reset the protractor.");
        for (int i = 0; i < 4; i++) interactor.DragInteraction(Vector2.zero, 0.016f);
        Require(pressed == 1, "Holding the button repeats its command.");
        interactor.enabled = false;
        Require(!button.IsPressed && released == 1 && !rig.player.ControlsLocked, "Button/interactor disable did not release.");
        interactor.enabled = true;
        PhysicalPushButton gunButton = Object.FindObjectsByType<PhysicalPushButton>().First(b => b.name == "ResetArtilleryButton");
        rig.artillery.AddAzimuth(25);
        gunButton.BeginInteract(interactor, default);
        gunButton.EndInteract(interactor);
        Require(Mathf.Abs(rig.artillery.Azimuth) < 0.1f && Mathf.Abs(rig.artillery.Elevation - 15) < 0.1f, "The physical artillery reset button is unbound.");
        testCamera.gameObject.SetActive(false);
        Debug.Log("PHYSICAL_CONTROL_TESTS_OK");
    }

    static void Capture(Camera camera, string filename)
    {
        string folder = "Artifacts/Interaction/Captures";
        Directory.CreateDirectory(folder);
        RenderTexture target = new RenderTexture(1600, 900, 24);
        RenderTexture previous = RenderTexture.active;
        RenderTexture previousTarget = camera.targetTexture;
        Texture2D image = new Texture2D(1600, 900, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target;
            camera.Render();
            camera.Render();
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
            image.Apply();
            Require(image.GetPixels32().Count(p => p.r > 220 && p.b > 220 && p.g < 40) < 100, "Error shader in " + filename);
            File.WriteAllBytes(Path.Combine(folder, filename), image.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = previousTarget;
            RenderTexture.active = previous;
            target.Release();
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(image);
        }
    }

    static void Finish(int code)
    {
        if (testTarget != null)
        {
            if (testCamera != null) testCamera.targetTexture = null;
            testTarget.Release();
            Object.DestroyImmediate(testTarget);
        }
        SessionState.SetBool(Key, false);
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= TrackError;
        EditorApplication.Exit(code);
    }
}
