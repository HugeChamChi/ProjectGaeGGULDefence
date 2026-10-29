import fs from 'node:fs';
import path from 'node:path';
import {createRequire} from 'node:module';
const require = createRequire(import.meta.url);
const workbook = require('./app/content-workbook.cjs');

export const {encodeConfigRows} = require('./app/chart-payload.cjs');

if (process.argv[1] && path.resolve(process.argv[1]) === path.resolve(new URL(import.meta.url).pathname.replace(/^\/(\w:)/,'$1'))) {
  const [input, output, releaseKey] = process.argv.slice(2);
  if (!input || !output || !releaseKey) throw Error('Usage: node export-event-chart.mjs <project.json> <output.xlsx> <releaseKey>');
  if (fs.existsSync(output)) throw Error('Output already exists');
  const engine = workbook.engine();
  const project = engine.Studio.shape(JSON.parse(fs.readFileSync(input,'utf8')));
  const issues = engine.Studio.validate(project).filter(i=>i.severity === 'error');
  if (issues.length) throw Error(JSON.stringify(issues));
  const config = workbook.toRuntime(project,workbook.bundles.missions);
  fs.writeFileSync(output,Buffer.from(engine.RewardCore.xlsx('MissionEvents',encodeConfigRows(config,releaseKey))),{flag:'wx'});
  console.log(output);
}
