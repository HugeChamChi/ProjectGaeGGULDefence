using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 로비 전투 탭 시안 실험실(FxLab_LobbyBattle). 사용자 10-16 "로비예시1(운빨존많겜)처럼, 가져올 건 가져오고 버릴 건 버리고".
/// 가져온 것: 최고 라운드 길(라운드 눈금·족장 위치), 다음 목표 보스와 도달 보상 말풍선, 최고 기록 배지, 스태미나 비용이 붙은 시작 버튼, 왼쪽 아이콘 열.
/// 버린 것: 친구랑 하기(자리는 편성 버튼으로), VIP·첫 결제·특가·영웅 모집·사냥 패스.
/// 백엔드·저장 없이 값만 바꿔 보는 시안이며 실제 로비 코드가 아니다. 보상 수치는 시안 값이다.
/// </summary>
public class LobbyBattleLab : MonoBehaviour
{
    private sealed class MilestoneView
    {
        public LobbyBattleMilestone Data;
        public int Side;
        public RectTransform Boss;
        public Image BossImage;
        public RectTransform Bubble;
        public Image BubbleImage;
        public GameObject ClaimChip;
        public GameObject LockChip;
        public GameObject DoneChip;
        public GameObject NextChip;
        public RectTransform RewardIcon;
        public Tween Pulse;
        public bool Cleared;
    }

    [Header("길")]
    [SerializeField] private ScrollRect _scroll;
    [SerializeField] private RectTransform _content;
    [SerializeField] private TMP_FontAsset _labelFont;
    [SerializeField] private TMP_FontAsset _numberFont;
    [SerializeField] private Sprite _roundedSprite;
    [SerializeField] private Sprite _circleSprite;
    [SerializeField] private Sprite _chiefSprite;
    [SerializeField] private Sprite[] _bossSprites;
    [SerializeField] private Sprite _goldIcon;
    [SerializeField] private Sprite _diamondIcon;
    [SerializeField] private int _maxRound = 100;
    [SerializeField] private int _markerStep = 5;
    [SerializeField] private float _pixelsPerRound = 105f;
    [SerializeField] private float _bottomPadding = 560f;
    [SerializeField] private float _roadWidth = 300f;
    [SerializeField] private float _sideOffset = 330f;
    [SerializeField, Range(0f, 1f)] private float _focusRatio = 0.3f;
    [SerializeField] private Color _roadColor = new Color(0.16f, 0.12f, 0.28f, 0.6f);
    [SerializeField] private Color _roadEdgeColor = new Color(0.55f, 0.45f, 0.9f, 0.55f);
    [SerializeField] private Color _roadFillColor = new Color(0.55f, 0.4f, 0.95f, 0.5f);
    [SerializeField] private Color _currencyAColor = new Color(0.18f, 0.72f, 0.66f);
    [SerializeField] private Color _currencyBColor = new Color(0.95f, 0.55f, 0.2f);

    [Header("시안 보상 (수치 미정)")]
    [SerializeField] private List<LobbyBattleMilestone> _milestones = new List<LobbyBattleMilestone>
    {
        new LobbyBattleMilestone(10, LobbyBattleRewardKind.Gold, 3000),
        new LobbyBattleMilestone(20, LobbyBattleRewardKind.CurrencyA, 30),
        new LobbyBattleMilestone(30, LobbyBattleRewardKind.Diamond, 100),
        new LobbyBattleMilestone(40, LobbyBattleRewardKind.CurrencyB, 30),
        new LobbyBattleMilestone(50, LobbyBattleRewardKind.CurrencyA, 60),
        new LobbyBattleMilestone(60, LobbyBattleRewardKind.Diamond, 200),
        new LobbyBattleMilestone(70, LobbyBattleRewardKind.CurrencyB, 60),
        new LobbyBattleMilestone(80, LobbyBattleRewardKind.Gold, 20000),
        new LobbyBattleMilestone(90, LobbyBattleRewardKind.CurrencyA, 100),
        new LobbyBattleMilestone(100, LobbyBattleRewardKind.Diamond, 500),
    };

