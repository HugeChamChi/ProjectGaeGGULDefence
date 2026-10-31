'use strict';
function table(config,kind){
 let rows;
 if(kind==='unitBaseStats')rows=config.Units.flatMap(u=>u.Stats.map(s=>({UnitKey:u.UnitKey,AssetKey:u.AssetKey,Name:u.Name,...s})));
 else if(kind==='missions'){
  rows=config.DataBindings.map(r=>({RecordType:'Binding',...r}));rows.push(...config.MissionEvents.map(r=>({RecordType:'Event',...r})));
  for(const {Rewards,...mission} of config.Missions){rows.push({RecordType:'Mission',...mission});Rewards.forEach((r,i)=>rows.push({RecordType:'Reward',MissionKey:mission.MissionKey,RewardIndex:i,ItemKey:r.itemId,Amount:r.amount}));}
  for(const event of config.attendanceEvents||[]){
   rows.push({RecordType:'AttendanceEvent',EventKey:event.eventKey,Name:event.name,StartAt:event.startAt,EndAt:event.endAt,ClaimEndAt:event.claimEndAt,Enabled:event.enabled,RepeatMode:event.repeatMode,CycleDays:event.cycleDays});
   for(const day of event.days)day.rewards.forEach((reward,index)=>rows.push({RecordType:'AttendanceReward',EventKey:event.eventKey,Day:day.day,RewardIndex:index,ItemKey:reward.itemKey,Amount:reward.amount}));
  }
 }else if(kind==='unitBonuses'){
  rows=[];for(const bonus of config.tables.BonusDefinition){rows.push({RecordType:'Bonus',...bonus});for(const effect of config.tables.BonusEffect.filter(r=>r.BonusId===bonus.BonusId))rows.push({RecordType:'Effect',...effect});}
 }else throw Error('Unsupported readable chart');
 const headers=[...new Set(rows.flatMap(Object.keys))];
 for(const row of rows)for(const value of Object.values(row))if(value!==null&&typeof value==='object')throw Error('Nested readable value');
 return [headers,...rows.map(row=>headers.map(key=>row[key]??''))];
}
module.exports={table};
