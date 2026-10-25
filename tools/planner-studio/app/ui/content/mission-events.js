(()=>{
  'use strict';
  const S=STUDIO_SCHEMAS,C=Studio,cat=EVENT_CATALOG;
  const f=(key,label,type='text',more={})=>({key,label,type,...more});
  const id=(key,label)=>f(key,label,'id',{required:true});
  const ref=(key,label,table)=>f(key,label,'ref',{table,required:true});
  const schema=(label,keys,fields,note,readonly=false)=>({label,keys,fields,note,readonly});
  S.EventData=schema('제공 데이터',['DataKey'],[id('DataKey','데이터 키'),f('Name','이름'),f('Operation','집계 방식'),f('Unit','단위')],'코드에서 제공하는 목록입니다. 신규 데이터는 개발자가 추가합니다.',true);
  S.ExecutionHooks=schema('제공 실행 지점',['HookKey'],[id('HookKey','실행 지점 키'),f('Name','이름'),f('Sources','제공 값')],'정상 종료만 포함합니다. 튜토리얼·미리보기·오류·중도 이탈은 포함하지 않습니다.',true);
  S.DataBindings=schema('실행 지점별 데이터',['BindingKey'],[
    id('BindingKey','설정 키'),ref('HookKey','실행 지점','ExecutionHooks'),ref('DataKey','갱신할 데이터','EventData'),
    f('ValueSource','사용할 값','enum',{values:['Constant','PlaySeconds','ReachedRound'],labels:{Constant:'고정값',PlaySeconds:'이번 판 플레이 시간 (초)',ReachedRound:'이번 판 도달 라운드'}}),
    f('ConstantValue','고정값 (고정값 선택 시)','float',{min:0}),f('Enabled','적용','bool')
  ],'실행 지점을 선택하고 갱신할 데이터를 연결하세요. Add / Max는 데이터 정의를 따릅니다. 동일 실행 지점에 같은 데이터는 한 번만 연결합니다.');
  S.MissionEvents=schema('미션 이벤트 설정',['EventKey'],[id('EventKey','이벤트 키'),f('Name','이름','text',{required:true}),f('StartAt','시작 (한국 시간)','datetime'),f('EndAt','종료 (한국 시간)','datetime'),f('Enabled','이벤트 활성','bool')],'이벤트 기간은 시작 포함·종료 제외입니다. 현재 미션 이벤트의 참여 대상은 전체 유저입니다.');
  S.Missions=schema('미션·보상',['MissionKey'],[id('MissionKey','미션 키'),ref('EventKey','소속 이벤트','MissionEvents'),f('Name','미션 이름','text',{required:true}),ref('DataKey','진행 데이터','EventData'),f('Target','목표값','float',{min:0.000001}),f('Period','누적 범위','enum',{values:['Event','Daily'],labels:{Event:'이벤트 기간 누적',Daily:'매일 (공통 초기화)'}}),f('Rewards','완료 보상','rewards')],'제공 데이터의 갱신으로 진행합니다. 보상은 아이콘을 클릭하고 수량을 입력하세요.');
  const names=['DataBindings','MissionEvents','Missions','EventData','ExecutionHooks'];
  for(const n of names)PlannerScope.active.add(n);
  STUDIO_NAV.splice(STUDIO_NAV.findIndex(n=>n.id==='attendance')+1,0,{id:'missions',label:'미션 이벤트',icon:'✓',tables:['Missions','MissionEvents']},{id:'dataBindings',label:'데이터 연결',icon:'↔',tables:['DataBindings','EventData','ExecutionHooks']});
  const defaults={
    EventData:cat.data,ExecutionHooks:cat.hooks,
    DataBindings:[
      ['normal_count','BattleEnd_Normal','BattleCount_Normal','Constant',1],
      ['normal_all','BattleEnd_Normal','BattleCount_All','Constant',1],
      ['hard_count','BattleEnd_Hard','BattleCount_Hard','Constant',1],
      ['hard_all','BattleEnd_Hard','BattleCount_All','Constant',1],
      ['normal_seconds','BattleEnd_Normal','PlaySeconds_Normal','PlaySeconds',0],
      ['normal_round','BattleEnd_Normal','BestRound_Normal','ReachedRound',0]
    ].map(([BindingKey,HookKey,DataKey,ValueSource,ConstantValue])=>({BindingKey,HookKey,DataKey,ValueSource,ConstantValue,Enabled:true})),
    MissionEvents:[{EventKey:'play_3week_sample',Name:'3주 플레이 이벤트 (예시)',StartAt:'2026-11-01T05:00:00+09:00',EndAt:'2026-11-22T05:00:00+09:00',Enabled:false}],
    Missions:[
      {MissionKey:'daily_normal_1',EventKey:'play_3week_sample',Name:'매일 일반 모드 1판',DataKey:'BattleCount_Normal',Target:1,Period:'Daily',Rewards:[{itemId:'Gold',amount:500}]},
      {MissionKey:'total_all_30',EventKey:'play_3week_sample',Name:'누적 30판 플레이',DataKey:'BattleCount_All',Target:30,Period:'Event',Rewards:[{itemId:'Diamond',amount:100}]}
    ]
  };
  Object.assign(STUDIO_SEED.tables,C.clone(defaults));
  function upgrade(p){const q=C.clone(p);for(const n of names)if(!q.tables[n])q.tables[n]=C.clone(defaults[n]);q.tables.EventData=C.clone(cat.data);q.tables.ExecutionHooks=C.clone(cat.hooks);return q;}
  const shape=C.shape,migrate=C.migrate;C.shape=p=>shape(upgrade(p));C.migrate=p=>p.schemaVersion===2?C.shape(p):upgrade(migrate(p));
  const validate=C.validate;
  C.validate=p=>{const issues=validate(p),add=(table,index,field,message)=>issues.push({table,index,field,message,severity:'error'}),pairs=new Set();
    p.tables.DataBindings.forEach((b,i)=>{const hook=cat.hooks.find(h=>h.HookKey===b.HookKey);if(hook&&!hook.Sources.split(';').includes(b.ValueSource))add('DataBindings',i,'ValueSource','이 실행 지점에서 제공하지 않는 값입니다.');const pair=b.HookKey+'/'+b.DataKey;if(b.Enabled&&pairs.has(pair))add('DataBindings',i,'DataKey','동일 실행 지점의 데이터 연결이 중복됩니다.');if(b.Enabled)pairs.add(pair);});
    p.tables.MissionEvents.forEach((e,i)=>{if(!e.StartAt||!e.EndAt||Date.parse(e.StartAt)>=Date.parse(e.EndAt))add('MissionEvents',i,'EndAt','시작보다 늦은 종료 시각을 입력하세요.');});
    p.tables.Missions.forEach((m,i)=>{if(!p.tables.DataBindings.some(b=>b.Enabled&&b.DataKey===m.DataKey))add('Missions',i,'DataKey','이 데이터를 갱신하는 활성 연결이 필요합니다.');});
    return issues;
  };
  globalThis.MissionEvents={names,runtime(p){return {CatalogVersion:cat.version,DataBindings:C.clone(p.tables.DataBindings),MissionEvents:C.clone(p.tables.MissionEvents),Missions:C.clone(p.tables.Missions)};}};
})();
