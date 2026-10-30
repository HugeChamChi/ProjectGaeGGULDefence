'use strict';
function table(config,kind){
 let rows;
 if(kind==='unitBaseStats')rows=config.Units.flatMap(u=>u.Stats.map(s=>({UnitKey:u.UnitKey,AssetKey:u.AssetKey,Name:u.Name,...s})));
 else if(kind==='missions'){
  rows=config.DataBindings.map(r=>({RecordType:'Binding',...r}));rows.push(...config.MissionEvents.map(r=>({RecordType:'Event',...r})));
  for(const {Rewards,...mission} of config.Missions){rows.push({RecordType:'Mission',...mission});Rewards.forEach((r,i)=>rows.push({RecordType:'Reward',MissionKey:mission.MissionKey,RewardIndex:i,ItemKey:r.itemId,Amount:r.amount}));}
 }else if(kind==='unitBonuses'){
  rows=config.tables.BonusEffect.map(row=>({RecordType:'Part',...row}));
  rows.push(...config.tables.BonusDescription.map(row=>({RecordType:'Description',...row})));
 }else throw Error('Unsupported readable chart');
 const headers=[...new Set(rows.flatMap(Object.keys))];
 for(const row of rows)for(const value of Object.values(row))if(value!==null&&typeof value==='object')throw Error('Nested readable value');
 return [headers,...rows.map(row=>headers.map(key=>row[key]??''))];
}
module.exports={table};
