'use strict';
const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict');
const {zipSync,strFromU8,strToU8}=require('./app/node_modules/fflate');
const workbook=require('./app/workbook.cjs'),excel=require('./app/content-workbook.cjs');
const {atomic}=require('./app/store.cjs');
const root=path.join(__dirname,'GGD-Planner/자료'),stamp=new Date().toISOString().replace(/[:.]/g,'-'),backup=path.join(__dirname,'backups','default-excel-'+stamp);
fs.mkdirSync(backup,{recursive:true});
function cleanAccount(bytes){
 const before=workbook.read(bytes,'account'),book=workbook.open(bytes);
 for(const [name,sheet]of book.sheets){
  let xml=strFromU8(book.parts[sheet.part]);
  xml=xml.replace(/<c\b[^>]*\br="([A-Z]+)(\d+)"[^>]*(?:\/>|>[\s\S]*?<\/c>)/g,(cell,col,rowText)=>{
   const row=Number(rowText);
   const keep=name==='안내_계정설정'?['A','B'].includes(col)&&[4,5,6,7,8,9,10,13,14].includes(row):name==='계정레벨'?['A','B','C'].includes(col)&&row>=4:name==='전투경험치'?/^[A-H]$/.test(col)&&row>=4&&row<=8:name==='획득량_예시'?/^[A-D]$/.test(col)&&row>=4&&row<=8:true;
   return keep?cell:'';
  });book.parts[sheet.part]=strToU8(xml);
 }
 // Remove unreferenced strings too, so deleted explanations are not left inside the ZIP.
 if(book.parts['xl/sharedStrings.xml']){
  const original=strFromU8(book.parts['xl/sharedStrings.xml']),strings=[...original.matchAll(/<si\b[^>]*>[\s\S]*?<\/si>/g)].map(m=>m[0]),used=new Set();let count=0;
  for(const sheet of book.sheets.values())for(const cell of strFromU8(book.parts[sheet.part]).matchAll(/<c\b(?=[^>]*\bt="s")[^>]*>[\s\S]*?<\/c>/g)){const id=/<v>(\d+)<\/v>/.exec(cell[0]);if(id){used.add(Number(id[1]));count++;}}
  const ids=[...used].sort((a,b)=>a-b),map=new Map(ids.map((id,i)=>[id,i]));
  for(const sheet of book.sheets.values())book.parts[sheet.part]=strToU8(strFromU8(book.parts[sheet.part]).replace(/<c\b(?=[^>]*\bt="s")[^>]*>[\s\S]*?<\/c>/g,cell=>cell.replace(/<v>(\d+)<\/v>/,(_,id)=>'<v>'+map.get(Number(id))+'</v>')));
  const start=/<sst\b[^>]*>/.exec(original)[0].replace(/\bcount="\d+"/,'count="'+count+'"').replace(/\buniqueCount="\d+"/,'uniqueCount="'+ids.length+'"');
  book.parts['xl/sharedStrings.xml']=strToU8('<?xml version="1.0" encoding="UTF-8"?>'+start+ids.map(id=>strings[id]).join('')+'</sst>');
 }
 const cleaned=Buffer.from(zipSync(book.parts)),after=workbook.read(cleaned,'account');
 assert.deepEqual(after.values,before.values);assert.deepEqual(after.context,before.context);return cleaned;
}
let project=excel.engine().Studio.create();const pending=[],report=[];
for(const name of ['UnitBalance.xlsx','PermanentUpgrade.xlsx','AttendanceEvent.xlsx','AttendanceEventExample.xlsx','Party.xlsx','AccountSettings.xlsx','Profile.xlsx','Tutorial.xlsx']){
 const file=path.join(root,name),bytes=fs.readFileSync(file),parsed=excel.read(bytes,project);project=parsed.project;
 const clean=excel.write(project,parsed.names);excel.read(clean,project);pending.push([file,clean]);report.push({file:name,status:'updated',tables:parsed.names});
}
for(const name of ['EndlessBalance.xlsx','AccountProgression.xlsx']){
 const file=path.join(root,name),bytes=fs.readFileSync(file);workbook.read(bytes,name.startsWith('Endless')?'endless':'account');
 pending.push([file,name.startsWith('Account')?cleanAccount(bytes):bytes]);report.push({file:name,status:name.startsWith('Account')?'notes removed, formula contract preserved':'already data-only; verified'});
}
for(const [file]of pending)fs.copyFileSync(file,path.join(backup,path.basename(file)));
const accountSeed=path.join(__dirname,'app/seeds/AccountProgression.xlsx');fs.copyFileSync(accountSeed,path.join(backup,'source-AccountProgression.xlsx'));
atomic(accountSeed,cleanAccount(fs.readFileSync(accountSeed)));
for(const [file,bytes]of pending)atomic(file,bytes);
atomic(path.join(backup,'report.json'),JSON.stringify(report,null,2));
console.log(JSON.stringify({ok:true,backup,files:report},null,2));
