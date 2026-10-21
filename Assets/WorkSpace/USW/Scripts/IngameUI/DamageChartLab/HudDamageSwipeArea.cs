using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>가로는 HUD 페이지 전환, 세로는 딜 행 스크롤에 전달한다. 드래그는 버튼 탭을 취소한다.</summary>
public sealed class HudDamageSwipeArea : ScrollRect
{
    private bool _horizontalDrag, _accepted;
    private int _pointerId = int.MinValue;
    /// <summary>같은 두 페이지를 소유하는 HUD.</summary>
    public IngameHudDamagePager Pager { get; set; }

    /// <summary>드래그 시작 방향을 고정해 가로 전환과 세로 스크롤을 분리한다.</summary>
    public override void OnBeginDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        if (_pointerId != int.MinValue) return;
        _pointerId = eventData.pointerId;
        eventData.eligibleForClick = false;
        Vector2 delta = eventData.position - eventData.pressPosition;
        _horizontalDrag = Pager != null && Mathf.Abs(delta.x) > Mathf.Abs(delta.y);
        _accepted = _horizontalDrag && Pager.BeginSwipe(eventData);
        if (!_horizontalDrag && content != null) base.OnBeginDrag(eventData);
    }

    /// <summary>가로 전환 또는 세로 목록 스크롤을 갱신한다.</summary>
    public override void OnDrag(PointerEventData eventData)
    {
        if (_pointerId != eventData.pointerId) return;
        if (_horizontalDrag) { if (_accepted) Pager.DragSwipe(eventData); }
        else if (content != null) base.OnDrag(eventData);
    }

    /// <summary>가로 전환을 확정하거나 세로 스크롤을 끝낸다.</summary>
    public override void OnEndDrag(PointerEventData eventData)
    {
        if (_pointerId != eventData.pointerId) return;
        if (_horizontalDrag) { if (_accepted) Pager.EndSwipe(eventData); }
        else if (content != null) base.OnEndDrag(eventData);
        _accepted = false;
        _pointerId = int.MinValue;
    }
}
