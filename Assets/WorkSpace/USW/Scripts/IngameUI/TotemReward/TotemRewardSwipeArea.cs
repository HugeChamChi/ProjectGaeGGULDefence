using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 토템 보상 상세 화면의 배경 터치 영역.
/// 탭하면 OnTap(개요로 돌아가기), 가로로 밀면 OnSwipe(+1 = 다음, -1 = 이전).
/// uGUI는 누른 오브젝트와 드래그 오브젝트가 같으면 드래그 뒤에도 클릭을 (EndDrag보다 먼저) 보내므로,
/// 탭 여부는 누른 위치로부터의 이동 거리로 직접 판정한다.
/// </summary>
public class TotemRewardSwipeArea : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    /// <summary>탭으로 인정하는 최대 이동 거리 (Threshold 대비 비율). 손가락 흔들림은 탭으로 본다.</summary>
    private const float TapSlopRatio = 0.25f;

    /// <summary>탭 (거의 움직이지 않고 떼었을 때).</summary>
    public event Action OnTap;
    /// <summary>가로 밀기 방향 (+1 = 왼쪽으로 밀어 다음, -1 = 오른쪽으로 밀어 이전).</summary>
    public event Action<int> OnSwipe;

    /// <summary>밀기 판정 거리 (이 오브젝트가 속한 캔버스 단위).</summary>
    public float Threshold { get; set; } = 120f;
    /// <summary>꺼져 있으면 밀기를 무시한다.</summary>
    public bool SwipeEnabled { get; set; } = true;

    private Canvas _canvas;

    /// <inheritdoc />
    public void OnPointerClick(PointerEventData eventData)
    {
        var delta = (eventData.position - eventData.pressPosition) / CanvasScale();
        if (delta.magnitude > Threshold * TapSlopRatio) return;
        OnTap?.Invoke();
    }

    /// <inheritdoc />
    public void OnBeginDrag(PointerEventData eventData) { }

    /// <inheritdoc />
    public void OnDrag(PointerEventData eventData) { }

    /// <inheritdoc />
    public void OnEndDrag(PointerEventData eventData)
    {
        if (!SwipeEnabled) return;
        var delta = (eventData.position - eventData.pressPosition) / CanvasScale();
        if (Mathf.Abs(delta.x) < Threshold || Mathf.Abs(delta.x) < Mathf.Abs(delta.y)) return;
        OnSwipe?.Invoke(delta.x < 0f ? 1 : -1);
    }

    private float CanvasScale()
    {
        if (_canvas == null) _canvas = GetComponentInParent<Canvas>();
        return _canvas != null ? Mathf.Max(0.0001f, _canvas.rootCanvas.scaleFactor) : 1f;
    }
}
