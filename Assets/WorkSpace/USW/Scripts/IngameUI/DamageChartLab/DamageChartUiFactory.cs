using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>실험실에서 공유하는 UGUI 생성 도구. 레이아웃 그룹 없이 앵커를 사용한다.</summary>
public sealed class DamageChartUiFactory
{
    private readonly TMP_FontAsset _font;
    /// <summary>프로젝트의 한글 폰트를 사용한다.</summary>
    public DamageChartUiFactory(TMP_FontAsset font) => _font = font;

    /// <summary>부모 안의 상대 영역을 생성한다.</summary>
    public RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false); rect.anchorMin = min; rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero; return rect;
    }
    /// <summary>레이캐스트를 막지 않는 단색 영역.</summary>
    public Image Image(string name, Transform parent, Vector2 min, Vector2 max, Color color)
    {
        var rect = Rect(name, parent, min, max); var image = rect.gameObject.AddComponent<Image>();
        image.color = color; image.raycastTarget = false; return image;
    }
    /// <summary>자동 축소가 가능한 한글 레이블.</summary>
    public TMP_Text Text(string name, Transform parent, string value, Vector2 min, Vector2 max,
        float size, Color color, TextAlignmentOptions alignment = TextAlignmentOptions.Left)
    {
        var text = Rect(name, parent, min, max).gameObject.AddComponent<TextMeshProUGUI>();
        text.font = _font; text.text = value; text.fontSize = size; text.color = color;
        text.alignment = alignment; text.raycastTarget = false; text.enableAutoSizing = true;
        text.fontSizeMin = size * .78f; text.fontSizeMax = size; text.overflowMode = TextOverflowModes.Ellipsis;
        return text;
    }
    /// <summary>터치로 누르는 실험실 비교 버튼.</summary>
    public Button Button(string name, Transform parent, string label, Vector2 min, Vector2 max, UnityAction action)
    {
        var rect = Rect(name, parent, min, max); var image = rect.gameObject.AddComponent<Image>();
        image.color = new Color(.12f,.21f,.26f,1f);
        var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
        button.transition = Selectable.Transition.None;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        button.onClick.AddListener(action);
        Text("Label", rect, label, new Vector2(.03f,.02f), new Vector2(.97f,.98f), 18f,
            new Color(.92f,.96f,.94f), TextAlignmentOptions.Center);
        return button;
    }
}
