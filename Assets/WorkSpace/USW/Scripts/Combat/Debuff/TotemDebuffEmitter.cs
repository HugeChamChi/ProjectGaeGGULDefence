using System;
using UnityEngine;

/// <summary>토템 배치 수명에 귀속된 발사기. 셀 재계산과 독립적으로 실제 투사체를 발사한다.</summary>
public sealed class TotemDebuffEmitter : MonoBehaviour
{
    private TotemBase _source;
    private BossManager _bosses;
    private ProjectilePool _projectiles;
    private GameManager _game;
    private double _elapsed;
    /// <summary>주입된 씬 의존성을 배치 시 전달한다. 재배치 시 발사 주기를 초기화한다.</summary>
    public void Initialize(TotemBase source, BossManager bosses, ProjectilePool projectiles, GameManager game)
    {
        _source = source; _bosses = bosses; _projectiles = projectiles; _game = game; _elapsed = 0;
        if (source.Data.DebuffFireInterval <= 0 || source.Data.DebuffImpactDamage < 0)
            throw new InvalidOperationException("Projectile debuff source requires positive fire interval and nonnegative impact damage.");
        enabled = true;
    }
    private void Update()
    {
        if (_source == null || !_source.IsActive || _game == null || _game.CurrentState != GameManager.GameState.Playing || _projectiles == null) return;
        var target = _bosses != null ? _bosses.CurrentBoss : null;
        if (target == null || target.IsDead) return;
        _elapsed += Time.deltaTime;
        if (_elapsed < _source.Data.DebuffFireInterval) return;
        _elapsed %= _source.Data.DebuffFireInterval;
        if (!_source.TryGetDebuffBinding(out var binding) || binding.Trigger != DebuffTrigger.ProjectileHit) return;
        int sourceId = _source.GetInstanceID();
        decimal impact = _source.Data.DebuffImpactDamage;
        Vector3 hitPosition = target.transform.position;
        _projectiles.Launch(transform.position, hitPosition, _source.Data.DebuffProjectile, () =>
        {
            // 발사 당시 대상 유지. 제거된 보스/다음 웨이브로 피해를 넘기지 않는다.
            if (target != null && !target.IsDead) target.ApplyDebuffImpact(binding, sourceId, impact, hitPosition);
        });
    }
}
