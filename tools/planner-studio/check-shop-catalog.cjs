'use strict';
// Shop editor contract (2026-11-17): the planner reads, edits and exports the same columns and rules as the game
// (Assets/WorkSpace/USW/Shop/ShopCatalogTable.Parse) and the authored chart sources Data/Balance/ShopDailyList.xlsx
// and Data/Balance/ShopCatalog.xlsx. The source workbooks are only read. Evidence goes to
// outputs/planner-shop-columns-20261117/ (never the seed workbook or Assets fixtures).
const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const excel=require('./app/content-workbook.cjs'),e=excel.engine(),C=e.Studio,T=e.ShopCatalogTools;
const {open}=require('./app/workbook.cjs');
const root=path.resolve(__dirname,'../..'),plain=x=>JSON.parse(JSON.stringify(x));
const output=path.join(root,'outputs/planner-shop-columns-20261117');fs.mkdirSync(output,{recursive:true});
const passed=[];const ok=label=>passed.push(label);

// Read a game workbook as header + string rows ('' for empty cells), like BACKND sees it.
function sheetRows(bytes,name){
  const book=open(bytes),sheet=book.sheets.get(name);assert(sheet,name+' sheet');
  const cells=[...sheet.cells.keys()],cols=[...new Set(cells.map(a=>a.replace(/[0-9]+$/,'')))].sort((a,b)=>a.length-b.length||a.localeCompare(b));
  const rows=[...new Set(cells.map(a=>Number(a.match(/\d+$/)[0])))].sort((a,b)=>a-b);
  return rows.map(r=>cols.map(c=>String(book.get(name,c+r).value??'')));
}
const gameDaily=fs.readFileSync(path.join(root,'Data/Balance/ShopDailyList.xlsx'));
const gameCatalog=fs.readFileSync(path.join(root,'Data/Balance/ShopCatalog.xlsx'));
const gameDailyRows=sheetRows(gameDaily,'ShopDailyList'),gameCatalogRows=sheetRows(gameCatalog,'ShopCatalog');

// 1. Default project = current chart source, valid under the game rules.
const p=C.create(),rows=p.tables.ShopCatalog;
assert.equal(rows.length,15);
assert.equal(C.validate(p).filter(i=>i.severity==='error').length,0,JSON.stringify(C.validate(p).slice(0,3)));
assert.deepEqual(plain(gameDailyRows[0]),plain(T.DAILY_HEADER));assert.deepEqual(plain(gameCatalogRows[0]),plain(T.CATALOG_HEADER));
ok('seed: 15 rows, no errors, headers equal the game workbooks');

// 2. Export = the two game workbooks, cell for cell (same columns, same order, same values).
const asText=table=>plain(table.map(r=>r.map(v=>String(v??''))));
const lists=T.listTables(p);
assert.deepEqual(Object.keys(lists).sort(),['ShopCatalog','ShopDailyList']);
assert.deepEqual(asText(lists.ShopDailyList),gameDailyRows);
assert.deepEqual(asText(lists.ShopCatalog),gameCatalogRows);
for(const [name,table]of Object.entries(lists)){
  const bytes=Buffer.from(e.RewardCore.xlsx(name,table));assert.equal(open(bytes).sheets.size,1);
  assert.deepEqual(sheetRows(bytes,name),asText(table));
  fs.writeFileSync(path.join(output,name+'.export.xlsx'),bytes);
}
ok('export: ShopDailyList and ShopCatalog equal Data/Balance cell for cell; single sheet each; no probability files');

// 3. Planner workbook roundtrip keeps every new column, including edits.
const edited=plain(p);const pack=edited.tables.ShopCatalog.find(r=>r.ShopID===5001),daily=edited.tables.ShopCatalog.find(r=>r.ShopID===1002);
Object.assign(pack,{Limit:2,Tag:'계정당 2회',LimitPeriod:'Weekly',ExtraRewards:'Essence:61;Gold:20001',StoreID:'package_first_adventure_v2',Price:2200});
Object.assign(daily,{Limit:3,Amount:1234});
assert.equal(C.validate(edited).filter(i=>i.severity==='error').length,0);
const book=excel.write(edited,['ShopCatalog']);fs.writeFileSync(path.join(output,'planner-roundtrip.xlsx'),book);
const round=excel.read(book,p).project.tables.ShopCatalog;
const keys=T.CATALOG_HEADER;
assert.deepEqual(plain(round.map(r=>keys.map(k=>r[k]))),plain(edited.tables.ShopCatalog.map(r=>keys.map(k=>r[k]))));
ok('roundtrip: Limit, Tag, LimitPeriod, ExtraRewards, StoreID and edited values survive write/read');

// 4. Import of the real game workbooks (read only): each keeps the other half of the shop.
const base=plain(p);base.tables.ShopCatalog.forEach(r=>r.Amount+=1); // make the open project differ
const afterDaily=excel.read(gameDaily,base);
assert.deepEqual(plain(afterDaily.names),['ShopCatalog']);
const dailyImported=afterDaily.project.tables.ShopCatalog;
assert.deepEqual(plain(dailyImported.filter(r=>r.Section==='Daily').map(r=>r.Amount)),[10,1000,150]);
assert(dailyImported.filter(r=>r.Section!=='Daily').every(r=>r.Amount===rows.find(x=>x.ShopID===r.ShopID).Amount+1));
const afterCatalog=excel.read(gameCatalog,afterDaily.project).project.tables.ShopCatalog;
assert.deepEqual(asText(T.listTables({tables:{ShopCatalog:afterCatalog}}).ShopCatalog),gameCatalogRows);
assert.deepEqual(asText(T.listTables({tables:{ShopCatalog:afterCatalog}}).ShopDailyList),gameDailyRows);
ok('import: ShopDailyList.xlsx replaces only daily rows, ShopCatalog.xlsx only the others; both together reproduce the charts');

