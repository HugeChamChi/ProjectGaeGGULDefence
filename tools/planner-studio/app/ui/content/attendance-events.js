'use strict';
globalThis.AttendanceEventsFeature = ({studio: Studio}) => {
  const S=STUDIO_SCHEMAS,field=(key,label,type='text',options={})=>({key,label,type,...options});
  const extra=[field('StartAt','이벤트 시작 (한국 시간)','datetime'),field('EndAt','이벤트 종료 (한국 시간)','datetime'),field('ClaimEndAt','수령 마감 (빈칸 = 종료 시각)','datetime'),field('RepeatMode','완주 후 반복','enum',{values:['Once','Cycle'],labels:{Once:'기간 중 한 번',Cycle:'다시 1일차부터'}}),field('Enabled','이벤트 활성','bool')];
  S.AttendancePlan.fields=S.AttendancePlan.fields.filter(f=>f.key!=='ResetHourKst');
  S.AttendancePlan.fields.push(...extra);S.AttendancePlan.label='이벤트 설정';S.Attendance.label='일차별 보상';
  S.AttendancePlan.note='7일 출석을 바탕으로 한 비활성 이벤트 예시입니다. 활성화하려면 시작·종료 시각을 입력하세요. 게임의 이벤트 진행/수령 처리는 별도 연결이 필요합니다.';
  const nav=STUDIO_NAV.find(n=>n.id==='attendance');nav.label='출석 이벤트';
  function defaults(p){for(const plan of p.tables.AttendancePlan||[]){delete plan.ResetHourKst;if(plan.StartAt===undefined)plan.StartAt='';if(plan.EndAt===undefined)plan.EndAt='';if(plan.ClaimEndAt===undefined)plan.ClaimEndAt='';if(!plan.RepeatMode)plan.RepeatMode='Once';if(plan.Enabled===undefined)plan.Enabled=false;}return p;}
  defaults(STUDIO_SEED);for(const plan of STUDIO_SEED.tables.AttendancePlan){plan.Enabled=false;plan.Name='7일 출석 이벤트 예시';}
  function validate(p){const issues=[];p.tables.AttendancePlan.forEach((plan,index)=>{
    const add=(key,message)=>issues.push({table:'AttendancePlan',index,field:key,message,severity:'error'});
    if(plan.Enabled&&(!plan.StartAt||!plan.EndAt))add('StartAt','활성 이벤트는 시작·종료 시각이 필요합니다.');
    if(plan.StartAt&&plan.EndAt&&Date.parse(plan.StartAt)>=Date.parse(plan.EndAt))add('EndAt','종료 시각은 시작보다 늦어야 합니다.');
    if(plan.ClaimEndAt&&plan.EndAt&&Date.parse(plan.ClaimEndAt)<Date.parse(plan.EndAt))add('ClaimEndAt','수령 마감은 종료 시각 이후여야 합니다.');
  });return issues;}
  const headers={AttendanceEvents:['EventKey','Name','StartAt','EndAt','ClaimEndAt','CycleDays','RepeatMode','Enabled'],AttendanceRewards:['EventKey','Day','Rewards']};
  const fields={AttendanceEvents:[field('EventKey','이벤트 키','id',{required:true}),field('Name','이름','text',{required:true}),...extra.filter(f=>f.type==='datetime'),...S.AttendancePlan.fields.filter(f=>['CycleDays','RepeatMode','Enabled'].includes(f.key))],AttendanceRewards:[field('EventKey','이벤트 키','text',{required:true}),field('Day','일차','int',{min:1,max:365}),field('Rewards','보상','rewards')]};
  globalThis.AttendanceEvents={headers,fields,sheetFor:{AttendancePlan:'AttendanceEvents',Attendance:'AttendanceRewards'}};
  return {
    prepare:p=>defaults(Studio.clone(p)),decorate:defaults,migrate:defaults,validate,
    newRecord(row,name){if(name==='AttendancePlan')Object.assign(row,{StartAt:'',EndAt:'',ClaimEndAt:'',RepeatMode:'Once',Enabled:false});return row;},
    tables(result,p){delete result.Attendance;delete result.AttendancePlan;result.AttendanceEvents=[headers.AttendanceEvents,...p.tables.AttendancePlan.map(r=>[r.AttendanceId,r.Name,r.StartAt,r.EndAt,r.ClaimEndAt,r.CycleDays,r.RepeatMode,r.Enabled?1:0])];result.AttendanceRewards=[headers.AttendanceRewards,...p.tables.Attendance.map(r=>[r.AttendanceId,r.Day,Studio.serialize(r.Rewards)])];return result;}
  };
};
