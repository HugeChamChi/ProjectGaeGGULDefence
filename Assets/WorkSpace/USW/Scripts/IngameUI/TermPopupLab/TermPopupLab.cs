using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 용어 팝업 실험실(FxLab_TermPopup) 드라이버 — 확정 B안(바텀 시트 + 영상 필름)을 게임 시스템 없이 레벨업 카드 3장으로 확인한다.
/// 실제 게임 부품(TermGlossary·TermLinker·TermLinkTapTarget·TermPopup)을 그대로 쓰고, 소유자 몫(가린 카드 밀어 올리기·카드 강조)만 여기서 한다.
/// 게임 레벨업 화면에 붙일 때 이 소유자 몫을 LevelUpUI로 옮긴다. 게임 코드·프리팹은 바꾸지 않는다.
/// 씬 구성은 Tools/USW/Fx/Build Term Popup Lab (TermPopupLabBuilder)이 만든다.
/// </summary>
public sealed class TermPopupLab : MonoBehaviour
{
    [Header("참조 (빌더가 연결)")]
    [SerializeField] private RectTransform _popupCanvas;
    [SerializeField] private GameObject _panelObject;
    [SerializeField] private RectTransform _cardContainer;
    [SerializeField] private LevelUpCardUI _cardPrefab;
    [SerializeField] private TermGlossary _glossary;
    [Tooltip("시트·칩의 둥근 모서리 (비우면 각진 모서리)")]
    [SerializeField] private Sprite _roundedSprite;
    [Tooltip("팝업 배경 스냅숏 블러 셰이더 (Hidden/USW/UI/SnapshotBlur). 비면 어둡게만 한다")]
    [SerializeField] private Shader _blurShader;
    [Tooltip("게임 레벨업 화면의 등급 배경(검은 바탕 + 글로우 + 올라오는 입자). 카드를 깔 때 가장 높은 등급으로 재생한다")]
    [SerializeField] private TierRevealFx _tierReveal;

    [Header("카드 (위에서부터)")]
    [SerializeField] private LevelUpData[] _cardData = new LevelUpData[3];
    [Tooltip("비우면 SO 설명. 용어는 사전 별칭으로 자동 링크된다")]
    [SerializeField, TextArea] private string[] _descriptions = new string[3];

    [Header("링크")]
    [SerializeField] private Color _termColor = new Color(0.5f, 0.83f, 0.93f, 1f);
    [Tooltip("영상이 있는 용어의 글자색 (누르면 영상이 나온다는 표시)")]
    [SerializeField] private Color _videoTermColor = new Color(1f, 0.76f, 0.3f, 1f);
    [Tooltip("링크 터치 판정을 글자보다 넓히는 양 (캔버스 기준 단위)")]
    [SerializeField, Min(0f)] private float _hitPadding = 18f;

    [Header("소유자 몫 (게임에선 LevelUpUI)")]
    [Tooltip("팝업이 열린 동안 다른 카드 투명도 (1 = 따로 흐리게 하지 않음. 지금은 팝업이 시트·필름 뒤 전체를 어둡게 한다)")]
    [SerializeField, Range(0f, 1f)] private float _dimAlpha = 1f;
    [Tooltip("시트가 가린 카드를 밀어 올릴 때 시트 위 여유")]
    [SerializeField, Min(0f)] private float _sheetClearance = 30f;
    [SerializeField, Min(0.01f)] private float _shiftDuration = 0.28f;
    [SerializeField, Min(0f)] private float _shiftOvershoot = 1.2f;
    [Tooltip("시트를 열 때 누른 카드가 살짝 튀는 정도")]
    [SerializeField, Min(0f)] private float _cardPunch = 0.05f;
    [Tooltip("카드 선택이 됐는지 보여 주는 확대 유지 시간 (실험실은 실제 효과를 적용하지 않는다)")]
    [SerializeField, Min(0f)] private float _selectPreviewSeconds = 0.7f;

    private readonly List<LevelUpCardUI> _cards = new List<LevelUpCardUI>();
    private readonly List<TermLinkTapTarget> _links = new List<TermLinkTapTarget>();
    private readonly List<List<string>> _cardTerms = new List<List<string>>();
    private TermLinker _linker;
    private TermPopup _popup;
    private int _activeCard = -1;
    private Vector2 _containerBase;
    private bool _spawning;

    /// <summary>실험실 검증용: 카드 선택 콜백이 불린 횟수.</summary>
    public int SelectionCount { get; private set; }
    /// <summary>실험실 검증용: 팝업(방패)이 열려 있는지.</summary>
    public bool PopupOpen => _popup != null && _popup.IsOpen;
    /// <summary>실험실 검증용: 팝업.</summary>
    public TermPopup Popup => _popup;
    /// <summary>실험실 검증용: 용어 사전.</summary>
    public TermGlossary Glossary => _glossary;
    /// <summary>실험실 검증용: 위에서부터 cardIndex번째 카드의 링크 판정 컴포넌트.</summary>
    public TermLinkTapTarget GetLink(int cardIndex) => cardIndex >= 0 && cardIndex < _links.Count ? _links[cardIndex] : null;
    /// <summary>실험실 검증용: cardIndex번째 카드.</summary>
    public LevelUpCardUI GetCard(int cardIndex) => cardIndex >= 0 && cardIndex < _cards.Count ? _cards[cardIndex] : null;

