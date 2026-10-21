using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>네 전투 버튼만 좌우로 전환한다. 식량, 판매 영역, 설정 메뉴는 원래 계층에 유지한다.</summary>
public sealed class IngameHudDamagePager : MonoBehaviour
{
    private IngameDamageChart _chart;
    private DamageChartLabSettings _settings;
    private RectTransform _buttons;
    private Vector2 _buttonPosition;
    private CanvasGroup _buttonGroup;
    private bool _previousInteractable, _previousBlocksRaycasts;
    private Tween _slide;
    private float _progress, _dragStart;
    private int _pointerId = int.MinValue;
    private TMP_Text _hint;
    private Color _hintColor;
    private CanvasGroup _graphGroup;
    // The options button overlaps the graph's top-right corner, so it is hidden on the graph page.
    private CanvasGroup _menuButtonGroup;
    private Tween _pop;
    private float _popScale = 1f;
    // +1: graph waits on the right of the buttons, -1: on the left. Set by the swipe direction.
    private float _side = 1f;

    /// <summary>튜토리얼에서 실제로 밀어야 하는 하단 영역.</summary>
    public RectTransform SwipeArea { get; private set; }
    /// <summary>같은 피해 집계 뷰를 표시하는 페이지.</summary>
    public RectTransform GraphPage { get; private set; }
    /// <summary>선택한 페이지가 딜 그래프인지.</summary>
    public bool IsChartVisible { get; private set; }
    /// <summary>슬라이드가 끝났는지.</summary>
    public bool IsSettled => _pointerId == int.MinValue && (_slide == null || !_slide.IsActive());
    /// <summary>전환 중 그래프를 갱신해야 하는지.</summary>
    public bool IsGraphExposed => _progress > 0f;

    /// <summary>공통 옵션 프리팹의 씬 내 형제 HUD에 연결한다. 씬 에셋은 변경하지 않는다.</summary>
    public bool Initialize(IngameDamageChart chart, IngameHudMenu menu, DamageChartLabSettings settings,
        TMP_FontAsset font, DamageChartStyle style)
    {
        if (menu == null || settings == null) return false;
        // The shared prefab cannot serialize a reference to its scene-owned sibling.
        Transform options = menu.transform;
        while (options != null && options.name != "IngameOptionsMenu") options = options.parent;
        var bar = options != null && options.parent != null
            ? options.parent.Find("MainUI_InteractableBar") as RectTransform : null;
        _buttons = bar != null ? bar.Find("Buttons") as RectTransform : null;
        if (_buttons == null) return false;
        _chart = chart; _settings = settings;
        var ui = new DamageChartUiFactory(font);
        SwipeArea = ui.Rect("CombatHudPager", bar, Vector2.zero, new Vector2(1f, 0f));
        SwipeArea.sizeDelta = new Vector2(0f, settings.HudHeight);
        SwipeArea.pivot = new Vector2(.5f, 0f);
        SwipeArea.anchoredPosition = Vector2.zero;
        var surface = SwipeArea.gameObject.AddComponent<Image>();
        surface.color = Color.clear;
        SwipeArea.gameObject.AddComponent<RectMask2D>();
        var input = SwipeArea.gameObject.AddComponent<HudDamageSwipeArea>();
        input.Pager = this; input.vertical = false; input.horizontal = false;
        _buttons.SetParent(SwipeArea, false);
        foreach (var button in _buttons.GetComponentsInChildren<Button>(true))
        {
            // UGUI cancels a button press when its drag target is a different object.
            // Keep the drag handler on that button until a deliberate swipe is accepted.
            var gesture = button.GetComponent<HudDamageButtonGesture>();
            if (gesture == null) gesture = button.gameObject.AddComponent<HudDamageButtonGesture>();
            gesture.Pager = this;
        }
        _buttonPosition = _buttons.anchoredPosition;
        _buttonGroup = _buttons.GetComponent<CanvasGroup>();
        if (_buttonGroup == null) _buttonGroup = _buttons.gameObject.AddComponent<CanvasGroup>();
        _previousInteractable = _buttonGroup.interactable;
        _previousBlocksRaycasts = _buttonGroup.blocksRaycasts;
        GraphPage = ui.Rect("HudDamageGraph", SwipeArea, Vector2.zero, Vector2.one);
        _hint = ui.Text("DamageSwipeHint", SwipeArea, settings.HudOpenHint, Vector2.zero, new Vector2(1f, .055f),
            18f, style.MutedColor, TextAlignmentOptions.Center);
        _hintColor = _hint.color;
        _graphGroup = GraphPage.gameObject.AddComponent<CanvasGroup>();
        if (menu.MenuButton != null)
        {
            _menuButtonGroup = menu.MenuButton.GetComponent<CanvasGroup>();
            if (_menuButtonGroup == null) _menuButtonGroup = menu.MenuButton.gameObject.AddComponent<CanvasGroup>();
        }
        SetProgress(0f);
        return true;
    }

