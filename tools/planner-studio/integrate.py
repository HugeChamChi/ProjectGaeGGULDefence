"""Extend the preserved Planner source; never edit the installed package here."""
from pathlib import Path
import shutil

ROOT = Path(__file__).resolve().parent
APP = ROOT / 'app'
SOURCE = ROOT.parent / 'reward-editor'
CONTENT = APP / 'ui' / 'content'
CONTENT.mkdir(exist_ok=True)
for name in ['index.html', 'style.css', 'studio.css', 'seed.js', 'schemas.js', 'core.js', 'studio-core.js', 'app.js']:
    shutil.copy2(SOURCE / name, CONTENT / name)

def replace(path, old, new):
    text = path.read_text(encoding='utf-8')
    if new in text:
        return
    if old in text:
        path.write_text(text.replace(old, new), encoding='utf-8')
    elif new not in text:
        raise RuntimeError(f'Missing integration anchor: {path}: {old[:60]}')

replace(APP/'ui/index.html', '<script src="renderer.js" defer></script>', '<script src="renderer.js" defer></script>\n  <script src="content-shell.js" defer></script>\n  <link rel="stylesheet" href="content-shell.css">')
replace(APP/'ui/index.html', '<nav aria-label="내 작업">', '<nav aria-label="내 작업">\n      <button class="nav-item active" id="balanceWorkspaceButton"><span class="nav-icon">≋</span>밸런스 편집</button>\n      <button class="nav-item" id="contentWorkspaceButton"><span class="nav-icon">◇</span>강화·보상 콘텐츠</button>')
if "'content-load'" not in (APP/'preload.cjs').read_text(encoding='utf-8'):
    replace(APP/'preload.cjs', "const channels = [", "const channels = ['content-load', 'content-save', 'content-export', ")
replace(APP/'main.cjs', "  handler('state', () => store.getState());", "  require('./content-service.cjs').register({ handler, workspace, store, atomic });\n  handler('state', () => store.getState());")

