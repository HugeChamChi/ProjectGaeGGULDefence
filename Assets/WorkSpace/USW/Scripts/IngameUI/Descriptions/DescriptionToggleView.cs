using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Shared brief/detailed switch using the standard Unity Toggle.</summary>
public static class DescriptionToggleView
{
    private static Sprite _trackSprite;
    private static Sprite _knobSprite;
    internal static Sprite TrackSprite => _trackSprite ??= RoundedSprite(92, 48);
    internal static Sprite KnobSprite => _knobSprite ??= RoundedSprite(38, 38);
    /// <summary>Creates a thumb switch; ON selects detailed descriptions.</summary>
    public static Toggle Create(Transform parent, TMP_Text source, Action<bool> changed)
    {
        var root = new GameObject("DetailedDescriptionToggle", typeof(RectTransform), typeof(Toggle));
        var rect = (RectTransform)root.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = Vector2.one;
        rect.pivot = Vector2.one;
        rect.anchoredPosition = new Vector2(-32, -40);
        rect.sizeDelta = new Vector2(340, 64);
        var background = root.AddComponent<Image>();
        background.color = Color.clear;
        var toggle = root.GetComponent<Toggle>();
        toggle.targetGraphic = background;
        toggle.transition = Selectable.Transition.None;
        toggle.onValueChanged.AddListener(value => changed?.Invoke(value));
        var visual = root.AddComponent<DescriptionSwitchVisual>();
        visual.Toggle = toggle;
        var track = new GameObject("Track", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        var trackRect = track.rectTransform;
        trackRect.SetParent(rect, false);
        trackRect.anchorMin = trackRect.anchorMax = new Vector2(0.5f, 0.5f);
        trackRect.sizeDelta = new Vector2(92, 48);
        track.sprite = TrackSprite;
        track.raycastTarget = false;
        visual.Track = track;
        var knob = new GameObject("Knob", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        knob.rectTransform.SetParent(trackRect, false);
        knob.rectTransform.anchorMin = knob.rectTransform.anchorMax = new Vector2(0, 0.5f);
        knob.rectTransform.sizeDelta = new Vector2(38, 38);
        knob.sprite = KnobSprite;
        knob.color = new Color(1, 0.96f, 0.86f);
        knob.raycastTarget = false;
        visual.Knob = knob.rectTransform;
        for (int i = 0; i < 2; i++)
        {
            bool detailed = i == 1;
            var label = new GameObject(detailed ? "DetailedLabel" : "BriefLabel", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            label.rectTransform.SetParent(rect, false);
            label.rectTransform.anchorMin = new Vector2(detailed ? 0.67f : 0f, 0f);
            label.rectTransform.anchorMax = new Vector2(detailed ? 1f : 0.33f, 1f);
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
            if (source != null) { label.font = source.font; label.fontSharedMaterial = source.fontSharedMaterial; }
            label.text = detailed ? "자세히" : "간단히";
            label.fontSize = 26f;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            visual.Labels[i] = label;
        }
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
