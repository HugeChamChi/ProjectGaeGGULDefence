using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 토템 보상 상세 화면의 배경 터치 영역.
/// 탭하면 OnTap(개요로 돌아가기), 가로로 밀면 OnSwipe(+1 = 다음, -1 = 이전).
/// 드래그가 시작되면 EventSystem이 클릭 자격을 없애므로 밀기와 탭이 겹치지 않는다.
/// </summary>
public class TotemRewardSwipeArea : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    /// <summary>탭 (드래그 없이 떼었을 때).</summary>
    public event Action OnTap;
    /// <summary>가로 밀기 방향 (+1 = 왼쪽으로 밀어 다음, -1 = 오른쪽으로 밀어 이전).</summary>
    public event Action<int> OnSwipe;

    /// <summary>밀기 판정 거리 (이 오브젝트가 속한 캔버스 단위).</summary>
    public float Threshold { get; set; } = 120f;
    /// <summary>꺼져 있으면 밀기를 무시한다.</summary>
    public bool SwipeEnabled { get; set; } = true;

    private Vector2 _start;
    private float _scale = 1f;

    /// <inheritdoc />
    public void OnPointerClick(PointerEventData eventData) => OnTap?.Invoke();

    /// <inheritdoc />
    public void OnBeginDrag(PointerEventData eventData)
    {
        _start = eventData.position;
        var canvas = GetComponentInParent<Canvas>();
        _scale = canvas != null ? canvas.rootCanvas.scaleFactor : 1f;
    }

    /// <inheritdoc />
    public void OnDrag(PointerEventData eventData) { }

    /// <inheritdoc />
    public void OnEndDrag(PointerEventData eventData)
    {
        if (!SwipeEnabled) return;
        var delta = (eventData.position - _start) / Mathf.Max(0.0001f, _scale);
        if (Mathf.Abs(delta.x) < Threshold || Mathf.Abs(delta.x) < Mathf.Abs(delta.y)) return;
        OnSwipe?.Invoke(delta.x < 0f ? 1 : -1);
    }
}