    /// <summary>메뉴·튜토리얼에서도 같은 페이지 전환을 사용한다.</summary>
    public void SetChartVisible(bool visible, bool animate = true)
    {
        _slide?.Kill(); _pointerId = int.MinValue;
        bool changed = IsChartVisible != visible;
        IsChartVisible = visible;
        _buttonGroup.interactable = !visible && _previousInteractable;
        _buttonGroup.blocksRaycasts = !visible && _previousBlocksRaycasts;
        float target = visible ? 1f : 0f;
        if (!animate) { _pop?.Kill(); _popScale = 1f; SetProgress(target); return; }
        _slide = DOTween.To(() => _progress, SetProgress, target, _settings.HudSlideSeconds)
            .SetEase(Ease.OutCubic).SetUpdate(true);
        if (changed) _slide.OnComplete(PlaySettlePop);
    }

    /// <summary>버튼의 짧은 흔들림과 세로 이동을 허용하고 명확한 가로 이동만 수락한다.</summary>
    public bool TryBeginButtonSwipe(PointerEventData eventData)
    {
        Vector2 delta = eventData.position - eventData.pressPosition;
        float horizontalDistance = Mathf.Abs(delta.x);
        if (horizontalDistance < ScreenWidth() * _settings.HudButtonSwipeStartFraction
            || horizontalDistance < Mathf.Abs(delta.y) * _settings.HudButtonSwipeDirectionRatio) return false;
        return BeginSwipe(eventData);
    }

    /// <summary>중단된 가로 제스처의 소유권을 반납하고 원래 페이지로 돌아간다.</summary>
    public void CancelSwipe(int pointerId)
    {
        if (_pointerId == pointerId) SetChartVisible(IsChartVisible);
    }

    /// <summary>한 손가락만 가로 전환을 시작한다.</summary>
    public bool BeginSwipe(PointerEventData eventData)
    {
        if (_pointerId != int.MinValue) return false;
        _slide?.Kill(); _pointerId = eventData.pointerId; _dragStart = _progress;
        return true;
    }

    /// <summary>손가락에 따라 페이지 위치를 바꾼다.</summary>
    public void DragSwipe(PointerEventData eventData)
    {
        if (_pointerId != eventData.pointerId) return;
        float delta = (eventData.position.x - eventData.pressPosition.x) / ScreenWidth();
        bool fromChart = _dragStart >= .5f;
        // Either direction leaves the current page; the other page enters from the opposite side.
        if (delta != 0f) _side = fromChart ? Mathf.Sign(delta) : -Mathf.Sign(delta);
        SetProgress(Mathf.Clamp01(_dragStart + (fromChart ? -1f : 1f) * Mathf.Abs(delta)));
    }

    /// <summary>짧거나 반대 방향인 드래그는 원래 페이지로 돌아간다.</summary>
    public void EndSwipe(PointerEventData eventData)
    {
        if (_pointerId != eventData.pointerId) return;
        float distance = eventData.position.x - eventData.pressPosition.x;
        bool open = IsChartVisible;
        if (Mathf.Abs(distance) >= ScreenWidth() * _settings.HudSwipeFraction) open = !IsChartVisible;
        _pointerId = int.MinValue;
        if (open) _chart.Open(); else _chart.Close();
    }

    private float ScreenWidth()
    {
        var corners = new Vector3[4]; SwipeArea.GetWorldCorners(corners);
        var canvas = SwipeArea.GetComponentInParent<Canvas>();
        var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        return Mathf.Max(1f, Mathf.Abs(RectTransformUtility.WorldToScreenPoint(camera, corners[2]).x -
            RectTransformUtility.WorldToScreenPoint(camera, corners[0]).x));
    }

    private void SetProgress(float progress)
    {
        _progress = Mathf.Clamp01(progress);
        float width = SwipeArea.rect.width;
        _buttons.anchoredPosition = _buttonPosition + Vector2.left * (_side * width * _progress);
        GraphPage.anchoredPosition = Vector2.right * (_side * width * (1f - _progress));
        // The leaving page shrinks and fades; the settled page gets a short pop.
        float minScale = _settings.HudPageMinScale, minAlpha = _settings.HudPageMinAlpha;
        _buttons.localScale = Vector3.one * (Mathf.Lerp(1f, minScale, _progress) * (IsChartVisible ? 1f : _popScale));
        GraphPage.localScale = Vector3.one * (Mathf.Lerp(minScale, 1f, _progress) * (IsChartVisible ? _popScale : 1f));
        _buttonGroup.alpha = Mathf.Lerp(1f, minAlpha, _progress);
        _graphGroup.alpha = Mathf.Lerp(minAlpha, 1f, _progress);
        if (_menuButtonGroup != null)
        {
            _menuButtonGroup.alpha = 1f - _progress;
            _menuButtonGroup.interactable = _menuButtonGroup.blocksRaycasts = _progress < .5f;
        }
        // Cross-fade the hint through the middle of the swipe.
        _hint.text = _progress >= .5f ? _settings.HudCloseHint : _settings.HudOpenHint;
        _hint.color = new Color(_hintColor.r, _hintColor.g, _hintColor.b, _hintColor.a * Mathf.Abs(_progress * 2f - 1f));
    }

    private void PlaySettlePop()
    {
        _pop?.Kill();
        _pop = DOVirtual.Float(_settings.HudSettlePopScale, 1f, _settings.HudSettlePopSeconds, v => _popScale = v)
            .SetEase(Ease.OutBack).SetUpdate(true);
    }

    private void LateUpdate()
    {
        if (SwipeArea != null) SetProgress(_progress); // Preserve relative position on resolution/safe-area changes.
    }

    private void OnDestroy() { _slide?.Kill(); _pop?.Kill(); }
}
