import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';
import { writeChartXlsx, readChartXlsx } from './xlsx.mjs';

const require = createRequire(import.meta.url);
const workbook = require('../planner-studio/app/content-workbook.cjs');
export const projectRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
export const tableKeys = {
  Units: ['UnitKey'], BaseStats: ['BaseStatKey', 'Tier'],
  UpgradeStats: ['UpgradeKey', 'TargetLevel'], LevelBonuses: ['LevelBonusKey', 'Level'],
};
const unitTables = ['Item', 'Unit', 'UnitBaseStat', 'UnitUpgrade', 'UnitMilestone'];
const hash = value => crypto.createHash('sha256').update(value).digest('hex');
const keyOf = (row, keys) => JSON.stringify(keys.map(key => row[key]));

export function localFile(relative, root = projectRoot) {
  const base = fs.realpathSync(root);
  const target = fs.realpathSync(path.resolve(base, relative));
  const rel = path.relative(base, target);
  if (rel === '..' || rel.startsWith('..' + path.sep) || path.isAbsolute(rel)) throw Error('프로젝트 밖 파일은 지원하지 않습니다.');
  if (!fs.statSync(target).isFile() || fs.statSync(target).size > 20 * 1024 * 1024) throw Error('20MB 이하 파일이 필요합니다.');
  return target;
}

export function readJson(file, root = projectRoot) {
  return JSON.parse(fs.readFileSync(localFile(file, root), 'utf8').replace(/^\uFEFF/, ''));
}

// Validate the raw input first: never fill missing unit balance with bundled sample values.
export function validateProject(project) {
  const engine = workbook.engine();
  if (project?.schemaVersion !== 2 || typeof project.name !== 'string' || !project.tables) throw Error('기획 작업실 outgame-project.json(v2)이 필요합니다.');
  for (const name of unitTables) {
    if (!Array.isArray(project.tables[name])) throw Error(`필수 표 누락: ${name}. 예시 데이터로 대체하지 않습니다.`);
    if (project.tables[name].length > 20000) throw Error(`${name}: 최대 20,000행입니다.`);
  }
  if (!project.tables.Unit.length) throw Error('유닛이 없는 프로젝트는 내보낼 수 없습니다.');
  for (const name of unitTables) for (const [index, row] of project.tables[name].entries()) {
    if (!row || typeof row !== 'object' || Array.isArray(row)) throw Error(`${name} ${index + 1}: 행 객체가 필요합니다.`);
    for (const field of engine.STUDIO_SCHEMAS[name].fields) {
      if (field.hidden || field.export === false) continue;
      const value = row[field.key];
      if (['int', 'float'].includes(field.type)) {
        if (typeof value !== 'number' || !Number.isFinite(value)) throw Error(`${name}.${field.key}: 숫자가 필요합니다.`);
      } else if (['costs', 'rewards', 'refs', 'parents', 'strings'].includes(field.type)) {
        if (!Array.isArray(value)) throw Error(`${name}.${field.key}: 배열이 필요합니다.`);
      } else if (field.type === 'bool') {
        if (typeof value !== 'boolean') throw Error(`${name}.${field.key}: boolean이 필요합니다.`);
      } else if (typeof value !== 'string') throw Error(`${name}.${field.key}: 문자열이 필요합니다.`);
    }
  }
  // Supply only empty unrelated tables, not seed records; validate references using this source's Item table.
  const candidate = { ...project, tables: Object.fromEntries(Object.keys(engine.STUDIO_SCHEMAS).map(name => [name, unitTables.includes(name) ? project.tables[name] : []])) };
  const issues = engine.Studio.validate(candidate).filter(issue => unitTables.includes(issue.table));
  const errors = issues.filter(issue => issue.severity === 'error');
  if (errors.length) return { valid: false, issues, tables: null };
  const tables = engine.UnitKeys.tables(candidate);
  // BACKND chart column names and values must satisfy the documented upload restrictions.
  for (const [name, rows] of Object.entries(tables)) {
    for (const header of rows[0]) if (!/^[A-Za-z0-9]+$/.test(header) || header.toLowerCase() === 'id') throw Error(`${name}: 뒤끝에서 사용할 수 없는 열 ${header}`);
    for (const row of rows.slice(1)) for (const value of row) {
      if (String(value).includes('\\')) throw Error(`${name}: 뒤끝 차트에는 역슬래시를 사용할 수 없습니다.`);
    }
  }
  return { valid: true, issues, tables };
}

