'use strict';
const fs=require('node:fs'),path=require('node:path');
const crypto=require('node:crypto'),{unzipSync}=require('./app/node_modules/fflate');
const excel=require('./app/content-workbook.cjs'),project=excel.engine().Studio.create();
const definitions={
 'UnitBalance.xlsx':['Unit','UnitBaseStat','UnitUpgrade','UnitMilestone'],
 'PermanentUpgrade.xlsx':['ResearchTree','ResearchNode'],
 'AttendanceEvent.xlsx':['AttendancePlan','Attendance'],
 'AttendanceEventExample.xlsx':['AttendancePlan','Attendance'],
 'MissionEvents.xlsx':['DataBindings','MissionEvents','Missions','EventData','ExecutionHooks'],
 'Party.xlsx':['Party'],
 'AccountSettings.xlsx':['Economy','AccountLevel','BattleReward'],
 'Profile.xlsx':['Profile'],
 'Tutorial.xlsx':['Tutorial']
};
const manifestFile=path.join(__dirname,'app/seeds/project-library-manifest.json');
const prior=fs.existsSync(manifestFile)?JSON.parse(fs.readFileSync(manifestFile,'utf8')):{files:{}};
function fingerprint(bytes){const parts=unzipSync(bytes),hash=crypto.createHash('sha256');for(const name of Object.keys(parts).sort())hash.update(name+'\0').update(parts[name]);return hash.digest('hex');}
const manifest={version:4,files:{}};
for(const [name,tables]of Object.entries(definitions)){
 const file=path.join(__dirname,'app/seeds',name),history=prior.files[name]?.previousFingerprints||[];
 if(fs.existsSync(file))history.push(fingerprint(fs.readFileSync(file)));
 let data=project;
 if(name==='AttendanceEventExample.xlsx'){
  data=excel.engine().Studio.clone(project);const plan=data.tables.AttendancePlan[0],oldKey=plan.AttendanceId;
  Object.assign(plan,{AttendanceId:'attendance_7day_sample',Name:'7일 출석 이벤트 (예시)',StartAt:'2026-11-01T05:00:00+09:00',EndAt:'2026-11-15T05:00:00+09:00',ClaimEndAt:'2026-11-16T05:00:00+09:00',RepeatMode:'Once',Enabled:false});
  for(const row of data.tables.Attendance)if(row.AttendanceId===oldKey)row.AttendanceId=plan.AttendanceId;
  plan.Name='일반 7일 출석 (예시)';
  const newcomer={...plan,AttendanceId:'attendance_newuser_14day_sample',Name:'신규 유저 14일 출석 (예시)',CycleDays:14,EndAt:'2026-12-01T05:00:00+09:00',ClaimEndAt:'2026-12-02T05:00:00+09:00'};
  data.tables.AttendancePlan.unshift(newcomer);
  const rewards=[
   [{itemId:'Gold',amount:500},{itemId:'Stamina',amount:10}],
   [{itemId:'Diamond',amount:10}], [{itemId:'Gold',amount:1000}],
   [{itemId:'Stamina',amount:20}], [{itemId:'Diamond',amount:20}],
   [{itemId:'Gold',amount:1500}], [{itemId:'Diamond',amount:50}],
   [{itemId:'Gold',amount:2000},{itemId:'Stamina',amount:10}],
   [{itemId:'Diamond',amount:20}], [{itemId:'Gold',amount:2500}],
   [{itemId:'Stamina',amount:30}], [{itemId:'Diamond',amount:30}],
   [{itemId:'Gold',amount:3000}], [{itemId:'Diamond',amount:100},{itemId:'Stamina',amount:30}]
  ];
  data.tables.Attendance.unshift(...rewards.map((Rewards,index)=>({...data.tables.Attendance[0],AttendanceId:newcomer.AttendanceId,Day:index+1,Rewards})));
 }
 const bytes=excel.write(data,tables);excel.read(bytes,data);
 fs.writeFileSync(file,bytes);
 manifest.files[name]={fingerprint:fingerprint(bytes),previousFingerprints:[...new Set(history)].slice(-20)};
}
fs.writeFileSync(manifestFile,JSON.stringify(manifest,null,2));
console.log('Project snapshot workbooks: '+Object.keys(definitions).length);
