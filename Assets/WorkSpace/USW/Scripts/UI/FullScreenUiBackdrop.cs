using UnityEngine;

/// <summary>Keeps a modal backdrop or input shield full-screen underneath a fitted content frame.</summary>
[DefaultExecutionOrder(200)]
[RequireComponent(typeof(RectTransform))]
public sealed class FullScreenUiBackdrop : MonoBehaviour
{
    private RectTransform _rect;
    private RectTransform _parent;
    private Canvas _canvas;
    private void Awake()
    {
        _rect = (RectTransform)transform;
        _parent = transform.parent as RectTransform;
        _canvas = GetComponentInParent<Canvas>();
    }
    private void LateUpdate()
    {
        if (_parent == null || _canvas == null) return;
        var camera = _canvas.rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.rootCanvas.worldCamera;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_parent, Vector2.zero, camera, out var min);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_parent, new Vector2(Screen.width, Screen.height), camera, out var max);
        _rect.anchorMin = _rect.anchorMax = new Vector2(0.5f, 0.5f);
        _rect.pivot = new Vector2(0.5f, 0.5f);
        _rect.sizeDelta = max - min;
        _rect.anchoredPosition = (min + max) * 0.5f - _parent.rect.center;
    }
}