export function checkedTables(file, root = projectRoot) {
  const result = validateProject(readJson(file, root));
  if (!result.valid) throw Error(result.issues.slice(0,12).map(i => `${i.table}[${i.index}].${i.field}: ${i.message}`).join('\n'));
  return result;
}

export function csv(rows) {
  return '\uFEFF' + rows.map(row => row.map(value => {
    const text = String(value ?? '');
    return /[",\r\n]/.test(text) ? '"' + text.replaceAll('"', '""') + '"' : text;
  }).join(',')).join('\r\n') + '\r\n';
}

// Strict RFC4180 reader for the generated chart files and console CSV downloads.
export function parseCsv(text) {
  text = text.replace(/^\uFEFF/, '');
  const rows = []; let row = [], cell = '', quoted = false, closed = false;
  for (let i = 0; i < text.length; i++) {
    const ch = text[i];
    if (quoted) {
      if (ch === '"' && text[i + 1] === '"') { cell += '"'; i++; }
      else if (ch === '"') { quoted = false; closed = true; }
      else cell += ch;
    } else if (ch === ',' || ch === '\r' || ch === '\n') {
      row.push(cell); cell = ''; closed = false;
      if (ch !== ',') { rows.push(row); row = []; if (ch === '\r' && text[i + 1] === '\n') i++; }
    } else if (ch === '"' && cell === '' && !closed) quoted = true;
    else { if (closed || ch === '"') throw Error('잘못된 CSV 따옴표입니다.'); cell += ch; }
  }
  if (quoted) throw Error('닫히지 않은 CSV 따옴표입니다.');
  if (cell || row.length || closed) { row.push(cell); rows.push(row); }
  if (!rows.length || new Set(rows[0]).size !== rows[0].length || rows[0].some(h => !h)) throw Error('CSV 열 이름이 없거나 중복됩니다.');
  if (rows.some(row => row.length !== rows[0].length)) throw Error('CSV 행의 열 수가 다릅니다.');
  return rows;
}

export function records(rows) {
  return rows.slice(1).map(row => Object.fromEntries(rows[0].map((key,i) => [key, String(row[i] ?? '')])));
}

export function compareRows(table, expected, actual) {
  const keys = tableKeys[table];
  if (!keys) throw Error('지원하지 않는 유닛 차트입니다.');
  const columns = expected[0];
  const wanted = records(expected);
  const mapRows = rows => {
    const map = new Map();
    for (const row of rows) {
      for (const column of columns) if (!Object.hasOwn(row, column)) throw Error(`비교 데이터의 열 누락: ${column}`);
      const normalized = Object.fromEntries(columns.map(column => [column, String(row[column] ?? '')]));
      const key = keyOf(normalized, keys);
      if (map.has(key)) throw Error(`비교 데이터의 중복 키: ${key}`);
      map.set(key, normalized);
    }
    return map;
  };
  const next = mapRows(wanted), previous = mapRows(actual);
  const added = [], removed = [], changed = [];
  for (const [key, after] of next) {
    const before = previous.get(key);
    if (!before) added.push(after);
    else {
      const fields = columns.filter(column => before[column] !== after[column]).map(column => ({ column, before: before[column], after: after[column] }));
      if (fields.length) changed.push({ key: JSON.parse(key), fields });
    }
  }
  for (const [key, row] of previous) if (!next.has(key)) removed.push(row);
  return { table, equal: !(added.length || removed.length || changed.length), counts: { added: added.length, removed: removed.length, changed: changed.length }, added: added.slice(0,50), removed: removed.slice(0,50), changed: changed.slice(0,50), detailLimit: 50 };
}

export async function exportCharts(file, releaseKey, root = projectRoot, format = 'xlsx', previews = false) {
  if (!['xlsx','csv'].includes(format)) throw Error('출력 형식은 xlsx 또는 csv입니다.');
  if (!/^[A-Za-z0-9][A-Za-z0-9_-]{0,63}$/.test(releaseKey)) throw Error('ReleaseKey는 영문/숫자/밑줄/하이픈 1~64자입니다.');
  const source = localFile(file, root);
  const { tables, issues } = checkedTables(file, root);
  const realRoot = fs.realpathSync(root);
  // Check each existing parent before creating its child (including Windows junctions).
  let base = realRoot;
  for (const part of ['outputs', 'backnd-mcp']) {
    base = path.join(base, part);
    if (!fs.existsSync(base)) fs.mkdirSync(base);
    const rel = path.relative(realRoot, fs.realpathSync(base));
    if (rel === '..' || rel.startsWith('..' + path.sep) || path.isAbsolute(rel)) throw Error('출력 폴더가 프로젝트 밖으로 연결되어 있습니다.');
  }
  const directory = path.join(base, releaseKey);
  fs.mkdirSync(directory); // Existing release folders are never overwritten.
  const charts = [];
  for (const [name, rows] of Object.entries(tables)) {
    const filename = name + '.' + format, target = path.join(directory, filename);
    if (format === 'xlsx') await writeChartXlsx(name, rows, target, previews ? path.join(directory, name + '.preview.png') : undefined);
    else fs.writeFileSync(target, csv(rows), { encoding: 'utf8', flag: 'wx' });
    const content = fs.readFileSync(target);
    const roundTrip = format === 'xlsx' ? readChartXlsx(content, name) : parseCsv(content.toString('utf8'));
    if (!compareRows(name, rows, records(roundTrip)).equal) throw Error(`${name}: ${format} 왕복 검증 실패`);
    if (format === 'xlsx' && rows.some((row, r) => row.some((value,c) => typeof value !== typeof roundTrip[r][c]))) throw Error(`${name}: Excel 셀 타입 보존 실패`);
    charts.push({ chartName: name, file: filename, rows: rows.length - 1, columns: rows[0], sha256: hash(content) });
  }
  const manifest = { schemaVersion: 2, format, releaseKey, createdAt: new Date().toISOString(), source: path.relative(realRoot, source), sourceSha256: hash(fs.readFileSync(source)), charts, remoteApplied: false, runtimeConnected: false };
  fs.writeFileSync(path.join(directory, 'manifest.json'), JSON.stringify(manifest,null,2) + '\n', { flag: 'wx' });
  return { directory, ...manifest, issues, note: '콘솔 업로드용 파일만 생성했습니다. 차트 생성/적용 및 게임 런타임 연결은 수행하지 않았습니다.' };
}

export function verifyBundle(directory, root = projectRoot) {
  const manifest = readJson(path.join(directory, 'manifest.json'), root);
  if (![1,2].includes(manifest.schemaVersion) || !Array.isArray(manifest.charts) || manifest.charts.length !== 4) throw Error('지원하지 않는 배포 manifest입니다.');
  const format = manifest.schemaVersion === 1 ? 'csv' : manifest.format;
  if (!['xlsx','csv'].includes(format)) throw Error('지원하지 않는 배포 파일 형식입니다.');
  const names = manifest.charts.map(c => c.chartName);
  if (new Set(names).size !== 4 || names.some(n => !tableKeys[n])) throw Error('배포 차트 목록이 다릅니다.');
  for (const chart of manifest.charts) {
    if (chart.file !== chart.chartName + '.' + format) throw Error('manifest 파일명이 올바르지 않습니다.');
    const content = fs.readFileSync(localFile(path.join(directory, chart.file), root));
    if (hash(content) !== chart.sha256) throw Error(`${chart.chartName}: 파일 해시 불일치`);
    const rows = format === 'xlsx' ? readChartXlsx(content, chart.chartName) : parseCsv(content.toString('utf8'));
    if (rows.length - 1 !== chart.rows || JSON.stringify(rows[0]) !== JSON.stringify(chart.columns)) throw Error(`${chart.chartName}: 행/열 불일치`);
    compareRows(chart.chartName, rows, records(rows));
  }
  return { valid: true, releaseKey: manifest.releaseKey, charts: manifest.charts.map(c => ({ name: c.chartName, rows: c.rows })), remoteApplied: false };
}
