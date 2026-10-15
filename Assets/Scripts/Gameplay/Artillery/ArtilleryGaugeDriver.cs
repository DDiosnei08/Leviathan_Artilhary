using UnityEngine;

public class ArtilleryGaugeDriver : MonoBehaviour
{
    [Header("Controller existente")]
    public ArtilleryTestController artilleryController;

    [Header("Objetos do modelo")]
    public Transform azimuthNeedleMesh;
    public Transform elevationNeedleMesh;
    public Transform azimuthCenter;
    public Transform elevationCenter;

    [Header("Ajustes de eixo")]
    public Vector3 azimuthAxis = Vector3.up;
    public Vector3 elevationAxis = Vector3.forward;
    public bool invertAzimuth;
    public bool invertElevation = true;
    public float azimuthZeroOffset;
    public float elevationZeroOffset;

    Transform azimuthRuntimePivot;
    Transform elevationRuntimePivot;
    Quaternion azimuthStartRotation;
    Quaternion elevationStartRotation;

    void Awake()
    {
        if (azimuthNeedleMesh == null)
            azimuthNeedleMesh = FindChildRecursive(transform, "Azimuth_Needle_Mesh");

        if (elevationNeedleMesh == null)
            elevationNeedleMesh = FindChildRecursive(transform, "Elevation_Needle_Mesh");

        if (azimuthCenter == null)
            azimuthCenter = FindChildRecursive(transform, "Azimuth_Center_Cap");

        if (elevationCenter == null)
            elevationCenter = FindChildRecursive(transform, "Elevation_Center_Cap");

        azimuthRuntimePivot = CreateRuntimePivot("Azimuth_Runtime_Pivot", azimuthCenter, azimuthNeedleMesh);
        elevationRuntimePivot = CreateRuntimePivot("Elevation_Runtime_Pivot", elevationCenter, elevationNeedleMesh);

        if (azimuthRuntimePivot != null)
            azimuthStartRotation = azimuthRuntimePivot.localRotation;

        if (elevationRuntimePivot != null)
            elevationStartRotation = elevationRuntimePivot.localRotation;
    }

    void LateUpdate()
    {
        if (artilleryController == null)
            return;

        if (azimuthRuntimePivot != null)
        {
            float azimuth = artilleryController.Azimuth;
            if (invertAzimuth)
                azimuth = -azimuth;

            azimuthRuntimePivot.localRotation =
                azimuthStartRotation * Quaternion.AngleAxis(azimuth + azimuthZeroOffset, azimuthAxis.normalized);
        }

        if (elevationRuntimePivot != null)
        {
            float elevation = artilleryController.Elevation;
            if (invertElevation)
                elevation = -elevation;

            elevationRuntimePivot.localRotation =
                elevationStartRotation * Quaternion.AngleAxis(elevation + elevationZeroOffset, elevationAxis.normalized);
        }
    }

    Transform CreateRuntimePivot(string pivotName, Transform center, Transform needle)
    {
        if (center == null || needle == null)
            return null;

        GameObject pivotObject = new GameObject(pivotName);
        Transform pivot = pivotObject.transform;

        pivot.SetParent(center.parent, false);
        pivot.position = center.position;
        pivot.rotation = center.rotation;
        pivot.localScale = Vector3.one;

        needle.SetParent(pivot, true);

        return pivot;
    }

    static Transform FindChildRecursive(Transform parent, string childName)
    {
        foreach (Transform child in parent)
        {
            if (child.name == childName)
                return child;

            Transform found = FindChildRecursive(child, childName);
            if (found != null)
                return found;
        }

        return null;
    }
}
