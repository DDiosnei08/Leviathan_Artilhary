
using UnityEngine;
using ProjectLeviathan.ProceduralTerrain;

public class ArtilleryTestSceneBuilder : MonoBehaviour
{
    [ContextMenu("Build Artillery Test Scene")]
    public void BuildScene()
    {
        var old = GameObject.Find("ArtilleryTestGenerated");
        if (old != null) DestroyImmediate(old);

        var root = new GameObject("ArtilleryTestGenerated");
        TerrainSettings terrainSettings = ProceduralBattlefieldSetup.LoadTerrainSettings();
        Vector3 battlefieldPosition = new Vector3(600f, 0f, 600f);
        ProceduralBattlefieldSetup.PlaceOnTerrain(root.transform, terrainSettings, battlefieldPosition, 0.35f);

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
            renderer.material.color = color;
        }

        var ground = Make(PrimitiveType.Cube, "StagingPad", root.transform,
            new Vector3(0, -0.08f, 2), new Vector3(8.5f, 0.16f, 10.5f));
        Material(ground, new Color(0.17f, 0.19f, 0.17f));

        var baseGun = Make(PrimitiveType.Cylinder, "GunBase", root.transform,
            new Vector3(0, 0.3f, 2), new Vector3(1.2f, 0.3f, 1.2f));
        Material(baseGun, new Color(0.22f, 0.24f, 0.23f));

        var azimuth = new GameObject("AzimuthPivot").transform;
        azimuth.SetParent(root.transform, false);
        azimuth.localPosition = new Vector3(0, 0.65f, 2);

        var turntable = Make(PrimitiveType.Cylinder, "Turntable",
            azimuth, new Vector3(0, 0, 0), new Vector3(0.8f, 0.12f, 0.8f));
        Material(turntable.gameObject, new Color(0.35f, 0.30f, 0.22f));

        var elevation = new GameObject("ElevationPivot").transform;
        elevation.SetParent(azimuth, false);
        elevation.localPosition = new Vector3(0, 0.2f, 0);

        var support = Make(PrimitiveType.Cube, "BarrelSupport",
            elevation, new Vector3(0, 0, 0),
            new Vector3(0.65f, 0.45f, 0.55f));
        Material(support, new Color(0.28f, 0.30f, 0.29f));

        var barrel = Make(PrimitiveType.Cylinder, "Barrel",
            elevation, new Vector3(0, 0, 1.25f),
            new Vector3(0.18f, 1.35f, 0.18f));
        barrel.transform.localRotation = Quaternion.Euler(90, 0, 0);
        Material(barrel, new Color(0.16f, 0.18f, 0.18f));

        var muzzle = Make(PrimitiveType.Cylinder, "Muzzle",
            elevation, new Vector3(0, 0, 2.55f),
            new Vector3(0.24f, 0.13f, 0.24f));
        muzzle.transform.localRotation = Quaternion.Euler(90, 0, 0);
        Material(muzzle, new Color(0.32f, 0.30f, 0.25f));

        var cameraObject = new GameObject("OperatorCamera");
        cameraObject.transform.SetParent(root.transform, false);
        cameraObject.transform.localPosition = new Vector3(0, 1.75f, -3.6f);
        cameraObject.transform.LookAt(root.transform.TransformPoint(new Vector3(0, 1.15f, -1.7f)));
        var cam = cameraObject.AddComponent<Camera>();
        cam.fieldOfView = 65f;
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 6000f;
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.tag = "MainCamera";
        ProceduralBattlefieldSetup.CreateBattlefield("Procedural Battlefield", cameraObject.transform);

        var lightObject = new GameObject("TestLight");
        lightObject.transform.SetParent(root.transform, false);
        lightObject.transform.rotation = Quaternion.Euler(50, -30, 0);
        var light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.25f;
        RenderSettings.ambientLight = new Color(0.34f, 0.36f, 0.33f);
        RenderSettings.fog = true;
        RenderSettings.fogColor = new Color(0.55f, 0.60f, 0.58f);
        RenderSettings.fogDensity = 0.00045f;

        var controller = root.AddComponent<ArtilleryTestController>();
        controller.azimuthPivot = azimuth;
        controller.elevationPivot = elevation;
        var interactor = cameraObject.AddComponent<CommandRoomInteractor>();
        interactor.viewCamera = cam;
        interactor.useScreenCenter = false;
        interactor.interactDistance = 4f;
        PhysicalInstrumentBuilder.CreateGauges(root.transform, controller);
        PhysicalInstrumentBuilder.CreateHandwheels(root.transform, controller);

        Debug.Log("Estrutura de teste criada. Configure e salve a cena.");
    }
}
