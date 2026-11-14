// 뒤끝 콘솔 업로드 흐름 기록용. 사람이 직접 업로드·적용하는 동안 클릭과 API 요청을 남긴다.
// 인증 헤더·쿠키·토큰은 기록하지 않는다. 브라우저 창을 닫으면 끝난다.
import fs from 'node:fs';
import path from 'node:path';
import { openConsole, projectRoot } from './console-browser.mjs';

const outDir = path.join(projectRoot, 'outputs', 'backnd-chart-upload', 'record-' + new Date().toISOString().replace(/[:.]/g, '-'));
fs.mkdirSync(outDir, { recursive: true });
const logFile = path.join(outDir, 'events.jsonl');
const log = e => fs.appendFileSync(logFile, JSON.stringify({ t: new Date().toISOString(), ...e }) + '\n');
const SECRET = /authorization|cookie|token|secret|password|signature|otp|code|auth/i;
const redact = obj => Object.fromEntries(Object.entries(obj ?? {}).map(([k, v]) => [k, SECRET.test(k) ? '<redacted>' : v]));
const redactBody = s => (s ?? '').replace(/("?[\w-]*(token|password|secret|otp|code|auth|email|phone|name)[\w-]*"?\s*[:=]\s*)"[^"]*"/gi, '$1"<redacted>"').slice(0, 20000);

const context = await openConsole();
await context.exposeBinding('__ggdLog', (_src, e) => log(e));
await context.addInitScript(() => {
  const describe = el => {
    const parts = [];
    for (let n = el, i = 0; n && n.nodeType === 1 && i < 5; n = n.parentElement, i++)
      parts.push(n.tagName.toLowerCase() + (n.id ? '#' + n.id : '') + (n.className && typeof n.className === 'string' ? '.' + n.className.trim().split(/\s+/).slice(0, 3).join('.') : ''));
    // 입력칸 값은 절대 읽지 않는다(비밀번호·인증번호).
    const isField = /^(input|textarea|select)$/i.test(el.tagName) || el.isContentEditable;
    const text = isField ? '<' + el.tagName.toLowerCase() + ' type=' + (el.type || '') + '>' : (el.innerText || el.getAttribute?.('aria-label') || '');
    return { text: text.trim().slice(0, 80), path: parts.join(' < ') };
  };
  addEventListener('click', e => { if (!location.pathname.includes('/login')) window.__ggdLog({ kind: 'click', url: location.href, ...describe(e.target) }); }, true);
  addEventListener('change', e => { if (e.target.type === 'file') window.__ggdLog({ kind: 'file', url: location.href, files: [...e.target.files].map(f => f.name), ...describe(e.target) }); }, true);
});
// 로그인·인증 서버와 분석 수집 요청은 아예 기록하지 않는다.
const SKIP = /consoleauth\.|consoleapi\.thebackend\.io\/account\/|amplitude\.com|contents\.cdn\.thebackend\.io|google|sentry/;
context.on('request', r => {
  if (!['xhr', 'fetch', 'document'].includes(r.resourceType()) || SKIP.test(r.url())) return;
  const ct = r.headers()['content-type'] ?? '';
  log({ kind: 'request', method: r.method(), url: r.url(), type: r.resourceType(), headers: redact(r.headers()),
    body: ct.includes('multipart') ? '<multipart ' + (r.postDataBuffer()?.length ?? 0) + ' bytes> ' + redactBody(r.postData()?.replace(/[^\x20-\x7e가-힣\r\n]/g, '').slice(0, 2000)) : redactBody(r.postData()) });
});
context.on('response', async r => {
  const req = r.request();
  if (!['xhr', 'fetch'].includes(req.resourceType()) || SKIP.test(r.url())) return;
  let body = '';
  try { body = redactBody(await r.text()); } catch {}
  log({ kind: 'response', method: req.method(), url: r.url(), status: r.status(), body });
});

const page = context.pages()[0] ?? await context.newPage();
await page.goto('https://console.thebackend.io/');
console.log('기록 중: ' + logFile);
console.log('로그인 → GaeGGULDefence → 차트에서 업로드·적용을 한 번 진행한 뒤 브라우저 창을 닫으세요.');
await new Promise(resolve => context.on('close', resolve));
console.log('기록 완료: ' + logFile);
