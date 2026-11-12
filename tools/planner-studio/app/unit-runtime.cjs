'use strict';
const path = require('node:path');
const workbook = require('./content-workbook.cjs');

// The legacy editor column AttackInterval contains UnitData.attackSpeed (attacks/second).
// Give the runtime field its actual unit without changing existing saved projects.
function unitRuntime(project) {
  const engine = workbook.engine(), shaped = engine.Studio.shape(project);
  const errors = engine.Studio.validate(shaped).filter(i => (i.table==='UnitBaseStat'||i.table==='Unit'&&i.field!=='LevelBonusKey') && i.severity === 'error');
  if (errors.length) throw Error(errors.map(i => i.message).join('\n'));
  const units = shaped.tables.Unit.filter(u => u.Kind === 'Unit').map(u => {
    const asset = path.posix.basename(String(u.Source || '').replaceAll('\\','/'), '.asset');
    if (!asset) throw Error('게임 유닛 매핑이 없습니다: ' + u.UnitKey);
    return {
      UnitKey: u.UnitKey, AssetKey: asset, Name: u.Name,
      Stats: shaped.tables.UnitBaseStat.filter(r => r.BaseStatKey === u.BaseStatKey).map(r => ({
        Tier: r.Tier, Attack: r.Attack, AttackFrequency: r.AttackInterval,
        SkillCooldown: r.SkillCooldown, FoodProduction: r.FoodProduction, MaxDroneCount: r.MaxDroneCount,
        // Chart columns added 2026-11-12; the game rejects a unit stat chart without them.
        SelfDestructCoefficient: r.SelfDestructCoefficient, CombatDroneAttackCoefficient: r.CombatDroneAttackCoefficient, FleetFoodPerDrone: r.FleetFoodPerDrone,
      })),
    };
  });
  if (!units.length || new Set(units.map(u => u.AssetKey)).size !== units.length) throw Error('유닛 매핑이 없거나 중복됩니다.');
  for (const u of units) {
    for (const k of ['SelfDestructCoefficient','CombatDroneAttackCoefficient','FleetFoodPerDrone'])
      if (u.Stats.some(x => typeof x[k] !== 'number' || !Number.isFinite(x[k]) || x[k] < 0)) throw Error(u.UnitKey + ': ' + k + ' 값이 없거나 잘못됐습니다.');
    if (new Set(u.Stats.map(x => x.CombatDroneAttackCoefficient)).size !== 1) throw Error(u.UnitKey + ': 전투 드론 공격 계수는 네 등급이 같은 값이어야 합니다.');
  }
  const UpgradeCosts = shaped.tables.Unit.flatMap(u => {
    const mapping = JSON.parse(u.BackendIds), key = u.Kind === 'Chief' ? 'chief:' + mapping : 'unit:' + mapping.normal;
    return shaped.tables.UnitUpgrade.filter(r => r.UnitId === u.UnitId).map(r => {
      if (r.Costs.length !== 1 || !['Gold','Diamond','UpgradeTokenA'].includes(r.Costs[0].itemId) || !Number.isSafeInteger(r.Costs[0].amount) || r.Costs[0].amount <= 0 || r.Costs[0].amount > 2147483647) throw Error('강화 비용은 Gold, Diamond, UpgradeTokenA 중 하나와 양의 정수 수량을 설정하세요.');
      return {Id:key, TargetLevel:r.TargetLevel, ItemId:r.Costs[0].itemId, Amount:r.Costs[0].amount};
    });
  });
  return {SchemaVersion:1, Units:units, UpgradeCosts};
}
module.exports = {unitRuntime};
