// 뒤끝 콘솔 차트 업로드·적용 명령어. 공개 업로드 API가 없어 콘솔 웹을 직접 사용한다.
//   node Tools/backnd-mcp/chart-upload.mjs login
//   node Tools/backnd-mcp/chart-upload.mjs status
//   node Tools/backnd-mcp/chart-upload.mjs upload --env DEV --chart SkillBalance --file Data/Balance/SkillBalance.xlsx
//   node Tools/backnd-mcp/chart-upload.mjs upload --env LIVE ... --confirm-live
// 엑셀 해석은 콘솔 페이지 자신의 코드가 한다(파일 입력에 파일을 넣음). 적용과 확인은 콘솔이 쓰는 API를 페이지 안에서 호출한다.
// 결과는 stdout 마지막 줄의 JSON 한 줄이다. 종료 코드 0 성공, 1 실패, 2 로그인 필요.
import fs from 'node:fs';
import path from 'node:path';
import { openConsole, projectRoot } from './console-browser.mjs';

const CONSOLE = 'https://console.thebackend.io';
const API = 'https://consoleapi.thebackend.io';
const PROJECT_NAME = 'GaeGGULDefence';
const UPLOAD_TIMEOUT_MS = 120_000;

class Fail extends Error { constructor(code, message, extra) { super(message); this.code = code; this.extra = extra; } }

function parseArgs(argv) {
  const [command, ...rest] = argv;
  const opts = {};
  for (let i = 0; i < rest.length; i++) {
    const a = rest[i];
    if (!a.startsWith('--')) throw new Fail('BAD_ARGS', '알 수 없는 인자: ' + a);
    const key = a.slice(2);
    if (key === 'confirm-live') opts.confirmLive = true;
    else if (key === 'headed') opts.headed = true;
    else opts[key] = rest[++i];
  }
  return { command, opts };
}

// 콘솔 페이지의 로그인 쿠키로 콘솔 API를 호출한다. 토큰은 브라우저 밖으로 나오지 않는다.
const api = (page, method, apiPath) => page.evaluate(async ([base, m, p]) => {
  const auth = decodeURIComponent((document.cookie.match(/(?:^|; )authorization=([^;]*)/) || [])[1] || '');
  const r = await fetch(base + p, { method: m, headers: { Authorization: auth }, signal: AbortSignal.timeout(30_000) });
  const text = await r.text();
  return { status: r.status, body: text };
}, [API, method, apiPath]);

async function apiJson(page, method, apiPath) {
  const r = await api(page, method, apiPath);
  if (r.status < 200 || r.status >= 300) throw new Fail('API_ERROR', `${method} ${apiPath.replace(/^\/[0-9a-f]{40,}/, '/{project}')} → ${r.status} ${r.body.slice(0, 300)}`);
  return r.body ? JSON.parse(r.body) : null;
}

// 콘솔을 열고 GaeGGULDefence 프로젝트 페이지까지 간다. 로그인이 풀려 있으면 LOGIN_REQUIRED.
async function openProject({ headless }) {
  const context = await openConsole({ headless });
  try {
    const page = context.pages()[0] ?? await context.newPage();
    await page.goto(CONSOLE + '/ko/');
    await page.waitForURL(u => /\/project\/[0-9a-f]+|\/login/.test(u.pathname), { timeout: 30_000 }).catch(() => {});
    if (!/\/project\/[0-9a-f]+/.test(new URL(page.url()).pathname)) throw new Fail('LOGIN_REQUIRED', '뒤끝 콘솔 로그인이 필요합니다. `chart-upload.mjs login`을 실행하세요.');
    const projectId = await findProjectId(page);
    return { context, page, projectId };
  } catch (e) { await context.close(); throw e; }
}

async function findProjectId(page) {
  const all = await apiJson(page, 'GET', '/project/v4.3');
  const rows = Object.values(all ?? {}).flatMap(region => region?.rows ?? []);
  const project = rows.find(r => r.title === PROJECT_NAME);
  if (!project) throw new Fail('PROJECT_NOT_FOUND', `콘솔에서 ${PROJECT_NAME} 프로젝트를 찾지 못했습니다.`);
  return project.clientAppId;
}

