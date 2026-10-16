using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>실제 로비 외형으로 하단 탭 모션을 비교하는 독립 실험실. 게임 데이터는 변경하지 않는다.</summary>
public sealed class LobbyBottomMotionLab : MonoBehaviour
{
    [SerializeField] private BottomPanelController.PanelButton[] _tabs;
    [SerializeField] private RectTransform _plate;
    [SerializeField] private Image _marker;
    [SerializeField] private TMP_Text _status;
    [SerializeField] private Button[] _variantButtons;
    [SerializeField] private Button _demoButton;
    [SerializeField] private Button _pageButton;
    private readonly Color _normal = new Color(0.23f, 0.20f, 0.30f);
    private readonly Color _selected = new Color(0.46f, 0.31f, 0.82f);
    private Sequence _motion;
    private int _current = 2;
    private int _variant = 4;
    private int _pending = -1;
    private bool _pages;
    private bool _demo;
    private float _nextDemo;
    private int _demoStep;
    private Canvas _appFade;
    private bool _fadeWasEnabled;
    private LobbyTabFlowGraphic[] _flowSurfaces;
    private Color[] _shadowColors;
    private static readonly int[] DemoOrder = { 1, 0, 3, 4, 2 };
    private static readonly string[] Names = { "현재 방식", "A · 또렷하게", "B · 탄력 있게", "C · 흐르는 선택", "D · 탄력 + 흐름" };
    private static readonly string[] Details = {
        "0.24초 · 높이 +48 / 아이콘 +20 / 1.16배",
        "0.22초 · 짧은 상승 → 가볍게 안착",
        "0.34초 · 살짝 압축 → 튀어오름 → 안착",
        "0.28초 · 선택 배경 이동 + 아이콘 교대",
        "배경은 매끄럽게 · 어두운 면 밀기 · 아이콘만 탄력" };

    /// <summary>현재 비교안.</summary>
    public int Variant => _variant;
    /// <summary>선택된 실제 로비 탭 번호.</summary>
    public int CurrentIndex => _current;
    /// <summary>애니메이션 진행 여부.</summary>
    public bool IsAnimating => _motion != null && _motion.IsActive();

    /// <summary>비교 버튼을 기존 실험실 씬에서 갱신한다.</summary>
    public void ConfigureVariantButtons(Button[] buttons) => _variantButtons = buttons;

    /// <summary>빌더에서 로비 참조와 실험실 컨트롤을 연결한다.</summary>
    public void Configure(BottomPanelController.PanelButton[] tabs, RectTransform plate, Image marker,
        TMP_Text status, Button[] variants, Button demo, Button pages)
    {
        _tabs = tabs; _plate = plate; _marker = marker; _status = status;
        _variantButtons = variants; _demoButton = demo; _pageButton = pages;
    }

    private void Awake()
    {
        // These decorative children must not become extra slots in the lobby layout group.
        foreach (var rect in new[] { _plate, _marker.rectTransform })
        {
            var layout = rect.GetComponent<LayoutElement>();
            if (layout == null) layout = rect.gameObject.AddComponent<LayoutElement>();
            layout.ignoreLayout = true;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
        }
        Canvas.ForceUpdateCanvases();
        _flowSurfaces = new LobbyTabFlowGraphic[_tabs.Length];
        _shadowColors = new Color[_tabs.Length];
        int tabIndex = 0;
        foreach (var tab in _tabs)
        {
            tab.Rect = (RectTransform)tab._panel.transform;
            tab.Origin = tab.Rect.anchoredPosition;
            tab.Group = tab._panel.GetComponent<CanvasGroup>();
            if (tab.Group == null) tab.Group = tab._panel.AddComponent<CanvasGroup>();
            tab.IconOrigin = tab._buttonImage.rectTransform.anchoredPosition;
            tab.IconScale = tab._buttonImage.rectTransform.localScale;
            _shadowColors[tabIndex] = tab._innerShadow != null ? tab._innerShadow.color : Color.white;
            _flowSurfaces[tabIndex++] = CreateFlowSurface(tab);
        }
        for (int i = 0; i < _tabs.Length; i++)
        {
            int index = i;
            _tabs[i].Click = () => Select(index);
            _tabs[i]._button.onClick.AddListener(_tabs[i].Click);
        }
        for (int i = 0; i < _variantButtons.Length; i++)
        {
            int variant = i;
            _variantButtons[i].onClick.AddListener(() => SetVariant(variant));
        }
        _demoButton.onClick.AddListener(ToggleDemo);
        _pageButton.onClick.AddListener(TogglePages);
        SetVariant(_variant);
    }

