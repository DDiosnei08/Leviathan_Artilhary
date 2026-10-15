using UnityEngine;

public class MechanicalDialIndicator : MonoBehaviour
{
    public Transform needle;
    public Vector3 localAxis = Vector3.up;
    Quaternion restRotation;
    bool initialized;

    public void SetReading(float angle)
    {
        if (needle == null) return;
        if (!initialized) { restRotation = needle.localRotation; initialized = true; }
        needle.localRotation = restRotation * Quaternion.AngleAxis(angle, localAxis.normalized);
    }
}
