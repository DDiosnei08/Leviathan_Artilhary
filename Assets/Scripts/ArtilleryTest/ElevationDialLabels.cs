
using UnityEngine;
using UnityEngine.UI;

public class ElevationDialLabels : MonoBehaviour
{
    public float radius = 52f;
    public int fontSize = 12;

    [ContextMenu("Rebuild Elevation Labels")]
    public void BuildLabels()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);

        for (int elevation = 0; elevation <= 75; elevation += 15)
        {
            float angle = -90f + elevation * 180f / 75f;

            GameObject obj = new GameObject($"Label_{elevation}");
            obj.transform.SetParent(transform, false);

            RectTransform rect = obj.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(36, 22);
            rect.anchoredPosition = new Vector2(
                Mathf.Sin(angle * Mathf.Deg2Rad) * radius,
                Mathf.Cos(angle * Mathf.Deg2Rad) * radius
            );

            Text label = obj.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.text = $"{elevation}°";
            label.fontSize = fontSize;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.raycastTarget = false;
        }
    }
}
