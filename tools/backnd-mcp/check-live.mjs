import path from 'node:path';
import { Client } from '@modelcontextprotocol/sdk/client/index.js';
import { StdioClientTransport } from '@modelcontextprotocol/sdk/client/stdio.js';
import { projectRoot } from './balance.mjs';

const client = new Client({ name: 'ggd-backnd-live-check', version: '1.0.0' });
async function call(name, args = {}) {
  const result = await client.callTool({ name, arguments: args }, undefined, { timeout: 90000 });
  if (result.isError) throw Error(result.content[0].text);
  return JSON.parse(result.content[0].text);
}
try {
  await client.connect(new StdioClientTransport({ command: process.execPath, args: [path.join(projectRoot, 'tools/backnd-mcp/server.mjs')], stderr: 'inherit' }));
  const status = await call('backnd_status');
  if (!status.editor.available) throw Error(status.editor.reason);
  const charts = await call('list_live_charts');
  if (!Array.isArray(charts)) throw Error('차트 목록이 배열이 아닙니다.');
  const selected = charts.find(chart => chart.chartName === 'EndlessBalance' && chart.selectedChartFileId && chart.selectedChartFileId !== '0')
    || charts.find(chart => chart.selectedChartFileId && chart.selectedChartFileId !== '0');
  if (!selected) throw Error('조회할 적용 차트가 없습니다.');
  const result = await call('read_live_chart', { chartName: selected.chartName, offset: 0, limit: 1 });
  if (!Array.isArray(result.rows) || !Number.isInteger(result.totalRows)) throw Error('차트 응답 형식이 올바르지 않습니다.');
  console.log(JSON.stringify({ verified: true, chartCount: charts.length, chartName: result.chartName,
    selectedChartFileId: result.selectedChartFileId, totalRows: result.totalRows,
    returnedRows: result.rows.length, columns: Object.keys(result.rows[0] || {}) }, null, 2));
} catch (error) {
  console.error(error.message);
  process.exitCode = 1;
} finally {
  await client.close();
}
