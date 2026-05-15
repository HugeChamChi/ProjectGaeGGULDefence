using UnityEngine;
using Cysharp.Threading.Tasks;

/// <summary>
/// UnitBase와 UnitAnimator의 동작을 검증하기 위한 테스트 스크립트입니다.
/// 인스펙터의 버튼을 통해 유닛의 상태를 강제로 변경하거나 쿨다운을 조절해볼 수 있습니다.
/// </summary>
public class Test_UnitAnimation : MonoBehaviour
{
    [Header("Target Unit")]
    [SerializeField] private UnitBase _targetUnit;

    [Button]
    public void ForceAttack()
    {
        if (_targetUnit == null) return;
        Debug.Log("[Test] 일반 공격 수동 트리거");
        var animator = _targetUnit.GetComponentInChildren<UnitAnimator>();
        if (animator != null) animator.PlayAttackAsync(destroyCancellationToken).Forget();
    }

    [Button]
    public void ForceSkill()
    {
        if (_targetUnit == null) return;
        Debug.Log("[Test] 스킬 발동 수동 트리거");
        var animator = _targetUnit.GetComponentInChildren<UnitAnimator>();
        if (animator != null) animator.PlaySkillAsync(destroyCancellationToken).Forget();
    }

    [Button]
    public void PauseUnit()
    {
        if (_targetUnit == null) return;
        Debug.Log("[Test] 유닛 루프 일시정지");
        _targetUnit.PauseLoops();
    }

    [Button]
    public void ResumeUnit()
    {
        if (_targetUnit == null) return;
        Debug.Log("[Test] 유닛 루프 재개");
        _targetUnit.ResumeLoops();
    }

    [Button]
    public void CheckUnitStatus()
    {
        if (_targetUnit == null) return;
        Debug.Log($"[Test] 현재 상태: {_targetUnit.CurrentState}");
        Debug.Log($"[Test] 스킬 게이지: {(_targetUnit.SkillGaugeProgress * 100f):F1}%");
    }

    private void Update()
    {
        // 실시간 게이지 확인용 (디버깅)
        if (_targetUnit != null && _targetUnit.CurrentState == UnitBase.UnitState.Idle)
        {
            // 인스펙터에서 실시간으로 변하는 것을 확인하기 위함
        }
    }
}
