using Cysharp.Threading.Tasks;
using UnityEngine;
using System.Threading;

/// <summary>
/// 족장 유닛들의 공통 기반 클래스.
/// - 스킬을 자동으로 사용하지 않습니다 (수동 발동).
/// - 수동 발동을 위한 메서드(ExecuteSkillManually)와 쿨다운 확인 프로퍼티를 제공합니다.
/// </summary>
public abstract class ChiefUnit : UnitBase
{
    public override bool CanAutoSkill => false;

    /// <summary>
    /// 족장 스킬 쿨타임(게이지)이 100% 찼는지 여부
    /// </summary>
    public bool IsSkillReady => SkillGaugeProgress >= 1f;

    /// <summary>
    /// 수동으로 족장 스킬을 발동합니다.
    /// UIDeveloper가 버튼 클릭 시 연동할 메서드입니다.
    /// </summary>
    public void ExecuteSkillManually()
    {
        if (currentCell == null || !gameObject.activeInHierarchy) return;

        if (IsSkillReady)
        {
            _skillTimer = 0f; // 스킬 게이지 초기화

            TriggerManualSkillAsync().Forget(e => 
            {
                if (e is not System.OperationCanceledException) Debug.LogException(e);
            });
        }
    }

    private async UniTask TriggerManualSkillAsync()
    {
        // OnSkillFull 오버라이드한 곳에서 실제 스킬 로직 발동
        OnSkillFull();

        if (animator != null) 
        {
            CurrentState = UnitState.Skilling;
            animator.SetSpeed(1f);
            await animator.PlaySkillAsync(this.GetCancellationTokenOnDestroy());
            CurrentState = UnitState.Idle;
        }
    }
}