# Reuse the existing Planner frame and palette instead of adding another app shell.
replace(CONTENT/'index.html', '<body>', '<body class="planner-content">')
replace(CONTENT/'index.html', '<link rel="stylesheet" href="studio.css">', '<link rel="stylesheet" href="studio.css"><link rel="stylesheet" href="planner-theme.css">')
replace(CONTENT/'index.html', '<script src="app.js"></script>', '<script src="desktop.js"></script><script src="app.js"></script>')
replace(CONTENT/'index.html', '<script src="desktop.js"></script>', '<script src="current-scope.js"></script><script src="desktop.js"></script>')
replace(CONTENT/'index.html', '<script src="desktop.js"></script>', '<script src="unit-base-seed.js"></script><script src="unit-keys.js"></script><script src="desktop.js"></script>')
replace(CONTENT/'index.html', '<script src="desktop.js"></script>', '<script src="attendance-events.js"></script><script src="desktop.js"></script>')
replace(CONTENT/'index.html', '<script src="app.js"></script>', '<script src="reward-grid.js"></script><script src="app.js"></script>')
replace(CONTENT/'index.html', '<script src="app.js"></script>', '<script src="unit-overview.js"></script><script src="app.js"></script>')
replace(CONTENT/'index.html', '<script src="app.js"></script>', '<script src="tutorial-rich.js"></script><script src="app.js"></script>')
replace(CONTENT/'index.html', '<div id="tabs"', '<div id="workspace-tabs" class="workspace-tabs"></div><div id="tabs"')
replace(CONTENT/'index.html', '<button id="export"', '<button id="workbook-export" class="primary">Excel 내보내기</button><button id="export"')
replace(CONTENT/'index.html', 'accept=".json"', 'accept=".json,.xlsx"')
replace(CONTENT/'index.html', '<button id="load">불러오기</button>', '<button id="load">Excel 가져오기</button>')
replace(CONTENT/'index.html', '검사 · 엑셀 추출 ↗', '전체 자료 · 검사')
replace(CONTENT/'index.html', '하나의 프로젝트 · 하나의 HTML', '기획 작업실 · 콘텐츠 설계')
replace(CONTENT/'schemas.js', '일괄 생성용 레벨당 성장 비율', '원본 레벨당 성장 비율')
replace(CONTENT/'schemas.js', '성장 비율 수정 후 일괄 생성 버튼으로 반영하세요.', '성장 비율은 원본 참고값이며 실제 수치는 레벨별 강화 표에서 편집하세요.')
replace(CONTENT/'studio-core.js', "map.get(x)?.Row>=n.Row", "map.get(x)?.Row>n.Row")
replace(CONTENT/'app.js', '(()=>{', '(async()=>{')
replace(CONTENT/'app.js', "  let recovery='';", "  let recovery='';\n  if(window.desktopContent) await window.desktopContent.restore(storageKey);")
replace(CONTENT/'app.js', 'localStorage.setItem(storageKey,JSON.stringify(project));', 'localStorage.setItem(storageKey,JSON.stringify(project));if(window.desktopContent)window.desktopContent.autosave(project);')
replace(CONTENT/'app.js', 'function download(name,data,type){', 'async function download(name,data,type){if(window.desktopContent)return await window.desktopContent.exportFile(name,data,type);')
replace(CONTENT/'app.js', 'function save(){document.activeElement?.blur();download(', 'async function save(){try{document.activeElement?.blur();await download(')
replace(CONTENT/'app.js', "toast('전체 프로젝트 JSON을 다운로드했습니다.');}", "toast('전체 프로젝트 JSON을 저장했습니다.');}catch(e){alert('저장 실패: '+e.message);}}")
replace(CONTENT/'app.js', "$('export-current').onclick=()=>", "$('export-current').onclick=async()=>")
replace(CONTENT/'app.js', "download(table+'.xlsx'", "await download(table+'.xlsx'")
replace(CONTENT/'app.js', 'function allExport(csv){', 'async function allExport(csv){')
replace(CONTENT/'app.js', "download(csv?'outgame-csv.zip'", "await download(csv?'outgame-csv.zip'")
replace(CONTENT/'app.js', "if(Math.hypot(e.clientX-d.x,e.clientY-d.y)<8)return;", "if(Math.hypot(e.clientX-d.x,e.clientY-d.y)<8){selected=d.index;renderTree();return;}")
import re
app_path = CONTENT/'app.js'
app = app_path.read_text(encoding='utf-8')
app = re.sub(r'  function renderOverview\(\)\{.*?(?=  const titleFor=)', "  function renderOverview(){ $('content').innerHTML=PlannerScope.overview(project); }\n", app, flags=re.S)
app = app.replace('S[table].fields.map(f=>fieldMarkup(f,r))', 'S[table].fields.filter(f=>!f.hidden).map(f=>fieldMarkup(f,r))')
app = app.replace("if(f.type==='textarea')return", "if(table==='Tutorial'&&f.key==='Instruction')return TutorialRich.field(v);\n    if(f.type==='textarea')return")
app = app.replace("const el=e.target;if(el.id==='search')", "const el=e.target;if(table==='Tutorial'&&el.dataset.field==='Instruction'){row().Instruction=el.value;changed();TutorialRich.update(el.value);}if(el.id==='search')")
app = app.replace('  function renderTable(){', "  function renderTable(){\n    if(['RewardGroup','Attendance'].includes(table))return RewardGrid.render({project,table,changed,render,toast});")
app = app.replace('  function renderTable(){', "  function renderTable(){\n    if(['Unit','UnitUpgrade'].includes(table))return UnitOverview.render({project,changed,render,toast});")
app = app.replace('nav.tables.map(name=>', "nav.tables.filter(name=>!(section==='units'&&name==='UnitUpgrade')).map(name=>")
app = app.replace("else renderTable();", "else renderTable();PlannerScope.renderTabs(section);")
app = app.replace("const loaded=C.migrate(JSON.parse(await file.text()));", "const imported=file.name.toLowerCase().endsWith('.xlsx')?await window.desktopContent.importWorkbook(file,project):null;const loaded=imported?imported.project:C.migrate(JSON.parse(await file.text()));")
app = app.replace("toast('전체 프로젝트를 불러왔습니다.');", "if(imported)window.StudioUI.loadProject(loaded,imported.names);toast('자료를 불러왔습니다.');")
app = app.replace("window.addEventListener('beforeunload'", "window.StudioUI={openWorkspace(kind){PlannerScope.workspace=kind;section=kind==='enhancement'?'units':kind==='rewards'?'rewards':'overview';table=kind==='enhancement'?'UnitUpgrade':'RewardGroup';selected=0;render();},loadProject(p,names=[]){project=C.shape(p);const first=names[0];if(first){table=first;section=N.find(n=>n.tables.includes(first))?.id||'overview';PlannerScope.workspace=['ResearchNode','ResearchTree','Unit','UnitUpgrade','UnitMilestone'].includes(first)?'enhancement':['Item','RewardGroup','Attendance','AttendancePlan'].includes(first)?'rewards':'settings';}selected=0;changed();render();}};document.addEventListener('click',e=>{const b=e.target.closest('[data-workspace-section]');if(b){section=b.dataset.workspaceSection;table=section==='units'?'UnitUpgrade':section==='research'?'ResearchNode':section==='attendance'?'Attendance':'RewardGroup';selected=0;render();}});$('workbook-export').onclick=async()=>{try{document.activeElement?.blur();const result=await window.desktopContent.exportWorkbook(project,PlannerScope.workspace,table);toast('Excel 저장 완료 · 왼쪽 내보낸 파일에서 확인');}catch(e){alert(e.message);}};window.addEventListener('beforeunload'")
app = app.replace("Object.keys(S).concat('ResearchLevel')", "[...PlannerScope.active,'ResearchLevel']")
app = app.replace('영구 강화', '영구강화')
app = app.replace('label:v}', 'label:f.labels?.[v]||v}')
app = app.replace("const t=C.tables(project);await download(table+'.xlsx',RewardCore.xlsx(table,t[table])", "const t=C.tables(project),sheet=UnitKeys.sheetFor[table]||AttendanceEvents.sheetFor[table]||table;await download(sheet+'.xlsx',RewardCore.xlsx(sheet,t[sheet])")
app = app.replace('window.StudioUI={openWorkspace', 'window.StudioUI={snapshot:()=>C.clone(project),openWorkspace')
app = app.replace('const first=names[0];', "const first=names.includes('UnitUpgrade')?'UnitUpgrade':names.includes('RewardGroup')?'RewardGroup':names[0];")
app = app.replace("section=kind==='enhancement'?'units':kind==='rewards'?'rewards':'overview';table=kind==='enhancement'?'UnitUpgrade':'RewardGroup'", "section=kind==='enhancement'?'units':kind==='rewards'?'attendance':'overview';table=kind==='enhancement'?'UnitUpgrade':'Attendance'")
app = app.replace("names.includes('RewardGroup')?'RewardGroup'", "names.includes('Attendance')?'Attendance':names.includes('ResearchNode')?'ResearchNode'")
app = app.replace('출석 일차 · 일정 · 확률', '출석 일차')
app = app.replace('statuses:project.meta.statuses', "statuses:Object.fromEntries(Object.entries(project.meta.statuses).filter(([name])=>PlannerScope.active.has(name)))")
app_path.write_text(app,encoding='utf-8')
core_path=CONTENT/'studio-core.js'
core=core_path.read_text(encoding='utf-8')
core=core.replace("out[name]=[schema.fields.map(f=>f.key),...p.tables[name].map(r=>schema.fields.map(f=>{", "const exportFields=schema.fields.filter(f=>!f.hidden&&f.export!==false);out[name]=[exportFields.map(f=>f.key),...p.tables[name].map(r=>exportFields.map(f=>{")
core_path.write_text(core,encoding='utf-8')
html_path=CONTENT/'index.html'
html=html_path.read_text(encoding='utf-8').replace('영구 강화','영구강화')
html=re.sub(r'<ol>.*?</ol>', '<ol><li>왼쪽에서 기능을 선택하고 위쪽 세부 항목을 고릅니다.</li><li>노드·수치·보상 수량을 편집하면 자동 저장됩니다.</li><li>프로젝트 저장으로 JSON을 보관하고, 검사 · 엑셀 추출로 현재 기능의 표를 내보냅니다.</li></ol>',html,flags=re.S)
html=html.replace('유저 저장 설계는 구조 참고용이며 실제 계정을 편집하지 않습니다. 이벤트·일반 상품·가챠 콘솔 원본은 조회하지 않았으므로 빈 표에서 기획합니다.','현재 프로젝트에 있는 기능만 편집합니다. 실제 유저 계정이나 서버에는 직접 적용하지 않습니다.')
html_path.write_text(html,encoding='utf-8')
print('Integrated source:', APP)

