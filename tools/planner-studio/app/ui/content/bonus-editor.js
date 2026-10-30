'use strict';
globalThis.BonusEditorFeature=({studio})=>{
 const modelVersion='html-20261030';STUDIO_SEED.meta.unitBonusModel=modelVersion;
 const field=(key,label,type='text',extra={})=>({key,label,type,...extra});
 const units=[...new Set([...STUDIO_SEED.tables.Unit.filter(u=>u.Kind==='Unit').map(u=>u.UnitKey||u.UnitId),...(typeof UNIT_BONUS_DEFAULTS==='undefined'?[]:UNIT_BONUS_DEFAULTS.map(r=>r.UnitKey))])];
 const effects=['StatModifierBonus','EveryNthCycleBonus'];
 const stats=['Attack','BasicAttackFrequency','CriticalChance','CriticalDamage','SkillCooldownReduction','FoodProduction','MaxDroneCount','ChargeAttackReduction','HackingCapacity','HackingPostSpeed','HackingPostDuration','HackingStackDamage','RecoveryNextBasic','TimeRecovery','BasicDamage','FoodSpeed','BossFoodSeconds','FoodFifthMultiplier','ChiefCooldown','ChiefStartsReady','ChiefGlobalAttack','ChiefSpeed','ChiefSpeedDuration','ChiefDamageTaken','ChiefDamageTakenDuration','HackingProducedNormal','HackingProducedRare','HackingProducedEpic','HackingProducedLegend','HackingConsumedNormal','HackingConsumedRare','HackingConsumedEpic','HackingConsumedLegend'];
 STUDIO_SCHEMAS.BonusEffect={label:'보너스 능력',keys:['UnitKey','Level','PartIndex'],note:'스탯 증가를 선택하고 스탯·단위·수치를 입력하세요. Percent=5는 5%입니다. 같은 레벨에 효과를 추가하려면 순번을 0,1로 나누세요. 주기 효과는 베탕 기본 자폭만 지원합니다.',fields:[field('UnitKey','대상 유닛','enum',{values:units}),field('Level','해금 레벨','int',{min:1,max:1000}),field('PartIndex','효과 순번 (0부터)','int',{min:0}),field('EffectType','능력 종류','enum',{values:effects,labels:{StatModifierBonus:'스탯 증가',EveryNthCycleBonus:'N번째 기본 행동 강화'}}),field('Stat','증가할 스탯','enum',{values:stats,labels:{Attack:'공격력',BasicAttackFrequency:'일반 공격속도',CriticalChance:'치명타 확률',CriticalDamage:'치명타 피해',SkillCooldownReduction:'스킬 쿨타임 감소',FoodProduction:'초당 식량',MaxDroneCount:'최대 드론 수',ChargeAttackReduction:"스킬 충전 필요 공격 횟수 감소",HackingCapacity:"공유 해킹 최대 스택 증가 (델탕 중 최댓값)",HackingPostSpeed:"해킹 후 자신의 공격속도 증가",HackingPostDuration:"해킹 후 공격속도 지속시간 (초)",HackingStackDamage:"해킹 스택 부분 피해 증가",RecoveryNextBasic:"시간 회복 후 다음 정규 일반공격 피해 증가",TimeRecovery:"시간 회복량 증가",BasicDamage:"일반공격 피해 증가",FoodSpeed:"자체 식량 생산속도 증가",BossFoodSeconds:"보스 처치 시 자체 식량 지급 (초분)",FoodFifthMultiplier:"매 5번째 자체 식량 배율 (높은 레벨로 교체)",ChiefCooldown:"알팡 액티브 쿨타임 감소",ChiefStartsReady:"알팡 전투 시작 충전 (1=완료)",ChiefGlobalAttack:"알팡 전체 유닛 공격력 증가",ChiefSpeed:"알팡 스킬 후 전체 유닛 공격속도 증가",ChiefSpeedDuration:"알팡 공격속도 지속시간 (초)",ChiefDamageTaken:"알팡 집결 적중 후 보스 받는 피해 증가",ChiefDamageTakenDuration:"알팡 받는 피해 증가 지속시간 (초)",HackingProducedNormal:"노말 해킹 추가 생산",HackingProducedRare:"레어 해킹 추가 생산",HackingProducedEpic:"에픽 해킹 추가 생산",HackingProducedLegend:"전설 해킹 추가 생산",HackingConsumedNormal:"노말 해킹 추가 소모 한도",HackingConsumedRare:"레어 해킹 추가 소모 한도",HackingConsumedEpic:"에픽 해킹 추가 소모 한도",HackingConsumedLegend:"전설 해킹 추가 소모 한도"}}),field('ValueMode','수치 단위','enum',{values:['Flat','Percent','PercentagePoints'],labels:{Flat:'고정 수치',Percent:'퍼센트 (%)',PercentagePoints:'직접 가산 (%)'}}),field('Value','증가/감소 수치','float',{min:0}),field('CycleId','행동 키 (주기 효과만)'),field('Every','몇 번째 행동','int',{min:0}),field('DamageBonusPercent','주기 피해 증가 (%)','float',{min:0}),field('ChargeReductionPercent','주기 충전 감소 (%)','float',{min:0,max:99.99})]};
 STUDIO_SCHEMAS.BonusDescription={label:'보너스 설명·알팡 링크',keys:['UnitKey','Level'],note:'[해킹], [자폭드론], [액티브 스킬]은 용어 링크입니다. {공격속도효과}는 알팡 효과 툴팁 링크입니다. 설명은 실제 능력을 바꾸지 않습니다. 숫자는 자동 강조되며 아래 옵션으로 끌 수 있습니다.',fields:[field('UnitKey','대상 유닛·족장','enum',{values:[...new Set([...units,'AlphanChief'])]}),field('Level','표시할 해금 레벨','int',{min:1,max:1000}),field('Description','표시할 설명','textarea',{required:true}),field('HighlightNumbers','숫자 주황색 강조','bool'),field('LinkTerms','용어 링크 사용','bool'),field('EffectLink','특수 효과 링크','enum',{values:['None','ChiefAttackSpeed'],labels:{None:'없음',ChiefAttackSpeed:'알팡 공격속도 효과'}})]};
 STUDIO_SEED.tables.BonusEffect=typeof UNIT_BONUS_DEFAULTS==='undefined'?[]:UNIT_BONUS_DEFAULTS.map(r=>({...r,Stat:r.Stat||'Attack',ValueMode:r.ValueMode||'Percent',Value:r.Value===''?0:r.Value,Every:r.Every===''?0:r.Every,DamageBonusPercent:r.DamageBonusPercent===''?0:r.DamageBonusPercent,ChargeReductionPercent:r.ChargeReductionPercent===''?0:r.ChargeReductionPercent}));
 STUDIO_SEED.tables.BonusDescription=UNIT_BONUS_DESCRIPTIONS.map(r=>({...r}));
 function syncMilestones(p){
  for(const text of p.tables.BonusDescription||[]){const unit=p.tables.Unit.find(u=>(u.UnitKey||u.UnitId)===text.UnitKey);if(!unit)continue;const parts=(p.tables.BonusEffect||[]).filter(r=>r.UnitKey===text.UnitKey&&r.Level===text.Level).sort((a,b)=>a.PartIndex-b.PartIndex);if(!parts.length)continue;
   let row=p.tables.UnitMilestone.find(r=>r.UnitId===unit.UnitId&&r.Level===text.Level);if(!row){row={UnitId:unit.UnitId,Level:text.Level};p.tables.UnitMilestone.push(row);}Object.assign(row,{Description:text.Description,EffectStatus:'Defined',EffectId:'CompositeBonus',EffectParams:JSON.stringify({Parts:parts.map(({UnitKey,Level,PartIndex,...part})=>part)})});
  }return p;
 }
 syncMilestones(STUDIO_SEED);
 STUDIO_SCHEMAS.UnitMilestone.note='보너스 능력·보너스 설명 탭에서 설정한 실제 효과의 요약입니다. 수치와 설명은 해당 탭에서 수정하세요.';
 for(const name of ['BonusEffect','BonusDescription']){PlannerScope.active.add(name);STUDIO_NAV.find(n=>n.id==='units').tables.push(name);}
 return {prepare(p){const copy=studio.clone(p);for(const name of ['BonusEffect','BonusDescription'])if(!Object.hasOwn(copy.tables,name))copy.tables[name]=studio.clone(STUDIO_SEED.tables[name]);const fields=['UnitKey','Level','PartIndex','EffectType','Stat','ValueMode','Value','CycleId','Every','DamageBonusPercent','ChargeReductionPercent'];
 const normalized=r=>fields.map(k=>String(r[k]??'')===''?['Every','DamageBonusPercent','ChargeReductionPercent','Value'].includes(k)?'0':k==='Stat'?'Attack':k==='ValueMode'?'Percent':'':String(r[k]));
 if(copy.meta?.unitBonusModel!==modelVersion){
 for(const legacy of UNIT_BONUS_PREVIOUS_DEFAULTS){
  const group=copy.tables.BonusEffect.filter(r=>r.UnitKey===legacy.UnitKey&&r.Level===legacy.Level);
  if(group.length===1&&JSON.stringify(normalized(group[0]))===JSON.stringify(normalized(legacy))){copy.tables.BonusEffect.splice(copy.tables.BonusEffect.indexOf(group[0]),1,...studio.clone(STUDIO_SEED.tables.BonusEffect.filter(r=>r.UnitKey===legacy.UnitKey&&r.Level===legacy.Level)));}
 }
 for(const row of STUDIO_SEED.tables.BonusEffect.filter(r=>r.UnitKey==='AlphanChief'))if(!copy.tables.BonusEffect.some(r=>r.UnitKey===row.UnitKey&&r.Level===row.Level&&r.PartIndex===row.PartIndex))copy.tables.BonusEffect.push(studio.clone(row));
 const legacyChief=['[액티브 스킬] 쿨타임 10% 감소','패턴 무효화 시 남은 쿨타임 15% 감소','전체 유닛 공격력 5% 증가','[액티브 스킬] 사용 시 전체 유닛 {공격속도효과}','전체 유닛 스킬 쿨타임 10% 감소'];
 for(const row of STUDIO_SEED.tables.BonusDescription){const index=copy.tables.BonusDescription.findIndex(r=>r.UnitKey===row.UnitKey&&r.Level===row.Level);if(index<0)copy.tables.BonusDescription.push(studio.clone(row));else if(row.UnitKey==='AlphanChief'&&copy.tables.BonusDescription[index].Description===legacyChief[row.Level/3-1])copy.tables.BonusDescription[index]=studio.clone(row);}
 }
 copy.tables.BonusEffect=copy.tables.BonusEffect.map(r=>({...r,Stat:r.Stat||'Attack',ValueMode:r.ValueMode||'Percent',Value:r.Value===''?0:r.Value,Every:r.Every===''?0:r.Every,DamageBonusPercent:r.DamageBonusPercent===''?0:r.DamageBonusPercent,ChargeReductionPercent:r.ChargeReductionPercent===''?0:r.ChargeReductionPercent}));
 return syncMilestones(copy);},decorate:syncMilestones,updated(p,name){if(name==='BonusEffect'||name==='BonusDescription')syncMilestones(p);},validate(p){const issues=[];for(const [index,row]of p.tables.BonusDescription.entries()){if(/[<>]/.test(row.Description))issues.push({table:'BonusDescription',index,field:'Description',severity:'error',message:'TMP 태그 대신 [용어]와 {공격속도효과}를 사용하세요.'});if(row.EffectLink==='ChiefAttackSpeed'&&row.UnitKey!=='AlphanChief')issues.push({table:'BonusDescription',index,field:'EffectLink',severity:'error',message:'알팡 전용 링크입니다.'});}return issues;}};
};
if(typeof document!=='undefined'){
 let last='',lastNode;
 function preview(){
  const editor=document.getElementById('editor');if(!editor)return;
  const description=editor.querySelector('[data-field="Description"]'),effect=editor.querySelector('[data-field="EffectLink"]');if(!description||!effect)return;
  const number=editor.querySelector('[data-field="HighlightNumbers"]')?.checked,terms=editor.querySelector('[data-field="LinkTerms"]')?.checked;
  const state=JSON.stringify([description.value,effect.value,number,terms]);if(state===last&&lastNode?.isConnected)return;
  let box=document.getElementById('bonus-text-preview');if(!box){box=document.createElement('div');box.id='bonus-text-preview';box.className='preview';editor.appendChild(box);}last=state;lastNode=box;box.replaceChildren();
  const heading=document.createElement('strong');heading.textContent='게임 설명 미리보기';box.appendChild(heading);const text=document.createElement('p');box.appendChild(text);
  for(const part of description.value.replaceAll('%p','%').split(/(\[(?:해킹|자폭드론|액티브 스킬)\]|\{공격속도효과\}|[+-]?\d+(?:\.\d+)?(?:%p?|초|회|배)?)/g)){
   const span=document.createElement('span');span.textContent=part;
   if(number&&/^[+-]?\d/.test(part))span.style.color='#FFA64D';
   if(terms&&/^\[/.test(part)){span.style.color='#FFDD8F';span.title='게임에서 용어 설명 열기';}
   if(part==='{공격속도효과}'&&effect.value==='ChiefAttackSpeed'){span.textContent='공격속도 증가';span.style.color='#91E6A6';span.title='게임에서 알팡 공격속도 효과 툴팁 열기';}
   text.appendChild(span);
  }
 }
 document.addEventListener('input',preview);document.addEventListener('change',preview);
 const content=document.getElementById('content');if(content)new MutationObserver(preview).observe(content,{childList:true,subtree:true});
}
