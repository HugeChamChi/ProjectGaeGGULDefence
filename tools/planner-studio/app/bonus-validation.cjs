'use strict';
const headers=['UnitKey','Level','PartIndex','EffectType','Stat','ValueMode','Value','CycleId','Every','DamageBonusPercent','ChargeReductionPercent'];
const modes={Attack:['Flat','Percent'],BasicAttackFrequency:['Flat','Percent'],CriticalChance:['PercentagePoints'],CriticalDamage:['PercentagePoints'],SkillCooldownReduction:['Flat','Percent'],FoodProduction:['Flat','Percent'],MaxDroneCount:['Flat']};
function validate(rows){const seen=new Set();for(const r of rows){
 const key=[r.UnitKey,r.Level,r.PartIndex].join('|');if(seen.has(key))throw Error('중복 효과: '+key);seen.add(key);
 if(!r.UnitKey||!Number.isInteger(r.Level)||r.Level<1||!Number.isInteger(r.PartIndex)||r.PartIndex<0)throw Error('키/레벨/효과 순번 오류');
 if(r.EffectType==='StatModifierBonus'){
  if(!modes[r.Stat]?.includes(r.ValueMode)||!Number.isFinite(r.Value)||r.Value<0)throw Error('지원하지 않는 스탯/단위/수치');
  if(r.Stat==='MaxDroneCount'&&!Number.isInteger(r.Value))throw Error('드론 수는 정수');
  if(r.Stat==='SkillCooldownReduction'&&r.ValueMode==='Percent'&&r.Value>=100)throw Error('쿨타임 감소는 100% 미만');
 }else if(r.EffectType==='EveryNthCycleBonus'){
  if(r.UnitKey!=='UnitData_Betan'||r.CycleId!=='basic_skill'||!Number.isInteger(r.Every)||r.Every<1||!Number.isFinite(r.DamageBonusPercent)||r.DamageBonusPercent<0||!Number.isFinite(r.ChargeReductionPercent)||r.ChargeReductionPercent<0||r.ChargeReductionPercent>=100)throw Error('주기 효과 값 오류');
 }else throw Error('지원하지 않는 효과 클래스');
 }for(const r of rows){const indices=rows.filter(x=>x.UnitKey===r.UnitKey&&x.Level===r.Level).map(x=>x.PartIndex).sort((a,b)=>a-b);if(indices.some((x,i)=>x!==i))throw Error('PartIndex는 0부터 연속');}return rows;}

module.exports={headers,modes,validate};
