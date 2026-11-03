import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { spawn } from 'node:child_process';
import { createRequire } from 'node:module';

const { open } = createRequire(import.meta.url)('../planner-studio/app/workbook.cjs');
const worker = fileURLToPath(new URL('./xlsx-worker.mjs', import.meta.url));

export async function writeChartXlsx(name, rows, file, previewFile) {
  await new Promise((resolve, reject) => {
    const child = spawn(process.execPath, [worker, file, ...(previewFile ? [previewFile] : [])], { windowsHide: true, stdio: ['pipe', 'ignore', 'pipe'] });
    let errorText = '';
    const timeout = setTimeout(() => { child.kill(); reject(Error('XLSX 생성 시간이 초과됐습니다.')); }, 60000);
    child.stderr.on('data', chunk => { errorText = (errorText + chunk.toString()).slice(-3000); });
    child.on('error', error => { clearTimeout(timeout); reject(error); });
    child.on('close', code => { clearTimeout(timeout); code === 0 ? resolve() : reject(Error('XLSX 생성 실패: ' + errorText)); });
    child.stdin.on('error', () => {}); // Spawn/exit errors are reported by the child handlers.
    child.stdin.end(JSON.stringify({ name, rows }));
  });
}

function columnIndex(address) {
  const letters = address.match(/^[A-Z]+/)[0];
  return [...letters].reduce((value, letter) => value * 26 + letter.charCodeAt(0) - 64, 0) - 1;
}
function columnName(index) {
  let name = '';
  for (let n = index + 1; n > 0; n = Math.floor((n - 1) / 26)) name = String.fromCharCode(65 + (n - 1) % 26) + name;
  return name;
}

export function readChartXlsx(bytes, expectedName) {
  const book = open(bytes);
  if (book.sheets.size !== 1) throw Error('뒤끝 업로드 Excel에는 시트가 정확히 1개여야 합니다.');
  const [name, sheet] = [...book.sheets][0];
  if (expectedName && name !== expectedName) throw Error(`Excel 시트 이름 불일치: ${name}`);
  const addresses = [...sheet.cells.keys()];
  const headerAddresses = addresses.filter(address => /^[A-Z]+1$/.test(address));
  if (!headerAddresses.length) throw Error('Excel 첫 행에 열 이름이 필요합니다.');
  const columns = Math.max(...headerAddresses.map(columnIndex)) + 1;
  const maxRow = Math.max(...addresses.map(address => Number(address.match(/\d+$/)[0])));
  if (maxRow > 20001 || addresses.some(address => columnIndex(address) >= columns)) throw Error('Excel 표 범위가 올바르지 않습니다.');
  const rows = Array.from({length: maxRow}, (_, row) => Array.from({length: columns}, (_, column) => {
    const address = columnName(column) + (row + 1), cell = book.get(name, address);
    if (cell.formula !== null) throw Error(`차트 Excel에는 수식을 넣을 수 없습니다: ${address}`);
    if (cell.value !== '' && !['s','inlineStr','str','b'].includes(cell.type)) {
      const value = Number(cell.value);
      if (!Number.isFinite(value)) throw Error(`유효하지 않은 Excel 숫자: ${address}`);
      return value;
    }
    return cell.value;
  }));
  if (rows[0].some(value => typeof value !== 'string' || !value) || new Set(rows[0]).size !== columns) throw Error('Excel 열 이름이 없거나 중복됩니다.');
  return rows;
}
