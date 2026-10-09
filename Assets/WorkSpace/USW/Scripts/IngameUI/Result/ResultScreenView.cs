using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 무한모드 판 종료 결과 화면 (실험실 1차, 2026-10-01, 레퍼런스 design/전투종료예시1·2).
/// 순서: 헤더·족장 → 라운드 카운트 → 마지막 보스 HP → 빌드 아이콘 → 보상 → 버튼.
/// 게임이 멈춘 상태에서 재생하므로 기본은 unscaled 시간. 재생 중 화면을 누르면 끝 상태로 건너뛴다.
/// 프리팹·실험실 씬은 Tools/USW/Fx/Build Result Screen Lab이 만든다.
/// </summary>
public class ResultScreenView : MonoBehaviour
{
    // 연출 시작 시각(초). HP·빌드·보상 구간은 앞 구간 길이에 따라 이어 붙는다.
    private const int BuildPopupSortingOffset = 20;
    private const float HeaderInTime = 0.1f;
    private const float ChiefInTime = 0.2f;
    private const float RoundInTime = 0.4f;

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

    private void Awake()
    {
        Cache();
        _retryButton.onClick.AddListener(() => RetryRequested?.Invoke());
        _homeButton.onClick.AddListener(() => HomeRequested?.Invoke());
        if (_adRewardButton != null) _adRewardButton.onClick.AddListener(() => AdRewardRequested?.Invoke());
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
    public void Show(ResultScreenData data, ResultScreenTone tone, bool showBuild)
    {
        Cache();
        Kill();
        gameObject.SetActive(true);

        bool hasBuild = showBuild && data.BuildChoices != null && data.BuildChoices.Length > 0;
        ResetVisuals(data, hasBuild);

        var s = DOTween.Sequence().SetUpdate(_useUnscaledTime).SetLink(gameObject);

        // ① 담백한 검은 배경
        s.Insert(0f, _dim.DOFade(1f, 0.25f));

        // ② 헤더 배너 낙하 + 족장 등장
        var headerRt = (RectTransform)_header.transform;
        headerRt.anchoredPosition = _headerBasePos + Vector2.up * _headerDropDistance;
        s.Insert(HeaderInTime, _header.DOFade(1f, 0.2f));
        s.Insert(HeaderInTime, headerRt.DOAnchorPos(_headerBasePos, 0.45f).SetEase(Ease.OutBack));
        s.Insert(ChiefInTime, _chief.DOScale(1f, 0.45f).SetEase(Ease.OutBack));

        // ③ 라운드 카운트
        s.Insert(RoundInTime, _recordBlock.DOFade(1f, 0.2f));
        int shownRound = 0;
        s.Insert(RoundInTime, DOTween.To(() => shownRound, v => { shownRound = v; _roundValue.text = v.ToString(); },
            data.Round, _roundCountDuration).SetEase(Ease.OutCubic));
        float t = RoundInTime + _roundCountDuration;

        if (_bossProgress != null && data.LastBossDamageRatio >= 0f)
        {
            var progressAnimation = _bossProgress.CreateAnimation(data.LastBossDamageRatio);
            s.Insert(t, progressAnimation);
            t += progressAnimation.Duration();
        }

        // ⑤ 이번 판 빌드
        if (hasBuild)
        {
            s.Insert(t, _buildBlock.DOFade(1f, 0.15f));
            for (int i = 0; i < data.BuildChoices.Length; i++)
            {
                var choice = data.BuildChoices[i];
                var icon = Spawn(_buildIconTemplate.gameObject).GetComponent<Image>();
                icon.sprite = choice?.Icon;
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
                _buildScroll.verticalNormalizedPosition = 1f;
            }
            t += 0.15f;
        }

        // ⑥ 보상 (+ 광고 2배 자리)
        s.Insert(t, _rewardBlock.DOFade(1f, 0.15f));
        if (data.Rewards != null)
        {
            foreach (var reward in data.Rewards)
            {
                var slot = Spawn(_rewardSlotTemplate.gameObject).transform;
                var icon = slot.Find("Icon").GetComponent<Image>();
                var amount = slot.GetComponentInChildren<TextMeshProUGUI>();
                icon.sprite = reward.Icon;
                amount.text = "0";
                slot.localScale = Vector3.zero;
                s.Insert(t, slot.DOScale(1f, 0.25f).SetEase(Ease.OutBack));
                int shownAmount = 0;
                int target = reward.Amount;
                s.Insert(t + 0.1f, DOTween.To(() => shownAmount, v => { shownAmount = v; amount.text = v.ToString("N0"); },
                    target, 0.4f).SetEase(Ease.OutCubic));
                t += _rewardStagger;
            }
        }
        bool canAdvertise = AdRewardRequested != null && data.Rewards != null && data.Rewards.Length > 0;
        _adSlot.gameObject.SetActive(canAdvertise);
        if (canAdvertise)
        {
            _adSlot.transform.SetAsLastSibling();
            s.Insert(t, _adSlot.DOFade(_adSlotAlpha, 0.2f));
            s.Insert(t, _adSlot.transform.DOScale(1f, 0.28f).SetEase(Ease.OutBack));
        }
        t += 0.25f;

        // ⑦ 버튼
        s.Insert(t, _buttons.DOFade(1f, 0.25f));
        s.OnComplete(() =>
        {
            _buttons.interactable = _buttons.blocksRaycasts = true;
            _buildBlock.interactable = true;
            _adSlot.interactable = _adSlot.blocksRaycasts = canAdvertise;
        });
        _sequence = s;
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
        _buildBlock.interactable = false;
        _buildBlock.gameObject.SetActive(showBuild);
        _rewardBlock.alpha = 0f;
        _rewardBlock.gameObject.SetActive(data.Rewards != null && data.Rewards.Length > 0);
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
