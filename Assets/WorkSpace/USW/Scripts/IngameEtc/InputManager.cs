using UnityEngine;
using VContainer;
using System;
using System.Collections.Generic;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public interface IDraggable
{
    void OnPointerClick();
    void OnBeginDrag();
    void OnDrag(Vector2 worldPosition);
    void OnEndDrag(Vector2 worldPosition);
}

public class InputManager : MonoBehaviour
{
    [Inject] private GameManager _gameManager;
    /// <summary>Optional scene-owned restriction for guided world interactions.</summary>
    public Func<IDraggable, bool> CanBeginInteraction { get; set; }
    /// <summary>Optional restriction evaluated before a world drag commits.</summary>
    public Func<IDraggable, Vector2, bool> CanEndInteraction { get; set; }
    /// <summary>Whether world clicks may open object information.</summary>
    public bool AllowPointerClicks { get; set; } = true;
    private Camera _mainCamera;
    private IDraggable _currentDraggable;
    private bool _isDragging = false;
    private const float DragThresholdPixels = 20f;
    private const int MousePointerId = -1;
    private int? _activeFingerId;
    private EventSystem _pointerEventSystem;
    private PointerEventData _uiPointer;
    private readonly List<RaycastResult> _uiHits = new List<RaycastResult>();
    private Vector2 _pointerDownPos;
    private Vector2 _pointerDownScreenPos;
    private bool _pointerDown = false;

    private static readonly Plane GroundPlane = new Plane(Vector3.forward, Vector3.zero);

    private Vector2 GetWorldPos(Vector2 screenPos)
    {
        if (_mainCamera == null) return Vector2.zero;
        Ray ray = _mainCamera.ScreenPointToRay(screenPos);
        if (GroundPlane.Raycast(ray, out float distance))
        {
            return ray.GetPoint(distance);
        }
        return Vector2.zero;
    }

    private void Awake()
    {
        _mainCamera = Camera.main;
    }

