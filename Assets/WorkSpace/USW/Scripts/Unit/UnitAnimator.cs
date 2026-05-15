using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;

/// <summary>
/// 유닛의 애니메이션을 제어하는 클래스.
/// Animator와의 직접적인 상호작용을 캡슐화하며, 
/// UniTask를 통해 애니메이션 재생 완료를 비동기적으로 대기할 수 있는 기능을 제공합니다.
/// </summary>
[RequireComponent(typeof(Animator))]
public class UnitAnimator : MonoBehaviour
{
    private Animator _animator;
    
    // 파라미터 캐싱 (Performance Optimization)
    private static readonly int AnimStateHash = Animator.StringToHash("State");
    private static readonly int AttackTriggerHash = Animator.StringToHash("Attack");
    private static readonly int SkillTriggerHash = Animator.StringToHash("Skill");

    // 애니메이션 상태 정의 (필요에 따라 Animator Controller와 매칭)
    public enum VisualState
    {
        Idle = 0,
        Attack = 1,
        Skill = 2
    }

    private void Awake()
    {
        _animator = GetComponent<Animator>();
    }

    /// <summary>
    /// 기본 대기 상태로 전환합니다.
    /// </summary>
    public void PlayIdle()
    {
        _animator.SetInteger(AnimStateHash, (int)VisualState.Idle);
    }

    /// <summary>
    /// 일반 공격 애니메이션을 재생하고 완료될 때까지 대기합니다.
    /// </summary>
    public async UniTask PlayAttackAsync(CancellationToken token)
    {
        _animator.SetInteger(AnimStateHash, (int)VisualState.Attack);
        _animator.SetTrigger(AttackTriggerHash);

        // 애니메이션 시작 및 완료 대기
        await WaitUntilAnimationComplete("Attack", token);
    }

    /// <summary>
    /// 스킬 애니메이션을 재생하고 완료될 때까지 대기합니다.
    /// </summary>
    public async UniTask PlaySkillAsync(CancellationToken token)
    {
        _animator.SetInteger(AnimStateHash, (int)VisualState.Skill);
        _animator.SetTrigger(SkillTriggerHash);

        await WaitUntilAnimationComplete("Skill", token);
    }

    /// <summary>
    /// 특정 이름의 애니메이션이 완료될 때까지 대기합니다.
    /// </summary>
    private async UniTask WaitUntilAnimationComplete(string stateName, CancellationToken token)
    {
        // Animator가 현재 상태로 전환될 때까지 잠시 대기 (Transition 시간 고려)
        await UniTask.NextFrame(token);

        // 현재 재생 중인 애니메이션 정보 확인
        var stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
        
        // 애니메이션이 끝날 때까지 대기 (normalizedTime >= 1.0f)
        // 주의: 루프 애니메이션인 경우 1.0을 넘어가므로, 상태 이름을 체크하거나 이벤트를 사용하는 것이 더 정확할 수 있음
        // 여기서는 비동기 제어를 위해 normalizedTime을 기준으로 함
        while (stateInfo.IsName(stateName) && stateInfo.normalizedTime < 1.0f)
        {
            if (token.IsCancellationRequested) return;
            
            stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
            await UniTask.Yield(token);
        }
    }
}
