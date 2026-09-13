using UnityEngine;

/// <summary>첫 적중의 최종 피해를 저장하고 후속 그림자 적중에 그대로 적용한다.</summary>
public sealed class RecordedDamage
{
    private readonly decimal _baseDamage;
    private long? _finalUnits;

    /// <summary>공격자 계산 및 치명타가 끝난 기준 피해를 보관한다.</summary>
    public RecordedDamage(decimal baseDamage) => _baseDamage = baseDamage;

    /// <summary>첫 호출만 대상 방어력을 계산한다. 후속 호출은 저장한 결과를 사용한다.</summary>
    public void Apply(BossBase target, Vector3 position)
    {
        if (target == null) return;
        if (_finalUnits.HasValue) target.ApplyRecordedDamage(_finalUnits.Value, position);
        else _finalUnits = target.TakeDamageAndRecord(_baseDamage, position);
    }
}
