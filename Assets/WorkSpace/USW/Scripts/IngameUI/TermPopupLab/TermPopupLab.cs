using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 용어 팝업 실험실(FxLab_TermPopup) 드라이버 — 게임 시스템 없이 레벨업 카드 3장에 용어 링크를 달고
/// A 말풍선 / B 바텀 시트 / A→B(요약 후 자세히)를 비교한다.
/// 카드는 실제 LevelUp 카드 프리팹을 쓰고, 설명만 실험실 문구([[용어]] 표기)로 바꾼다. 게임 코드·프리팹은 바꾸지 않는다.
/// 씬 구성은 Tools/USW/Fx/Build Term Popup Lab (TermPopupLabBuilder)이 만든다.
/// </summary>
public sealed class TermPopupLab : MonoBehaviour
{
    private static readonly Regex TermMarkup = new Regex(@"\[\[(.+?)\]\]");
    private const string LinkPrefix = "term:";

    [Header("참조 (빌더가 연결)")]
    [SerializeField] private RectTransform _popupCanvas;
    [SerializeField] private GameObject _panelObject;
    [SerializeField] private RectTransform _cardContainer;
    [SerializeField] private LevelUpCardUI _cardPrefab;
    [Tooltip("말풍선·시트·칩의 둥근 모서리 (비우면 각진 모서리)")]
    [SerializeField] private Sprite _roundedSprite;
    [SerializeField] private TMP_Text _modeLabel;
    [SerializeField] private TMP_Text _punchLabel;
    [Tooltip("게임 레벨업 화면의 등급 배경(검은 바탕 + 글로우 + 올라오는 입자). 카드를 깔 때 가장 높은 등급으로 재생한다")]
    [SerializeField] private TierRevealFx _tierReveal;

    [Header("카드 (위에서부터)")]
    [SerializeField] private LevelUpData[] _cardData = new LevelUpData[3];
    [Tooltip("[[용어]]로 감싼 단어가 링크가 된다. 비우면 원래 SO 설명")]
    [SerializeField, TextArea] private string[] _descriptions = new string[3];

    [Header("용어 (실험실 예시 문구)")]
    [SerializeField] private TermPopupLabTerm[] _terms = Array.Empty<TermPopupLabTerm>();

    [Header("비교")]
    [SerializeField] private TermPopupLabMode _mode = TermPopupLabMode.BubbleThenSheet;
    [Tooltip("링크 터치 판정을 글자보다 넓히는 양 (캔버스 기준 단위)")]
    [SerializeField, Min(0f)] private float _hitPadding = 18f;
    [SerializeField] private Color _termColor = new Color(0.5f, 0.83f, 0.93f, 1f);
    [Tooltip("팝업이 열린 동안 다른 카드 투명도")]
    [SerializeField, Range(0f, 1f)] private float _dimAlpha = 0.35f;
    [Tooltip("시트가 가린 카드를 밀어 올릴 때 시트 위 여유")]
    [SerializeField, Min(0f)] private float _sheetClearance = 30f;
    [SerializeField, Min(0.01f)] private float _shiftDuration = 0.28f;
    [Tooltip("B 연출 NEW: 시트를 열 때 누른 카드가 살짝 튄다 / 밀어 올릴 때 넘쳤다 돌아오는 정도")]
    [SerializeField] private bool _punchySheet = true;
    [SerializeField, Min(0f)] private float _cardPunch = 0.05f;
    [SerializeField, Min(0f)] private float _shiftOvershoot = 1.2f;
    [Tooltip("카드 선택이 됐는지 보여 주는 확대 유지 시간 (실험실은 실제 효과를 적용하지 않는다)")]
    [SerializeField, Min(0f)] private float _selectPreviewSeconds = 0.7f;

    private readonly List<LevelUpCardUI> _cards = new List<LevelUpCardUI>();
    private readonly List<TermLinkTapTarget> _links = new List<TermLinkTapTarget>();
    private TermPopupLabView _view;
    private int _activeCard = -1;
    private string _activeTerm;
    private Vector2 _containerBase;
    private bool _spawning;

