using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class PhysicalInteractionSceneSetup
{
    static readonly string[] ScenePaths =
    {
        "Assets/Scenes/Tests/Test_ArtilleryControls/Test_ArtilleryControls.unity",
        BaseIntegrationBuilder.ScenePath
    };

    [MenuItem("Tools/Leviathan/Interaction/Configure Physical Controls In Open Scene")]
    public static void ConfigureOpenScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        ConfigureScene(SceneManager.GetActiveScene());
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }

    public static void ApplyBatch()
    {
        foreach (string path in ScenePaths)
        {
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            ConfigureScene(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Cannot save " + path);
            Debug.Log("PHYSICAL_SCENE_UPDATED: " + path);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("PHYSICAL_SCENES_SETUP_OK");
    }

    static T[] Components<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
        .SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();

    public static void ConfigureScene(Scene scene)
    {
        foreach (Canvas canvas in Components<Canvas>(scene))
            if (canvas != null) Object.DestroyImmediate(canvas.gameObject);
        if (Components<ProjectLeviathan.Base.BaseAssembly>(scene).Length>0)
        {
            // The rotating-base prefab owns its independently wired twin-cannon controls.
            Debug.Log("Physical rotating-base controls already configured by the prefab.");
            return;
        }
        ArtilleryTestController[] controllers = Components<ArtilleryTestController>(scene);
        ArtilleryTestController controller = controllers.FirstOrDefault(c => c.GetComponentsInChildren<Renderer>(true).Length > 0)
            ?? controllers.FirstOrDefault();
        if (controller == null) throw new InvalidOperationException("This scene has no artillery controller.");

        foreach (HandwheelInteraction wheel in Components<HandwheelInteraction>(scene))
        {
            wheel.controller = controller;
            wheel.enabled = true;
            wheel.interactionDistance = 3.5f;
            wheel.deadZonePixels = 12f;
            wheel.maxAngleStep = 30f;
            AddWheelWitnessMarks(wheel);
        }
        foreach (ArtilleryCommandLever lever in Components<ArtilleryCommandLever>(scene)) lever.artilleryController = controller;
        if (Components<ArtilleryGaugeDriver>(scene).Length == 0) PhysicalInstrumentBuilder.CreateGauges(controller.transform, controller);
        foreach (ArtilleryGaugeDriver gauges in Components<ArtilleryGaugeDriver>(scene)) gauges.artilleryController = controller;
        foreach (Transform surface in Components<Transform>(scene).Where(t => t.name == "MapSurface"))
            PhysicalInstrumentBuilder.CreateMapControls(surface, controller);

        IntegratedBattlefieldRig rig = Components<IntegratedBattlefieldRig>(scene).FirstOrDefault();
        if (rig != null)
        {
            rig.operatorInteractor = Interactor(rig.operatorCamera, false);
            rig.interactor = rig.player.GetComponent<CommandRoomInteractor>();
            rig.interactor.playerController = rig.player;
            rig.interactor.viewCamera = rig.playerCamera;
            rig.interactor.interactDistance = 3.5f;
            rig.interactor.useScreenCenter = true;
            rig.handwheels = Components<HandwheelInteraction>(scene);
            rig.operatorInteractor.enabled = false;
        }
        else
        {
            LeviathanPlayerController player = Components<LeviathanPlayerController>(scene).FirstOrDefault();
            Camera camera = player != null ? player.GetComponentInChildren<Camera>(true) : Components<Camera>(scene).FirstOrDefault();
            if (camera == null) throw new InvalidOperationException("This scene has no interaction camera.");
            foreach (Camera other in Components<Camera>(scene))
            {
                other.enabled = other == camera;
                other.tag = other == camera ? "MainCamera" : "Untagged";
                if (other.TryGetComponent(out AudioListener listener)) listener.enabled = other == camera;
            }
            CommandRoomInteractor interactor = player != null ? player.GetComponent<CommandRoomInteractor>() : null;
            if (interactor == null) interactor = Interactor(camera, player != null);
            interactor.playerController = player;
            interactor.viewCamera = camera;
            interactor.interactDistance = 3.5f;
            interactor.useScreenCenter = player != null;
            interactor.enabled = true;
        }
        if (Components<Canvas>(scene).Length != 0) throw new InvalidOperationException("Canvas remains in gameplay scene.");
    }

    static CommandRoomInteractor Interactor(Camera camera, bool center)
    {
        if (camera == null) throw new InvalidOperationException("Missing control camera.");
        CommandRoomInteractor interactor = camera.GetComponent<CommandRoomInteractor>();
        if (interactor == null) interactor = camera.gameObject.AddComponent<CommandRoomInteractor>();
        interactor.viewCamera = camera;
        interactor.useScreenCenter = center;
        interactor.interactDistance = 3.5f;
        return interactor;
    }

    static void AddWheelWitnessMarks(HandwheelInteraction wheel)
    {
        if (wheel.wheelVisual == null || wheel.wheelVisual.Find("RotationWitnessMark_Front") != null) return;
        Renderer[] renderers = wheel.wheelVisual.GetComponentsInChildren<Renderer>(true);
        Renderer[] rims = renderers.Where(renderer => renderer.name.Contains("WheelRim")).ToArray();
        if (rims.Length > 0) renderers = rims;
        if (renderers.Length == 0) return;
        Bounds local = default;
        bool initialized = false;
        foreach (Renderer renderer in renderers)
        {
            Bounds bounds = renderer.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = bounds.center + Vector3.Scale(bounds.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 point = wheel.wheelVisual.InverseTransformPoint(corner);
                if (!initialized) { local = new Bounds(point, Vector3.zero); initialized = true; }
                else local.Encapsulate(point);
            }
        }
        Vector3 axis = wheel.rotationAxis.normalized;
        Vector3 radial = Vector3.ProjectOnPlane(Vector3.up, axis).normalized;
        if (radial.sqrMagnitude < 0.1f) radial = Vector3.ProjectOnPlane(Vector3.forward, axis).normalized;
        Vector3 absoluteAxis = new Vector3(Mathf.Abs(axis.x), Mathf.Abs(axis.y), Mathf.Abs(axis.z));
        Vector3 absoluteRadial = new Vector3(Mathf.Abs(radial.x), Mathf.Abs(radial.y), Mathf.Abs(radial.z));
        float radius = Mathf.Max(0.03f, Vector3.Dot(local.extents, absoluteRadial));
        float halfDepth = Vector3.Dot(local.extents, absoluteAxis);
        Material accent = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Base/BaseSafety.mat");
        foreach (int side in new[] { -1, 1 })
        {
            GameObject mark = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mark.name = side < 0 ? "RotationWitnessMark_Front" : "RotationWitnessMark_Back";
            mark.transform.SetParent(wheel.wheelVisual, false);
            mark.transform.localPosition = local.center + radial * radius * 0.7f + axis * side * (halfDepth + 0.009f);
            mark.transform.localRotation = Quaternion.LookRotation(axis, radial);
            mark.transform.localScale = new Vector3(radius * 0.18f, radius * 0.3f, 0.012f);
            mark.GetComponent<Renderer>().sharedMaterial = accent;
            Object.DestroyImmediate(mark.GetComponent<Collider>());
        }
    }
}
