using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// FxLab_HackBloom 전용 — design/hp,디버프예씨.png 스타일의 디버프 줄 대역(debuff_background 35px 타일 + 실제 디버프 아이콘).
/// 실험실 보스에는 실제 디버프가 없으므로 예시 디버프를 고정으로 보여준다. 디버프 줄이 HP바 바로 아래 자리를 먼저 가진다(10-07 결정).
/// </summary>
public sealed class HackDebuffRowMock
{
    // design/hp,디버프예씨.png 측정값 (1080 폭 캔버스): 칸 35, 칸 간격 포함 53, HP바 왼쪽 아래 기준 시작 위치
    private const float TileSize = 35f;
    private const float Pitch = 53f;
    private static readonly Vector2 RowOffset = new Vector2(18f, -11f);
    private readonly RectTransform _root;

    /// <summary>예시 디버프 칸들을 만든다. counts[i]가 비어 있으면 숫자를 띄우지 않는다.</summary>
    public HackDebuffRowMock(RectTransform parent, TMP_FontAsset font, Material fontMaterial, Sprite tile, Sprite[] icons, string[] counts)
    {
        _root = new GameObject("DebuffRow_Mock", typeof(RectTransform)).GetComponent<RectTransform>();
        _root.SetParent(parent, false);
        _root.anchorMin = _root.anchorMax = new Vector2(.5f, .5f);
        _root.pivot = new Vector2(0f, 1f);
        _root.sizeDelta = new Vector2(Pitch * icons.Length, TileSize);
        _root.SetAsFirstSibling();
        for (int i = 0; i < icons.Length; i++)
        {
            var slot = Image("Debuff" + i, _root, new Vector2(i * Pitch, 0f), Vector2.one * TileSize, tile, new Vector2(0f, 1f));
            Image("Icon", slot.rectTransform, Vector2.zero, Vector2.one * 23f, icons[i], new Vector2(.5f, .5f)).preserveAspect = true;
            if (counts == null || i >= counts.Length || string.IsNullOrEmpty(counts[i])) continue;
            var t = new GameObject("Count", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            var rect = t.rectTransform;
            rect.SetParent(slot.rectTransform, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(8f, -7f); rect.sizeDelta = new Vector2(40f, 22f);
            if (font != null) t.font = font;
            if (fontMaterial != null) t.fontSharedMaterial = fontMaterial;
            t.fontSize = 19f; t.fontStyle = FontStyles.Bold | FontStyles.Italic;
            t.alignment = TextAlignmentOptions.BottomRight; t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.text = counts[i];
        }
    }

    /// <summary>HP바 바로 아래 디버프 줄 자리에 놓는다 (해킹 UI가 이 자리를 차지하지 않는다).</summary>
    public void Tick(Rect bar) => _root.anchoredPosition = new Vector2(bar.xMin + RowOffset.x, bar.yMin + RowOffset.y);

    private static Image Image(string name, RectTransform parent, Vector2 position, Vector2 size, Sprite sprite, Vector2 anchor)
    {
        var img = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        var rect = img.rectTransform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
        rect.anchoredPosition = position; rect.sizeDelta = size;
        img.sprite = sprite; img.raycastTarget = false;
        return img;
    }
}
