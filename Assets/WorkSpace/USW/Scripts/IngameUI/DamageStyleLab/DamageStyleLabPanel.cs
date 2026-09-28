using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// TotemSelectTest 씬 전용 데미지 표시 테스트 패널 (팀원 APK 비교용).
/// [데미지 테스트] 버튼으로 펼치고, 방식(모비노기형/쿠키런형)과 폰트를 바로 바꾸며, 샘플 피해를 재생한다.
/// 화면 요소는 Start에서 코드로 만든다 — 이 오브젝트에 Canvas가 있어야 한다.
/// </summary>
public class DamageStyleLabPanel : MonoBehaviour
{
    private static readonly Vector2 ButtonSize = new Vector2(420f, 96f);
    private const float Spacing = 14f;
    private const int ButtonCount = 4;
    private const float LabelFontSize = 40f;
    private static readonly Color PanelColor = new Color(0f, 0f, 0f, 0.75f);
    private static readonly Color ButtonColor = new Color(0.18f, 0.18f, 0.18f, 0.95f);

    [SerializeField] private DamageStyleLab _lab;
    [Tooltip("버튼 글자 폰트 (한글 필요)")]
    [SerializeField] private TMP_FontAsset _font;
    [Tooltip("패널 왼쪽 위 모서리 위치 (기준 해상도 1080x1920, 화면 왼쪽 위 기준)")]
    [SerializeField] private Vector2 _topLeft = new Vector2(24f, -300f);

    private RectTransform _panel;
    private TextMeshProUGUI _styleLabel;
    private TextMeshProUGUI _fontLabel;

    private void Start()
    {
        if (_lab == null) { Debug.LogError("[DamageStyleLabPanel] DamageStyleLab 미연결", this); return; }

        var root = (RectTransform)transform;
        Place(NewButton(root, "데미지 테스트", out _, () => _panel.gameObject.SetActive(!_panel.gameObject.activeSelf)),
            _topLeft, new Vector2(260f, 90f));

        _panel = new GameObject("Panel", typeof(RectTransform)).GetComponent<RectTransform>();
        _panel.SetParent(root, false);
        _panel.gameObject.AddComponent<Image>().color = PanelColor;
        Place(_panel, _topLeft + new Vector2(0f, -100f), new Vector2(ButtonSize.x + 40f, ButtonCount * (ButtonSize.y + Spacing) + 30f));

        float y = -20f;
        Place(NewButton(_panel, "", out _styleLabel, () => { _lab.NextStyle(); Refresh(); }), new Vector2(20f, y), ButtonSize);
        y -= ButtonSize.y + Spacing;
        Place(NewButton(_panel, "", out _fontLabel, () => { _lab.NextFont(); Refresh(); }), new Vector2(20f, y), ButtonSize);
        y -= ButtonSize.y + Spacing;
        Place(NewButton(_panel, "샘플 피해 재생", out _, _lab.PlaySample), new Vector2(20f, y), ButtonSize);
        y -= ButtonSize.y + Spacing;
        Place(NewButton(_panel, "닫기", out _, () => _panel.gameObject.SetActive(false)), new Vector2(20f, y), ButtonSize);

        Refresh();
        _panel.gameObject.SetActive(false);
    }

    private void Refresh()
    {
        _styleLabel.text = $"방식: {_lab.StyleName}";
        _fontLabel.text = $"폰트: {_lab.FontName}";
    }

    private RectTransform NewButton(Transform parent, string text, out TextMeshProUGUI label, UnityEngine.Events.UnityAction onClick)
    {
        var rt = new GameObject("Button", typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        var image = rt.gameObject.AddComponent<Image>();
        image.color = ButtonColor;
        var button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(onClick);

        var labelRt = new GameObject("Label", typeof(RectTransform)).GetComponent<RectTransform>();
        labelRt.SetParent(rt, false);
        labelRt.anchorMin = Vector2.zero; labelRt.anchorMax = Vector2.one;
        labelRt.offsetMin = labelRt.offsetMax = Vector2.zero;
        label = labelRt.gameObject.AddComponent<TextMeshProUGUI>();
        if (_font != null) label.font = _font;
        label.text = text;
        label.fontSize = LabelFontSize;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        return rt;
    }

    /// <summary>부모의 왼쪽 위 기준으로 배치.</summary>
    private static void Place(RectTransform rt, Vector2 topLeft, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = topLeft;
        rt.sizeDelta = size;
    }
}
