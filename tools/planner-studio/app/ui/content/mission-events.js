'use strict';
globalThis.MissionEventsFeature = ({studio: C}) => {
  const S=STUDIO_SCHEMAS,cat=EVENT_CATALOG;
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
  S.MissionEvents=schema('이벤트',['EventKey'],[id('EventKey','이벤트 키'),f('Name','이름','text',{required:true}),f('StartAt','시작 (한국 시간)','datetime'),f('EndAt','종료 (한국 시간)','datetime'),f('ClaimEndAt','보상 수령 마감 (빈칸 = 종료)','datetime'),f('Enabled','이벤트 활성','bool')],'이벤트 기간은 시작 포함·종료 제외입니다. 출석과 전투 조건은 연결된 미션에서 설정합니다.');
  S.Missions=schema('미션·보상',['MissionKey'],[f('MissionType','Mission Type','enum',{values:['Normal','Event'],labels:{Normal:'Normal',Event:'Event'}}),id('MissionKey','미션 키'),f('EventKey','소속 이벤트','ref',{table:'MissionEvents'}),f('Name','미션 이름','text',{required:true}),ref('DataKey','진행 데이터','EventData'),f('Target','목표값','float',{min:0.000001}),f('Period','누적 범위','enum',{values:['Total','Daily','Weekly'],labels:{Total:'이벤트 기간 누적',Daily:'매일 (공통 초기화)'}}),f('Rewards','완료 보상','rewards')],'제공 데이터의 갱신으로 진행합니다. 보상은 아이콘을 클릭하고 수량을 입력하세요.');
  S.Missions.fields.find(f=>f.key==='MissionType').label='미션 타입';
  S.Missions.fields.find(f=>f.key==='MissionType').labels={Normal:'상시 미션',Event:'이벤트 미션'};
  S.Missions.fields.find(f=>f.key==='Period').labels={Total:'전체 누적',Daily:'매일 (공통 초기화)',Weekly:'매주 월요일 (공통 초기화)'};
  const names=['DataBindings','MissionEvents','Missions','EventData','ExecutionHooks'];
  for(const n of names)PlannerScope.active.add(n);
  STUDIO_NAV.splice(STUDIO_NAV.findIndex(n=>n.id==='attendance')+1,0,{id:'missions',label:'미션 이벤트',icon:'✓',tables:['MissionEvents','Missions']},{id:'dataBindings',label:'데이터 연결',icon:'↔',tables:['DataBindings','EventData','ExecutionHooks']});
  STUDIO_NAV.splice(STUDIO_NAV.findIndex(n=>n.id==='attendance'),1);
  const dataNav=STUDIO_NAV.findIndex(n=>n.id==='dataBindings');if(dataNav>=0)STUDIO_NAV.splice(dataNav,1);
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
  function upgrade(p){const q=C.clone(p);for(const n of names)if(!q.tables[n])q.tables[n]=C.clone(defaults[n]);q.tables.EventData=C.clone(cat.data);q.tables.EventData.push({DataKey:'AttendanceDays',Name:'누적 출석일',Operation:'Add',Unit:'일'});
    for(const plan of q.tables.AttendancePlan||[]){
      if(plan.RepeatMode==='Cycle')throw Error('기존 반복 출석은 일일/주간 미션으로 변경해 주세요: '+plan.AttendanceId);
      if(!q.tables.MissionEvents.some(e=>e.EventKey===plan.AttendanceId)){
        q.tables.MissionEvents.push({EventKey:plan.AttendanceId,Name:plan.Name,StartAt:plan.StartAt||'2026-11-01T05:00:00+09:00',EndAt:plan.EndAt||'2026-12-01T05:00:00+09:00',ClaimEndAt:plan.ClaimEndAt||'',Enabled:plan.Enabled});
        for(const day of q.tables.Attendance||[])if(day.AttendanceId===plan.AttendanceId)q.tables.Missions.push({MissionKey:plan.AttendanceId+'_day_'+day.Day,EventKey:plan.AttendanceId,Name:day.Day+'일 출석',DataKey:'AttendanceDays',Target:day.Day,Period:'Event',Rewards:day.Rewards});
      }
    }
    q.tables.AttendancePlan=[];q.tables.Attendance=[];
    for(const m of q.tables.Missions){m.MissionType??=m.EventKey?'Event':'Normal';if(m.Period==='Event')m.Period='Total';}
    for(const event of q.tables.MissionEvents)event.ClaimEndAt??='';
    q.tables.ExecutionHooks=C.clone(cat.hooks);return q;}
  function validate(p){const issues=[],add=(table,index,field,message)=>issues.push({table,index,field,message,severity:'error'}),pairs=new Set();
    p.tables.DataBindings.forEach((b,i)=>{const hook=cat.hooks.find(h=>h.HookKey===b.HookKey);if(hook&&!hook.Sources.split(';').includes(b.ValueSource))add('DataBindings',i,'ValueSource','이 실행 지점에서 제공하지 않는 값입니다.');const pair=b.HookKey+'/'+b.DataKey;if(b.Enabled&&pairs.has(pair))add('DataBindings',i,'DataKey','동일 실행 지점의 데이터 연결이 중복됩니다.');if(b.Enabled)pairs.add(pair);});
    p.tables.MissionEvents.forEach((e,i)=>{if(!e.StartAt||!e.EndAt||Date.parse(e.StartAt)>=Date.parse(e.EndAt))add('MissionEvents',i,'EndAt','시작보다 늦은 종료 시각을 입력하세요.');if(e.ClaimEndAt&&Date.parse(e.ClaimEndAt)<Date.parse(e.EndAt))add('MissionEvents',i,'ClaimEndAt','수령 마감은 종료 시각 이후여야 합니다.');});
    p.tables.Missions.forEach((m,i)=>{if(m.MissionType==='Event'&&!p.tables.MissionEvents.some(e=>e.EventKey===m.EventKey))add('Missions',i,'EventKey','Event missions require an existing EventKey.');if(m.MissionType==='Normal'&&m.EventKey)add('Missions',i,'EventKey','Normal missions must have an empty EventKey.');if(m.DataKey==='AttendanceDays'&&!Number.isInteger(m.Target))add('Missions',i,'Target','출석일 목표는 양의 정수여야 합니다.');if(m.DataKey!=='AttendanceDays'&&!p.tables.DataBindings.some(b=>b.Enabled&&b.DataKey===m.DataKey))add('Missions',i,'DataKey','이 데이터를 갱신하는 활성 연결이 필요합니다.');});
    return issues;
  }
  globalThis.MissionEvents={names,runtime(p){return {CatalogVersion:cat.version,DataBindings:C.clone(p.tables.DataBindings),MissionEvents:C.clone(p.tables.MissionEvents),Missions:C.clone(p.tables.Missions).map(m=>({...m,MissionType:m.MissionType==='Normal'?1:0}))};}};
  return {prepare:upgrade,decorate:upgrade,migrate:upgrade,validate,tables(result){for(const name of ['AttendanceEvents','AttendanceRewards','DataBindings','EventData','ExecutionHooks'])delete result[name];return result;}};
};