// 최상위 차트와 폴더 안 차트를 모두 본다.
async function listCharts(page, projectId) {
  const charts = (await apiJson(page, 'GET', `/${projectId}/project/chart/v3.1`)).rows.map(c => ({ ...c, folderId: null }));
  const folders = (await apiJson(page, 'GET', `/${projectId}/project/chart/v3/folders`)).rows;
  for (const f of folders) {
    const inner = (await apiJson(page, 'GET', `/${projectId}/project/chart/v3.1/folder/${f.id}`)).rows;
    charts.push(...inner.map(c => ({ ...c, folderId: f.id })));
  }
  return charts;
}

// 최신 파일이 앞에 온다(2026-11-14 확인). 새 업로드는 항상 첫 페이지에 있다.
const listFiles = async (page, projectId, chartId) =>
  (await apiJson(page, 'GET', `/${projectId}/project/chart/v2/${chartId}?limit=20&offset=0`)).rows;

async function readStoredRows(page, projectId, chartId, fileId) {
  const rows = [];
  for (;;) {
    const r = await apiJson(page, 'GET', `/${projectId}/project/chart/storage/v1/${chartId}/${fileId}?limit=200&offset=${rows.length}`);
    rows.push(...r.rows);
    if (rows.length >= r.totalCount || r.rows.length === 0) return rows;
  }
}

async function login() {
  const context = await openConsole({ headless: false });
  const page = context.pages()[0] ?? await context.newPage();
  await page.goto(CONSOLE + '/ko/');
  console.error('브라우저 창에서 뒤끝 콘솔에 로그인하세요. 프로젝트 화면이 열리면 자동으로 닫힙니다.');
  await page.waitForURL(u => /\/project\/[0-9a-f]+/.test(u.pathname), { timeout: 10 * 60_000 });
  await context.close();
  return { ok: true, command: 'login' };
}

async function status() {
  const { context, page, projectId } = await openProject({ headless: true });
  try {
    const charts = await listCharts(page, projectId);
    return { ok: true, command: 'status', project: PROJECT_NAME, chartCount: charts.length, charts: charts.map(c => c.chartName) };
  } finally { await context.close(); }
}

