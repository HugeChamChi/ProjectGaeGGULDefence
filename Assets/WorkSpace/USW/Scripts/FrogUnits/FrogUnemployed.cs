public class FrogUnemployed : UnitBase
{
    protected override void OnSkillFull()
    {
        var lu = Manager.LevelUp;
        if (lu?.HasUnemployedFoodNegate != true)
        {
            base.OnSkillFull();
            return;
        }

        // 3056 식충이 — 식량 생산 제거, 스킬마다 공격력 +N 누적
        _unemployedAtkBonus += lu.UnemployedSkillAtkGain;

        bool attackDisabled = currentCell != null &&
            (currentCell.Model.IsAttackDisabled || currentCell.Model.TotemAttackDisabled);

        if (!attackDisabled && _boss != null && !_boss.IsDead)
        {
            LaunchProjectile();
            _boss.TakeDamage(GetSkillDamage());
        }

        onSkillFull?.Invoke();
    }
}
