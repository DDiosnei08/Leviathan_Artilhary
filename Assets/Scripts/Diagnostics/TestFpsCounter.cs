using System.Globalization;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectLeviathan.Diagnostics
{
    [DisallowMultipleComponent]
    public sealed class TestFpsCounter : MonoBehaviour
    {
        public bool visible = true;
        [Tooltip("Display only in the Editor or a Development Build.")]
        public bool developmentOnly = true;
        [Range(0.1f, 2f)] public float sampleInterval = 0.5f;
        public float FramesPerSecond { get; private set; }
        public float FrameMilliseconds { get; private set; }
        public int SampleCount { get; private set; }

        float elapsed;
        int frames;
        readonly GUIContent label = new GUIContent("FPS: --   |   -- ms");
        GUIStyle style;

        void OnEnable()
        {
            if (developmentOnly && !Application.isEditor && !Debug.isDebugBuild)
            { enabled = false; return; }
            elapsed = 0; frames = 0; SampleCount = 0;
            FramesPerSecond = FrameMilliseconds = 0;
            label.text = "FPS: --   |   -- ms";
        }
        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f8Key.wasPressedThisFrame) visible = !visible;
            float delta = Time.unscaledDeltaTime;
            if (delta <= 0) return;
            elapsed += delta; frames++;
            if (elapsed < Mathf.Max(0.1f, sampleInterval)) return;
            FramesPerSecond = frames / elapsed;
            FrameMilliseconds = elapsed * 1000f / frames;
            SampleCount++;
            // Cache the text twice a second, not on every IMGUI event or rendered frame.
            label.text = string.Format(CultureInfo.InvariantCulture, "FPS: {0:F0}   |   {1:F2} ms", FramesPerSecond, FrameMilliseconds);
            elapsed = 0; frames = 0;
        }
        void OnGUI()
        {
            if (!visible || !Application.isPlaying || Event.current.type != EventType.Repaint) return;
            if (style == null)
            {
                style = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.MiddleLeft,
                    padding = new RectOffset(8, 8, 0, 0) };
                style.normal.textColor = Color.white;
            }
            Rect bounds = new Rect(10, 10, Mathf.Min(230, Screen.width - 20), 30);
            Color previous = GUI.color;
            GUI.color = new Color(0, 0, 0, 0.65f); GUI.DrawTexture(bounds, Texture2D.whiteTexture);
            GUI.color = Color.white; GUI.Label(bounds, label, style);
            GUI.color = previous;
        }
    }
}
