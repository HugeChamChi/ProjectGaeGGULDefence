using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 전투 종료 결과 화면. design/전투종료예시.png의 화면 전환·배치로 표시한다.
/// 순서: 배경 암전 → 배너·라운드·보스 이미지 → 보상 → 마지막 보스 HP → 빌드 → 버튼.
/// 게임이 멈춘 상태에서 재생하므로 기본은 unscaled 시간. 재생 중 화면을 누르면 끝 상태로 건너뛴다.
/// 프리팹·실험실 씬은 Tools/USW/Fx/Build Result Screen Lab이 만든다.
/// </summary>
public class ResultScreenView : MonoBehaviour
{
    // 연출 시작 시각(초). HP·빌드·보상 구간은 앞 구간 길이에 따라 이어 붙는다.
    private const int BuildPopupSortingOffset = 20;
    private const float HeaderInTime = 0.15f;
    private const float ChiefInTime = 0.55f;
    private const float RoundInTime = 0.45f;

    [Header("공통")]
    [SerializeField] private Image _dim;
    [SerializeField] private Button _skipCatcher;

    [Header("헤더")]
    [SerializeField] private CanvasGroup _header;
    [SerializeField] private Image _headerBanner;
    [SerializeField] private TextMeshProUGUI _headerTitle;
    [SerializeField] private Sprite _plainBanner;
    [SerializeField] private RectTransform _chief;

    [Header("기록")]
    [SerializeField] private CanvasGroup _recordBlock;
    [SerializeField] private TextMeshProUGUI _roundValue;
    [SerializeField] private ResultBossProgressView _bossProgress;

    [Header("빌드 / 보상")]
    [SerializeField] private CanvasGroup _buildBlock;
    [SerializeField] private Image _buildIconTemplate;
    [SerializeField] private ScrollRect _buildScroll;
    [SerializeField] private CanvasGroup _rewardBlock;
    [SerializeField] private RectTransform _rewardSlotTemplate;
    [SerializeField] private CanvasGroup _adSlot;
    [SerializeField] private Button _adRewardButton;
    [Tooltip("빌드 줄을 숨길 때 보상 묶음을 위로 올리는 거리")]
    [SerializeField] private float _rewardShiftWithoutBuild = 260f;

    [SerializeField] private Sprite[] _unavailableRewardIcons;
    [SerializeField] private TextMeshProUGUI _rewardUnavailableLabel;
    [SerializeField] private TextMeshProUGUI _emptyBuildLabel;
    [SerializeField] private Button _statsButton;
    [SerializeField] private Sprite _statsIcon;

    [Header("선택지 설명 팝업")]
    [SerializeField] private GameObject _buildPopup;
    [SerializeField] private RectTransform _buildPopupPanel;
    [SerializeField] private CanvasGroup _buildPopupGroup;
    [SerializeField] private Button _buildPopupBackdrop;
    [SerializeField] private Button _buildPopupClose;
    [SerializeField] private Image _buildPopupIcon;
    [SerializeField] private TextMeshProUGUI _buildPopupTitle;
    [SerializeField] private TextMeshProUGUI _buildPopupDescription;
    [SerializeField] private ScrollRect _buildPopupScroll;

    [Header("설명창 등장 연출")]
    [SerializeField, Min(0.01f)] private float _popupEnterDuration = 0.22f;
    [SerializeField, Range(0.1f, 1f)] private float _popupEnterScale = 0.82f;
    [SerializeField, Min(0f)] private float _popupEnterOvershoot = 1.8f;
    [SerializeField, Min(0.01f)] private float _popupFadeDuration = 0.1f;
    [SerializeField, Min(0.01f)] private float _popupExitDuration = 0.12f;
    [SerializeField, Min(0.01f)] private float _popupExitFadeDuration = 0.08f;
    [SerializeField, Range(0.1f, 1f)] private float _popupExitScale = 0.9f;

    [Header("버튼")]
    [SerializeField] private CanvasGroup _buttons;
    [SerializeField] private Button _retryButton;
    [SerializeField] private Button _homeButton;

    [Header("연출 값")]
    [SerializeField, Range(0f, 1f)] private float _adSlotAlpha = 1f;
    [SerializeField] private float _headerDropDistance = 260f;
    [SerializeField] private float _roundCountDuration = 0.6f;
    [SerializeField] private float _iconStagger = 0.06f;
    [SerializeField] private float _rewardStagger = 0.1f;
    [SerializeField] private bool _useUnscaledTime = true;

