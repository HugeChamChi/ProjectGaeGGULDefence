'use strict';
globalThis.BonusEditorFeature=({studio})=>{
 const field=(key,label,type='text',extra={})=>({key,label,type,...extra});
 const units=[...new Set([...STUDIO_SEED.tables.Unit.filter(u=>u.Kind==='Unit').map(u=>u.UnitKey||u.UnitId),...(typeof UNIT_BONUS_DEFAULTS==='undefined'?[]:UNIT_BONUS_DEFAULTS.map(r=>r.UnitKey))])];
 const effects=['StatModifierBonus','EveryNthCycleBonus'];
 const stats=['Attack','BasicAttackFrequency','CriticalChance','CriticalDamage','SkillCooldownReduction','FoodProduction','MaxDroneCount'];
 STUDIO_SCHEMAS.BonusEffect={label:'보너스 능력',keys:['UnitKey','Level','PartIndex'],note:'스탯 증가를 선택하고 스탯·단위·수치를 입력하세요. Percent=5는 5%입니다. 같은 레벨에 효과를 추가하려면 순번을 0,1로 나누세요. 주기 효과는 베탕 기본 자폭만 지원합니다.',fields:[field('UnitKey','대상 유닛','enum',{values:units}),field('Level','해금 레벨','int',{min:1,max:1000}),field('PartIndex','효과 순번 (0부터)','int',{min:0}),field('EffectType','능력 종류','enum',{values:effects,labels:{StatModifierBonus:'스탯 증가',EveryNthCycleBonus:'N번째 기본 행동 강화'}}),field('Stat','증가할 스탯','enum',{values:stats,labels:{Attack:'공격력',BasicAttackFrequency:'일반 공격속도',CriticalChance:'치명타 확률',CriticalDamage:'치명타 피해',SkillCooldownReduction:'스킬 쿨타임 감소',FoodProduction:'초당 식량',MaxDroneCount:'최대 드론 수'}}),field('ValueMode','수치 단위','enum',{values:['Flat','Percent','PercentagePoints'],labels:{Flat:'고정 수치',Percent:'퍼센트 (%)',PercentagePoints:'직접 가산 (%)'}}),field('Value','증가/감소 수치','float',{min:0}),field('CycleId','행동 키 (주기 효과만)'),field('Every','몇 번째 행동','int',{min:0}),field('DamageBonusPercent','주기 피해 증가 (%)','float',{min:0}),field('ChargeReductionPercent','주기 충전 감소 (%)','float',{min:0,max:99.99})]};
 STUDIO_SCHEMAS.BonusDescription={label:'보너스 설명·알팡 링크',keys:['UnitKey','Level'],note:'[해킹], [자폭드론], [액티브 스킬]은 용어 링크입니다. {공격속도효과}는 알팡 효과 툴팁 링크입니다. 설명은 실제 능력을 바꾸지 않습니다. 숫자는 자동 강조되며 아래 옵션으로 끌 수 있습니다.',fields:[field('UnitKey','대상 유닛·족장','enum',{values:[...units,'AlphanChief']}),field('Level','표시할 해금 레벨','int',{min:1,max:1000}),field('Description','표시할 설명','textarea',{required:true}),field('HighlightNumbers','숫자 주황색 강조','bool'),field('LinkTerms','용어 링크 사용','bool'),field('EffectLink','특수 효과 링크','enum',{values:['None','ChiefAttackSpeed'],labels:{None:'없음',ChiefAttackSpeed:'알팡 공격속도 효과'}})]};
 STUDIO_SEED.tables.BonusEffect=typeof UNIT_BONUS_DEFAULTS==='undefined'?[]:UNIT_BONUS_DEFAULTS.map(r=>({...r,Stat:r.Stat||'Attack',ValueMode:r.ValueMode||'Percent',Value:r.Value===''?0:r.Value,Every:r.Every===''?0:r.Every,DamageBonusPercent:r.DamageBonusPercent===''?0:r.DamageBonusPercent,ChargeReductionPercent:r.ChargeReductionPercent===''?0:r.ChargeReductionPercent}));
 const chief=['[액티브 스킬] 쿨타임 10% 감소','패턴 무효화 시 남은 쿨타임 15% 감소','전체 유닛 공격력 5% 증가','[액티브 스킬] 사용 시 전체 유닛 {공격속도효과}','전체 유닛 스킬 쿨타임 10% 감소'];
 const betan=['공격력 +5%','매 2번째 기본 [자폭드론] 피해 +40% (선택지 추가 자폭 제외)','일반공격 속도 +5%','매 2번째 기본 [자폭드론] 충전시간 -30% (선택지 추가 자폭 제외)','유닛 치명타 피해 +50%'];
 STUDIO_SEED.tables.BonusDescription=[...betan.map((Description,i)=>({UnitKey:'UnitData_Betan',Level:(i+1)*3,Description,HighlightNumbers:true,LinkTerms:true,EffectLink:'None'})),...chief.map((Description,i)=>({UnitKey:'AlphanChief',Level:(i+1)*3,Description,HighlightNumbers:true,LinkTerms:true,EffectLink:i===3?'ChiefAttackSpeed':'None'}))];
 for(const name of ['BonusEffect','BonusDescription']){PlannerScope.active.add(name);STUDIO_NAV.find(n=>n.id==='units').tables.push(name);}
 return {prepare(p){const copy=studio.clone(p);for(const name of ['BonusEffect','BonusDescription'])if(!Object.hasOwn(copy.tables,name))copy.tables[name]=studio.clone(STUDIO_SEED.tables[name]);return copy;},validate(p){const issues=[];for(const [index,row]of p.tables.BonusDescription.entries()){if(/[<>]/.test(row.Description))issues.push({table:'BonusDescription',index,field:'Description',severity:'error',message:'TMP 태그 대신 [용어]와 {공격속도효과}를 사용하세요.'});if(row.EffectLink==='ChiefAttackSpeed'&&row.UnitKey!=='AlphanChief')issues.push({table:'BonusDescription',index,field:'EffectLink',severity:'error',message:'알팡 전용 링크입니다.'});}return issues;}};
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
