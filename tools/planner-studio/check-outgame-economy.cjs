'use strict';
const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path'),os=require('node:os');
const excel=require('./app/content-workbook.cjs'),e=excel.engine(),E=e.OutgameEconomy,plain=x=>JSON.parse(JSON.stringify(x));
const p=e.Studio.create(),c=E.config(p),errors=p=>plain(e.Studio.validate(p).filter(i=>i.severity==='error'));
assert.deepEqual(errors(p),[]);
const costs=[120,440,920,1560,2340,3270,4330,5540,6870,8330,9920,11630,13470,15420,15890,16350,16810,17280,17740];
assert.deepEqual(Array.from({length:19},(_,i)=>E.unitCost(c,i+2)),costs);
assert.equal(costs.reduce((a,b)=>a+b),168230);
const nodes=p.tables.ResearchNode.filter(n=>n.TreeKey==='BASIC');
assert.equal(nodes.length,21);assert.equal(nodes.reduce((s,n)=>s+n.MaxLevel,0),99);
assert.equal(nodes.reduce((s,n)=>s+n.BaseCost*n.MaxLevel+n.CostStep*n.MaxLevel*(n.MaxLevel-1)/2,0),1684450);
for(const [id,first,step]of [['1-1',200,50],['6-1',1100,250],['6-2',2200,550],['12-1',38850,9700]]){
  const n=nodes.find(n=>n.NodeId===id);assert.equal(n.BaseCost,first);assert.equal(n.CostStep,step);
}
assert.deepEqual(plain(E.reward(c,100,[{maxHp:400,minHp:200}])),{Gold:39,UpgradeTokenA:3});
assert.deepEqual(plain(E.reward(c,100,[{maxHp:400,minHp:200},{maxHp:400,minHp:200}])),{Gold:79,UpgradeTokenA:7});
assert.deepEqual(plain(E.reward(c,100,[{maxHp:100,minHp:1000},{maxHp:100,minHp:-1000}],.1,.2)),{Gold:66,UpgradeTokenA:7});
assert.deepEqual(plain(E.reward(c,100,[])),{Gold:0,UpgradeTokenA:0});
const expected=[[5,319,31],[10,685,68],[20,1561,156],[30,2679,267],[35,3345,334],[40,4107,410],[50,5931,593],[60,8259,825]];
for(const [count,Gold,UpgradeTokenA]of expected){const bosses=Array.from({length:count},(_,i)=>({maxHp:100*Math.pow(1.13,i)*([3,6,0].includes((i+1)%10)?1.1*Math.pow(1.13,2):1),minHp:0}));assert.deepEqual(plain(E.reward(c,100,bosses)),{Gold,UpgradeTokenA});}
assert.throws(()=>E.reward(c,0,[]));assert.throws(()=>E.reward(c,100,[{maxHp:0,minHp:0}]));assert.throws(()=>E.reward(c,100,[],NaN));assert.throws(()=>E.unitCost(c,21));
const legacy=plain(p);delete legacy.tables.OutgameEconomy;legacy.tables.UnitUpgrade[0].Costs[0].amount=789;legacy.tables.ResearchNode[0].BaseCost=1234;legacy.tables.ResearchNode[0].CostItemId='Gold';
const restored=e.Studio.shape(legacy);assert.equal(restored.tables.UnitUpgrade[0].Costs[0].amount,789);assert.equal(restored.tables.ResearchNode[0].BaseCost,1234);assert.deepEqual(plain(E.config(restored)),plain(c));
const untouched=plain({chief:restored.tables.UnitUpgrade.filter(r=>r.UnitId==='AlphanChief'),other:restored.tables.ResearchNode.filter(n=>n.TreeKey!=='BASIC'),units:restored.tables.Unit,bonuses:restored.tables.BonusDefinition});
E.apply(restored);assert.deepEqual(plain({chief:restored.tables.UnitUpgrade.filter(r=>r.UnitId==='AlphanChief'),other:restored.tables.ResearchNode.filter(n=>n.TreeKey!=='BASIC'),units:restored.tables.Unit,bonuses:restored.tables.BonusDefinition}),untouched);
assert.deepEqual(errors(restored),[]);assert.deepEqual(plain(e.Studio.shape(restored).tables),plain(restored.tables));
for(const u of restored.tables.Unit.filter(u=>u.Kind==='Unit'))assert.deepEqual(plain(restored.tables.UnitUpgrade.filter(r=>r.UnitId===u.UnitId).map(r=>r.Costs[0].amount)),costs);
for(const [key,value]of [['UnitRound',0],['UnitSoftCap',14.5],['WeightMin',4],['HpExponent',null],['UnitExponent',10]]){const bad=plain(p);bad.tables.OutgameEconomy[0][key]=value;assert(errors(bad).length,key);}
const unsupported=plain(p);unsupported.tables.ResearchNode.find(n=>n.TreeKey==='BASIC').Stat='BattleGoldGain';assert.deepEqual(errors(unsupported),[]);assert.throws(()=>E.apply(unsupported));
excel.write(unsupported,['ShopCatalog']); // A proposal-specific pricing rule cannot block unrelated drafts.
const overflow=plain(p);overflow.tables.OutgameEconomy[0].ResearchRowRatio=10;assert.throws(()=>E.apply(overflow),/정수 범위/);
const variant=plain(p);variant.tables.ResearchNode.find(n=>n.TreeKey==='BASIC').MaxLevel=4;const before=JSON.stringify(variant.tables);assert.throws(()=>E.apply(variant),/21노드/);assert.equal(JSON.stringify(variant.tables),before);
const bundle=require('./app/outgame-economy-export.cjs');assert.equal(Object.keys(bundle.tables(p)).length,3);
const stale=plain(p);stale.tables.OutgameEconomy[0].UnitCoefficient*=1.1;assert.throws(()=>bundle.tables(stale),/다시 생성/);E.apply(stale);bundle.tables(stale);
const wrong=plain(p);wrong.tables.OutgameEconomy=[];assert(errors(wrong).length);wrong.tables.OutgameEconomy=[plain(c),plain(c)];assert(errors(wrong).length);
const folder=process.env.GGD_ECONOMY_OUTPUT||fs.mkdtempSync(path.join(os.tmpdir(),'ggd-economy-')),handlers={};fs.mkdirSync(folder,{recursive:true});
require('./app/content-service.cjs').register({workspace:folder,handler:(n,fn)=>handlers[n]=fn,atomic:(file,data)=>fs.writeFileSync(file,data),store:{exportFolder:()=>folder}});
const output=handlers['content-workbook-export']({project:p,table:'OutgameEconomy',bundle:'enhancement'});
assert.equal(output.chartFiles.length,3);assert.equal(output.functionFiles.length,2);
for(const file of output.chartFiles){const book=require('./app/workbook.cjs').open(fs.readFileSync(file));assert.equal(book.sheets.size,1);for(const [name,sheet]of book.sheets)for(const address of sheet.cells.keys())assert.equal(book.get(name,address).formula,null);}
assert.deepEqual(plain(excel.read(fs.readFileSync(output.file),p).project.tables.OutgameEconomy),plain(p.tables.OutgameEconomy));
const direct=handlers['content-workbook-export']({project:p,table:'ResearchNode',bundle:'enhancement'});assert.equal(path.basename(direct.file),'ResearchBalance.backend.xlsx');
const research=require('./app/readable-chart.cjs').table(p,'research');for(const field of ['CostItemId','BaseCost','CostStep'])assert(research[0].includes(field));
// The old authoring workbook omitted costs: import must preserve the current row's costs.
const fields=e.Studio.tables(p).ResearchNode[0],kept=fields.map((key,i)=>[key,i]).filter(([key])=>!['CostItemId','BaseCost','CostStep'].includes(key));
const oldRows=e.Studio.tables(p).ResearchNode.map(row=>kept.map(([,i])=>row[i]));
assert.equal(excel.read(Buffer.from(e.RewardCore.xlsx('ResearchNode',oldRows)),p).project.tables.ResearchNode[0].BaseCost,p.tables.ResearchNode[0].BaseCost);
fs.writeFileSync(path.join(folder,'parity.json'),JSON.stringify({config:c,nodes,unitCosts:costs,rewards:expected},null,2));
console.log(JSON.stringify({ok:true,checks:['19 cost rows / 168230 tokens','21 BASIC nodes / 99 ranks / 1684450 gold','HP partial/overkill/final flooring/bonuses','eight GDD reward examples','saved drafts / chief / other trees preserved','bad inputs / overflow / unsupported tree rejection','stale formula blocks export','three single-sheet formula-free charts / Function sources','legacy research import / new cost export'],folder,output},null,2));
