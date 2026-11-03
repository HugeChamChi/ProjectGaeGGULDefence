'use strict';
globalThis.BonusEditorFeature=({studio})=>{
 const M=BonusModel,field=(key,label,type='text',extra={})=>({key,label,type,...extra});
 const defaults={effects:UNIT_BONUS_DEFAULTS,descriptions:UNIT_BONUS_DESCRIPTIONS,previous:UNIT_BONUS_PREVIOUS_DEFAULTS,definitions:UNIT_BONUS_MODEL_DEFAULTS.BonusDefinition};
 const units=STUDIO_SEED.tables.Unit,unitKeys=[...new Set([...units.map(u=>u.UnitKey||u.UnitId),...UNIT_BONUS_MODEL_DEFAULTS.BonusDefinition.map(r=>r.UnitKey)])];
 STUDIO_SCHEMAS.BonusDefinition={label:'레벨 보너스 설계',keys:['BonusId'],note:'보너스 하나에 해금 정보·설명·효과를 함께 관리합니다. 설명의 숫자는 효과 값에서 가져옵니다.',fields:[field('BonusId','보너스 ID','id',{required:true}),field('UnitKey','대상 유닛','enum',{values:unitKeys,labels:Object.fromEntries(units.map(u=>[u.UnitKey||u.UnitId,u.Name]))}),field('Level','해금 레벨','int',{min:1,max:1000}),field('DescriptionTemplate','설명 템플릿','textarea',{required:true}),field('HighlightNumbers','숫자 강조','bool'),field('LinkTerms','용어 링크','bool'),field('EffectLink','특수 링크','enum',{values:['None','ChiefAttackSpeed'],labels:{None:'없음',ChiefAttackSpeed:'알팡 공격속도 효과'}})]};
 STUDIO_SCHEMAS.BonusEffect={label:'보너스 효과',keys:['BonusId','EffectKey'],note:'효과 하나당 한 행. Value는 효과 규약의 순서대로 쉼표로 구분합니다.',fields:[field('BonusId','소속 보너스','ref',{table:'BonusDefinition',required:true}),field('EffectKey','효과 키','id',{required:true}),field('EffectType','효과 종류','enum',{values:Object.keys(M.tokenFields),labels:M.contracts.labels}),field('Stat','능력치','enum',{values:['',...Object.keys(M.modes)],labels:M.labels}),field('ValueMode','수치 단위','enum',{values:['','Flat','Percent','PercentagePoints'],labels:{Flat:'고정 수치',Percent:'비율 (%)',PercentagePoints:'직접 가산 (%)'}}),field('Value','규약 순서의 값','text',{required:true})]};
 function sync(p){
  let parts;try{parts=M.legacyRows(p);}catch{return p;}
  const owned=new Set(p.tables.Unit.map(u=>u.UnitId));
  p.tables.UnitMilestone=p.tables.UnitMilestone.filter(r=>!owned.has(r.UnitId));
  for(const bonus of p.tables.BonusDefinition){const unit=p.tables.Unit.find(u=>(u.UnitKey||u.UnitId)===bonus.UnitKey);if(!unit)continue;
   let row=p.tables.UnitMilestone.find(r=>r.UnitId===unit.UnitId&&r.Level===bonus.Level);if(!row){row={UnitId:unit.UnitId,Level:bonus.Level};p.tables.UnitMilestone.push(row);}
   let text;try{text=M.render(bonus,p.tables.BonusEffect);}catch{text=bonus.DescriptionTemplate;}
   Object.assign(row,{Description:text,EffectStatus:'Defined',EffectId:'CompositeBonus',EffectParams:JSON.stringify({Parts:parts.filter(r=>r.UnitKey===bonus.UnitKey&&r.Level===bonus.Level).map(({UnitKey,Level,PartIndex,...part})=>part)})});
  }return p;
 }
 Object.assign(STUDIO_SEED.tables,studio.clone(UNIT_BONUS_MODEL_DEFAULTS));STUDIO_SEED.meta.bonusSchemaVersion=3;
 sync(STUDIO_SEED);
 STUDIO_SCHEMAS.UnitMilestone.readonly=true;STUDIO_SCHEMAS.UnitMilestone.note='레벨 보너스 설계에서 자동 생성한 요약입니다.';
 const nav=STUDIO_NAV.find(n=>n.id==='units');nav.tables=nav.tables.filter(n=>!['UnitMilestone','BonusEffect','BonusDescription'].includes(n));nav.tables.push('BonusDefinition');
 PlannerScope.active.add('BonusDefinition');PlannerScope.active.add('BonusEffect');
 return {prepare(p){if(!p.tables.BonusDefinition&&!p.tables.BonusEffect){p=studio.clone(p);Object.assign(p.tables,studio.clone(UNIT_BONUS_MODEL_DEFAULTS));}return sync(M.migrate(p,defaults));},decorate:sync,updated(p,name){if(name==='BonusDefinition'||name==='BonusEffect')sync(p);},validate(p){try{M.validate(p);return [];}catch(error){return [{table:'BonusDefinition',index:0,field:'DescriptionTemplate',severity:'error',message:error.message}];}},label(text,name,row){if(name==='BonusDefinition'){const unit=units.find(u=>(u.UnitKey||u.UnitId)===row.UnitKey);return (unit?.Name||row.UnitKey)+' · Lv.'+row.Level;}return text;}};
};
