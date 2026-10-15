using UnityEngine;
using UnityEngine.Events;
using System.Linq;

public static class PhysicalInstrumentBuilder
{
    static Material Material(string name)
    {
#if UNITY_EDITOR
        Material asset = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Base/" + name + ".mat");
        if (asset != null) return asset;
#endif
        return new Material(Shader.Find("Universal Render Pipeline/Lit"));
    }

    static GameObject Part(string name, Transform parent, Vector3 position, Vector3 scale, Material material,
        PrimitiveType primitive = PrimitiveType.Cube, bool collider = false)
    {
        GameObject part = GameObject.CreatePrimitive(primitive);
        part.name = name;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.transform.localScale = scale;
        part.GetComponent<Renderer>().sharedMaterial = material;
        if (!collider) Object.DestroyImmediate(part.GetComponent<Collider>());
        return part;
    }

    static Transform Root(string name, Transform parent, Vector3 position)
    {
        Transform root = new GameObject(name).transform;
        root.SetParent(parent, false);
        root.localPosition = position;
        return root;
    }

    static void Print(string value, Transform parent, Vector3 position, float size = 0.03f)
    {
        Transform label = Root("Plate_" + value, parent, position);
        label.localRotation = Quaternion.Euler(90f, 0f, 0f);
        TextMesh text = label.gameObject.AddComponent<TextMesh>();
        text.text = value;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.GetComponent<MeshRenderer>().sharedMaterial = text.font.material;
        text.fontSize = 64;
        text.characterSize = size;
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
        text.color = new Color(0.92f, 0.95f, 0.9f);
    }

    static Transform Dial(Transform parent, string name, Vector3 position, bool elevation, out Transform needle)
    {
        Transform dial = Root(name, parent, position);
        Material metal = Material("BaseMetal"), face = Material("BaseConcrete"), accent = Material("BaseSafety");
        Part("DialHousing", dial, Vector3.zero, new Vector3(0.76f, 0.02f, 0.76f), metal, PrimitiveType.Cylinder);
        Part("DialFace", dial, Vector3.up * 0.025f, new Vector3(0.7f, 0.008f, 0.7f), face, PrimitiveType.Cylinder);
        for (int i = 0; i < 36; i++)
        {
            float angle = i * 10f;
            Vector3 radial = Quaternion.Euler(0, angle, 0) * Vector3.forward;
            GameObject tick = Part("Tick_" + i, dial, radial * 0.315f + Vector3.up * 0.04f,
                new Vector3(0.012f, 0.006f, i % 3 == 0 ? 0.065f : 0.035f), accent);
            tick.transform.localRotation = Quaternion.Euler(0, angle, 0);
            if (elevation ? i % 3 == 0 && i <= 6 : i % 9 == 0) Print(elevation ? angle.ToString("0") : new[] { "N", "E", "S", "O" }[i / 9],
                dial, radial * 0.23f + Vector3.up * 0.05f, 0.03f);
        }
        Transform pointer = Root("NeedlePivot", dial, Vector3.up * 0.06f);
        needle = Part("Needle", pointer, Vector3.forward * 0.13f, new Vector3(0.025f, 0.01f, 0.29f), accent).transform;
        return pointer;
    }

    public static ArtilleryGaugeDriver CreateGauges(Transform parent, ArtilleryTestController controller)
    {
        Transform existing = parent.Find("PhysicalGaugeConsole");
        if (existing != null) return existing.GetComponent<ArtilleryGaugeDriver>();
        Transform root = Root("PhysicalGaugeConsole", parent, new Vector3(0, 1.3f, -2f));
        root.localRotation = Quaternion.Euler(65f, 0, 0);
        Part("ConsolePlate", root, new Vector3(0, -0.04f, 0), new Vector3(1.7f, 0.05f, 0.95f), Material("BaseMetal"));
        Transform azimuth = Dial(root, "AzimuthDial3D", new Vector3(-0.43f, 0, 0), false, out Transform azimuthNeedle);
        Transform elevation = Dial(root, "ElevationDial3D", new Vector3(0.43f, 0, 0), true, out Transform elevationNeedle);
        Print("AZIMUTE", root, new Vector3(-0.43f, 0.015f, -0.43f), 0.025f);
        Print("ELEVACAO", root, new Vector3(0.43f, 0.015f, -0.43f), 0.025f);
        ArtilleryGaugeDriver driver = root.gameObject.AddComponent<ArtilleryGaugeDriver>();
        driver.artilleryController = controller;
        driver.azimuthNeedleMesh = azimuthNeedle;
        driver.elevationNeedleMesh = elevationNeedle;
        driver.azimuthCenter = azimuth;
        driver.elevationCenter = elevation;
        driver.azimuthAxis = driver.elevationAxis = Vector3.up;
        driver.invertElevation = false;
        return driver;
    }

