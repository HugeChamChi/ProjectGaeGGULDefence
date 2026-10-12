using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;

/// <summary>
/// 레일건 투사체 (임시 — 디시그망). 날아가지 않고 총구에서 _chargeSeconds 동안 충전한 뒤 착탄점에 즉시 적중한다.
/// 시각은 자식 RailgunBeamFx가 담당하고, 적중 판정/피해는 기존 Projectile 경로(OnHit → onComplete)를 그대로 쓴다.
/// ProjectileData.projectilePrefab으로 지정해 쓰며 ProjectilePool이 발사마다 생성/파괴한다.
/// </summary>
public class RailgunProjectile : Projectile
{
    [Tooltip("총구 충전 시간 — 이 시간이 지나야 빔이 나가고 피해가 들어간다")]
    [SerializeField] private float _chargeSeconds = 0.4f;
    [Tooltip("총구 위치 보정 (발사 시작 위치 기준, 월드)")]
    [SerializeField] private Vector2 _muzzleOffset = Vector2.zero;
    [SerializeField] private RailgunBeamFx _fx;
    [Tooltip("Authored cue pair; skill prefab selects the skill pair.")]
    [SerializeField] private GameAudioCue _chargeCue = GameAudioCue.DisigmanBasicCharge;
    [SerializeField] private GameAudioCue _shotCue = GameAudioCue.DisigmanBasicShot;
    [SerializeField] private bool _showHackLink;
    private DisigmanGlitchLinkPresentation _hackLink;
    private float _hackSeconds;

    [Inject]
    private void ConfigureHackLink(IObjectResolver resolver) => resolver.TryResolve(out _hackLink);

    /// <inheritdoc />
    public override void Launch(Vector3 from, Vector3 to, Action<Projectile> onComplete, ProjectileData data = null,
        UnitBase sourceUnit = null, float sizeMultiplier = 1f)
    {
        _hackSeconds = _showHackLink && sourceUnit is Disigman disigman ? disigman.SecondsRecovered : 0f;
        base.Launch(from, to, onComplete, data, sourceUnit, sizeMultiplier);
    }

    protected override void OnDisable()
    {
        _audioManager?.StopOwnedCue(this);
        base.OnDisable();
    }

    protected override async UniTask MoveAsync(Vector3 target, CancellationToken token)
    {
        try
        {
            _audioManager?.PlayOwnedCue(_chargeCue, this);
            Vector3 from = transform.position + (Vector3)_muzzleOffset;
            if (_hackSeconds > 0f) _hackLink?.Begin(this, from, _chargeSeconds, _hackSeconds);
            float scale = BaseScale.x > 1e-4f ? transform.localScale.x / BaseScale.x : 1f;
            if (_fx != null)
            {
                _fx.Play(transform, from, target, _chargeSeconds, scale);
                _fx = null; // 분리된 연출은 스스로 정리한다. 이 투사체는 한 발만 쓴다.
            }

            if (_chargeSeconds > 0f)
                await UniTask.Delay(TimeSpan.FromSeconds(_chargeSeconds), cancellationToken: token);

            _audioManager?.StopOwnedCue(this);
            _audioManager?.PlayCue(_shotCue);
            transform.position = target;
            _hackLink?.Impact(this, target);
            OnHit(target);
            _onComplete?.Invoke(this);
        }
        catch (OperationCanceledException) { }
        finally { _audioManager?.StopOwnedCue(this); }
    }
}
