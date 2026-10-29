import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { createRequire } from 'node:module';
import { exportCharts, parseCsv, records } from '../balance.mjs';
import { chartTarget, deploymentPlan } from '../deployment.mjs';
import { verifyLiveBundle } from '../live-bundle.mjs';

test('DEV default, exact environment, identical promotion bytes and no cross-environment fallback', async t => {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'ggd-deployment-'));
  t.after(() => fs.rmSync(root, { recursive: true, force: true }));
  const workbook = createRequire(import.meta.url)('../../planner-studio/app/content-workbook.cjs');
  fs.writeFileSync(path.join(root, 'project.json'), JSON.stringify(workbook.engine().Studio.create()));
  const bundle = await exportCharts('project.json', 'routing-test', root, 'csv');
  assert.equal(chartTarget().environment, 'DEV');
  for (const invalid of ['', 'dev', 'PROD', 'DEV_', null]) assert.throws(() => chartTarget(invalid));
  const dev = deploymentPlan(bundle.directory, undefined, root);
  const live = deploymentPlan(bundle.directory, 'LIVE', root);
  assert.deepEqual(dev.charts.map(c => c.targetChartName), ['DEV_Units','DEV_BaseStats','DEV_UpgradeStats']);
  assert.deepEqual(live.charts.map(c => c.targetChartName), ['LIVE_Units','LIVE_BaseStats','LIVE_UpgradeStats']);
  assert.deepEqual(dev.charts.map(c => [c.file,c.sha256]), live.charts.map(c => [c.file,c.sha256]));
  const calls = [];
  const reader = async (_, name) => {
    calls.push(name);
    const table = name.replace(/^DEV_/, '');
    return { rows: records(parseCsv(fs.readFileSync(path.join(bundle.directory, table + '.csv'), 'utf8'))) };
  };
  assert.equal((await verifyLiveBundle(bundle.directory, undefined, reader, root)).verified, true);
  assert.deepEqual(calls, dev.charts.map(c => c.targetChartName));
  calls.length = 0;
  const missing = await verifyLiveBundle(bundle.directory, 'DEV', async (_, name) => {
    calls.push(name); throw Error('missing chart');
  }, root);
  assert.equal(missing.verified, false);
  assert.deepEqual(calls, dev.charts.map(c => c.targetChartName));
  assert.deepEqual(missing.deferredCharts, ['LevelBonuses']);
});
