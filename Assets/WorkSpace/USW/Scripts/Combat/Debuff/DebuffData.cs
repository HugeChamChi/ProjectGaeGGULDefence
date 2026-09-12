using UnityEngine;

/// <summary>시트 준비 전/오프라인 정의 에셋. 활성 보스의 상태를 저장하지 않는다.</summary>
[CreateAssetMenu(menuName = "Game/Debuff", fileName = "DebuffData")]
public sealed class DebuffData : ScriptableObject
{
    [SerializeField] private int _id;
    [SerializeField] private string _key;
    [SerializeField] private DebuffKind _kind;
    [SerializeField] private string _stackGroup;
    [SerializeField] private double _duration;
    [SerializeField] private int _maxStacks;
    [SerializeField] private double _tickInterval;
    [SerializeField] private double _snapshotPercent;
    [SerializeField] private double _minTickDamage;
    [SerializeField] private double _armorStrength;
    [SerializeField] private double _damageMultiplier = 1;
    /// <summary>검증된 불변 정의를 생성한다.</summary>
    public DebuffDefinition CreateDefinition() => new DebuffDefinition(_id, _key, _kind, _stackGroup,
        _duration, _maxStacks, _tickInterval, (decimal)_snapshotPercent / 100, (decimal)_minTickDamage,
        _armorStrength, (decimal)_damageMultiplier);
}
