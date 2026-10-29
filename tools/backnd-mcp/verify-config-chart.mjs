import fs from 'node:fs';
import path from 'node:path';
import {createRequire} from 'node:module';
import {fileURLToPath} from 'node:url';
import {localFile,records} from './balance.mjs';
import {readChartXlsx} from './xlsx.mjs';
import {readLive} from './unity.mjs';

export const {decodeConfigRows}=createRequire(import.meta.url)('../planner-studio/app/chart-payload.cjs');

export async function verifyConfigChart(file,chartName) {
  if (!/^(DEV|LIVE)_(MissionEvents|UnitBaseStats)$/.test(chartName)) throw Error('Unsupported configuration chart');
  const sheet=chartName.replace(/^(DEV|LIVE)_/,'');
  const expected=decodeConfigRows(records(readChartXlsx(fs.readFileSync(localFile(file)),sheet)));
  const server=await readLive('read',chartName);
  const actual=decodeConfigRows(server.rows);
  if (actual.releaseKey!==expected.releaseKey || actual.sha256!==expected.sha256 || !actual.bytes.equals(expected.bytes))
    throw Error('Applied server file differs from the prepared configuration');
  return {chartName,selectedChartFileId:server.selectedChartFileId,releaseKey:actual.releaseKey,sha256:actual.sha256,
    serverFileMatches:true,gameplayVerified:false};
}

if (process.argv[1] && path.resolve(process.argv[1])===fileURLToPath(import.meta.url)) {
  try { console.log(JSON.stringify(await verifyConfigChart(process.argv[2],process.argv[3]),null,2)); }
  catch(error) { console.error(error.message);process.exitCode=1; }
}