    private static LobbyTabFlowGraphic CreateFlowSurface(BottomPanelController.PanelButton tab)
    {
        var maskObject = new GameObject("FlowSurfaceMask", typeof(RectTransform), typeof(Image), typeof(Mask), typeof(LayoutElement));
        var maskRect = (RectTransform)maskObject.transform;
        maskRect.SetParent(tab._mainRect, false);
        maskRect.anchorMin = Vector2.zero; maskRect.anchorMax = Vector2.one;
        maskRect.offsetMin = maskRect.offsetMax = Vector2.zero;
        maskRect.SetSiblingIndex(tab._buttonImage.transform.GetSiblingIndex());
        maskObject.GetComponent<LayoutElement>().ignoreLayout = true;
        var maskImage = maskObject.GetComponent<Image>();
        maskImage.sprite = tab._background.sprite; maskImage.type = Image.Type.Sliced; maskImage.raycastTarget = false;
        maskObject.GetComponent<Mask>().showMaskGraphic = false;
        var surfaceObject = new GameObject("FlowSurface", typeof(RectTransform), typeof(CanvasRenderer), typeof(LobbyTabFlowGraphic));
        var surfaceRect = (RectTransform)surfaceObject.transform;
        surfaceRect.SetParent(maskRect, false);
        surfaceRect.anchorMin = Vector2.zero; surfaceRect.anchorMax = Vector2.one;
        surfaceRect.offsetMin = surfaceRect.offsetMax = Vector2.zero;
        var surface = surfaceObject.GetComponent<LobbyTabFlowGraphic>(); surface.raycastTarget = false;
        return surface;
    }

    private void Start()
    {
        // VContainer's automatic app root still creates its fade prefab in standalone labs.
        // Suppress only that overlay for this lab and restore its state when leaving.
        var fade = FindFirstObjectByType<FadeScreen>();
        if (fade != null)
        {
            _appFade = fade.GetComponentInParent<Canvas>();
            if (_appFade != null) { _fadeWasEnabled = _appFade.enabled; _appFade.enabled = false; }
        }
        Canvas.ForceUpdateCanvases();
        Settle();
    }

    /// <summary>비교안을 바꾸고 동일한 기준 상태로 돌아간다.</summary>
    public void SetVariant(int variant)
    {
        if (variant < 0 || variant >= Names.Length) return;
        StopMotion(); _variant = variant; _pending = -1;
        Settle(); RefreshStatus();
    }

    /// <summary>탭을 선택한다. 개선안은 연타 시 현재 위치에서 마지막 입력으로 이어진다.</summary>
    public void Select(int index)
    {
        if (!isActiveAndEnabled || index < 0 || index >= _tabs.Length || index == _current) return;
        if (_variant == 0 && IsAnimating) { _pending = index; return; }
        StopMotion();
        int previous = _current;
        _current = index;
        _motion = DOTween.Sequence().SetUpdate(true);
        float direction = index > previous ? 1f : -1f;
        for (int i = 0; i < _tabs.Length; i++) AnimateTab(i, i == index, direction);
        if (_variant >= 3)
        {
            _plate.gameObject.SetActive(true);
            _motion.Insert(0f, _plate.DOAnchorPos(PlatePosition(index), 0.28f).SetEase(Ease.OutCubic));
        }
        _marker.gameObject.SetActive(_variant != 0);
        _motion.Insert(0f, _marker.rectTransform.DOAnchorPos(MarkerPosition(index), _variant == 4 ? 0.28f : 0.22f).SetEase(Ease.OutCubic));
        if (_pages)
        {
            var outgoing = _tabs[previous]; var incoming = _tabs[index];
            float width = ((RectTransform)incoming.Rect.parent).rect.width;
            foreach (var tab in _tabs)
            {
                if (tab != outgoing && tab != incoming) tab._panel.SetActive(false);
                tab.Group.blocksRaycasts = tab.Group.interactable = false;
            }
            incoming.Rect.anchoredPosition = incoming.Origin + Vector2.right * direction * width;
            incoming._panel.SetActive(true);
            _motion.Insert(0f, outgoing.Rect.DOAnchorPos(outgoing.Origin - Vector2.right * direction * width, 0.32f).SetEase(Ease.OutCubic));
            _motion.Insert(0f, incoming.Rect.DOAnchorPos(incoming.Origin, 0.32f).SetEase(Ease.OutCubic));
        }
        _motion.OnComplete(() =>
        {
            _motion = null;
            SetPages();
            int pending = _pending; _pending = -1;
            if (pending >= 0) Select(pending);
        });
        RefreshStatus();
    }

