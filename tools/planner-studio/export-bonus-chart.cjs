'use strict';
const fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto');
const bonus=require('./bonus-excel.cjs');
const workbook=require('./app/content-workbook.cjs');
const {open}=require('./app/workbook.cjs');
function exportChart(input,directory,releaseKey){
 if(fs.existsSync(directory))throw Error('출력 폴더가 이미 존재합니다. 새 릴리스 폴더를 사용하세요.');
 const source=fs.readFileSync(input),parts=bonus.read(source);
 const config={SchemaVersion:1,Kind:'UnitLevelBonuses',Parts:parts};
 if(!/^[A-Za-z0-9_-]{1,80}$/.test(releaseKey))throw Error('릴리스 키 오류');
 const headers=['SchemaVersion','ReleaseKey',...bonus.headers];
 const table=[headers,...parts.map(part=>[1,releaseKey,...bonus.headers.map(key=>part[key]??'')])];
 const bytes=Buffer.from(workbook.engine().RewardCore.xlsx('UnitLevelBonuses',table));
 const book=open(bytes),sheet=book.sheets.get('UnitLevelBonuses');
 if(book.sheets.size!==1)throw Error('단일 시트 오류');
 const cols=headers.map((_,i)=>String.fromCharCode(65+i));
 const rows=table.slice(1).map((_,index)=>Object.fromEntries(table[0].map((key,i)=>{
  const cell=book.get('UnitLevelBonuses',cols[i]+(index+2));if(cell.formula!==null)throw Error('수식 검출');return [key,cell.value];
 })));
 for(let index=0;index<rows.length;index++)for(let column=0;column<headers.length;column++)
  if(rows[index][headers[column]]!==String(table[index+1][column]))throw Error('엑셀 재읽기 내용 불일치');
 fs.mkdirSync(directory,{recursive:true});
 const filename='UnitLevelBonuses.backend.xlsx';fs.writeFileSync(path.join(directory,filename),bytes);
 fs.writeFileSync(path.join(directory,'UnitLevelBonuses.json'),JSON.stringify(config,null,2));
 const manifest={releaseKey,format:'columns',targetChartName:'DEV_UnitLevelBonuses',file:filename,units:new Set(parts.map(p=>p.UnitKey)).size,bonuses:new Set(parts.map(p=>p.UnitKey+'|'+p.Level)).size,parts:parts.length,rows:rows.length,columns:headers,sha256:crypto.createHash('sha256').update(bytes).digest('hex'),sourceSha256:crypto.createHash('sha256').update(source).digest('hex'),xlsxRoundtripVerified:true,remoteApplied:false,gameLoaderConnected:false};
 fs.writeFileSync(path.join(directory,'manifest.json'),JSON.stringify(manifest,null,2));
 fs.writeFileSync(path.join(directory,'UPLOAD.txt'),'뒤끝 차트 이름: DEV_UnitLevelBonuses\n업로드 파일: '+filename+'\n기존 DEV_UnitBaseStats 차트에 덮어쓰지 마세요.\n업로드 후 차트 파일 적용이 별도로 필요합니다.\n현재 게임에는 이 별도 차트의 소비 로더가 없습니다. 업로드만으로 효과가 적용되지는 않습니다.\n');
 return manifest;
}
module.exports={exportChart};
if(require.main===module)console.log(JSON.stringify(exportChart(...process.argv.slice(2)),null,2));
