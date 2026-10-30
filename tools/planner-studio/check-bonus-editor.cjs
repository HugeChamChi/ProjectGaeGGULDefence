const assert=require('node:assert/strict'),fs=require('node:fs');
const excel=require('./app/content-workbook.cjs');const {table}=require('./app/readable-chart.cjs');
const e=excel.engine(),p=JSON.parse(JSON.stringify(e.Studio.create()));
assert.deepEqual(JSON.parse(JSON.stringify(e.Studio.validate(p).filter(x=>x.severity==='error'))),[]);
assert.equal(p.tables.BonusEffect.length,25);assert.equal(p.tables.BonusDescription.length,10);
const bytes=excel.write(p,['BonusEffect','BonusDescription']);const back=excel.read(bytes,p).project;
assert.deepEqual(back.tables.BonusEffect,p.tables.BonusEffect);assert.deepEqual(back.tables.BonusDescription,p.tables.BonusDescription);
p.tables.BonusDescription.find(r=>r.UnitKey==='AlphanChief'&&r.Level===12).Description='[액티브 스킬] 후 {공격속도효과}';
const rows=table(p,'unitBonuses');assert.equal(rows.length,36);
const old=JSON.parse(JSON.stringify(p));delete old.tables.BonusEffect;delete old.tables.BonusDescription;
assert.equal(e.Studio.shape(old).tables.BonusDescription.length,10);
const bad=JSON.parse(JSON.stringify(p));bad.tables.BonusDescription[0].Description='<color=red>오류</color>';assert.ok(e.Studio.validate(bad).some(x=>x.table==='BonusDescription'&&x.severity==='error'));
if(process.argv.includes('--write')){const directory='outputs/bonus-editor-20261030';fs.mkdirSync(directory,{recursive:true});fs.writeFileSync(directory+'/UnitLevelBonuses_작성용_2026-10-30.xlsx',bytes);fs.writeFileSync(directory+'/UnitLevelBonuses_2026-10-30.xlsx',Buffer.from(e.RewardCore.xlsx('UnitLevelBonuses',rows)));fs.writeFileSync(directory+'/project.json',JSON.stringify(p,null,2));fs.writeFileSync('outputs/bonus-description-rows.json',JSON.stringify(rows.slice(1).map(row=>Object.fromEntries(rows[0].map((key,i)=>[key,String(row[i])])))));}
console.log('PASS real editor schema, 25 effects/5 Betan + 5 chief descriptions, Excel roundtrip, legacy migration, raw tag rejection');
