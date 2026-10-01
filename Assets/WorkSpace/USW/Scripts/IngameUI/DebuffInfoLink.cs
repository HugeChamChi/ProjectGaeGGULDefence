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
    private int _pointer = int.MinValue;
    private int _pressedLink = -1;
    private Vector2 _down;
    private bool _dragged;
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
    public void Clear() { _popup?.HideImmediately(); _pointer = int.MinValue; _pressedLink = -1; }
    /// <summary>Captures the one pointer and term that began this tap.</summary>
    public void OnPointerDown(PointerEventData data)
    {
        if (_pointer != int.MinValue || Input.touchCount > 1 || BlocksOwnerInput) { _dragged = true; return; }
        _pointer = data.pointerId; _down = data.position; _dragged = false;
        _pressedLink = FindLink(data);
    }
    /// <summary>Tracks movement even when an ancestor ScrollRect handled dragging.</summary>
    public void OnPointerUp(PointerEventData data)
    {
        if (_pointer != data.pointerId) return;
        if (Vector2.Distance(_down, data.position) > DragThreshold || data.dragging) _dragged = true;
    }
    /// <summary>Opening is guarded by the original pointer, drag distance and link identity.</summary>
    public void OnPointerClick(PointerEventData data)
    {
        if (_pointer != data.pointerId) return;
        _pointer = int.MinValue;
        if (_dragged || data.dragging || Input.touchCount > 1 || BlocksOwnerInput || _pressedLink < 0 ||
            Vector2.Distance(_down, data.position) > DragThreshold || _pressedLink != FindLink(data)) return;
        string key = _text.textInfo.linkInfo[_pressedLink].GetLinkID();
        if (!key.StartsWith("debuff:") || !int.TryParse(key.Substring(7), out int id) ||
            _presenter == null || !_presenter.TryBuild(id, _binding, out var model)) return;
        if (_popup == null) _popup = DebuffInfoPopup.Create(_owner, _text);
        _popup.Show(model);
        data.Use();
    }
    /// <summary>Scroll gestures are not taps.</summary>
    public void OnBeginDrag(PointerEventData data) { _dragged = true; }
    private float DragThreshold => EventSystem.current != null ? EventSystem.current.pixelDragThreshold : 10f;
    private int FindLink(PointerEventData data) => TMP_TextUtilities.FindIntersectingLink(_text, data.position, data.pressEventCamera);
    private void LateUpdate()
    {
        if (Input.touchCount > 1) _dragged = true;
        if (_ownerCanvases != null)
            foreach (var canvas in _ownerCanvases) if (canvas != null && !canvas.enabled) { Clear(); break; }
        // A scroll cancels OnPointerClick; release pointer ownership after the whole gesture.
        if (_pointer != int.MinValue && Input.touchCount == 0 && !Input.GetMouseButton(0)) _pointer = int.MinValue;
    }
    private void OnDisable() => Clear();
    private void OnDestroy() { if (_popup != null) Destroy(_popup.gameObject); }
}