async function upload(opts) {
  const env = opts.env;
  if (!['DEV', 'LIVE'].includes(env)) throw new Fail('BAD_ARGS', '--env는 DEV 또는 LIVE여야 합니다.');
  if (env === 'LIVE' && !opts.confirmLive) throw new Fail('LIVE_NOT_CONFIRMED', 'LIVE 업로드는 출시 앱에 바로 반영됩니다. --confirm-live를 함께 지정해야 합니다.');
  if (!opts.chart || /^(DEV|LIVE|COMMON)_/.test(opts.chart)) throw new Fail('BAD_ARGS', '--chart에는 접두어 없는 내용 이름을 넣습니다. 예: SkillBalance');
  if (!opts.file) throw new Fail('BAD_ARGS', '--file이 필요합니다.');
  const file = path.resolve(projectRoot, opts.file);
  if (!fs.existsSync(file)) throw new Fail('FILE_NOT_FOUND', '파일이 없습니다: ' + file);
  if (!/\.(xlsx|xls|csv)$/i.test(file)) throw new Fail('BAD_ARGS', '차트 파일은 xlsx, xls, csv만 됩니다.');
  const chartName = `${env}_${opts.chart}`;

  const { context, page, projectId } = await openProject({ headless: !opts.headed });
  try {
    const chart = (await listCharts(page, projectId)).find(c => c.chartName === chartName);
    if (!chart) throw new Fail('CHART_NOT_FOUND', `${chartName} 차트가 콘솔에 없습니다. 콘솔에서 먼저 만드세요.`);
    const before = new Set((await listFiles(page, projectId, chart.id)).map(f => f.id));

    // 1) 업로드: 콘솔 화면의 파일 입력에 넣으면 콘솔 코드가 검사·변환 후 POST한다.
    const route = chart.folderId ? `folder/${chart.folderId}/${chart.id}` : `${chart.id}`;
    await page.goto(`${CONSOLE}/ko/project/${projectId}/baseChart/${route}`);
    const input = page.locator('input[type=file][accept*=".xlsx"]');
    await input.waitFor({ state: 'attached', timeout: 30_000 });
    const posted = page.waitForRequest(r => r.method() === 'POST' && r.url().includes(`/project/chart/storage/v1.1/${chart.id}`), { timeout: 60_000 })
      .catch(() => null);
    await input.setInputFiles(file);
    const request = await posted;
    if (!request) {
      // 콘솔 검사 오류는 '안내' 모달로 뜬다. 공지 팝업은 빼고 그 모달만 보고한다.
      const modals = await page.locator('.ui.modal, [role=dialog]').allInnerTexts().catch(() => []);
      const message = modals.find(t => t.trim().startsWith('안내')) ?? modals[0] ?? '';
      throw new Fail('UPLOAD_REJECTED', '콘솔이 파일을 거절했습니다. ' + message.replace(/^안내\s*/, '').replace(/\s*확인[\s\S]*$/, '').trim().slice(0, 300));
    }
    const response = await request.response();
    if (!response || response.status() >= 300)
      throw new Fail('UPLOAD_FAILED', `업로드 요청 실패: ${response?.status()} ${(await response?.text().catch(() => '')).slice(0, 300)}`);
    const sentRows = JSON.parse(request.postData()).data;

    // 2) 서버 처리 완료 대기: 새 파일이 생기고 isCreating이 n이 될 때까지.
    let created;
    for (const start = Date.now(); Date.now() - start < UPLOAD_TIMEOUT_MS; await page.waitForTimeout(1500)) {
      created = (await listFiles(page, projectId, chart.id)).find(f => !before.has(f.id) && f.chartFileName === path.basename(file));
      if (created && created.isCreating === 'n') break;
    }
    if (!created || created.isCreating !== 'n') throw new Fail('UPLOAD_TIMEOUT', '업로드한 파일의 서버 처리가 끝나지 않았습니다. 콘솔에서 확인하세요.');

    // 3) 저장된 내용이 콘솔이 보낸 내용과 같은지 확인한 뒤에만 적용한다.
    const stored = await readStoredRows(page, projectId, chart.id, created.id);
    const norm = rows => rows.map(r => JSON.stringify(Object.keys(r).sort().map(k => [k, String(r[k])]))).sort();
    const a = norm(sentRows), b = norm(stored);
    if (a.length !== b.length || a.some((x, i) => x !== b[i]))
      throw new Fail('STORED_MISMATCH', `서버에 저장된 행이 보낸 행과 다릅니다(보냄 ${a.length}, 저장 ${b.length}). 적용하지 않았습니다.`, { uploadedFileId: created.id });

    // 4) 적용
    await apiJson(page, 'PUT', `/${projectId}/project/chart/v2/${chart.id}/${created.id}`);
    let applied = false;
    for (let i = 0; i < 5 && !applied; i++, await page.waitForTimeout(2000))
      applied = (await listCharts(page, projectId)).find(c => c.id === chart.id)?.selectedChartFileId === created.id;
    if (!applied)
      throw new Fail('APPLY_NOT_CONFIRMED', '적용 요청은 성공했지만 선택 파일 변경을 확인하지 못했습니다. 적용됐을 수 있으니 다시 올리지 말고 콘솔에서 확인하세요.', { uploadedFileId: created.id });

    return {
      ok: true, command: 'upload', env, chartName, chartId: chart.id, file: path.relative(projectRoot, file).replace(/\\/g, '/'),
      fileId: created.id, previousFileId: chart.selectedChartFileId, rowCount: stored.length,
    };
  } finally { await context.close(); }
}

const command = process.argv[2];
try {
  const { opts } = parseArgs(process.argv.slice(2));
  const run = { login, status, upload }[command];
  if (!run) throw new Fail('BAD_ARGS', '명령: login | status | upload');
  console.log(JSON.stringify(await run(opts)));
} catch (e) {
  const code = e instanceof Fail ? e.code : 'ERROR';
  console.log(JSON.stringify({ ok: false, command, code, message: e.message, ...(e.extra ?? {}) }));
  process.exit(code === 'LOGIN_REQUIRED' ? 2 : 1);
}
