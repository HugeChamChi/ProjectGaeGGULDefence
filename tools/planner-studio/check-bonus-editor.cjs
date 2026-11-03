const assert=require('node:assert/strict'),fs=require('node:fs');
const excel=require('./app/content-workbook.cjs'),{table}=require('./app/readable-chart.cjs');
const e=excel.engine(),clone=x=>JSON.parse(JSON.stringify(x)),p=clone(e.Studio.create()),M=e.BonusModel;
assert.deepEqual(clone(e.Studio.validate(p).filter(x=>x.severity==='error')),[]);
assert.equal(p.tables.BonusDefinition.length,30);assert.equal(p.tables.BonusEffect.length,30);assert(!p.tables.BonusDescription);
for(const b of p.tables.BonusDefinition){assert(M.render(b,p.tables.BonusEffect));assert.equal(p.tables.BonusEffect.filter(r=>r.BonusId===b.BonusId).length>0,true);}
const edit=clone(p),bonus=edit.tables.BonusDefinition.find(r=>r.BonusId==='deltan_lv15');
edit.tables.BonusEffect.find(r=>r.BonusId===bonus.BonusId&&r.EffectType==='HackingAttackSpeedBonus').Value='23.5,8';
assert(M.render(bonus,edit.tables.BonusEffect).includes('8초'));assert(M.render(bonus,edit.tables.BonusEffect).includes('23.5%'));
M.renameBonus(edit,bonus,'custom_delta');assert(edit.tables.BonusEffect.filter(r=>r.BonusId==='custom_delta').length===1);
const effect=edit.tables.BonusEffect.find(r=>r.BonusId===bonus.BonusId&&r.EffectType==='HackingAttackSpeedBonus');M.renameEffect(edit,effect,'speed');assert(bonus.DescriptionTemplate.includes('{speed.Value}'));M.validate(edit);
const legacy=clone(p);delete legacy.tables.BonusDefinition;legacy.tables.BonusEffect=clone(e.UNIT_BONUS_DEFAULTS);legacy.tables.BonusDescription=clone(e.UNIT_BONUS_DESCRIPTIONS);
legacy.tables.BonusEffect.find(r=>r.UnitKey==='UnitData_Deltan'&&r.Level===15&&r.Stat==='HackingPostSpeed').Value=27;
const migrated=e.Studio.shape(legacy);assert.equal(migrated.tables.BonusDefinition.length,30);assert.equal(migrated.tables.BonusEffect.find(r=>r.BonusId==='deltan_lv15'&&r.EffectType==='HackingAttackSpeedBonus').Value,'27,2');assert(M.render(migrated.tables.BonusDefinition.find(r=>r.BonusId==='deltan_lv15'),migrated.tables.BonusEffect).includes('27%'));
const previous=clone(legacy);delete previous.meta.unitBonusModel;previous.tables.BonusEffect=clone(e.UNIT_BONUS_PREVIOUS_DEFAULTS);assert.equal(e.Studio.shape(previous).tables.BonusEffect.length,30);
for(const mutate of [q=>q.tables.BonusEffect[0].BonusId='missing',q=>q.tables.BonusDefinition[0].DescriptionTemplate='{missing.Value}',q=>q.tables.BonusDefinition[0].DescriptionTemplate='{attack.Every}',q=>q.tables.BonusDefinition[0].DescriptionTemplate='<color=red>x</color>',q=>q.tables.BonusEffect.push({...q.tables.BonusEffect[0]}),q=>q.tables.BonusEffect.push({...q.tables.BonusEffect[0],EffectKey:'duplicateStat'})]){const bad=clone(p);mutate(bad);assert(e.Studio.validate(e.Studio.shape(bad)).some(r=>r.severity==='error'));}
const bytes=excel.write(p,['BonusDefinition','BonusEffect']),back=excel.read(bytes,p).project;assert.deepEqual(back.tables.BonusDefinition,p.tables.BonusDefinition);assert.deepEqual(back.tables.BonusEffect,p.tables.BonusEffect);
const full=excel.read(excel.write(p,['Unit','UnitBaseStat','UnitUpgrade','UnitMilestone','BonusDefinition','BonusEffect']),p).project;assert.deepEqual(full.tables.BonusEffect,p.tables.BonusEffect);
assert.deepEqual(clone(e.UnitKeys.headers.LevelBonuses),['LevelBonusKey','Level','Description']);
const rows=table(p,'unitBonuses');assert.equal(rows.length,61);assert(!rows[0].includes('PartIndex'));assert(!rows[0].includes('Description'));assert(!rows[0].includes('UnitName'));assert(!rows[0].includes('Name'));
if(process.argv.includes('--write')){const dir='outputs/bonus-model-v2-20261030';fs.mkdirSync(dir,{recursive:true});fs.writeFileSync(dir+'/UnitLevelBonuses_작성용_2026-10-30.xlsx',bytes);fs.writeFileSync(dir+'/UnitLevelBonuses_2026-10-30.xlsx',Buffer.from(e.RewardCore.xlsx('UnitLevelBonuses',rows)));fs.writeFileSync(dir+'/project.json',JSON.stringify(p,null,2));const objects=r=>r.slice(1).map(row=>Object.fromEntries(r[0].map((key,i)=>[key,String(row[i])])));fs.writeFileSync('outputs/bonus-description-rows.json',JSON.stringify(objects(rows)));fs.writeFileSync(dir+'/modified-rows.json',JSON.stringify(objects(table(edit,'unitBonuses'))));}
console.log('PASS 30 owned bonuses/30 effect rows, live numeric templates, ID rename, legacy value migration, FK/token/type/duplicate rejection, 2/6-sheet Excel roundtrip and scalar upload');