    /// <summary>실험실 검증용: 카드 선택 콜백이 불린 횟수.</summary>
    public int SelectionCount { get; private set; }
    /// <summary>실험실 검증용: 팝업(방패)이 열려 있는지.</summary>
    public bool PopupOpen => _view != null && _view.IsOpen;
    /// <summary>실험실 검증용: 위에서부터 cardIndex번째 카드의 링크 판정 컴포넌트.</summary>
    public TermLinkTapTarget GetLink(int cardIndex) => cardIndex >= 0 && cardIndex < _links.Count ? _links[cardIndex] : null;
    /// <summary>실험실 검증용: 비교 모드를 바로 정한다.</summary>
    public void SetMode(TermPopupLabMode mode)
    {
        _mode = mode;
        _view?.Close();
        RefreshModeLabel();
    }

    private void Start()
    {
        if (_panelObject != null) _panelObject.SetActive(true);
        RefreshModeLabel();
        Respawn();
    }

    /// <summary>MODE 버튼: A → B → A+B 순으로 바꾼다. 열린 팝업은 닫는다.</summary>
    public void CycleMode()
    {
        _mode = (TermPopupLabMode)(((int)_mode + 1) % 3);
        _view?.Close();
        RefreshModeLabel();
        Debug.Log($"[TermPopupLab] 모드: {ModeName(_mode)}");
    }

    /// <summary>B 연출 버튼: NEW(텐션) ↔ OLD(처음 버전). 열린 팝업은 닫는다.</summary>
    public void TogglePunch()
    {
        _punchySheet = !_punchySheet;
        if (_view != null) { _view.Close(); _view.PunchySheet = _punchySheet; }
        RefreshModeLabel();
        Debug.Log($"[TermPopupLab] B 연출: {(_punchySheet ? "NEW" : "OLD")}");
    }

    /// <summary>RESET 버튼: 카드를 다시 깐다.</summary>
    public void Respawn()
    {
        if (_spawning) return;
        SpawnAsync(this.GetCancellationTokenOnDestroy()).Forget();
    }

