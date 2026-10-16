using DG.Tweening;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;

/// <summary>Production battle-tab presentation. Persistent records and rewards are supplied separately.</summary>
public sealed class LobbyBattleView : MonoBehaviour
{
    private struct RoundMarker
    {
        internal RectTransform Rect;
        internal int Round;
        internal float Offset;
    }
    [SerializeField] private ScrollRect _scroll;
    [SerializeField] private RectTransform _content;
    [SerializeField] private TMP_FontAsset _labelFont;
    [SerializeField] private TMP_FontAsset _numberFont;
    [SerializeField] private Sprite _roundedSprite;
    [SerializeField] private Sprite _circleSprite;
    [SerializeField] private Sprite _chiefSprite;
    [SerializeField] private Sprite[] _bossSprites;
    [SerializeField] private Button _bestButton;
    [SerializeField] private TMP_Text _bestValue;
    [SerializeField] private Button _startButton;
    [SerializeField] private Button _partyButton;
    [SerializeField] private TMP_Text _staminaCostText;
    [SerializeField] private TMP_Text _partySubtitle;
    [SerializeField] private UI_PartySelectView _partyView;
    [SerializeField] private StaminaConfig _staminaConfig;
    [Header("Track layout in canvas units")]
    [SerializeField] private int _maxRound = 100;
    [SerializeField] private int _markerStep = 5;
    [SerializeField] private int _milestoneStep = 10;
    [SerializeField] private float _pixelsPerRound = 62f;
    [SerializeField] private float _bottomPadding = 330f;
    [SerializeField, Range(0f, 1f)] private float _focusRatio = 0.22f;
    [SerializeField] private float _chiefSize = 206f;
    private RectTransform _chief;
    private RectTransform _chiefBody;
    private TMP_Text _positionLabel;
    private Tween _hover;
    private Tween _focus;
    private PlayerDataController _playerData;
    private bool _built;
    private int? _bestRound;
    private Vector2 _viewportSize;
    private readonly List<RoundMarker> _roundMarkers = new List<RoundMarker>();
    private const float GoalTopSpace = 220f;
    // Production IngameScene currently authors NORMAL; HARD is an inactive draft, not a lobby selection.
    private const string RecordMode = "NORMAL";
    private float _nextRecordRetry;
    private const float RecordRetrySeconds = 30f;

    private void Awake()
    {
        _bestButton.onClick.AddListener(FocusCurrent);
        _bestButton.onClick.AddListener(RetryRecords);
        _partyButton.onClick.AddListener(OpenParty);
        _startButton.onClick.AddListener(OpenParty);
    }

    private void Start()
    {
        BuildOfferContainer();
        Canvas.ForceUpdateCanvases();
        BuildTrack();
        _built = true;
        ShowBestRound(Player.RoundRecords.GetBest(RecordMode));
        RefreshRecord();
        RefreshStamina(_playerData?.Data);
        StartHover();
    }

    /// <summary>Dedicated icon/banner parent. Content stays empty until real offers and eligibility are configured.</summary>
    public RectTransform OfferContainer { get; private set; }

    private void BuildOfferContainer()
    {
        OfferContainer = new GameObject("BattleOfferIcons", typeof(RectTransform)).GetComponent<RectTransform>();
        OfferContainer.SetParent(transform, false);
        OfferContainer.anchorMin = OfferContainer.anchorMax = Vector2.one;
        OfferContainer.pivot = Vector2.one; OfferContainer.anchoredPosition = new Vector2(-24, -100);
        OfferContainer.sizeDelta = new Vector2(160, 320);
        BottomPanelController tabs = null;
        foreach (var root in gameObject.scene.GetRootGameObjects())
        { tabs = root.GetComponentInChildren<BottomPanelController>(true); if (tabs != null) break; }
        OfferContainer.gameObject.AddComponent<LobbyOfferVisibility>().Configure(tabs);
    }

