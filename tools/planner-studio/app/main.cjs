'use strict';
const { app, BrowserWindow, ipcMain, dialog, shell, protocol, net, session } = require('electron');
const path = require('node:path');
const fs = require('node:fs');
const { pathToFileURL } = require('node:url');
const { StudioStore, atomic } = require('./store.cjs');
const schema = require('./schema.cjs');

app.setName('GGD-Planner');
const argument = name => process.argv.find(a => a.startsWith(name + '='))?.slice(name.length + 1);
const smokePath = argument('--smoke');
const contentSmokePath = argument('--content-smoke');
const workspace = path.resolve(argument('--workspace') || app.getPath('userData'));
app.setPath('userData', workspace);
app.setPath('sessionData', path.join(workspace, 'browser-cache'));
protocol.registerSchemesAsPrivileged([{ scheme: 'ggd', privileges: { standard: true, secure: true, supportFetchAPI: true } }]);
let window, store, dirty = false, closing = false;
const gotLock = app.requestSingleInstanceLock();
if (!gotLock) app.quit();
else {
  app.on('second-instance', () => { if (window) { window.restore(); window.focus(); } });
  app.whenReady().then(start).catch(error => {
    if (smokePath || contentSmokePath) atomic(smokePath || contentSmokePath, JSON.stringify({ ok: false, error: error.stack }, null, 2));
    else dialog.showErrorBox('기획 작업실을 열 수 없습니다', error.message);
    app.exit(1);
  });
}
app.on('window-all-closed', () => app.quit());

