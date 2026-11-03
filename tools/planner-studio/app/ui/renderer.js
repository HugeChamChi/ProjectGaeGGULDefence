'use strict';

const $ = id => document.getElementById(id);
let state, values = {}, selected = 'endless.StartSeconds', resultFields = [], dirty = false;
let searchSequence = 0, previewSequence = 0, previewTimer, toastTimer, lastExport;
let editingGuideUid;
const display = value => Number.isFinite(Number(value)) && String(value).trim() !== '' ? Number(value).toLocaleString('ko-KR', { maximumFractionDigits: 5 }) : String(value);
const domainOf = id => state.domains.find(domain => domain.id === id);
const selectedField = () => state.fields.find(field => field.uid === selected);
const selectedDomain = () => domainOf(selectedField().domain);
const element = (tag, className, text) => { const node = document.createElement(tag); if (className) node.className = className; if (text !== undefined) node.textContent = text; return node; };

async function call(name, payload) {
  const response = await window.studio[name](payload);
  if (!response.ok) throw new Error(response.error || '요청을 처리하지 못했습니다.');
  return response.data;
}
function toast(message, error = false) {
  clearTimeout(toastTimer); $('toast').textContent = message; $('toast').className = 'toast' + (error ? ' error' : ''); $('toast').hidden = false;
  toastTimer = setTimeout(() => { $('toast').hidden = true; }, error ? 7000 : 3500);
}
function onError(error) { toast(error.message || String(error), true); $('statusText').textContent = error.message || String(error); }
function action(fn) { return event => Promise.resolve(fn(event)).catch(onError); }
function title() { return $('experimentTitle').value.trim() || selectedDomain().name + ' 실험'; }
function payload() { return { values, title: title(), note: $('experimentNote').value, domain: selectedDomain().id }; }
function changeCount() { return state.fields.filter(field => String(values[field.uid]) !== String(state.baselineValues[field.uid])).length; }
function updateDirty() {
  dirty = state.fields.some(field => String(values[field.uid]) !== String(state.values[field.uid])) || $('experimentTitle').value !== (state.current.title || '') || $('experimentNote').value !== (state.current.note || '');
  window.studio.dirty({ dirty });
  $('saveStatus').textContent = dirty ? '저장 전' : state.current.savedAt ? '저장됨 · ' + new Date(state.current.savedAt).toLocaleTimeString('ko-KR', { hour: '2-digit', minute: '2-digit' }) : '저장 전';
  $('statusText').textContent = `${changeCount()}개 항목 변경`;
}
function confirmDiscard() { return !dirty || window.confirm('저장하지 않은 수정이 있습니다. 현재 수정을 버리고 계속할까요?'); }

async function applyState(next) {
  state = next; values = { ...next.values }; dirty = false;
  $('experimentTitle').value = next.current.title || ''; $('experimentNote').value = next.current.note || '';
  $('historyCount').textContent = next.history.length;
  await call('dirty', { dirty: false });
  renderEditor(); renderGuide(); await renderResults(); updateDirty();
}

async function renderResults() {
  const sequence = ++searchSequence;
  const results = await call('search', { query: $('searchInput').value, domain: 'all' });
  if (sequence !== searchSequence) return;
  resultFields = results;
  const list = $('resultsList'); list.replaceChildren(); $('resultCount').textContent = results.length;
  if (!results.length) list.append(element('div', 'empty-state', '찾는 항목이 없어요.\n다른 이름이나 키워드로 검색해 보세요.'));
  for (const field of results) {
    const card = element('button', 'result-card' + (field.uid === selected ? ' selected' : ''));
    card.type = 'button'; card.dataset.field = field.uid;
    card.append(element('span', 'result-domain', `${field.domainName}  ›  ${field.destination}`));
    card.append(element('strong', '', field.label)); card.append(element('code', 'result-key', field.key));
    const bottom = element('span', 'result-bottom');
    bottom.append(element('span', 'result-value', display(values[field.uid]) + ' ' + field.unit));
    bottom.append(element('span', 'result-route', String(values[field.uid]) !== String(state.baselineValues[field.uid]) ? '수정됨' : '기준값'));
    card.append(bottom); card.addEventListener('click', () => select(field.uid)); list.append(card);
  }
}

