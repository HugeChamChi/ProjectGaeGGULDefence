const assert=require('node:assert/strict');
const e=require('./app/content-workbook.cjs').engine(),p=JSON.parse(JSON.stringify(e.Studio.create()));
for(const bonus of p.tables.BonusDefinition)assert(!e.BonusModel.render(bonus,p.tables.BonusEffect).includes('%p'));
const critical=p.tables.BonusEffect.find(r=>r.BonusId==='betan_lv15');assert.equal(critical.ValueMode,'PercentagePoints');assert.equal(critical.Value,'50');
const bonus=p.tables.BonusDefinition.find(r=>r.BonusId==='betan_lv15');bonus.DescriptionTemplate='치명타 피해 +{'+critical.EffectKey+'.Value}%p';assert.equal(e.BonusModel.render(bonus,p.tables.BonusEffect),'치명타 피해 +50%');
console.log('PASS percentage display without p; additive value mode and value50 preserved; legacy text normalized');