async function start() {
  const bundledFolder = app.isPackaged ? path.join(path.dirname(process.execPath), '자료') : path.join(__dirname, 'seeds');
  store = new StudioStore(workspace, path.join(__dirname, 'seeds'), bundledFolder);
  const uiFolder = path.join(__dirname, 'ui');
  protocol.handle('ggd', request => {
    const url = new URL(request.url);
    const pathname = decodeURIComponent(url.pathname);
    const file = path.resolve(uiFolder, '.' + pathname);
    if (url.host !== 'app' || !file.startsWith(uiFolder + path.sep) || !['.html', '.css', '.js', '.svg'].includes(path.extname(file))) return new Response('Not found', { status: 404 });
    return net.fetch(pathToFileURL(file).toString());
  });
  session.defaultSession.setPermissionRequestHandler((_wc, _permission, callback) => callback(false));
  session.defaultSession.setPermissionCheckHandler(() => false);
  window = new BrowserWindow({ width: 1540, height: 990, minWidth: 1180, minHeight: 760,
    backgroundColor: '#f7f8fb', title: 'GGD 기획 작업실', show: !smokePath && !contentSmokePath && argument('--qa') !== '1', autoHideMenuBar: true,
    webPreferences: { preload: path.join(__dirname, 'preload.cjs'), contextIsolation: true, nodeIntegration: false, sandbox: true, spellcheck: false, backgroundThrottling: !smokePath && !contentSmokePath && argument('--qa') !== '1' } });
  window.webContents.setWindowOpenHandler(() => ({ action: 'deny' }));
  window.webContents.on('will-navigate', event => event.preventDefault());
  window.on('close', event => {
    if (!closing && dirty && !smokePath && dialog.showMessageBoxSync(window, { type: 'question', title: '아직 저장하지 않은 실험', message: '수정한 값이 로컬 실험으로 저장되지 않았습니다.', buttons: ['계속 작업', '저장하지 않고 닫기'], defaultId: 0, cancelId: 0 }) === 0) event.preventDefault();
  });
  const handler = (name, action) => ipcMain.handle('studio:' + name, async (event, payload) => {
    if (event.sender !== window.webContents || event.senderFrame !== window.webContents.mainFrame || !event.senderFrame.url.startsWith('ggd://app/')) throw new Error('지원하지 않는 요청입니다.');
    try { return { ok: true, data: await action(payload || {}) }; }
    catch (error) { return { ok: false, error: error.message }; }
  });
  require('./content-service.cjs').register({ handler, workspace, store, atomic });
  handler('state', () => store.getState());
  handler('save-guide', p => store.saveGuide(p.uid, p.guide));
  handler('settings', () => store.getSettings());
  handler('library', () => store.library());
  handler('import-library', p => { const result = store.importLibrary(p.name, p.sha256); dirty = false; return result; });
  handler('use-bundled-folder', () => store.useBundledFolder());
  handler('choose-excel-folder', async () => {
    const choice = await dialog.showOpenDialog(window, { title: '엑셀 폴더 선택', defaultPath: store.getSettings().excelFolder || undefined, properties: ['openDirectory', 'createDirectory'] });
    return choice.canceled ? null : store.setExcelFolder(choice.filePaths[0]);
  });
  handler('open-excel-folder', async () => {
    const folder = store.getSettings().excelFolder;
    if (!folder) throw new Error('설정에서 엑셀 폴더를 먼저 선택하세요.');
    const error = await shell.openPath(folder); if (error) throw new Error(error); return true;
  });
  handler('dirty', p => { dirty = Boolean(p.dirty); return dirty; });
  handler('validate', p => store.validate(p.values));
  handler('search', p => store.search(String(p.query || '').slice(0, 200), p.domain || 'all'));
  handler('preview', p => {
    const check = store.validate(p.values);
    if (!check.ok) return { errors: check.errors, before: [], after: [] };
    const context = store.contexts()[p.domain];
    return { errors: [], before: schema.preview(p.domain, store.baselineValues(), context), after: schema.preview(p.domain, check.values, context) };
  });
  handler('save', p => { const result = store.save(p.title, p.note, p.values); dirty = false; return result; });
  handler('load', p => { const result = store.load(p.id); dirty = false; return result; });
  handler('reset', () => { const result = store.reset(); dirty = false; return result; });
  handler('import', async p => {
    const choice = await dialog.showOpenDialog(window, { title: '자료 가져오기', defaultPath: store.getSettings().excelFolder || undefined, properties: ['openFile'], filters: [{ name: '기획 자료', extensions: ['xlsx', 'ggdplan'] }] });
    if (choice.canceled) return null;
    const file = choice.filePaths[0];
    if (fs.statSync(file).size > 32 * 1024 * 1024) throw new Error('지원 파일 크기를 초과합니다.');
    const bytes = fs.readFileSync(file);
    let includeGuides = false;
    if (path.extname(file).toLowerCase() === '.ggdplan' && Object.keys(JSON.parse(bytes.toString('utf8')).guides || {}).length) {
      const choice = await dialog.showMessageBox(window, { type: 'question', title: '함께 받은 항목 설명',
        message: '실험에 작성자가 수정한 설명이 있습니다. 같은 항목의 내 설명도 바꿀까요?', buttons: ['내 설명 유지', '받은 설명 적용', '가져오기 취소'], defaultId: 0, cancelId: 2 });
      if (choice.response === 2) return null;
      includeGuides = choice.response === 1;
    }
    const result = path.extname(file).toLowerCase() === '.ggdplan' ? store.importBundle(bytes, includeGuides) : store.importSource(p.domain, bytes, path.basename(file));
    dirty = false;
    return result;
  });
  handler('export', p => store.export(p.domain, p.values, p.title, p.note));
  handler('share', async p => {
    const choice = await dialog.showSaveDialog(window, { title: '다른 PC에서 열 수 있는 실험 묶음 저장', defaultPath: path.join(workspace, '내실험.ggdplan'), filters: [{ name: 'GGD 실험 묶음', extensions: ['ggdplan'] }] });
    if (choice.canceled) return null;
    atomic(choice.filePath, JSON.stringify(store.bundle(p.values, p.title, p.note), null, 2));
    return { file: choice.filePath };
  });
  handler('open-exports', async () => { const folder = store.exportFolder(); if (store.excelFolder() && !fs.existsSync(store.excelFolder())) throw new Error('설정에서 엑셀 폴더를 다시 선택하세요.'); fs.mkdirSync(folder, { recursive: true }); const error = await shell.openPath(folder); if (error) throw new Error(error); return true; });
  handler('open-console', async () => { await shell.openExternal('https://console.thebackend.io/'); return true; });
  await window.loadURL('ggd://app/index.html');
  if (smokePath) await smoke();
  if (contentSmokePath) await require('./content-smoke.cjs')({window, app, store, workspace, resultPath:contentSmokePath, atomic});
}

