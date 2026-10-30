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
assert.deepEqual(workbook.tables.UnitMilestone.filter(row => row.UnitId === 'UnitData_Betan').map(row => row.Description), authored.map(row => row.Description));
console.log('PASS: explicit composition ignores feature file order; frozen API; saved edits preserved; authored unit/bonus workbook defaults match');