    private void OnEnable()
    {
        Player.RoundRecords.OnChanged += RefreshRecord;
        RefreshRecord();
        RetryRecords();
        _playerData = Player.PlayerData;
        if (_playerData != null) _playerData.OnUpdateUI += RefreshStamina;
        RefreshStamina(_playerData?.Data);
        if (_built) StartHover();
    }

    private void OnDisable()
    {
        Player.RoundRecords.OnChanged -= RefreshRecord;
        if (_playerData != null) _playerData.OnUpdateUI -= RefreshStamina;
        _playerData = null;
        _hover?.Kill(); _hover = null;
        _focus?.Kill(); _focus = null;
        _scroll.StopMovement();
        if (_chiefBody != null) _chiefBody.anchoredPosition = new Vector2(0f, 14f);
    }

    private void OnDestroy()
    {
        _bestButton.onClick.RemoveListener(FocusCurrent);
        _bestButton.onClick.RemoveListener(RetryRecords);
        _partyButton.onClick.RemoveListener(OpenParty);
        _startButton.onClick.RemoveListener(OpenParty);
    }

    /// <summary>Displays a supplied record; null is unknown, never a fabricated sample record.</summary>
    public void ShowBestRound(int? bestRound)
    {
        _bestRound = bestRound.HasValue ? Mathf.Max(0, bestRound.Value) : (int?)null;
        if (!_built) return;
        _bestValue.text = _bestRound.HasValue ? _bestRound.Value.ToString() : "—";
        _positionLabel.text = _bestRound.HasValue ? $"최고 {_bestRound.Value} 라운드" : "도전 준비";
        _chief.anchoredPosition = new Vector2(0f, RoundY(TrackRound));
        Canvas.ForceUpdateCanvases();
        FocusImmediately();
    }

    private int TrackRound => Mathf.Min(_bestRound ?? 0, _maxRound);

    private void RefreshRecord()
    {
        int? best = Player.RoundRecords.GetBest(RecordMode);
        if (!_built || best != _bestRound) ShowBestRound(best);
        if (!_built) return;
        _positionLabel.text = best.HasValue ? $"최고 {best.Value} 라운드" : "도전 준비";
        if (!string.IsNullOrEmpty(Player.RoundRecords.Status))
            _positionLabel.text += "\n<size=65%>" + Player.RoundRecords.Status + "</size>";
        ((RectTransform)_positionLabel.transform.parent).sizeDelta = new Vector2(340f,
            string.IsNullOrEmpty(Player.RoundRecords.Status) ? 58f : 88f);
    }

    private void RetryRecords()
    {
        _nextRecordRetry = Time.unscaledTime + RecordRetrySeconds;
        Player.RoundRecords.RefreshAsync().Forget();
    }

    private void OnApplicationPause(bool paused)
    {
        if (!paused) RetryRecords();
    }

    private void OpenParty()
    {
        // Keep the existing selection -> stamina/tutorial -> scene entry contract.
        // No stamina is spent by this presentation component.
        _partyView?.Open();
    }

    private void RefreshStamina(PlayerData data)
    {
        if (_staminaCostText == null) return;
        if (_staminaConfig == null) { _staminaCostText.text = "—"; return; }
        _staminaCostText.text = _staminaConfig.stageEntryCost.ToString();
        _staminaCostText.color = data != null && data.Stamina < _staminaConfig.stageEntryCost
            ? new Color(1f, 0.38f, 0.38f) : Color.white;
        if (_partySubtitle != null)
            _partySubtitle.text = GlobalData.SelectedParty != null ? GlobalData.SelectedParty.partyName : "족장과 덱 선택";
    }

    private void LateUpdate()
    {
        if (Time.unscaledTime >= _nextRecordRetry && !string.IsNullOrEmpty(Player.RoundRecords.Status)) RetryRecords();
        if (!_built || _scroll.viewport.rect.size == _viewportSize) return;
        // Respond to safe-area/aspect changes without overriding normal user dragging.
        _focus?.Kill(); _focus = null;
        UpdateTrackLayout();
        FocusImmediately();
    }