# Mission data extension.
replace(CONTENT/'index.html', '<script src="attendance-events.js"></script>', '<script src="attendance-events.js"></script><script src="event-catalog.js"></script><script src="mission-events.js"></script>')
replace(CONTENT/'app.js', "['Item','RewardGroup','Attendance','AttendancePlan'].includes(first)", "['Item','RewardGroup','Attendance','AttendancePlan',...MissionEvents.names].includes(first)")
replace(CONTENT/'app.js', "section==='attendance'?'Attendance':'RewardGroup'", "section==='attendance'?'Attendance':section==='missions'?'Missions':section==='dataBindings'?'DataBindings':'RewardGroup'")
replace(CONTENT/'index.html', '<script src="app.js"></script>', '<script src="data-bindings-ui.js"></script><script src="app.js"></script>')
replace(CONTENT/'app.js', "  function renderTable(){", "  function renderTable(){\n    if(table==='DataBindings')return DataBindingsUI.render({project,changed,render,toast});")
replace(CONTENT/'app.js', "if(f.type==='enum')return f.values.map", "if(table==='DataBindings'&&f.key==='ValueSource')return f.values.filter(v=>EVENT_CATALOG.hooks.find(h=>h.HookKey===r.HookKey)?.Sources.split(';').includes(v)).map(v=>({value:v,label:f.labels?.[v]||v}));\n    if(f.type==='enum')return f.values.map")