    [Header("하단·측면")]
    [SerializeField] private TextMeshProUGUI _bestValue;
    [SerializeField] private Button _bestButton;
    [SerializeField] private GameObject _claimBadge;
    [SerializeField] private TextMeshProUGUI _claimBadgeText;
    [SerializeField] private Button _startButton;
    [SerializeField] private TextMeshProUGUI _staminaCostText;
    [SerializeField] private Button _partyButton;
    [SerializeField] private GameObject[] _candidateIcons;
    [SerializeField] private TextMeshProUGUI _hudStaminaText;

    [Header("획득 연출")]
    [SerializeField] private RectTransform _fxLayer;
    [SerializeField] private RectTransform _goldTarget;
    [SerializeField] private RectTransform _diamondTarget;
    [SerializeField] private RectTransform _currencyATarget;
    [SerializeField] private RectTransform _currencyBTarget;
    [SerializeField] private int _flyIconCount = 6;

    [Header("실험 값")]
    [SerializeField] private int _bestRound = 23;
    [SerializeField] private int _stamina = 30;
    [SerializeField] private int _maxStamina = 30;
    [SerializeField] private int _staminaCost = 5;
    [SerializeField] private TextMeshProUGUI _labStatus;
    [SerializeField] private CanvasGroup _labStatusGroup;
    [SerializeField] private GameObject _labPanel;

    private readonly List<MilestoneView> _views = new List<MilestoneView>();
    private readonly HashSet<int> _claimed = new HashSet<int>();
    private RectTransform _chief;
    private RectTransform _chiefBody;
    private RectTransform _roadFill;
    private float _roadStart;
    private bool _candidatesVisible;
    private Tween _advance;

    private void Awake()
    {
        if (_startButton != null) _startButton.onClick.AddListener(OnStartClicked);
        if (_partyButton != null) _partyButton.onClick.AddListener(OnPartyClicked);
        if (_bestButton != null) _bestButton.onClick.AddListener(OnBestBadgeClicked);
        SetCandidates(false);
    }

    private void Start()
    {
        BuildTrack();
        ApplyBest(_bestRound, false);
        Canvas.ForceUpdateCanvases();
        _scroll.verticalNormalizedPosition = NormalizedFor(RoundY(_bestRound));
        Status("최고 라운드 길 시안. 오른쪽 LAB 버튼으로 값을 바꿔 본다.");
    }

    // ───────────── 실험 버튼 ─────────────

    /// <summary>최고 라운드를 바로 바꾼다. 그보다 높은 보상의 수령 기록은 지운다.</summary>
    public void SetBestRound(int round)
    {
        _advance?.Kill();
        _claimed.RemoveWhere(r => r > round);
        ApplyBest(Mathf.Clamp(round, 0, _maxRound), false);
        _scroll.DOVerticalNormalizedPos(NormalizedFor(RoundY(_bestRound)), 0.35f).SetEase(Ease.OutCubic).SetLink(gameObject);
        Status($"최고 {_bestRound}라운드로 설정.");
    }

    /// <summary>런을 마치고 기록을 갱신해 돌아온 장면. 족장이 길을 따라 올라가고, 지난 보스는 옆으로 밀려나며 보상이 열린다.</summary>
    public void AdvanceBy(int rounds)
    {
        _advance?.Kill();
        int from = _bestRound;
        int to = Mathf.Clamp(from + rounds, 0, _maxRound);
        if (to == from) { Status("이미 끝 라운드다."); return; }
        _scroll.verticalNormalizedPosition = NormalizedFor(RoundY(from));
        float duration = Mathf.Clamp(0.12f * (to - from), 0.8f, 2.2f);
        float value = from;
        _advance = DOTween.To(() => value, v =>
            {
                value = v;
                PlaceChief(v);
                _scroll.verticalNormalizedPosition = NormalizedFor(RoundY(v));
                int passed = Mathf.FloorToInt(v);
                if (passed == _bestRound) return;
                bool crossedMilestone = false;
                foreach (var view in _views)
                    if (view.Data.Round > _bestRound && view.Data.Round <= passed) crossedMilestone = true;
                _bestRound = passed;
                _bestValue.text = passed.ToString();
                if (crossedMilestone) RefreshMilestones(true);
            }, to, duration)
            .SetDelay(0.35f).SetEase(Ease.InOutSine).SetLink(gameObject)
            .OnComplete(() => { ApplyBest(to, true); Status($"기록 갱신 {from} → {to}. 지난 보스 보상이 열렸다."); });
        Status($"런 종료 연출: {from} → {to}");
    }

