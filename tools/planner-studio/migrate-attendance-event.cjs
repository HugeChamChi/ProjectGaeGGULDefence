'use strict';
const fs=require('node:fs'),path=require('node:path');
const excel=require('./app/content-workbook.cjs'),{atomic}=require('./app/store.cjs');
const root=path.join(__dirname,'GGD-Planner/자료'),backup=path.join(__dirname,'backups','attendance-event-'+new Date().toISOString().replace(/[:.]/g,'-'));
fs.mkdirSync(backup,{recursive:true});const current=path.join(root,'AttendanceEvent.xlsx'),old=path.join(root,'Attendance.xlsx');
let p=excel.engine().Studio.create();const source=fs.existsSync(current)?current:fs.existsSync(old)?old:null;
if(source){fs.copyFileSync(source,path.join(backup,path.basename(source)));p=excel.read(fs.readFileSync(source),p).project;}
const bytes=excel.write(p,['AttendancePlan','Attendance']);excel.read(bytes,p);atomic(current,bytes);
for(const relative of ['GGD-Planner/자료/Attendance.xlsx','app/seeds/Attendance.xlsx']){
 const file=path.resolve(__dirname,relative);if(!file.startsWith(path.resolve(__dirname)+path.sep))throw Error('Unexpected path');
 if(fs.existsSync(file))fs.renameSync(file,path.join(backup,(relative.startsWith('app/')?'source-':'retired-')+'Attendance.xlsx'));
}
console.log(JSON.stringify({ok:true,backup,sheets:excel.inspect(bytes).names},null,2));
