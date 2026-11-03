'use strict';

const crypto = require('node:crypto');
const path = require('node:path');
const { unzipSync, zipSync, strFromU8, strToU8 } = require('fflate');
const { XMLParser } = require('fast-xml-parser');
const schema = require('./schema.cjs');
const parser = new XMLParser({ ignoreAttributes: false, removeNSPrefix: true, parseTagValue: false, parseAttributeValue: false,
  isArray: name => ['sheet', 'Relationship', 'row', 'c', 'si'].includes(name) });
const hash = bytes => crypto.createHash('sha256').update(bytes).digest('hex');
const content = value => value && typeof value === 'object' ? content(value['#text'] ?? value.t ?? '') : String(value ?? '');

function open(bytes) {
  if (bytes.length > 8 * 1024 * 1024) throw new Error('이번 버전은 8MB 이하의 XLSX를 지원합니다.');
  let total = 0;
  const parts = unzipSync(bytes, { filter: file => {
    total += file.originalSize;
    if (file.originalSize > 32 * 1024 * 1024 || total > 64 * 1024 * 1024) throw new Error('압축 해제한 Excel이 지원 크기를 초과합니다.');
    return true;
  } });
  const xml = name => {
    if (!parts[name]) throw new Error(`Excel 구성요소가 없습니다: ${name}`);
    const text = strFromU8(parts[name]);
    if (/<!DOCTYPE|<!ENTITY/i.test(text)) throw new Error('외부 엔티티가 포함된 Excel XML은 지원하지 않습니다.');
    return parser.parse(text);
  };
  const book = xml('xl/workbook.xml').workbook;
  const relationships = new Map(xml('xl/_rels/workbook.xml.rels').Relationships.Relationship.map(r => [r['@_Id'], r['@_Target']]));
  let strings = [];
  if (parts['xl/sharedStrings.xml']) {
    strings = (xml('xl/sharedStrings.xml').sst.si || []).map(si => {
      if (si.r) return (Array.isArray(si.r) ? si.r : [si.r]).map(r => content(r.t)).join('');
      return content(si.t);
    });
  }
  const sheets = new Map();
  for (const sheet of book.sheets.sheet) {
    const target = relationships.get(sheet['@_id']);
    if (!target || /^(?:https?:|file:)/i.test(target)) throw new Error('지원하지 않는 Excel 시트 참조입니다.');
    const part = path.posix.normalize(target.startsWith('/') ? target.slice(1) : 'xl/' + target);
    const rows = xml(part).worksheet.sheetData.row || [];
    const cells = new Map(rows.flatMap(row => row.c || []).map(cell => [cell['@_r'], cell]));
    sheets.set(sheet['@_name'], { part, cells });
  }
  const get = (sheet, address) => {
    const value = sheets.get(sheet)?.cells.get(address);
    if (!value) return { value: '', formula: null };
    const type = value['@_t'];
    const text = type === 's' ? strings[Number(content(value.v))] : type === 'inlineStr' ? content(value.is) : content(value.v);
    if (type === 'e') throw new Error(`${sheet}!${address}: Excel 오류 ${text}`);
    return { value: text, formula: value.f === undefined ? null : content(value.f), type };
  };
  return { parts, sheets, get };
}

function number(book, sheet, cell, formula = undefined) {
  const value = book.get(sheet, cell);
  if (value.formula !== null && value.formula !== formula) throw new Error(`${sheet}!${cell}: 지원하지 않는 수식입니다.`);
  if (value.formula === null && ['s', 'inlineStr', 'str', 'b'].includes(value.type)) throw new Error(`${sheet}!${cell}: 텍스트가 아닌 숫자 셀이 필요합니다.`);
  const result = schema.numeric(value.value);
  if (!Number.isFinite(result)) throw new Error(`${sheet}!${cell}: 숫자 입력이 필요합니다.`);
  return result;
}

function requiredText(book, sheet, cell, expected) {
  const value = book.get(sheet, cell);
  if (value.formula !== null || value.value !== expected) throw new Error(`${sheet}!${cell}: '${expected}'가 필요합니다.`);
}