    private void AnimateTab(int index, bool selected, float direction)
    {
        var t = _tabs[index]; var icon = t._buttonImage.rectTransform;
        float duration = _variant == 0 ? (selected ? 0.24f : 0.168f) : 0.20f;
        float height = selected ? (_variant == 0 ? 248f : _variant >= 3 ? 200f : 232f) : 200f;
        float rise = selected ? (_variant == 0 ? 20f : _variant == 2 || _variant == 4 ? 24f : 14f) : 0f;
        float scale = selected ? (_variant == 0 ? 1.16f : 1.10f) : 1f;
        _motion.Insert(0f, t._mainRect.DOSizeDelta(new Vector2(t._mainRect.sizeDelta.x, height), duration).SetEase(Ease.OutCubic));
        var background = selected ? _selected : _normal;
        if (_variant >= 3) background.a = 0f;
        _motion.Insert(0f, t._background.DOColor(background, duration));
        _motion.Insert(0f, t._selectionFill.DOFillAmount(selected && _variant < 3 ? 1f : 0f, duration).SetEase(Ease.OutCubic));
        if (_variant == 4)
        {
            var flow = _flowSurfaces[index];
            if (flow.Reveal < 0.001f || flow.Reveal > 0.999f) flow.Direction = selected ? direction : -direction;
            _motion.Insert(0f, DOTween.To(() => flow.Reveal, value => flow.Reveal = value, selected ? 1f : 0f, 0.28f).SetEase(Ease.OutCubic));
        }
        _motion.Insert(0f, t._label.DOFade(selected ? 1f : 0.65f, duration));
        var sprite = selected ? t._activeSprite : t._inactiveSprite;
        if (sprite != null) t._buttonImage.sprite = sprite;
        _motion.Insert(0f, t._buttonImage.DOColor(selected ? Color.white : new Color(0.75f, 0.77f, 0.85f), duration));
        Vector2 target = t.IconOrigin + Vector2.up * rise;
        Vector3 targetScale = t.IconScale * scale;
        if (selected && (_variant == 2 || _variant == 4))
        {
            _motion.Insert(0f, icon.DOScale(Vector3.Scale(t.IconScale, new Vector3(1.06f, 0.94f, 1f)), 0.055f));
            _motion.Insert(0f, icon.DOAnchorPos(t.IconOrigin + Vector2.down * 4f, 0.055f));
            _motion.Insert(0.055f, icon.DOAnchorPos(target + Vector2.up * 8f, 0.12f).SetEase(Ease.OutCubic));
            _motion.Insert(0.055f, icon.DOScale(t.IconScale * 1.15f, 0.12f).SetEase(Ease.OutCubic));
            _motion.Insert(0.175f, icon.DOAnchorPos(target, 0.165f).SetEase(Ease.OutSine));
            _motion.Insert(0.175f, icon.DOScale(targetScale, 0.165f).SetEase(Ease.OutSine));
        }
        else
        {
            _motion.Insert(0f, icon.DOAnchorPos(target, _variant == 0 ? duration : 0.22f).SetEase(selected ? Ease.OutBack : Ease.OutCubic, _variant == 0 ? 1.7f : 0.65f));
            _motion.Insert(0f, icon.DOScale(targetScale, _variant == 0 ? duration : 0.22f).SetEase(selected ? Ease.OutBack : Ease.OutCubic, _variant == 0 ? 1.7f : 0.65f));
        }
    }

    private Vector2 PlatePosition(int index)
    {
        var parent = (RectTransform)_plate.parent;
        var tab = _tabs[index]._mainRect;
        var bottom = (Vector2)parent.InverseTransformPoint(tab.TransformPoint(new Vector3(tab.rect.center.x, tab.rect.yMin)));
        return bottom - new Vector2(parent.rect.center.x, parent.rect.yMin);
    }
    private Vector2 MarkerPosition(int index) => PlatePosition(index) + Vector2.up * 8f;

