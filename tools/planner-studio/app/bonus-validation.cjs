'use strict';
const headers=['UnitKey','Level','PartIndex','EffectType','Stat','ValueMode','Value','CycleId','Every','DamageBonusPercent','ChargeReductionPercent'];
const modes={Attack:['Flat','Percent'],BasicAttackFrequency:['Flat','Percent'],CriticalChance:['PercentagePoints'],CriticalDamage:['PercentagePoints'],SkillCooldownReduction:['Flat','Percent'],FoodProduction:['Flat','Percent'],MaxDroneCount:['Flat'],ChargeAttackReduction:['Flat'],HackingCapacity:['Flat'],HackingPostSpeed:['Percent'],HackingPostDuration:['Flat'],HackingStackDamage:['Percent'],RecoveryNextBasic:['Percent'],TimeRecovery:['Percent'],BasicDamage:['Percent'],FoodSpeed:['Percent'],BossFoodSeconds:['Flat'],FoodFifthMultiplier:['Flat'],ChiefCooldown:['Percent'],ChiefStartsReady:['Flat'],ChiefGlobalAttack:['Percent'],ChiefSpeed:['Percent'],ChiefSpeedDuration:['Flat'],ChiefDamageTaken:['Percent'],ChiefDamageTakenDuration:['Flat'],HackingProducedNormal:['Flat'],HackingProducedRare:['Flat'],HackingProducedEpic:['Flat'],HackingProducedLegend:['Flat'],HackingConsumedNormal:['Flat'],HackingConsumedRare:['Flat'],HackingConsumedEpic:['Flat'],HackingConsumedLegend:['Flat']};
function validate(rows){const seen=new Set();for(const r of rows){
 const key=[r.UnitKey,r.Level,r.PartIndex].join('|');if(seen.has(key))throw Error('중복 효과: '+key);seen.add(key);
 if(!r.UnitKey||!Number.isInteger(r.Level)||r.Level<1||!Number.isInteger(r.PartIndex)||r.PartIndex<0)throw Error('키/레벨/효과 순번 오류');
 if(r.EffectType==='StatModifierBonus'){
  if(!modes[r.Stat]?.includes(r.ValueMode)||!Number.isFinite(r.Value)||r.Value<0)throw Error('지원하지 않는 스탯/단위/수치');
  const owner=r.Stat.startsWith('Chief')?'AlphanChief':r.Stat==='ChargeAttackReduction'||r.Stat==='HackingCapacity'||r.Stat.startsWith('HackingProduced')||r.Stat.startsWith('HackingPost')?'UnitData_Deltan':r.Stat==='HackingStackDamage'||r.Stat.startsWith('HackingConsumed')?'UnitData_Gamman':['RecoveryNextBasic','TimeRecovery','BasicDamage'].includes(r.Stat)?'UnitData_Disigman':['FoodSpeed','BossFoodSeconds','FoodFifthMultiplier'].includes(r.Stat)?'UnitData_Zeltan':null;
  if(owner&&r.UnitKey!==owner||r.UnitKey==='AlphanChief'&&!r.Stat.startsWith('Chief'))throw Error('대상 유닛 전용 스탯을 선택하세요');
  if((r.Stat==='ChargeAttackReduction'||r.Stat==='HackingCapacity'||/^Hacking(?:Produced|Consumed)/.test(r.Stat))&&(!Number.isInteger(r.Value)||r.Value>10000))throw Error('공격 횟수와 스택은 0~10000 정수');
  if(r.Stat==='ChiefStartsReady'&&![0,1].includes(r.Value))throw Error('시작 충전은 0 또는 1');
  if(r.Stat==='ChiefCooldown'&&r.Value>=100)throw Error('족장 쿨타임 감소는 100% 미만');
  if(r.Stat==='MaxDroneCount'&&!Number.isInteger(r.Value))throw Error('드론 수는 정수');
  if(r.Stat==='SkillCooldownReduction'&&r.ValueMode==='Percent'&&r.Value>=100)throw Error('쿨타임 감소는 100% 미만');
 }else if(r.EffectType==='EveryNthCycleBonus'){
  if(r.UnitKey!=='UnitData_Betan'||r.CycleId!=='basic_skill'||!Number.isInteger(r.Every)||r.Every<1||!Number.isFinite(r.DamageBonusPercent)||r.DamageBonusPercent<0||!Number.isFinite(r.ChargeReductionPercent)||r.ChargeReductionPercent<0||r.ChargeReductionPercent>=100)throw Error('주기 효과 값 오류');
 }else throw Error('지원하지 않는 효과 클래스');
 }for(const r of rows){const indices=rows.filter(x=>x.UnitKey===r.UnitKey&&x.Level===r.Level).map(x=>x.PartIndex).sort((a,b)=>a-b);if(indices.some((x,i)=>x!==i))throw Error('PartIndex는 0부터 연속');}return rows;}

module.exports={headers,modes,validate};