function read(bytes, domain) {
  const book = open(bytes);
  const values = {};
  let context;
  if (domain === 'endless') {
    if (book.sheets.size !== 1 || !book.sheets.has('EndlessBalance')) throw new Error('EndlessBalance 단일 시트의 업로드용 Excel을 선택하세요.');
    const expected = ['SchemaVersion', 'Mode', 'StartSeconds', 'HpGrowthPercent', 'BreatherSeconds', 'HardSeconds'];
    expected.forEach((text, i) => requiredText(book, 'EndlessBalance', String.fromCharCode(65 + i) + '1', text));
    if (number(book, 'EndlessBalance', 'A2') !== 1) throw new Error('무한모드 SchemaVersion은 1이어야 합니다.');
    requiredText(book, 'EndlessBalance', 'B2', 'NORMAL');
    for (const [address, cell] of book.sheets.get('EndlessBalance').cells) {
      if (Number(address.replace(/[A-Z]/g, '')) > 2 && (content(cell.v) || cell.is || cell.f)) throw new Error('무한모드 표는 데이터 행이 정확히 하나여야 합니다.');
    }
    for (const field of schema.domains[0].fields) values['endless.' + field.id] = number(book, field.sheet, field.cell);
    context = { mode: 'NORMAL', schemaVersion: 1, firstGrowthRound: 11, growthInterval: 1 };
  } else if (domain === 'account') {
    for (const name of ['안내_계정설정', '계정레벨', '전투경험치', '획득량_예시']) if (!book.sheets.has(name)) throw new Error('계정 표에 필요한 탭이 없습니다: ' + name);
    const sheet = '안내_계정설정';
    const maxLevel = number(book, sheet, 'B5');
    const initialLevel = number(book, sheet, 'B6');
    const initialExperience = number(book, sheet, 'B7');
    if (!Number.isInteger(maxLevel) || maxLevel < 2 || maxLevel > 10000 || !Number.isInteger(initialLevel) || initialLevel < 1 || initialLevel > maxLevel || !Number.isInteger(initialExperience) || initialExperience < 0 || (initialLevel === maxLevel && initialExperience !== 0)) throw new Error('계정 상한·시작 상태를 확인하세요.');
    requiredText(book, sheet, 'B9', '이월'); requiredText(book, sheet, 'B10', '추가 경험치 미적립');
    for (const field of schema.domains[1].fields) values['account.' + field.id] = number(book, field.sheet, field.cell);
    let cumulative = 0;
    for (let level = 1; level <= maxLevel; level++) {
      const row = level + 4;
      if (number(book, '계정레벨', 'A' + row) !== level) throw new Error('계정 레벨은 1부터 상한까지 연속이어야 합니다.');
      const requirement = level === maxLevel ? null : values['account.FirstRequiredExperience'] + (level - 1) * values['account.RequirementStep'];
      const expected = `IF(A${row}>='${sheet}'!$B$5,"",'${sheet}'!$B$8+(A${row}-1)*'${sheet}'!$B$13)`;
      const actual = book.get('계정레벨', 'B' + row);
      if (actual.formula !== expected) throw new Error(`계정레벨!B${row}: 첫 버전은 연속 증가 수식으로 작성된 표만 편집합니다. 개별 입력값을 덮어쓰지 않습니다.`);
      const prev = row - 1;
      const cf = level === 1 ? `IF(A${row}="","",IF(A${row}=1,0,""))` : `IF(A${row}="","",IF(AND(ISNUMBER(A${prev}),A${row}=A${prev}+1,ISNUMBER(B${prev}),B${prev}>0,ISNUMBER(C${prev})),C${prev}+B${prev},""))`;
      if (book.get('계정레벨', 'C' + row).formula !== cf) throw new Error(`계정레벨!C${row}: 지원하지 않는 누적 수식입니다.`);
      cumulative += requirement || 0;
    }
    for (const [address] of book.sheets.get('계정레벨').cells) if (/^A\d+$/.test(address) && Number(address.slice(1)) > maxLevel + 4 && book.get('계정레벨', address).value !== '') throw new Error('상한 이후에 추가 계정 레벨이 있습니다.');
    const policies = [];
    ['NORMAL_WIN', 'NORMAL_LOSE', 'NORMAL_QUIT', 'TUTORIAL'].forEach((id, i) => {
      const row = i + 5;
      requiredText(book, '전투경험치', 'A' + row, id);
      requiredText(book, '전투경험치', 'B' + row, i === 3 ? 'TUTORIAL' : 'NORMAL');
      requiredText(book, '전투경험치', 'C' + row, ['승리', '패배', '중도 종료', '튜토리얼 종료'][i]);
      requiredText(book, '전투경험치', 'E' + row, '보스별 최대 HP 감소 비율');
      const policy = book.get('전투경험치', 'D' + row).value;
      if (!['지급', '미지급'].includes(policy) || (i >= 2 && policy !== '미지급')) throw new Error('지원하지 않는 종료 지급 정책입니다.');
      if (number(book, '전투경험치', 'F' + row) !== 0 || number(book, '전투경험치', 'H' + row) !== 1) throw new Error('지원하지 않는 HP 비율 규칙입니다.');
      const g = book.get('전투경험치', 'G' + row);
      if (g.formula !== `'${sheet}'!$B$14`) throw new Error('이번 버전은 보스 기준 경험치를 참조하는 전투 경험치 표만 지원합니다.');
      policies.push(policy);
    });
    for (const [address] of book.sheets.get('전투경험치').cells) if (/^[B-H]\d+$/.test(address) && Number(address.slice(1)) > 8 && (book.get('전투경험치', address).value !== '' || book.get('전투경험치', address).formula !== null)) throw new Error('추가 전투 경험치 규칙은 현재 편집기에서 지원하지 않습니다.');
    const examples = [];
    for (let row = 5; row <= 8; row++) {
      const kills = number(book, '획득량_예시', 'B' + row); const ratio = number(book, '획득량_예시', 'C' + row);
      if (!Number.isInteger(kills) || kills < 0) throw new Error('예시의 보스 수는 0 이상 정수여야 합니다.');
      if (book.get('획득량_예시', 'D' + row).formula !== `ROUNDDOWN((B${row}+MIN(1,MAX(0,C${row})))*'${sheet}'!$B$14,0)`) throw new Error('지원하지 않는 획득량 예시 수식입니다.');
      examples.push({ row, kills, ratio });
    }
    context = { maxLevel, initialLevel, initialExperience, policies, examples };
  } else throw new Error('지원하지 않는 데이터 종류입니다.');
  const combined = Object.fromEntries(schema.fields.map(f => [f.uid, f.domain === domain ? values[f.uid] : f.min || 1]));
  const check = schema.validate(combined, domain === 'account' ? { account: context } : {});
  const errors = check.errors.filter(e => e.uid.startsWith(domain + '.'));
  if (errors.length) throw new Error(errors.map(e => e.message).join('\n'));
  return { values, context, sha256: hash(bytes), sheets: [...book.sheets.keys()] };
}