    /// <summary>받은 보상 기록을 지운다.</summary>
    public void ResetClaims()
    {
        _claimed.Clear();
        RefreshMilestones(false);
        Status("보상 수령 기록 초기화.");
    }

    /// <summary>스태미나 가득/부족 상태를 바꾼다.</summary>
    public void ToggleStaminaEmpty()
    {
        _stamina = _stamina >= _staminaCost ? 0 : _maxStamina;
        RefreshStamina();
        Status(_stamina >= _staminaCost ? "스태미나 가득." : "스태미나 부족 상태.");
    }

    /// <summary>후보 아이콘(출석·퀘스트, 시스템 없음)을 보이거나 숨긴다.</summary>
    public void ToggleCandidates()
    {
        SetCandidates(!_candidatesVisible);
        Status(_candidatesVisible ? "후보 아이콘 표시 (출석·퀘스트는 시스템 없음)." : "후보 아이콘 숨김.");
    }

    /// <summary>실험 버튼 묶음을 접거나 편다.</summary>
    public void ToggleLabPanel()
    {
        if (_labPanel != null) _labPanel.SetActive(!_labPanel.activeSelf);
    }

    // ───────────── 화면 버튼 ─────────────

    private void OnStartClicked()
    {
        var rect = (RectTransform)_startButton.transform;
        rect.DOKill(true);
        if (_stamina < _staminaCost)
        {
            rect.DOShakeAnchorPos(0.35f, new Vector2(18f, 0f), 18, 0f).SetLink(gameObject);
            _staminaCostText.transform.DOKill(true);
            _staminaCostText.transform.DOPunchScale(Vector3.one * 0.35f, 0.35f).SetLink(gameObject);
            Status("스태미나 부족 → 실제 로비에서는 다이아 충전 팝업으로 연결할 자리.");
            return;
        }
        _stamina -= _staminaCost;
        RefreshStamina();
        rect.DOPunchScale(Vector3.one * 0.08f, 0.25f, 8, 0.6f).SetLink(gameObject);
        Status($"출격 (실험실이라 씬 이동 없음). 스태미나 -{_staminaCost}.");
    }

    private void OnPartyClicked()
    {
        var rect = (RectTransform)_partyButton.transform;
        rect.DOKill(true);
        rect.DOPunchScale(Vector3.one * 0.06f, 0.22f, 8, 0.6f).SetLink(gameObject);
        Status("편성 → 실제 로비에서는 기존 파티·족장 선택 창(UI_PartySelectView)을 연다.");
    }

    private void OnBestBadgeClicked()
    {
        MilestoneView first = null;
        foreach (var view in _views)
            if (IsClaimable(view)) { first = view; break; }
        if (first == null)
        {
            _scroll.DOVerticalNormalizedPos(NormalizedFor(RoundY(_bestRound)), 0.35f).SetEase(Ease.OutCubic).SetLink(gameObject);
            Status("받을 보상 없음 → 내 위치로.");
            return;
        }
        _scroll.DOVerticalNormalizedPos(NormalizedFor(RoundY(first.Data.Round)), 0.45f).SetEase(Ease.OutCubic).SetLink(gameObject);
        Status($"{first.Data.Round}라운드 보상 위치로 이동.");
    }

    // ───────────── 길 만들기 ─────────────

    private float RoundY(float round) => _bottomPadding + round * _pixelsPerRound;

    private float NormalizedFor(float y)
    {
        float viewport = _scroll.viewport != null ? _scroll.viewport.rect.height : ((RectTransform)_scroll.transform).rect.height;
        float range = _content.rect.height - viewport;
        if (range <= 0f) return 0f;
        return Mathf.Clamp01((y - viewport * _focusRatio) / range);
    }

