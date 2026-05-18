using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 판매 버튼 뷰.
/// 판매 대상 유닛 보관, 클릭 시 OnSellRequested 이벤트 발행.
/// Manager 직접 참조 없음 — InGameInstaller가 이벤트를 UnitSpawner에 연결.
/// </summary>
public class SellButtonUI : MonoBehaviour
{
    private UnitBase _targetUnit;

    public event Action<UnitBase> OnSellRequested;

    private void Awake()
    {
        GetComponent<Button>().onClick.AddListener(OnSellButtonClicked);
    }

    /// <summary>UnitActionPopupUI.Show()에서 호출 — 판매 대상 유닛 설정</summary>
    public void SetUnit(UnitBase unit) => _targetUnit = unit;

    private void OnSellButtonClicked()
    {
        if (_targetUnit == null) return;
        OnSellRequested?.Invoke(_targetUnit);
    }
}
