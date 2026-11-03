/* Positional Value contracts are explicit. Changing their order requires a new contract/migration. */
(function(root){
 'use strict';
 const f=(key,label,extra={})=>({key,label,...extra});
 const fields={
  StatModifierBonus:[f('Value','수치')],
  EveryNthCycleBonus:[f('Every','몇 번째 기본 자폭',{integer:true,min:1}),f('DamageBonusPercent','피해 증가율 (%)'),f('ChargeReductionPercent','충전 감소율 (%)',{exclusiveMax:100})],
  HackingAttackSpeedBonus:[f('Value','공격속도 증가율 (%)'),f('Duration','지속시간 (초)')],
  ChiefAttackSpeedBonus:[f('Value','전체 공격속도 증가율 (%)'),f('Duration','지속시간 (초)')],
  RallyVulnerabilityBonus:[f('Value','받는 피해 증가율 (%)'),f('Duration','지속시간 (초)')],
  HackingProductionBonus:['Normal','Rare','Epic','Legend'].map((key,i)=>f(key,['노말','레어','에픽','전설'][i]+' 추가 생산',{integer:true,max:10000})),
  HackingConsumptionBonus:['Normal','Rare','Epic','Legend'].map((key,i)=>f(key,['노말','레어','에픽','전설'][i]+' 추가 소모 한도',{integer:true,max:10000}))
 };
 const labels={StatModifierBonus:'능력치·고유 능력',EveryNthCycleBonus:'N번째 기본 자폭 강화',HackingAttackSpeedBonus:'해킹 후 자신의 공속 증가',ChiefAttackSpeedBonus:'족장 스킬 후 전체 공속 증가',RallyVulnerabilityBonus:'집결 적중 후 받는 피해 증가',HackingProductionBonus:'해킹 추가 생산 (등급별)',HackingConsumptionBonus:'해킹 추가 소모 한도 (등급별)'};
 const owners={EveryNthCycleBonus:'UnitData_Betan',HackingAttackSpeedBonus:'UnitData_Deltan',ChiefAttackSpeedBonus:'AlphanChief',RallyVulnerabilityBonus:'AlphanChief',HackingProductionBonus:'UnitData_Deltan',HackingConsumptionBonus:'UnitData_Gamman'};
 const defaults={StatModifierBonus:'5',EveryNthCycleBonus:'2,40,0',HackingAttackSpeedBonus:'15,2',ChiefAttackSpeedBonus:'20,10',RallyVulnerabilityBonus:'10,5',HackingProductionBonus:'1,1,2,3',HackingConsumptionBonus:'1,1,2,4'};
 const projections={HackingAttackSpeedBonus:[['HackingPostSpeed','Percent','Value'],['HackingPostDuration','Flat','Duration']],ChiefAttackSpeedBonus:[['ChiefSpeed','Percent','Value'],['ChiefSpeedDuration','Flat','Duration']],RallyVulnerabilityBonus:[['ChiefDamageTaken','Percent','Value'],['ChiefDamageTakenDuration','Flat','Duration']],HackingProductionBonus:['Normal','Rare','Epic','Legend'].map(key=>['HackingProduced'+key,'Flat',key]),HackingConsumptionBonus:['Normal','Rare','Epic','Legend'].map(key=>['HackingConsumed'+key,'Flat',key])};
 function parse(effect){
  const schema=fields[effect.EffectType];if(!schema)throw Error('지원하지 않는 효과 규약: '+effect.EffectType);
  const values=String(effect.Value??'').split(',').map(s=>s.trim());
  if(values.length!==schema.length)throw Error(labels[effect.EffectType]+': Value는 '+schema.length+'개 값이 필요합니다 ('+schema.map(f=>f.label).join(', ')+')');
  return Object.fromEntries(schema.map((field,i)=>{const raw=values[i];if(!/^\+?(?:\d+(?:\.\d*)?|\.\d+)(?:[eE][+-]?\d+)?$/.test(raw))throw Error(field.label+': 숫자를 입력하세요.');const value=Number(raw);if(!Number.isFinite(value)||value>Number.MAX_SAFE_INTEGER||value<(field.min??0)||field.integer&&!Number.isInteger(value)||field.max!==undefined&&value>field.max||field.exclusiveMax!==undefined&&value>=field.exclusiveMax)throw Error(field.label+': 범위 또는 정수 조건을 확인하세요.');return [field.key,value];}));
 }
 function parts(effect){
  const args=parse(effect),stat=(Stat,ValueMode,Value,parameter)=>({EffectType:'StatModifierBonus',Stat,ValueMode,Value,CycleId:'',Every:0,DamageBonusPercent:0,ChargeReductionPercent:0,parameter});
  if(effect.EffectType==='StatModifierBonus')return [stat(effect.Stat,effect.ValueMode,args.Value,'Value')];
  if(effect.EffectType==='EveryNthCycleBonus')return [{EffectType:'EveryNthCycleBonus',Stat:'Attack',ValueMode:'Percent',Value:0,CycleId:'basic_skill',...args,parameter:'cycle'}];
  return projections[effect.EffectType].map(([key,mode,param])=>stat(key,mode,args[param],param));
 }
 function compact(project){
  const p=JSON.parse(JSON.stringify(project));
  for(const bonus of p.tables.BonusDefinition)delete bonus.Name;
  if(!p.tables.BonusEffect.some(r=>typeof r.Value==='number'||Object.hasOwn(r,'Every')))return p;
  p.tables.BonusEffect=p.tables.BonusEffect.map(r=>({BonusId:r.BonusId,EffectKey:r.EffectKey,EffectType:r.EffectType,Stat:r.EffectType==='StatModifierBonus'?r.Stat:'',ValueMode:r.EffectType==='StatModifierBonus'?r.ValueMode:'',Value:r.EffectType==='EveryNthCycleBonus'?[r.Every,r.DamageBonusPercent,r.ChargeReductionPercent].join(','):String(r.Value)}));
  for(const bonus of p.tables.BonusDefinition){
   for(const [kind,projection]of Object.entries(projections)){
    const matches=projection.map(([stat,mode])=>p.tables.BonusEffect.filter(r=>r.BonusId===bonus.BonusId&&r.EffectType==='StatModifierBonus'&&r.Stat===stat&&r.ValueMode===mode));
    if(matches.some(list=>list.length!==1))continue;
    const source=matches.map(list=>list[0]),primary=source[0];
    for(let i=0;i<source.length;i++)bonus.DescriptionTemplate=bonus.DescriptionTemplate.replaceAll('{'+source[i].EffectKey+'.Value}','{'+primary.EffectKey+'.'+projection[i][2]+'}');
    const replacement={BonusId:bonus.BonusId,EffectKey:primary.EffectKey,EffectType:kind,Stat:'',ValueMode:'',Value:source.map(r=>r.Value).join(',')};
    const index=p.tables.BonusEffect.indexOf(primary);p.tables.BonusEffect=p.tables.BonusEffect.filter(r=>!source.includes(r));p.tables.BonusEffect.splice(Math.min(index,p.tables.BonusEffect.length),0,replacement);
   }
  }
  return p;
 }
 function expand(project){
  const p=JSON.parse(JSON.stringify(project)),result=[],used=new Set(p.tables.BonusEffect.map(r=>r.BonusId+'/'+r.EffectKey));
  for(const effect of p.tables.BonusEffect){const bonus=p.tables.BonusDefinition.find(b=>b.BonusId===effect.BonusId),list=parts(effect);
   list.forEach((part,i)=>{const {parameter,...data}=part;let key=effect.EffectKey;
    if(i>0){const base=key+'_'+parameter;key=base;let n=2;while(used.has(effect.BonusId+'/'+key))key=base+'_'+n++;used.add(effect.BonusId+'/'+key);}
    if(parameter!=='cycle'&&bonus)bonus.DescriptionTemplate=bonus.DescriptionTemplate.replaceAll('{'+effect.EffectKey+'.'+parameter+'}','{'+key+'.Value}');
    result.push({BonusId:effect.BonusId,EffectKey:key,...data});
   });
  }p.tables.BonusEffect=result;return p;
 }
 const api={fields,labels,owners,defaults,projections,parse,parts,compact,expand};
 if(typeof module!=='undefined'&&module.exports)module.exports=api;else root.BonusContracts=api;
})(globalThis);
