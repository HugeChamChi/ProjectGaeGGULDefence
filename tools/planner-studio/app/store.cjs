'use strict';

const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const schema = require('./schema.cjs');
const workbook = require('./workbook.cjs');
const library = require('./library.cjs');

function atomic(file, bytes) {
  fs.mkdirSync(path.dirname(file), { recursive: true });
  const temporary = file + '.' + crypto.randomUUID() + '.tmp';
  try {
    fs.writeFileSync(temporary, bytes, { flag: 'wx' });
    fs.renameSync(temporary, file);
  } finally { if (fs.existsSync(temporary)) fs.unlinkSync(temporary); }
}
const json = (file, value) => atomic(file, JSON.stringify(value, null, 2));
const readJSON = file => JSON.parse(fs.readFileSync(file, 'utf8'));

class StudioStore {
  constructor(root, seedFolder, defaultExcelFolder = null) {
    this.defaultExcelFolder = defaultExcelFolder;
    this.root = path.resolve(root);
    fs.mkdirSync(this.root, { recursive: true });
    this.settingsPath = path.join(this.root, 'settings.json');
    this.settings = fs.existsSync(this.settingsPath) ? readJSON(this.settingsPath) : { excelFolder: null };
    if (!this.settings || (this.settings.excelFolder !== null && (typeof this.settings.excelFolder !== 'string' || !path.isAbsolute(this.settings.excelFolder)))) throw new Error('엑셀 폴더 설정을 읽을 수 없습니다.');
    this.guidesPath = path.join(this.root, 'field-guides.json');
    this.guides = fs.existsSync(this.guidesPath) ? readJSON(this.guidesPath) : { version: 1, fields: {} };
    if (!this.guides || this.guides.version !== 1 || !this.guides.fields || Array.isArray(this.guides.fields) || typeof this.guides.fields !== 'object') throw new Error('저장된 항목 설명 형식을 읽을 수 없습니다.');
    for (const [uid, guide] of Object.entries(this.guides.fields)) this.validateGuide(uid, guide);
    this.currentPath = path.join(this.root, 'current.json');
    if (fs.existsSync(this.currentPath)) {
      this.current = readJSON(this.currentPath);
      this.checkSnapshot(this.current);
    } else {
      const sources = schema.domains.map(domain => this.putSource(domain.id, fs.readFileSync(path.join(seedFolder, domain.file)), domain.file, '앱 동봉 자료'));
      this.current = { format: 'ggd-experiment', version: 1, id: null, title: '', note: '', sources, values: this.baselineValues(sources) };
      json(this.currentPath, this.current);
    }
  }
  sourceBytes(source) {
    if (!/^[a-f0-9]{64}$/.test(source.sha256)) throw new Error('자료 해시가 올바르지 않습니다.');
    const bytes = fs.readFileSync(path.join(this.root, 'sources', source.sha256 + '.xlsx'));
    if (workbook.hash(bytes) !== source.sha256) throw new Error('기준 자료가 변경되거나 손상되었습니다. 원래 파일을 다시 가져오세요.');
    return bytes;
  }
  validateGuide(uid, guide) {
    if (!schema.fields.some(field => field.uid === uid)) throw new Error('설명할 항목을 선택하세요.');
    if (!guide || typeof guide !== 'object' || Array.isArray(guide) || Object.keys(guide).some(key => !['description', 'example'].includes(key)) || typeof guide.description !== 'string' || !guide.description.trim() || guide.description.length > 2000 || typeof guide.example !== 'string' || guide.example.length > 1000) throw new Error('설명은 1~2,000자, 입력 예시는 1,000자 이내로 작성하세요.');
    return { description: guide.description.trim(), example: guide.example.trim() };
  }
  fieldDefinitions() {
    return schema.fields.map(field => ({ ...field, ...this.guides.fields[field.uid], defaultDescription: field.description, defaultExample: field.example }));
  }
  search(query, domain = 'all') { return schema.search(query, domain, this.fieldDefinitions()); }
  saveGuide(uid, guide) {
    const clean = this.validateGuide(uid, guide);
    const field = schema.fields.find(item => item.uid === uid);
    const next = { version: 1, fields: { ...this.guides.fields } };
    if (clean.description === field.description && clean.example === field.example) delete next.fields[uid];
    else next.fields[uid] = clean;
    json(this.guidesPath, next);
    this.guides = next;
    return this.fieldDefinitions();
  }
  getSettings() {
    return { excelFolder: this.excelFolder(), defaultExcelFolder: this.defaultExcelFolder, customFolder: Boolean(this.settings.excelFolder), exportFolder: this.exportFolder() };
  }
  excelFolder() { return this.settings.excelFolder || this.defaultExcelFolder; }
  library() { return library.scan(this.excelFolder(), this.current.sources); }
  importLibrary(name, sha256) {
    const entry = library.readEntry(this.excelFolder(), name);
    if (entry.parsed.sha256 !== sha256) throw new Error('목록을 연 뒤 파일이 바뀌었습니다. 새로고침 후 다시 선택하세요.');
    return this.importSource(entry.domain, entry.bytes, entry.name);
  }
  useBundledFolder() {
    if (!this.defaultExcelFolder) throw new Error('동봉 자료 폴더가 없습니다.');
    json(this.settingsPath, { excelFolder: null });
    this.settings = { excelFolder: null };
    return this.getSettings();
  }
  setExcelFolder(folder) {
    if (typeof folder !== 'string' || !path.isAbsolute(folder) || !fs.statSync(folder).isDirectory()) throw new Error('사용할 엑셀 폴더를 선택하세요.');
    fs.accessSync(folder, fs.constants.R_OK | fs.constants.W_OK);
    const next = { excelFolder: path.resolve(folder) };
    json(this.settingsPath, next);
    this.settings = next;
    return this.getSettings();
  }
  exportFolder() {
    if (!this.excelFolder()) return path.join(this.root, 'exports');
    return path.join(this.excelFolder(), '내보내기');
  }
  putSource(domain, bytes, filename, origin = '가져온 자료') {
    if (!schema.domains.some(d => d.id === domain)) throw new Error('지원하지 않는 자료 종류입니다.');
    const parsed = workbook.read(bytes, domain);
    const file = path.join(this.root, 'sources', parsed.sha256 + '.xlsx');
    if (fs.existsSync(file)) {
      if (workbook.hash(fs.readFileSync(file)) !== parsed.sha256) throw new Error('보관된 기준 자료가 손상되었습니다.');
    } else atomic(file, bytes);
    return { domain, sha256: parsed.sha256, name: path.basename(filename).slice(0, 150), origin, addedAt: new Date().toISOString() };
  }
  baselineValues(sources = this.current.sources) {
    return Object.assign({}, ...sources.map(source => workbook.read(this.sourceBytes(source), source.domain).values));
  }
  contexts(sources = this.current.sources) {
    return Object.fromEntries(sources.map(source => [source.domain, workbook.read(this.sourceBytes(source), source.domain).context]));
  }
  checkSnapshot(snapshot) {
    if (snapshot.format !== 'ggd-experiment' || snapshot.version !== 1 || !Array.isArray(snapshot.sources) || snapshot.sources.length !== schema.domains.length || new Set(snapshot.sources.map(s => s.domain)).size !== schema.domains.length || !schema.domains.every(d => snapshot.sources.some(s => s.domain === d.id))) throw new Error('지원하지 않는 실험 파일 형식입니다.');
    if (!snapshot.values || typeof snapshot.values !== 'object' || Object.keys(snapshot.values).length !== schema.fields.length || Object.keys(snapshot.values).some(uid => !schema.fields.some(f => f.uid === uid))) throw new Error('실험 항목의 구성이 현재 앱과 다릅니다.');
    const check = schema.validate(snapshot.values, this.contexts(snapshot.sources));
    if (!check.ok) throw new Error(check.errors.map(e => e.message).join('\n'));
    return check.values;
  }
  getState() {
    return { domains: schema.domains, fields: this.fieldDefinitions(), sources: this.current.sources, values: this.current.values,
      baselineValues: this.baselineValues(), contexts: this.contexts(), current: { id: this.current.id, title: this.current.title, note: this.current.note, savedAt: this.current.savedAt }, history: this.history(), workspace: this.root };
  }
  history() {
    const folder = path.join(this.root, 'experiments');
    if (!fs.existsSync(folder)) return [];
    return fs.readdirSync(folder).filter(name => /^[a-f0-9-]{36}\.json$/.test(name)).map(name => {
      try {
        const entry = readJSON(path.join(folder, name));
        if (entry.format !== 'ggd-experiment' || entry.id !== name.slice(0, -5)) return null;
        return { id: entry.id, title: entry.title, note: entry.note, savedAt: entry.savedAt, changeCount: entry.changeCount };
      } catch { return null; }
    }).filter(Boolean).sort((a, b) => b.savedAt.localeCompare(a.savedAt)).slice(0, 200);
  }
  validate(values) { return schema.validate(values, this.contexts()); }
  save(title, note, values) {
    if (typeof title !== 'string' || !title.trim() || title.trim().length > 80 || typeof note !== 'string' || note.length > 4000) throw new Error('실험 이름은 1~80자, 메모는 4,000자 이내로 입력하세요.');
    const check = this.validate(values);
    if (!check.ok) throw new Error(check.errors.map(e => e.message).join('\n'));
    const baseline = this.baselineValues();
    const snapshot = { format: 'ggd-experiment', version: 1, id: crypto.randomUUID(), parentId: this.current.id,
      title: title.trim(), note, savedAt: new Date().toISOString(), sources: structuredClone(this.current.sources), values: check.values,
      changeCount: schema.fields.filter(f => check.values[f.uid] !== baseline[f.uid]).length };
    json(path.join(this.root, 'experiments', snapshot.id + '.json'), snapshot);
    json(this.currentPath, snapshot);
    this.current = snapshot;
    return this.getState();
  }
  load(id) {
    if (typeof id !== 'string' || !/^[a-f0-9-]{36}$/.test(id)) throw new Error('실험 ID가 올바르지 않습니다.');
    const snapshot = readJSON(path.join(this.root, 'experiments', id + '.json'));
    if (snapshot.id !== id) throw new Error('실험 ID가 파일과 일치하지 않습니다.');
    this.checkSnapshot(snapshot);
    json(this.currentPath, snapshot);
    this.current = snapshot;
    return this.getState();
  }
  reset() {
    this.current = { ...this.current, id: null, title: '', note: '', values: this.baselineValues(), savedAt: undefined };
    json(this.currentPath, this.current);
    return this.getState();
  }
  importSource(domain, bytes, name) {
    const source = this.putSource(domain, bytes, name);
    const sources = this.current.sources.map(s => s.domain === domain ? source : s);
    const snapshot = { format: 'ggd-experiment', version: 1, id: null, title: '', note: '', sources, values: this.baselineValues(sources) };
    this.checkSnapshot(snapshot);
    json(this.currentPath, snapshot);
    this.current = snapshot;
    return this.getState();
  }
  export(domainId, values, title, note) {
    const domain = schema.domains.find(d => d.id === domainId);
    if (!domain) throw new Error('내보낼 데이터 종류를 선택하세요.');
    const check = this.validate(values);
    if (!check.ok) throw new Error(check.errors.map(e => e.message).join('\n'));
    const source = this.current.sources.find(s => s.domain === domainId);
    const bytes = workbook.write(this.sourceBytes(source), domainId, check.values);
    if (this.excelFolder() && (!fs.existsSync(this.excelFolder()) || !fs.statSync(this.excelFolder()).isDirectory())) throw new Error('엑셀 폴더를 찾을 수 없습니다. 설정에서 폴더를 다시 선택하세요.');
    const folder = path.join(this.exportFolder(), new Date().toISOString().replace(/[:.]/g, '-') + '-' + crypto.randomUUID().slice(0, 8));
    const file = path.join(folder, domain.file);
    const baseline = this.baselineValues();
    const manifest = { format: 'ggd-export', version: 1, createdAt: new Date().toISOString(), title: String(title || '').slice(0, 80), note: String(note || '').slice(0, 4000), domain: domainId,
      destination: domain.destination, target: domain.target, applicationStatus: 'not_verified', experimentId: this.current.id,
      source: { sha256: source.sha256, name: source.name }, output: { filename: domain.file, sha256: workbook.hash(bytes) },
      values: Object.fromEntries(domain.fields.map(f => [f.id, check.values[domainId + '.' + f.id]])),
      changes: domain.fields.filter(f => check.values[domainId + '.' + f.id] !== baseline[domainId + '.' + f.id]).map(f => ({ key: f.key, label: f.label, before: baseline[domainId + '.' + f.id], after: check.values[domainId + '.' + f.id] })) };
    try {
      atomic(file, bytes); json(path.join(folder, 'manifest.json'), manifest);
      const lines = [`# ${domain.name} 변경 요약`, '', `실험: ${manifest.title || '이름 없음'}`, manifest.note, '',
        '| 항목 | 기준 | 변경 | 단위 |', '|---|---:|---:|---|', ...domain.fields.map(f => `| ${f.label} | ${baseline[domainId + '.' + f.id]} | ${check.values[domainId + '.' + f.id]} | ${f.unit} |`), '',
        '## 항목 설명', '', ...this.fieldDefinitions().filter(f => f.domain === domainId).flatMap(f => [f.label, f.description, f.example, f.impact || '', '']),
        domain.instruction, '', '파일 생성 완료 · 게임/서버 적용 미확인'];
      atomic(path.join(folder, '변경요약.md'), lines.join('\n'));
      atomic(path.join(folder, '적용안내.md'), `# ${domain.name} 내보내기\n\n${domain.instruction}\n\n이 파일은 로컬에서 생성했습니다. 뒤끝·게임 반영은 확인하지 않았습니다.\n\n원래 Excel의 설명과 메모는 보존했습니다. 설명에 적힌 숫자는 과거 작성 예시일 수 있으므로 실제 입력값과 manifest.json의 변경 내역을 기준으로 확인하세요.\n\n원본 SHA256: ${source.sha256}\n출력 SHA256: ${manifest.output.sha256}\n`);
    } catch (error) {
      throw new Error('파일 묶음 저장이 끝나지 않았습니다. 폴더의 파일은 적용하지 마세요.\n' + error.message);
    }
    return { file, folder, manifest, instruction: domain.instruction };
  }
  bundle(values, title, note) {
    const check = this.validate(values);
    if (!check.ok) throw new Error(check.errors.map(e => e.message).join('\n'));
    return { format: 'ggd-plan-bundle', version: 1, sharedAt: new Date().toISOString(), title: String(title || '공유 실험').slice(0, 80), note: String(note || '').slice(0, 4000),
      sources: this.current.sources.map(source => ({ ...source, data: this.sourceBytes(source).toString('base64') })), values: check.values,
      guides: structuredClone(this.guides.fields) };
  }
  importBundle(bytes, includeGuides = false) {
    if (bytes.length > 32 * 1024 * 1024) throw new Error('실험 묶음 크기가 지원 범위를 초과합니다.');
    const bundle = JSON.parse(Buffer.from(bytes).toString('utf8'));
    if (bundle.format !== 'ggd-plan-bundle' || bundle.version !== 1 || !Array.isArray(bundle.sources) || bundle.sources.length !== 2 || new Set(bundle.sources.map(s => s.domain)).size !== 2 || typeof bundle.title !== 'string' || typeof bundle.note !== 'string') throw new Error('지원하지 않는 실험 묶음입니다.');
    const incomingGuides = {};
    if (includeGuides && bundle.guides !== undefined) {
      if (!bundle.guides || typeof bundle.guides !== 'object' || Array.isArray(bundle.guides)) throw new Error('전달받은 설명 형식이 올바르지 않습니다.');
      for (const [uid, guide] of Object.entries(bundle.guides)) incomingGuides[uid] = this.validateGuide(uid, guide);
    }
    const prepared = bundle.sources.map(source => {
      if (typeof source.data !== 'string' || source.data.length > 12 * 1024 * 1024 || typeof source.name !== 'string' || !schema.domains.some(d => d.id === source.domain)) throw new Error('실험 묶음의 기준 자료가 잘못되었습니다.');
      const raw = Buffer.from(source.data, 'base64');
      if (workbook.hash(raw) !== source.sha256) throw new Error('실험 묶음의 자료 해시가 일치하지 않습니다.');
      return { source, raw, parsed: workbook.read(raw, source.domain) };
    });
    const check = schema.validate(bundle.values || {}, Object.fromEntries(prepared.map(p => [p.source.domain, p.parsed.context])));
    if (!check.ok || Object.keys(bundle.values || {}).length !== schema.fields.length) throw new Error('실험 묶음의 입력값이 올바르지 않습니다.');
    const sources = prepared.map(p => this.putSource(p.source.domain, p.raw, p.source.name, '전달받은 실험 자료'));
    const previous = this.current;
    this.current = { ...this.current, sources, id: null };
    try { this.save(bundle.title || '전달받은 실험', bundle.note, check.values); }
    catch (error) { this.current = previous; throw error; }
    if (includeGuides && Object.keys(incomingGuides).length) {
      try {
        const next = { version: 1, fields: { ...this.guides.fields, ...incomingGuides } };
        json(this.guidesPath, next); this.guides = next;
      } catch { return { ...this.getState(), importWarning: '실험은 가져왔지만 항목 설명 저장에 실패했습니다. 내 설명은 유지됩니다.' }; }
    }
    return this.getState();
  }
}

module.exports = { StudioStore, atomic };
