using UnityEngine;
using System.Collections.Generic;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class LeviathanPlayerController : MonoBehaviour
{
    [Header("Movimento")]
    public float walkSpeed = 4f;
    public float sprintSpeed = 7f;
    public float jumpHeight = 1.2f;
    public float gravity = -18f;

    [Header("Camera")]
    public Transform cameraPivot;
    public float mouseSensitivity = 0.12f;
    public float minLookAngle = -75f;
    public float maxLookAngle = 80f;

    CharacterController characterController;
    Vector3 verticalVelocity;
    float cameraPitch;
    bool controlsLocked;

    readonly HashSet<object> controlLocks = new HashSet<object>();
    public bool ControlsLocked => controlsLocked || controlLocks.Count > 0;
    public bool HasExternalControlLocks => controlLocks.Count > 0;

    public void AcquireControlLock(object owner) { if (owner != null) controlLocks.Add(owner); }
    public void ReleaseControlLock(object owner) { if (owner != null) controlLocks.Remove(owner); }

    void Awake()
    {
        characterController = GetComponent<CharacterController>();

        if (cameraPivot == null && Camera.main != null)
            cameraPivot = Camera.main.transform;
    }

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
    
    void Update()
    {
        if (ControlsLocked)
        {
            HandleMovement(null);
            return;
        }

        var keyboard = Keyboard.current;
        var mouse = Mouse.current;

        HandleLook(mouse);
        HandleMovement(keyboard);
    }

    public void SetControlsLocked(bool locked)
    {
        controlsLocked = locked;

        if (!locked)
            verticalVelocity = Vector3.zero;
    }

    void HandleLook(Mouse mouse)
    {
        if (mouse == null || cameraPivot == null)
            return;
        if (Cursor.lockState != CursorLockMode.Locked)
            return;

        Vector2 mouseDelta = mouse.delta.ReadValue() * mouseSensitivity;

        transform.Rotate(Vector3.up, mouseDelta.x);

        cameraPitch -= mouseDelta.y;
        cameraPitch = Mathf.Clamp(cameraPitch, minLookAngle, maxLookAngle);
        cameraPivot.localRotation = Quaternion.Euler(cameraPitch, 0f, 0f);
    }

    void HandleMovement(Keyboard keyboard)
    {
        Vector2 input = Vector2.zero;

        if (keyboard != null)
        {
            if (keyboard.wKey.isPressed) input.y += 1f;
            if (keyboard.sKey.isPressed) input.y -= 1f;
            if (keyboard.dKey.isPressed) input.x += 1f;
            if (keyboard.aKey.isPressed) input.x -= 1f;
        }

        input = Vector2.ClampMagnitude(input, 1f);

        float speed = keyboard != null && keyboard.leftShiftKey.isPressed ? sprintSpeed : walkSpeed;
        Vector3 move =
            transform.right * input.x +
            transform.forward * input.y;

        characterController.Move(move * speed * Time.deltaTime);

        if (characterController.isGrounded && verticalVelocity.y < 0f)
            verticalVelocity.y = -2f;

        if (characterController.isGrounded && keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
            verticalVelocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);

        verticalVelocity.y += gravity * Time.deltaTime;
        characterController.Move(verticalVelocity * Time.deltaTime);
    }
}
