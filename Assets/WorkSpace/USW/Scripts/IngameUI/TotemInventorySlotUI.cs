using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>A draggable inventory icon. Pointer ownership remains with this slot until release.</summary>
public sealed class TotemInventorySlotUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private Image _icon;
    private TotemInventoryUI _owner;
    private int _index;
    /// <summary>Binds one stored item.</summary>
    public void Bind(TotemInventoryUI owner, int index, TotemData data)
    {
        _owner = owner; _index = index;
        _icon.sprite = data != null ? data.DisplaySprite : null;
        _icon.color = Color.white;
    }
    /// <inheritdoc />
    public void OnBeginDrag(PointerEventData eventData) => _owner.BeginDrag(_index, _icon.sprite, eventData);
    /// <inheritdoc />
    public void OnDrag(PointerEventData eventData) => _owner.Drag(eventData);
    /// <inheritdoc />
    public void OnEndDrag(PointerEventData eventData) => _owner.EndDrag(eventData);
}