    private void Start()
    {
        if (_panelObject != null) _panelObject.SetActive(true);
        _linker = new TermLinker(_glossary, _termColor, _videoTermColor);
        Respawn();
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
            _popup?.HideImmediately();
            ResetFocus(immediate: true);
            ClearCards();
            var layout = _cardContainer != null ? _cardContainer.GetComponent<LayoutGroup>() : null;
            if (layout != null) layout.enabled = true;

            for (int i = 0; i < _cardData.Length; i++)
            {
                var data = _cardData[i];
                if (data == null) continue;
                var card = Instantiate(_cardPrefab, _cardContainer);
                string text = i < _descriptions.Length && !string.IsNullOrWhiteSpace(_descriptions[i]) ? _descriptions[i] : data.description;
                string linked = _linker.AddLinks(text);
                card.Setup(data, OnCardClicked, linked);
                var tmp = FindDescription(card, linked);
                TermLinkTapTarget link = null;
                if (tmp != null)
                {
                    link = tmp.gameObject.AddComponent<TermLinkTapTarget>();
                    link.Configure(card.gameObject, OnTermTapped, _hitPadding);
                }
                else Debug.LogWarning($"[TermPopupLab] {data.chooseName}: 설명 TMP를 찾지 못해 링크 없음");
                _cards.Add(card);
                _links.Add(link);
                _cardTerms.Add(TermLinker.LinkedIds(linked));
            }
            if (_popup == null)
            {
                var source = _links.Find(l => l != null);
                if (source != null)
                {
                    _popup = TermPopup.Create(_popupCanvas, source.Text, _roundedSprite,
                        _blurShader != null ? _blurShader : Shader.Find("Hidden/USW/UI/SnapshotBlur"));
                    _popup.Closed += () => ResetFocus(immediate: false);
                    _popup.SiblingTapped += id => ShowTerm(_activeCard, id);
                }
            }

            PlayTierBackdrop();
            // 게임(LevelUpUI)처럼 첫 배치 후 레이아웃을 끈다 — 선택 확대(SetAsLastSibling)가 순서를 바꿔도 자리가 유지된다.
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, token);
            if (layout != null) layout.enabled = false;
            if (_cardContainer != null) _containerBase = _cardContainer.anchoredPosition;
        }
        catch (OperationCanceledException) { }
        finally { _spawning = false; }
    }

    private void OnTermTapped(TermLinkTapTarget source, string linkId)
    {
        if (_popup == null || !linkId.StartsWith(TermLinker.LinkPrefix, StringComparison.Ordinal)) return;
        int cardIndex = _links.IndexOf(source);
        if (cardIndex < 0) return;
        ShowTerm(cardIndex, linkId.Substring(TermLinker.LinkPrefix.Length));
    }

    private void ShowTerm(int cardIndex, string id)
    {
        if (cardIndex < 0 || cardIndex >= _cards.Count || _glossary == null || !_glossary.TryFind(id, out var entry))
        {
            Debug.LogWarning($"[TermPopupLab] 용어 없음: {id}");
            return;
        }
        bool wasOpen = _popup.IsOpen;
        _activeCard = cardIndex;
        Focus(cardIndex);
        _popup.Show(entry, Siblings(cardIndex));
        // 영상 용어는 영상+시트가 카드 위를 덮는다 → 밀어 올리지 않는다. 글 용어는 시트가 가린 카드만 올린다.
        if (entry.Clip != null) ReturnCards();
        else ShiftCardAboveSheet(_cards[cardIndex]);
        if (!wasOpen && _cardPunch > 0f)
        {
            var t = _cards[cardIndex].transform;
            t.DOKill(true);
            t.DOPunchScale(Vector3.one * _cardPunch, 0.3f, 6, 0.6f).SetUpdate(true).SetLink(t.gameObject);
        }
    }

    private List<TermEntry> Siblings(int cardIndex)
    {
        var result = new List<TermEntry>();
        if (cardIndex < 0 || cardIndex >= _cardTerms.Count) return result;
        foreach (string id in _cardTerms[cardIndex])
            if (_glossary.TryFind(id, out var entry)) result.Add(entry);
        return result;
    }

    // 시트가 카드를 가리면 카드 묶음을 가린 만큼만 위로 올린다.
    private void ShiftCardAboveSheet(LevelUpCardUI card)
    {
        if (_cardContainer == null || card == null) return;
        var corners = new Vector3[4];
        ((RectTransform)card.transform).GetWorldCorners(corners);
        float currentShift = _cardContainer.anchoredPosition.y - _containerBase.y;
        float cardBottom = _popupCanvas.InverseTransformPoint(corners[0]).y - currentShift * CanvasPerContainerUnit();
        float sheetTop = _popupCanvas.rect.yMin + _popup.SheetHeight + _sheetClearance;
        float shift = Mathf.Max(0f, sheetTop - cardBottom) / CanvasPerContainerUnit();
        _cardContainer.DOKill();
        _cardContainer.DOAnchorPosY(_containerBase.y + shift, _shiftDuration).SetEase(Ease.OutBack, _shiftOvershoot)
                      .SetUpdate(true).SetLink(gameObject);
    }

    // 밀어 올렸던 카드 묶음을 제자리로.
    private void ReturnCards()
    {
        if (_cardContainer == null) return;
        _cardContainer.DOKill();
        _cardContainer.DOAnchorPosY(_containerBase.y, _shiftDuration).SetEase(Ease.OutCubic).SetUpdate(true).SetLink(gameObject);
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
        foreach (var card in _cards) SetAlpha(card, 1f);
        if (_cardContainer == null) return;
        _cardContainer.DOKill();
        if (immediate) _cardContainer.anchoredPosition = _containerBase;
        else ReturnCards();
    }

    private static void SetAlpha(LevelUpCardUI card, float alpha)
    {
        if (card == null) return;
        var group = card.GetComponent<CanvasGroup>();
        if (group == null) group = card.gameObject.AddComponent<CanvasGroup>();
        group.alpha = alpha;
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
        _cardTerms.Clear();
    }

    private void OnDestroy()
    {
        if (_cardContainer != null) _cardContainer.DOKill();
    }
}
