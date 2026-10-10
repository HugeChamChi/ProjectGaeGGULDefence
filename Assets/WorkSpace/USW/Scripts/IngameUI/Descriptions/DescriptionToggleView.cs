using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Top-right switch shared by production and test choice panels.</summary>
public static class DescriptionToggleView
{
    private static Sprite _trackSprite;
    private static Sprite _knobSprite;
    internal static Sprite TrackSprite => _trackSprite ??= RoundedSprite(92, 48);
    internal static Sprite KnobSprite => _knobSprite ??= RoundedSprite(38, 38);
    public static Toggle Create(Transform parent, TMP_Text source, Action<bool> changed)
    {
        var root = new GameObject("DetailedDescriptionToggle", typeof(RectTransform), typeof(Toggle));
        var rect = (RectTransform)root.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = Vector2.one;
        rect.pivot = Vector2.one;
        rect.anchoredPosition = new Vector2(-32, -40);
        rect.sizeDelta = new Vector2(280, 64);
        var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        var lr = (RectTransform)label.transform; lr.SetParent(rect, false);
        lr.anchorMin = Vector2.zero; lr.anchorMax = new Vector2(0.62f, 1);
        lr.offsetMin = lr.offsetMax = Vector2.zero;
        var text = label.GetComponent<TextMeshProUGUI>();
        text.font = source.font; text.fontSharedMaterial = source.fontSharedMaterial;
        text.text = "상세 설명"; text.fontSize = 26; text.color = Color.white;
        text.alignment = TextAlignmentOptions.MidlineRight; text.raycastTarget = false;
        var track = new GameObject("Track", typeof(RectTransform), typeof(Image));
        var tr = (RectTransform)track.transform; tr.SetParent(rect, false);
        tr.anchorMin = tr.anchorMax = new Vector2(1, 0.5f); tr.pivot = new Vector2(1, 0.5f);
        tr.sizeDelta = new Vector2(92, 48);
        var background = track.GetComponent<Image>();
        background.sprite = TrackSprite;
        var knob = new GameObject("Knob", typeof(RectTransform), typeof(Image));
        var kr = (RectTransform)knob.transform; kr.SetParent(tr, false);
        kr.anchorMin = kr.anchorMax = new Vector2(0, 0.5f);
        kr.sizeDelta = new Vector2(38, 38);
        var knobImage = knob.GetComponent<Image>();
        knobImage.sprite = KnobSprite;
        knobImage.color = new Color(1, 0.96f, 0.86f); knobImage.raycastTarget = false;
        var toggle = root.GetComponent<Toggle>();
        toggle.targetGraphic = background;
        toggle.onValueChanged.AddListener(value => changed(value));
        var visual = root.AddComponent<DescriptionSwitchVisual>();
        visual.Toggle = toggle; visual.Track = background; visual.Knob = kr;
        return toggle;
    }

    private static Sprite RoundedSprite(int width, int height)
    {
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.hideFlags = HideFlags.HideAndDontSave;
        texture.wrapMode = TextureWrapMode.Clamp;
        float radius = height * 0.5f;
        var pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            float center = Mathf.Clamp(x + 0.5f, radius, width - radius);
            float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(center, radius));
            pixels[y * width + x] = new Color(1, 1, 1, Mathf.Clamp01(radius - distance));
        }
        texture.SetPixels(pixels);
        texture.Apply(false, true);
        var sprite = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f));
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }
}
