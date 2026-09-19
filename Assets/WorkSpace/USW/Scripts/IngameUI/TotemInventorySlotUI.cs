using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>A draggable inventory icon. Pointer ownership remains with this slot until release.</summary>
public sealed class TotemInventorySlotUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    [SerializeField] private Image _icon;
    [SerializeField, Min(1f)] private float _dragThresholdPixels = 20f;
    private TotemInventoryUI _owner;
    private int _index;
    private bool _dragging;
    private int _pointerId;
    /// <summary>Binds one stored item.</summary>
    public void Bind(TotemInventoryUI owner, int index, TotemData data)
    {
        _owner = owner; _index = index;
        _icon.sprite = data != null ? data.DisplaySprite : null;
        _icon.color = Color.white;
    }
    /// <inheritdoc />
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (_dragging || (eventData.position - eventData.pressPosition).sqrMagnitude < _dragThresholdPixels * _dragThresholdPixels) return;
        _dragging = true;
        _pointerId = eventData.pointerId;
        _owner.BeginDrag(_index, _icon.sprite, eventData);
    }
    /// <inheritdoc />
    public void OnDrag(PointerEventData eventData)
    {
        // UGUI may start dragging at its smaller global threshold. Wait for an
        // intentional movement before hiding the inventory.
        if (!_dragging) OnBeginDrag(eventData);
        if (_dragging && eventData.pointerId == _pointerId) _owner.Drag(eventData);
    }
    /// <inheritdoc />
    public void OnEndDrag(PointerEventData eventData)
    {
        if (!_dragging || eventData.pointerId != _pointerId) return;
        _dragging = false;
        _owner.EndDrag(eventData);
    }
    /// <summary>A tap keeps the inventory open and does not activate an ancestor button.</summary>
    public void OnPointerClick(PointerEventData eventData) { }

    private void OnDisable() => _dragging = false;
}
