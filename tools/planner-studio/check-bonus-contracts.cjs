'use strict';
const assert=require('node:assert/strict'),fs=require('node:fs');
const excel=require('./app/content-workbook.cjs'),{table}=require('./app/readable-chart.cjs');
const e=excel.engine(),M=e.BonusModel,C=M.contracts,p=e.Studio.create(),clone=x=>JSON.parse(JSON.stringify(x));
for(const [type,value]of Object.entries(C.defaults)){const args=C.parse({EffectType:type,Value:value});assert.equal(Object.keys(args).length,C.fields[type].length);}
for(const value of ['15','15,2,3','15,','-1,2','NaN,2','1e999,2','9007199254740992,2'])assert.throws(()=>C.parse({EffectType:'HackingAttackSpeedBonus',Value:value}));
assert.throws(()=>C.parse({EffectType:'HackingProductionBonus',Value:'1,1,2.5,3'}));
const multi=clone(p);multi.tables.BonusEffect.push({BonusId:'deltan_lv15',EffectKey:'extraAttack',EffectType:'StatModifierBonus',Stat:'Attack',ValueMode:'Flat',Value:'7'});M.validate(multi);assert.equal(M.legacyRows(multi).length,40);
const draft=clone(p);const broken=draft.tables.BonusEffect.find(r=>r.EffectType==='HackingAttackSpeedBonus');broken.Value='15,';e.Studio.update(draft,'BonusEffect',draft.tables.BonusEffect.indexOf(broken),'Value',broken.Value);assert(e.Studio.validate(draft).some(r=>r.severity==='error'));assert.throws(()=>excel.write(draft,['BonusDefinition','BonusEffect']));
const expanded=C.expand(p);assert.equal(expanded.tables.BonusEffect.length,39);assert.deepEqual(clone(M.legacyRows(C.compact(expanded))),clone(M.legacyRows(p)));
const upload=table(p,'unitBonuses');assert.equal(upload.length,61);for(const name of ['Name','UnitName','Every','CycleId','DamageBonusPercent','ChargeReductionPercent'])assert(!upload[0].includes(name));
// Produce the previous named-effect workbook using its original schemas, then import through the current reader.
const schema=e.STUDIO_SCHEMAS.BonusEffect;e.STUDIO_SCHEMAS.BonusEffect=M.legacySchemas.BonusEffectV2;
const oldBytes=Buffer.from(e.RewardCore.xlsx('BonusEffect',[e.STUDIO_SCHEMAS.BonusEffect.fields.map(f=>f.key),...expanded.tables.BonusEffect.map(r=>e.STUDIO_SCHEMAS.BonusEffect.fields.map(f=>r[f.key]??''))]));
e.STUDIO_SCHEMAS.BonusEffect=schema;
const imported=excel.read(oldBytes,p).project;assert.deepEqual(clone(M.legacyRows(imported)),clone(M.legacyRows(p)));
console.log('PASS 7 Value contracts; malformed CSV/arity/range; multiple effect rows; incomplete draft validation; scalar headers; 39-part parity and old v2 XLSX migration');
