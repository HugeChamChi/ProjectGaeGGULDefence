'use strict';
const fs=require('node:fs'),path=require('node:path'),vm=require('node:vm');
const {zipSync,unzipSync,strToU8}=require('fflate');
const {open,hash}=require('./workbook.cjs');
let context;
function engine(){
  if(!context){context=vm.createContext({TextEncoder,TextDecoder,Uint8Array,Uint32Array,DataView,ArrayBuffer});
    for(const name of ['seed','schemas','core','studio-core','current-scope','unit-base-seed','unit-keys','drone-only','attendance-events','event-catalog','mission-events','bonus-legacy-defaults','bonus-defaults','bonus-contracts','bonus-model','bonus-editor','item-catalog','studio-compose'])vm.runInContext(fs.readFileSync(path.join(__dirname,'ui/content',name+'.js'),'utf8'),context);
  }return context;
}
const bundles={items:['Item','ItemFunction','SelectionList','SelectionOption'],bonuses:['BonusDefinition','BonusEffect'],enhancement:['Unit','UnitUpgrade','UnitMilestone','BonusDefinition','BonusEffect','ResearchTree','ResearchNode'],rewards:['AttendancePlan','Attendance'],missions:['DataBindings','MissionEvents','Missions','EventData','ExecutionHooks']};
function write(project,names){
  const e=engine(),all=e.Studio.tables(project),files={};let index=0;
  names=[...new Set(names.flatMap(n=>e.UnitKeys.sheetFor[n]?Object.keys(e.UnitKeys.headers):e.AttendanceEvents.sheetFor[n]?Object.keys(e.AttendanceEvents.headers):[n]))];
  for(const name of names){if(!all[name])throw Error('지원하지 않는 표입니다.');
    const single=unzipSync(e.RewardCore.xlsx(name,all[name]));
    if(!index)for(const [key,value]of Object.entries(single))if(!key.startsWith('xl/worksheets/'))files[key]=value;
    files[`xl/worksheets/sheet${++index}.xml`]=single['xl/worksheets/sheet1.xml'];
  }
  files['xl/workbook.xml']=strToU8(`<?xml version="1.0" encoding="UTF-8"?><workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets>${names.map((n,i)=>`<sheet name="${n}" sheetId="${i+1}" r:id="rId${i+1}"/>`).join('')}</sheets></workbook>`);
  files['xl/_rels/workbook.xml.rels']=strToU8(`<?xml version="1.0"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">${names.map((_,i)=>`<Relationship Id="rId${i+1}" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet${i+1}.xml"/>`).join('')}</Relationships>`);
  files['[Content_Types].xml']=strToU8(`<?xml version="1.0"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>${names.map((_,i)=>`<Override PartName="/xl/worksheets/sheet${i+1}.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>`).join('')}</Types>`);
  return Buffer.from(zipSync(files));
}
function column(address){return address.replace(/[0-9]+$/,'');}
function inspect(bytes){
  const book=open(bytes),e=engine(),names=[...book.sheets.keys()].filter(n=>e.PlannerScope.active.has(n)||n==='RewardGroup'||e.UnitKeys.headers[n]||e.AttendanceEvents.headers[n]||n==='BonusDescription');
  if(!names.length)throw Error('기획작업실의 콘텐츠 Excel 양식이 아닙니다.');
  let count=0;
  const tables={};
  for(const name of names){
    const sheet=book.sheets.get(name),headers=new Map();let schema=e.STUDIO_SCHEMAS[name]||e.BonusModel.legacySchemas[name]||{label:name,fields:e.UnitKeys.fields[name]||e.AttendanceEvents.fields[name]};
    for(const address of sheet.cells.keys())if(/^[A-Z]+1$/.test(address))headers.set(column(address),book.get(name,address).value);
    if(name==='BonusEffect'&&[...headers.values()].includes('UnitKey'))schema=e.BonusModel.legacySchemas.BonusEffect;
    else if(name==='BonusEffect'&&[...headers.values()].includes('Every'))schema=e.BonusModel.legacySchemas.BonusEffectV2;
    // Normalize old column names before validating, including ambiguous old/new duplicates.
    for(const [col,key]of headers){const field=schema.fields.find(f=>f.legacyAliases?.includes(key));if(field)headers.set(col,field.key);}
    let fields=schema.fields.filter(f=>!f.hidden&&f.export!==false);
    if(name==='Unit'&&![...headers.values()].includes('UnitKey'))fields=fields.filter(f=>!['UnitKey','BaseStatKey','UpgradeKey','LevelBonusKey'].includes(f.key));
    if(name==='AttendancePlan')fields=fields.filter(f=>!['StartAt','EndAt','ClaimEndAt','RepeatMode','Enabled'].includes(f.key)||[...headers.values()].includes(f.key));
    if(name==='Party'&&![...headers.values()].includes('ChiefId'))fields=fields.filter(f=>f.key!=='ChiefId').concat(schema.fields.filter(f=>f.key==='ChiefAsset'));
    // Accept the previous workbook layout while exporting only direct attendance rewards.
    if(name==='Attendance'&&![...headers.values()].includes('Rewards'))fields=schema.fields.filter(f=>f.key!=='Rewards'&&f.export!==false);
    if(name==='UnitMilestone')fields=fields.filter(f=>!['EffectStatus','EffectId','EffectParams'].includes(f.key)||[...headers.values()].includes(f.key));
    if(name==='ResearchNode')fields=fields.filter(f=>f.key!=='IconAddress'||[...headers.values()].includes(f.key));
    if(fields.some(f=>![...headers.values()].includes(f.key))||new Set(headers.values()).size!==headers.size)throw Error(`${name}: 열 이름이 양식과 다릅니다.`);
    const rowNumbers=[...new Set([...sheet.cells.keys()].map(a=>Number(a.match(/\d+$/)[0])).filter(n=>n>1))].sort((a,b)=>a-b);
    tables[name]=rowNumbers.map(n=>{
      const result={};
      for(const [col,key]of headers){
        const field=fields.find(f=>f.key===key);if(!field)continue;
        const cell=book.get(name,col+n),v=cell.value;
        if(cell.formula!==null)throw Error(`${name} ${n}행: 입력 셀의 수식은 지원하지 않습니다.`);
        if(['rewards','costs'].includes(field.type)){
          try{result[key]=e.RewardCore.parseItems(v);}catch(error){throw Error(`${name} ${n}행 ${key}: ${error.message}`);}
        }
        else if(['parents','refs','strings'].includes(field.type))result[key]=v?v.split(';').map(s=>s.trim()):[];
        else if(['int','float'].includes(field.type)){if(v.trim()===''||!Number.isFinite(Number(v)))throw Error(`${name} ${n}행: ${key} 숫자가 필요합니다.`);result[key]=Number(v);}
        else if(field.type==='bool'){if(!['0','1','true','false'].includes(v))throw Error(`${name} ${n}행: ${key}는 0 또는 1입니다.`);result[key]=['1','true'].includes(v);}
        else result[key]=v;
      }return result;
    });count+=tables[name].length;
  }
  return {tables,names,count,sha256:hash(bytes),label:names.map(n=>e.STUDIO_SCHEMAS[n]?.label||({Units:'유닛',BaseStats:'기본 스탯',UpgradeStats:'강화 증가량',LevelBonuses:'레벨 보너스'}[n])).join(' · ')};
}
function read(bytes,project){
  const e=engine(),parsed=inspect(bytes),p=e.Studio.shape(project);
  if(parsed.names.some(n=>e.MissionEvents.names.includes(n))){
    for(const name of ['DataBindings','MissionEvents','Missions'])if(!parsed.tables[name])throw Error('Mission workbook requires DataBindings, MissionEvents and Missions.');
    for(const [name,expected]of [['EventData',e.EVENT_CATALOG.data],['ExecutionHooks',e.EVENT_CATALOG.hooks]])if(parsed.tables[name]){
      const key=e.STUDIO_SCHEMAS[name].keys[0];
      for(const row of parsed.tables[name]){const original=expected.find(r=>r[key]===row[key]);if(!original||Object.keys(original).some(k=>original[k]!==row[k]))throw Error(name+': developer catalogue cannot be modified: '+row[key]);}
    }
  }
  const evented=Object.keys(parsed.tables).some(n=>e.AttendanceEvents.headers[n]);
  if(evented){
    if(!parsed.tables.AttendanceEvents||!parsed.tables.AttendanceRewards)throw Error('출석 이벤트 Excel에는 AttendanceEvents / AttendanceRewards 시트가 모두 필요합니다.');
    p.tables.AttendancePlan=parsed.tables.AttendanceEvents.map(r=>({...r,AttendanceId:r.EventKey,Source:'Excel event import'}));
    p.tables.Attendance=parsed.tables.AttendanceRewards.map(r=>({AttendanceId:r.EventKey,Day:r.Day,Rewards:r.Rewards,RewardGroupId:'',Source:'Excel event import'}));
  }
  const keyed=Object.keys(parsed.tables).some(n=>e.UnitKeys.headers[n]);
  if(keyed){
    for(const name of Object.keys(e.UnitKeys.headers))if(!parsed.tables[name])throw Error('유닛 Excel에는 Units / BaseStats / UpgradeStats / LevelBonuses 시트가 모두 필요합니다.');
    for(const [name,keys]of Object.entries({Units:['UnitKey'],BaseStats:['BaseStatKey','Tier'],UpgradeStats:['UpgradeKey','TargetLevel'],LevelBonuses:['LevelBonusKey','Level']})){
      const seen=new Set();for(const row of parsed.tables[name]){const key=JSON.stringify(keys.map(k=>row[k]));if(seen.has(key))throw Error(name+': 중복 키 '+key);seen.add(key);}
    }
    const old=p.tables.Unit;
    p.tables.Unit=parsed.tables.Units.map(r=>({...e.Studio.newRecord('Unit',p),...old.find(u=>u.UnitKey===r.UnitKey),UnitId:r.UnitKey,UnitKey:r.UnitKey,Name:r.Name,Kind:r.Kind,BackendIds:r.BackendKeys,IconKey:r.IconKey,MaxLevel:r.MaxLevel,BaseStatKey:r.BaseStatKey,UpgradeKey:r.UpgradeKey,LevelBonusKey:r.LevelBonusKey}));
    p.tables.UnitBaseStat=parsed.tables.BaseStats.map(({AttackFrequency,...row})=>({...row,AttackInterval:AttackFrequency}));
    p.tables.UnitUpgrade=[];p.tables.UnitMilestone=[];
    for(const unit of p.tables.Unit){
      let multiplier=1,global=0;
      for(const r of parsed.tables.UpgradeStats.filter(r=>unit.UpgradeKey&&r.UpgradeKey===unit.UpgradeKey).sort((a,b)=>a.TargetLevel-b.TargetLevel)){
        multiplier=Number((multiplier+r.MultiplierIncrease).toPrecision(12));global=Number((global+r.GlobalBonusIncrease).toPrecision(12));
        p.tables.UnitUpgrade.push({UnitId:unit.UnitId,TargetLevel:r.TargetLevel,Costs:r.Costs,Multiplier:multiplier,GlobalBonus:global,Source:'Excel key import'});
      }
      for(const r of parsed.tables.LevelBonuses.filter(r=>unit.LevelBonusKey&&r.LevelBonusKey===unit.LevelBonusKey))p.tables.UnitMilestone.push({UnitId:unit.UnitId,Level:r.Level,Description:r.Description,EffectStatus:r.EffectStatus||'DescriptionOnly',EffectId:r.EffectKey||'',EffectParams:r.EffectParams||'{}',Source:'Excel key import'});
    }
    for(const r of parsed.tables.UpgradeStats)if(!p.tables.Unit.some(u=>u.UpgradeKey===r.UpgradeKey))throw Error('연결되지 않은 강화 키: '+r.UpgradeKey);
    for(const r of parsed.tables.LevelBonuses)if(!p.tables.Unit.some(u=>u.LevelBonusKey===r.LevelBonusKey))throw Error('연결되지 않은 보너스 키: '+r.LevelBonusKey);
  }
  for(const [name,rows]of Object.entries(parsed.tables).filter(([name])=>!e.UnitKeys.headers[name]&&!e.AttendanceEvents.headers[name]))p.tables[name]=rows.map(row=>{
    const schema=name==='BonusDescription'||name==='BonusEffect'&&Object.hasOwn(row,'UnitKey')?e.BonusModel.legacySchemas[name]:e.STUDIO_SCHEMAS[name],old=p.tables[name]?.find(r=>schema.keys.every(k=>r[k]===row[k]));
    return {...(old||(e.STUDIO_SCHEMAS[name]?e.Studio.newRecord(name,p):{})),...row};
  });
  if(parsed.tables.Party)parsed.tables.Party.forEach((row,index)=>{
    const party=p.tables.Party[index];
    if(Object.hasOwn(row,'ChiefId'))party.ChiefAsset=e.PlannerScope.chiefPaths.get(row.ChiefId)||'';
    else party.ChiefId=[...e.PlannerScope.chiefPaths].find(([,asset])=>asset===party.ChiefAsset)?.[0]||'';
  });
  if(parsed.tables.UnitMilestone)parsed.tables.UnitMilestone.forEach((row,index)=>{
    if(!Object.hasOwn(row,'EffectStatus'))Object.assign(p.tables.UnitMilestone[index],{EffectStatus:'DescriptionOnly',EffectId:'',EffectParams:'{}'});
  });
  if(parsed.tables.Attendance)parsed.tables.Attendance.forEach((row,index)=>{
    if(!Object.hasOwn(row,'Rewards')){
      const group=p.tables.RewardGroup.find(g=>g.RewardGroupId===row.RewardGroupId);
      if(!group)throw Error(`출석 ${index+1}행: 연결된 이전 보상 묶음이 없습니다.`);
      p.tables.Attendance[index].Rewards=JSON.parse(JSON.stringify(group.Rewards));
    }
  });
  if(parsed.tables.BonusEffect?.some(r=>Object.hasOwn(r,'UnitKey')))p.meta.unitBonusModel='html-20261030';
  const clean=e.Studio.shape(p),errors=e.Studio.validate(clean).filter(i=>i.severity==='error');
  if(errors.length)throw Error(errors.slice(0,4).map(i=>`${e.STUDIO_SCHEMAS[i.table].label} ${i.index+1}행: ${i.message}`).join('\n'));
  return {project:JSON.parse(JSON.stringify(clean)),names:[...(keyed?['Unit','UnitBaseStat','UnitUpgrade','UnitMilestone']:[]),...(evented?['AttendancePlan','Attendance']:[]),...parsed.names.filter(n=>!e.UnitKeys.headers[n]&&!e.AttendanceEvents.headers[n])]};
}
function toRuntime(project,names){
  const e=engine(),p=e.Studio.shape(project);e.Studio.tables(p);
  const result={schemaVersion:4};
  if(names.includes('BonusDefinition')||names.includes('BonusEffect')){result.bonusDefinitions=p.tables.BonusDefinition;result.bonusEffects=p.tables.BonusEffect;}
  if(names.includes('Attendance')||names.includes('AttendanceRewards'))result.attendanceEvents=p.tables.AttendancePlan.map(plan=>({eventKey:plan.AttendanceId,name:plan.Name,startAt:plan.StartAt,endAt:plan.EndAt,claimEndAt:plan.ClaimEndAt||plan.EndAt,enabled:plan.Enabled,repeatMode:plan.RepeatMode,cycleDays:plan.CycleDays,days:p.tables.Attendance.filter(r=>r.AttendanceId===plan.AttendanceId).sort((a,b)=>a.Day-b.Day).map(r=>({day:r.Day,rewards:r.Rewards.map(x=>({itemKey:x.itemId,amount:x.amount}))}))}));
  for(const [table,key]of [['ItemFunction','itemFunctions'],['SelectionList','selectionLists'],['SelectionOption','selectionOptions'],['ResearchTree','researchTrees'],['ResearchNode','researchNodes'],['Tutorial','tutorialSteps'],['Party','parties'],['Profile','profiles'],['Economy','economy'],['AccountLevel','accountLevels'],['BattleReward','battleRewards'],['Item','items']])if(names.includes(table))result[key]=p.tables[table].map(r=>Object.fromEntries(e.STUDIO_SCHEMAS[table].fields.filter(f=>!f.hidden&&f.export!==false).map(f=>[f.key,r[f.key]])));
  if(names.some(n=>e.UnitKeys.sheetFor[n]||e.UnitKeys.headers[n])){
    for(const [name,rows]of Object.entries(e.UnitKeys.tables(p)))result[{Units:'units',BaseStats:'baseStats',UpgradeStats:'upgradeStats',LevelBonuses:'levelBonuses'}[name]]=rows.slice(1).map(row=>Object.fromEntries(rows[0].map((key,i)=>[key,key==='Costs'?e.RewardCore.parseItems(row[i]).map(r=>({itemKey:r.itemId,amount:r.amount})):key==='EffectParams'?JSON.parse(row[i]):row[i]])));
  }
  if(names.some(n=>e.MissionEvents.names.includes(n)))Object.assign(result,e.MissionEvents.runtime(p));
  return result;
}
function contract(names){
  const e=engine();names=[...new Set(names.flatMap(n=>e.UnitKeys.sheetFor[n]?Object.keys(e.UnitKeys.headers):e.AttendanceEvents.sheetFor[n]?Object.keys(e.AttendanceEvents.headers):[n]))];return {schemaVersion:4,headerRow:1,firstDataRow:2,rewardEncoding:'ItemKey:positiveInteger;ItemKey:positiveInteger',booleanEncoding:'0 or 1',formulaPolicy:'reject',tables:names.map(name=>({sheet:name,keys:({AttendanceEvents:['EventKey'],AttendanceRewards:['EventKey','Day'],Units:['UnitKey'],BaseStats:['BaseStatKey','Tier'],UpgradeStats:['UpgradeKey','TargetLevel'],LevelBonuses:['LevelBonusKey','Level']}[name]||e.STUDIO_SCHEMAS[name].keys),columns:(e.UnitKeys.fields[name]||e.AttendanceEvents.fields[name]||e.STUDIO_SCHEMAS[name].fields).filter(f=>!f.hidden&&f.export!==false).map(f=>({key:f.key,type:f.type,required:!!f.required,...(f.values?{values:f.values}:{}),...(f.min!==undefined?{min:f.min}:{}),...(f.max!==undefined?{max:f.max}:{}),...(f.unit?{unit:f.unit}:{}),...(f.legacyAliases?{legacyAliases:f.legacyAliases}:{})}))}))};
}
module.exports={engine,bundles,write,inspect,read,toRuntime,contract};
