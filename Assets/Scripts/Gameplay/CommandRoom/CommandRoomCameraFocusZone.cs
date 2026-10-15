using UnityEngine;

public class CommandRoomCameraFocusZone : MonoBehaviour
{
    [Header("Player")]
    public Transform playerCamera;
    public LeviathanPlayerController playerController;

    [Header("Posicao melhor para observar")]
    public Transform focusPoint;
    public float blendSpeed = 8f;
    public bool lockPlayerMovementWhileInside;

    Vector3 originalLocalPosition;
    Quaternion originalLocalRotation;
    bool playerInside;

    void Start()
    {
        if (playerCamera != null)
        {
            originalLocalPosition = playerCamera.localPosition;
            originalLocalRotation = playerCamera.localRotation;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (playerController == null)
            playerController = other.GetComponentInParent<LeviathanPlayerController>();

        if (playerController == null)
            return;

        if (playerCamera == null && Camera.main != null)
            playerCamera = Camera.main.transform;

        playerInside = true;

        if (lockPlayerMovementWhileInside)
            playerController.SetControlsLocked(true);
    }

    void OnTriggerExit(Collider other)
    {
        if (playerController == null || other.GetComponentInParent<LeviathanPlayerController>() != playerController)
            return;

        playerInside = false;

        if (lockPlayerMovementWhileInside)
            playerController.SetControlsLocked(false);
    }

    void LateUpdate()
    {
        if (playerCamera == null)
            return;
        if (playerController != null && playerController.HasExternalControlLocks)
            return;

        if (playerInside && focusPoint != null)
        {
            playerCamera.position = Vector3.Lerp(playerCamera.position, focusPoint.position, Time.deltaTime * blendSpeed);
            playerCamera.rotation = Quaternion.Slerp(playerCamera.rotation, focusPoint.rotation, Time.deltaTime * blendSpeed);
        }
        else
        {
            playerCamera.localPosition = Vector3.Lerp(playerCamera.localPosition, originalLocalPosition, Time.deltaTime * blendSpeed);
            playerCamera.localRotation = Quaternion.Slerp(playerCamera.localRotation, originalLocalRotation, Time.deltaTime * blendSpeed);
        }
    }
}