    private void Update()
    {
        if (_gameManager?.IsFinished == true) { if (_pointerDown) CancelPointer(); return; }
        if (_mainCamera == null)
        {
            _mainCamera = Camera.main;
            if (_mainCamera == null) { CancelPointer(); return; }
        }

        // 소유 손가락은 배열 순서와 무관하게 종료/취소까지 같은 ID로 추적한다.
        if (_activeFingerId.HasValue)
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                var touch = Input.GetTouch(i);
                if (touch.fingerId != _activeFingerId.Value) continue;
                switch (touch.phase)
                {
                    case TouchPhase.Moved:
                    case TouchPhase.Stationary:
                        ProcessPointerMove(touch.position);
                        break;
                    case TouchPhase.Ended:
                        ProcessPointerUp(touch.position);
                        break;
                    case TouchPhase.Canceled:
                        CancelPointer();
                        break;
                }
                return;
            }
            // Ended가 전달되지 않고 손가락이 사라져도 다음 탭을 막지 않는다.
            CancelPointer();
            return;
        }

        if (Input.touchCount > 0)
        {
            if (_pointerDown) CancelPointer(); // 마우스 폴백에서 터치로 전환.
            for (int i = 0; i < Input.touchCount; i++)
            {
                var touch = Input.GetTouch(i);
                if (touch.phase != TouchPhase.Began || IsOverUI(touch.position, touch.fingerId)) continue;
                ProcessPointerDown(touch.position);
                if (!_pointerDown) continue;
                _activeFingerId = touch.fingerId;
                break; // 월드 드래그는 한 번에 하나만 소유한다.
            }
            return;
        }

        // 2. 에디터 / PC 마우스 입력 처리 (폴백)
        if (Input.GetMouseButtonDown(0))
        {
            if (IsOverUI(Input.mousePosition, MousePointerId))
            {
                return;
            }

            ProcessPointerDown(Input.mousePosition);
        }
        else if (Input.GetMouseButton(0) && _pointerDown)
        {
            ProcessPointerMove(Input.mousePosition);
        }
        else if (Input.GetMouseButtonUp(0) && _pointerDown)
        {
            ProcessPointerUp(Input.mousePosition);
        }
    }

    private bool IsOverUI(Vector2 screenPosition, int pointerId)
    {
        var eventSystem = EventSystem.current;
        if (eventSystem == null) return false;
        if (_pointerEventSystem != eventSystem)
        {
            _pointerEventSystem = eventSystem;
            _uiPointer = new PointerEventData(eventSystem);
        }
        _uiPointer.Reset();
        _uiPointer.position = screenPosition;
        _uiPointer.pointerId = pointerId;
        _uiHits.Clear();
        eventSystem.RaycastAll(_uiPointer, _uiHits);
        foreach (var hit in _uiHits)
            if (hit.module is GraphicRaycaster) return true;
        return false;
    }

    private void ProcessPointerDown(Vector2 screenPos)
    {
        _pointerDownScreenPos = screenPos;
        _pointerDownPos = GetWorldPos(screenPos);
        _pointerDown = true;
        _isDragging = false;
        _currentDraggable = null;

        // Totems may have been spawned or moved since the last physics step.
        Physics2D.SyncTransforms();
        RaycastHit2D[] hits = Physics2D.RaycastAll(_pointerDownPos, Vector2.zero);
        _currentDraggable = PickDraggable(hits, _pointerDownPos);
        if (_currentDraggable == null)
        {
            _pointerDown = false;
            return;
        }
        if (_currentDraggable != null && CanBeginInteraction != null && !CanBeginInteraction(_currentDraggable))
        {
            _currentDraggable = null;
            _pointerDown = false;
            return;
        }
        if (_currentDraggable is DragHandler handler) handler.BeginPress();
    }

    // 유닛 콜라이더는 위 칸까지, 토템 콜라이더는 옆 칸까지 겹칠 수 있고 RaycastAll의 겹침 순서는 보장되지 않는다.
    // 손가락 아래 칸(GridCell)을 차지한 오브젝트를 우선하고, 없으면 콜라이더 중심이 가장 가까운 것을 고른다.
    private static IDraggable PickDraggable(RaycastHit2D[] hits, Vector2 worldPos)
    {
        GridCell pointerCell = null;
        foreach (var h in hits)
        {
            if (h.collider == null) continue;
            pointerCell = h.collider.GetComponent<GridCell>();
            if (pointerCell != null) break;
        }

        IDraggable nearest = null;
        float nearestSqr = float.MaxValue;
        foreach (var h in hits)
        {
            if (h.collider == null) continue;
            var draggable = h.collider.GetComponent<IDraggable>();
            if (draggable == null) continue;
            if (pointerCell != null && h.collider.GetComponentInParent<GridCell>() == pointerCell) return draggable;
            float sqr = ((Vector2)h.collider.bounds.center - worldPos).sqrMagnitude;
            if (sqr < nearestSqr) { nearestSqr = sqr; nearest = draggable; }
        }
        return nearest;
    }

    private void ProcessPointerMove(Vector2 screenPos)
    {
        Vector2 currentPos = GetWorldPos(screenPos);

        if (!_isDragging && Vector2.Distance(_pointerDownScreenPos, screenPos) > DragThresholdPixels)
        {
            _isDragging = true;
            if (_currentDraggable != null)
            {
                _currentDraggable.OnBeginDrag();
            }
        }

        if (_isDragging && _currentDraggable != null)
        {
            _currentDraggable.OnDrag(currentPos);
        }
    }

    private void ProcessPointerUp(Vector2 screenPos)
    {

        Vector2 currentPos = GetWorldPos(screenPos);

        if (_isDragging)
        {
            if (_currentDraggable != null && CanEndInteraction != null && !CanEndInteraction(_currentDraggable, currentPos))
            {
                CancelPointer();
                return;
            }
            if (_currentDraggable != null)
            {
                _currentDraggable.OnEndDrag(currentPos);
            }
        }
        else
        {
            if (_currentDraggable != null)
            {
                if (AllowPointerClicks) _currentDraggable.OnPointerClick();
            }
        }

        if (_currentDraggable is DragHandler released && released != null) released.EndPress();
        _pointerDown = false;
        _isDragging = false;
        _currentDraggable = null;
        _activeFingerId = null;
    }

    private void OnDisable() => CancelPointer();
    private void OnApplicationFocus(bool focused) { if (!focused) CancelPointer(); }
    private void OnApplicationPause(bool paused) { if (paused) CancelPointer(); }
    private void CancelPointer()
    {
        var handler = _currentDraggable as DragHandler;
        bool wasDragging = _isDragging;
        _pointerDown = false; _isDragging = false; _currentDraggable = null;
        _activeFingerId = null;
        if (handler == null) return;
        if (wasDragging) handler.CancelPointerDrag();
        else handler.EndPress();
    }
}
