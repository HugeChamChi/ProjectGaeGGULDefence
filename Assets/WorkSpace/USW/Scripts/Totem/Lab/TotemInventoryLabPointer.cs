using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Touch and mouse pointer bridge for the lab's board and storage targets.</summary>
public sealed class TotemInventoryLabPointer : MonoBehaviour, IPointerClickHandler,
    IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
{
    /// <summary>Owning scene view.</summary>
    public TotemInventoryLab Owner;
    /// <summary>True for a storage slot or its containing drop area.</summary>
    public bool Storage;
    /// <summary>Cell or slot index; minus one denotes the storage drop area.</summary>
    public int Index;
    /// <inheritdoc />
    public void OnPointerClick(PointerEventData e) { if (!e.dragging) Owner.Select(Storage, Index); }
    /// <inheritdoc />
    public void OnBeginDrag(PointerEventData e) => Owner.BeginDrag(Storage, Index, e);
    /// <inheritdoc />
    public void OnDrag(PointerEventData e) => Owner.Drag(e);
    /// <inheritdoc />
    public void OnEndDrag(PointerEventData e) => Owner.EndDrag(e);
    /// <inheritdoc />
    public void OnDrop(PointerEventData e) => Owner.Drop(Storage, Index, e);
}
