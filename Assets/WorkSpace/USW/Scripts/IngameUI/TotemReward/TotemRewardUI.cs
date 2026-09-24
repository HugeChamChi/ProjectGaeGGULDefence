using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using GaeGGUL.UI;
using GaeGGUL.UI.Totem;

/// <summary>
/// 보스 처치 토템 보상 화면 (design/test_1~4 스케치 기반, TotemSelectUI 대체 후보).
///
/// 흐름: 화면 어두워짐 → 사선 띠 3개가 좌우에서 번갈아 등장(개요) → 띠 터치 → 상세
///       (큰 아이콘 · 이름 · 설명 · 미니 그리드 범위 · 확정) → 확정 시 인벤토리에 넣고 닫힘.
///       상세에서 확정 버튼이 아닌 곳을 누르면 개요로 돌아간다. 시간 제한과 자동 선택은 없다.
///
/// 열려 있는 동안 게임 속도 0 · 보스 타이머 정지 · 유닛 루프 정지 (TotemSelectUI와 동일).
/// 인벤토리가 가득 차면 fallbackFood 만큼 식량으로 바뀐다 (안내는 튜토리얼 담당).
/// 화면 요소는 처음 열 때 코드로 만든다 — 이 컴포넌트가 붙은 오브젝트에 Canvas가 있어야 한다.
/// 모든 연출은 실제 시간(SetUpdate(true))으로 재생된다.
/// </summary>
public class TotemRewardUI : MonoBehaviour
{
    private const int ChoiceCount = 3;
    private const float TextBlockWidth = 0.55f;
    private const float DetailBlockWidth = 0.84f;
    private const float BackArrowSize = 110f;
    private const float DotSize = 22f;
    private const float RangeBoxPadding = 5f;  // 정보창 Grid_List와 같은 값
    private const float RangeBoxSpacing = 3f;
    private static readonly Vector2 ConfirmSize = new Vector2(740f, 150f);
    private static readonly Color ConfirmColor = new Color(1f, 0.72f, 0.17f, 1f);
    private static readonly Color TextDark = new Color(0.08f, 0.08f, 0.08f, 1f);
    private static readonly Color PanelDark = new Color(0f, 0f, 0f, 0.5f);

    [Inject] private TimerController _timerManager;
    [Inject] private TimeScaleService _timeScale;
    [Inject] private GridManager _gridManager;
    [Inject] private CurrencyManager _currencyManager;
    [Inject] private TotemInventory _inventory;

    [SerializeField] private TotemRewardSettings _settings;
    [Header("토템 풀 (랜덤 3개 대상)")]
    [SerializeField] private TotemData[] _totemPool;
    [Header("인벤토리 가득 찼을 때 대체 식량")]
    [SerializeField] private float _fallbackFood = 500f;

    /// <summary>개요 띠에 설명을 표시할지 (다음에 열 때 반영).</summary>
    public bool ShowDescriptionOnOverview { get; set; }
    /// <summary>상세 화면 좌우 밀기 허용 (다음에 열 때 반영).</summary>
    public bool AllowDetailSwipe { get; set; }
    /// <summary>띠 색을 등급 색으로 (다음에 열 때 반영).</summary>
    public bool UseTierColors { get; set; }
    /// <summary>보상 화면이 열려 있는지.</summary>
    public bool IsOpen => _isOpen;

    private sealed class BandView
    {
        public RectTransform Root;
        public UIPolygonGraphic Shape;
        public Image Icon;
        public TextMeshProUGUI Name;
        public GameObject DescriptionPanel;
        public TextMeshProUGUI Description;
        public RectTransform TextBlock;
    }

    private readonly List<TotemData> _choices = new List<TotemData>();
    private readonly HashSet<int> _chosenTotems = new HashSet<int>();
    private readonly Queue<Action> _pending = new Queue<Action>();
    private readonly BandView[] _bands = new BandView[ChoiceCount];
    private readonly List<Image> _dots = new List<Image>();