    private void BuildTrack()
    {
        float height = RoundY(_maxRound + 4);
        _content.sizeDelta = new Vector2(_content.sizeDelta.x, height);
        _roadStart = RoundY(0) - 140f;

        var road = NewImage("Road", _content, _roundedSprite, _roadColor);
        road.type = Image.Type.Sliced;
        road.pixelsPerUnitMultiplier = 0.25f;
        var roadRect = road.rectTransform;
        SetBottom(roadRect, new Vector2(0f, _roadStart), new Vector2(_roadWidth, height - _roadStart));
        roadRect.pivot = new Vector2(0.5f, 0f);

        _roadFill = NewImage("ClearedFill", roadRect, _roundedSprite, _roadFillColor).rectTransform;
        _roadFill.GetComponent<Image>().type = Image.Type.Sliced;
        _roadFill.GetComponent<Image>().pixelsPerUnitMultiplier = 0.25f;
        _roadFill.anchorMin = new Vector2(0f, 0f);
        _roadFill.anchorMax = new Vector2(1f, 0f);
        _roadFill.pivot = new Vector2(0.5f, 0f);
        _roadFill.anchoredPosition = Vector2.zero;

        foreach (float x in new[] { -0.5f, 0.5f })
        {
            var edge = NewImage("Edge", roadRect, null, _roadEdgeColor).rectTransform;
            edge.anchorMin = new Vector2(0.5f + x, 0f);
            edge.anchorMax = new Vector2(0.5f + x, 1f);
            edge.pivot = new Vector2(0.5f, 0.5f);
            edge.sizeDelta = new Vector2(6f, -40f);
            edge.anchoredPosition = Vector2.zero;
        }

        for (int r = _markerStep; r <= _maxRound; r += _markerStep)
        {
            BuildDashes(RoundY(r));
            if (FindMilestone(r) != null) continue;
            var number = NewText($"Round{r}", _content, r.ToString(), _numberFont, 130f, new Color(1f, 1f, 1f, 0.16f));
            SetBottom(number.rectTransform, new Vector2(0f, RoundY(r) + 90f), new Vector2(400f, 160f));
        }

        for (int i = 0; i < _milestones.Count; i++)
            _views.Add(BuildMilestone(_milestones[i], i));

        BuildChief();
    }

    private void BuildDashes(float y)
    {
        const int count = 7;
        float step = _roadWidth / count;
        for (int i = 0; i < count; i++)
        {
            var dash = NewImage("Dash", _content, null, new Color(1f, 1f, 1f, 0.22f)).rectTransform;
            SetBottom(dash, new Vector2(-_roadWidth * 0.5f + step * (i + 0.5f), y), new Vector2(step * 0.55f, 6f));
        }
    }

    private LobbyBattleMilestone FindMilestone(int round)
    {
        foreach (var m in _milestones) if (m.Round == round) return m;
        return null;
    }

