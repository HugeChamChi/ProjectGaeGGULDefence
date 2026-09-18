using UnityEngine;

/// <summary>
/// 디버프 드론 소환 유닛.
/// 배치 시 드론 1마리 생성(등급 무관 고정), 스킬 발동 시 보스 피해 증폭 디버프 적용.
/// 유닛 제거 시 소속 드론을 풀로 반환한다.
/// </summary>
public class Drone_Deltan : DroneSpawnerBase
{
    /// <inheritdoc />
    public override float SkillCooldownMultiplier => 1f - (DroneSelections?.Get(DroneSelectionKind.DeltanCooldown)?.Value ?? 0f);
    protected override bool ApplySkillDebuff()
    {
        if (!TryGetDebuffBinding(out var binding) || binding.Trigger != DebuffTrigger.SkillActivated) return false;
        var target = SkillDebuffTarget;
        return target != null && target.TryApplyDebuff(binding, GetInstanceID(),
            (decimal)(DroneSelections?.Get(DroneSelectionKind.DeltanDamageTaken)?.Value ?? 0f),
            DroneSelections?.Get(DroneSelectionKind.DeltanDefenseReduction)?.Value ?? 0f);
    }



    protected override void OnSkillFull()
    {

        if (unitData == null) return;
        _audioManager?.PlaySFX("05.Drone_Debuff");
        FlashOwnedDrones();
    }
}