    private float RoundY(float round)
    {
        float available = _scroll.viewport.rect.height * (1f - _focusRatio) - GoalTopSpace;
        float spacing = Mathf.Min(_pixelsPerRound, Mathf.Max(1f, available / _milestoneStep));
        return _bottomPadding + round * spacing;
    }

    private void UpdateTrackLayout()
    {
        float height = RoundY(_maxRound) + _scroll.viewport.rect.height;
        _content.sizeDelta = new Vector2(_content.sizeDelta.x, height);
        foreach (var marker in _roundMarkers)
            marker.Rect.anchoredPosition = new Vector2(marker.Rect.anchoredPosition.x, RoundY(marker.Round) + marker.Offset);
        _chief.anchoredPosition = new Vector2(0f, RoundY(TrackRound));
    }

    private void PlaceRound(RectTransform rect, int round, Vector2 offset, Vector2 size)
    {
        Place(rect, new Vector2(offset.x, RoundY(round) + offset.y), size);
        _roundMarkers.Add(new RoundMarker { Rect = rect, Round = round, Offset = offset.y });
    }

    private float FocusPosition()
    {
        float height = _scroll.viewport.rect.height;
        float range = _content.rect.height - height;
        return range <= 0f ? 0f : Mathf.Clamp01((RoundY(TrackRound) - height * _focusRatio) / range);
    }

    private void FocusImmediately()
    {
        _viewportSize = _scroll.viewport.rect.size;
        _scroll.StopMovement();
        _scroll.verticalNormalizedPosition = FocusPosition();
    }

    private void FocusCurrent()
    {
        _focus?.Kill();
        _scroll.StopMovement();
        _focus = _scroll.DOVerticalNormalizedPos(FocusPosition(), 0.28f).SetEase(Ease.OutCubic).SetUpdate(true);
    }

