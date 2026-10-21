using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>버튼의 탭을 보존하고 의도적인 가로 드래그만 HUD 전환에 전달한다.</summary>
[DisallowMultipleComponent]
public sealed class HudDamageButtonGesture : MonoBehaviour, IInitializePotentialDragHandler,
    IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private int _pointerId = int.MinValue;
    private bool _swiping;

    /// <summary>버튼 페이지를 소유하는 HUD.</summary>
    public IngameHudDamagePager Pager { get; set; }

    /// <summary>UGUI의 드래그 대상을 버튼 자체로 유지해 부모가 탭을 자동 취소하지 않게 한다.</summary>
    public void OnInitializePotentialDrag(PointerEventData eventData)
    {
        eventData.useDragThreshold = true;
    }

    /// <summary>작은 움직임은 탭 후보로 남겨 두고 가로 의도가 확인될 때만 전환을 시작한다.</summary>
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left || _pointerId != int.MinValue) return;
        _pointerId = eventData.pointerId;
        OnDrag(eventData);
    }

    /// <summary>가로 이동 거리와 방향 조건을 만족한 뒤에만 클릭을 취소한다.</summary>
    public void OnDrag(PointerEventData eventData)
    {
        if (_pointerId != eventData.pointerId || Pager == null) return;
        if (!_swiping) _swiping = Pager.TryBeginButtonSwipe(eventData);
        if (!_swiping) return;
        eventData.eligibleForClick = false;
        Pager.DragSwipe(eventData);
    }

    /// <summary>확정된 가로 제스처만 페이지 전환으로 마무리한다.</summary>
    public void OnEndDrag(PointerEventData eventData)
    {
        if (_pointerId != eventData.pointerId) return;
        if (_swiping && Pager != null) Pager.EndSwipe(eventData);
        _swiping = false;
        _pointerId = int.MinValue;
    }

    private void OnDisable()
    {
        if (_swiping && Pager != null) Pager.CancelSwipe(_pointerId);
        _swiping = false;
        _pointerId = int.MinValue;
    }
}
