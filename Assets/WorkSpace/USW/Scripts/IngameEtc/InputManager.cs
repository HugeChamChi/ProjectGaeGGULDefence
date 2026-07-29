using UnityEngine;
using VContainer;
using System;

public interface IDraggable
{
    void OnPointerClick();
    void OnBeginDrag();
    void OnDrag(Vector2 worldPosition);
    void OnEndDrag(Vector2 worldPosition);
}

public class InputManager : MonoBehaviour
{
    private Camera _mainCamera;
    private IDraggable _currentDraggable;
    private bool _isDragging = false;
    private float _dragThreshold = 20f;
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
        if (_mainCamera == null)
        {
            _mainCamera = Camera.main;
            if (_mainCamera == null) return;
        }

        // 1. 모바일 터치 입력 처리
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);

            if (touch.phase == TouchPhase.Began)
            {
                if (UnityEngine.EventSystems.EventSystem.current != null &&
                    UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject(touch.fingerId))
                {
                    return;
                }

                ProcessPointerDown(touch.position);
            }
            else if (touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary)
            {
                if (_pointerDown)
                {
                    ProcessPointerMove(touch.position);
                }
            }
            else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
            {
                if (_pointerDown)
                {
                    ProcessPointerUp(touch.position);
                }
            }
            return;
        }

        // 2. 에디터 / PC 마우스 입력 처리 (폴백)
        if (Input.GetMouseButtonDown(0))
        {
            if (UnityEngine.EventSystems.EventSystem.current != null && 
                UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
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

    private void ProcessPointerDown(Vector2 screenPos)
    {
        _pointerDownScreenPos = screenPos;
        _pointerDownPos = GetWorldPos(screenPos);
        _pointerDown = true;
        _isDragging = false;
        _currentDraggable = null;

        RaycastHit2D[] hits = Physics2D.RaycastAll(_pointerDownPos, Vector2.zero);
        foreach (var h in hits)
        {
            if (h.collider != null)
            {
                var draggable = h.collider.GetComponent<IDraggable>();
                if (draggable != null)
                {
                    _currentDraggable = draggable;
                    break;
                }
            }
        }
    }

    private void ProcessPointerMove(Vector2 screenPos)
    {
        Vector2 currentPos = GetWorldPos(screenPos);

        if (!_isDragging && Vector2.Distance(_pointerDownScreenPos, screenPos) > _dragThreshold)
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
            if (_currentDraggable != null)
            {
                _currentDraggable.OnEndDrag(currentPos);
            }
        }
        else
        {
            if (_currentDraggable != null)
            {
                _currentDraggable.OnPointerClick();
            }
        }

        _pointerDown = false;
        _isDragging = false;
        _currentDraggable = null;
    }
}