    private void Settle()
    {
        for (int i = 0; i < _tabs.Length; i++)
        {
            var t = _tabs[i]; bool selected = i == _current;
            t._mainRect.sizeDelta = new Vector2(t._mainRect.sizeDelta.x, selected && _variant < 3 ? (_variant == 0 ? 248f : 232f) : 200f);
            var color = selected ? _selected : _normal;
            if (_variant >= 3) color.a = 0f;
            t._background.color = color;
            t._selectionFill.fillAmount = selected && _variant < 3 ? 1f : 0f;
            _flowSurfaces[i].transform.parent.gameObject.SetActive(_variant == 4);
            _flowSurfaces[i].Reveal = selected ? 1f : 0f;
            if (t._innerShadow != null)
            {
                var shadow = _shadowColors[i];
                if (_variant == 4) shadow.a = 0f;
                t._innerShadow.color = shadow;
            }
            t._label.alpha = selected ? 1f : 0.65f;
            var sprite = selected ? t._activeSprite : t._inactiveSprite;
            if (sprite != null) t._buttonImage.sprite = sprite;
            t._buttonImage.color = selected ? Color.white : new Color(0.75f, 0.77f, 0.85f);
            t._buttonImage.rectTransform.anchoredPosition = t.IconOrigin + Vector2.up * (selected ? (_variant == 0 ? 20f : _variant == 2 || _variant == 4 ? 24f : 14f) : 0f);
            t._buttonImage.rectTransform.localScale = t.IconScale * (selected ? (_variant == 0 ? 1.16f : 1.10f) : 1f);
        }
        _plate.gameObject.SetActive(_variant >= 3);
        _plate.sizeDelta = new Vector2(_tabs[_current]._mainRect.rect.width, 228f);
        _plate.anchoredPosition = PlatePosition(_current);
        _marker.gameObject.SetActive(_variant != 0);
        _marker.rectTransform.anchoredPosition = MarkerPosition(_current);
        SetPages();
    }

    private void SetPages()
    {
        for (int i = 0; i < _tabs.Length; i++)
        {
            var t = _tabs[i]; bool active = i == (_pages ? _current : 2);
            t.Rect.anchoredPosition = t.Origin;
            t.Group.alpha = 1f;
            t.Group.blocksRaycasts = t.Group.interactable = false;
            t._panel.SetActive(active);
        }
    }

    private void TogglePages() { StopMotion(); _pages = !_pages; _pending = -1; Settle(); RefreshStatus(); }
    private void ToggleDemo() { _demo = !_demo; _nextDemo = Time.unscaledTime + 0.3f; RefreshStatus(); }
    private void Update()
    {
        if (!_demo || Time.unscaledTime < _nextDemo) return;
        _nextDemo = Time.unscaledTime + 1.1f;
        Select(DemoOrder[_demoStep++ % DemoOrder.Length]);
    }

    private void RefreshStatus()
    {
        _status.text = "하단 UI 모션 실험실\n" + Names[_variant] + "  |  " + Details[_variant] + "\n아래 탭을 직접 눌러 비교하세요";
        for (int i = 0; i < _variantButtons.Length; i++)
            _variantButtons[i].image.color = i == _variant ? _selected : _normal;
        _demoButton.GetComponentInChildren<TMP_Text>().text = _demo ? "자동 비교 · 켜짐" : "자동 비교 · 꺼짐";
        _pageButton.GetComponentInChildren<TMP_Text>().text = _pages ? "페이지 이동 · 켜짐" : "페이지 이동 · 꺼짐";
    }

    private void StopMotion() { _motion?.Kill(); _motion = null; }
    private void OnDisable() { StopMotion(); if (_tabs != null && _tabs.Length > 0 && _tabs[0].Rect != null) Settle(); }
    private void OnDestroy()
    {
        if (_appFade != null) _appFade.enabled = _fadeWasEnabled;
        if (_tabs == null) return;
        foreach (var t in _tabs)
            if (t._button != null && t.Click != null) t._button.onClick.RemoveListener(t.Click);
    }
}