    private MilestoneView BuildMilestone(LobbyBattleMilestone data, int index)
    {
        float y = RoundY(data.Round);
        var view = new MilestoneView { Data = data, Side = 1 };

        var node = NewImage($"Node{data.Round}", _content, _circleSprite, new Color(0.46f, 0.31f, 0.82f));
        SetBottom(node.rectTransform, new Vector2(0f, y), new Vector2(84f, 84f));
        var nodeText = NewText("Value", node.rectTransform, data.Round.ToString(), _numberFont, 38f, Color.white);
        Stretch(nodeText.rectTransform);

        var sprite = _bossSprites != null && _bossSprites.Length > 0 ? _bossSprites[index % _bossSprites.Length] : null;
        view.BossImage = NewImage($"Boss{data.Round}", _content, sprite, Color.white);
        view.BossImage.preserveAspect = true;
        view.Boss = view.BossImage.rectTransform;
        SetBottom(view.Boss, new Vector2(0f, y + 150f), new Vector2(230f, 230f));

        view.BubbleImage = NewImage($"Bubble{data.Round}", _content, _roundedSprite, Color.white);
        view.BubbleImage.type = Image.Type.Sliced;
        view.BubbleImage.pixelsPerUnitMultiplier = 0.35f;
        view.BubbleImage.raycastTarget = true;
        view.Bubble = view.BubbleImage.rectTransform;
        SetBottom(view.Bubble, new Vector2(0f, y + 365f), new Vector2(290f, 150f));
        var button = view.Bubble.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(() => Claim(view));

        var tail = NewImage("Tail", view.Bubble, null, Color.white).rectTransform;
        tail.anchorMin = tail.anchorMax = new Vector2(0.5f, 0f);
        tail.sizeDelta = new Vector2(30f, 30f);
        tail.anchoredPosition = new Vector2(0f, 2f);
        tail.localRotation = Quaternion.Euler(0f, 0f, 45f);
        tail.SetAsFirstSibling();

        var header = NewText("Header", view.Bubble, $"{data.Round}라운드 보상", _labelFont, 26f, new Color(0.25f, 0.22f, 0.32f));
        header.rectTransform.anchorMin = header.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        header.rectTransform.sizeDelta = new Vector2(280f, 40f);
        header.rectTransform.anchoredPosition = new Vector2(0f, -30f);

        view.RewardIcon = CreateRewardIcon(view.Bubble, data.Kind, 66f);
        view.RewardIcon.anchorMin = view.RewardIcon.anchorMax = new Vector2(0.5f, 0.5f);
        view.RewardIcon.anchoredPosition = new Vector2(-52f, -14f);
        var amount = NewText("Amount", view.Bubble, $"x{data.Amount:N0}", _numberFont, 38f, new Color(0.2f, 0.17f, 0.27f));
        amount.alignment = TextAlignmentOptions.MidlineLeft;
        amount.rectTransform.anchorMin = amount.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        amount.rectTransform.sizeDelta = new Vector2(160f, 60f);
        amount.rectTransform.anchoredPosition = new Vector2(66f, -14f);

        view.ClaimChip = Chip(view.Bubble, "받기!", new Color(0.2f, 0.75f, 0.35f), Color.white);
        view.LockChip = Chip(view.Bubble, "도달 필요", new Color(0.55f, 0.53f, 0.6f), Color.white);
        view.DoneChip = Chip(view.Bubble, "받음", new Color(0.3f, 0.27f, 0.38f), new Color(1f, 1f, 1f, 0.8f));
        view.NextChip = Chip(view.Bubble, "다음 목표", new Color(0.46f, 0.31f, 0.82f), Color.white);
        return view;
    }

    private GameObject Chip(RectTransform parent, string text, Color background, Color textColor)
    {
        var chip = NewImage($"Chip_{text}", parent, _roundedSprite, background);
        chip.type = Image.Type.Sliced;
        chip.pixelsPerUnitMultiplier = 0.3f;
        var rect = chip.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
        rect.sizeDelta = new Vector2(150f, 48f);
        rect.anchoredPosition = new Vector2(0f, -4f);
        var label = NewText("Label", rect, text, _labelFont, 26f, textColor);
        Stretch(label.rectTransform);
        return chip.gameObject;
    }