    private RectTransform _root;
    private CanvasGroup _rootGroup;
    private RectTransform _container;
    private Image _dim;
    private RectTransform _overview;
    private CanvasGroup _overviewGroup;
    private RectTransform _detail;
    private CanvasGroup _detailGroup;
    private Image _detailBackground;
    private TotemRewardSwipeArea _swipeArea;
    private RectTransform _detailContent;
    private CanvasGroup _detailContentGroup;
    private Image _detailIcon;
    private TextMeshProUGUI _detailName;
    private TextMeshProUGUI _detailDescription;
    private UI_TotemRangeGrid _rangeGrid;
    private TextMeshProUGUI _swipeHint;

    private Action _onChoiceMade;
    private bool _isOpen;
    private bool _busy;
    private int _detailIndex = -1;

    private void Awake()
    {
        if (_settings != null)
        {
            ShowDescriptionOnOverview = _settings.ShowDescriptionOnOverview;
            AllowDetailSwipe = _settings.AllowDetailSwipe;
            UseTierColors = _settings.UseTierColors;
        }
    }

    // ── 열기 ───────────────────────────────────────────────────

    /// <summary>보상 선택을 연다. 이미 열려 있으면 닫힌 뒤 이어서 연다. 선택이 끝나면 onChoiceMade를 부른다.</summary>
    public void Show(Action onChoiceMade)
    {
        if (_isOpen) { _pending.Enqueue(onChoiceMade); return; }
        ShowAsync(onChoiceMade, this.GetCancellationTokenOnDestroy()).Forget();
    }

    private async UniTaskVoid ShowAsync(Action onChoiceMade, CancellationToken token)
    {
        if (_settings == null)
        {
            Debug.LogError("[TotemRewardUI] TotemRewardSettings 미연결");
            onChoiceMade?.Invoke();
            return;
        }

        var choices = TotemChoiceRoller.Roll(_totemPool, _chosenTotems, ChoiceCount);
        if (choices.Count == 0)
        {
            Debug.LogWarning("[TotemRewardUI] 뽑을 토템이 없음");
            onChoiceMade?.Invoke();
            return;
        }

        _isOpen = true;
        _busy = true;
        _onChoiceMade = onChoiceMade;
        PauseGame();

        try
        {
            var loads = new List<UniTask>();
            foreach (var data in choices) loads.Add(data.LoadAssetsAsync());
            await UniTask.WhenAll(loads);
            token.ThrowIfCancellationRequested();

            EnsureBuilt();
            _choices.Clear();
            _choices.AddRange(choices);
            PopulateOverview();
            await PlayEntranceAsync(token);
        }
        catch (OperationCanceledException) { return; }
        finally { _busy = false; }
    }

    // ── 연출 ───────────────────────────────────────────────────

    private async UniTask PlayEntranceAsync(CancellationToken token)
    {
        _container.gameObject.SetActive(true);
        _rootGroup.alpha = 1f;
        _rootGroup.blocksRaycasts = true;
        _detail.gameObject.SetActive(false);
        _overview.gameObject.SetActive(true);
        _overviewGroup.alpha = 1f;

        float width = _root.rect.width;
        for (int i = 0; i < _bands.Length; i++)
        {
            bool visible = i < _choices.Count;
            _bands[i].Root.gameObject.SetActive(visible);
            _bands[i].Root.anchoredPosition = new Vector2(SlideSign(i) * width, 0f);
        }

        var dimColor = Color.black; dimColor.a = 0f;
        _dim.color = dimColor;
        await _dim.DOFade(_settings.DimAlpha, _settings.DimSeconds).SetUpdate(true).ToUniTask(cancellationToken: token);

        var slides = new List<UniTask>();
        for (int i = 0; i < _choices.Count; i++)
            slides.Add(SlideBandInAsync(_bands[i], i * _settings.BandStaggerSeconds, token));
        await UniTask.WhenAll(slides);
    }

    private async UniTask SlideBandInAsync(BandView band, float delay, CancellationToken token)
    {
        await band.Root.DOAnchorPosX(0f, _settings.BandSlideSeconds).SetDelay(delay).SetEase(Ease.OutCubic)
            .SetUpdate(true).ToUniTask(cancellationToken: token);
        band.Icon.transform.localScale = Vector3.one;
        _ = band.Icon.transform.DOPunchScale(Vector3.one * 0.18f, 0.3f, 6, 0.6f).SetUpdate(true);
    }

