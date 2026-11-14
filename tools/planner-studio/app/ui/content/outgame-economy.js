/* The planner preview mirrors Function/OutgameEconomy.cs. No Excel formula cells. */
'use strict';
globalThis.OutgameEconomyFeature=({studio:C})=>{
  const defaults={RuleKey:'HpLinkedV1',BaseGold:60,BaseToken:6,HpExponent:0.2,
    UnitCoefficient:122.83401441574097,UnitExponent:1.831298828125,UnitSoftCap:14,UnitTailSlope:0.03,UnitRound:10,
    ResearchBase:200,ResearchRowRatio:1.6145706176757812,ResearchStepRatio:0.25,ResearchRound:50,
    ReferenceGain:0.002,WeightMin:0.5,WeightMax:3,UtilityWeight:1,CritChance:0.3,CritMultiplier:2,SkillShare:0.5};
  const groups=[['전투 보상',[['BaseGold','기본 골드',0,1000000],['BaseToken','기본 강화석',0,1000000],['HpExponent','체력 보상 지수',0,1]]],
    ['일반 유닛 강화석',[['UnitCoefficient','비용 계수 A',0.000001,1000000],['UnitExponent','비용 지수 p',0,10],['UnitSoftCap','완만해지는 k',1,19],['UnitTailSlope','후반 증가율',0,10],['UnitRound','반올림 단위',1,1000000]]],
    ['BASIC 영구강화 골드',[['ResearchBase','첫 행 기본 비용',1,1000000],['ResearchRowRatio','행당 배율 q',1,10],['ResearchStepRatio','단계당 증가 비율',0,10],['ResearchRound','반올림·최소 단위',1,1000000],['ReferenceGain','기준 DPS 증가율',0.000001,1],['WeightMin','최소 가중치',0.000001,100],['WeightMax','최대 가중치',0.000001,100],['UtilityWeight','식량·토템 임시 가중치',0.000001,100],['CritChance','기준 치명타 확률',0,1],['CritMultiplier','기준 치명타 배율',1,100],['SkillShare','시간충전 스킬 피해 비중',0,1]]]];
  const integers=new Set(['BaseGold','BaseToken','UnitSoftCap','UnitRound','ResearchRound']);
  const fields=[{key:'RuleKey',label:'공식 버전',type:'enum',values:['HpLinkedV1']},...groups.flatMap(([,items])=>items.map(([key,label,min,max])=>({key,label,type:integers.has(key)?'int':'float',min,max})))];
  STUDIO_SCHEMAS.OutgameEconomy={label:'체력 연동 경제',keys:['RuleKey'],fields,note:'보스 등장 최대 HP와 전투 중 최저 HP로 계산합니다. 재화별 합산·연구 보너스 적용 후 한 번 내림. 일반 유닛 Lv20과 현재 BASIC 트리만 대상입니다.'};
  STUDIO_SEED.tables.OutgameEconomy=[C.clone(defaults)];
  PlannerScope.active.add('OutgameEconomy');
  STUDIO_NAV.push({id:'outgameEconomy',label:'체력 연동 경제',icon:'◉',tables:['OutgameEconomy']});
  // Cost fields are now an authoring/export contract. Existing drafts retain their costs.
  for(const f of STUDIO_SCHEMAS.ResearchNode.fields)if(['CostItemId','BaseCost','CostStep'].includes(f.key))f.hidden=false;
  STUDIO_SCHEMAS.ResearchNode.note='BASIC의 비용은 체력 연동 경제에서 생성하거나 직접 편집합니다. 비용표의 게임 차감 연결과 Function 배포는 별도입니다.';
  const round=(x,unit)=>Math.floor(x/unit+0.5)*unit;
  function amount(x){if(!Number.isSafeInteger(x)||x<0||x>2147483647)throw Error('계산 결과가 재화 정수 범위를 벗어납니다.');return x;}
  function unitCost(c,target){
    if(!Number.isInteger(target)||target<2||target>20)throw Error('일반 유닛 목표 레벨은 2~20입니다.');
    const k=target-1,base=c.UnitCoefficient*Math.pow(Math.min(k,c.UnitSoftCap),c.UnitExponent);
    return amount(round(base*(k>c.UnitSoftCap?1+c.UnitTailSlope*(k-c.UnitSoftCap):1),c.UnitRound));
  }
  function weight(c,n){
    const v=n.ValuePerLevel,crit=1+c.CritChance*(c.CritMultiplier-1);let gain;
    switch(n.Stat){
      case 'FoodProduction':case 'StartFood':case 'TotemEffect':return c.UtilityWeight;
      case 'AttackPercent':case 'BossDamage':gain=v;break;
      case 'CritChance':gain=(Math.min(1,c.CritChance+v)-c.CritChance)*(c.CritMultiplier-1)/crit;break;
      case 'CritDamage':gain=c.CritChance*v/crit;break;
      case 'AttackSpeedPercent':gain=(1-c.SkillShare)*v;break;
      case 'SkillDamage':gain=c.SkillShare*v;break;
      case 'SkillCooldownReduction':if(v>=1)throw Error('쿨타임 감소는 1 미만이어야 합니다.');gain=c.SkillShare*(1/(1-v)-1);break;
      default:throw Error('비용 가중치가 정의되지 않은 BASIC 스탯: '+n.Stat);
    }
    return Math.max(c.WeightMin,Math.min(c.WeightMax,gain/c.ReferenceGain));
  }
  function researchCost(c,n){
    const raw=c.ResearchBase*Math.pow(c.ResearchRowRatio,n.Row)*weight(c,n);
    const BaseCost=amount(Math.max(c.ResearchRound,round(raw,c.ResearchRound))),CostStep=amount(Math.max(c.ResearchRound,round(raw*c.ResearchStepRatio,c.ResearchRound)));
    amount(BaseCost+CostStep*(n.MaxLevel-1));
    return {CostItemId:'Gold',BaseCost,CostStep};
  }
  function reward(c,referenceHp,bosses,goldBonus=0,tokenBonus=0){
    if(!Number.isFinite(referenceHp)||referenceHp<=0||![goldBonus,tokenBonus].every(x=>Number.isFinite(x)&&x>=0))throw Error('기준 체력과 보너스를 확인하세요.');
    let total=0;
    for(const b of bosses){
      if(!Number.isFinite(b.maxHp)||b.maxHp<=0||!Number.isFinite(b.minHp))throw Error('보스 등장 최대/전투 중 최저 체력이 필요합니다.');
      total+=Math.pow(b.maxHp/referenceHp,c.HpExponent)*Math.max(0,Math.min(1,(b.maxHp-b.minHp)/b.maxHp));
    }
    return {Gold:amount(Math.floor(c.BaseGold*total*(1+goldBonus))),UpgradeTokenA:amount(Math.floor(c.BaseToken*total*(1+tokenBonus)))};
  }
  function config(p){if(p.tables.OutgameEconomy.length!==1)throw Error('체력 연동 경제 설정은 한 행이어야 합니다.');return p.tables.OutgameEconomy[0];}
  function validate(p){
    const issues=[],add=(message,field='RuleKey')=>issues.push({table:'OutgameEconomy',index:0,field,message,severity:'error'});
    try{
      const c=config(p);
      if(c.WeightMin>c.WeightMax)add('최소 가중치는 최대 가중치 이하여야 합니다.','WeightMin');
      for(let l=2;l<=20;l++)if(unitCost(c,l)<=0)add('유닛 비용은 양의 정수여야 합니다.','UnitCoefficient');
    }catch(e){add(e.message);}
    return issues;
  }
  function apply(p){
    const errors=C.validate(p).filter(i=>i.severity==='error');if(errors.length)throw Error(errors[0].message);
    const c=config(p),nodes=p.tables.ResearchNode.filter(n=>n.TreeKey==='BASIC'),units=p.tables.Unit.filter(u=>u.Kind==='Unit');
    // Stage a complete result first, so an unsupported tree/level never leaves a partial edit.
    if(nodes.length!==21||nodes.reduce((s,n)=>s+n.MaxLevel,0)!==99)throw Error('현재 BASIC 21노드·99회 강화 전용입니다. 다른 트리는 별도 비용 기획이 필요합니다.');
    if(units.some(u=>u.MaxLevel!==20))throw Error('일반 유닛 최대 레벨을 20으로 설정한 뒤 생성하세요.');
    const costs=units.flatMap(u=>Array.from({length:19},(_,i)=>{
      const old=p.tables.UnitUpgrade.find(r=>r.UnitId===u.UnitId&&r.TargetLevel===i+2);
      return {...(old||{Multiplier:1,GlobalBonus:0,Source:'체력 연동 경제'}),UnitId:u.UnitId,TargetLevel:i+2,Costs:[{itemId:'UpgradeTokenA',amount:unitCost(c,i+2)}]};
    }));
    const research=nodes.map(n=>[n,researchCost(c,n)]),ids=new Set(units.map(u=>u.UnitId));
    p.tables.UnitUpgrade=p.tables.UnitUpgrade.filter(r=>!ids.has(r.UnitId)).concat(costs);
    for(const [n,cost]of research)Object.assign(n,cost);
  }
  globalThis.OutgameEconomy={defaults,groups,config,unitCost,researchCost,weight,reward,apply};
  // Fresh projects start with the proposal. prepare() only adds missing settings to saved drafts.
  for(const n of STUDIO_SEED.tables.ResearchNode)n.RequireAllParentsMax??=false;
  for(const n of STUDIO_SEED.tables.ResearchNode.filter(n=>n.TreeKey==='BASIC'))Object.assign(n,researchCost(defaults,n));
  const normalIds=new Set(STUDIO_SEED.tables.Unit.filter(u=>u.Kind==='Unit').map(u=>u.UnitId));
  for(const r of STUDIO_SEED.tables.UnitUpgrade)if(normalIds.has(r.UnitId))r.Costs=[{itemId:'UpgradeTokenA',amount:unitCost(defaults,r.TargetLevel)}];
  return {prepare(p){const q=C.clone(p);q.tables.OutgameEconomy??=[C.clone(defaults)];return q;},validate,
    decorate(p){p.meta.statuses.OutgameEconomy='전투 보상과 아웃게임 비용의 공식·계수. 서버 Function용 계산 소스와 차트를 출력합니다.';return p;}};
};
