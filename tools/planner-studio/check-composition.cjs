'use strict';
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const excel = require('./app/content-workbook.cjs');
const scripts = path.join(__dirname, 'app/ui/content');
function load(features) {
  const context = vm.createContext({TextEncoder, TextDecoder, Uint8Array, Uint32Array, DataView, ArrayBuffer});
  for (const name of ['seed', 'schemas', 'core', 'studio-core', 'unit-base-seed', 'event-catalog', ...features, 'studio-compose']) {
    vm.runInContext(fs.readFileSync(path.join(scripts, name + '.js'), 'utf8'), context);
  }
  return context;
}
const normal = load(['current-scope', 'unit-keys', 'attendance-events', 'mission-events']);
const reversed = load(['mission-events', 'attendance-events', 'unit-keys', 'current-scope']);
const plain = value => JSON.parse(JSON.stringify(value));
const project = normal.Studio.create();
assert.deepEqual(plain(reversed.Studio.create()), plain(project));
assert.deepEqual(plain(reversed.Studio.tables(project)), plain(normal.Studio.tables(project)));
assert(Object.isFrozen(normal.Studio) && Object.isFrozen(normal.StudioCore));
assert.deepEqual(plain(normal.Studio.shape(project)), plain(project));
const saved = normal.Studio.clone(project);
saved.tables.UnitMilestone.find(row => row.UnitId === 'UnitData_Betan').Description = '기획자가 저장한 변경';
assert.equal(normal.Studio.shape(saved).tables.UnitMilestone.find(row => row.UnitId === 'UnitData_Betan').Description, '기획자가 저장한 변경');
const authored = plain(normal.UNIT_AUTHORED_BONUSES);
assert.equal(authored.length, 5);
assert.deepEqual(project.tables.UnitMilestone.filter(row => row.UnitId === 'UnitData_Betan').map(row => row.Description),
  normal.UNIT_AUTHORED_BONUSES.map(row => row.Description));
const workbook = excel.read(fs.readFileSync(path.join(__dirname, 'app/seeds/UnitBalance.xlsx')), project).project;
assert.deepEqual(plain(workbook.tables.UnitBaseStat), plain(project.tables.UnitBaseStat));
const current=excel.engine().Studio.create();
assert.deepEqual(workbook.tables.UnitMilestone.filter(row => row.UnitId === 'UnitData_Betan').map(row => row.Description), Array.from(current.tables.UnitMilestone.filter(row => row.UnitId === 'UnitData_Betan'),row=>row.Description));
console.log('PASS: explicit composition ignores feature file order; frozen API; saved edits preserved; authored unit/bonus workbook defaults match');

{
'use strict';
const assert = require('node:assert/strict');
const fs = require('node:fs');
const excel = require('./app/content-workbook.cjs');
const workbook = require('./app/workbook.cjs');
const e = excel.engine();
const project = e.Studio.create();
const plain = value => JSON.parse(JSON.stringify(value));

// Existing, independently edited drafts must survive JSON recovery without
// replacing the separate game-backed AccountProgression workbook.
project.tables.AccountLevel[0].RequiredExperience = 12345;
project.tables.BattleReward[0].WinBossExperience = 678;
const restored = e.Studio.shape(JSON.parse(JSON.stringify(project)));
for (const name of ['AccountLevel', 'BattleReward']) {
  assert.deepEqual(plain(restored.tables[name]), plain(project.tables[name]));
  assert(!e.PlannerScope.active.has(name));
  assert(!(name in e.Studio.tables(restored)));
  assert.throws(() => excel.write(restored, [name]), /지원하지 않는 표/);
}
assert(!e.STUDIO_NAV.some(n => n.id === 'account'));
assert(e.PlannerScope.overview(restored).includes('밸런스 편집 → 계정 성장'));

// Older mixed AccountSettings files still import Economy, without overwriting
// archived account rows or bringing the duplicate sheets back into exports.
const rows = e.Studio.tables(restored).Economy;
const oldTables = {Economy: rows};
for (const name of ['AccountLevel', 'BattleReward']) {
  const fields = e.STUDIO_SCHEMAS[name].fields.filter(f => !f.hidden && f.export !== false);
  oldTables[name] = [fields.map(f => f.key), ...project.tables[name].map(r => fields.map(f => r[f.key]))];
}
const zip = require('./app/node_modules/fflate');
const current = zip.unzipSync(excel.write(restored, ['Economy']));
for (const [index, name] of ['AccountLevel', 'BattleReward'].entries()) {
  current[`xl/worksheets/sheet${index + 2}.xml`] = zip.unzipSync(e.RewardCore.xlsx(name, oldTables[name]))['xl/worksheets/sheet1.xml'];
}
const xml = new TextDecoder().decode(current['xl/workbook.xml']).replace('</sheets>', '<sheet name="AccountLevel" sheetId="2" r:id="rId2"/><sheet name="BattleReward" sheetId="3" r:id="rId3"/></sheets>');
current['xl/workbook.xml'] = zip.strToU8(xml);
const rels = new TextDecoder().decode(current['xl/_rels/workbook.xml.rels']).replace('</Relationships>', [2,3].map(i => `<Relationship Id="rId${i}" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet${i}.xml"/>`).join('') + '</Relationships>');
current['xl/_rels/workbook.xml.rels'] = zip.strToU8(rels);
const imported = excel.read(Buffer.from(zip.zipSync(current)), restored);
assert.deepEqual(plain(imported.names), ['Economy']);
for (const name of ['AccountLevel', 'BattleReward']) assert.deepEqual(plain(imported.project.tables[name]), plain(restored.tables[name]));
assert.deepEqual(plain(excel.inspect(excel.write(restored, ['Economy'])).names), ['Economy']);
const account = workbook.read(fs.readFileSync(require('node:path').join(__dirname, 'app/seeds/AccountProgression.xlsx')), 'account');
assert(Number.isFinite(account.values['account.FirstRequiredExperience']));
console.log('PASS: one editable account source; legacy JSON preserved; old mixed workbook imports Economy only; account game workbook remains readable');
}