    /// <summary>위 · 아래 띠는 왼쪽에서, 가운데 띠는 오른쪽에서 들어온다 (아이콘 쪽과 같은 방향).</summary>
    private static float SlideSign(int slot) => slot % 2 == 0 ? -1f : 1f;

    // ── 개요 (사선 띠) ─────────────────────────────────────────

    private void PopulateOverview()
    {
        for (int i = 0; i < _choices.Count; i++)
        {
            var data = _choices[i];
            var band = _bands[i];
            band.Shape.color = BandColor(i);
            band.Icon.sprite = data.icon;
            band.Icon.enabled = data.icon != null;
            band.Name.text = data.totemName;
            band.Description.text = data.GetDisplayDescription();
            band.DescriptionPanel.SetActive(ShowDescriptionOnOverview && !string.IsNullOrWhiteSpace(data.description));
            LayoutRebuilder.ForceRebuildLayoutImmediate(band.TextBlock);
        }
    }

    private Color BandColor(int slot) =>
        _settings.GetBandColor(slot, _choices[slot].tier, UseTierColors);

    private void OnBandClicked(int slot)
    {
        if (_busy || !_isOpen || slot >= _choices.Count) return;
        _bands[slot].Root.DOKill(true);
        _bands[slot].Icon.transform.DOKill(true);
        OpenDetailAsync(slot, this.GetCancellationTokenOnDestroy()).Forget();
    }

    // ── 상세 ───────────────────────────────────────────────────

    private async UniTaskVoid OpenDetailAsync(int slot, CancellationToken token)
    {
        _busy = true;
        try
        {
            _overview.gameObject.SetActive(false);
            _detail.gameObject.SetActive(true);
            _detailGroup.alpha = 1f;
            _swipeArea.SwipeEnabled = AllowDetailSwipe && _choices.Count > 1;
            _swipeArea.Threshold = _settings.SwipeThreshold;
            _swipeHint.gameObject.SetActive(_swipeArea.SwipeEnabled);
            foreach (var dot in _dots) dot.gameObject.SetActive(_swipeArea.SwipeEnabled);

            ShowDetail(slot);
            _detailBackground.color = BandColor(slot);
            _detailContent.anchoredPosition = Vector2.zero;
            _detailContentGroup.alpha = 0f;
            _detailIcon.transform.localScale = Vector3.one * 0.6f;
            _ = _detailIcon.transform.DOScale(1f, _settings.SwitchSeconds * 1.5f).SetEase(Ease.OutBack).SetUpdate(true);
            await _detailContentGroup.DOFade(1f, _settings.SwitchSeconds).SetUpdate(true).ToUniTask(cancellationToken: token);
        }
        catch (OperationCanceledException) { }
        finally { _busy = false; }
    }

    private void ShowDetail(int slot)
    {
        _detailIndex = slot;
        var data = _choices[slot];
        _detailIcon.sprite = data.icon;
        _detailIcon.enabled = data.icon != null;
        _detailName.text = data.totemName;
        _detailDescription.text = data.GetDisplayDescription();
        _detailDescription.transform.parent.gameObject.SetActive(!string.IsNullOrWhiteSpace(_detailDescription.text));
        _rangeGrid?.SetData(data);
        for (int i = 0; i < _dots.Count; i++)
        {
            _dots[i].gameObject.SetActive(_swipeArea.SwipeEnabled && i < _choices.Count);
            _dots[i].color = i == slot ? Color.white : new Color(1f, 1f, 1f, 0.35f);
        }
        LayoutRebuilder.ForceRebuildLayoutImmediate(_detailContent);
    }

    private void OnDetailSwipe(int direction)
    {
        if (_busy || _detailIndex < 0 || _choices.Count < 2) return;
        int next = (_detailIndex + direction + _choices.Count) % _choices.Count;
        SwitchDetailAsync(next, direction, this.GetCancellationTokenOnDestroy()).Forget();
    }

