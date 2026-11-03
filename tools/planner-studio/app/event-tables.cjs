'use strict';
const eventHeaders=['EventKey','Name','StartAt','EndAt','ClaimEndAt','Enabled'];
const missionHeaders=['MissionKey','MissionType','EventKey','Name','DataKey','Target','Period','Rewards'];
function config(project){
 const events=project.tables.MissionEvents.map(e=>({...e,ClaimEndAt:e.ClaimEndAt||e.EndAt}));
 const missions=project.tables.Missions.map(m=>({...m,MissionType:m.MissionType||(m.EventKey?'Event':'Normal'),Period:m.Period==='Event'?'Total':m.Period}));
 for(const plan of project.tables.AttendancePlan||[]){
  if(events.some(e=>e.EventKey===plan.AttendanceId))continue;
  if(plan.RepeatMode==='Cycle')throw Error('반복 출석은 미션의 Daily/Weekly 반복 범위로 설정하세요. '+plan.AttendanceId);
  events.push({EventKey:plan.AttendanceId,Name:plan.Name,StartAt:plan.StartAt,EndAt:plan.EndAt,ClaimEndAt:plan.ClaimEndAt||plan.EndAt,Enabled:plan.Enabled});
  for(const day of project.tables.Attendance.filter(d=>d.AttendanceId===plan.AttendanceId))missions.push({MissionKey:plan.AttendanceId+'_day_'+day.Day,EventKey:plan.AttendanceId,Name:day.Day+'일 출석',DataKey:'AttendanceDays',Target:day.Day,Period:'Event',Rewards:day.Rewards});
 }
 return {events,missions};
}
function tables(project){const {events,missions}=config(project);const result={
 Events:[eventHeaders,...events.map(e=>eventHeaders.map(k=>k==='Enabled'?(e[k]?1:0):e[k]||''))],
 Missions:[missionHeaders,...missions.map(m=>missionHeaders.map(k=>k==='Rewards'?m.Rewards.map(r=>r.itemId+':'+r.amount).join(';'):m[k]))]
};return result;}
module.exports={config,tables,eventHeaders,missionHeaders};
