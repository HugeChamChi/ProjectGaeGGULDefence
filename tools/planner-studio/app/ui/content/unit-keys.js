'use strict';
globalThis.UnitKeysFeature = ({studio: Studio}) => {
  const S=STUDIO_SCHEMAS,field=(key,label,type='text',extra={})=>({key,label,type,...extra});
  S.UnitBaseStat={label:'기본 스탯',keys:['BaseStatKey','Tier'],fields:[field('BaseStatKey','기본 스탯 키','id',{required:true}),field('Tier','등급','enum',{values:['normal','rare','epic','legend']}),...['Attack','AttackInterval','SkillCooldown','FoodProduction'].map(k=>field(k,{Attack:'공격력',AttackInterval:'공격 속도(회/초)',SkillCooldown:'스킬 쿨타임(초)',FoodProduction:'초당 식량'}[k],'float',{min:0})),field('MaxDroneCount','최대 드론 수','int',{min:0,max:2147483647})],note:'실제 UnitData의 등급별 원본 스탯입니다. 강화 시안 배율과 구분합니다.'};
  STUDIO_SEED.tables.UnitBaseStat=Studio.clone(UNIT_BASE_STATS);
  // Only new project defaults use authored descriptions; saved planning drafts keep their edits.
  for(const bonus of UNIT_AUTHORED_BONUSES){
    const existing=STUDIO_SEED.tables.UnitMilestone.find(row=>row.UnitId===bonus.UnitId&&row.Level===bonus.Level);
    if(existing)Object.assign(existing,bonus);
    else STUDIO_SEED.tables.UnitMilestone.push({...bonus,EffectStatus:'DescriptionOnly',EffectId:'',EffectParams:'{}'});
  }
  PlannerScope.active.add('UnitBaseStat');STUDIO_NAV.find(n=>n.id==='units').tables.push('UnitBaseStat');
  for(const [key,label]of [['UnitKey','유닛 키'],['BaseStatKey','기본 스탯 키'],['UpgradeKey','강화 성장 키'],['LevelBonusKey','레벨 보너스 키']])S.Unit.fields.push(field(key,label));
  function decorate(p){
    if(!p.tables.UnitBaseStat)p.tables.UnitBaseStat=Studio.clone(UNIT_BASE_STATS);
    for(const u of p.tables.Unit){
      if(u.UnitKey===undefined)u.UnitKey=u.UnitId;
      if(u.BaseStatKey===undefined)u.BaseStatKey=u.Kind==='Unit'?'base_'+u.UnitId:'';
      if(u.UpgradeKey===undefined)u.UpgradeKey=p.tables.UnitUpgrade.some(r=>r.UnitId===u.UnitId)?'growth_'+u.UnitId:'';
      if(u.LevelBonusKey===undefined)u.LevelBonusKey=p.tables.UnitMilestone.some(r=>r.UnitId===u.UnitId)?'bonus_'+u.UnitId:'';
    }return p;
  }
  decorate(STUDIO_SEED);
  function prepare(p){const copy=Studio.clone(p);if(!copy.tables.UnitBaseStat)copy.tables.UnitBaseStat=Studio.clone(UNIT_BASE_STATS);for(const u of copy.tables.Unit){const original=STUDIO_SEED.tables.Unit.find(x=>x.UnitId===u.UnitId);for(const k of ['UnitKey','BaseStatKey','UpgradeKey','LevelBonusKey'])if(!Object.hasOwn(u,k))u[k]=original?.[k]??(k==='UnitKey'?u.UnitId:'');}return copy;}
  const headers={
    Units:['UnitKey','Name','Kind','BackendKeys','IconKey','MaxLevel','BaseStatKey','UpgradeKey','LevelBonusKey'],
    BaseStats:S.UnitBaseStat.fields.map(f=>f.key==='AttackInterval'?'AttackFrequency':f.key),
    UpgradeStats:['UpgradeKey','TargetLevel','Costs','MultiplierIncrease','GlobalBonusIncrease'],
    LevelBonuses:['LevelBonusKey','Level','Description','EffectStatus','EffectKey','EffectParams']
  };
  function tables(p){
    const out={Units:[headers.Units,...p.tables.Unit.map(u=>[u.UnitKey,u.Name,u.Kind,u.BackendIds,u.IconKey,u.MaxLevel,u.BaseStatKey,u.UpgradeKey,u.LevelBonusKey])],BaseStats:[headers.BaseStats,...p.tables.UnitBaseStat.map(r=>headers.BaseStats.map(k=>r[k==='AttackFrequency'?'AttackInterval':k]))],UpgradeStats:[headers.UpgradeStats],LevelBonuses:[headers.LevelBonuses]};
    const growth=new Map(),bonus=new Map();
    for(const u of p.tables.Unit){
      const levels=p.tables.UnitUpgrade.filter(r=>r.UnitId===u.UnitId).sort((a,b)=>a.TargetLevel-b.TargetLevel);let multiplier=1,global=0;
      for(const r of levels){const row=[u.UpgradeKey,r.TargetLevel,Studio.serialize(r.Costs),Number((r.Multiplier-multiplier).toPrecision(12)),Number((r.GlobalBonus-global).toPrecision(12))];multiplier=r.Multiplier;global=r.GlobalBonus;
        const key=u.UpgradeKey+'|'+r.TargetLevel;if(growth.has(key)&&JSON.stringify(growth.get(key))!==JSON.stringify(row))throw Error('공유 강화 키의 수치가 서로 다릅니다: '+key);growth.set(key,row);
      }
      for(const r of p.tables.UnitMilestone.filter(r=>r.UnitId===u.UnitId)){const row=[u.LevelBonusKey,r.Level,r.Description,r.EffectStatus,r.EffectId,r.EffectParams],key=u.LevelBonusKey+'|'+r.Level;if(bonus.has(key)&&JSON.stringify(bonus.get(key))!==JSON.stringify(row))throw Error('공유 보너스 키의 내용이 서로 다릅니다: '+key);bonus.set(key,row);}
    }
    out.UpgradeStats.push(...growth.values());out.LevelBonuses.push(...bonus.values());return out;
  }
  function validate(p){
    const issues=[],keys=new Set();
    p.tables.Unit.forEach((u,index)=>{
      const add=message=>issues.push({table:'Unit',index,field:'UnitKey',message,severity:'error'});
      if(!/^[A-Za-z_][A-Za-z0-9_.-]*$/.test(u.UnitKey)||keys.has(u.UnitKey))add('유닛 키는 영문 문자열 키여야 하며 중복될 수 없습니다.');keys.add(u.UnitKey);
      for(const key of ['BaseStatKey','UpgradeKey','LevelBonusKey'])if(u[key]&&!/^[A-Za-z_][A-Za-z0-9_.-]*$/.test(u[key]))add(key+'는 영문 문자열 키여야 합니다.');
      if(u.BaseStatKey&&!p.tables.UnitBaseStat.some(r=>r.BaseStatKey===u.BaseStatKey))add('연결된 기본 스탯 키가 없습니다.');
      if(u.Kind==='Unit'&&!u.BaseStatKey)add('유닛에는 기본 스탯 키가 필요합니다.');
      if(u.BaseStatKey&&['normal','rare','epic','legend'].some(tier=>!p.tables.UnitBaseStat.some(r=>r.BaseStatKey===u.BaseStatKey&&r.Tier===tier)))add('기본 스탯은 4개 등급이 모두 필요합니다.');
      if(u.UpgradeKey&&!p.tables.UnitUpgrade.some(r=>r.UnitId===u.UnitId))add('연결된 강화 성장 데이터가 없습니다.');
      if(u.LevelBonusKey&&!p.tables.UnitMilestone.some(r=>r.UnitId===u.UnitId))add('연결된 레벨 보너스 데이터가 없습니다.');
      if(!u.UpgradeKey&&p.tables.UnitUpgrade.some(r=>r.UnitId===u.UnitId))add('강화 수치가 있는 유닛에는 강화 키가 필요합니다.');
      if(!u.LevelBonusKey&&p.tables.UnitMilestone.some(r=>r.UnitId===u.UnitId))add('보너스가 있는 유닛에는 보너스 키가 필요합니다.');
      const levels=p.tables.UnitUpgrade.filter(r=>r.UnitId===u.UnitId).sort((a,b)=>a.TargetLevel-b.TargetLevel);
      if(u.UpgradeKey&&(levels.length!==u.MaxLevel-1||levels.some((r,i)=>r.TargetLevel!==i+2)))add('강화 성장 표는 Lv.2부터 최대 레벨까지 연속이어야 합니다.');
    });
    try{tables(p);}catch(e){issues.push({table:'Unit',index:0,field:'UpgradeKey',message:e.message,severity:'error'});}return issues;
  }
  const fields={
    Units:[field('UnitKey','유닛 키','id',{required:true}),field('Name','이름','text',{required:true}),field('Kind','종류','enum',{values:['Unit','Chief']}),field('BackendKeys','서버 매핑'),field('IconKey','아이콘','icon'),field('MaxLevel','최대 레벨','int',{min:1,max:1000}),field('BaseStatKey','기본 스탯 키'),field('UpgradeKey','강화 키'),field('LevelBonusKey','보너스 키')],
    BaseStats:S.UnitBaseStat.fields.map(f=>f.key==='AttackInterval'?{...f,key:'AttackFrequency',unit:'attacks/second',legacyAliases:['AttackInterval']}:f),
    UpgradeStats:[field('UpgradeKey','강화 키','id',{required:true}),field('TargetLevel','목표 레벨','int',{min:2,max:1000}),field('Costs','비용','costs'),field('MultiplierIncrease','배율 증가량','float'),field('GlobalBonusIncrease','전체 보너스 증가량','float')],
    LevelBonuses:[field('LevelBonusKey','보너스 키','id',{required:true}),field('Level','해금 레벨','int',{min:1,max:1000}),field('Description','설명','text',{required:true}),...S.UnitMilestone.fields.filter(f=>['EffectStatus','EffectParams'].includes(f.key)),field('EffectKey','효과 키')]
  };
  const sheetFor={Unit:'Units',UnitBaseStat:'BaseStats',UnitUpgrade:'UpgradeStats',UnitMilestone:'LevelBonuses'};
  fields.LevelBonuses.sort((a,b)=>headers.LevelBonuses.indexOf(a.key)-headers.LevelBonuses.indexOf(b.key));
  function propagate(p,id,bonus=false){const unit=p.tables.Unit.find(u=>u.UnitId===id);const key=bonus?'LevelBonusKey':'UpgradeKey',table=bonus?'UnitMilestone':'UnitUpgrade';for(const other of p.tables.Unit){if(other===unit)continue;if(unit[key]&&other[key]===unit[key]){const rows=p.tables[table].filter(r=>r.UnitId===id).map(r=>({...Studio.clone(r),UnitId:other.UnitId})),indices=p.tables[table].map((r,i)=>r.UnitId===other.UnitId?i:-1).filter(i=>i>=0);if(rows.length===indices.length)indices.forEach((index,i)=>Object.assign(p.tables[table][index],rows[i]));else p.tables[table]=p.tables[table].filter(r=>r.UnitId!==other.UnitId).concat(rows);}}}
  globalThis.UnitKeys={headers,fields,tables,decorate,sheetFor,propagate};
  return {
    prepare,decorate,migrate:decorate,validate,
    updated(p,name,index){if(name==='UnitMilestone')propagate(p,p.tables[name][index].UnitId,true);},
    tables(result,p){for(const name of Object.keys(sheetFor))delete result[name];return {...result,...tables(p)};}
  };
};
