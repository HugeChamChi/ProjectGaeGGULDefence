using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 토템 판매 버튼. 클릭 시 TotemActionPopupUI를 통해 OnSellTotemRequested 발행.
/// Manager 직접 참조 없음 — InGameInstaller가 이벤트를 TotemSpawner에 연결.
/// </summary>
public class SellTotemButtonUI : MonoBehaviour
{
    private TotemBase          _targetTotem;
    private TotemActionPopupUI _popup;

    private void Awake()
    {
        GetComponent<Button>().onClick.AddListener(OnSellButtonClicked);
    }

    public void SetTotem(TotemBase totem)          => _targetTotem = totem;
    public void SetPopup(TotemActionPopupUI popup)  => _popup       = popup;

    private void OnSellButtonClicked()
    {
        if (_targetTotem == null || _popup == null) return;
        _popup.Hide();
        _popup.RaiseSellRequested(_targetTotem);
    }
}
