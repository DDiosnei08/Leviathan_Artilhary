using UnityEngine;
using ProjectLeviathan.ProceduralTerrain;

public class CommandRoomTestSceneBuilder : MonoBehaviour
{
    [ContextMenu("Build Command Room Test Scene")]
    public void BuildScene()
    {
        var old = GameObject.Find("CommandRoomGenerated");
        if (old != null) DestroyImmediate(old);

        var root = new GameObject("CommandRoomGenerated");
        TerrainSettings terrainSettings = ProceduralBattlefieldSetup.LoadTerrainSettings();
        Vector3 bunkerPosition = new Vector3(620f, 0f, 590f);
        ProceduralBattlefieldSetup.PlaceOnTerrain(root.transform, terrainSettings, bunkerPosition, 0.18f);

        GameObject Make(PrimitiveType type, string name,
            Transform parent, Vector3 pos, Vector3 scale)
        {
            var obj = GameObject.CreatePrimitive(type);
            obj.name = name;
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = pos;
            obj.transform.localScale = scale;
            return obj;
        }

        void Material(GameObject obj, Color color)
        {
            var renderer = obj.GetComponent<Renderer>();
            if (renderer != null)
                renderer.material.color = color;
        }

        var floor = Make(PrimitiveType.Cube, "Floor", root.transform,
            new Vector3(0, -0.1f, 0), new Vector3(12, 0.2f, 12));
        Material(floor, new Color(0.18f, 0.20f, 0.19f));

        var backWall = Make(PrimitiveType.Cube, "BackWall", root.transform,
            new Vector3(0, 1.5f, 5.9f), new Vector3(12, 3f, 0.2f));
        Material(backWall, new Color(0.24f, 0.25f, 0.24f));

        var leftWall = Make(PrimitiveType.Cube, "LeftWall", root.transform,
            new Vector3(-5.9f, 1.5f, 0), new Vector3(0.2f, 3f, 12));
        Material(leftWall, new Color(0.22f, 0.23f, 0.23f));

        var rightWall = Make(PrimitiveType.Cube, "RightWall", root.transform,
            new Vector3(5.9f, 1.5f, 0), new Vector3(0.2f, 3f, 12));
        Material(rightWall, new Color(0.22f, 0.23f, 0.23f));

        var ceiling = Make(PrimitiveType.Cube, "Ceiling", root.transform,
            new Vector3(0, 3.05f, 0), new Vector3(12, 0.15f, 12));
        Material(ceiling, new Color(0.16f, 0.17f, 0.17f));

        var controlBase = Make(PrimitiveType.Cube, "ArtilleryControlBase", root.transform,
            new Vector3(0, 0.55f, 3.5f), new Vector3(3.4f, 1.1f, 1.2f));
        Material(controlBase, new Color(0.28f, 0.29f, 0.27f));

        var panelTop = Make(PrimitiveType.Cube, "SlantedControlPanel", controlBase.transform,
            new Vector3(0, 0.58f, -0.06f), new Vector3(3.2f, 0.16f, 1.0f));
        panelTop.transform.localRotation = Quaternion.Euler(-18f, 0, 0);
        Material(panelTop, new Color(0.12f, 0.13f, 0.12f));

        var mapTable = Make(PrimitiveType.Cube, "CentralMapTable", root.transform,
            new Vector3(0, 0.55f, 0), new Vector3(3.6f, 0.25f, 2.3f));
        Material(mapTable, new Color(0.26f, 0.20f, 0.14f));

        var mapSurface = Make(PrimitiveType.Cube, "MapSurface", root.transform,
            new Vector3(0, 0.72f, 0), new Vector3(3.25f, 0.035f, 1.95f));
        Material(mapSurface, new Color(0.12f, 0.28f, 0.22f));

        for (int i = -2; i <= 2; i++)
        {
            var mapLineA = Make(PrimitiveType.Cube, "MapGridLineX_" + i, root.transform,
                new Vector3(i * 0.55f, 0.745f, 0), new Vector3(0.015f, 0.01f, 1.85f));
            Material(mapLineA, new Color(0.55f, 0.70f, 0.55f));

            var mapLineB = Make(PrimitiveType.Cube, "MapGridLineZ_" + i, root.transform,
                new Vector3(0, 0.75f, i * 0.34f), new Vector3(3.05f, 0.01f, 0.015f));
            Material(mapLineB, new Color(0.55f, 0.70f, 0.55f));
        }

        var ammoRack = Make(PrimitiveType.Cube, "AmmoRack", root.transform,
            new Vector3(-4.4f, 0.65f, 2.7f), new Vector3(1.1f, 1.3f, 1.0f));
        Material(ammoRack, new Color(0.18f, 0.16f, 0.12f));

        for (int i = 0; i < 4; i++)
        {
            var shell = Make(PrimitiveType.Cylinder, "AmmoShell_" + i, root.transform,
                new Vector3(-4.75f + i * 0.22f, 1.42f, 2.7f), new Vector3(0.08f, 0.45f, 0.08f));
            shell.transform.localRotation = Quaternion.Euler(90f, 0, 0);
            Material(shell, new Color(0.45f, 0.38f, 0.18f));
        }

        var artilleryRoot = new GameObject("VirtualArtilleryController");
        artilleryRoot.transform.SetParent(root.transform, false);

        var azimuthPivot = new GameObject("AzimuthPivot").transform;
        azimuthPivot.SetParent(artilleryRoot.transform, false);

        var elevationPivot = new GameObject("ElevationPivot").transform;
        elevationPivot.SetParent(azimuthPivot, false);

        var artilleryController = artilleryRoot.AddComponent<ArtilleryTestController>();
        artilleryController.azimuthPivot = azimuthPivot;
        artilleryController.elevationPivot = elevationPivot;

        var azLever = CreateLever("AzimuthLever", root.transform,
            new Vector3(-0.75f, 1.35f, 3.15f), artilleryController,
            ArtilleryCommandLever.LeverMode.Azimuth);

        var elLever = CreateLever("ElevationLever", root.transform,
            new Vector3(0.75f, 1.35f, 3.15f), artilleryController,
            ArtilleryCommandLever.LeverMode.Elevation);
        elLever.leverAxis = Vector3.forward;

        var ammoLever = CreateLever("AmmoLever", root.transform,
            new Vector3(1.35f, 1.25f, 3.65f), artilleryController,
            ArtilleryCommandLever.LeverMode.AmmoType);
        ammoLever.minLeverAngle = -25f;
        ammoLever.maxLeverAngle = 25f;

        var player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        player.name = "Player";
        player.transform.SetParent(root.transform, false);
        player.transform.localPosition = new Vector3(0, 1f, -3.8f);
        player.transform.localScale = new Vector3(1f, 1f, 1f);
        Material(player, new Color(0.20f, 0.30f, 0.36f));
        DestroyImmediate(player.GetComponent<CapsuleCollider>());
        player.AddComponent<CharacterController>();

        var cameraPivot = new GameObject("PlayerCamera");
        cameraPivot.transform.SetParent(player.transform, false);
        cameraPivot.transform.localPosition = new Vector3(0, 0.62f, 0);
        var camera = cameraPivot.AddComponent<Camera>();
        camera.fieldOfView = 70f;
        camera.nearClipPlane = 0.05f;
        camera.farClipPlane = 6000f;
        camera.tag = "MainCamera";
        ProceduralBattlefieldSetup.CreateBattlefield("Procedural Battlefield", camera.transform);

        var playerController = player.AddComponent<LeviathanPlayerController>();
        playerController.cameraPivot = cameraPivot.transform;

        var interactor = player.AddComponent<CommandRoomInteractor>();
        interactor.viewCamera = camera;
        interactor.interactDistance = 3f;

        PhysicalInstrumentBuilder.CreateGauges(root.transform, artilleryController);
        PhysicalInstrumentBuilder.CreateMapControls(mapSurface.transform, artilleryController);

        var lightObject = new GameObject("CommandRoomLight");
        lightObject.transform.SetParent(root.transform, false);
        lightObject.transform.rotation = Quaternion.Euler(55, -35, 0);
        var light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.15f;

        RenderSettings.ambientLight = new Color(0.25f, 0.26f, 0.25f);
        RenderSettings.fog = true;
        RenderSettings.fogColor = new Color(0.55f, 0.60f, 0.58f);
        RenderSettings.fogDensity = 0.00045f;

        artilleryController.SendMessage("Start", SendMessageOptions.DontRequireReceiver);

        Debug.Log("Sala de comando criada. Aproxime das alavancas, mire no centro da tela e segure clique esquerdo.");
    }