    /// <summary>다시하기 버튼.</summary>
    public event Action RetryRequested;

    /// <summary>로비로 버튼.</summary>
    public event Action HomeRequested;

    /// <summary>광고를 보고 2배 보상을 받는 요청. 실제 광고 시청과 지급은 호출자가 처리한다.</summary>
    public event Action AdRewardRequested;

    /// <summary>timeScale 무시 여부. 실험실 고정 프레임 캡처 중에만 false.</summary>
    public bool UseUnscaledTime
    {
        get => _useUnscaledTime;
        set => _useUnscaledTime = value;
    }

    private readonly List<GameObject> _spawned = new List<GameObject>();
    private Sequence _sequence;
    private Sequence _popupSequence;
    private bool _popupClosing;
    private Vector2 _headerBasePos;
    private Vector2 _rewardBasePos;
    private bool _cached;
    private ResultScreenData _data;
    private SafeAreaContentFrame _contentFrame;

    private void Awake()
    {
        Cache();
        _retryButton.onClick.AddListener(() => RetryRequested?.Invoke());
        _homeButton.onClick.AddListener(() => HomeRequested?.Invoke());
        if (_adRewardButton != null) _adRewardButton.onClick.AddListener(() => AdRewardRequested?.Invoke());
        if (_statsButton != null) _statsButton.onClick.AddListener(ShowRunStats);
        _skipCatcher.onClick.AddListener(Skip);
        if (_buildPopupBackdrop != null) _buildPopupBackdrop.onClick.AddListener(CloseBuildPopup);
        if (_buildPopupClose != null) _buildPopupClose.onClick.AddListener(CloseBuildPopup);
    }

    private void OnDisable()
    {
        Kill();
        HideBuildPopupImmediately();
    }

    /// <summary>결과 화면을 처음부터 재생한다.</summary>
    public void Show(ResultScreenData data, ResultScreenTone tone, bool showBuild, bool isWin = false)
    {
        if (data == null) return;
        Cache();
        Kill();
        _data = data;
        gameObject.SetActive(true);
        _contentFrame?.Refresh();
        RefreshGraphics();
        bool hasBuild = showBuild && data.BuildChoices != null && data.BuildChoices.Length > 0;
        ResetVisuals(data, showBuild);
        _headerTitle.text = isWin ? "전투 승리!" : "전투 종료";
        if (_emptyBuildLabel != null) _emptyBuildLabel.gameObject.SetActive(showBuild && !hasBuild);

        var s = DOTween.Sequence().SetUpdate(_useUnscaledTime).SetLink(gameObject);
        s.Insert(0f, _dim.DOFade(1f, 0.65f));
        var headerRt = (RectTransform)_header.transform;
        headerRt.anchoredPosition = _headerBasePos + Vector2.up * _headerDropDistance;
        s.Insert(HeaderInTime, _header.DOFade(1f, 0.25f));
        s.Insert(HeaderInTime, headerRt.DOAnchorPos(_headerBasePos, 0.5f).SetEase(Ease.OutBack));
        s.Insert(ChiefInTime, _chief.DOScale(1f, 0.45f).SetEase(Ease.OutBack));
        s.Insert(RoundInTime, _recordBlock.DOFade(1f, 0.2f));
        int shownRound = 0;
        s.Insert(RoundInTime, DOTween.To(() => shownRound, v => { shownRound = v; _roundValue.text = v.ToString(); },
            data.Round, _roundCountDuration).SetEase(Ease.OutCubic));

        // 미연결 보상은 수량을 만들지 않고 원본 아이콘과 대시로 표시한다.
        bool hasRewards = data.Rewards != null && data.Rewards.Length > 0;
        int rewardCount = hasRewards ? data.Rewards.Length : (_unavailableRewardIcons?.Length ?? 0);
        float t = RoundInTime + _roundCountDuration;
        s.Insert(t, _rewardBlock.DOFade(1f, 0.15f));
        for (int i = 0; i < rewardCount; i++)
        {
            var slot = Spawn(_rewardSlotTemplate.gameObject).transform;
            var icon = slot.Find("Icon").GetComponent<Image>();
            var amount = slot.GetComponentInChildren<TextMeshProUGUI>();
            icon.sprite = hasRewards ? data.Rewards[i].Icon : _unavailableRewardIcons[i];
            amount.text = hasRewards ? "0" : "—";
            slot.localScale = Vector3.zero;
            if (!hasRewards)
            {
                var group = slot.GetComponent<CanvasGroup>();
                if (group == null) group = slot.gameObject.AddComponent<CanvasGroup>();
                group.alpha = 0.45f;
            }
            s.Insert(t, slot.DOScale(1f, 0.25f).SetEase(Ease.OutBack));
            if (hasRewards)
            {
                int shownAmount = 0;
                int target = data.Rewards[i].Amount;
                s.Insert(t + 0.1f, DOTween.To(() => shownAmount,
                    v => { shownAmount = v; amount.text = v.ToString("N0"); }, target, 0.4f).SetEase(Ease.OutCubic));
            }
            t += _rewardStagger;
        }
        t += 0.15f;
        if (_bossProgress != null && data.LastBossDamageRatio >= 0f)
        {
            var animation = _bossProgress.CreateAnimation(data.LastBossDamageRatio);
            s.Insert(t, animation);
            t += animation.Duration();
        }
        if (showBuild)
        {
            s.Insert(t, _buildBlock.DOFade(1f, 0.15f));
            if (hasBuild)
            {
                for (int i = 0; i < data.BuildChoices.Length; i++)
                {
                    var choice = data.BuildChoices[i];
                    if (choice == null) continue;
                    var icon = Spawn(_buildIconTemplate.gameObject).GetComponent<Image>();
                    icon.sprite = choice.Icon;
                    icon.preserveAspect = true;
                    icon.GetComponent<Button>().onClick.AddListener(() => ShowBuildPopup(choice, icon.rectTransform));
                    icon.transform.localScale = Vector3.zero;
                    s.Insert(t, icon.transform.DOScale(1f, 0.25f).SetEase(Ease.OutBack));
                    t += _iconStagger;
                }
                if (_buildScroll != null)
                {
                    LayoutRebuilder.ForceRebuildLayoutImmediate(_buildScroll.content);
                    _buildScroll.StopMovement();
                    _buildScroll.horizontalNormalizedPosition = 0f;
                    _buildScroll.verticalNormalizedPosition = 1f;
                }
            }
            t += 0.15f;
        }
        bool canAdvertise = AdRewardRequested != null && hasRewards;
        _adSlot.gameObject.SetActive(true);
        _adRewardButton.interactable = canAdvertise;
        s.Insert(t, _adSlot.DOFade(canAdvertise ? _adSlotAlpha : 0.5f, 0.2f));
        s.Insert(t, _adSlot.transform.DOScale(1f, 0.28f).SetEase(Ease.OutBack));
        s.Insert(t + 0.1f, _buttons.DOFade(1f, 0.25f));
        s.OnComplete(() => FinishEntrance(showBuild, canAdvertise));
        _sequence = s;
    }

