
using UnityEngine;
using UnityEngine.UI;

public class ElevationDialMarks : MonoBehaviour
{
    public int divisions = 15;
    public float radius = 72f;
    public float tickWidth = 3f;
    public float minorLength = 8f;
    public float majorLength = 14f;

    [ContextMenu("Rebuild Elevation Marks")]
    public void BuildMarks()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);

        for (int i = 0; i <= divisions; i++)
        {
            float elevation = i * 75f / divisions;
            float angle = -90f + elevation * 180f / 75f;
            bool major = i % 3 == 0;
            float length = major ? majorLength : minorLength;

            GameObject tick = new GameObject($"Tick_{elevation:00}");
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
