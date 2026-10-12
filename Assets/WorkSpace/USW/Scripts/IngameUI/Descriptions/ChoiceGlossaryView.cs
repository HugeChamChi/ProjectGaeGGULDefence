using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;

/// <summary>Connects the approved glossary presentation to the production preference and choice owner.</summary>
public sealed class ChoiceGlossaryView : MonoBehaviour
{
    private const float FocusSeconds = 0.25f;
    private const float SheetClearance = 30f;
    private const float CardPunch = 0.05f;
    private const float CardPunchSeconds = 0.3f;
    private readonly DescriptionModeState _mode = new DescriptionModeState();
    private DescriptionDisplaySettings _settings;
    private TermGlossary _glossary;
    private TermPopup _popup;
    private RectTransform _frame;
    private RectTransform _cards;
    private Vector2 _home;
    private LevelUpCardUI _active;
    private TermEntry _entry;
    private bool _hasFocus;
    private Tween _punch;
    private Transform _punchedCard;
    private Vector3 _cardScale;

    /// <summary>Whether this view still owns the closing or opening touch.</summary>
    public bool BlocksOwnerInput => _popup != null && _popup.BlocksOwnerInput;
    /// <summary>Currently visible term video area for scene-owned tutorial pointing.</summary>
    public RectTransform VideoArea => _popup != null && _popup.Film != null &&
        _popup.Film.Rect.gameObject.activeInHierarchy ? _popup.Film.Rect : null;

    /// <summary>Creates a safe-area sheet with an edge-to-edge modal shield.</summary>
    public static ChoiceGlossaryView Create(Transform parent, TMP_Text font, RectTransform cards,
        TermGlossary glossary, Shader blur, DescriptionDisplaySettings settings, TermLinker linker = null)
    {
        var root = new GameObject("ChoiceGlossary", typeof(RectTransform));
        root.transform.SetParent(parent, false);
        var frame = root.AddComponent<SafeAreaContentFrame>();
        frame.UseFullSafeArea();
        var view = root.AddComponent<ChoiceGlossaryView>();
        view._frame = (RectTransform)root.transform;
        view._cards = cards;
        view._glossary = glossary;
        view._settings = settings;
        view._popup = TermPopup.Create(view._frame, font, DescriptionToggleView.TrackSprite, blur);
        view._popup.ConfigureLinks(linker);
        view._mode.Set(settings.Detailed ? DescriptionMode.Detailed : DescriptionMode.Brief);
        view._popup.ConfigureDescriptionMode(view._mode);
        view._mode.OnChanged += view.ChangeMode;
        settings.Changed += view.RefreshMode;
        view._popup.SiblingTapped += view.ShowSibling;
        view._popup.Closed += view.ReturnCards;
        view._popup.OnLayoutChanged += view.ShiftCard;
        return view;
    }

    /// <summary>Shows a pointed term without consuming the selection.</summary>
    public bool Show(LevelUpCardUI card, string id)
    {
        if (!_glossary.TryFind(id, out var entry)) return false;
        bool wasOpen = _popup.IsOpen;
        if (!_hasFocus) _home = _cards.anchoredPosition;
        _hasFocus = true;
        _active = card;
        _entry = entry;
        var siblings = new List<TermEntry>();
        foreach (var termId in card.GetData().DescriptionTerms)
            if (_glossary.TryFind(termId, out var other)) siblings.Add(other);
        _popup.Show(entry, siblings);
        ShiftCard();
        if (!wasOpen)
        {
            RestoreCardScale();
            _punchedCard = card.transform;
            _cardScale = _punchedCard.localScale;
            _punch = _punchedCard.DOPunchScale(Vector3.one * CardPunch, CardPunchSeconds, 6, 0.6f)
                .SetUpdate(true).SetLink(card.gameObject);
        }
        return true;
    }

    private void ShowSibling(string id) { if (_active != null) Show(_active, id); }
    private void ChangeMode(DescriptionMode mode) => _settings.SetDetailed(mode == DescriptionMode.Detailed);
    private void RefreshMode() => _mode.Set(_settings.Detailed ? DescriptionMode.Detailed : DescriptionMode.Brief);
    private void ShiftCard()
    {
        if (_active == null || _entry == null || !_popup.IsOpen) return;
        var corners = new Vector3[4];
        ((RectTransform)_active.transform).GetWorldCorners(corners);
        float units = _cards.parent.lossyScale.y / _frame.lossyScale.y;
        float bottom = _frame.InverseTransformPoint(corners[0]).y - (_cards.anchoredPosition.y - _home.y) * units;
        float shift = _entry.Clip != null ? 0f : Mathf.Max(0f, _frame.rect.yMin + _popup.SheetHeight + SheetClearance - bottom) / units;
        _cards.DOKill();
        _cards.DOAnchorPosY(_home.y + shift, FocusSeconds).SetEase(Ease.OutBack).SetUpdate(true).SetLink(gameObject);
    }
    private void ReturnCards()
    {
        RestoreCardScale();
        _cards.DOKill();
        _cards.DOAnchorPos(_home, FocusSeconds).SetEase(Ease.OutCubic).SetUpdate(true).SetLink(gameObject)
            .OnComplete(() => _hasFocus = false);
        _active = null;
        _entry = null;
    }

    /// <summary>Restores the owner layout immediately before reroll, disable or cancellation.</summary>
    public void HideImmediately()
    {
        RestoreCardScale();
        bool hadFocus = _hasFocus;
        _popup?.HideImmediately();
        if (hadFocus && _cards != null) { _cards.DOKill(); _cards.anchoredPosition = _home; }
        _active = null;
        _entry = null;
        _hasFocus = false;
    }
    private void RestoreCardScale()
    {
        _punch?.Kill();
        _punch = null;
        if (_punchedCard != null) _punchedCard.localScale = _cardScale;
        _punchedCard = null;
    }
    private void OnDisable() => HideImmediately();
    private void OnDestroy()
    {
        if (_settings != null) _settings.Changed -= RefreshMode;
        _mode.OnChanged -= ChangeMode;
    }
}
