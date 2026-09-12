#if UNITY_EDITOR
using System;
using UnityEngine;

/// <summary>토템 편집기 검증용 투사체. 실제 풀 호출을 받고 적중 시점만 테스트에서 제어한다.</summary>
public sealed class TotemCheckProjectile : Projectile
{
    private Action<Projectile> _completion;
    /// <summary>이 풀 인스턴스의 발사 횟수.</summary>
    public int LaunchCount { get; private set; }
    /// <inheritdoc />
    public override void Launch(Vector3 from, Vector3 to, Action<Projectile> onComplete,
        ProjectileData data = null, UnitBase sourceUnit = null, float sizeMultiplier = 1f)
    {
        LaunchCount++;
        _completion = onComplete;
    }
    /// <summary>발사 시 저장된 실제 게임 적중 콜백을 한 번 실행한다.</summary>
    public void Complete()
    {
        var completion = _completion;
        _completion = null;
        completion?.Invoke(this);
    }
}
#endif
