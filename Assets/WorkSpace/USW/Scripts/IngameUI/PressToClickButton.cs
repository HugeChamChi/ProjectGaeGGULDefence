using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>버튼을 누르는 순간 클릭 리스너를 실행하고 손을 뗄 때의 클릭은 중복 실행하지 않는다.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public sealed class PressToClickButton : MonoBehaviour, IPointerDownHandler
{
    private Button _button;

    private void Awake() => _button = GetComponent<Button>();

    /// <summary>UGUI가 클릭 후보로 표시한 직후 호출되므로 release 클릭을 끄고 바로 실행한다.</summary>
    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        eventData.eligibleForClick = false;
        if (_button == null || !_button.IsActive() || !_button.IsInteractable()) return;
        _button.onClick.Invoke();
    }
}
