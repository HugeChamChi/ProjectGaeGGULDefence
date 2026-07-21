using UnityEngine;

/// <summary>[리더] 용사 (전사). 패시브 "훈련의 성과"는 PassiveData_훈련의성과 에셋으로 구현됨. 액티브(정의 구현)는 패시브 배율을 2배로 받는다.</summary>
public class FrogWarrior : ChiefUnit
{
    [Tooltip("정의 구현 스킬에 적용되는 '훈련의 성과' 패시브 배율 (기본 공격 대비 2배)")]
    [SerializeField] private float _skillProjectileSizeAtkMultiplier = 2f;

    public override int GetSkillDamage() => GetSkillDamage(_skillProjectileSizeAtkMultiplier);

    protected override void OnSkillFull()
    {
        onSkillFull?.Invoke();
        LaunchProjectile(GetSkillDamage());
    }
}
