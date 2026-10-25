'use strict';
const assert=require('node:assert/strict'),fs=require('node:fs'),os=require('node:os'),path=require('node:path');
const excel=require('./app/content-workbook.cjs'),e=excel.engine(),p=e.Studio.create();
const folder=fs.mkdtempSync(path.join(os.tmpdir(),'ggd-workbooks-'));
for(const [name,rows]of Object.entries(e.Studio.tables(p))){
 for(const field of ['Note','Source','ChiefAsset','LegacySaveName','GrowthPerLevel'])assert(!rows[0].includes(field),name+':'+field);
 assert(!JSON.stringify(rows).includes('Assets/'),name);
}
const partyBytes=excel.write(p,['Party']),partyLoaded=excel.read(partyBytes,p).project;
assert.equal(partyLoaded.tables.Party[0].ChiefAsset,p.tables.Party[0].ChiefAsset);
assert.equal(excel.toRuntime(p,['Party']).parties[0].ChiefId,'AlphanChief');
assert(!JSON.stringify(excel.toRuntime(p,['Party','Unit','ResearchNode'])).includes('Assets/'));
assert(!excel.contract(['Party','Unit']).tables.some(t=>t.columns.some(c=>['Source','Note','ChiefAsset'].includes(c.key))));
const oldPartyRows=[['PartyId','Name','IsUnlocked','UseUniformSummonPool','Units','ChiefAsset','Source'],...p.tables.Party.map(r=>[r.PartyId,r.Name,r.IsUnlocked?1:0,r.UseUniformSummonPool?1:0,r.Units.join(';'),r.ChiefAsset,r.Source])];
assert.equal(excel.read(e.RewardCore.xlsx('Party',oldPartyRows),p).project.tables.Party[0].ChiefId,'AlphanChief');
for(const [kind,names]of Object.entries(excel.bundles)){
 const bytes=excel.write(p,names),loaded=excel.read(bytes,p).project;
 for(const name of names){
  const visible=e.STUDIO_SCHEMAS[name].fields.filter(f=>!f.hidden&&f.export!==false).map(f=>f.key);
  assert.equal(JSON.stringify(loaded.tables[name].map(r=>visible.map(k=>r[k])).sort((a,b)=>JSON.stringify(a).localeCompare(JSON.stringify(b)))),JSON.stringify(p.tables[name].map(r=>visible.map(k=>r[k])).sort((a,b)=>JSON.stringify(a).localeCompare(JSON.stringify(b)))));
 }
 fs.writeFileSync(path.join(folder,kind+'.xlsx'),bytes);
}
const edited=e.Studio.clone(p);edited.tables.Attendance[0].Rewards=[{itemId:'Gold',amount:500},{itemId:'Diamond',amount:10},{itemId:'Stamina',amount:30}];
const data=excel.write(edited,excel.bundles.rewards);assert.equal(JSON.stringify(excel.read(data,p).project.tables.Attendance[0].Rewards),JSON.stringify(edited.tables.Attendance[0].Rewards));
// Import rejects broken cross references; writing also rejects invalid values.
edited.tables.Attendance[0].Rewards[0].amount=-1;assert.throws(()=>excel.write(edited,excel.bundles.rewards));
const {zipSync,unzipSync,strFromU8,strToU8}=require('./app/node_modules/fflate');
const parts=unzipSync(data);parts['xl/worksheets/sheet2.xml']=strToU8(strFromU8(parts['xl/worksheets/sheet2.xml']).replace('Gold:500','MissingItem:500'));assert.throws(()=>excel.read(zipSync(parts),p));
const defined=e.Studio.clone(p),bonus=defined.tables.UnitMilestone[0];
bonus.EffectStatus='Defined';bonus.EffectId='skill_cooldown_reduction';bonus.EffectParams='{"percent":20,"durationSeconds":5}';
const bonusBytes=excel.write(defined,excel.bundles.enhancement),loadedBonus=excel.read(bonusBytes,p).project;
const runtime=excel.toRuntime(loadedBonus,excel.bundles.enhancement);
assert.equal(runtime.levelBonuses.find(b=>b.EffectStatus==='Defined').EffectKey,'skill_cooldown_reduction');
assert.equal(runtime.levelBonuses.find(b=>b.EffectStatus==='Defined').EffectParams.percent,20);
assert.equal(excel.toRuntime(p,excel.bundles.enhancement).levelBonuses[0].EffectStatus,'DescriptionOnly');
bonus.EffectParams='broken';assert.throws(()=>excel.write(defined,excel.bundles.enhancement));
bonus.EffectParams='{"percent":1e400}';assert.throws(()=>excel.write(defined,excel.bundles.enhancement));
const legacy=e.Studio.clone(p);legacy.tables.RewardGroup[0].Rewards[0].amount=77;for(const day of legacy.tables.Attendance)delete day.Rewards;
assert.equal(e.Studio.shape(legacy).tables.Attendance[0].Rewards[0].amount,77);
const oldRows=[['AttendanceId','Day','RewardGroupId','Source'],...p.tables.Attendance.map(r=>[r.AttendanceId,r.Day,r.RewardGroupId,r.Source])];
assert.equal(excel.read(e.RewardCore.xlsx('Attendance',oldRows),p).project.tables.Attendance[0].Rewards[0].amount,10);
const sheets=excel.inspect(data).names;assert.deepEqual(Array.from(sheets),['AttendanceEvents','AttendanceRewards']);
fs.writeFileSync(path.join(folder,'bonus-defined.xlsx'),bonusBytes);
console.log(JSON.stringify({ok:true,checks:['enhancement workbook roundtrip','direct attendance workbook roundtrip','multiple rewards in one cell','invalid amount rejected','missing item rejected','typed bonus effect roundtrip','description-only is not an applied effect','invalid effect JSON rejected','previous JSON and workbook migrated'],folder},null,2));