// 5. Old lottery layout: saved projects are replaced by the chart source, old Excel is refused with a reason.
const old=plain(p);old.tables.ShopCatalog=[{ShopID:1001,Name:'일일 무료 골드',Section:'Daily',FixedFree:1,Weight:0,SortOrder:0,RewardCurrency:'Gold',Amount:100,CurrencyType:'Free',Price:0,StoreID:'',Enabled:1,Source:'old'}];
const migrated=C.shape(old);assert.equal(migrated.tables.ShopCatalog.length,15);assert.equal(migrated.tables.ShopCatalog[0].Limit,1);assert(!Object.hasOwn(migrated.tables.ShopCatalog[0],'FixedFree'));
const legacyBook=Buffer.from(e.RewardCore.xlsx('ShopCatalog',[['ShopID','Name','Section','FixedFree','Weight','SortOrder','RewardCurrency','Amount','CurrencyType','Price','StoreID','Enabled'],[1001,'a','Daily',1,0,0,'Gold',100,'Free',0,'',1]]));
assert.throws(()=>excel.read(legacyBook,p),/옛 추첨형/);
ok('legacy: FixedFree/Weight projects migrate to the chart source; old Excel refused');

// 6. Every game parser rule is enforced before export.
const reject=(label,change)=>{const q=plain(p),r=q.tables.ShopCatalog,by=id=>r.find(x=>x.ShopID===id);change(r,by);
  assert(C.validate(q).some(i=>i.table==='ShopCatalog'&&i.severity==='error'),label);assert.throws(()=>excel.write(q,['ShopCatalog']),label);};
reject('duplicate ShopID',(r,by)=>by(1002).ShopID=1001);
reject('amount 0',(r,by)=>by(1001).Amount=0);
reject('unknown reward',(r,by)=>by(1001).RewardCurrency='Cash');
reject('daily free with price',(r,by)=>by(1001).Price=5);
reject('daily paid without price',(r,by)=>Object.assign(by(1001),{CurrencyType:'Diamond',Price:0}));
reject('daily cash',(r,by)=>Object.assign(by(1001),{CurrencyType:'Cash'}));
reject('daily limit 0',(r,by)=>by(1002).Limit=0);
reject('daily store id',(r,by)=>by(1003).StoreID='x');
reject('gold reward mismatch',(r,by)=>by(3001).RewardCurrency='Essence');
reject('essence paid by gold',(r,by)=>by(6001).CurrencyType='Gold');
reject('exchange with limit',(r,by)=>by(3002).Limit=1);
reject('inapp without store',(r,by)=>by(4001).StoreID='');
reject('inapp with price',(r,by)=>by(4002).Price=1100);
reject('duplicate store id',(r,by)=>by(4002).StoreID=by(4001).StoreID);
reject('package without tag',(r,by)=>by(5001).Tag='');
reject('package without period',(r,by)=>by(5001).LimitPeriod='');
reject('package period unknown',(r,by)=>by(5001).LimitPeriod='Daily');
reject('package limit 0',(r,by)=>by(5002).Limit=0);
reject('extras on non-package',(r,by)=>by(3001).ExtraRewards='Gold:10');
reject('period on non-package',(r,by)=>by(3001).LimitPeriod='Account');
reject('extras bad format',(r,by)=>by(5001).ExtraRewards='Essence=60');
reject('extras bad currency',(r,by)=>by(5001).ExtraRewards='Cash:60');
reject('extras zero',(r,by)=>by(5001).ExtraRewards='Gold:0');
reject('more than three rewards',(r,by)=>by(5001).ExtraRewards='Essence:1;Gold:2;Diamond:3');
reject('weekly section',(r,by)=>by(1001).Section='Weekly');
reject('daily over capacity',(r)=>r.push({...r[0],ShopID:1004,SortOrder:3}));
reject('package over capacity',(r,by)=>r.push({...by(5001),ShopID:5003,StoreID:'package_three'}));
reject('inapp over capacity',(r,by)=>by(4004).Enabled=1);
ok('rules: 28 rejection cases mirror ShopCatalogTable.Parse');

// 7. New records per section start valid enough to edit; ids continue after the highest.
const next=C.newRecord('ShopCatalog',p);assert.equal(next.ShopID,6004);assert.equal(next.Enabled,0);assert.equal(next.Limit,1);
ok('new record: next id 6004, disabled, daily defaults');

const report={result:'PASS',checks:passed,sources:['Data/Balance/ShopDailyList.xlsx','Data/Balance/ShopCatalog.xlsx'],written:fs.readdirSync(output).filter(n=>n.endsWith('.xlsx'))};
fs.writeFileSync(path.join(output,'check-shop-catalog.json'),JSON.stringify(report,null,2));
console.log('PASS '+passed.length+' shop catalog groups: '+passed.join(' | '));
