const assert=require('node:assert/strict'),fs=require('node:fs');
const excel=require('./app/content-workbook.cjs');const {table}=require('./app/readable-chart.cjs');
const e=excel.engine(),p=JSON.parse(JSON.stringify(e.Studio.create()));
assert.deepEqual(JSON.parse(JSON.stringify(e.Studio.validate(p).filter(x=>x.severity==='error'))),[]);
assert.equal(p.tables.BonusEffect.length,39);assert.equal(p.tables.BonusDescription.length,30);

const legacy=JSON.parse(JSON.stringify(p));delete legacy.meta.unitBonusModel;legacy.tables.BonusEffect=JSON.parse(JSON.stringify(e.UNIT_BONUS_PREVIOUS_DEFAULTS));legacy.tables.BonusDescription=legacy.tables.BonusDescription.filter(r=>r.UnitKey==='UnitData_Betan');
const migrated=e.Studio.shape(legacy);assert.equal(migrated.tables.BonusEffect.length,39);assert.equal(migrated.tables.BonusDescription.length,30);
const edited=JSON.parse(JSON.stringify(legacy));edited.tables.BonusEffect.find(r=>r.UnitKey==='UnitData_Deltan'&&r.Level===3).Value=77;
assert.equal(e.Studio.shape(edited).tables.BonusEffect.find(r=>r.UnitKey==='UnitData_Deltan'&&r.Level===3).Value,77);
assert.equal(migrated.tables.UnitMilestone.filter(r=>r.EffectStatus==='Defined').length,30);
assert.equal(e.Studio.shape(migrated).tables.BonusEffect.length,39);
const deliberate=JSON.parse(JSON.stringify(migrated));deliberate.tables.BonusEffect.find(r=>r.UnitKey==='UnitData_Deltan'&&r.Level===3).Value=5;assert.equal(e.Studio.shape(deliberate).tables.BonusEffect.find(r=>r.UnitKey==='UnitData_Deltan'&&r.Level===3).Value,5);
const full=excel.read(excel.write(p,['Unit','UnitBaseStat','UnitUpgrade','UnitMilestone','BonusEffect','BonusDescription']),p).project;assert.deepEqual(full.tables.BonusEffect,p.tables.BonusEffect);assert.deepEqual(full.tables.BonusDescription,p.tables.BonusDescription);
const bytes=excel.write(p,['BonusEffect','BonusDescription']);const back=excel.read(bytes,p).project;
assert.deepEqual(back.tables.BonusEffect,p.tables.BonusEffect);assert.deepEqual(back.tables.BonusDescription,p.tables.BonusDescription);

const rows=table(p,'unitBonuses');assert.equal(rows.length,70);
const old=JSON.parse(JSON.stringify(p));delete old.tables.BonusEffect;delete old.tables.BonusDescription;
assert.equal(e.Studio.shape(old).tables.BonusDescription.length,30);
const bad=JSON.parse(JSON.stringify(p));bad.tables.BonusDescription[0].Description='<color=red>오류</color>';assert.ok(e.Studio.validate(bad).some(x=>x.table==='BonusDescription'&&x.severity==='error'));
if(process.argv.includes('--write')){const directory='outputs/bonus-editor-20261030';fs.mkdirSync(directory,{recursive:true});fs.writeFileSync(directory+'/UnitLevelBonuses_작성용_2026-10-30.xlsx',bytes);fs.writeFileSync(directory+'/UnitLevelBonuses_2026-10-30.xlsx',Buffer.from(e.RewardCore.xlsx('UnitLevelBonuses',rows)));fs.writeFileSync(directory+'/project.json',JSON.stringify(p,null,2));fs.writeFileSync('outputs/bonus-description-rows.json',JSON.stringify(rows.slice(1).map(row=>Object.fromEntries(rows[0].map((key,i)=>[key,String(row[i])])))));}
console.log('PASS real editor schema, 39 effects / 30 drone and chief descriptions, Excel roundtrip, legacy migration, raw tag rejection');
