using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 합성 버튼 — 합성 가능 여부 색상 표시 및 클릭 처리
///
/// ─ 역할 ──────────────────────────────────────────────────
/// UnitActionPopupUI의 자식 컴포넌트
/// 팝업 생명주기(Show/Hide/위치)는 UnitActionPopupUI가 담당
///
/// ─ Inspector 연결 ────────────────────────────────────────
///   buttonImage → 이 오브젝트의 Image 컴포넌트
///
/// ─ Button 컴포넌트 OnClick ───────────────────────────────
///   → MergeButtonUI.OnMergeButtonClicked() 연결
/// </summary>
public class MergeButtonUI : MonoBehaviour
{
    [SerializeField] private Image buttonImage;

    [SerializeField] private Color colorActive   = Color.white;
    [SerializeField] private Color colorInactive = new Color(0.369f, 0.369f, 0.369f, 1f);

    /// <summary>UnitActionPopupUI.Show()에서 호출 — 합성 가능 여부에 따라 색상 변경</summary>
    public void SetState(bool canMerge)
    {
        buttonImage.color = canMerge ? colorActive : colorInactive;
    }

    /// <summary>Inspector Button.OnClick에서 연결</summary>
    public void OnMergeButtonClicked()
    {
        Manager.Merge.ExecuteMerge();
    }
}
