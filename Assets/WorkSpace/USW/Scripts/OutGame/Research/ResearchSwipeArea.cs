using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 좌우 스와이프 감지 (강화 화면 하단 패널의 방식 전환용). 이 오브젝트에 raycast 되는 Graphic이 있어야 한다.
/// 자식 버튼 위에서 시작한 드래그도 여기로 올라온다 (Button은 드래그를 받지 않음). 드래그가 시작되면 uGUI가 버튼 클릭을 취소한다.
/// </summary>
public sealed class ResearchSwipeArea : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Tooltip("화면 너비 대비 이만큼 밀어야 넘어간다")]
    [SerializeField, Range(0.01f, 0.5f)] private float _thresholdRatio = 0.08f;

    /// <summary>스와이프 방향: +1 = 왼쪽으로 밀기(다음), -1 = 오른쪽으로 밀기(이전).</summary>
    public event Action<int> OnSwipe;

    public void OnBeginDrag(PointerEventData eventData) { }

    public void OnDrag(PointerEventData eventData) { }

    public void OnEndDrag(PointerEventData eventData)
    {
        Vector2 delta = eventData.position - eventData.pressPosition;
        if (Mathf.Abs(delta.x) < Screen.width * _thresholdRatio || Mathf.Abs(delta.x) < Mathf.Abs(delta.y)) return;
        OnSwipe?.Invoke(delta.x < 0f ? 1 : -1);
    }
}