function select(uid) {
  const previousDomain = selectedField()?.domain;
  selected = uid;
  if (previousDomain !== selectedField().domain) renderEditor();
  document.querySelectorAll('.field-row').forEach(row => row.classList.toggle('focused', row.dataset.field === uid));
  document.querySelectorAll('.result-card').forEach(card => card.classList.toggle('selected', card.dataset.field === uid));
  renderGuide(); updateDirty();
}

function renderEditor() {
  const domain = selectedDomain(); const source = state.sources.find(source => source.domain === domain.id);
  $('domainName').textContent = domain.name; $('domainIcon').textContent = domain.icon;
  $('domainSubtitle').textContent = domain.subtitle;
  $('destinationPill').textContent = domain.destination; $('destinationPill').className = 'destination-pill ' + domain.color;
  $('sourceName').textContent = source.name; $('sourceName').title = 'SHA256: ' + source.sha256;
  const list = $('fieldsList'); list.replaceChildren();
  for (const definition of domain.fields) {
    const uid = domain.id + '.' + definition.id;
    const row = element('div', 'field-row' + (uid === selected ? ' focused' : '') + (String(values[uid]) !== String(state.baselineValues[uid]) ? ' changed' : ''));
    row.dataset.field = uid;
    const info = element('div', 'field-info'); const label = element('label', '', definition.label);
    label.htmlFor = 'value-' + uid; info.append(label, element('code', '', definition.key));
    const inputs = element('div', 'field-inputs'); inputs.append(element('span', 'baseline-number', display(state.baselineValues[uid])), element('span', 'field-arrow', '→'));
    const wrapper = element('div', 'number-wrap'); const input = element('input');
    input.id = 'value-' + uid; input.dataset.uid = uid; input.type = 'text'; input.inputMode = 'decimal'; input.autocomplete = 'off';
    input.value = values[uid]; input.setAttribute('aria-label', definition.label); input.spellcheck = false;
    input.addEventListener('focus', () => select(uid));
    input.addEventListener('input', () => {
      values[uid] = input.value;
      row.classList.toggle('changed', input.value !== String(state.baselineValues[uid]));
      updateDirty(); renderResults().catch(onError); schedulePreview();
    });
    wrapper.append(input, element('span', '', definition.unit)); inputs.append(wrapper); row.append(info, inputs); list.append(row);
  }
  schedulePreview();
}

function renderGuide() {
  const field = selectedField(), domain = selectedDomain();
  $('guideLabel').textContent = field.label; $('guideDescription').textContent = field.description; $('guideExample').textContent = field.example;
  $('guideKey').textContent = field.key; $('guideUnit').textContent = field.unit;
  $('guideRule').textContent = (field.exclusiveMin ? '0보다 큰 ' : `${field.min} 이상의 `) + (field.integer ? '정수' : '숫자');
  const source = state.sources.find(item => item.domain === domain.id);
  $('guideLocation').textContent = `${source.name}\n${field.sheet} ! ${field.cell}`;
  $('guideTarget').textContent = `${domain.destination} · ${domain.target}`;
  let impact = $('guideImpact');
  if (!impact) { impact = element('p', 'guide-impact'); impact.id = 'guideImpact'; $('guideExample').parentElement.after(impact); }
  impact.textContent = field.impact;
}

async function showLibrary() {
  if (!$('libraryDialog').open) $('libraryDialog').showModal();
  $('libraryList').replaceChildren(); $('libraryError').textContent = '';
  $('refreshLibraryButton').disabled = true;
  try {
    const result = await call('library');
    $('libraryFolder').textContent = result.folder;
    if (!result.entries.length) $('libraryList').append(element('p', 'empty-state', '이 폴더에 Excel이 없습니다. 파일을 넣고 새로고침하거나 동봉 자료를 사용하세요.'));
    if (result.truncated) $('libraryError').textContent = '100개까지 표시합니다. 필요한 자료를 별도 폴더에 모아 주세요.';
    for (const entry of result.entries) {
      const row = element('div', 'history-item'); const info = element('div');
      info.append(element('strong', '', entry.name));
      info.append(element('p', '', entry.status === 'unsupported' ? entry.error : `${entry.label} · ${entry.count}개 편집 항목 · ${entry.domain==='content'?'기획 데이터 · 가져와 편집':entry.status === 'same' ? '현재 기준과 동일' : '현재 기준과 다른 파일'}`));
      row.append(info);
      if (entry.status !== 'unsupported') {
        const button = element('button', 'button secondary', '가져와 편집'); button.dataset.libraryName = entry.name;
        button.addEventListener('click', action(async () => {
          if (!confirmDiscard()) return;
          button.disabled = true;
          try {
            if(entry.domain==='content'){
              if(!confirm('선택한 Excel에 포함된 표를 현재 콘텐츠에 가져올까요?'))return;
              const result=await call('contentLibraryImport',{name:entry.name,sha256:entry.sha256});
              window.dispatchEvent(new CustomEvent('content-imported',{detail:result}));
              $('libraryDialog').close();toast('Excel을 가져왔습니다.');return;
            }
            const next = await call('importLibrary', { name: entry.name, sha256: entry.sha256 });
            selected = next.fields.find(f => f.domain === entry.domain).uid;
            await applyState(next); $('libraryDialog').close(); toast('선택한 파일을 편집 기준으로 가져왔습니다.');
          } finally { button.disabled = false; }
        })); row.append(button);
      }
      $('libraryList').append(row);
    }
  } catch (error) { $('libraryError').textContent = error.message; }
  finally { $('refreshLibraryButton').disabled = false; }
}