    private async UniTaskVoid SwitchDetailAsync(int next, int direction, CancellationToken token)
    {
        _busy = true;
        try
        {
            float shift = _root.rect.width * 0.35f;
            float half = _settings.SwitchSeconds * 0.5f;
            var outSeq = DOTween.Sequence().SetUpdate(true)
                .Join(_detailContent.DOAnchorPosX(-direction * shift, half).SetEase(Ease.InCubic))
                .Join(_detailContentGroup.DOFade(0f, half));
            await outSeq.ToUniTask(cancellationToken: token);

            ShowDetail(next);
            _detailContent.anchoredPosition = new Vector2(direction * shift, 0f);
            var inSeq = DOTween.Sequence().SetUpdate(true)
                .Join(_detailContent.DOAnchorPosX(0f, half).SetEase(Ease.OutCubic))
                .Join(_detailContentGroup.DOFade(1f, half))
                .Join(_detailBackground.DOColor(BandColor(next), _settings.SwitchSeconds));
            await inSeq.ToUniTask(cancellationToken: token);
        }
        catch (OperationCanceledException) { }
        finally { _busy = false; }
    }

    private void BackToOverview()
    {
        if (_busy || !_detail.gameObject.activeSelf) return;
        BackToOverviewAsync(this.GetCancellationTokenOnDestroy()).Forget();
    }

    /// <summary>
    /// 개요(띠)를 먼저 온전히 깔아 둔 뒤 그 위의 상세를 걷어낸다.
    /// 개요를 투명에서 올리면 그 사이 뒤의 인게임 화면이 비치므로, 항상 불투명한 층이 한 겹 남도록 쌓는다.
    /// </summary>
    private async UniTaskVoid BackToOverviewAsync(CancellationToken token)
    {
        _busy = true;
        try
        {
            _overview.gameObject.SetActive(true);
            _overviewGroup.alpha = 1f;
            for (int i = 0; i < _choices.Count; i++) _bands[i].Root.anchoredPosition = Vector2.zero;
            await _detailGroup.DOFade(0f, _settings.SwitchSeconds).SetUpdate(true).ToUniTask(cancellationToken: token);
        }
        catch (OperationCanceledException) { return; }
        finally { _busy = false; }

        _detail.gameObject.SetActive(false);
        _detailGroup.alpha = 1f;
        _detailIndex = -1;
    }

    // ── 확정 · 닫기 ────────────────────────────────────────────

    private void OnConfirmClicked()
    {
        if (_busy || _detailIndex < 0 || _detailIndex >= _choices.Count) return;
        var data = _choices[_detailIndex];
        _chosenTotems.Add(data.totemId);
        if (_inventory == null || !_inventory.TryAdd(data))
        {
            Debug.Log($"[TotemRewardUI] 토템 인벤토리 가득 참 — 식량 {_fallbackFood} 지급");
            _currencyManager?.AddCurrency(_fallbackFood);
        }
        CloseAsync(this.GetCancellationTokenOnDestroy()).Forget();
    }

    private async UniTaskVoid CloseAsync(CancellationToken token)
    {
        _busy = true;
        try
        {
            _rootGroup.blocksRaycasts = false;
            await _rootGroup.DOFade(0f, _settings.SwitchSeconds).SetUpdate(true).ToUniTask(cancellationToken: token);
        }
        catch (OperationCanceledException) { return; }
        finally { _busy = false; }

        _container.gameObject.SetActive(false);
        _detailIndex = -1;
        _isOpen = false;
        ResumeGame();

        var callback = _onChoiceMade;
        _onChoiceMade = null;
        callback?.Invoke();

        if (!_isOpen && _pending.Count > 0) Show(_pending.Dequeue());
    }

    private void PauseGame()
    {
        _timeScale?.Pause(this);
        _timerManager?.StopTimer();
        if (_gridManager == null) return;
        foreach (var cell in _gridManager.GetOccupiedCells())
            cell.OccupyingUnit?.PauseLoops();
    }

    private void ResumeGame()
    {
        _timeScale?.Release(this);
        _timerManager?.ResumeTimer();
        if (_gridManager == null) return;
        foreach (var cell in _gridManager.GetOccupiedCells())
            cell.OccupyingUnit?.ResumeLoops();
    }

    private void OnDestroy()
    {
        if (_isOpen) _timeScale?.Release(this);
    }

    // ── 화면 구성 (최초 1회) ───────────────────────────────────

