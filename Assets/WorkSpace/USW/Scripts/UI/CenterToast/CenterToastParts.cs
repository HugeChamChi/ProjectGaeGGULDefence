using UnityEngine;
using UnityEngine.UI;

/// <summary>중앙 알림 방식들이 부품 모양을 잡을 때 쓰는 도우미.</summary>
public static class CenterToastParts
{
    /// <summary>모서리 반지름 radius인 둥근 사각형.</summary>
    public static void Rounded(Image img, CenterToastSprites sprites, Color color, Vector2 size, float radius)
    {
        img.enabled = true;
        img.sprite = sprites.RoundedRect;
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = sprites.RoundedBorder / Mathf.Max(0.5f, Mathf.Min(radius, size.y * 0.5f));
        img.color = color;
        img.rectTransform.sizeDelta = size;
    }

    /// <summary>각진 단색 사각형.</summary>
    public static void Plain(Image img, Color color, Vector2 size)
    {
        img.enabled = true;
        img.sprite = null;
        img.type = Image.Type.Simple;
        img.color = color;
        img.rectTransform.sizeDelta = size;
    }

    /// <summary>스프라이트 그대로 (원·빛 등).</summary>
    public static void Simple(Image img, Sprite sprite, Color color, Vector2 size)
    {
        img.enabled = true;
        img.sprite = sprite;
        img.type = Image.Type.Simple;
        img.color = color;
        img.rectTransform.sizeDelta = size;
    }

    /// <summary>글자 크기를 정하고 문구 폭을 다시 잰다.</summary>
    public static void Measure(CenterToastEntry e, float fontSize)
    {
        e.Text.fontSize = fontSize;
        e.TextWidth = e.Text.GetPreferredValues(e.Message).x;
    }

    public static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);
}
