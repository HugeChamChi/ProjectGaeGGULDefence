import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { createRequire } from 'node:module';

// Runs outside the MCP process so renderer/library messages cannot corrupt stdio JSON-RPC.
const runtime = process.env.GGD_ARTIFACT_DEPENDENCIES || path.join(os.homedir(), '.cache/codex-runtimes/codex-primary-runtime/dependencies/node');
const { Workbook, SpreadsheetFile } = createRequire(path.join(runtime, 'bootstrap.cjs'))('@oai/artifact-tool');
let input = '';
for await (const chunk of process.stdin) input += chunk;
const { name, rows } = JSON.parse(input);
const file = process.argv[2], previewFile = process.argv[3];
const workbook = Workbook.create();
const sheet = workbook.worksheets.add(name);
sheet.showGridLines = false;
const range = sheet.getRangeByIndexes(0, 0, rows.length, rows[0].length);
range.values = rows.map(row => row.map(value => typeof value === 'string' && value.startsWith('=') ? "'" + value : value));
range.format.font = { name: 'Arial', size: 10 };
range.format.rowHeight = 22;
range.format.verticalAlignment = 'center';
const header = sheet.getRangeByIndexes(0, 0, 1, rows[0].length);
header.format.fill = '#26354A';
header.format.font = { name: 'Arial', size: 10, bold: true, color: '#FFFFFF' };
header.format.rowHeight = 28;
for (let column = 0; column < rows[0].length; column++) {
  const values = rows.map(row => row[column]);
  const longest = Math.max(...values.map(value => [...String(value ?? '')].reduce((n, char) => n + (/[^\x00-\x7F]/.test(char) ? 2 : 1), 0)));
  const cells = sheet.getRangeByIndexes(0, column, rows.length, 1);
  cells.format.columnWidth = Math.min(75, Math.max(18, longest + 3));
  if (rows.length > 1) {
    const body = sheet.getRangeByIndexes(1, column, rows.length - 1, 1);
    const numeric = values.slice(1).some(value => typeof value === 'number');
    body.setNumberFormat(numeric ? (values.slice(1).every(value => Number.isInteger(value)) ? '0' : 'General') : '@');
    if (longest > 72) { body.format.wrapText = true; body.format.autofitRows(); }
  }
}
sheet.freezePanes.freezeRows(1);
workbook.recalculate();
await workbook.inspect({ kind: 'table', range: `${name}!A1:D4`, include: 'values,formulas', tableMaxRows: 4, tableMaxCols: 4, maxChars: 1000 });
const output = await SpreadsheetFile.exportXlsx(workbook);
await output.save(file);
if (previewFile) {
  const lastColumn = String.fromCharCode(64 + rows[0].length);
  const preview = await workbook.render({ sheetName: name, range: `A1:${lastColumn}${Math.min(rows.length,7)}`, scale: 1, format: 'png' });
  fs.writeFileSync(previewFile, new Uint8Array(await preview.arrayBuffer()));
}
