using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// TotemSelectTest 씬 전용 테스트 패널 (팀원 APK 테스트용).
/// [테스트] 버튼으로 펼치고, 보스를 잡지 않아도 보상 화면을 바로 열며, 표시 방식을 켜고 끈다.
/// 토글은 다음에 보상 화면을 열 때 반영된다. 화면 요소는 Start에서 코드로 만든다 — 이 오브젝트에 Canvas가 있어야 한다.
/// </summary>
public class TotemSelectTestPanel : MonoBehaviour
{
    private static readonly Vector2 ButtonSize = new Vector2(420f, 96f);
    private const float Spacing = 14f;
    private static readonly Color PanelColor = new Color(0f, 0f, 0f, 0.75f);
    private static readonly Color ButtonColor = new Color(0.18f, 0.18f, 0.18f, 0.95f);
    private static readonly Color OnColor = new Color(0.2f, 0.55f, 0.2f, 1f);
    private static readonly Color OffColor = new Color(0.38f, 0.38f, 0.38f, 1f);

    [SerializeField] private TotemRewardUI _reward;
    [SerializeField] private TMP_FontAsset _font;
    [Tooltip("패널 왼쪽 위 모서리 위치 (기준 해상도 1080x1920, 화면 왼쪽 위 기준)")]
    [SerializeField] private Vector2 _topLeft = new Vector2(24f, -300f);

    private RectTransform _panel;
    private TextMeshProUGUI _descLabel;
    private TextMeshProUGUI _swipeLabel;
    private TextMeshProUGUI _colorLabel;
    private Image _descImage;
    private Image _swipeImage;
    private Image _colorImage;

    private void Start()
    {
        if (_reward == null) { Debug.LogError("[TotemSelectTestPanel] TotemRewardUI 미연결"); return; }

        var root = (RectTransform)transform;
        var toggle = NewButton(root, "테스트", out _, () => _panel.gameObject.SetActive(!_panel.gameObject.activeSelf));
        Place(toggle, _topLeft, new Vector2(200f, 90f));

        _panel = new GameObject("Panel", typeof(RectTransform)).GetComponent<RectTransform>();
        _panel.SetParent(root, false);
        _panel.gameObject.AddComponent<Image>().color = PanelColor;
        Place(_panel, _topLeft + new Vector2(0f, -100f), new Vector2(ButtonSize.x + 40f, 5 * (ButtonSize.y + Spacing) + 30f));

        float y = -20f;
        Place(NewButton(_panel, "보상 화면 열기", out _, OpenReward), new Vector2(20f, y), ButtonSize);
        y -= ButtonSize.y + Spacing;
        Place(NewButton(_panel, "", out _descLabel, ToggleDescription, out _descImage), new Vector2(20f, y), ButtonSize);
        y -= ButtonSize.y + Spacing;
        Place(NewButton(_panel, "", out _swipeLabel, ToggleSwipe, out _swipeImage), new Vector2(20f, y), ButtonSize);
        y -= ButtonSize.y + Spacing;
        Place(NewButton(_panel, "", out _colorLabel, ToggleColor, out _colorImage), new Vector2(20f, y), ButtonSize);
        y -= ButtonSize.y + Spacing;
        Place(NewButton(_panel, "닫기", out _, () => _panel.gameObject.SetActive(false)), new Vector2(20f, y), ButtonSize);

        Refresh();
        _panel.gameObject.SetActive(false);
    }

    private void OpenReward()
    {
        if (_reward.IsOpen) return;
        _panel.gameObject.SetActive(false);
        _reward.Show(null);
    }

    private void ToggleDescription() { _reward.ShowDescriptionOnOverview = !_reward.ShowDescriptionOnOverview; Refresh(); }
    private void ToggleSwipe() { _reward.AllowDetailSwipe = !_reward.AllowDetailSwipe; Refresh(); }
    private void ToggleColor() { _reward.UseTierColors = !_reward.UseTierColors; Refresh(); }

    private void Refresh()
    {
        SetState(_descLabel, _descImage, "구역 설명", _reward.ShowDescriptionOnOverview);
        SetState(_swipeLabel, _swipeImage, "상세 좌우 넘김", _reward.AllowDetailSwipe);
        _colorLabel.text = _reward.UseTierColors ? "띠 색: 등급 색" : "띠 색: 자리 고정";
        _colorImage.color = ButtonColor;
    }

    private static void SetState(TextMeshProUGUI label, Image image, string name, bool on)
    {
        label.text = $"{name}: {(on ? "켜짐" : "꺼짐")}";
        image.color = on ? OnColor : OffColor;
    }

    private RectTransform NewButton(Transform parent, string text, out TextMeshProUGUI label, UnityEngine.Events.UnityAction onClick) =>
        NewButton(parent, text, out label, onClick, out _);

    private RectTransform NewButton(Transform parent, string text, out TextMeshProUGUI label, UnityEngine.Events.UnityAction onClick, out Image image)
    {
        var rt = new GameObject("Button", typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        image = rt.gameObject.AddComponent<Image>();
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
        label.fontSize = 40f;
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