    ArtilleryCommandLever CreateLever(string name, Transform parent, Vector3 pos,
        ArtilleryTestController controller, ArtilleryCommandLever.LeverMode mode)
    {
        var root = new GameObject(name);
        root.transform.SetParent(parent, false);
        root.transform.localPosition = pos;

        var baseObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        baseObj.name = name + "_Base";
        baseObj.transform.SetParent(root.transform, false);
        baseObj.transform.localScale = new Vector3(0.24f, 0.08f, 0.24f);
        baseObj.GetComponent<Renderer>().material.color = new Color(0.16f, 0.16f, 0.15f);

        var pivot = new GameObject(name + "_Pivot");
        pivot.transform.SetParent(root.transform, false);
        pivot.transform.localPosition = new Vector3(0, 0.12f, 0);

        var stick = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        stick.name = name + "_Stick";
        stick.transform.SetParent(pivot.transform, false);
        stick.transform.localPosition = new Vector3(0, 0.28f, 0);
        stick.transform.localScale = new Vector3(0.045f, 0.32f, 0.045f);
        stick.GetComponent<Renderer>().material.color = new Color(0.42f, 0.38f, 0.28f);

        var handle = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        handle.name = name + "_Handle";
        handle.transform.SetParent(pivot.transform, false);
        handle.transform.localPosition = new Vector3(0, 0.62f, 0);
        handle.transform.localScale = new Vector3(0.18f, 0.18f, 0.18f);
        handle.GetComponent<Renderer>().material.color = new Color(0.58f, 0.10f, 0.08f);

        var collider = root.AddComponent<BoxCollider>();
        collider.center = new Vector3(0, 0.36f, 0);
        collider.size = new Vector3(0.55f, 0.9f, 0.55f);

        var lever = root.AddComponent<ArtilleryCommandLever>();
        lever.displayName = name;
        lever.artilleryController = controller;
        lever.mode = mode;
        lever.leverPivot = pivot.transform;

        return lever;
    }
}
