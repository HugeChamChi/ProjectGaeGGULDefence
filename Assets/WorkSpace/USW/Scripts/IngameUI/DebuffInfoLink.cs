using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Touch-only TMP term links. Drag and multiple-pointer gestures never open details.</summary>
[RequireComponent(typeof(TMP_Text))]
public sealed class DebuffInfoLink : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler, IBeginDragHandler
{
    private TMP_Text _text;
    private UI_Base _owner;
    private DebuffInfoPresenter _presenter;
    private DebuffBinding _binding;
    private DebuffInfoPopup _popup;
    private Canvas[] _ownerCanvases;
    private readonly DescriptionLinkInteraction _interaction = new DescriptionLinkInteraction();
    private string _sourceText;
    /// <summary>True while the explanation or its release shield is open.</summary>
    public bool BlocksOwnerInput => _popup != null && _popup.BlocksOwnerInput;

    /// <summary>Explicit dependency supply from the owning information panel.</summary>
    public void Configure(UI_Base owner, DebuffInfoPresenter presenter)
    {
        _text = GetComponent<TMP_Text>();
        _text.raycastTarget = true;
        _owner = owner; _presenter = presenter;
        _ownerCanvases = owner.GetComponentsInParent<Canvas>(true);
    }
    /// <summary>Resets details when the information target changes, and links only its known debuff.</summary>
    public string SetDescription(string text, DebuffBinding binding)
    {
        if (_sourceText != text || _binding.DebuffId != binding.DebuffId ||
            _binding.Trigger != binding.Trigger || _binding.StacksPerApply != binding.StacksPerApply) Clear();
        _sourceText = text; _binding = binding;
        return _presenter != null ? _presenter.AddLinks(text, binding) : text;
    }
    /// <summary>Closes any owned popup without changing gameplay pause ownership.</summary>
    public void Clear() { _popup?.HideImmediately(); _interaction.Reset(); }
    /// <summary>Captures the one pointer and term that began this tap.</summary>
    public void OnPointerDown(PointerEventData data)
    {
        if (BlocksOwnerInput) return;
        _interaction.TryBegin(_text, data);
    }
    public void OnPointerUp(PointerEventData data) => _interaction.Observe(data);
    public void OnPointerClick(PointerEventData data)
    {
        if (BlocksOwnerInput || !_interaction.TryRelease(data, float.PositiveInfinity, out string key)) return;
        var resolver = new DescriptionTermResolver(null, _presenter, _binding);
        if (!resolver.TryResolve(key, out var term)) return;
        if (_popup == null) _popup = DebuffInfoPopup.Create(_owner, _text);
        _popup.Show(term.DisplayName, term.Body);
        data.Use();
    }
    public void OnBeginDrag(PointerEventData data) => _interaction.Cancel();
    private void LateUpdate()
    {
        _interaction.Tick();
        if (_ownerCanvases != null)
            foreach (var canvas in _ownerCanvases) if (canvas != null && !canvas.enabled) { Clear(); break; }
        // A scroll cancels OnPointerClick; release pointer ownership after the whole gesture.
        if (_interaction.HasPress && Input.touchCount == 0 && !Input.GetMouseButton(0)) _interaction.Reset();
    }
    private void OnDisable() => Clear();
    private void OnDestroy() { if (_popup != null) Destroy(_popup.gameObject); }
}
