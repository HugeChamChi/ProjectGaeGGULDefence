using System;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VContainer;

/// <summary>Bottom-docked inventory drawer with drag-to-cell placement.</summary>
public sealed class TotemInventoryUI : MonoBehaviour
{
    [Inject] private TotemInventory _inventory;
    [Inject] private GridManager _gridManager;
    [SerializeField] private GameObject _panel;
    [SerializeField] private Button _toggleButton;
    [SerializeField] private TotemInventorySlotUI[] _slots;
    [SerializeField] private Image _dragImage;
    [SerializeField] private TMP_Text _countText;
    [SerializeField] private Button _backgroundCloseButton;
    [SerializeField] private GaeGGUL.Animation.Anim_InOutBase _panelAnim;
    [SerializeField] private Button _closeButton;
    [SerializeField, Min(0f)] private float _edgePadding = 12f;
    [Header("배경 (화면 바닥까지 채움)")]
    [Tooltip("_panel과 분리된 배경 이미지. 지정하면 실제 화면 바닥까지(Safe Area 무시) 늘어나고, 아이콘/닫기 버튼은 _panel(Safe Area) 기준을 그대로 따른다.")]
    [SerializeField] private RectTransform _background;
    [Tooltip("배경 아래쪽 테두리 그림이 화면 밖으로 완전히 가려지도록 실제 화면 바닥보다 더 내려 그리는 양.")]
    [SerializeField, Min(0f)] private float _backgroundBottomBleed = 40f;
    private Rect _lastSafeArea;
    private Vector2Int _lastScreenSize;
    private Camera _camera;
    private CanvasGroup _panelGroup;
    private int _dragIndex = -1;
    private int _pointerId;
    private bool _placing;
    private bool _closing;
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
        if (_closeButton != null) _closeButton.onClick.AddListener(CloseInventory);
        if (_backgroundCloseButton != null)
        {
            // Decorative drawer space must reach the backdrop's close handler.
            // Slot graphics retain their own raycasts and consume taps/drags.
            var panelImage = _panel.GetComponent<Image>();
            if (panelImage != null) panelImage.raycastTarget = false;
            if (_background != null && _background.TryGetComponent<Graphic>(out var backgroundGraphic))
                backgroundGraphic.raycastTarget = false;
            _backgroundCloseButton.gameObject.SetActive(false);
            _backgroundCloseButton.onClick.AddListener(CloseInventory);
        }
        _panel.SetActive(false);
        _dragImage.gameObject.SetActive(false);
        Refresh();
    }
    private void OnDestroy()
    {
        if (_inventory != null) _inventory.OnChanged -= Refresh;
        if (_toggleButton != null) _toggleButton.onClick.RemoveListener(Toggle);
        if (_closeButton != null) _closeButton.onClick.RemoveListener(CloseInventory);
        if (_backgroundCloseButton != null) _backgroundCloseButton.onClick.RemoveListener(CloseInventory);
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
        if (_placing || _closing) return;
        if (_dragIndex >= 0) { CancelDrag(); return; }
        CancelDrag();
        if (_panel.activeSelf) CloseInventory();
        else OpenInventory();
    }
    private void OpenInventory()
    {
        _panel.SetActive(true);
        PositionAtBottom();
        if (_backgroundCloseButton != null) _backgroundCloseButton.gameObject.SetActive(true);
        if (_panelAnim != null) _panelAnim.PlayIn().Forget();
    }
    private void LateUpdate()
    {
        if (_panel != null && _panel.activeSelf && !_closing && _dragIndex < 0 &&
            (_lastSafeArea != Screen.safeArea || _lastScreenSize != new Vector2Int(Screen.width, Screen.height)))
            PositionAtBottom();
    }
    private void PositionAtBottom()
    {
        _panelAnim?.Stop();
        Canvas.ForceUpdateCanvases();
        var panelRect = (RectTransform)_panel.transform;
        var parent = (RectTransform)panelRect.parent;
        var canvas = _panel.GetComponentInParent<Canvas>().rootCanvas;
        var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        var safe = Screen.safeArea;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, safe.min, camera, out var bottomLeft);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, safe.max, camera, out var topRight);
        panelRect.anchorMin = panelRect.anchorMax = Vector2.zero;
        panelRect.pivot = Vector2.zero;
        panelRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, topRight.x - bottomLeft.x - 2f * _edgePadding);
        panelRect.anchoredPosition = bottomLeft - parent.rect.min + Vector2.one * _edgePadding;
        PositionBackground(panelRect, camera);
        _lastSafeArea = safe;
        _lastScreenSize = new Vector2Int(Screen.width, Screen.height);
        _panelAnim?.Initialize(panelRect);
    }

    /// <summary>배경만 실제 화면 바닥(Safe Area 무시)까지 채우고, 테두리 두께만큼 화면 밖으로 더 내려서 잘리게 한다.
    /// _panel(콘텐츠: 아이콘/닫기 버튼)의 크기·위치는 절대 건드리지 않는다. _background는 _panel의 자식이라
    /// 슬라이드 애니메이션에도 함께 따라 움직인다.</summary>
    private void PositionBackground(RectTransform panelRect, Camera camera)
    {
        if (_background == null) return;
        if (_background.parent != panelRect) _background.SetParent(panelRect, false);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(panelRect, new Vector2(0f, 0f), camera, out var screenBottomLeft);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(panelRect, new Vector2(Screen.width, 0f), camera, out var screenBottomRight);
        float bottomY = screenBottomLeft.y - _backgroundBottomBleed; // 실제 화면 바닥보다 더 아래로 내려서 테두리를 가림
        float topY = panelRect.rect.yMax; // 위쪽은 콘텐츠(_panel) 상단과 그대로 맞춤 — 절대 줄이지 않고 아래로만 늘림
        _background.anchorMin = _background.anchorMax = Vector2.zero;
        _background.pivot = Vector2.zero;
        _background.anchoredPosition = new Vector2(screenBottomLeft.x, bottomY);
        _background.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, screenBottomRight.x - screenBottomLeft.x);
        _background.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(0f, topY - bottomY));
        _background.SetAsFirstSibling(); // 콘텐츠(슬롯/닫기 버튼)보다 뒤에 그려지도록
    }
    private void CloseInventory()
    {
        if (_closing || !_panel.activeSelf) return;
        CloseAnimatedAsync().Forget();
    }
    private async UniTaskVoid CloseAnimatedAsync()
    {
        _closing = true;
        try
        {
            if (_backgroundCloseButton != null) _backgroundCloseButton.gameObject.SetActive(false);
            if (_panelAnim != null) await _panelAnim.PlayOut();
            _panel.SetActive(false);
        }
        finally { _closing = false; }
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
        // Backdrop is a sibling, not under _panelGroup, so it needs its own raycast pass-through.
        if (_backgroundCloseButton != null) _backgroundCloseButton.gameObject.SetActive(false);
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
            if (_backgroundCloseButton != null) _backgroundCloseButton.gameObject.SetActive(false);
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