function schedulePreview() { clearTimeout(previewTimer); previewTimer = setTimeout(() => renderPreview().catch(onError), 70); }
async function renderPreview() {
  const sequence = ++previewSequence;
  const domain = selectedDomain(); const data = await call('preview', { domain: domain.id, values });
  if (sequence !== previewSequence || selectedDomain().id !== domain.id) return;
  $('fieldsList').querySelectorAll('.field-error').forEach(node => node.remove());
  for (const error of data.errors) {
    const row = [...document.querySelectorAll('.field-row')].find(row => row.dataset.field === error.uid);
    if (row) row.after(element('p', 'field-error', error.message));
  }
  const chart = $('graphContainer'); chart.replaceChildren(); const metrics = $('metrics'); metrics.replaceChildren();
  $('graphTitle').textContent = domain.id === 'endless' ? '보스 HP 성장 비교' : '레벨별 필요 경험치';
  $('graphCaption').textContent = domain.id === 'endless'
    ? '기준 HP 100 · 11라운드부터 매 라운드 성장 가정 · 실제 전투 시뮬레이션은 아닙니다.'
    : '현재 계정 표의 증가식 비교 · 전투 중 선택지 경험치와는 별개입니다.';
  if (data.errors.length) { chart.append(element('div', 'empty-state', '입력값을 확인하면 비교 그래프가 표시됩니다.')); return; }
  const points = [...data.before, ...data.after];
  if (points.some(p => !Number.isFinite(p.y))) { chart.append(element('div', 'empty-state', '계산 범위를 초과해 그래프를 표시할 수 없습니다.')); return; }
  const width = 420, height = 111, left = 37, right = 10, top = 10, bottom = 18;
  const maxY = Math.max(1, ...points.map(p => p.y)); const lastIndex = Math.max(0, data.after.length - 1); const count = Math.max(1, lastIndex);
  const ns = 'http://www.w3.org/2000/svg';
  const svg = document.createElementNS(ns, 'svg'); svg.setAttribute('viewBox', `0 0 ${width} ${height}`); svg.setAttribute('role', 'img'); svg.setAttribute('aria-label', $('graphTitle').textContent);
  const add = (tag, attributes, text) => { const node = document.createElementNS(ns, tag); for (const [name, value] of Object.entries(attributes)) node.setAttribute(name, value); if (text !== undefined) node.textContent = text; svg.append(node); return node; };
  for (const fraction of [0, .5, 1]) {
    const y = top + (height - top - bottom) * fraction;
    add('line', { x1: left, x2: width - right, y1: y, y2: y, stroke: '#e8edf4', 'stroke-width': 1, 'stroke-dasharray': '3 4' });
    add('text', { x: left - 6, y: y + 3, 'text-anchor': 'end', fill: '#9aa6b8', 'font-size': 8, 'font-family': 'Malgun Gothic' }, compact(maxY * (1 - fraction)));
  }
  for (const [series, color, stroke] of [[data.before, '#b8c6d4', 2], [data.after, '#7b65d6', 2.3]]) {
    const coordinates = series.map((point, i) => `${left + i / count * (width - left - right)},${height - bottom - (point.y / maxY) * (height - top - bottom)}`).join(' ');
    add('polyline', { points: coordinates, fill: 'none', stroke: color, 'stroke-width': stroke, 'stroke-linejoin': 'round', 'stroke-linecap': 'round' });
  }
  for (const i of new Set([0, Math.floor(lastIndex / 2), lastIndex])) add('text', { x: left + i / count * (width - left - right), y: height - 1, 'text-anchor': 'middle', fill: '#9aa6b8', 'font-size': 8 }, data.after[i].x + (domain.id === 'endless' ? 'R' : 'Lv'));
  chart.append(svg);
  const metric = (label, before, after, unit = '') => {
    const box = element('div', 'metric'); box.append(element('small', '', label));
    const line = element('strong'); line.append(document.createTextNode(compact(before) + ' → '), element('em', '', compact(after) + unit)); box.append(line); metrics.append(box);
  };
  if (domain.id === 'endless') {
    metric('시작 시간', state.baselineValues['endless.StartSeconds'], values['endless.StartSeconds'], '초');
    metric('40R 예상 HP 배율', data.before.at(-1).y / 100, data.after.at(-1).y / 100, '배');
  } else {
    const last = state.contexts.account.maxLevel - 2;
    metric('마지막 레벨업 필요량', Number(state.baselineValues['account.FirstRequiredExperience']) + last * Number(state.baselineValues['account.RequirementStep']), Number(values['account.FirstRequiredExperience']) + last * Number(values['account.RequirementStep']), ' EXP');
    if (state.contexts.account.maxLevel > 101) $('graphCaption').textContent += ' 그래프는 첫 100레벨 구간을 표시합니다.';
    metric('보스 3.5마리 예시', Math.floor(state.baselineValues['account.BossExperience'] * 3.5), Math.floor(Number(values['account.BossExperience']) * 3.5), ' EXP');
  }
}
function compact(value) {
  const number = Number(value);
  if (!Number.isFinite(number)) return '범위 초과';
  if (Math.abs(number) >= 1e9) return number.toExponential(1);
  if (Math.abs(number) >= 10000) return (number / 10000).toLocaleString('ko-KR', { maximumFractionDigits: 1 }) + '만';
  return number.toLocaleString('ko-KR', { maximumFractionDigits: 1 });
}

