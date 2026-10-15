using UnityEngine;
using UnityEngine.Events;

public class PhysicalPushButton : CommandRoomClickable
{
    public Transform buttonVisual;
    public Vector3 pressAxis = Vector3.down;
    [Min(0f)] public float pressTravel = 0.025f;
    [Min(0f)] public float returnSpeed = 0.15f;
    public UnityEvent onPressed = new UnityEvent();
    public UnityEvent onReleased = new UnityEvent();
    public bool IsPressed { get; private set; }

    Vector3 restPosition;
    bool initialized;

    public override void BeginInteract(CommandRoomInteractor interactor, RaycastHit hit)
    {
        if (IsPressed) return;
        Initialize();
        IsPressed = true;
        if (buttonVisual != null) buttonVisual.localPosition = restPosition + pressAxis.normalized * pressTravel;
        onPressed.Invoke();
    }

    public override void EndInteract(CommandRoomInteractor interactor)
    {
        if (!IsPressed) return;
        IsPressed = false;
        onReleased.Invoke();
    }

    void Update()
    {
        if (!initialized || IsPressed || buttonVisual == null) return;
        buttonVisual.localPosition = Vector3.MoveTowards(buttonVisual.localPosition, restPosition, returnSpeed * Time.deltaTime);
    }

    void OnDisable()
    {
        EndInteract(null);
        if (initialized && buttonVisual != null) buttonVisual.localPosition = restPosition;
    }

    void Initialize()
    {
        if (initialized) return;
        if (buttonVisual == null) buttonVisual = transform;
        restPosition = buttonVisual.localPosition;
        initialized = true;
    }
}
