using ProjectLeviathan.ProceduralTerrain;
using UnityEngine;
using UnityEngine.InputSystem;

public class IntegratedBattlefieldRig : MonoBehaviour
{
    public enum ViewMode { Overview, Player, Operator }

    [Header("World and artillery")]
    public TerrainWorldController terrain;
    public ArtilleryTestController artillery;
    public Transform operatorStation;
    public float stationDistance = 4f;

    [Header("Player and cameras")]
    public LeviathanPlayerController player;
    public CommandRoomInteractor interactor;
    public Camera overviewCamera;
    public Camera playerCamera;
    public Camera operatorCamera;
    public HandwheelInteraction[] handwheels;

    [Header("Physical interaction")]
    public CommandRoomInteractor operatorInteractor;
    public ViewMode initialView = ViewMode.Overview;

    public ViewMode CurrentView { get; private set; }
    bool cursorReleased;

    void Start()
    {
        SetView(initialView);
    }

    void Update()
    {
        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;
        if (keyboard != null)
        {
            if (keyboard.tabKey.wasPressedThisFrame)
                SetView(CurrentView == ViewMode.Overview ? ViewMode.Player : ViewMode.Overview);

            if (keyboard.eKey.wasPressedThisFrame)
            {
                if (CurrentView == ViewMode.Operator)
                    SetView(ViewMode.Player);
                else if (CurrentView == ViewMode.Player && player != null && operatorStation != null &&
                         Vector3.Distance(player.transform.position, operatorStation.position) <= stationDistance)
                    SetView(ViewMode.Operator);
            }

            if (keyboard.escapeKey.wasPressedThisFrame && CurrentView == ViewMode.Player)
            {
                cursorReleased = !cursorReleased;
                ApplyPlayerInput();
            }

        }

        if (cursorReleased && mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            cursorReleased = false;
            ApplyPlayerInput();
        }

    }

    public void SetView(ViewMode mode)
    {
        if (interactor != null) interactor.CancelInteraction();
        if (operatorInteractor != null) operatorInteractor.CancelInteraction();
        CurrentView = mode;
        cursorReleased = false;
        SelectCamera(overviewCamera, mode == ViewMode.Overview);
        SelectCamera(playerCamera, mode == ViewMode.Player);
        SelectCamera(operatorCamera, mode == ViewMode.Operator);
        if (handwheels != null)
            foreach (HandwheelInteraction wheel in handwheels)
                if (wheel != null) wheel.enabled = mode != ViewMode.Overview;

        if (terrain != null)
        {
            Camera active = mode == ViewMode.Overview ? overviewCamera :
                mode == ViewMode.Player ? playerCamera : operatorCamera;
            if (active != null) terrain.viewer = active.transform;
        }
        ApplyPlayerInput();
    }

    void ApplyPlayerInput()
    {
        bool walking = CurrentView == ViewMode.Player && !cursorReleased;
        if (player != null) player.enabled = walking;
        if (interactor != null) interactor.enabled = walking;
        if (operatorInteractor != null) operatorInteractor.enabled = CurrentView == ViewMode.Operator;
        Cursor.lockState = walking ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !walking;
    }

    static void SelectCamera(Camera camera, bool selected)
    {
        if (camera == null) return;
        camera.enabled = selected;
        camera.tag = selected ? "MainCamera" : "Untagged";
        AudioListener listener = camera.GetComponent<AudioListener>();
        if (listener != null) listener.enabled = selected;
    }

    void OnDisable()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}