    private void EnsureBuilt()
    {
        if (_container != null) return;

        _root = (RectTransform)transform;
        _rootGroup = gameObject.GetComponent<CanvasGroup>();
        if (_rootGroup == null) _rootGroup = gameObject.AddComponent<CanvasGroup>();

        _container = NewRect("Container", _root);
        Stretch(_container);

        _dim = NewImage("Dim", _container, Color.black);
        Stretch(_dim.rectTransform);

        _overview = NewRect("Overview", _container);
        Stretch(_overview);
        _overviewGroup = _overview.gameObject.AddComponent<CanvasGroup>();
        for (int i = 0; i < ChoiceCount; i++) _bands[i] = BuildBand(i);

        BuildDetail();
        _container.gameObject.SetActive(false);
    }

    private BandView BuildBand(int slot)
    {
        var band = new BandView();
        band.Root = NewRect($"Band_{slot}", _overview);
        Stretch(band.Root);

        band.Shape = band.Root.gameObject.AddComponent<UIPolygonGraphic>();
        var u = _settings.UpperBoundary;
        var l = _settings.LowerBoundary;
        Vector2[] points;
        int[] edges;
        switch (slot)
        {
            case 0:
                points = new[] { new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f - u.y), new Vector2(0f, 1f - u.x) };
                edges = new[] { 2 };
                break;
            case 1:
                points = new[] { new Vector2(0f, 1f - u.x), new Vector2(1f, 1f - u.y), new Vector2(1f, 1f - l.y), new Vector2(0f, 1f - l.x) };
                edges = new[] { 0, 2 };
                break;
            default:
                points = new[] { new Vector2(0f, 1f - l.x), new Vector2(1f, 1f - l.y), new Vector2(1f, 0f), new Vector2(0f, 0f) };
                edges = new[] { 0 };
                break;
        }
        band.Shape.SetShape(points, edges, _settings.EdgeColor, _settings.EdgeWidth);
        var button = band.Root.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = band.Shape;
        int captured = slot;
        button.onClick.AddListener(() => OnBandClicked(captured));

        // 아이콘 쪽: 위·아래 띠는 왼쪽, 가운데 띠는 오른쪽 (스케치 test_3)
        bool iconLeft = slot % 2 == 0;
        float iconX = iconLeft ? 0.22f : 0.78f;
        float textX = iconLeft ? 0.675f : 0.325f;
        var size = _root.rect.size;

        band.Icon = NewImage("Icon", band.Root, Color.white);
        band.Icon.preserveAspect = true;
        band.Icon.raycastTarget = false;
        PlaceAt(band.Icon.rectTransform, new Vector2(iconX * size.x, BandMidY(points, iconX) * size.y),
            Vector2.one * _settings.OverviewIconSize);

        band.TextBlock = NewRect("TextBlock", band.Root);
        PlaceAt(band.TextBlock, new Vector2(textX * size.x, BandMidY(points, textX) * size.y),
            new Vector2(TextBlockWidth * size.x, 0f));
        var layout = band.TextBlock.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = iconLeft ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight;
        layout.childControlWidth = true; layout.childControlHeight = true;
        layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
        layout.spacing = 14f;
        band.TextBlock.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var align = iconLeft ? TextAlignmentOptions.Left : TextAlignmentOptions.Right;
        band.Name = NewText("Name", band.TextBlock, _settings.OverviewNameSize, Color.white, align);
        band.Name.fontStyle = FontStyles.Bold;
        band.Name.outlineWidth = 0.25f;
        band.Name.outlineColor = Color.black;

