const assert=require('node:assert/strict'),fs=require('node:fs');
const bonus=require('./bonus-excel.cjs'),engine=require('./app/content-workbook.cjs').engine();
const rows=JSON.parse(JSON.stringify(engine.BonusModel.legacyRows(engine.Studio.create())));
const bytes=bonus.write(rows),back=bonus.read(bytes);assert.deepEqual(back,rows);assert.equal(rows.length,39);
assert.throws(()=>bonus.validate([{...rows[0],EffectType:'Unknown'}]));assert.throws(()=>bonus.validate([{...rows[0],Value:Infinity}]));assert.throws(()=>bonus.validate([...rows,rows[0]]));
assert.throws(()=>bonus.validate([{...rows.find(r=>r.Stat==='RecoveryNextBasic'),UnitKey:'UnitData_Betan'}]));
const project=JSON.parse(JSON.stringify(engine.Studio.create()));const applied=bonus.apply(project,back);assert.equal(applied.tables.UnitMilestone.filter(x=>x.EffectId==='CompositeBonus').length,30);
assert.equal(applied.tables.BonusEffect.length,30);
if(process.argv.includes('--write')){fs.mkdirSync('outputs/unit-level-bonuses',{recursive:true});fs.writeFileSync('outputs/unit-level-bonuses/UnitLevelBonuses.xlsx',bytes);fs.writeFileSync('outputs/unit-level-bonuses/bonus-parts.json',JSON.stringify(rows,null,2));}
console.log('PASS 39 bonus parts Excel roundtrip, 30 milestone summaries, invalid class/number/duplicate/owner rejection');