    public static void CreateHandwheels(Transform parent, ArtilleryTestController controller)
    {
        foreach (HandwheelInteraction.ControlType type in System.Enum.GetValues(typeof(HandwheelInteraction.ControlType)))
        {
            string name = type + "HandwheelPivot";
            if (parent.Find(name) != null) continue;
            Transform root = Root(name, parent, new Vector3(type == HandwheelInteraction.ControlType.Azimuth ? -0.85f : 0.85f, 1.05f, -1.7f));
            Transform rotor = Root("WheelRotationPivot", root, Vector3.zero);
            Material metal = Material("BaseMetal"), accent = Material("BaseSafety");
            for (int i = 0; i < 16; i++)
            {
                float angle = i * 22.5f;
                Vector3 radial = Quaternion.Euler(0, 0, angle) * Vector3.right;
                GameObject rim = Part("WheelRim_" + i, rotor, radial * 0.24f, new Vector3(0.11f, 0.035f, 0.035f), metal);
                rim.transform.localRotation = Quaternion.Euler(0, 0, angle + 90f);
            }
            Part("SpokeX", rotor, Vector3.zero, new Vector3(0.45f, 0.025f, 0.03f), accent);
            Part("SpokeY", rotor, Vector3.zero, new Vector3(0.025f, 0.45f, 0.03f), accent);
            Part("WheelHandle", rotor, new Vector3(0.24f, 0, -0.055f), new Vector3(0.05f, 0.05f, 0.11f), metal);
            BoxCollider collider = root.gameObject.AddComponent<BoxCollider>();
            collider.size = new Vector3(0.58f, 0.58f, 0.2f);
            HandwheelInteraction wheel = root.gameObject.AddComponent<HandwheelInteraction>();
            wheel.displayName = name;
            wheel.controller = controller;
            wheel.controlType = type;
            wheel.wheelVisual = rotor;
            wheel.wheelCenter = root;
            wheel.interactionDistance = 3.5f;
        }
    }

    public static void CreateMapControls(Transform surface, ArtilleryTestController controller)
    {
        Transform existing = surface.parent.Find("MapPhysicalInstruments");
        if (existing != null)
        {
            foreach (TextMesh label in existing.GetComponentsInChildren<TextMesh>(true))
            {
                if (label.text == "TRANSFERIDOR") label.characterSize = 0.016f;
                if (label.text == "ZERAR MAPA" || label.text == "ZERAR PECA") label.characterSize = 0.007f;
            }
            PhysicalPushButton reset = existing.GetComponentsInChildren<PhysicalPushButton>(true)
                .FirstOrDefault(button => button.name == "ResetArtilleryButton");
            if (reset != null && controller != null)
            {
                reset.onPressed = new UnityEvent();
                Connect(reset.onPressed, controller.ResetAim);
            }
            return;
        }
        Transform root = Root("MapPhysicalInstruments", surface.parent, surface.localPosition + Vector3.up * 0.085f);
        root.localRotation = surface.localRotation;
        Material metal = Material("BaseMetal"), accent = Material("BaseSafety");
        Transform crank = Root("MapCrank", root, new Vector3(-0.75f, 0, -0.4f));
        Part("CrankHousing", crank, Vector3.zero, new Vector3(0.5f, 0.05f, 0.5f), metal, PrimitiveType.Cylinder);
        Transform rotor = Root("CrankRotor", crank, Vector3.up * 0.1f);
        Part("CrankArm", rotor, Vector3.right * 0.12f, new Vector3(0.3f, 0.04f, 0.05f), accent);
        Part("CrankHandle", rotor, new Vector3(0.25f, 0.06f, 0), new Vector3(0.045f, 0.075f, 0.045f), metal, PrimitiveType.Cylinder);
        BoxCollider grab = crank.gameObject.AddComponent<BoxCollider>();
        grab.center = Vector3.up * 0.09f;
        grab.size = new Vector3(0.62f, 0.25f, 0.62f);
        RotaryInstrumentInteraction rotary = crank.gameObject.AddComponent<RotaryInstrumentInteraction>();
        rotary.displayName = "Manivela do transferidor";
        rotary.wheelVisual = rotor;
        rotary.wheelCenter = rotor;
        rotary.rotationAxis = Vector3.up;
        rotary.interactionDistance = 3.5f;
        Transform dial = Dial(root, "MapProtractor", new Vector3(0, 0, -0.4f), false, out Transform needle);
        MechanicalDialIndicator indicator = dial.gameObject.AddComponent<MechanicalDialIndicator>();
        indicator.needle = dial;
        Connect(rotary.onValueChanged, indicator.SetReading);
        Print("TRANSFERIDOR", root, new Vector3(0, 0.045f, -0.85f), 0.016f);
        PhysicalPushButton resetMap = Button(root, "ResetMapButton", new Vector3(0.65f, 0, -0.55f), "ZERAR MAPA");
        Connect(resetMap.onPressed, rotary.ResetRotation);
        if (controller != null)
        {
            PhysicalPushButton resetGun = Button(root, "ResetArtilleryButton", new Vector3(1.03f, 0, -0.55f), "ZERAR PECA");
            Connect(resetGun.onPressed, controller.ResetAim);
        }
    }

    static PhysicalPushButton Button(Transform parent, string name, Vector3 position, string caption)
    {
        Transform root = Root(name, parent, position);
        Part("ButtonHousing", root, Vector3.zero, new Vector3(0.21f, 0.04f, 0.21f), Material("BaseMetal"));
        Transform cap = Part("ButtonCap", root, Vector3.up * 0.045f, new Vector3(0.14f, 0.025f, 0.14f),
            Material("BaseSafety"), PrimitiveType.Cylinder).transform;
        BoxCollider collider = root.gameObject.AddComponent<BoxCollider>();
        collider.center = Vector3.up * 0.025f;
        collider.size = new Vector3(0.23f, 0.15f, 0.23f);
        PhysicalPushButton button = root.gameObject.AddComponent<PhysicalPushButton>();
        button.displayName = caption;
        button.buttonVisual = cap;
        button.interactionDistance = 3.5f;
        Print(caption, root, new Vector3(0, 0.008f, -0.2f), 0.007f);
        return button;
    }

    static void Connect(UnityEvent action, UnityAction receiver)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying) { UnityEditor.Events.UnityEventTools.AddPersistentListener(action, receiver); return; }
#endif
        action.AddListener(receiver);
    }

    static void Connect(UnityEvent<float> action, UnityAction<float> receiver)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying) { UnityEditor.Events.UnityEventTools.AddPersistentListener(action, receiver); return; }
#endif
        action.AddListener(receiver);
    }
}