    private void StartHover()
    {
        _hover?.Kill();
        if (_chiefBody == null) return;
        _chiefBody.anchoredPosition = new Vector2(0f, 14f);
        _hover = _chiefBody.DOAnchorPosY(20f, 1.1f).SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo).SetUpdate(true);
    }

    private void BuildTrack()
    {
        float height = RoundY(_maxRound) + _scroll.viewport.rect.height;
        _content.sizeDelta = new Vector2(_content.sizeDelta.x, height);
        // Draw progression directly over the authored cloud backdrop. Horizontal
        // anchors use the available screen width instead of a narrow fixed road.
        for (int round = _markerStep; round <= _maxRound; round += _markerStep)
        {
            // Soft lavender marks dissolve toward the cloud edges, leaving the
            // central number readable without dividing the scene into boxes.
            for (int segment = 0; segment < 4; segment++)
            {
                float distance = 0.085f + segment * 0.065f;
                float alpha = Mathf.Lerp(0.24f, 0.06f, segment / 3f);
                foreach (float side in new[] { -1f, 1f })
                {
                    var dash = Image($"Tick{round}_{side}_{segment}", _content, _roundedSprite,
                        new Color(0.78f, 0.75f, 0.94f, alpha));
                    dash.type = UnityEngine.UI.Image.Type.Sliced;
                    PlaceRound(dash.rectTransform, round, Vector2.zero, new Vector2(0f, 4f));
                    float center = 0.5f + side * distance;
                    dash.rectTransform.anchorMin = new Vector2(center - 0.018f, 0f);
                    dash.rectTransform.anchorMax = new Vector2(center + 0.018f, 0f);
                }
            }
            var number = Text("Round" + round, _content, round.ToString(), _numberFont, 42f, new Color(0.83f, 0.80f, 0.96f, 0.88f));
            PlaceRound(number.rectTransform, round, Vector2.zero, new Vector2(100f, 54f));
            if (round % _milestoneStep == 0) BuildMilestone(round);
        }
        BuildChief();
    }

    private void BuildMilestone(int round)
    {
        int index = round / _milestoneStep - 1;
        Sprite sprite = _bossSprites != null && _bossSprites.Length > 0 ? _bossSprites[index % _bossSprites.Length] : null;
        var boss = Image("Boss" + round, _content, sprite, index == 0 ? Color.white : new Color(0.09f, 0.07f, 0.15f, 0.7f));
        boss.preserveAspect = true;
        PlaceRound(boss.rectTransform, round, new Vector2(0f, 118f), new Vector2(174f, 174f));
        var bubble = Image("Milestone" + round, _content, _roundedSprite, new Color(0.94f, 0.92f, 1f, index == 0 ? 1f : 0.72f));
        bubble.type = UnityEngine.UI.Image.Type.Sliced;
        PlaceRound(bubble.rectTransform, round, new Vector2(0f, 118f), new Vector2(260f, 96f));
        bubble.rectTransform.anchorMin = bubble.rectTransform.anchorMax = new Vector2(0.74f, 0f);
        var title = Text("Title", bubble.transform, $"{round} 라운드", _labelFont, 28f, new Color(0.22f, 0.18f, 0.34f));
        Center(title.rectTransform, new Vector2(0f, 20f), new Vector2(244f, 38f));
        var subtitle = Text("RewardState", bubble.transform, "도달 보상 · 준비 중", _labelFont, 20f, new Color(0.43f, 0.37f, 0.54f));
        Center(subtitle.rectTransform, new Vector2(0f, -22f), new Vector2(244f, 32f));
    }

    private void BuildChief()
    {
        _chief = new GameObject("ProgressChief", typeof(RectTransform)).GetComponent<RectTransform>();
        _chief.SetParent(_content, false);
        Place(_chief, new Vector2(0f, RoundY(0)), new Vector2(_chiefSize, _chiefSize));
        var shadow = Image("GroundRing", _chief, _circleSprite, new Color(0.46f, 0.32f, 0.82f, 0.8f));
        Center(shadow.rectTransform, Vector2.zero, new Vector2(168f, 32f));
        var inner = Image("GroundCenter", _chief, _circleSprite, new Color(0.1f, 0.09f, 0.2f, 0.8f));
        Center(inner.rectTransform, Vector2.zero, new Vector2(136f, 19f));
        var body = Image("ChiefBody", _chief, _chiefSprite, Color.white);
        body.preserveAspect = true;
        _chiefBody = body.rectTransform;
        Center(_chiefBody, new Vector2(0f, 14f), new Vector2(_chiefSize, _chiefSize));
        _chiefBody.pivot = new Vector2(0.5f, 0f);
        var badge = Image("PositionBadge", _chief, _roundedSprite, new Color(0.13f, 0.10f, 0.23f, 0.97f));
        badge.type = UnityEngine.UI.Image.Type.Sliced;
        Center(badge.rectTransform, new Vector2(0f, -58f), new Vector2(240f, 58f));
        _positionLabel = Text("PositionLabel", badge.transform, "도전 준비", _labelFont, 26f, new Color(1f, 0.86f, 0.5f));
        Stretch(_positionLabel.rectTransform);
    }

    private static Image Image(string name, Transform parent, Sprite sprite, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<UnityEngine.UI.Image>();
        image.sprite = sprite; image.color = color; image.raycastTarget = false;
        return image;
    }

    private static TMP_Text Text(string name, Transform parent, string value, TMP_FontAsset font, float size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var text = go.GetComponent<TextMeshProUGUI>();
        text.font = font; text.text = value; text.fontSize = size; text.color = color;
        text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        return text;
    }

    private static void Place(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0.5f); rect.sizeDelta = size; rect.anchoredPosition = position;
    }
    private static void Center(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size; rect.anchoredPosition = position;
    }
    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}