    private void BuildChief()
    {
        _chief = new GameObject("Chief", typeof(RectTransform)).GetComponent<RectTransform>();
        _chief.SetParent(_content, false);
        SetBottom(_chief, Vector2.zero, new Vector2(280f, 280f));

        var shadow = NewImage("Shadow", _chief, _circleSprite, new Color(0f, 0f, 0f, 0.35f)).rectTransform;
        shadow.anchorMin = shadow.anchorMax = new Vector2(0.5f, 0.5f);
        shadow.sizeDelta = new Vector2(170f, 44f);
        shadow.anchoredPosition = new Vector2(0f, -6f);

        var body = NewImage("Body", _chief, _chiefSprite, Color.white);
        body.preserveAspect = true;
        _chiefBody = body.rectTransform;
        _chiefBody.anchorMin = _chiefBody.anchorMax = new Vector2(0.5f, 0.5f);
        _chiefBody.pivot = new Vector2(0.5f, 0f);
        _chiefBody.sizeDelta = new Vector2(260f, 260f);
        _chiefBody.anchoredPosition = new Vector2(0f, 0f);
        _chiefBody.DOAnchorPosY(16f, 0.9f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetLink(gameObject);
    }

    private void PlaceChief(float round)
    {
        _chief.anchoredPosition = new Vector2(0f, RoundY(round));
        _roadFill.sizeDelta = new Vector2(0f, Mathf.Max(0f, RoundY(round) - _roadStart));
    }

    // ───────────── 상태 반영 ─────────────

    private void ApplyBest(int best, bool animate)
    {
        _bestRound = best;
        PlaceChief(best);
        _bestValue.text = best.ToString();
        if (animate) _bestValue.transform.DOPunchScale(Vector3.one * 0.3f, 0.4f, 8, 0.6f).SetLink(gameObject);
        RefreshMilestones(animate);
        RefreshStamina();
    }

    private bool IsClaimable(MilestoneView view) => view.Data.Round <= _bestRound && !_claimed.Contains(view.Data.Round);

    private void RefreshMilestones(bool animate)
    {
        int claimable = 0;
        float y;
        int next = -1;
        foreach (var view in _views)
            if (view.Data.Round > _bestRound) { next = view.Data.Round; break; }
        foreach (var view in _views)
        {
            bool cleared = view.Data.Round <= _bestRound;
            bool claimed = _claimed.Contains(view.Data.Round);
            bool newlyCleared = cleared && !view.Cleared;
            view.Cleared = cleared;
            y = RoundY(view.Data.Round);

            var bossPos = cleared ? new Vector2(view.Side * _sideOffset, y + 90f) : new Vector2(0f, y + 140f);
            var bossScale = cleared ? Vector3.one * 0.72f : Vector3.one;
            bool isNext = view.Data.Round == next;
            var bossColor = cleared ? new Color(0.55f, 0.52f, 0.62f, 0.7f) : isNext ? Color.white : new Color(0.05f, 0.03f, 0.1f, 0.55f);
            var bubblePos = cleared ? new Vector2(view.Side * _sideOffset, y + 280f) : new Vector2(0f, y + 345f);
            view.Boss.DOKill();
            view.BossImage.DOKill();
            view.Bubble.DOKill();
            view.Pulse?.Kill();
            if (animate && newlyCleared)
            {
                view.Boss.DOAnchorPos(bossPos, 0.45f).SetEase(Ease.OutBack).SetLink(gameObject);
                view.Boss.DOScale(bossScale, 0.45f).SetLink(gameObject);
                view.Boss.DOPunchRotation(new Vector3(0f, 0f, 25f * view.Side), 0.5f, 6).SetLink(gameObject);
                view.BossImage.DOColor(bossColor, 0.45f).SetLink(gameObject);
                view.Bubble.DOAnchorPos(bubblePos, 0.45f).SetEase(Ease.OutBack).SetLink(gameObject);
            }
            else
            {
                view.Boss.anchoredPosition = bossPos;
                view.Boss.localScale = bossScale;
                view.Boss.localRotation = Quaternion.identity;
                view.BossImage.color = bossColor;
                view.Bubble.anchoredPosition = bubblePos;
            }

            bool canClaim = cleared && !claimed;
            view.ClaimChip.SetActive(canClaim);
            view.LockChip.SetActive(!cleared && !isNext);
            view.NextChip.SetActive(isNext);
            view.DoneChip.SetActive(cleared && claimed);
            view.BubbleImage.color = canClaim ? new Color(1f, 0.95f, 0.72f) : claimed ? new Color(0.78f, 0.76f, 0.84f, 0.75f) : isNext ? Color.white : new Color(1f, 1f, 1f, 0.5f);
            var iconColor = IconColor(view.Data.Kind);
            iconColor.a = claimed ? 0.45f : 1f;
            view.RewardIcon.GetComponent<Graphic>().color = iconColor;
            view.Bubble.localScale = claimed ? Vector3.one * 0.82f : !cleared && !isNext ? Vector3.one * 0.85f : Vector3.one;
            if (canClaim)
            {
                claimable++;
                view.Pulse = view.Bubble.DOScale(1.07f, 0.55f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo)
                    .SetDelay(animate ? 0.45f : 0f).SetLink(gameObject);
            }
        }
        _claimBadge.SetActive(claimable > 0);
        _claimBadgeText.text = $"보상 {claimable}";
        if (claimable > 0 && animate) _claimBadge.transform.DOPunchScale(Vector3.one * 0.4f, 0.4f).SetLink(gameObject);
    }

    private void RefreshStamina()
    {
        bool enough = _stamina >= _staminaCost;
        _staminaCostText.color = enough ? Color.white : new Color(1f, 0.38f, 0.38f);
        if (_hudStaminaText != null) _hudStaminaText.text = $"{_stamina}/{_maxStamina}";
    }

    private void SetCandidates(bool visible)
    {
        _candidatesVisible = visible;
        if (_candidateIcons == null) return;
        foreach (var icon in _candidateIcons) if (icon != null) icon.SetActive(visible);
    }

    // ───────────── 보상 받기 ─────────────

    private void Claim(MilestoneView view)
    {
        if (!view.Cleared)
        {
            view.Bubble.DOKill(true);
            view.Bubble.DOShakeAnchorPos(0.3f, new Vector2(12f, 0f), 16, 0f).SetLink(gameObject);
            Status($"{view.Data.Round}라운드에 도달하면 받는다.");
            return;
        }
        if (_claimed.Contains(view.Data.Round)) { Status("이미 받은 보상."); return; }
        _claimed.Add(view.Data.Round);
        view.Pulse?.Kill();
        view.Bubble.DOKill(true);
        view.Bubble.localScale = Vector3.one;
        view.Bubble.DOPunchScale(Vector3.one * 0.18f, 0.3f, 8, 0.6f).SetLink(gameObject)
            .OnComplete(() => RefreshMilestones(false));
        FlyReward(view);
        Status($"{view.Data.Round}라운드 보상 {KindName(view.Data.Kind)} x{view.Data.Amount:N0} 받음 (실험실이라 저장 없음).");
    }

    private void FlyReward(MilestoneView view)
    {
        var target = TargetFor(view.Data.Kind);
        if (_fxLayer == null || target == null) return;
        Vector2 from = ToFxLocal(view.RewardIcon);
        Vector2 to = ToFxLocal(target);
        for (int i = 0; i < _flyIconCount; i++)
        {
            var icon = CreateRewardIcon(_fxLayer, view.Data.Kind, 72f);
            icon.anchorMin = icon.anchorMax = new Vector2(0.5f, 0.5f);
            icon.anchoredPosition = from;
            icon.localScale = Vector3.one * 0.4f;
            var burst = from + Random.insideUnitCircle * 140f;
            float delay = i * 0.05f;
            var seq = DOTween.Sequence().SetLink(icon.gameObject);
            seq.Append(icon.DOAnchorPos(burst, 0.28f).SetEase(Ease.OutQuad).SetDelay(delay));
            seq.Join(icon.DOScale(1f, 0.28f).SetDelay(delay));
            seq.Append(icon.DOAnchorPos(to, 0.5f).SetEase(Ease.InQuad));
            seq.Join(icon.DOScale(0.55f, 0.5f));
            bool last = i == _flyIconCount - 1;
            seq.OnComplete(() =>
            {
                target.DOKill(true);
                target.DOPunchScale(Vector3.one * 0.25f, 0.25f, 8, 0.6f).SetLink(target.gameObject);
                if (last) ShowUseHint(view.Data.Kind, to);
                Destroy(icon.gameObject);
            });
        }
    }

    private void ShowUseHint(LobbyBattleRewardKind kind, Vector2 at)
    {
        string hint = kind switch
        {
            LobbyBattleRewardKind.CurrencyA => "캐릭터 강화에서 사용",
            LobbyBattleRewardKind.CurrencyB => "마을 강화에서 사용",
            _ => null
        };
        if (hint == null) return;
        var bubble = NewImage("UseHint", _fxLayer, _roundedSprite, new Color(0.1f, 0.08f, 0.16f, 0.92f));
        bubble.type = Image.Type.Sliced;
        bubble.pixelsPerUnitMultiplier = 0.3f;
        var rect = bubble.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(330f, 64f);
        rect.anchoredPosition = at + new Vector2(0f, 190f);
        var label = NewText("Label", rect, hint, _labelFont, 28f, Color.white);
        Stretch(label.rectTransform);
        var group = rect.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        var seq = DOTween.Sequence().SetLink(rect.gameObject);
        seq.Append(group.DOFade(1f, 0.2f));
        seq.Join(rect.DOAnchorPosY(at.y + 220f, 0.3f).SetEase(Ease.OutBack));
        seq.AppendInterval(1.4f);
        seq.Append(group.DOFade(0f, 0.3f));
        seq.OnComplete(() => Destroy(rect.gameObject));
    }

    private RectTransform TargetFor(LobbyBattleRewardKind kind) => kind switch
    {
        LobbyBattleRewardKind.Gold => _goldTarget,
        LobbyBattleRewardKind.Diamond => _diamondTarget,
        LobbyBattleRewardKind.CurrencyA => _currencyATarget,
        LobbyBattleRewardKind.CurrencyB => _currencyBTarget,
        _ => null
    };

    private static string KindName(LobbyBattleRewardKind kind) => kind switch
    {
        LobbyBattleRewardKind.Gold => "골드",
        LobbyBattleRewardKind.Diamond => "다이아",
        LobbyBattleRewardKind.CurrencyA => "재화A",
        _ => "재화B"
    };

    private Vector2 ToFxLocal(RectTransform source)
    {
        var sourceCanvas = source.GetComponentInParent<Canvas>().rootCanvas;
        var sourceCamera = sourceCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : sourceCanvas.worldCamera;
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(sourceCamera, source.TransformPoint(source.rect.center));
        var fxCanvas = _fxLayer.GetComponentInParent<Canvas>().rootCanvas;
        var fxCamera = fxCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : fxCanvas.worldCamera;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_fxLayer, screen, fxCamera, out var local);
        return local;
    }