async function saveExperiment() {
  const next = await call('save', payload()); await applyState(next); toast('실험을 저장했습니다.');
}
async function exportExcel() {
  const check = await call('validate', { values });
  if (!check.ok) {
    select(check.errors[0].uid); await renderPreview(); throw new Error(check.errors[0].message);
  }
  lastExport = await call('export', payload());
  $('exportInstruction').textContent = lastExport.instruction; $('exportFile').value = lastExport.file;
  $('consoleButton').hidden = lastExport.manifest.domain !== 'endless';
  $('exportDialog').showModal(); $('statusText').textContent = 'Excel 내보내기 완료';
}
async function history() {
  const list = $('historyList'); list.replaceChildren();
  if (!state.history.length) list.append(element('p', 'empty-state', '아직 저장한 실험이 없어요. 수치를 바꾼 뒤 실험 이름과 메모를 남겨보세요.'));
  for (const entry of state.history) {
    const button = element('button', 'history-item'); const body = element('div');
    body.append(element('strong', '', entry.title), element('p', '', new Date(entry.savedAt).toLocaleString('ko-KR') + ' · ' + entry.changeCount + '개 변경'), element('p', '', entry.note || '메모 없음'));
    button.append(body, element('span', '', '불러오기 →'));
    button.addEventListener('click', action(async () => { if (!confirmDiscard()) return; await applyState(await call('load', { id: entry.id })); $('historyDialog').close(); toast('실험을 불러왔습니다.'); }));
    list.append(button);
  }
  $('historyDialog').showModal();
}

function editGuide() {
  const field = selectedField(); editingGuideUid = field.uid;
  $('guideEditField').textContent = field.label + ' · ' + field.key;
  $('guideEditError').hidden = true;
  $('guideDescriptionInput').value = field.description; $('guideExampleInput').value = field.example;
  $('guideDialog').showModal();
}
async function saveGuide() {
  const button = $('saveGuideButton'); button.disabled = true;
  try {
    state.fields = await call('saveGuide', { uid: editingGuideUid, guide: { description: $('guideDescriptionInput').value, example: $('guideExampleInput').value } });
    renderGuide(); await renderResults(); $('guideDialog').close(); toast('설명을 저장했습니다.');
  } catch (error) { $('guideEditError').textContent = error.message; $('guideEditError').hidden = false;
  } finally { button.disabled = false; }
}

