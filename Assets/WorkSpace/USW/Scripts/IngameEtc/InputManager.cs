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

    private void Awake()
    {
        _mainCamera = Camera.main;
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            if (UnityEngine.EventSystems.EventSystem.current != null && 
                UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            _pointerDownScreenPos = Input.mousePosition;
            _pointerDownPos = _mainCamera.ScreenToWorldPoint(Input.mousePosition);
            _pointerDown = true;
            _isDragging = false;
            
            RaycastHit2D hit = Physics2D.Raycast(_pointerDownPos, Vector2.zero);
            if (hit.collider != null)
            {
                _currentDraggable = hit.collider.GetComponent<IDraggable>();
            }
        }
        else if (Input.GetMouseButton(0) && _pointerDown)
        {
            Vector2 currentPos = _mainCamera.ScreenToWorldPoint(Input.mousePosition);
            
            if (!_isDragging && Vector2.Distance(_pointerDownScreenPos, (Vector2)Input.mousePosition) > _dragThreshold)
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
        else if (Input.GetMouseButtonUp(0) && _pointerDown)
        {
            Vector2 currentPos = _mainCamera.ScreenToWorldPoint(Input.mousePosition);
            
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
}
