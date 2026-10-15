using UnityEngine;
using UnityEngine.Events;

public class RotaryInstrumentInteraction : CommandRoomClickable
{
    public enum DragMode { Circular, Horizontal, Vertical }

    [Header("Physical instrument")]
    public Transform wheelVisual;
    public Transform wheelCenter;
    public Vector3 rotationAxis = Vector3.forward;
    public DragMode dragMode;
    public bool invertDirection;

    [Header("Drag")]
    public float deadZonePixels = 12f;
    public float maxAngleStep = 30f;
    public float degreesPerPixel = 0.8f;

    [Header("Mechanical stops")]
    public bool limitRotation;
    public float minimumRotation = -90f;
    public float maximumRotation = 90f;

    [Header("Connections")]
    public UnityEvent<float> onRotation = new UnityEvent<float>();
    public UnityEvent<float> onValueChanged = new UnityEvent<float>();

    public float Rotation { get; private set; }
    Quaternion restRotation;
    bool initialized;
    bool hasPreviousAngle;
    float previousAngle;

    public override bool CanInteract(CommandRoomInteractor interactor, RaycastHit hit)
    {
        return base.CanInteract(interactor, hit) && wheelVisual != null && rotationAxis.sqrMagnitude > 0.0001f;
    }

    public override void BeginInteract(CommandRoomInteractor interactor, RaycastHit hit)
    {
        Initialize();
        hasPreviousAngle = TryGetAngle(interactor, out previousAngle);
    }

    public override void HoldInteract(CommandRoomInteractor interactor, RaycastHit hit, float holdTime)
    {
        float delta;
        if (dragMode == DragMode.Circular)
        {
            if (!TryGetAngle(interactor, out float angle))
            {
                hasPreviousAngle = false;
                return;
            }
            delta = hasPreviousAngle ? Mathf.DeltaAngle(previousAngle, angle) : 0f;
            previousAngle = angle;
            hasPreviousAngle = true;
        }
        else
        {
            delta = (dragMode == DragMode.Horizontal ? interactor.PointerDelta.x : interactor.PointerDelta.y) * degreesPerPixel;
        }
        float fine = interactor.FineAdjustment ? 0.2f : 1f;
        RotateBy(Mathf.Clamp(delta, -maxAngleStep, maxAngleStep) * fine * (invertDirection ? -1f : 1f));
    }

    public override void EndInteract(CommandRoomInteractor interactor)
    {
        hasPreviousAngle = false;
    }

    public void RotateBy(float degrees)
    {
        Initialize();
        if (wheelVisual == null || !float.IsFinite(degrees) || Mathf.Abs(degrees) < 0.0001f) return;
        float desired = limitRotation ? Mathf.Clamp(Rotation + degrees, minimumRotation, maximumRotation) - Rotation : degrees;
        float accepted = ResolveRotation(desired);
        Rotation = limitRotation ? Mathf.Clamp(Rotation + accepted, minimumRotation, maximumRotation) : Mathf.Repeat(Rotation + accepted, 360f);
        wheelVisual.localRotation = restRotation * Quaternion.AngleAxis(Rotation, rotationAxis.normalized);
        if (Mathf.Abs(accepted) < 0.0001f) return;
        onRotation.Invoke(accepted);
        onValueChanged.Invoke(Rotation);
    }

    protected virtual float ResolveRotation(float degrees) => degrees;

    public void ResetRotation()
    {
        Initialize();
        Rotation = limitRotation ? Mathf.Clamp(0f, minimumRotation, maximumRotation) : 0f;
        if (wheelVisual != null) wheelVisual.localRotation = restRotation * Quaternion.AngleAxis(Rotation, rotationAxis.normalized);
        onValueChanged.Invoke(Rotation);
    }

    void Initialize()
    {
        if (initialized || wheelVisual == null) return;
        restRotation = wheelVisual.localRotation;
        initialized = true;
    }

    bool TryGetAngle(CommandRoomInteractor interactor, out float angle)
    {
        angle = 0f;
        if (interactor.viewCamera == null || wheelVisual == null) return false;
        Vector3 center = (wheelCenter != null ? wheelCenter : wheelVisual).position;
        Vector3 screenCenter = interactor.viewCamera.WorldToScreenPoint(center);
        if (screenCenter.z <= 0f || Vector2.Distance(interactor.PointerPosition, new Vector2(screenCenter.x, screenCenter.y)) < deadZonePixels) return false;
        Vector3 axis = wheelVisual.TransformDirection(rotationAxis.normalized);
        Plane plane = new Plane(axis, center);
        Ray ray = interactor.viewCamera.ScreenPointToRay(interactor.PointerPosition);
        if (!plane.Raycast(ray, out float distance)) return false;
        Vector3 radial = ray.GetPoint(distance) - center;
        Vector3 reference = Mathf.Abs(Vector3.Dot(axis, Vector3.up)) < 0.95f ? Vector3.up : Vector3.forward;
        Vector3 x = Vector3.Cross(reference, axis).normalized;
        Vector3 y = Vector3.Cross(axis, x);
        angle = Mathf.Atan2(Vector3.Dot(radial, y), Vector3.Dot(radial, x)) * Mathf.Rad2Deg;
        return radial.sqrMagnitude > 0.000001f;
    }
}