async function smoke() {
  const checks = [];
  const originalOpenDialog = dialog.showOpenDialog;
  const originalMessageBox = dialog.showMessageBox;
  const excelFolder = path.join(workspace, 'excel-library');
  const dialogCalls = [];
  try {
    fs.mkdirSync(excelFolder, { recursive: true });
    fs.copyFileSync(path.join(__dirname, 'seeds', 'EndlessBalance.xlsx'), path.join(excelFolder, 'FolderEndless.xlsx'));
    const guideBundle = store.bundle(store.current.values, '받은 실험', '설명 전달 검사');
    guideBundle.guides = { 'endless.StartSeconds': { description: '준비모래시계: 다른 기획자가 공유한 설명', example: '240초 = 4분' } };
    const sharedFile = path.join(workspace, 'shared.ggdplan');
    atomic(sharedFile, JSON.stringify(guideBundle));
    const choices = [
      { canceled: true, filePaths: [] },
      { canceled: false, filePaths: [excelFolder] },
      { canceled: false, filePaths: [path.join(excelFolder, 'FolderEndless.xlsx')] },
      ...[0, 1, 2].map(() => ({ canceled: false, filePaths: [sharedFile] }))
    ];
    const guideChoices = [2, 0, 1];
    dialog.showMessageBox = async () => {
      if (!guideChoices.length) throw new Error('Unexpected message dialog during smoke');
      return { response: guideChoices.shift() };
    };
    dialog.showOpenDialog = async (_window, options) => {
      dialogCalls.push(options);
      if (!choices.length) throw new Error('Unexpected native dialog during smoke');
      return choices.shift();
    };
    for (let attempt = 0; attempt < 100; attempt++) {
      if (await window.webContents.executeJavaScript('Boolean(window.__studioReady)')) break;
      await new Promise(resolve => setTimeout(resolve, 100));
    }
    if (!await window.webContents.executeJavaScript('Boolean(window.__studioReady)')) throw new Error('UI ready timeout');
    checks.push('planner starts without a Unity checkout');
    const uiChecks = await window.webContents.executeJavaScript('window.runStudioSmoke()');
    checks.push(...uiChecks);
    if (choices.length || guideChoices.length || !dialogCalls[0].properties.includes('openDirectory') || dialogCalls[2].defaultPath !== excelFolder) throw new Error('Configured folder or description choice was not used by the import dialog');
    if (new StudioStore(workspace, path.join(__dirname, 'seeds')).getSettings().excelFolder !== excelFolder) throw new Error('Excel folder preference did not persist');
    checks.push('folder picker and import dialog use the persisted Excel folder');
    if (!new StudioStore(workspace, path.join(__dirname, 'seeds')).fieldDefinitions().find(field => field.uid === 'endless.StartSeconds').description.includes('준비모래시계')) throw new Error('Description edit did not persist');
    checks.push('edited field descriptions survive reopening the store');
    const exportResult = store.export('endless', { ...store.current.values, 'endless.StartSeconds': 180 }, '배포 검사', '별도 검사 폴더');
    if (exportResult.manifest.applicationStatus !== 'not_verified') throw new Error('Incorrect deployment status');
    if (path.dirname(exportResult.folder) !== path.join(excelFolder, '내보내기')) throw new Error('Export did not use the configured folder');
    checks.push('real XLSX export and manifest, no live deployment claim');
    await window.webContents.executeJavaScript('window.finishStudioSmoke()');
    window.setSize(1540, 990);
    await window.webContents.executeJavaScript('new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve)))');
    await new Promise(resolve => setTimeout(resolve, 400));
    const screenshot = await window.webContents.capturePage();
    atomic(smokePath.replace(/\.json$/i, '.png'), screenshot.toPNG());
    window.setSize(1180, 760);
    await window.webContents.executeJavaScript('new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve)))');
    await window.webContents.executeJavaScript('window.checkStudioLayout()');
    checks.push('editor controls fit the minimum window size');
    atomic(smokePath.replace(/\.json$/i, '-compact.png'), (await window.webContents.capturePage()).toPNG());
    window.setSize(1540, 990);
    await window.webContents.executeJavaScript('window.showStudioSettings()');
    await window.webContents.executeJavaScript('new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve)))');
    atomic(smokePath.replace(/\.json$/i, '-settings.png'), (await window.webContents.capturePage()).toPNG());
    await window.webContents.executeJavaScript('window.showStudioGuide()');
    await window.webContents.executeJavaScript('new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve)))');
    atomic(smokePath.replace(/\.json$/i, '-guide.png'), (await window.webContents.capturePage()).toPNG());
    await window.webContents.executeJavaScript('document.getElementById("guideDialog").close(); window.showStudioLibrary()');
    await window.webContents.executeJavaScript('new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve)))');
    atomic(smokePath.replace(/\.json$/i, '-library.png'), (await window.webContents.capturePage()).toPNG());
    atomic(smokePath, JSON.stringify({ ok: true, checks, exportedFile: exportResult.file, excelFolder, workspace, nativeDialogsSimulated: true, electron: process.versions.electron }, null, 2));
    dialog.showOpenDialog = originalOpenDialog;
    dialog.showMessageBox = originalMessageBox;
    closing = true; app.exit(0);
  } catch (error) {
    dialog.showOpenDialog = originalOpenDialog;
    dialog.showMessageBox = originalMessageBox;
    atomic(smokePath, JSON.stringify({ ok: false, error: error.stack, checks }, null, 2));
    closing = true; app.exit(1);
  }
}