    private void FinishEntrance(bool showBuild, bool canAdvertise)
    {
        // 첫 활성화 프레임에 건너뛰어도 숨겨진 그룹·정적 글자까지 끝 상태를 확정한다.
        _dim.color = new Color(_dim.color.r, _dim.color.g, _dim.color.b, 1f);
        _header.alpha = _recordBlock.alpha = _rewardBlock.alpha = _buttons.alpha = 1f;
        ((RectTransform)_header.transform).anchoredPosition = _headerBasePos;
        _chief.localScale = Vector3.one;
        _roundValue.text = _data.Round.ToString();
        if (_bossProgress != null && _data.LastBossDamageRatio >= 0f)
            _bossProgress.Settle(_data.LastBossDamageRatio);
        _buildBlock.alpha = 1f;
        _buttons.interactable = _buttons.blocksRaycasts = true;
        _buildBlock.interactable = _buildBlock.blocksRaycasts = showBuild;
        _adSlot.alpha = canAdvertise ? _adSlotAlpha : 0.5f;
        _adSlot.transform.localScale = Vector3.one;
        _adSlot.interactable = _adSlot.blocksRaycasts = canAdvertise;
        int rewardIndex = 0;
        foreach (var go in _spawned)
        {
            go.transform.localScale = Vector3.one;
            if (go.transform.Find("Icon") == null) continue;
            var amount = go.GetComponentInChildren<TextMeshProUGUI>();
            if (amount != null)
                amount.text = _data.Rewards != null && rewardIndex < _data.Rewards.Length
                    ? _data.Rewards[rewardIndex].Amount.ToString("N0") : "—";
            rewardIndex++;
        }
        RefreshGraphics();
    }

