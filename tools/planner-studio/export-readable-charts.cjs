'use strict';
const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict');
const workbook=require('./app/content-workbook.cjs'),{open}=require('./app/workbook.cjs');
const {decodeConfigRows}=require('./app/chart-payload.cjs');
function records(file,sheet){const b=open(fs.readFileSync(file)),s=b.sheets.get(sheet);if(!s)throw Error('Missing '+sheet);const headers=[...s.cells.keys()].filter(a=>/^[A-Z]+1$/.test(a)).map(a=>[a.replace(/1$/,''),b.get(sheet,a).value]);return [...new Set([...s.cells.keys()].map(a=>Number(a.match(/\d+$/)[0])).filter(n=>n>1))].sort((a,b)=>a-b).map(n=>Object.fromEntries(headers.map(([col,key])=>[key,b.get(sheet,col+n).value])));}
function config(file,sheet){return JSON.parse(decodeConfigRows(records(file,sheet)).bytes.toString('utf8'));}
function write(directory,name,rows){
 const columns=[...new Set(rows.flatMap(Object.keys))];
 if(!columns.length)throw Error('Empty table '+name);
 for(const r of rows)for(const v of Object.values(r))if(v!==null&&typeof v==='object')throw Error('Nested data forbidden');
 const table=[columns,...rows.map(r=>columns.map(k=>r[k]??''))];
 const file=path.join(directory,name+'_2026-10-30.xlsx');
 fs.writeFileSync(file,Buffer.from(workbook.engine().RewardCore.xlsx(name,table)));
 const actual=records(file,name);assert.deepEqual(actual,JSON.parse(JSON.stringify(rows.map(r=>Object.fromEntries(columns.map(k=>[k,String(r[k]??'')]))))));
 return {chart:'DEV_'+name,file:path.basename(file),rows:rows.length,columns};
}
function run(directory){
 if(fs.existsSync(directory))throw Error('Output exists');fs.mkdirSync(directory,{recursive:true});
 const units=require('./app/unit-runtime.cjs').unitRuntime(workbook.engine().Studio.create());
 const missions=config('outputs/dev-chart-upload-1793181571206/MissionEvents.backend.xlsx','MissionEvents');
 const manifest=[];
 manifest.push(write(directory,'UnitBaseStats',units.Units.flatMap(u=>u.Stats.map(s=>({UnitKey:u.UnitKey,AssetKey:u.AssetKey,Name:u.Name,...s})))));
 const missionRows=[];
 for(const r of missions.DataBindings)missionRows.push({RecordType:'Binding',...r});
 for(const r of missions.MissionEvents)missionRows.push({RecordType:'Event',...r});
 for(const r of missions.Missions){const {Rewards,...mission}=r;missionRows.push({RecordType:'Mission',...mission});Rewards.forEach((reward,i)=>missionRows.push({RecordType:'Reward',MissionKey:r.MissionKey,RewardIndex:i,ItemKey:reward.itemId,Amount:reward.amount}));}
 manifest.push(write(directory,'MissionEvents',missionRows));
 const bonusProject=workbook.engine().Studio.create();
 const bonuses=[...bonusProject.tables.BonusEffect.map(row=>({RecordType:'Part',...row})),...bonusProject.tables.BonusDescription.map(row=>({RecordType:'Description',...row}))];
 manifest.push(write(directory,'UnitLevelBonuses',bonuses));
 // Research arrays become individual relation rows rather than JSON cells.
 const research=records('outputs/backend-upload-20261029/02_DEV_ResearchBalance/ResearchBalance.xlsx','ResearchBalance');
 const researchRows=[];
 for(const r of research){const {SchemaVersion,ReleaseKey,Parents,LaneNames,...node}=r;researchRows.push({RecordType:'Node',...node});JSON.parse(Parents).forEach((parent,index)=>researchRows.push({RecordType:'Parent',TreeKey:r.TreeKey,NodeId:r.NodeId,ParentIndex:index,ParentNodeId:parent}));}
 for(const key of [...new Set(research.map(r=>r.TreeKey))])JSON.parse(research.find(r=>r.TreeKey===key).LaneNames).forEach((name,index)=>researchRows.push({RecordType:'Lane',TreeKey:key,LaneIndex:index,LaneName:name}));
 manifest.push(write(directory,'ResearchBalance',researchRows));
 fs.writeFileSync(path.join(directory,'tables.json'),JSON.stringify(manifest,null,2));
 fs.writeFileSync(path.join(directory,'必読_적용전확인.txt'),'일반 표 형식. Base64/JSON/ReleaseKey 열 없음.\nMissionEvents와 ResearchBalance의 RecordType은 행 종류입니다.\n이 파일들은 기존 payload 및 Research 파서와 호환되지 않습니다. 게임 로더 전환 전 서버의 현재 적용 파일을 교체하지 마세요.\nUnitLevelBonuses의 전투 효과 연결도 아직 완료되지 않았습니다.\n이 파일은 마지막 업로드 준비본의 내용을 변환했으며 새 사용자 편집본을 자동 선택하지 않았습니다.\n');
 console.log(JSON.stringify(manifest,null,2));
}
module.exports={records,write,run};if(require.main===module)run(process.argv[2]);
