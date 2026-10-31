'use strict';
globalThis.PlannerScopeFeature = ({studio: Studio}) => {
  const S = STUDIO_SCHEMAS;
  globalThis.STUDIO_NAV = [
    {id:'overview',label:'작업 안내',icon:'◈',tables:[]},
    {id:'research',label:'영구강화',icon:'⌘',tables:['ResearchNode','ResearchTree']},
    {id:'units',label:'유닛강화',icon:'⬡',tables:['Unit','UnitUpgrade','UnitMilestone']},
    {id:'attendance',label:'출석',icon:'▦',tables:['Attendance','AttendancePlan']},
    {id:'party',label:'파티·덱',icon:'◇',tables:['Party']},
    {id:'economy',label:'재화·스태미나',icon:'◉',tables:['Economy']},
    {id:'account',label:'계정 성장',icon:'↗',tables:['AccountLevel','BattleReward']},
    {id:'profile',label:'프로필',icon:'◎',tables:['Profile']},
    {id:'tutorial',label:'튜토리얼',icon:'▤',tables:['Tutorial']}
  ];
  const active = new Set(STUDIO_NAV.flatMap(n=>n.tables));
  const chiefPaths=new Map();
  for(const party of STUDIO_SEED.tables.Party)if(party.ChiefAsset){
    const unit=STUDIO_SEED.tables.Unit.find(u=>u.Kind==='Chief'&&u.Source===party.ChiefAsset);
    chiefPaths.set(unit?.UnitId||party.ChiefAsset.split('/').pop().replace(/\.asset$/,''),party.ChiefAsset);
  }
  const chiefKey=asset=>[...chiefPaths].find(([,path])=>path===asset)?.[0]||'';
  S.Party.fields.splice(S.Party.fields.length-1,0,{key:'ChiefId',label:'족장 키',type:'enum',values:['',...chiefPaths.keys()]});
  for(const party of STUDIO_SEED.tables.Party)party.ChiefId=chiefKey(party.ChiefAsset);
  for(const schema of Object.values(S))for(const f of schema.fields)if(['Source','Note','ChiefAsset','LegacySaveName','GrowthPerLevel'].includes(f.key))f.export=false;
  active.add('Item'); // Shared icon catalogue, not another reward-editing page.
  S.Attendance.fields.splice(S.Attendance.fields.length-1,0,{key:'Rewards',label:'지급 아이템',type:'rewards'});
  S.Attendance.fields.find(f=>f.key==='RewardGroupId').required=false;
  function upgrade(p){
    if(!p?.tables?.Attendance)return p;
    const copy=Studio.clone(p);
    for(const party of copy.tables.Party||[])if(!Object.hasOwn(party,'ChiefId'))party.ChiefId=chiefKey(party.ChiefAsset);
    for(const r of copy.tables.Attendance)if(!Object.hasOwn(r,'Rewards')){
      const group=copy.tables.RewardGroup?.find(g=>g.RewardGroupId===r.RewardGroupId);
      if(r.RewardGroupId&&!group)throw Error('이전 출석 보상 연결을 찾을 수 없습니다: '+r.RewardGroupId);
      r.Rewards=Studio.clone(group?.Rewards||[]);
    }
    return copy;
  }
  for(const r of STUDIO_SEED.tables.Attendance)r.Rewards=Studio.clone(STUDIO_SEED.tables.RewardGroup.find(g=>g.RewardGroupId===r.RewardGroupId)?.Rewards||[]);
  const hidden = {
    ResearchNode:['CostItemId','BaseCost','CostStep'],
    Tutorial:['RewardGroupId'],
    BattleReward:['WinRewardGroupId','LoseRewardGroupId']
  };
  hidden.Attendance=['RewardGroupId'];
  const bonusFields=[{key:'EffectStatus',label:'효과 데이터 상태',type:'enum',values:['DescriptionOnly','Defined'],labels:{DescriptionOnly:'설명만 있음',Defined:'효과 데이터 정의됨'}},{key:'EffectId',label:'효과 키 (정의한 경우)',type:'text'},{key:'EffectParams',label:'효과 수치 JSON',type:'textarea'}];
  S.UnitMilestone.fields.splice(S.UnitMilestone.fields.length-1,0,...bonusFields);
  function upgradeBonuses(p){
    for(const b of p.tables.UnitMilestone){
      if(!b.EffectStatus)b.EffectStatus='DescriptionOnly';
      if(b.EffectId===undefined)b.EffectId='';
      if(!b.EffectParams)b.EffectParams='{}';
    }return p;
  }
  upgradeBonuses(STUDIO_SEED);
  for(const [name,keys] of Object.entries(hidden)) for(const f of S[name].fields) if(keys.includes(f.key)) f.hidden=true;
  for(const schema of Object.values(S)) {
    schema.label=schema.label.replaceAll('영구 강화','영구강화');
    schema.note=schema.note.replaceAll('영구 강화','영구강화');
  }
  Object.assign(S.ResearchNode,{label:'노드·수치·선행 조건',note:'노드를 선택해 강화 수치와 연결을 편집하세요. 퍼센트 수치는 0.02 = 2%입니다. 현재 게임의 영구강화는 무료입니다.'});
  S.ResearchTree.label='트리 기본 설정';
  S.Unit.label='유닛 정보'; S.UnitUpgrade.label='레벨별 비용·성장'; S.UnitMilestone.label='레벨업 보너스';
  S.Item.label='재화 목록'; S.Item.note='현재 지원하는 재화는 골드·다이아·스태미나입니다.';
  S.Item.fields.find(f=>f.key==='ItemType').values=['Currency'];
  S.Attendance.label='일차별 보상'; S.AttendancePlan.label='출석 주기·초기화';
  S.AccountLevel.label='레벨별 필요 경험치'; S.BattleReward.label='승리·패배 경험치';
  S.DynamicShop.label='상점별 유지 시간'; S.MailMapping.label='우편 재화 매핑';
  const statuses = {
    ResearchNode:'영구강화 · 노드 배치 / 수치 / 선행 조건. BASIC은 전투 연결, CHOICE·LANES는 기존 시안입니다.',
    Unit:'유닛강화 · 현재 시안의 유닛·족장 기본값입니다.',
    UnitUpgrade:'유닛강화 · 시안의 비용과 성장 수치를 레벨별로 편집합니다.',
    UnitMilestone:'유닛강화 · 현재 화면에 표시되는 레벨 보너스 설명입니다.',
    RewardGroup:'보상 · 현재 출석 보상에서 가져온 묶음입니다. 아이콘과 수량으로 구성하세요.',
    Item:'보상에 사용하는 재화 목록 · 골드 / 다이아 / 스태미나',
    DynamicShop:'현재 한정 상점 A·B·C의 유지 시간입니다.',
    BattleReward:'현재 전투 결과에서 지급하는 계정 경험치 설정입니다.'
  };
  statuses.Attendance='일차별 셀에 아이콘을 추가하고 수량을 입력하세요. 이 값이 출석 Excel에 직접 저장됩니다.';
  Object.assign(STUDIO_SEED.meta.statuses,statuses);
  const decorate=p=>{Object.assign(p.meta.statuses,statuses);return upgradeBonuses(p);};
  // Preserve older JSON drafts, but validate and export only the current workspace.
  function current(p){
    const copy=Studio.clone(p);
    for(const name of Object.keys(S)) if(!active.has(name)) copy.tables[name]=[];
    for(const [name,keys] of Object.entries(hidden)) for(const r of copy.tables[name]) for(const key of keys) {
      const f=S[name].fields.find(f=>f.key===key);r[key]=['int','float'].includes(f.type)?0:'';
    }
    return copy;
  }
  function validate(p){
    const issues=[];
    p.tables.UnitMilestone.forEach((b,index)=>{
      const add=(field,message)=>issues.push({table:'UnitMilestone',index,field,message,severity:'error'});
      let params;try{params=JSON.parse(b.EffectParams);if(!params||Array.isArray(params)||typeof params!=='object')throw Error();JSON.stringify(params,(_,v)=>{if(typeof v==='number'&&!Number.isFinite(v))throw Error();return v;});}catch{add('EffectParams','효과 수치는 유한한 숫자를 사용하는 JSON 객체여야 합니다. 예: {"percent":20}');}
      if(b.EffectStatus==='Defined'&&!/^[A-Za-z][A-Za-z0-9_]*$/.test(b.EffectId))add('EffectId','정의한 효과에는 영문 효과 키가 필요합니다.');
      if(b.EffectStatus==='DescriptionOnly'&&(b.EffectId||params&&Object.keys(params).length))add('EffectStatus','효과 키/수치를 입력했다면 상태를 Defined로 바꾸세요.');
    });return issues;
  }
  S.UnitMilestone.note='한 행은 유닛 ID + 해금 레벨로 구분합니다. DescriptionOnly는 현재 설명만 존재하는 항목입니다. 효과를 정의하려면 Defined, 효과 키, JSON 수치를 입력합니다. 게임의 효과 처리 연결은 별도입니다.';
  const esc=s=>String(s).replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
  globalThis.PlannerScope={active,chiefPaths,overview(p){
    const groups=[
      ['강화 밸런스',['units','research']],['출석',['attendance']],
      ['기본 설정',['party','economy','account','profile','tutorial']]
    ];
    return `<div class="scope-heading"><h2>현재 기능 작업실</h2><p>왼쪽에서 기능을 고르고, 위쪽 세부 항목에서 편집하세요.</p></div>${groups.map(([label,ids])=>`<section class="panel scope-group"><div class="panel-head"><h2>${label}</h2></div><div class="workspace-grid">${ids.map(id=>{const n=STUDIO_NAV.find(n=>n.id===id);return `<button data-nav="${id}"><strong>${n.icon} ${n.label}</strong><small>${n.tables.map(t=>`${esc(S[t].label)} ${p.tables[t].length}`).join(' · ')}</small></button>`;}).join('')}</div></section>`).join('')}<div class="scope-note">현재 프로젝트에서 확인한 데이터만 표시합니다. 유닛강화는 기존 시안의 수치이며, Excel 추출 후 게임에 적용하는 연결은 별도입니다.</div>`;
  },workspace:'settings',renderTabs(section){
    const workspace=this.workspace;
    const options=workspace==='enhancement'?[['units','유닛'],['research','영구강화']]:workspace==='rewards'?[['attendance','출석 이벤트'],['missions','미션 이벤트'],['dataBindings','데이터 연결']]:[];
    document.getElementById('workspace-tabs').innerHTML=options.map(([id,label])=>`<button class="${section===id?'active':''}" data-workspace-section="${id}">${label}</button>`).join('');
    if(options.length)document.getElementById('page-title').textContent=workspace==='enhancement'?'강화 밸런스':'보상·출석';
    if(workspace==='rewards')document.getElementById('page-title').textContent='이벤트';
  }};
  return {
    prepare:upgrade, decorate, validationProject:current, validate,
    filterIssues:issues=>issues.filter(i=>active.has(i.table)&&!hidden[i.table]?.includes(i.field)),
    migrate(p){for(const row of p.tables.Attendance)row.Rewards=Studio.clone(p.tables.RewardGroup.find(g=>g.RewardGroupId===row.RewardGroupId)?.Rewards||[]);return decorate(p);},
    updated(p,name,index,key,value){if(name==='Party'&&key==='ChiefId')p.tables.Party[index].ChiefAsset=chiefPaths.get(value)||'';},
    newRecord(row,name){if(name==='UnitMilestone')Object.assign(row,{EffectStatus:'DescriptionOnly',EffectId:'',EffectParams:'{}'});return row;},
    label(text,name,row){return name==='ResearchNode'?`${row.NodeId} · ${STUDIO_SEED.meta.stats.find(s=>s.value===row.Stat)?.label||row.Stat}`:text;},
    tables:tables=>Object.fromEntries(Object.entries(tables).filter(([name])=>active.has(name)||name==='ResearchLevel'))
  };
};
