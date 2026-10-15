using UnityEngine;

public class ArtilleryCommandLever : CommandRoomClickable
{
    public enum LeverMode
    {
        Azimuth,
        Elevation,
        AmmoType
    }

    [Header("Artilharia")]
    public ArtilleryTestController artilleryController;
    public LeverMode mode;

    [Header("Controle")]
    public float sensitivity = 0.12f;
    public float minLeverAngle = -35f;
    public float maxLeverAngle = 35f;

    [Header("Munição")]
    public string[] ammoTypes = { "HE", "AP", "SMOKE" };

    [Header("Visual")]
    public Transform leverPivot;
    public Vector3 leverAxis = Vector3.right;

    float leverAngle;
    int ammoIndex;
    Quaternion restRotation;

    void Awake()
    {
        if (leverPivot == null)
            leverPivot = transform;
        restRotation = leverPivot.localRotation;
    }

    public override void BeginInteract(CommandRoomInteractor interactor, RaycastHit hit)
    {
        if (mode == LeverMode.AmmoType)
        {
            if (ammoTypes == null || ammoTypes.Length == 0) return;
            ammoIndex = (ammoIndex + 1) % ammoTypes.Length;
            leverAngle = Mathf.Lerp(minLeverAngle, maxLeverAngle, ammoIndex / Mathf.Max(1f, ammoTypes.Length - 1f));
            ApplyVisual();
            Debug.Log("Munição selecionada: " + ammoTypes[ammoIndex]);
        }
    }

    public override void HoldInteract(CommandRoomInteractor interactor, RaycastHit hit, float holdTime)
    {
        if (mode == LeverMode.AmmoType || artilleryController == null)
            return;

        Vector2 mouseDelta = interactor.PointerDelta;
        float input = mode == LeverMode.Azimuth ? mouseDelta.x : mouseDelta.y;
        float amount = input * sensitivity * (interactor.FineAdjustment ? 0.2f : 1f);

        if (mode == LeverMode.Azimuth)
            artilleryController.AddAzimuth(amount);
        else
            artilleryController.AddElevation(amount);

        leverAngle = Mathf.Clamp(leverAngle + input * 0.25f, minLeverAngle, maxLeverAngle);
        ApplyVisual();
    }

    public override void EndInteract(CommandRoomInteractor interactor)
    {
        if (mode == LeverMode.AmmoType)
            return;

        leverAngle = 0f;
        ApplyVisual();
    }

    void ApplyVisual()
    {
        if (leverPivot == null)
            return;

        leverPivot.localRotation = restRotation * Quaternion.AngleAxis(leverAngle, leverAxis.normalized);
    }
}