    private void RefreshGraphics()
    {
        // 첫 활성화에서 투명/0크기 상태로 만들어진 정적 UI 배치를 갱신한다.
        foreach (var graphic in GetComponentsInChildren<Graphic>())
        {
            graphic.SetAllDirty();
            if (graphic is TextMeshProUGUI text) text.ForceMeshUpdate();
        }
        Canvas.ForceUpdateCanvases();
    }

    private void ShowRunStats()
    {
        if (_data == null || _statsButton == null) return;
        int seconds = Mathf.Max(0, Mathf.FloorToInt(_data.SurvivalSeconds));
        ShowBuildPopup(new ResultBuildChoice
        {
            Icon = _statsIcon,
            Name = "전투 기록",
            Description = $"도달 라운드  {_data.Round}\n보스 처치  {_data.BossKills}\n생존 시간  {seconds / 60:00}:{seconds % 60:00}"
        }, (RectTransform)_statsButton.transform);
    }

    /// <summary>재생 중이면 끝 상태로 건너뛴다.</summary>
    public void Skip()
    {
        if (_sequence != null && _sequence.IsActive() && !_sequence.IsComplete())
            _sequence.Complete(true);
    }

    private void ShowBuildPopup(ResultBuildChoice choice, RectTransform source)
    {
        if (_buildPopup == null || choice == null) return;
        ResetPopupAnimation();
        _buildPopupIcon.sprite = choice.Icon;
        _buildPopupIcon.preserveAspect = true;
        _buildPopupIcon.enabled = choice.Icon != null;
        _buildPopupTitle.text = choice.Name;
        _buildPopupDescription.text = choice.Description;
        _buildPopup.SetActive(true);
        if (_buildPopupPanel != null)
        {
            if (_buildPopupScroll != null)
            {
                const float minDescriptionHeight = 34f;
                const float maxDescriptionHeight = 132f;
                const float popupChromeHeight = 118f;
                float width = _buildPopupScroll.viewport.rect.width;
                float textHeight = _buildPopupDescription.GetPreferredValues(choice.Description, width, 0f).y;
                float height = Mathf.Clamp(textHeight, minDescriptionHeight, maxDescriptionHeight);
                _buildPopupScroll.viewport.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
                _buildPopupPanel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height + popupChromeHeight);
            }
            var popupRect = (RectTransform)_buildPopup.transform;
            Vector3 local = popupRect.InverseTransformPoint(source.TransformPoint(source.rect.center));
            Vector2 halfSize = _buildPopupPanel.rect.size * 0.5f;
            const float screenPadding = 24f;
            const float iconGap = 16f;
            _buildPopupPanel.anchoredPosition = new Vector2(
                Mathf.Clamp(local.x, popupRect.rect.xMin + halfSize.x + screenPadding, popupRect.rect.xMax - halfSize.x - screenPadding),
                Mathf.Clamp(local.y + source.rect.height * 0.5f + halfSize.y + iconGap,
                    popupRect.rect.yMin + halfSize.y + screenPadding, popupRect.rect.yMax - halfSize.y - screenPadding));
        }
        if (_buildPopupScroll != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(_buildPopupScroll.content);
            _buildPopupScroll.StopMovement();
            _buildPopupScroll.verticalNormalizedPosition = 1f;
        }
        if (_buildPopupPanel != null)
        {
            _buildPopupPanel.localScale = Vector3.one * _popupEnterScale;
            var sequence = DOTween.Sequence().SetUpdate(_useUnscaledTime).SetLink(_buildPopup);
            sequence.Append(_buildPopupPanel.DOScale(1f, _popupEnterDuration)
                .SetEase(Ease.OutBack, _popupEnterOvershoot));
            if (_buildPopupGroup != null)
            {
                _buildPopupGroup.alpha = 0f;
                sequence.Join(_buildPopupGroup.DOFade(1f, _popupFadeDuration).SetEase(Ease.OutQuad));
            }
            _popupSequence = sequence;
        }
    }

    private void CloseBuildPopup()
    {
        if (_buildPopup == null || !_buildPopup.activeSelf || _popupClosing) return;
        if (_buildPopupPanel == null || !isActiveAndEnabled)
        {
            HideBuildPopupImmediately();
            return;
        }
        _popupSequence?.Kill();
        _popupClosing = true;
        if (_buildPopupScroll != null) _buildPopupScroll.StopMovement();
        if (_buildPopupGroup != null) _buildPopupGroup.interactable = false;
        var sequence = DOTween.Sequence().SetUpdate(_useUnscaledTime).SetLink(_buildPopup);
        sequence.Append(_buildPopupPanel.DOScale(_buildPopupPanel.localScale * _popupExitScale, _popupExitDuration).SetEase(Ease.InQuad));
        if (_buildPopupGroup != null)
            sequence.Join(_buildPopupGroup.DOFade(0f, Mathf.Min(_popupExitDuration, _popupExitFadeDuration)).SetEase(Ease.OutQuad));
        sequence.OnComplete(FinishPopupClose);
        _popupSequence = sequence;
    }

    private void FinishPopupClose()
    {
        _popupSequence = null;
        HideBuildPopupImmediately();
    }

    private void HideBuildPopupImmediately()
    {
        // 숨김 직후 scale/alpha 복구가 렌더링 잔상처럼 보이지 않도록 투명 상태를 유지한다.
        if (_buildPopupGroup != null) _buildPopupGroup.alpha = 0f;
        if (_buildPopup != null) _buildPopup.SetActive(false);
        ResetPopupAnimation(false);
    }

    private void ResetPopupAnimation(bool visible = true)
    {
        _popupSequence?.Kill();
        _popupSequence = null;
        _popupClosing = false;
        if (_buildPopupPanel != null) _buildPopupPanel.localScale = Vector3.one;
        if (_buildPopupGroup != null)
        {
            _buildPopupGroup.alpha = visible ? 1f : 0f;
            _buildPopupGroup.interactable = visible;
        }
    }

    private void ResetVisuals(ResultScreenData data, bool showBuild)
    {
        ClearSpawned();
        HideBuildPopupImmediately();
        var dimColor = _dim.color;
        dimColor.a = 0f;
        _dim.color = dimColor;
        _header.alpha = 0f;
        _chief.localScale = Vector3.zero;
        _recordBlock.alpha = 0f;
        _buildBlock.alpha = 0f;
        _buildBlock.interactable = _buildBlock.blocksRaycasts = false;
        _buildBlock.gameObject.SetActive(showBuild);
        _rewardBlock.alpha = 0f;
        _rewardBlock.gameObject.SetActive(true);
        if (_rewardUnavailableLabel != null)
            _rewardUnavailableLabel.gameObject.SetActive(data.Rewards == null || data.Rewards.Length == 0);
        _adSlot.alpha = 0f;
        _adSlot.transform.localScale = Vector3.one * 0.9f;
        _adSlot.interactable = _adSlot.blocksRaycasts = false;
        _buttons.alpha = 0f;
        _buttons.interactable = _buttons.blocksRaycasts = false;
        ((RectTransform)_rewardBlock.transform).anchoredPosition =
            _rewardBasePos + (showBuild ? Vector2.zero : Vector2.up * _rewardShiftWithoutBuild);

        _headerBanner.sprite = _plainBanner;
        _headerTitle.text = "전투 종료";
        _roundValue.text = "0";
        if (_bossProgress != null) _bossProgress.Prepare(data);
    }

    private GameObject Spawn(GameObject template)
    {
        var go = Instantiate(template, template.transform.parent, false);
        go.SetActive(true);
        _spawned.Add(go);
        return go;
    }

    private void ClearSpawned()
    {
        foreach (var go in _spawned)
            if (go != null)
            {
                go.SetActive(false);
                Destroy(go);
            }
        _spawned.Clear();
    }

    private void Kill()
    {
        _sequence?.Kill();
        _sequence = null;
    }

    private void Cache()
    {
        if (_cached) return;
        _cached = true;
        _contentFrame = GetComponentInChildren<SafeAreaContentFrame>(true);
        // Lab parent order is 0; production result order may be 2100 or higher.
        // Keep the popup above its result instead of using the prefab's absolute lab order.
        if (_buildPopup != null)
        {
            var popupCanvas = _buildPopup.GetComponent<Canvas>();
            var parentCanvas = _buildPopup.transform.parent.GetComponentInParent<Canvas>(true);
            if (popupCanvas != null && parentCanvas != null)
            {
                popupCanvas.overrideSorting = true;
                popupCanvas.sortingOrder = parentCanvas.sortingOrder + BuildPopupSortingOffset;
            }
        }
        _headerBasePos = ((RectTransform)_header.transform).anchoredPosition;
        _rewardBasePos = ((RectTransform)_rewardBlock.transform).anchoredPosition;
        _buildIconTemplate.gameObject.SetActive(false);
        _rewardSlotTemplate.gameObject.SetActive(false);
    }
}
