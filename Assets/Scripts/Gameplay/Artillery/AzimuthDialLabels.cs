
using UnityEngine;
using UnityEngine.UI;

public class AzimuthDialLabels : MonoBehaviour
{
    public float radius = 52f;
    public int fontSize = 14;

    [ContextMenu("Rebuild Labels")]
    public void BuildLabels()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);

        CreateLabel("0°", 0);
        CreateLabel("90°", 90);
        CreateLabel("180°", 180);
        CreateLabel("270°", 270);
    }

    void CreateLabel(string text, float angle)
    {
        GameObject obj = new GameObject($"Label_{text}");
        obj.transform.SetParent(transform, false);

        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(48, 24);
        rect.anchoredPosition = new Vector2(
            Mathf.Sin(angle * Mathf.Deg2Rad) * radius,
            Mathf.Cos(angle * Mathf.Deg2Rad) * radius
        );

        Text label = obj.AddComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.text = text;
        label.fontSize = fontSize;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;
        label.raycastTarget = false;
    }
}
