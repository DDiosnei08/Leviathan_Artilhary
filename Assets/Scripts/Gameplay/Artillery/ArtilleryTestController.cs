using UnityEngine;

public class ArtilleryTestController : MonoBehaviour
{
    [Header("Pivots")]
    public Transform azimuthPivot;
    public Transform elevationPivot;
    [Header("Limits")]
    public float minElevation = 0f;
    public float maxElevation = 60f;

    public float Azimuth { get; private set; }
    public float Elevation { get; private set; } = 15f;

    void Start() => UpdateVisuals();

    void UpdateVisuals()
    {
        if (azimuthPivot != null) azimuthPivot.localRotation = Quaternion.Euler(0f, Azimuth, 0f);
        if (elevationPivot != null) elevationPivot.localRotation = Quaternion.Euler(-Elevation, 0f, 0f);
    }

    public void AddElevation(float amount)
    {
        Elevation = Mathf.Clamp(Elevation + amount, minElevation, maxElevation);
        UpdateVisuals();
    }

    public void AddAzimuth(float amount)
    {
        Azimuth = Mathf.Repeat(Azimuth + amount, 360f);
        UpdateVisuals();
    }

    public void ResetAim()
    {
        Azimuth = 0f;
        Elevation = 15f;
        UpdateVisuals();
    }
}
