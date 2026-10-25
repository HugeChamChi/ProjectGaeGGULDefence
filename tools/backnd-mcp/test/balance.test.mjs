import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { createRequire } from 'node:module';
import { validateProject, exportCharts, verifyBundle, compareRows, records, csv, parseCsv, localFile } from '../balance.mjs';
import { unwrapRead } from '../unity.mjs';
import { writeChartXlsx, readChartXlsx } from '../xlsx.mjs';

const workbook = createRequire(import.meta.url)('../../planner-studio/app/content-workbook.cjs');
const sample = () => JSON.parse(JSON.stringify(workbook.engine().Studio.create()));

test('existing planner data exports four charts without unit state', () => {
  const result = validateProject(sample());
  assert.equal(result.valid, true);
  assert.deepEqual(Object.keys(result.tables), ['Units', 'BaseStats', 'UpgradeStats', 'LevelBonuses']);
  assert.ok(result.tables.Units.length > 1);
});

test('missing balance is rejected rather than populated from seeds', () => {
  const project = sample(); delete project.tables.UnitBaseStat;
  assert.throws(() => validateProject(project), /필수 표 누락/);
  const another = sample(); delete another.tables.Unit[0].UpgradeKey;
  assert.throws(() => validateProject(another), /문자열/);
});

test('negative costs, duplicate units, and missing tier fail validation', () => {
  const cost = sample(); cost.tables.UnitUpgrade[0].Costs[0].amount = -1;
  assert.equal(validateProject(cost).valid, false);
  const duplicate = sample(); duplicate.tables.Unit.push(structuredClone(duplicate.tables.Unit[0]));
  assert.equal(validateProject(duplicate).valid, false);
  const tier = sample(); tier.tables.UnitBaseStat.shift();
  assert.equal(validateProject(tier).valid, false);
});

test('chart upload rejects backslashes', () => {
  const project = sample(); project.tables.Unit[0].Name = 'bad\\name';
  assert.throws(() => validateProject(project), /역슬래시/);
});

test('CSV preserves Korean, commas, quotes, newlines, empty cells and decimals', () => {
  const rows = [['Name','Value'],['베탕, "강화"\n설명','0.002'],['','']];
  assert.deepEqual(parseCsv(csv(rows)), rows);
  assert.throws(() => parseCsv('A,A\n1,2'), /중복/);
  assert.throws(() => parseCsv('A,B\n"open,2'), /따옴표/);
  assert.throws(() => parseCsv('A,B\n1'), /열 수/);
});

test('comparison ignores row order and SDK metadata, reports additions/removals/changes', () => {
  const rows = [['UnitKey','Name'],['betang','베탕'],['gamang','감망']];
  assert.equal(compareRows('Units',rows,[{UnitKey:'gamang',Name:'감망',id:'2'},{UnitKey:'betang',Name:'베탕',id:'1'}]).equal,true);
  const changed = compareRows('Units',rows,[{UnitKey:'betang',Name:'old'},{UnitKey:'removed',Name:'removed'}]);
  assert.deepEqual(changed.counts,{added:1,removed:1,changed:1});
  assert.throws(() => compareRows('Units',rows,[{UnitKey:'betang'}]),/열 누락/);
  assert.throws(() => compareRows('Units',rows,[{UnitKey:'betang',Name:'a'},{UnitKey:'betang',Name:'b'}]),/중복 키/);
});

test('XLSX bundle roundtrip, tamper detection, immutable releases and path boundary', async t => {
  const root = fs.mkdtempSync(path.join(os.tmpdir(),'ggd-backnd-test-'));
  t.after(() => fs.rmSync(root,{recursive:true,force:true}));
  fs.writeFileSync(path.join(root,'project.json'),JSON.stringify(sample()));
  const result = await exportCharts('project.json','test-release',root);
  assert.equal(result.format,'xlsx');
  assert.equal(result.remoteApplied,false);
  assert.equal(verifyBundle(result.directory,root).valid,true);
  await assert.rejects(exportCharts('project.json','test-release',root),/EEXIST/);
  await assert.rejects(exportCharts('project.json','../escape',root),/ReleaseKey/);
  assert.throws(() => localFile(import.meta.filename,root),/프로젝트 밖/);
  fs.appendFileSync(path.join(result.directory,'Units.xlsx'),'tampered');
  assert.throws(() => verifyBundle(result.directory,root),/해시 불일치/);
});

test('XLSX preserves strings, Korean descriptions, JSON, decimal numbers and empty cells', async t => {
  const root = fs.mkdtempSync(path.join(os.tmpdir(),'ggd-xlsx-test-'));
  t.after(() => fs.rmSync(root,{recursive:true,force:true}));
  const rows = [['UnitKey','Name','Amount','EffectParams'],['001','베탕, "강화"\n설명',0.002,'{"ratio":0.15}'],['002','=literal',0,'']];
  const file = path.join(root,'Units.xlsx');
  await writeChartXlsx('Units',rows,file);
  assert.deepEqual(readChartXlsx(fs.readFileSync(file),'Units'),rows);
});

test('CSV option and existing v1 manifests remain readable', async t => {
  const root = fs.mkdtempSync(path.join(os.tmpdir(),'ggd-csv-test-'));
  t.after(() => fs.rmSync(root,{recursive:true,force:true}));
  fs.writeFileSync(path.join(root,'project.json'),JSON.stringify(sample()));
  const result = await exportCharts('project.json','csv-release',root,'csv');
  assert.equal(result.format,'csv');
  const manifestFile = path.join(result.directory,'manifest.json');
  const manifest = JSON.parse(fs.readFileSync(manifestFile,'utf8'));
  manifest.schemaVersion=1; delete manifest.format;
  fs.writeFileSync(manifestFile,JSON.stringify(manifest));
  assert.equal(verifyBundle(result.directory,root).valid,true);
});

test('JSON response bridge fails closed on SDK or protocol failures', () => {
  assert.deepEqual(unwrapRead({success:true,result:{success:true,result:'{"rows":[]}'}}),{rows:[]});
  assert.throws(() => unwrapRead({success:false,result:'{}'}),/실패/);
  assert.throws(() => unwrapRead({success:true,result:{success:false,result:'{}'}}),/실패/);
  assert.throws(() => unwrapRead({success:true,result:{success:true,result:{rows:[]}}}),/응답/);
});
