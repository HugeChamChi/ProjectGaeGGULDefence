const assert=require('node:assert/strict');
const e=require('./app/content-workbook.cjs').engine(),p=JSON.parse(JSON.stringify(e.Studio.create()));
for(const name of ['BonusDescription','UnitMilestone'])assert(p.tables[name].every(r=>!r.Description.includes('%p')));
const critical=p.tables.BonusEffect.find(r=>r.UnitKey==='UnitData_Betan'&&r.Level===15);assert.equal(critical.ValueMode,'PercentagePoints');assert.equal(critical.Value,50);
p.tables.BonusDescription[0].Description='치명타 피해 +50%p';assert.equal(e.Studio.shape(p).tables.BonusDescription[0].Description,'치명타 피해 +50%');
console.log('PASS percentage display without p; additive value mode and value50 preserved; legacy text normalized');
