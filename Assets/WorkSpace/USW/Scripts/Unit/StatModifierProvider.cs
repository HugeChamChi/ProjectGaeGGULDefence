using UnityEngine;

/// <summary>기존 집계 API를 공통 입력으로 변환한다. 계산 직전 현재 상태를 읽어 갱신 이벤트 누락을 피한다.</summary>
public sealed class StatModifierProvider
{
    private readonly StatModifierRegistry _basic = new StatModifierRegistry();
    private readonly StatModifierRegistry _attack = new StatModifierRegistry();
    private readonly StatModifierRegistry _damage = new StatModifierRegistry();
    private readonly StatModifierRegistry _intervals = new StatModifierRegistry();
    private readonly UnitBase _unit;
    private readonly UnitDependencies _deps;
    public StatModifierProvider(UnitBase unit, UnitDependencies deps) { _unit = unit; _deps = deps; }
    private float Level(UnitBonusStat stat) => _unit.LevelBonuses?.GetStat(stat) ?? 0f;
    private static StatModifier Flat(StatStage stage, float value) => new StatModifier(stage.ToString(), stage, ModifierOperation.Flat, value);
    private static StatModifier Scale(StatStage stage, float value) => new StatModifier(stage.ToString(), stage, ModifierOperation.IndependentMultiplier, value);

    public StatModifierSnapshot CaptureAttack()
    {
        var r = _attack;
        r.ReplaceSource("Account", new[] { Scale(StatStage.AccountAttackScale, _unit.AccountUpgradeMultiplier * _unit.LevelAttackGrowth) });
        r.ReplaceSource("Level", new[] { Flat(StatStage.LevelAttackFlat, Level(UnitBonusStat.AttackFlat)), Flat(StatStage.LevelAttackRatio, Level(UnitBonusStat.AttackPercent)) });
        r.ReplaceSource("Alphan", new[] { Flat(StatStage.AlphanAttackRatio, _deps?.Alphan?.GlobalAttackBonus ?? 0f) });
        r.ReplaceSource("Upgrade", new[] { Scale(StatStage.UpgradeAttackScale, AttackUpgrade) });
        return r.Capture();
    }

    private float AttackUpgrade => _deps?.UpgradeManager != null && _deps.UpgradeManager.IsLoaded
        ? _deps.UpgradeManager.GetAtkUpgradeMultiplier(_unit.unitData.characterId.Get(_unit.currentTier)) : 1f;
    private float SpeedUpgrade => _deps?.UpgradeManager != null && _deps.UpgradeManager.IsLoaded
        ? _deps.UpgradeManager.GetAttackSpeedUpgradeMultiplier(_unit.unitData.characterId.Get(_unit.currentTier)) : 1f;

    public StatModifierSnapshot CaptureDamage(float passiveScale, float burstEnd, bool attackBased, float callerScale)
    {
        var r = _damage;
        int row = _unit.currentCell?.GridPosition.y ?? 0;
        int rows = _deps?.GridManager?.Rows ?? 4;
        var selection = _deps?.SelectionCombat;
        float size = (_deps?.TotemBuffManager?.ProjectileSizeMultiplier ?? 1f) * (_unit.Buffs?.GetStatMultiplier(StatKind.ProjectileSize) ?? 1f);
        float projectile = selection?.HasProjectileSizeScalesAtk == true
            ? Mathf.Max(0f, (size - 1f) / .1f * selection.ProjectileSizeAtkPerUnit) : 0f;
        // Preserve the existing aggregate's addition order; do not reverse-engineer individual contributions.
        r.ReplaceSource("LegacyStatAggregate", new[] {
            Flat(StatStage.DamageFlat, _unit.GetStatBonus(StatKind.AttackFlat, passiveScale)),
            Flat(StatStage.DamageRatio, _unit.GetStatBonus(StatKind.AttackPercent, passiveScale)),
            Flat(StatStage.CriticalChance, _unit.GetStatBonus(StatKind.CritChance)),
            Flat(StatStage.CriticalDamage, _unit.GetStatBonus(StatKind.CritDamage)) });
        r.ReplaceSource("Cell", new[] {
            Scale(StatStage.CellDamageScale, _unit.currentCell != null ? (_unit.currentCell.Model.NullifyDamageDebuff ? 1f : _unit.currentCell.Model.DamageModifier) : 1f),
            Scale(StatStage.TotemAttackScale, _unit.currentCell?.Model.TotemAttackModifier ?? 1f) });
        r.ReplaceSource("Selection", new[] {
            Scale(StatStage.RowAttackScale, selection?.GetRowAttackMultiplier(row, rows) ?? 1f),
            Scale(StatStage.ProjectileAttackScale, 1f + projectile * passiveScale),
            Scale(StatStage.BurstAttackScale, Time.time < burstEnd && selection != null ? 1f + selection.BurstAttackBonus : 1f),
            Flat(StatStage.SelectionCriticalChance, selection?.CritChance ?? 0f),
            Flat(StatStage.SelectionCriticalDamage, selection != null ? selection.CritDamageMultiplier
                : _deps?.CombatSettings?.BaseCritDamageMultiplier ?? CombatSettings.DefaultBaseCritDamageMultiplier),
            Flat(StatStage.GlitchAttackRatio, _deps?.GlitchEffects?.GlitchAttackBonus ?? 0f),
            Flat(StatStage.SacrificeAttackRatio, (_deps?.GlitchEffects as IOverdriveEffectReader)?.SacrificeAttackBonus ?? 0f) });
        r.ReplaceSource("Unit", new[] {
            Flat(StatStage.BetanAttackReduction, attackBased && _unit is Drone_Betan ? (_deps?.DroneEffects?.BetanAttackReductionRatio ?? 0f) : 0f),
            Scale(StatStage.CallerDamageScale, callerScale) });
        r.ReplaceSource("Level", new[] {
            Flat(StatStage.LevelCriticalChance, Level(UnitBonusStat.CriticalChancePoints)),
            Flat(StatStage.LevelCriticalDamage, Level(UnitBonusStat.CriticalDamagePoints)) });
        r.ReplaceSource("Research", new[] {
            Flat(StatStage.ResearchCriticalChance, _deps?.Research?.Get(ResearchStat.CritChance) ?? 0f),
            Flat(StatStage.ResearchCriticalDamage, _deps?.Research?.Get(ResearchStat.CritDamage) ?? 0f),
            Flat(StatStage.ResearchAttackRatio, _deps?.Research?.Get(ResearchStat.AttackPercent) ?? 0f) });
        return r.Capture();
    }

