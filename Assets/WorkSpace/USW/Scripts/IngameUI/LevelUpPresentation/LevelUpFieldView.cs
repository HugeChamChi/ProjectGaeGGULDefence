using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Choice footer toggle and lower HUD presentation; LevelUpUI retains pause ownership.</summary>
public sealed class LevelUpFieldView : MonoBehaviour
{
    [SerializeField] private LevelUpUI _choices;
    [SerializeField] private CanvasGroup _panel;
    [SerializeField] private GameObject _controls;
    [SerializeField] private GameObject _shield;
    [SerializeField] private Button _button;
    [SerializeField] private TMP_Text _label;
    [SerializeField] private FieldViewEye _eye;
    [SerializeField] private UI_Peekthrough _peek;
    [SerializeField] private IngameHudMotion _hudMotion;
    private bool _open, _acceptInput, _transitioning;
    private LevelUpCardUI[] _cards = System.Array.Empty<LevelUpCardUI>();

    /// <summary>True while choices are hidden by the explicit footer button.</summary>
    public bool ViewingField { get; private set; }
    /// <summary>Hidden choices and the return gesture cannot select cards or advance their timer.</summary>
    public bool BlocksSelection => _open && (ViewingField || _transitioning);

    /// <summary>Starts or refreshes one choice presentation, including rerolls and chained level-ups.</summary>
    public void BeginSession()
    {
        ResetFieldView();
        _open = true;
        _acceptInput = true;
        _cards = _choices.ChoiceArea.GetComponentsInChildren<LevelUpCardUI>();
        _controls.SetActive(true);
        RefreshLabel();
        _hudMotion?.Acquire(this);
    }

    /// <summary>Locks the footer as soon as a card's selection animation starts.</summary>
    public void StopInteraction()
    {
        _acceptInput = false;
        _button.interactable = false;
    }

    /// <summary>Restores the captured HUD state on completion, cancellation or owner destruction.</summary>
    public void EndSession(bool animate)
    {
        ResetFieldView();
        _open = false;
        _acceptInput = false;
        if (_controls != null) _controls.SetActive(false);
        _hudMotion?.Release(this, animate);
    }

    /// <summary>Fades choices out or back in without rerolling, acquiring a card or releasing pause.</summary>
    public void Toggle()
    {
        if (!CanToggle()) return;
        foreach (var card in _cards)
            if (card != null) card.CancelDescriptionPress();
        if (!ViewingField)
        {
            if (_peek == null || !_peek.TryBeginPeek(this)) return;
            ViewingField = true;
        }
        else
        {
            _peek.EndPeek(this);
            ViewingField = false;
        }
        _transitioning = true;
        _button.interactable = false;
        _shield.SetActive(true);
        RefreshLabel();
    }

    private bool CanToggle() => _open && _acceptInput && !_transitioning && _choices != null &&
        _choices.IsReadyForSelection && !_choices.DescriptionBlocksInput &&
        (ViewingField || !_choices.FieldPreviewBlocksInput) && !HasCardPress();

    private bool HasCardPress()
    {
        foreach (var card in _cards)
            if (card != null && (card.HasActivePress || card.TutorialPreviewOnly)) return true;
        return false;
    }

    private void LateUpdate()
    {
        if (!_open) return;
        // Focus loss or disabling the shared preview cancels the toggle too.
        if (ViewingField && !_peek.OwnsPeek(this))
        {
            ViewingField = false;
            _transitioning = true;
            RefreshLabel();
        }
        if (_transitioning && _peek.IsSettled && (ViewingField || !_peek.BlocksSelection))
        {
            foreach (var card in _cards) if (card != null) card.CancelDescriptionPress();
            if (!ViewingField) _shield.SetActive(false);
            _transitioning = false;
        }
        _button.interactable = CanToggle();
    }

    private void ResetFieldView()
    {
        _peek?.CancelPeek(this);
        ViewingField = false;
        _transitioning = false;
        if (_shield != null) _shield.SetActive(false);
        if (_button != null) _button.interactable = false;
        if (_label != null) RefreshLabel();
    }

    private void RefreshLabel()
    {
        const float IconWidth = 42f;
        const float Gap = 18f;
        _label.textWrappingMode = TextWrappingModes.NoWrap;
        _label.text = ViewingField ? "선택지로 돌아가기" : "필드 보기";
        float width = _label.GetPreferredValues(_label.text).x;
        _label.rectTransform.sizeDelta = new Vector2(width + 8f, 86f);
        _label.rectTransform.anchoredPosition = new Vector2((IconWidth + Gap) * 0.5f, 2f);
        if (_eye != null) _eye.rectTransform.anchoredPosition = new Vector2(-(width + Gap) * 0.5f, 2f);
    }

    private void OnDisable() => EndSession(false);
    private void OnDestroy() => EndSession(false);
}
