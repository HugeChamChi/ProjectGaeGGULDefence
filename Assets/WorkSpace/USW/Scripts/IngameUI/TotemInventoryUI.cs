using System;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VContainer;

/// <summary>Right-aligned inventory with drag-to-cell placement.</summary>
public sealed class TotemInventoryUI : MonoBehaviour
{
    [Inject] private TotemInventory _inventory;
    [Inject] private GridManager _gridManager;
    [SerializeField] private GameObject _panel;
    [SerializeField] private Button _toggleButton;
    [SerializeField] private TotemInventorySlotUI[] _slots;
    [SerializeField] private Image _dragImage;
    [SerializeField] private TMP_Text _countText;
    private Camera _camera;
    private CanvasGroup _panelGroup;
    private int _dragIndex = -1;
    private int _pointerId;
    private bool _placing;
    private GameObject _previewHost;
    private TotemBase _placementPreview;
    private GridCell _hoverCell;
    private readonly System.Collections.Generic.List<RaycastResult> _raycastResults = new System.Collections.Generic.List<RaycastResult>();

    private void Start()
    {
        _camera = Camera.main;
        _panelGroup = _panel.GetComponent<CanvasGroup>();
        if (_panelGroup == null) _panelGroup = _panel.AddComponent<CanvasGroup>();
        _inventory.OnChanged += Refresh;
        _toggleButton.onClick.AddListener(Toggle);
        _panel.SetActive(false);
        _dragImage.gameObject.SetActive(false);
        Refresh();
    }
    private void OnDestroy()
    {
        if (_inventory != null) _inventory.OnChanged -= Refresh;
        if (_toggleButton != null) _toggleButton.onClick.RemoveListener(Toggle);
    }
    private void OnDisable() => CancelDrag();
    private void OnApplicationFocus(bool focused) { if (!focused) CancelDrag(); }
    private void Update()
    {
        if (_dragIndex < 0) return;
        for (int i = 0; i < Input.touchCount; i++)
        {
            var touch = Input.GetTouch(i);
            if (touch.fingerId == _pointerId && touch.phase == TouchPhase.Canceled) CancelDrag();
        }
    }
    private void Toggle()
    {
        if (_placing) return;
        if (_dragIndex >= 0) { CancelDrag(); return; }
        CancelDrag();
        _panel.SetActive(!_panel.activeSelf);
    }
    private void Refresh()
    {
        // Hierarchy order is reversed: oldest item stays rightmost.
        for (int i = 0; i < _slots.Length; i++)
        {
            bool used = i < _inventory.Items.Count;
            _slots[i].gameObject.SetActive(used);
            if (used) _slots[i].Bind(this, i, _inventory.Items[i]);
        }
        if (_countText != null) _countText.text = $"{_inventory.Items.Count}/{_inventory.Capacity}";
    }
    /// <summary>Starts a UI drag without removing the stored item.</summary>
    public void BeginDrag(int index, Sprite sprite, PointerEventData pointer)
    {
        if (_placing || _dragIndex >= 0 || index < 0 || index >= _inventory.Items.Count) return;
        _dragIndex = index; _pointerId = pointer.pointerId;
        // Keep the slot active until EndDrag so UGUI retains the initiating pointer.
        // Hide its panel and remove UI raycasts immediately; the drag ghost is a sibling.
        _panelGroup.alpha = 0f;
        _panelGroup.blocksRaycasts = false;
        _panelGroup.interactable = false;
        _gridManager.ClearTotemRangePreview();
        var data = _inventory.Items[index];
        _previewHost = new GameObject("TotemPlacementPreview");
        _previewHost.transform.SetParent(transform, false);
        _previewHost.SetActive(false);
        if (data.prefab != null)
            _placementPreview = Instantiate(data.prefab, _previewHost.transform).GetComponent<TotemBase>();
        else
            _placementPreview = _previewHost.AddComponent<GenericBuffTotem>();
        _dragImage.sprite = sprite;
        _dragImage.gameObject.SetActive(true);
        Drag(pointer);
    }
    /// <summary>Moves the non-raycast ghost.</summary>
    public void Drag(PointerEventData pointer)
    {
        if (_dragIndex < 0 || pointer.pointerId != _pointerId) return;
        var parent = (RectTransform)_dragImage.transform.parent;
        var canvas = _dragImage.canvas;
        Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, pointer.position, uiCamera, out var point))
            _dragImage.rectTransform.anchoredPosition = point;
        var cell = GetDropCell(pointer);
        if (cell != null && !cell.IsAvailable) cell = null;
        if (cell != _hoverCell)
        {
            _hoverCell = cell;
            if (cell != null && _placementPreview != null)
                _placementPreview.PreviewPlacement(_inventory.Items[_dragIndex], cell, _gridManager);
            else _gridManager.ClearTotemRangePreview();
        }
    }
    /// <summary>Drops only onto an available grid cell.</summary>
    public void EndDrag(PointerEventData pointer)
    {
        if (_dragIndex < 0 || pointer.pointerId != _pointerId) return;
        for (int i = 0; i < Input.touchCount; i++)
            if (Input.GetTouch(i).fingerId == pointer.pointerId && Input.GetTouch(i).phase == TouchPhase.Canceled) { CancelDrag(); return; }
        int index = _dragIndex;
        CancelDrag();
        var target = GetDropCell(pointer);
        if (target == null || !target.IsAvailable) return;
        PlaceAsync(index, target).Forget();
    }

    private GridCell GetDropCell(PointerEventData pointer)
    {
        // UI over the board must not receive a placement through it.
        _raycastResults.Clear();
        EventSystem.current?.RaycastAll(pointer, _raycastResults);
        if (_raycastResults.Count > 0) return null;
        if (_camera == null) _camera = Camera.main;
        if (_camera == null) return null;
        var ray = _camera.ScreenPointToRay(pointer.position);
        if (!new Plane(Vector3.forward, Vector3.zero).Raycast(ray, out var distance)) return null;
        GridCell target = null;
        foreach (var hit in Physics2D.RaycastAll(ray.GetPoint(distance), Vector2.zero))
        {
            var cell = hit.collider.GetComponentInParent<GridCell>();
            if (cell != null) { target = cell; break; }
        }
        return target;
    }
    private async UniTaskVoid PlaceAsync(int index, GridCell cell)
    {
        _placing = true;
        try { await _inventory.TryPlaceAsync(index, cell, this.GetCancellationTokenOnDestroy()); }
        catch (OperationCanceledException) { }
        catch (Exception exception) { Debug.LogException(exception, this); }
        finally { _placing = false; }
    }
    private void CancelDrag()
    {
        if (_dragIndex >= 0)
        {
            _gridManager?.ClearTotemRangePreview();
            _panel.SetActive(false);
            _panelGroup.alpha = 1f;
            _panelGroup.blocksRaycasts = true;
            _panelGroup.interactable = true;
        }
        _dragIndex = -1;
        _hoverCell = null;
        _placementPreview = null;
        if (_previewHost != null) Destroy(_previewHost);
        _previewHost = null;
        if (_dragImage != null) _dragImage.gameObject.SetActive(false);
    }
}