        band.Description = NewPanelText(band.TextBlock, _settings.OverviewDescriptionSize, align, out band.DescriptionPanel);
        return band;
    }

    /// <summary>다각형 윗변과 아랫변의 x 위치 높이 평균 (정규화). 꼭짓점 순서: 좌상, 우상, 우하, 좌하.</summary>
    private static float BandMidY(Vector2[] p, float x)
    {
        float top = Mathf.Lerp(p[0].y, p[1].y, x);
        float bottom = Mathf.Lerp(p[3].y, p[2].y, x);
        return (top + bottom) * 0.5f;
    }

    private void BuildDetail()
    {
        var size = _root.rect.size;
        _detail = NewRect("Detail", _container);
        Stretch(_detail);
        _detailGroup = _detail.gameObject.AddComponent<CanvasGroup>();

        _detailBackground = NewImage("Background", _detail, Color.white);
        Stretch(_detailBackground.rectTransform);
        _swipeArea = _detailBackground.gameObject.AddComponent<TotemRewardSwipeArea>();
        _swipeArea.OnTap += BackToOverview;
        _swipeArea.OnSwipe += OnDetailSwipe;

        // 뒤로가기: 설정의 아이콘 스프라이트(흰색 + 그림자), 없으면 삼각형으로 대체
        Graphic backGraphic;
        if (_settings.BackIcon != null)
        {
            var icon = NewImage("Back", _detail, Color.white);
            icon.sprite = _settings.BackIcon;
            icon.preserveAspect = true;
            var shadow = icon.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.45f);
            shadow.effectDistance = new Vector2(3f, -4f);
            backGraphic = icon;
        }
        else
        {
            var arrow = NewRect("Back", _detail).gameObject.AddComponent<UIPolygonGraphic>();
            arrow.color = Color.white;
            arrow.SetShape(new[] { new Vector2(0.1f, 0.5f), new Vector2(0.9f, 0.95f), new Vector2(0.9f, 0.05f) },
                new[] { 0, 1, 2 }, TextDark, 10f);
            backGraphic = arrow;
        }
        PlaceAt(backGraphic.rectTransform, new Vector2(90f, size.y - 110f), Vector2.one * BackArrowSize);
        var backButton = backGraphic.gameObject.AddComponent<Button>();
        backButton.targetGraphic = backGraphic;
        backButton.onClick.AddListener(BackToOverview);

        _detailContent = NewRect("Content", _detail);
        Stretch(_detailContent);
        _detailContentGroup = _detailContent.gameObject.AddComponent<CanvasGroup>();
        _detailContentGroup.blocksRaycasts = false; // 내용은 터치를 막지 않는다 — 배경 탭 = 돌아가기

        _detailIcon = NewImage("Icon", _detailContent, Color.white);
        _detailIcon.preserveAspect = true;
        _detailIcon.raycastTarget = false;
        PlaceAt(_detailIcon.rectTransform, new Vector2(size.x * 0.5f, size.y * 0.77f), Vector2.one * _settings.DetailIconSize);

        var block = NewRect("TextBlock", _detailContent);
        block.anchorMin = block.anchorMax = Vector2.zero;
        block.pivot = new Vector2(0.5f, 1f);
        block.anchoredPosition = new Vector2(size.x * 0.5f, size.y * 0.77f - _settings.DetailIconSize * 0.55f);
        block.sizeDelta = new Vector2(size.x * DetailBlockWidth, 0f);
        var layout = block.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true; layout.childControlHeight = true;
        layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
        layout.spacing = 18f;
        block.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        _detailName = NewText("Name", block, _settings.DetailNameSize, Color.white, TextAlignmentOptions.Center);
        _detailName.fontStyle = FontStyles.Bold;
        _detailName.outlineWidth = 0.25f;
        _detailName.outlineColor = Color.black;
        _detailDescription = NewPanelText(block, _settings.DetailDescriptionSize, TextAlignmentOptions.Center, out _);

        BuildRangeGrid(size);

        _swipeHint = NewText("SwipeHint", _detailContent, 30f, Color.white, TextAlignmentOptions.Center);
        _swipeHint.outlineWidth = 0.25f;
        _swipeHint.outlineColor = Color.black;
        _swipeHint.text = _settings.SwipeHint;
        PlaceAt(_swipeHint.rectTransform, new Vector2(size.x * 0.5f, size.y * 0.175f), new Vector2(size.x * 0.8f, 44f));
        for (int i = 0; i < ChoiceCount; i++)
        {
            var dot = NewImage($"Dot_{i}", _detailContent, Color.white);
            dot.raycastTarget = false;
            PlaceAt(dot.rectTransform, new Vector2(size.x * 0.5f + (i - (ChoiceCount - 1) * 0.5f) * DotSize * 2.2f, size.y * 0.145f),
                Vector2.one * DotSize);
            dot.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            _dots.Add(dot);
        }

        // 확정 버튼은 Content(터치 통과) 밖에 둔다
        var confirm = NewImage("Confirm", _detail, ConfirmColor);
        PlaceAt(confirm.rectTransform, new Vector2(size.x * 0.5f, size.y * 0.075f), ConfirmSize);
        var confirmButton = confirm.gameObject.AddComponent<Button>();
        confirmButton.targetGraphic = confirm;
        confirmButton.onClick.AddListener(OnConfirmClicked);
        var border = confirm.gameObject.AddComponent<Outline>(); // 노란 배경 위에서도 버튼 경계가 보이도록
        border.effectColor = TextDark;
        border.effectDistance = new Vector2(6f, -6f);
        var label = NewText("Label", confirm.rectTransform, 64f, Color.white, TextAlignmentOptions.Center);
        label.outlineWidth = 0.25f;
        label.outlineColor = Color.black;
        label.text = _settings.ConfirmLabel;
        label.fontStyle = FontStyles.Bold;
        Stretch(label.rectTransform);

        _detail.gameObject.SetActive(false);
    }

    /// <summary>
    /// 토템 정보창과 같은 범위 그리드 (명일방주식: 범위 칸 + 토템 칸만, "적용 범위" 캡션, 범위 없으면 "범위 없음").
    /// 정보창의 UI_TotemRangeGrid · 칸 프리팹 · 표시 설정 · 박스 스프라이트를 그대로 써서 모양을 통일한다.
    /// </summary>
    private void BuildRangeGrid(Vector2 screen)
    {
        if (_settings.RangeDisplaySettings == null || _settings.RangeCellPrefab == null)
        {
            Debug.LogWarning("[TotemRewardUI] 범위 그리드 설정(표시 설정/칸 프리팹) 미연결 — 범위 표시 생략");
            return;
        }

        var holder = NewRect("RangeGrid", _detailContent);
        PlaceAt(holder, new Vector2(screen.x * 0.5f, screen.y * 0.31f), _settings.RangeBoxSize);
        holder.localScale = Vector3.one * _settings.RangeBoxScale;

        var box = NewImage("Grid_List", holder, Color.white);
        box.sprite = _settings.RangeBoxSprite;
        box.raycastTarget = false;
        Stretch(box.rectTransform);
        var layout = box.gameObject.AddComponent<UIGridLayout>();
        layout.paddingLeft = layout.paddingRight = layout.paddingTop = layout.paddingBottom = RangeBoxPadding;
        layout.spacing = Vector2.one * RangeBoxSpacing;

        _rangeGrid = holder.gameObject.AddComponent<UI_TotemRangeGrid>();
        _rangeGrid.Configure(_settings.RangeDisplaySettings, _settings.RangeCellPrefab, layout);
        _rangeGrid.BlinkRangeCells = true; // 적용 범위 칸 깜빡임으로 범위 강조
    }

    // ── UI 생성 헬퍼 ───────────────────────────────────────────

    private static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static Image NewImage(string name, Transform parent, Color color)
    {
        var rt = NewRect(name, parent);
        var image = rt.gameObject.AddComponent<Image>();
        image.color = color;
        return image;
    }

    private TextMeshProUGUI NewText(string name, Transform parent, float fontSize, Color color, TextAlignmentOptions align)
    {
        var rt = NewRect(name, parent);
        var text = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (_settings.Font != null) text.font = _settings.Font;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = align;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.richText = true;
        return text;
    }

    /// <summary>반투명 검은 판 위의 흰 글자 (띠 색 위에서도 효과 색 글자가 읽히도록).</summary>
    private TextMeshProUGUI NewPanelText(Transform parent, float fontSize, TextAlignmentOptions align, out GameObject panel)
    {
        var bg = NewImage("DescriptionPanel", parent, PanelDark);
        bg.raycastTarget = false;
        var layout = bg.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(24, 24, 16, 16);
        layout.childControlWidth = true; layout.childControlHeight = true;
        layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
        panel = bg.gameObject;
        return NewText("Text", bg.rectTransform, fontSize, Color.white, align);
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static void PlaceAt(RectTransform rt, Vector2 position, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
    }

}
