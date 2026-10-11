
using UnityEngine;
using UnityEngine.InputSystem;

public class HandwheelInteraction : MonoBehaviour
{
    public enum ControlType { Azimuth, Elevation }

    [Header("Referências")]
    public Camera playerCamera;
    public ArtilleryTestController controller;
    public Transform wheelVisual;
    public Transform wheelCenter;

    [Header("Controle")]
    public ControlType controlType;
    public float interactionDistance = 2f;
    public float degreesPerTurn = 180f;
    public float deadZonePixels = 25f;
    public float maxAngleStep = 15f;

    bool interacting;
    float previousAngle;

    void Update()
    {
        var mouse = Mouse.current;

        if (mouse == null || playerCamera == null ||
            controller == null || wheelVisual == null ||
            wheelCenter == null)
            return;

        if (mouse.leftButton.wasPressedThisFrame)
            TryStartInteraction(mouse);

        if (!mouse.leftButton.isPressed)
            interacting = false;

        if (interacting)
            RotateWheel(mouse);
    }

    void TryStartInteraction(Mouse mouse)
    {
        Ray ray = playerCamera.ScreenPointToRay(mouse.position.ReadValue());

        if (!Physics.Raycast(ray, out RaycastHit hit, interactionDistance))
            return;

        if (hit.transform != transform &&
            !hit.transform.IsChildOf(transform))
            return;

        Vector2 offset = GetMouseOffset(mouse);

        if (offset.magnitude < deadZonePixels)
            return;

        previousAngle = GetMouseAngle(offset);
        interacting = true;
    }

    void RotateWheel(Mouse mouse)
    {
        Vector2 offset = GetMouseOffset(mouse);

        if (offset.magnitude < deadZonePixels)
            return;

        float angle = GetMouseAngle(offset);
        float delta = Mathf.DeltaAngle(previousAngle, angle);
        previousAngle = angle;

        delta = Mathf.Clamp(delta, -maxAngleStep, maxAngleStep);

        if (Mathf.Abs(delta) < 0.01f)
            return;

        wheelVisual.Rotate(0f, 0f, delta, Space.Self);

        float change = delta * degreesPerTurn / 360f;

        if (controlType == ControlType.Azimuth)
            controller.AddAzimuth(change);
        else
            controller.AddElevation(change);
    }

    Vector2 GetMouseOffset(Mouse mouse)
    {
        Vector3 center = playerCamera.WorldToScreenPoint(wheelCenter.position);
        return mouse.position.ReadValue() - new Vector2(center.x, center.y);
    }

    float GetMouseAngle(Vector2 offset)
    {
        return Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg;
    }
}
