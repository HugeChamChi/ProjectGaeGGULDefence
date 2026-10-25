import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { verifyBundle, readJson, localFile, parseCsv, compareRows } from './balance.mjs';
import { readChartXlsx } from './xlsx.mjs';
import { readLive } from './unity.mjs';

export async function verifyLiveBundle(directory, chartPrefix = '') {
  verifyBundle(directory);
  const manifest = readJson(path.join(directory, 'manifest.json'));
  const charts = [];
  for (const chart of manifest.charts) {
    const chartName = chartPrefix + chart.chartName;
    try {
      const bytes = fs.readFileSync(localFile(path.join(directory, chart.file)));
      const expected = chart.file.endsWith('.xlsx') ? readChartXlsx(bytes, chart.chartName) : parseCsv(bytes.toString('utf8'));
      const current = await readLive('read', chartName);
      charts.push({ chartName, selectedChartFileId: current.selectedChartFileId, ...compareRows(chart.chartName, expected, current.rows) });
    } catch (error) {
      charts.push({ chartName, equal: false, error: error.message });
    }
  }
  return { verified: charts.every(chart => chart.equal), releaseKey: manifest.releaseKey, verifiedAt: new Date().toISOString(), charts };
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  try {
    const result = await verifyLiveBundle(process.argv[2], process.argv[3] || '');
    console.log(JSON.stringify(result, null, 2));
    if (!result.verified) process.exitCode = 1;
  } catch (error) {
    console.error(error.message); process.exitCode = 1;
  }
}
