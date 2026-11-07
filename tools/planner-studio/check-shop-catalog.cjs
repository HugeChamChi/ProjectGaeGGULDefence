'use strict';
const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const excel=require('./app/content-workbook.cjs'),e=excel.engine(),C=e.Studio;
const plain=x=>JSON.parse(JSON.stringify(x));
const p=C.create(),rows=p.tables.ShopCatalog;
assert.equal(rows.length,25);assert(rows.every(r=>r.Enabled===1));
assert.equal(C.validate(p).filter(i=>i.severity==='error').length,0);
const workbook=excel.write(p,['ShopCatalog']);
assert.deepEqual(plain(excel.read(workbook,p).project.tables.ShopCatalog),plain(rows));
const legacy=plain(p);delete legacy.tables.ShopCatalog;
assert.equal(C.shape(legacy).tables.ShopCatalog.length,25);
const old=plain(p);for(const r of old.tables.ShopCatalog)delete r.Weight;
const migrated=C.shape(old);assert.equal(migrated.tables.ShopCatalog[0].Weight,0);assert.equal(migrated.tables.ShopCatalog[1].Weight,1);
const next=C.newRecord('ShopCatalog',p);assert.equal(next.ShopID,4005);assert.equal(next.Enabled,0);
const reject=change=>{const q=plain(p);change(q.tables.ShopCatalog);assert(C.validate(q).some(i=>i.table==='ShopCatalog'&&i.severity==='error'));assert.throws(()=>excel.write(q,['ShopCatalog']));};
reject(r=>r[1].ShopID=r[0].ShopID);reject(r=>r[0].Amount=0);reject(r=>r[0].RewardCurrency='Unknown');
reject(r=>r[0].Price=1);reject(r=>r[1].Weight=0);reject(r=>r[0].Weight=5);
reject(r=>Object.assign(r[1],{FixedFree:1,Weight:0,CurrencyType:'Free',Price:0}));
reject(r=>r[18].FixedFree=1);reject(r=>r[21].StoreID='');reject(r=>r[22].StoreID=r[21].StoreID);
const edited=plain(p);Object.assign(edited.tables.ShopCatalog[0],{RewardCurrency:'Diamond',Amount:777,SortOrder:999});
edited.tables.ShopCatalog[1].Weight=333;
const round=excel.read(excel.write(edited,['ShopCatalog']),p).project;
assert.equal(round.tables.ShopCatalog[0].Amount,777);assert.equal(round.tables.ShopCatalog[1].Weight,333);
const {open}=require('./app/workbook.cjs');
function checkTables(project){
 const tables=e.ShopCatalogTools.probabilityTables(project);
 for(const [name,table]of Object.entries(tables)){
   assert.deepEqual(plain(table[0]),['ShopID','percent']);
   assert(table[0].every(h=>/^[A-Za-z0-9]+$/.test(h)&&h!=='id'));
   assert.equal(table.length,9);assert(!table.slice(1).some(r=>r[0]===1001||r[0]===2001));
   assert.equal(table.slice(1).reduce((sum,r)=>sum+Math.round(r[1]*10000000),0),1000000000);
   assert(table.slice(1).every(r=>r[1]>0&&Math.abs(r[1]*10000000-Math.round(r[1]*10000000))<.000001));
   const bytes=Buffer.from(e.RewardCore.xlsx(name,table)),book=open(bytes);assert.equal(book.sheets.size,1);
   assert.equal(book.get(name,'B1').value,'percent');
 }
 return tables;
}
checkTables(edited);const tables=checkTables(p);
for(const [name,table]of Object.entries(e.ShopCatalogTools.listTables(p))){
 const bytes=Buffer.from(e.RewardCore.xlsx(name,table)),book=open(bytes);assert.equal(book.sheets.size,1);
 const sections=table.slice(1).map(r=>r[2]);
 assert(sections.every(s=>name==='ShopDailyList'?s==='Daily':name==='ShopWeeklyList'?s==='Weekly':['Gold','InApp'].includes(s)));
 if(name!=='ShopCatalog'){
   const probabilities=tables[name==='ShopDailyList'?'ShopDailyProbability':'ShopWeeklyProbability'];
   assert(probabilities.slice(1).every(r=>table.slice(1).some(product=>product[0]===r[0]&&product[3]===0)));
 }
}
const output=path.resolve(__dirname,'../../outputs/shop-chart');fs.mkdirSync(output,{recursive:true});
fs.writeFileSync(path.join(output,'ShopCatalog.xlsx'),workbook);
for(const [name,table]of Object.entries(tables))fs.writeFileSync(path.join(output,name+'.xlsx'),Buffer.from(e.RewardCore.xlsx(name,table)));
const data=excel.toRuntime(p,['ShopCatalog']).shopProducts;
fs.writeFileSync(path.resolve(__dirname,'../../Assets/WorkSpace/USW/Shop/Data/ShopCatalogEditorFixture.json'),JSON.stringify(data,null,2));
fs.writeFileSync(path.join(__dirname,'app/seeds/ShopCatalog.xlsx'),workbook);
console.log('PASS: editable weights, free exclusion, Excel roundtrip, BACKND single sheet/headers/100 percent/7 decimals, legacy migration');
