
using UnityEngine;
using UnityEngine.UI;

public class ArtilleryTestSceneBuilder : MonoBehaviour
{
    [ContextMenu("Build Artillery Test Scene")]
    public void BuildScene()
    {
        var old = GameObject.Find("ArtilleryTestGenerated");
        if (old != null) DestroyImmediate(old);

        var root = new GameObject("ArtilleryTestGenerated");

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

        var ground = Make(PrimitiveType.Cube, "Ground", root.transform,
            new Vector3(0, -0.25f, 2), new Vector3(12, 0.5f, 18));
        Material(ground, new Color(0.20f, 0.22f, 0.20f));

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
        cameraObject.transform.position = new Vector3(0, 2.3f, -5.5f);
        cameraObject.transform.LookAt(new Vector3(0, 1.5f, 4f));
        var cam = cameraObject.AddComponent<Camera>();
        cam.fieldOfView = 65f;
        cam.nearClipPlane = 0.05f;
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.tag = "MainCamera";

        var lightObject = new GameObject("TestLight");
        lightObject.transform.SetParent(root.transform, false);
        lightObject.transform.rotation = Quaternion.Euler(50, -30, 0);
        lightObject.AddComponent<Light>().type = LightType.Directional;

        var canvasObject = new GameObject("OperatorPanel");
        canvasObject.transform.SetParent(root.transform, false);
        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObject.AddComponent<CanvasScaler>().uiScaleMode =
            CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasObject.AddComponent<GraphicRaycaster>();

        var controller = root.AddComponent<ArtilleryTestController>();
        controller.azimuthPivot = azimuth;
        controller.elevationPivot = elevation;

        var panel = new GameObject("ReadoutPanel");
        panel.transform.SetParent(canvas.transform, false);
        var panelRect = panel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0);
        panelRect.anchorMax = new Vector2(0.5f, 0);
        panelRect.pivot = new Vector2(0.5f, 0);
        panelRect.anchoredPosition = new Vector2(0, 24);
        panelRect.sizeDelta = new Vector2(520, 105);

        var panelImage = panel.AddComponent<Image>();
        panelImage.color = new Color(0.06f, 0.07f, 0.06f, 0.92f);

        Text Label(string name, string value, Vector2 pos, Transform parent)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var rect = obj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = new Vector2(240, 35);

            var text = obj.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = 20;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(0.85f, 0.78f, 0.55f);
            text.text = value;
            return text;
        }

        controller.azimuthText = Label("AzimuthReadout",
            "AZIMUTE 000.0°", new Vector2(-125, 15), panel.transform);
        controller.elevationText = Label("ElevationReadout",
            "ELEVAÇÃO 15.0°", new Vector2(125, 15), panel.transform);

        Label("ControlsHelp",
            "A/D: AZIMUTE    W/S: ELEVAÇÃO    SHIFT: FINO    R: RESET",
            new Vector2(0, -25), panel.transform);

        Debug.Log("Estrutura de teste criada. Configure e salve a cena.");
    }
}
