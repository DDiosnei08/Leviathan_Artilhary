using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(-50)]
public class CommandRoomInteractor : MonoBehaviour
{
    public Camera viewCamera;
    public LeviathanPlayerController playerController;
    public float interactDistance = 3f;
    public LayerMask interactMask = ~0;
    public bool useScreenCenter = true;
    public bool lockPlayerWhileInteracting = true;

    public CommandRoomClickable ActiveControl => activeClickable;
    public Vector2 PointerPosition { get; private set; }
    public Vector2 PointerDelta { get; private set; }
    public bool FineAdjustment { get; private set; }

    CommandRoomClickable activeClickable;
    RaycastHit capturedHit;
    Vector3 localGrabPoint;
    float holdTime;
    bool captured;
    LeviathanPlayerController lockedPlayer;

    void Awake()
    {
        if (viewCamera == null) viewCamera = Camera.main;
        if (playerController == null) playerController = GetComponentInParent<LeviathanPlayerController>();
    }

    void Update()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null || viewCamera == null || !viewCamera.isActiveAndEnabled)
        {
            CancelInteraction();
            return;
        }
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            CancelInteraction();
            return;
        }
        if (mouse.leftButton.wasPressedThisFrame && !captured)
        {
            Vector2 point = useScreenCenter
                ? new Vector2(viewCamera.pixelRect.center.x, viewCamera.pixelRect.center.y)
                : mouse.position.ReadValue();
            TryBeginInteraction(point);
            return;
        }
        if (!captured) return;
        if (!mouse.leftButton.isPressed)
        {
            CancelInteraction();
            return;
        }
        bool fine = Keyboard.current != null && (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed);
        Vector2 delta = Cursor.lockState == CursorLockMode.Locked
            ? mouse.delta.ReadValue() : mouse.position.ReadValue() - PointerPosition;
        DragInteraction(delta, Time.deltaTime, fine);
    }

    public bool TryBeginInteraction(Vector2 screenPoint)
    {
        if (captured || !isActiveAndEnabled || viewCamera == null || !viewCamera.isActiveAndEnabled) return false;
        Ray ray = viewCamera.ScreenPointToRay(screenPoint);
        if (!Physics.Raycast(ray, out RaycastHit hit, interactDistance, interactMask, QueryTriggerInteraction.Ignore)) return false;
        CommandRoomClickable control = hit.collider.GetComponentInParent<CommandRoomClickable>();
        if (control == null || hit.distance > control.interactionDistance || !control.CanInteract(this, hit)) return false;
        activeClickable = control;
        capturedHit = hit;
        localGrabPoint = hit.collider.transform.InverseTransformPoint(hit.point);
        PointerPosition = screenPoint;
        PointerDelta = Vector2.zero;
        FineAdjustment = false;
        holdTime = 0f;
        captured = true;
        if (lockPlayerWhileInteracting && playerController != null)
        {
            lockedPlayer = playerController;
            lockedPlayer.AcquireControlLock(this);
        }
        try { control.BeginInteract(this, hit); }
        catch { CancelInteraction(); throw; }
        return true;
    }

    public void DragInteraction(Vector2 delta, float deltaTime, bool fineAdjustment = false)
    {
        if (!captured) return;
        if (activeClickable == null || !activeClickable.isActiveAndEnabled || viewCamera == null ||
            !viewCamera.isActiveAndEnabled || capturedHit.collider == null)
        {
            CancelInteraction();
            return;
        }
        Vector3 point = capturedHit.collider.transform.TransformPoint(localGrabPoint);
        Vector3 direction = point - viewCamera.transform.position;
        float distance = direction.magnitude;
        if (distance > Mathf.Min(interactDistance, activeClickable.interactionDistance) + 0.05f ||
            !Physics.Raycast(viewCamera.transform.position, direction.normalized, out RaycastHit obstruction,
                distance + 0.05f, interactMask, QueryTriggerInteraction.Ignore) ||
            obstruction.collider.GetComponentInParent<CommandRoomClickable>() != activeClickable)
        {
            CancelInteraction();
            return;
        }
        PointerDelta = delta;
        PointerPosition += delta;
        FineAdjustment = fineAdjustment;
        holdTime += Mathf.Max(0f, deltaTime);
        if (activeClickable.canHold) activeClickable.HoldInteract(this, capturedHit, holdTime);
    }

    public void CancelInteraction()
    {
        CommandRoomClickable previous = activeClickable;
        activeClickable = null;
        captured = false;
        PointerDelta = Vector2.zero;
        holdTime = 0f;
        try { if (previous != null) previous.EndInteract(this); }
        finally
        {
            if (lockedPlayer != null) lockedPlayer.ReleaseControlLock(this);
            lockedPlayer = null;
        }
    }

    void OnDisable() => CancelInteraction();
    void OnApplicationFocus(bool focus) { if (!focus) CancelInteraction(); }
}