    // ───────────── 부품 ─────────────

    /// <summary>보상 아이콘. 재화A·B는 아이콘이 없어 색 원 + 글자로 둔다.</summary>
    private RectTransform CreateRewardIcon(RectTransform parent, LobbyBattleRewardKind kind, float size)
    {
        Sprite sprite = kind == LobbyBattleRewardKind.Gold ? _goldIcon : kind == LobbyBattleRewardKind.Diamond ? _diamondIcon : _circleSprite;
        var image = NewImage($"Icon_{kind}", parent, sprite, IconColor(kind));
        image.preserveAspect = true;
        image.rectTransform.sizeDelta = new Vector2(size, size);
        if (kind == LobbyBattleRewardKind.CurrencyA || kind == LobbyBattleRewardKind.CurrencyB)
        {
            var letter = NewText("Letter", image.rectTransform, kind == LobbyBattleRewardKind.CurrencyA ? "A" : "B", _numberFont, size * 0.6f, Color.white);
            Stretch(letter.rectTransform);
        }
        return image.rectTransform;
    }

    private Color IconColor(LobbyBattleRewardKind kind) =>
        kind == LobbyBattleRewardKind.CurrencyA ? _currencyAColor : kind == LobbyBattleRewardKind.CurrencyB ? _currencyBColor : Color.white;

    private static Image NewImage(string name, Transform parent, Sprite sprite, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static TextMeshProUGUI NewText(string name, Transform parent, string text, TMP_FontAsset font, float size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var label = go.GetComponent<TextMeshProUGUI>();
        if (font != null) label.font = font;
        label.text = text;
        label.fontSize = size;
        label.color = color;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;
        return label;
    }

    private static void SetBottom(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private void Status(string message)
    {
        if (_labStatus == null) return;
        _labStatus.text = message;
        if (_labStatusGroup == null) return;
        _labStatusGroup.DOKill();
        _labStatusGroup.alpha = 1f;
        _labStatusGroup.DOFade(0f, 0.4f).SetDelay(3f).SetLink(gameObject);
    }
}