    public StatModifierSnapshot CaptureBasic()
    {
        _basic.ReplaceSource("Level", new[] { Flat(StatStage.BasicDamageRatio, Level(UnitBonusStat.BasicDamagePercent)) });
        _basic.ReplaceSource("Unit", new[] { Scale(StatStage.RegularBasicScale, _unit.Combat?.RegularBasicDamageMultiplier ?? 1f) });
        return _basic.Capture();
    }

    public StatModifierSnapshot CaptureIntervals()
    {
        var r = _intervals;
        int row = _unit.currentCell?.GridPosition.y ?? 0;
        r.ReplaceSource("Level", new[] {
            Flat(StatStage.FrequencyFlat, Level(UnitBonusStat.BasicAttackFrequencyFlat)),
            Flat(StatStage.LevelFrequencyRatio, (1f + Level(UnitBonusStat.BasicAttackFrequencyPercent)) * _unit.LevelSpeedGrowth - 1f),
            Flat(StatStage.SkillCooldownFlatReduction, Level(UnitBonusStat.SkillCooldownFlat)),
            Flat(StatStage.SkillCooldownRatioReduction, Level(UnitBonusStat.SkillCooldownPercent)) });
        r.ReplaceSource("Upgrade", new[] { Scale(StatStage.UpgradeFrequencyScale, SpeedUpgrade) });
        r.ReplaceSource("Unit", new[] {
            Flat(StatStage.UnitFrequencyRatio, _unit.LevelAttackSpeedBonus),
            Scale(StatStage.PatternFrequencyScale, _unit.PatternAttackSpeedMultiplier),
            Scale(StatStage.UnitSkillCooldownScale, _unit.SkillCooldownMultiplier) });
        r.ReplaceSource("Selection", new[] {
            Flat(StatStage.AcceleratedFrequencyRatio, (_deps?.GlitchEffects as IOverdriveEffectReader)?.AcceleratedAttackSpeedBonus ?? 0f),
            Scale(StatStage.RowSpeedScale, _deps?.SelectionCombat?.GetRowSpeedMultiplier(row, _deps?.GridManager?.Rows ?? 4) ?? 1f) });
        r.ReplaceSource("Alphan", new[] { Flat(StatStage.AlphanFrequencyRatio, _deps?.Alphan?.AttackSpeedBonus ?? 0f) });
        r.ReplaceSource("LegacyStatAggregate", new[] {
            Flat(StatStage.SpeedRatio, _unit.GetStatBonus(StatKind.Speed)),
            Flat(StatStage.GaugeSpeedRatio, _unit.GetStatBonus(StatKind.GaugeSpeed)) });
        r.ReplaceSource("Cell", new[] {
            Scale(StatStage.CellSpeedScale, _unit.currentCell?.Model.SpeedModifier ?? 1f),
            Scale(StatStage.TotemSpeedScale, _unit.currentCell?.Model.TotemSpeedModifier ?? 1f) });
        r.ReplaceSource("Research", new[] {
            Flat(StatStage.ResearchFrequencyRatio, _deps?.Research?.Get(ResearchStat.AttackSpeedPercent) ?? 0f),
            Flat(StatStage.ResearchSkillCooldownRatioReduction, _deps?.Research?.Get(ResearchStat.SkillCooldownReduction) ?? 0f) });
        return r.Capture();
    }
}
