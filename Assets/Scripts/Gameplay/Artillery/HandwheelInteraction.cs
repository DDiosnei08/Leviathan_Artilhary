using UnityEngine;

public class HandwheelInteraction : RotaryInstrumentInteraction
{
    public enum ControlType { Azimuth, Elevation }

    [Header("Artillery connection")]
    [HideInInspector] public Camera playerCamera;
    public ArtilleryTestController controller;
    public ControlType controlType;
    public float degreesPerTurn = 180f;

    public override bool CanInteract(CommandRoomInteractor interactor, RaycastHit hit)
    {
        return base.CanInteract(interactor, hit) && controller != null && Mathf.Abs(degreesPerTurn) > 0.0001f;
    }

    protected override float ResolveRotation(float degrees)
    {
        if (controller == null || Mathf.Abs(degreesPerTurn) < 0.0001f) return 0f;
        float ratio = degreesPerTurn / 360f;
        if (controlType == ControlType.Azimuth)
        {
            controller.AddAzimuth(degrees * ratio);
            return degrees;
        }
        float before = controller.Elevation;
        controller.AddElevation(degrees * ratio);
        return (controller.Elevation - before) / ratio;
    }
}
