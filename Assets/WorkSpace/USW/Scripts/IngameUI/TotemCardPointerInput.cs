using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>자식 Button이 먼저 받는 포인터 입력을 카드의 탭/홀드 처리로 전달한다.</summary>
[DisallowMultipleComponent]
public sealed class TotemCardPointerInput : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler, IPointerClickHandler
{
    private TotemSelectCardUI _card;

    /// <summary>이 버튼을 소유한 카드를 연결한다.</summary>
    public void Configure(TotemSelectCardUI card) => _card = card;

    /// <summary>누름 시작을 전달한다.</summary>
    public void OnPointerDown(PointerEventData eventData) => _card?.OnPointerDown(eventData);

    /// <summary>누름 해제를 전달한다.</summary>
    public void OnPointerUp(PointerEventData eventData) => _card?.OnPointerUp(eventData);

    /// <summary>버튼 영역을 벗어난 입력을 취소한다.</summary>
    public void OnPointerExit(PointerEventData eventData) => _card?.OnPointerExit(eventData);

    /// <summary>완료된 탭을 전달한다.</summary>
    public void OnPointerClick(PointerEventData eventData) => _card?.OnPointerClick(eventData);

    private void OnDisable()
    {
        if (_card != null) _card.CancelPress();
    }
}