function patchCell(xml, address, value) {
  const expression = new RegExp(`(<((?:[\\w]+:)?c)\\b(?=[^>]*\\br=["']${address}["'])[^>]*?)(?:\\s*/>|>([\\s\\S]*?)<\\/\\2>)`);
  if (!expression.test(xml)) throw new Error('저장할 셀을 찾을 수 없습니다: ' + address);
  return xml.replace(expression, (_, openTag, tag, inside = '') => {
    const prefix = tag.includes(':') ? tag.split(':')[0] + ':' : '';
    if (value !== null) openTag = openTag.replace(/\s+t=["'][^"']*["']/g, '');
    inside = inside.replace(/<(?:[\w]+:)?v\b[^>]*(?:\/>|>[\s\S]*?<\/(?:[\w]+:)?v>)/g, '');
    return `${openTag}>${inside}<${prefix}v>${value === null ? '' : value}</${prefix}v></${tag}>`;
  });
}

function write(bytes, domain, values) {
  const baseline = read(bytes, domain);
  const allValues = Object.fromEntries(schema.fields.map(f => [f.uid, f.domain === domain ? values[f.uid] : f.min || 1]));
  const check = schema.validate(allValues, domain === 'account' ? { account: baseline.context } : {});
  const errors = check.errors.filter(e => e.uid.startsWith(domain + '.'));
  if (errors.length) throw new Error(errors.map(e => e.message).join('\n'));
  const book = open(bytes); const changed = new Map();
  const set = (sheet, cell, value) => {
    const part = book.sheets.get(sheet).part;
    changed.set(part, patchCell(changed.get(part) ?? strFromU8(book.parts[part]), cell, value));
  };
  for (const field of schema.domains.find(d => d.id === domain).fields) set(field.sheet, field.cell, check.values[domain + '.' + field.id]);
  if (domain === 'account') {
    let cumulative = 0;
    const first = check.values['account.FirstRequiredExperience']; const step = check.values['account.RequirementStep']; const boss = check.values['account.BossExperience'];
    for (let level = 1; level <= baseline.context.maxLevel; level++) {
      const requirement = level === baseline.context.maxLevel ? null : first + (level - 1) * step;
      set('계정레벨', 'B' + (level + 4), requirement); set('계정레벨', 'C' + (level + 4), cumulative);
      cumulative += requirement || 0;
    }
    for (let row = 5; row <= 8; row++) set('전투경험치', 'G' + row, boss);
    for (const example of baseline.context.examples) set('획득량_예시', 'D' + example.row, Math.floor((example.kills + Math.min(1, Math.max(0, example.ratio))) * boss));
  }
  for (const [part, text] of changed) book.parts[part] = strToU8(text);
  const output = Buffer.from(zipSync(book.parts, { level: 6 }));
  const verified = read(output, domain);
  for (const field of schema.domains.find(d => d.id === domain).fields) if (verified.values[domain + '.' + field.id] !== check.values[domain + '.' + field.id]) throw new Error('내보낸 Excel 재검증에 실패했습니다.');
  return output;
}

module.exports = { open, read, write, patchCell, hash };
