'use strict';
const fs = require('node:fs');
const path = require('node:path');
const schema = require('./schema.cjs');
const workbook = require('./workbook.cjs');

function readEntry(folder, name) {
  if (typeof name !== 'string' || path.basename(name) !== name || !/\.xlsx$/i.test(name) || name.startsWith('~$')) throw new Error('자료 목록의 Excel 파일을 선택하세요.');
  const file = path.join(folder, name);
  const stat = fs.lstatSync(file);
  if (!stat.isFile() || stat.isSymbolicLink() || stat.size > 12 * 1024 * 1024) throw new Error('12MB 이하의 일반 Excel 파일만 지원합니다.');
  const bytes = fs.readFileSync(file);
  for (const domain of schema.domains) {
    try { return { name, domain: domain.id, bytes, parsed: workbook.read(bytes, domain.id) }; }
    catch { /* Try each explicitly supported schema; never infer editable cell mappings. */ }
  }
  throw new Error('지원 양식과 다르거나 파일이 손상되었습니다. 현재 무한모드·계정 성장 양식만 편집합니다.');
}

function scan(folder, sources) {
  if (!folder || !fs.existsSync(folder) || !fs.statSync(folder).isDirectory()) throw new Error('자료 폴더를 찾을 수 없습니다. 설정에서 폴더를 선택하세요.');
  const names = fs.readdirSync(folder).filter(name => /\.xlsx$/i.test(name) && !name.startsWith('~$')).sort();
  const output=path.join(folder,'내보내기');
  if(fs.existsSync(output)&&!fs.lstatSync(output).isSymbolicLink())for(const sub of fs.readdirSync(output).filter(n=>n.startsWith('콘텐츠-')).sort().reverse().slice(0,30)){
    const dir=path.join(output,sub);if(!fs.lstatSync(dir).isDirectory()||fs.lstatSync(dir).isSymbolicLink())continue;
    for(const name of fs.readdirSync(dir).filter(n=>/\.xlsx$/i.test(n)))names.push(path.join('내보내기',sub,name));
  }
  return { folder, total: names.length, truncated: names.length > 100, entries: names.slice(0, 100).map(name => {
    try {
      const entry = readEntry(folder, name);
      const domain = schema.domains.find(d => d.id === entry.domain);
      const current = sources.find(source => source.domain === entry.domain);
      return { name, domain: entry.domain, label: domain.name, count: domain.fields.length, sha256: entry.parsed.sha256,
        status: current?.sha256 === entry.parsed.sha256 ? 'same' : 'different' };
    } catch (error) {
      try {
        const stat=fs.lstatSync(path.join(folder,name));if(!stat.isFile()||stat.isSymbolicLink()||stat.size>8*1024*1024)throw Error('지원 크기 초과');
        const parsed=require('./content-workbook.cjs').inspect(fs.readFileSync(path.join(folder,name)));
        return {name,domain:'content',label:parsed.label,count:parsed.count,sha256:parsed.sha256,status:'different'};
      }catch{return { name, status: 'unsupported', error: error.message };}
    }
  }) };
}

function writePackage(folder, seedFolder) {
  fs.mkdirSync(folder, { recursive: true });
  const files = schema.domains.map(domain => {
    const bytes = fs.readFileSync(path.join(seedFolder, domain.file));
    const parsed = workbook.read(bytes, domain.id);
    fs.writeFileSync(path.join(folder, domain.file), bytes, { flag: 'wx' });
    return { file: domain.file, domain: domain.id, sha256: parsed.sha256, editableFields: domain.fields.length };
  });
  fs.writeFileSync(path.join(folder, '자료정보.json'), JSON.stringify({ version: 1, appVersion: require('./package.json').version,
    createdAt: new Date().toISOString(), kind: 'bundled_snapshot', files, fields: schema.fields }, null, 2));
  const guide = ['# 동봉 기획 자료', '', 'Unity 프로젝트나 Excel 설치 없이 앱에서 바로 편집합니다. 자료 목록에서 파일을 선택하세요.',
    '이 파일들은 배포 시점의 기준 사본입니다. 팀/서버의 최신 적용본을 자동으로 조회하지 않습니다.',
    '파일을 직접 바꿨다면 앱의 자료 목록에서 새로고침 후 가져오세요. 개인 실험은 자동으로 바뀌지 않습니다.', '',
    ...schema.domains.flatMap(d => [`## ${d.name} — ${d.file}`, '', d.instruction, '', ...schema.fields.filter(f => f.domain === d.id).flatMap(f =>
      [`### ${f.label}`, f.description, f.example, f.impact || '', `단위: ${f.unit} / 위치: ${f.sheet}!${f.cell} / 연결 키: ${f.key}`, ''])])];
  fs.writeFileSync(path.join(folder, '항목안내.md'), guide.join('\n'));
  return files;
}
module.exports = { readEntry, scan, writePackage };
