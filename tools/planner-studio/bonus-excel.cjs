'use strict';
const fs=require('node:fs'),path=require('node:path');
const workbook=require('./app/content-workbook.cjs');
const {open}=require('./app/workbook.cjs');
const {headers,modes,validate}=require('./app/bonus-validation.cjs');
function write(rows){validate(rows);return Buffer.from(workbook.engine().RewardCore.xlsx('LevelBonusParts',[headers,...rows.map(r=>headers.map(k=>r[k]??''))]));}
function read(bytes){const b=open(bytes),sheet='LevelBonusParts';if(!b.sheets.has(sheet))throw Error('LevelBonusParts 시트 필요');const cells=b.sheets.get(sheet).cells;const cols=headers.map((_,i)=>String.fromCharCode(65+i));headers.forEach((h,i)=>{if(b.get(sheet,cols[i]+'1').value!==h)throw Error('열 이름 오류: '+h);});const numbers=[...new Set([...cells.keys()].map(a=>Number(a.match(/\d+$/)[0])).filter(n=>n>1))].sort((a,b)=>a-b);return validate(numbers.map(n=>Object.fromEntries(headers.map((h,i)=>{const c=b.get(sheet,cols[i]+n);if(c.formula!==null)throw Error('수식은 지원하지 않음');const value=c.value;return [h,['Level','PartIndex','Value','Every','DamageBonusPercent','ChargeReductionPercent'].includes(h)&&value!==''?Number(value):value];}))));}
function apply(project,rows){
 validate(rows);const e=workbook.engine(),p=e.BonusModel.expand(e.Studio.shape(project)),groups=new Map();
 for(const row of rows){const unit=p.tables.Unit.find(u=>(u.UnitKey||u.UnitId)===row.UnitKey);if(!unit||!['Unit','Chief'].includes(unit.Kind)||row.Level>unit.MaxLevel)throw Error('유닛/최대 레벨 오류: '+row.UnitKey);const key=row.UnitKey+'|'+row.Level;if(!groups.has(key))groups.set(key,[]);groups.get(key).push(row);}
 for(const group of groups.values()){
  const first=group[0];let bonus=p.tables.BonusDefinition.find(b=>b.UnitKey===first.UnitKey&&b.Level===first.Level);
  if(!bonus){bonus={BonusId:e.BonusModel.bonusId(first.UnitKey,first.Level),UnitKey:first.UnitKey,Level:first.Level,Name:first.UnitKey+' Lv.'+first.Level,DescriptionTemplate:'',HighlightNumbers:true,LinkTerms:true,EffectLink:'None'};p.tables.BonusDefinition.push(bonus);}
  const previous=p.tables.BonusEffect.filter(r=>r.BonusId===bonus.BonusId).sort((a,b)=>a.EffectKey<b.EffectKey?-1:1);p.tables.BonusEffect=p.tables.BonusEffect.filter(r=>r.BonusId!==bonus.BonusId);
  const used=new Set();
  const effects=group.sort((a,b)=>a.PartIndex-b.PartIndex).map(({UnitKey,Level,PartIndex,...r})=>{const match=previous.find(x=>x.EffectType===r.EffectType&&x.Stat===r.Stat&&!used.has(x.EffectKey));let key=match?.EffectKey||e.BonusModel.keyFor(r),n=2;while(used.has(key))key=e.BonusModel.keyFor(r)+'_'+n++;used.add(key);return {...r,BonusId:bonus.BonusId,EffectKey:key,Stat:r.Stat||'Attack',ValueMode:r.ValueMode||'Percent',Value:r.Value===''?0:r.Value,Every:r.Every===''?0:r.Every,DamageBonusPercent:r.DamageBonusPercent===''?0:r.DamageBonusPercent,ChargeReductionPercent:r.ChargeReductionPercent===''?0:r.ChargeReductionPercent};});
  p.tables.BonusEffect.push(...effects);if(!bonus.DescriptionTemplate)bonus.DescriptionTemplate=effects.map(r=>r.EffectType==='StatModifierBonus'?r.Stat+' +{'+r.EffectKey+'.Value}': '매 {'+r.EffectKey+'.Every}번째 강화').join(', ');
 }
 return e.Studio.shape(p);
}
module.exports={headers,modes,validate,write,read,apply};
if(require.main===module){const [command,input,project,output]=process.argv.slice(2);if(command==='export')fs.writeFileSync(project,write(JSON.parse(fs.readFileSync(input,'utf8'))));else if(command==='import'){if(fs.existsSync(output))throw Error('출력 파일이 이미 존재합니다');fs.writeFileSync(output,JSON.stringify(apply(JSON.parse(fs.readFileSync(project,'utf8')),read(fs.readFileSync(input))),null,2));}else throw Error('export rows.json output.xlsx | import edited.xlsx project.json new-project.json');}
