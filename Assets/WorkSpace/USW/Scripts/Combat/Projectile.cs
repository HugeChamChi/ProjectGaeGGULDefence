using System;
using VContainer;
using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;

/// <summary>
/// 투사체 베이스 클래스. 실제 이동/이펙트 동작은 ProjectileData(SerializeReference 구성)에 위임한다.
/// Projectile 프리팹 자체는 공유되고, ProjectileData로 직선/유도 이동, 발사/피격 이펙트 조합이 갈린다.
/// ProjectilePool 또는 RM.Instantiate 등을 통해 소환됩니다.
/// </summary>
public class Projectile : MonoBehaviour
{
    [Inject] protected TotemBuffManager _totemBuffManager;
    [Inject] protected AudioManager _audioManager;

    [Header("투사체 구성 (Launch()에서 지정하지 않으면 이 값을 사용)")]
    [SerializeField] protected ProjectileData _data;

    protected Action<Projectile>      _onComplete;
    protected CancellationTokenSource _moveCts;

    // 풀링으로 재사용되는 오브젝트라 Launch()마다 transform.localScale에 곱연산하면 재사용될 때마다 계속 커진다.
    // Awake()는 실제 인스턴스 생성 시 한 번만 호출되므로, 여기서 프리팹 원본 스케일을 고정해 두고
    // 매 Launch()마다 이 값을 기준으로 다시 계산한다.
    private Vector3 _baseScale;

    // _data가 전혀 설정되지 않은 경우를 위한 안전한 기본값 (기존 하드코딩 동작과 동일한 직선 이동 + 피격 사운드).
    private static readonly IMovement _fallbackMovement = new StraightMovement();
    private static readonly IProjectileEffect _fallbackHitEffect = new PrefabProjectileEffect { sfxName = "05.Drone_Attack_Hit" };

    protected virtual void Awake()
    {
        _baseScale = transform.localScale;
    }

    public virtual void Launch(Vector3 from, Vector3 to, Action<Projectile> onComplete, ProjectileData data = null, UnitBase sourceUnit = null)
    {
        StopMove();

        if (data != null) _data = data;

        from.z = 0f;
        to.z = 0f;
        transform.position = from;
        float totemMult = _totemBuffManager?.ProjectileSizeMultiplier ?? 1f;
        float unitBuffMult = sourceUnit?.Buffs?.GetStatMultiplier(StatKind.ProjectileSize) ?? 1f;
        transform.localScale = _baseScale * (totemMult * unitBuffMult);
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
