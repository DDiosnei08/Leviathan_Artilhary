
using UnityEngine;
using UnityEngine.UI;

public class AzimuthDialMarks : MonoBehaviour
{
    public int divisions = 36;
    public float radius = 72f;
    public float tickLength = 10f;
    public float majorTickLength = 16f;
    public float tickWidth = 3f;

    void Start()
    {
        BuildMarks();
    }

    [ContextMenu("Rebuild Marks")]
    public void BuildMarks()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);

        for (int i = 0; i < divisions; i++)
        {
            float angle = i * 360f / divisions;
            bool major = i % 3 == 0;
            float length = major ? majorTickLength : tickLength;

            GameObject tick = new GameObject($"Tick_{angle:000}");
            tick.transform.SetParent(transform, false);

            RectTransform rect = tick.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(tickWidth, length);
            rect.anchoredPosition = new Vector2(
                Mathf.Sin(angle * Mathf.Deg2Rad) * radius,
                Mathf.Cos(angle * Mathf.Deg2Rad) * radius
            );
            rect.localRotation = Quaternion.Euler(0, 0, -angle);

            Image image = tick.AddComponent<Image>();
            image.color = major ? Color.white : new Color(0.65f, 0.7f, 0.68f);
            image.raycastTarget = false;
        }
    }
}
