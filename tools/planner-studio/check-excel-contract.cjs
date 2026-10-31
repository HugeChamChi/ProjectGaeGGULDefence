'use strict';
const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const crypto = require('node:crypto');
const {zipSync, unzipSync, strFromU8, strToU8} = require('./app/node_modules/fflate');
const excel = require('./app/content-workbook.cjs');
const {unitRuntime} = require('./app/unit-runtime.cjs');
const {decodeConfigRows} = require('./app/chart-payload.cjs');
const {open} = require('./app/workbook.cjs');
const e = excel.engine(), project = e.Studio.create();
const plain = value => JSON.parse(JSON.stringify(value));

// Every authored worksheet must match its advertised column order and types.
for (const [name, rows] of Object.entries(e.Studio.tables(project))) {
  if (name === 'ResearchLevel') continue; // Calculated preview, not an authored workbook sheet.
  const contract = excel.contract([name]).tables.find(t => t.sheet === name);
  assert.deepEqual(plain(contract.columns.map(c => c.key)), plain(rows[0]), name);
}
const bytes = excel.write(project, ['Unit']);
const restored = excel.read(bytes, project).project;
assert.deepEqual(unitRuntime(restored), unitRuntime(project));
const frequency = excel.contract(['Unit']).tables.find(t => t.sheet === 'BaseStats').columns.find(c => c.key === 'AttackFrequency');
assert.equal(frequency.unit, 'attacks/second');
assert.equal(excel.toRuntime(project, ['Unit']).baseStats[0].AttackFrequency, project.tables.UnitBaseStat[0].AttackInterval);

// Old workbooks retain their actual frequency; never reciprocate the value.
const old = unzipSync(bytes);
old['xl/worksheets/sheet2.xml'] = strToU8(strFromU8(old['xl/worksheets/sheet2.xml']).replace('AttackFrequency', 'AttackInterval'));
assert.deepEqual(unitRuntime(excel.read(zipSync(old), project).project), unitRuntime(project));
old['xl/worksheets/sheet2.xml'] = strToU8(strFromU8(old['xl/worksheets/sheet2.xml']).replace('</row>', '<c r="I1" t="inlineStr"><is><t>AttackFrequency</t></is></c></row>'));
assert.throws(() => excel.inspect(zipSync(old)), /BaseStats/);

// Identical item cells have identical meaning and rejection rules in every feature.
const itemCell = 'Gold:500;Diamond:10;Stamina:30';
for (const [sheet, key] of [['AttendanceRewards','Rewards'], ['Missions','Rewards'], ['UpgradeStats','Costs']]) {
  const rows = plain(e.Studio.tables(project)[sheet]);
  const index = rows[0].indexOf(key);
  rows[1][index] = itemCell;
  assert.deepEqual(plain(excel.inspect(e.RewardCore.xlsx(sheet, rows)).tables[sheet][0][key]), plain(e.RewardCore.parseItems(itemCell)));
  for (const invalid of ['Gold:0', 'Gold:1;Gold:2', 'Gold:2147483648', 'Gold:1.5', 'Gold:-1']) {
    rows[1][index] = invalid;
    assert.throws(() => excel.inspect(e.RewardCore.xlsx(sheet, rows)), undefined, sheet + ': ' + invalid);
  }
}

// A deployment's review workbook and transport must come from the same snapshot.
const folder = fs.mkdtempSync(path.join(os.tmpdir(), 'ggd-contract-'));
const handlers = new Map();
require('./app/backend-deployment.cjs').register({
  handler: (name, fn) => handlers.set(name, fn), workspace: folder,
  atomic: (file, value) => fs.writeFileSync(file, value), getCurrentProject: () => project,
  exportFile: (name, value) => {const file = path.join(folder, name);fs.writeFileSync(file, value);return {file};},
});
for (const kind of ['missions', 'unitBaseStats']) {
  const readable = handlers.get('content-backend-prepare')({kind, project});
  const readableBook=open(fs.readFileSync(readable.file)),readableSheet=[...readableBook.sheets.keys()][0];
  const expectedTable=require('./app/readable-chart.cjs').table(kind==='missions'?excel.toRuntime(project,excel.bundles.missions):unitRuntime(project),kind);
  const col=i=>{let s='';for(i++;i;i=Math.floor((i-1)/26))s=String.fromCharCode(65+(i-1)%26)+s;return s;};
  expectedTable.forEach((row,r)=>row.forEach((value,c)=>assert.equal(readableBook.get(readableSheet,col(c)+(r+1)).value,String(value),'readable chart cell')));
  const record = handlers.get('content-backend-prepare')({kind, project, base64:true});
  const authoringBytes = fs.readFileSync(record.authoringFile);
  assert.equal(crypto.createHash('sha256').update(authoringBytes).digest('hex'), record.authoringSha256);
  const review = excel.read(authoringBytes, project).project;
  const book = open(fs.readFileSync(record.file));
  const sheet = [...book.sheets.keys()][0];
  const keys = ['SchemaVersion','ReleaseKey','PartIndex','PartCount','Sha256','Payload'];
  const rows = [];
  for (let r = 2; book.get(sheet, 'A' + r).value; r++) rows.push(Object.fromEntries(keys.map((key, i) => [key, book.get(sheet, String.fromCharCode(65 + i) + r).value])));
  const decoded = decodeConfigRows(rows);
  const expected = kind === 'missions' ? excel.toRuntime(review, excel.bundles.missions) : unitRuntime(review);
  assert.deepEqual(JSON.parse(decoded.bytes.toString('utf8')), plain(expected));
  assert.equal(record.remoteApplied, false);
}
console.log('PASS: all worksheet contracts, frequency units and legacy import, shared item cells, deployment/review snapshot equivalence');