function bind() {
  $('libraryButton').addEventListener('click', action(showLibrary));
  $('refreshLibraryButton').addEventListener('click', action(showLibrary));
  $('bundledFolderButton').addEventListener('click', action(async () => { await call('useBundledFolder'); await showLibrary(); }));
  $('editGuideButton').addEventListener('click', editGuide);
  $('guideForm').addEventListener('submit', action(async event => { event.preventDefault(); await saveGuide(); }));
  $('defaultGuideButton').addEventListener('click', () => {
    const field = state.fields.find(item => item.uid === editingGuideUid);
    $('guideDescriptionInput').value = field.defaultDescription; $('guideExampleInput').value = field.defaultExample;
  });
  $('searchInput').addEventListener('input', action(renderResults));
  $('saveButton').addEventListener('click', action(saveExperiment)); $('exportButton').addEventListener('click', action(exportExcel)); $('historyButton').addEventListener('click', action(history));
  $('resetButton').addEventListener('click', action(async () => { if (!confirmDiscard()) return; await applyState(await call('reset')); toast('기준값으로 되돌렸습니다.'); }));
  $('experimentTitle').addEventListener('input', updateDirty); $('experimentNote').addEventListener('input', updateDirty);
  $('importButton').addEventListener('click', () => $('importDialog').showModal());
  document.querySelectorAll('[data-import]').forEach(button => button.addEventListener('click', action(async () => {
    if (!confirmDiscard()) return;
    const imported = await call('import', { domain: button.dataset.import === 'bundle' ? selectedDomain().id : button.dataset.import });
    if (imported) {
      if (button.dataset.import !== 'bundle') selected = imported.fields.find(f => f.domain === button.dataset.import).uid;
      await applyState(imported); $('importDialog').close(); toast(imported.importWarning || '자료를 가져왔습니다.', Boolean(imported.importWarning));
    }
  })));
  $('shareButton').addEventListener('click', action(async () => { const shared = await call('share', payload()); if (shared) toast('실험 묶음을 저장했습니다.'); }));
  $('openExportsButton').addEventListener('click', action(() => call('openExports'))); $('revealExportButton').addEventListener('click', action(() => call('openExports')));
  $('settingsButton').addEventListener('click', action(async () => { renderSettings(await call('settings')); $('settingsDialog').showModal(); }));
  $('chooseExcelFolderButton').addEventListener('click', action(async () => {
    const button = $('chooseExcelFolderButton'); button.disabled = true;
    try { const settings = await call('chooseExcelFolder'); if (settings) { renderSettings(settings); toast('엑셀 폴더를 변경했습니다.'); } }
    finally { button.disabled = false; }
  }));
  $('openExcelFolderButton').addEventListener('click', action(() => call('openExcelFolder')));
  $('consoleButton').addEventListener('click', action(() => call('openConsole')));
  $('exportFile').addEventListener('click', () => $('exportFile').select());
  document.querySelectorAll('[data-close]').forEach(button => button.addEventListener('click', () => $(button.dataset.close).close()));
  document.addEventListener('keydown', event => {
    if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'k') { event.preventDefault(); $('searchInput').focus(); $('searchInput').select(); }
    if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 's') { event.preventDefault(); if ($('guideDialog').open) $('guideForm').requestSubmit(); else saveExperiment().catch(onError); }
  });
}

function renderSettings(settings) {
  $('excelFolder').value = settings.excelFolder || '';
  $('excelFolder').title = settings.excelFolder || '';
  $('exportFolderHint').textContent = '내보내기 위치: ' + settings.exportFolder;
  $('openExcelFolderButton').disabled = !settings.excelFolder;
}

