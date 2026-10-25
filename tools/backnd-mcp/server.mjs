import fs from 'node:fs';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { StdioServerTransport } from '@modelcontextprotocol/sdk/server/stdio.js';
import { z } from 'zod';
import { projectRoot, tableKeys, readJson, localFile, validateProject, checkedTables, exportCharts, verifyBundle, compareRows, parseCsv, records } from './balance.mjs';
import { readLive, unity } from './unity.mjs';
import { readChartXlsx } from './xlsx.mjs';
import { verifyLiveBundle } from './live-bundle.mjs';

const server = new McpServer({ name: 'ggd-backnd', version: '0.1.0' });
const file = z.string().min(1).describe('Unity 프로젝트 기준 상대 파일 경로.');
const table = z.enum(Object.keys(tableKeys));
function register(name, description, inputSchema, handler, readOnly = true, openWorld = false) {
  server.registerTool(name, { description, inputSchema, annotations: {
    readOnlyHint: readOnly, destructiveHint: false, idempotentHint: readOnly, openWorldHint: openWorld,
  } }, async args => {
    try {
      const result = await handler(args);
      return { content: [{ type: 'text', text: JSON.stringify(result, null, 2) }] };
    } catch (error) {
      return { isError: true, content: [{ type: 'text', text: error.message }] };
    }
  });
}

register('backnd_status', '로컬 MCP 기능 및 Unity Pipeline 연결 상태. 인증정보는 반환하지 않습니다.', {}, async () => {
  let editor;
  try { editor = { available: true, details: await unity(['command', 'editor_status', '--project-path', projectRoot]) }; }
  catch (error) { editor = { available: false, reason: error.message }; }
  return { projectRoot, editor, capabilities: { unitBalanceValidation: true, unitChartExport: true, localComparison: true,
    sdkChartRead: 'Unity Pipeline 연결 + 뒤끝 로그인 필요', remoteChartWrite: false, createTable: false, gameplayIntegration: false } };
});

register('validate_unit_balance', '기획 작업실 outgame-project.json의 유닛/강화/보너스 키·수치·참조를 검사합니다. 입력을 수정하지 않습니다.', { projectFile: file }, ({ projectFile }) => {
  const result = validateProject(readJson(projectFile));
  return { valid: result.valid, issues: result.issues, charts: result.tables && Object.fromEntries(Object.entries(result.tables).map(([name, rows]) => [name, { rows: rows.length - 1, columns: rows[0] }])) };
});

register('export_unit_charts', '검증한 프로젝트를 차트별 단일 시트 XLSX 4개와 manifest로 생성합니다. format=csv도 선택할 수 있습니다. outputs/backnd-mcp/<releaseKey>에 저장하며 기존 폴더를 덮어쓰지 않습니다.', {
  projectFile: file, releaseKey: z.string().regex(/^[A-Za-z0-9][A-Za-z0-9_-]{0,63}$/), format: z.enum(['xlsx','csv']).default('xlsx'),
}, ({ projectFile, releaseKey, format }) => exportCharts(projectFile, releaseKey, projectRoot, format), false);

register('verify_chart_bundle', '생성한 XLSX/CSV 차트의 해시·행·열·중복 키 및 XLSX 단일 시트/수식 없음을 재검증합니다. 서버 적용 확인과는 다릅니다.', { directory: file }, ({ directory }) => verifyBundle(directory));

register('compare_unit_chart', '로컬 프로젝트와 단일 시트 XLSX, 콘솔 CSV 또는 SDK rows JSON을 키 기준으로 비교합니다. 추가/삭제/변경 내역을 반환합니다.', {
  projectFile: file, table, currentFile: file,
}, ({ projectFile, table, currentFile }) => {
  const { tables } = checkedTables(projectFile);
  const current = /\.xlsx$/i.test(currentFile) ? records(readChartXlsx(fs.readFileSync(localFile(currentFile))))
    : /\.csv$/i.test(currentFile) ? records(parseCsv(fs.readFileSync(localFile(currentFile), 'utf8'))) : readJson(currentFile);
  const rows = Array.isArray(current) ? current : current.rows;
  if (!Array.isArray(rows)) throw Error('비교 JSON은 행 배열 또는 {rows:[...]}여야 합니다.');
  return compareRows(table, tables[table], rows);
});

register('list_live_charts', '실행 중인 Unity의 기존 뒤끝 로그인 세션으로 적용된 차트 목록을 읽습니다. 자동 로그인 및 서버 쓰기는 하지 않습니다.', {}, () => readLive('list'), true, true);

register('read_live_chart', '차트 이름으로 현재 적용된 뒤끝 차트를 SDK에서 읽습니다. Unity Pipeline 연결과 기존 뒤끝 로그인이 필요합니다.', {
  chartName: z.string().min(1).max(200), offset: z.number().int().min(0).default(0), limit: z.number().int().min(1).max(200).default(50),
}, async ({ chartName, offset, limit }) => {
  const current = await readLive('read', chartName);
  return { chartName: current.chartName, selectedChartFileId: current.selectedChartFileId, totalRows: current.rows.length, offset, rows: current.rows.slice(offset, offset + limit) };
}, true, true);

register('compare_live_unit_chart', '전체 실서버 차트를 읽어 로컬 유닛 차트와 비교합니다. 차트명은 실제 콘솔 이름을 명시합니다. 서버 데이터는 변경하지 않습니다.', {
  projectFile: file, table, chartName: z.string().min(1).max(200),
}, async ({ projectFile, table, chartName }) => {
  const { tables } = checkedTables(projectFile);
  const current = await readLive('read', chartName);
  return { chartName, selectedChartFileId: current.selectedChartFileId, ...compareRows(table, tables[table], current.rows) };
}, true, true);

register('verify_live_chart_bundle', '검증한 XLSX/CSV 배포 묶음의 차트 4개를 실제 서버에서 모두 읽어 원본과 비교합니다. 개발용 차트는 chartPrefix로 구분하며 누락·조회 실패도 검증 실패로 반환합니다.', {
  directory: file, chartPrefix: z.string().max(120).default(''),
}, ({ directory, chartPrefix }) => verifyLiveBundle(directory, chartPrefix), true, true);

await server.connect(new StdioServerTransport());