    private async UniTaskVoid SpawnAsync(System.Threading.CancellationToken token)
    {
        _spawning = true;
        try
        {
            _view?.HideImmediately();
            ResetFocus(immediate: true);
            ClearCards();
            var layout = _cardContainer != null ? _cardContainer.GetComponent<LayoutGroup>() : null;
            if (layout != null) layout.enabled = true;

            for (int i = 0; i < _cardData.Length; i++)
            {
                var data = _cardData[i];
                if (data == null) continue;
                var card = Instantiate(_cardPrefab, _cardContainer);
                string markup = i < _descriptions.Length && !string.IsNullOrWhiteSpace(_descriptions[i]) ? _descriptions[i] : data.description;
                string rich = ToRich(markup);
                card.Setup(data, OnCardClicked, rich);
                var text = FindDescription(card, rich);
                TermLinkTapTarget link = null;
                if (text != null)
                {
                    link = text.gameObject.AddComponent<TermLinkTapTarget>();
                    link.Configure(card.gameObject, OnTermTapped, _hitPadding);
                }
                else Debug.LogWarning($"[TermPopupLab] {data.chooseName}: 설명 TMP를 찾지 못해 링크 없음");
                _cards.Add(card);
                _links.Add(link);
            }
            if (_view == null && _links.Count > 0)
            {
                var source = _links.Find(l => l != null);
                if (source != null)
                {
                    _view = TermPopupLabView.Create(_popupCanvas, source.Text, _roundedSprite);
                    _view.PunchySheet = _punchySheet;
                    _view.Closed += () => ResetFocus(immediate: false);
                    _view.MoreRequested += OnMoreRequested;
                    _view.ChipTapped += id => ShowSheet(_activeCard, id);
                }
            }

            // 게임(LevelUpUI)처럼 첫 배치 후 레이아웃을 끈다 — 선택 확대(SetAsLastSibling)가 순서를 바꿔도 자리가 유지된다.
            PlayTierBackdrop();
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, token);
            if (layout != null) layout.enabled = false;
            if (_cardContainer != null) _containerBase = _cardContainer.anchoredPosition;
        }
        catch (OperationCanceledException) { }
        finally { _spawning = false; }
    }

    // LevelUpRevealSequence와 같이 카드 중 가장 높은 등급으로 배경 연출을 튼다.
    private void PlayTierBackdrop()
    {
        if (_tierReveal == null) return;
        var top = Tier.Rare;
        foreach (var data in _cardData)
            if (data != null && data.tier > top) top = data.tier;
        _tierReveal.Play(top);
    }

    private void OnTermTapped(TermLinkTapTarget source, string linkId)
    {
        if (_view == null || !linkId.StartsWith(LinkPrefix, StringComparison.Ordinal)) return;
        string id = linkId.Substring(LinkPrefix.Length);
        int cardIndex = _links.IndexOf(source);
        var term = FindTerm(id);
        if (cardIndex < 0 || term == null) { Debug.LogWarning($"[TermPopupLab] 용어 데이터 없음: {id}"); return; }
        _activeCard = cardIndex;
        _activeTerm = id;
        Focus(cardIndex);

        if (_mode == TermPopupLabMode.Sheet) { ShowSheet(cardIndex, id); return; }
        if (!source.TryGetLinkWorldBounds(linkId, out var min, out var max)) return;
        var corners = new Vector3[4];
        ((RectTransform)_cards[cardIndex].transform).GetWorldCorners(corners);
        _view.ShowBubble(term, min, max, corners[0], corners[2], summary: _mode == TermPopupLabMode.BubbleThenSheet);
    }

    private void OnMoreRequested()
    {
        if (_activeCard >= 0 && _activeTerm != null) ShowSheet(_activeCard, _activeTerm);
    }

    private void ShowSheet(int cardIndex, string id)
    {
        var term = FindTerm(id);
        if (term == null || cardIndex < 0 || cardIndex >= _cards.Count) return;
        _activeTerm = id;
        _view.ShowSheet(term, TermsOfCard(cardIndex));
        ShiftCardAboveSheet(_cards[cardIndex]);
        if (_punchySheet && _cardPunch > 0f)
        {
            var t = _cards[cardIndex].transform;
            t.DOKill(true);
            t.DOPunchScale(Vector3.one * _cardPunch, 0.3f, 6, 0.6f).SetUpdate(true).SetLink(t.gameObject);
        }
    }

    // 시트가 카드를 가리면 카드 묶음을 가린 만큼만 위로 올린다.
    private void ShiftCardAboveSheet(LevelUpCardUI card)
    {
        if (_cardContainer == null || card == null) return;
        var canvas = _popupCanvas;
        var corners = new Vector3[4];
        ((RectTransform)card.transform).GetWorldCorners(corners);
        float currentShift = _cardContainer.anchoredPosition.y - _containerBase.y;
        float cardBottom = canvas.InverseTransformPoint(corners[0]).y - currentShift * CanvasPerContainerUnit();
        float sheetTop = canvas.rect.yMin + _view.SheetHeight + _sheetClearance;
        float shift = Mathf.Max(0f, sheetTop - cardBottom) / CanvasPerContainerUnit();
        _cardContainer.DOKill();
        var move = _cardContainer.DOAnchorPosY(_containerBase.y + shift, _shiftDuration).SetUpdate(true).SetLink(gameObject);
        if (_punchySheet) move.SetEase(Ease.OutBack, _shiftOvershoot); else move.SetEase(Ease.OutCubic);
    }

    // 카드 묶음 부모의 배율이 캔버스와 다를 때 단위를 맞춘다.
    private float CanvasPerContainerUnit()
    {
        var parent = _cardContainer.parent;
        if (parent == null || _popupCanvas == null) return 1f;
        float canvasScale = _popupCanvas.lossyScale.y;
        return canvasScale > 0f ? parent.lossyScale.y / canvasScale : 1f;
    }

    private void Focus(int cardIndex)
    {
        for (int i = 0; i < _cards.Count; i++) SetAlpha(_cards[i], i == cardIndex ? 1f : _dimAlpha);
    }

    private void ResetFocus(bool immediate)
    {
        _activeCard = -1;
        _activeTerm = null;
        foreach (var card in _cards) SetAlpha(card, 1f);
        if (_cardContainer == null) return;
        _cardContainer.DOKill();
        if (immediate) _cardContainer.anchoredPosition = _containerBase;
        else _cardContainer.DOAnchorPosY(_containerBase.y, _shiftDuration).SetEase(Ease.OutCubic).SetUpdate(true).SetLink(gameObject);
    }

    private static void SetAlpha(LevelUpCardUI card, float alpha)
    {
        if (card == null) return;
        var group = card.GetComponent<CanvasGroup>();
        if (group == null) group = card.gameObject.AddComponent<CanvasGroup>();
        group.alpha = alpha;
    }

    // 실험실은 선택 효과를 적용하지 않는다 — 선택이 일어났다는 것만 확대로 보여 준다.
    private void OnCardClicked(LevelUpCardUI card)
    {
        SelectionCount++;
        Debug.Log($"[TermPopupLab] 카드 선택됨: {card.GetData()?.chooseName}");
        card.Select();
        DeselectLaterAsync(card, this.GetCancellationTokenOnDestroy()).Forget();
    }

    private async UniTaskVoid DeselectLaterAsync(LevelUpCardUI card, System.Threading.CancellationToken token)
    {
        try
        {
            await UniTask.Delay(TimeSpan.FromSeconds(_selectPreviewSeconds), DelayType.Realtime, cancellationToken: token);
            if (card != null) card.Deselect();
        }
        catch (OperationCanceledException) { }
    }

    private List<TermPopupLabTerm> TermsOfCard(int cardIndex)
    {
        var result = new List<TermPopupLabTerm>();
        if (cardIndex < 0 || cardIndex >= _descriptions.Length || _descriptions[cardIndex] == null) return result;
        foreach (Match m in TermMarkup.Matches(_descriptions[cardIndex]))
        {
            var term = FindTerm(m.Groups[1].Value);
            if (term != null && !result.Contains(term)) result.Add(term);
        }
        return result;
    }

    private TermPopupLabTerm FindTerm(string id)
    {
        foreach (var term in _terms) if (term != null && term.Id == id) return term;
        return null;
    }

    private string ToRich(string markup)
    {
        if (string.IsNullOrEmpty(markup)) return string.Empty;
        string hex = ColorUtility.ToHtmlStringRGB(_termColor);
        return TermMarkup.Replace(markup, m =>
            $"<link=\"{LinkPrefix}{m.Groups[1].Value}\"><color=#{hex}><u>{m.Groups[1].Value}</u></color></link>");
    }

    // LevelUpCardUI.descriptionText는 private이라, Setup이 넣은 글자와 같은 TMP를 찾는다.
    private static TMP_Text FindDescription(LevelUpCardUI card, string text)
    {
        foreach (var tmp in card.GetComponentsInChildren<TMP_Text>(true))
            if (tmp.text == text) return tmp;
        return null;
    }

    private void ClearCards()
    {
        foreach (var card in _cards)
        {
            if (card == null) continue;
            card.transform.SetParent(null, false);
            Destroy(card.gameObject);
        }
        _cards.Clear();
        _links.Clear();
    }

    private void RefreshModeLabel()
    {
        if (_modeLabel != null) _modeLabel.text = $"MODE {ModeName(_mode)}";
        if (_punchLabel != null) _punchLabel.text = $"B {(_punchySheet ? "NEW" : "OLD")}";
    }

    private static string ModeName(TermPopupLabMode mode) => mode switch
    {
        TermPopupLabMode.Bubble => "A",
        TermPopupLabMode.Sheet => "B",
        _ => "A+B",
    };

    private void OnDestroy()
    {
        if (_cardContainer != null) _cardContainer.DOKill();
    }
}
