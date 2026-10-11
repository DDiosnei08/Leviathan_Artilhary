
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class ArtilleryTestController : MonoBehaviour
{
    [Header("Pivots")]
    public Transform azimuthPivot;
    public Transform elevationPivot;

    [Header("Instrumentos")]
    public RectTransform azimuthNeedle;
    public RectTransform elevationNeedle;
    public Text azimuthText;
    public Text elevationText;

    [Header("Limites")]
    public float azimuthSpeed = 35f;
    public float elevationSpeed = 20f;
    public float minElevation = 0f;
    public float maxElevation = 60f;

    public float Azimuth { get; private set; }
    public float Elevation { get; private set; } = 15f;

    void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        float speed = keyboard.leftShiftKey.isPressed ||
                      keyboard.rightShiftKey.isPressed ? 5f : 1f;

        float azimuthInput = 0f;
        float elevationInput = 0f;

        if (keyboard.aKey.isPressed) azimuthInput--;
        if (keyboard.dKey.isPressed) azimuthInput++;
        if (keyboard.leftArrowKey.isPressed) azimuthInput--;
        if (keyboard.rightArrowKey.isPressed) azimuthInput++;

        if (keyboard.sKey.isPressed) elevationInput--;
        if (keyboard.wKey.isPressed) elevationInput++;
        if (keyboard.downArrowKey.isPressed) elevationInput--;
        if (keyboard.upArrowKey.isPressed) elevationInput++;

        Azimuth = Mathf.Repeat(
            Azimuth + azimuthInput * azimuthSpeed * speed * Time.deltaTime,
            360f
        );

        Elevation = Mathf.Clamp(
            Elevation + elevationInput * elevationSpeed * speed * Time.deltaTime,
            minElevation,
            maxElevation
        );

        if (keyboard.rKey.wasPressedThisFrame)
        {
            Azimuth = 0f;
            Elevation = 15f;
        }

        UpdateVisuals();
    }

    void UpdateVisuals()
    {
        if (azimuthPivot != null)
            azimuthPivot.localRotation = Quaternion.Euler(0f, Azimuth, 0f);

        if (elevationPivot != null)
            elevationPivot.localRotation = Quaternion.Euler(-Elevation, 0f, 0f);

        if (azimuthText != null)
            azimuthText.text = $"AZIMUTE: {Azimuth:000}°";

        if (elevationText != null)
            elevationText.text = $"ELEVAÇÃO: {Elevation:00.0}°";
        if (azimuthNeedle != null)
            azimuthNeedle.localRotation = Quaternion.Euler(0f, 0f, -Azimuth);
        if (elevationNeedle != null)
        {
            float dialAngle = 90f - (Elevation / 75f) * 180f;
            elevationNeedle.localRotation = Quaternion.Euler(0f, 0f, dialAngle);
        } 
    }

    void Start()
    {
        UpdateVisuals();
    }
}
