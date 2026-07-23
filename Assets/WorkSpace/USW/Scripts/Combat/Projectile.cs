using System;
using VContainer;
using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;

/// <summary>
/// 투사체 베이스 클래스. 실제 이동/이펙트 동작은 ProjectileData(SerializeReference 구성)에 위임한다.
/// Projectile 프리팹 자체는 공유되고, ProjectileData로 직선/유도 이동, 발사/피격 이펙트 조합이 갈린다.
/// ProjectilePool 또는 RM.Instantiate 등을 통해 소환됩니다.
/// 크기 배율은 BaseObject.ApplyScale()을 통해 항상 프리팹 원본 스케일(BaseScale) 기준으로 재계산되므로,
/// 풀링으로 재사용되어도 Launch()를 반복 호출할 때마다 크기가 누적되어 계속 커지지 않는다.
/// </summary>
public class Projectile : BaseObject
{
    [Inject] protected TotemBuffManager _totemBuffManager;
    [Inject] protected AudioManager _audioManager;

    [Header("투사체 구성 (Launch()에서 지정하지 않으면 이 값을 사용)")]
    [SerializeField] protected ProjectileData _data;

    protected Action<Projectile>      _onComplete;
    protected CancellationTokenSource _moveCts;

    // _data가 전혀 설정되지 않은 경우를 위한 안전한 기본값 (기존 하드코딩 동작과 동일한 직선 이동 + 피격 사운드).
    private static readonly IMovement _fallbackMovement = new StraightMovement();
    private static readonly IProjectileEffect _fallbackHitEffect = new PrefabProjectileEffect { sfxName = "05.Drone_Attack_Hit" };

    /// <summary>from → to로 투사체를 발사한다.
    /// sizeMultiplier: 이 발사 1회에만 적용되는 추가 크기 배율(예: 스킬 데이터의 투사체 크기 증가치). 기본 1(변화 없음).</summary>
    public virtual void Launch(Vector3 from, Vector3 to, Action<Projectile> onComplete, ProjectileData data = null, UnitBase sourceUnit = null, float sizeMultiplier = 1f)
    {
        StopMove();

        if (data != null) _data = data;

        from.z = 0f;
        to.z = 0f;
        transform.position = from;
        float totemMult = _totemBuffManager?.ProjectileSizeMultiplier ?? 1f;
        float unitBuffMult = sourceUnit?.Buffs?.GetStatMultiplier(StatKind.ProjectileSize) ?? 1f;
        ApplyScale(totemMult * unitBuffMult * sizeMultiplier);
        _onComplete        = onComplete;

        _moveCts = CancellationTokenSource.CreateLinkedTokenSource(
            this.GetCancellationTokenOnDestroy());

        _data?.fireEffect?.Play(from, transform.parent, _audioManager);

        MoveAsync(to, _moveCts.Token).Forget(e => { if (e is not System.OperationCanceledException) UnityEngine.Debug.LogException(e); });
    }

    protected void StopMove()
    {
        if (_moveCts == null) return;
        _moveCts.Cancel();
        _moveCts.Dispose();
        _moveCts = null;
    }

    protected virtual async UniTask MoveAsync(Vector3 target, CancellationToken token)
    {
        try
        {
            IMovement movement = _data?.movement ?? _fallbackMovement;
            await movement.MoveAsync(transform, transform.position, target, token);

            OnHit(target);

            _onComplete?.Invoke(this);
        }
        catch (OperationCanceledException) { }
    }

    protected virtual void OnHit(Vector3 pos)
    {
        // _data가 지정된 경우 그 구성(hitEffect가 비어있으면 이펙트 없음)을 그대로 존중하고,
        // _data 자체가 없는 경우에만 기존 하드코딩 동작(피격 사운드)으로 폴백한다.
        if (_data != null)
            _data.hitEffect?.Play(pos, transform.parent, _audioManager);
        else
            _fallbackHitEffect.Play(pos, transform.parent, _audioManager);
    }

    protected virtual void OnDisable()
    {
        StopMove();
    }

    protected virtual void OnDestroy()
    {
        StopMove();
    }
}
