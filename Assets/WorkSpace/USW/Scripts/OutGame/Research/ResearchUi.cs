using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>연구 화면 공용 UI 생성 도우미 (코드로 화면을 만들 때 쓴다).</summary>
public static class ResearchUi
{
    /// <summary>빈 RectTransform.</summary>
    public static RectTransform NewRect(Transform parent, string name)
    {
        var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        rect.SetParent(parent, false);
        return rect;
    }

    /// <summary>부모를 꽉 채우고 가장자리에서 offset만큼 들인다 (left, bottom, right, top).</summary>
    public static RectTransform Stretch(RectTransform rect, float left = 0f, float bottom = 0f, float right = 0f, float top = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
        return rect;
    }

    /// <summary>가운데 정렬 이미지. raycast는 꺼져 있다.</summary>
    public static Image NewImage(Transform parent, string name, Sprite sprite, Vector2 size, Vector2 position)
    {
        var rect = NewRect(parent, name);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        var image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        return image;
    }

    /// <summary>부모를 꽉 채우는 글자. raycast는 꺼져 있다.</summary>
    public static TextMeshProUGUI NewText(Transform parent, string name, string text, float fontSize, TMP_FontAsset font, TextAlignmentOptions alignment)
    {
        var rect = Stretch(NewRect(parent, name));
        var tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) tmp.font = font;
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = alignment;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.raycastTarget = false;
        return tmp;
    }

    /// <summary>글자가 들어간 버튼 (9-slice 바탕).</summary>
    public static Button NewButton(Transform parent, string name, Sprite sprite, Color color, string label, float fontSize, TMP_FontAsset font, out Image background, out TextMeshProUGUI text)
    {
        var rect = NewRect(parent, name);
        background = rect.gameObject.AddComponent<Image>();
        background.sprite = sprite;
        background.type = Image.Type.Sliced;
        background.color = color;
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = background;
        text = NewText(rect, "Label", label, fontSize, font, TextAlignmentOptions.Center);
        return button;
    }

    /// <summary>앵커를 한 점에 두고 위치·크기를 준다.</summary>
    public static RectTransform Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }
}
