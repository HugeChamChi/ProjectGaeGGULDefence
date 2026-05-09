using UnityEngine;

/// <summary>
/// 판매 버튼 — 선택된 유닛 판매 처리
///
/// ─ 역할 ──────────────────────────────────────────────────
/// UnitActionPopupUI의 자식 컴포넌트
/// 판매 대상은 UnitActionPopupUI.Show() 시점에 SetUnit()으로 주입됨
///
/// ─ Button 컴포넌트 OnClick ───────────────────────────────
///   → SellButtonUI.OnSellButtonClicked() 연결
/// </summary>
public class SellButtonUI : MonoBehaviour
{
    private UnitBase _targetUnit;

    /// <summary>UnitActionPopupUI.Show()에서 호출 — 판매 대상 유닛 설정</summary>
    public void SetUnit(UnitBase unit)
    {
        _targetUnit = unit;
    }

    /// <summary>Inspector Button.OnClick에서 연결</summary>
    public void OnSellButtonClicked()
    {
        if (_targetUnit == null) return;
        Manager.Spawner.SellUnit(_targetUnit);
        Manager.Merge.HideButton();
    }
}