const pause = milliseconds => new Promise(resolve => setTimeout(resolve, milliseconds));
async function until(condition, message) { for (let i = 0; i < 100; i++) { if (condition()) return; await pause(50); } throw new Error(message); }
window.runStudioSmoke = async () => {
  const checks = [];
  await showLibrary();
  if (!$('libraryList').querySelector('[data-library-name="EndlessBalance.xlsx"]') || !$('libraryList').querySelector('[data-library-name="AccountProgression.xlsx"]')) throw new Error('기본 동봉 자료를 찾지 못했습니다.');
  $('libraryList').querySelector('button').click();
  await until(() => !$('libraryDialog').open, '동봉 자료 가져오기 실패');
  checks.push('bundled library lists and imports editable workbooks without a project');
  $('settingsButton').click(); await until(() => $('settingsDialog').open, '설정 창 없음');
  const beforeFolder = $('excelFolder').value;
  $('chooseExcelFolderButton').click(); await until(() => !$('chooseExcelFolderButton').disabled, '폴더 선택 취소 실패');
  if ($('excelFolder').value !== beforeFolder) throw new Error('폴더 선택 취소로 설정이 바뀌었습니다.');
  checks.push('canceling folder selection preserves preferences');
  $('chooseExcelFolderButton').click(); await until(() => !$('chooseExcelFolderButton').disabled, '폴더 설정 실패');
  if (!$('excelFolder').value.endsWith('excel-library') || $('openExcelFolderButton').disabled) throw new Error('폴더 설정 표시 실패');
  checks.push('settings displays the chosen Excel and export folders');
  $('settingsDialog').close();
  $('importButton').click(); document.querySelector('[data-import="endless"]').click();
  await until(() => !$('importDialog').open && state.sources.some(s => s.name === 'FolderEndless.xlsx'), '설정 폴더의 자료 가져오기 실패');
  checks.push('import from configured folder updates the editable baseline');
  await showLibrary();
  if (!$('libraryList').textContent.includes('현재 기준과 동일')) throw new Error('자료 기준 상태 표시 실패');
  $('libraryDialog').close(); checks.push('library refresh shows configured folder and baseline status');
  await applyState(await call('reset'));
  $('searchInput').value = '시간'; await renderResults();
  if (resultFields.length !== 3) throw new Error('시간 검색 결과가 3개가 아닙니다.'); checks.push('Korean keyword search');
  $('searchInput').value = '피통'; await renderResults();
  if (resultFields.length !== 1 || resultFields[0].id !== 'HpGrowthPercent') throw new Error('별칭 검색 실패'); checks.push('synonym search');
  $('searchInput').value = 'StartSeconds'; await renderResults();
  if (resultFields.length !== 1) throw new Error('데이터 키 검색 실패'); checks.push('real data key search');
  $('resultsList').querySelector('button').click();
  const setValue = (uid, value) => { const input = document.querySelector(`input[data-uid="${uid}"]`); if (!input) throw new Error('편집 칸 없음: ' + uid); input.value = value; input.dispatchEvent(new Event('input', { bubbles: true })); };
  setValue('endless.StartSeconds', '0');
  if ((await call('validate', { values })).ok) throw new Error('0초 허용 오류'); checks.push('invalid value rejected');
  setValue('endless.StartSeconds', '180'); setValue('endless.HpGrowthPercent', '15');
  await renderPreview();
  if (!$('graphContainer').querySelector('svg') || !$('guideDescription').textContent.includes('타이머')) throw new Error('설명/그래프 없음'); checks.push('inline edit, field guide and before-after chart');
  if (!$('guideImpact').textContent.includes('처치 보너스')) throw new Error('항목 영향 설명 없음');
  checks.push('planner sees field impact without opening project references');
  const descriptionBefore = selectedField().description, draftBefore = JSON.stringify(values);
  $('editGuideButton').click(); $('guideDescriptionInput').value = '취소할 설명';
  $('guideDialog').querySelector('[data-close]').click();
  if (selectedField().description !== descriptionBefore) throw new Error('취소한 설명이 적용되었습니다.');
  checks.push('canceling description edits preserves the previous guide');
  $('editGuideButton').click(); $('guideDescriptionInput').value = '   '; $('guideForm').requestSubmit();
  await until(() => !$('guideEditError').hidden, '빈 설명 오류가 표시되지 않았습니다.');
  if (!$('guideDialog').open) throw new Error('잘못된 설명을 저장했습니다.');
  checks.push('invalid description is rejected inside the editor');
  $('guideDescriptionInput').value = '준비모래시계: NORMAL 전투의 시작 타이머입니다.\n<설명 예시> 시간을 초 단위로 입력하세요.';
  $('guideExampleInput').value = '180을 입력하면 3분으로 시작합니다.'; $('guideForm').requestSubmit();
  await until(() => !$('guideDialog').open, '설명 저장 실패');
  if (!$('guideDescription').textContent.includes('<설명 예시>') || $('guideDescription').children.length || JSON.stringify(values) !== draftBefore || !dirty) throw new Error('설명 편집으로 텍스트/수치 초안이 변경되었습니다.');
  checks.push('description and example save safely without losing unsaved numeric edits');
  $('searchInput').value = '준비모래시계'; await renderResults();
  if (resultFields.length !== 1 || resultFields[0].id !== 'StartSeconds') throw new Error('수정한 설명 검색 실패');
  checks.push('custom descriptions are searchable');
  $('experimentTitle').value = '초반 여유 시간 늘리기'; $('experimentNote').value = '시작 +30초, HP 성장률 +2%p 비교';
  $('saveButton').click(); await until(() => state.current.title === '초반 여유 시간 늘리기' && !dirty, '실험 저장 실패');
  if (!state.history.length) throw new Error('실험 이력 없음'); checks.push('private experiment persistence');
  $('exportButton').click(); await until(() => $('exportDialog').open, '내보내기 창 없음');
  if (!$('exportFile').value.endsWith('EndlessBalance.xlsx') || !$('exportDialog').textContent.includes('서버·게임 적용 미확인')) throw new Error('내보내기 안내 오류');
  checks.push('manual backend handoff, no auto browser opening'); $('exportDialog').close();
  const historyEntry = state.history[0]; await applyState(await call('load', { id: historyEntry.id }));
  if (Number(values['endless.StartSeconds']) !== 180) throw new Error('실험 다시 불러오기 실패'); checks.push('reload keeps snapshot values');
  select('account.BossExperience'); setValue('account.BossExperience', '200'); await renderPreview();
  if (!$('guideTarget').textContent.includes('Unity')) throw new Error('계정 데이터 반영 대상 오류');
  checks.push('account curve edit and Unity destination guide');
  await exportExcel();
  if (!lastExport.file.endsWith('AccountProgression.xlsx') || getComputedStyle($('consoleButton')).display !== 'none') throw new Error('계정 Excel을 뒤끝 업로드 대상으로 잘못 표시했습니다.');
  $('exportDialog').close(); checks.push('account XLSX export does not offer backend upload');
  setValue('account.BossExperience', String(state.values['account.BossExperience'])); select('endless.StartSeconds');
  const beforeImport = state.current.id;
  const canceled = await call('import', { domain: 'endless' });
  if (canceled !== null || (await call('state')).current.id !== beforeImport) throw new Error('설명 선택에서 취소해도 실험을 바꿨습니다.');
  checks.push('canceling shared-guide prompt preserves current experiment');
  const ownGuide = selectedField().description;
  await applyState(await call('import', { domain: 'endless' }));
  if (selectedField().description !== ownGuide) throw new Error('내 설명 유지 선택 실패');
  checks.push('shared experiment can keep recipient descriptions');
  await applyState(await call('import', { domain: 'endless' }));
  if (!selectedField().description.includes('다른 기획자가 공유한 설명')) throw new Error('받은 설명 적용 선택 실패');
  checks.push('shared experiment applies descriptions only after explicit choice');
  const editRect = $('saveButton').getBoundingClientRect(); const guideRect = $('guideLabel').getBoundingClientRect();
  if (editRect.bottom > window.innerHeight || guideRect.right > window.innerWidth) throw new Error('주요 조작부가 창 밖에 있습니다.'); checks.push('editor and actions fit within window');
  return checks;
};
window.finishStudioSmoke = async () => { $('searchInput').value = ''; selected = 'endless.StartSeconds'; await renderResults(); renderEditor(); renderGuide(); await renderPreview(); $('toast').hidden = true; };
window.checkStudioLayout = () => {
  for (const id of ['saveButton', 'exportButton', 'settingsButton', 'guideLabel']) {
    const rect = $(id).getBoundingClientRect();
    if (rect.left < 0 || rect.top < 0 || rect.right > window.innerWidth || rect.bottom > window.innerHeight) throw new Error('창 밖의 조작부: ' + id);
  }
  if (document.documentElement.scrollWidth > window.innerWidth) throw new Error('가로 넘침');
};
window.showStudioSettings = async () => { renderSettings(await call('settings')); $('settingsDialog').showModal(); $('toast').hidden = true; };
window.showStudioGuide = () => { $('settingsDialog').close(); editGuide(); $('toast').hidden = true; };
window.showStudioLibrary = showLibrary;

async function start() {
  bind(); await applyState(await call('state')); await renderPreview(); window.__studioReady = true;
}
start().catch(error => { $('statusText').textContent = '자료를 불러올 수 없습니다: ' + error.message; onError(error); });
