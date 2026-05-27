using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;
using GaeGGUL.Animation;
using System;

/// <summary>
/// 유닛의 애니메이션을 제어하는 클래스.
/// Animator와의 직접적인 상호작용을 캡슐화하며, 
/// UniTask를 통해 애니메이션 재생 완료를 비동기적으로 대기할 수 있는 기능을 제공합니다.
/// </summary>
[RequireComponent(typeof(Animator))]
public class UnitAnimator : MonoBehaviour
{
    private Animator _animator;
    private Anim_Base _breathingAnim;

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
        _breathingAnim = GetComponent<Anim_Base>();
    }

    private void OnEnable()
    {
        // 유닛이 SetActive(false) -> SetActive(true) 될 때(스폰 이펙트 후 등) 애니메이션이 죽어있는 것을 방지
        if (_breathingAnim != null)
        {
            _breathingAnim.Play().Forget();
        }
    }

    public void Initialize(UnitBase unit)
    {
        if(_animator == null) _animator = GetComponent<Animator>();
        if(_breathingAnim == null) _breathingAnim = GetComponent<Anim_Base>();

        // 강제로 unit.transform을 타겟으로 잡으면 DOTween의_originScale 기록 시점이나 
        // Animator의 Root Scale Lock과 충돌할 수 있으므로, Anim_Base 자체의 초기화(Awake)를 존중합니다.
        // 필요에 따라 Prefab의 Anim_Base 인스펙터에서 Animation Target을 할당하세요.
        
        if (_breathingAnim != null)
        {
            _breathingAnim.Play().Forget();
        }
    }

    /// <summary>
    /// 기본 대기 상태로 전환합니다.
    /// </summary>
    public void PlayIdle()
    {
        _animator.SetInteger(AnimStateHash, (int)VisualState.Idle);
    }

    /// <summary>
    /// 애니메이션 재생 속도를 설정합니다.
    /// </summary>
    public void SetSpeed(float speed)
    {
        if (_animator != null) _animator.speed = speed;
    }

    /// <summary>
    /// 일반 공격 애니메이션을 재생하고 완료될 때까지 대기합니다.
    /// targetDuration이 지정되면 해당 시간 내에 애니메이션이 완료되도록 속도를 조절합니다.
    /// </summary>
    public async UniTask PlayAttackAsync(CancellationToken token, float targetDuration = 0f)
    {
        _animator.SetInteger(AnimStateHash, (int)VisualState.Attack);
        
        // Trigger 대신 Play를 사용하여 즉시 첫 프레임부터 재생 (삐걱거림 방지)
        _animator.Play("Attack", 0, 0f);

        if (targetDuration > 0)
        {
            // 정확한 타이밍을 맞추기 위해 전체 대기 시간을 미리 시작
            var delayTask = UniTask.Delay(TimeSpan.FromSeconds(targetDuration), cancellationToken: token);
            
            // 1프레임 대기 후 Animator 상태 정보 갱신
            await UniTask.Yield(PlayerLoopTiming.Update, token);
            
            var stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
            if (stateInfo.IsName("Attack"))
            {
                _animator.speed = stateInfo.length / targetDuration;
            }
            
            // 남은 시간만큼 정확히 대기 (프레임 밀림으로 인한 오차 방지)
            await delayTask;
        }
        else
        {
            // 완료 대기 (Fallback)
            await WaitUntilAnimationComplete("Attack", token);
        }
        
        // 재생 완료 후 속도 복구
        _animator.speed = 1.0f;
    }

    /// <summary>
    /// 스킬 애니메이션을 재생하고 완료될 때까지 대기합니다.
    /// </summary>
    public async UniTask PlaySkillAsync(CancellationToken token)
    {
        _animator.SetInteger(AnimStateHash, (int)VisualState.Skill);
        _animator.Play("Skill", 0, 0f);

        await WaitUntilAnimationComplete("Skill", token);
    }

    /// <summary>
    /// 특정 스테이트가 활성화될 때까지 대기합니다.
    /// </summary>
    private async UniTask WaitUntilStateActive(string stateName, CancellationToken token)
    {
        const int MaxWaitFrames = 60;
        int waited = 0;

        while (!_animator.GetCurrentAnimatorStateInfo(0).IsName(stateName))
        {
            if (token.IsCancellationRequested || ++waited > MaxWaitFrames) return;
            if (await UniTask.Yield(PlayerLoopTiming.Update, token).SuppressCancellationThrow())
                return;
        }
    }

    /// <summary>
    /// 특정 이름의 애니메이션이 완료될 때까지 대기합니다.
    /// </summary>
    private async UniTask WaitUntilAnimationComplete(string stateName, CancellationToken token)
    {
        // 1. 해당 스테이트로 전환될 때까지 대기
        await WaitUntilStateActive(stateName, token);

        // 2. 애니메이션이 끝날 때까지 대기 (normalizedTime >= 1.0f)
        int waited = 0;
        const int MaxWaitFrames = 600; // 완료는 좀 더 길게 대기 가능하도록 (배속 고려)
        
        var stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
        while (stateInfo.IsName(stateName) && stateInfo.normalizedTime < 1.0f)
        {
            if (token.IsCancellationRequested || ++waited > MaxWaitFrames) return;
            stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
            if (await UniTask.Yield(PlayerLoopTiming.Update, token).SuppressCancellationThrow())
                return;
        }
    }
}
