import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import {createRequire} from 'node:module';
import {projectRoot} from '../balance.mjs';
import {readResearch,validateResearch,researchFromProject,exportResearch} from '../research.mjs';

const source='Assets/WorkSpace/USW/Resources/ResearchBalance.json';
test('current research data, malformed graphs and unsupported costs',()=>{
  const rows=readResearch(source);
  assert.deepEqual(validateResearch(rows),{valid:true,trees:3,nodes:61,releaseKey:'research-initial-20261025'});
  for(const mutate of [r=>r.push(structuredClone(r[0])),r=>r[0].Parents=JSON.stringify([r[0].NodeId]),r=>r[0].Parents='["missing"]',r=>r[0].Stat='0',r=>r[0].ValuePerLevel='NaN',r=>r[0].IconAddress='',r=>r[0].ReleaseKey='mixed']) {
    const changed=structuredClone(rows); mutate(changed); assert.throws(()=>validateResearch(changed));
  }
  const w=createRequire(import.meta.url)('../../planner-studio/app/content-workbook.cjs');
  const project=JSON.parse(JSON.stringify(w.engine().Studio.create()));
  assert.equal(researchFromProject(project,'test').length,61);
  const paid=researchFromProject(project,'test').find(n=>n.TreeKey==='BASIC');
  assert.equal(paid.CostItemId,'Gold'); assert.ok(paid.BaseCost>0);
  for(const change of [n=>n.BaseCost=0,n=>n.BaseCost=-1,n=>n.CostStep=-1,n=>n.CostStep=2147483647,n=>n.CostItemId='Diamond']) {
    const altered=structuredClone(project); change(altered.tables.ResearchNode.find(n=>n.TreeKey==='BASIC'));
    assert.throws(()=>researchFromProject(altered,'test'),/비용/);
  }
  const missing=structuredClone(rows); delete missing[0].BaseCost;
  assert.throws(()=>validateResearch(missing),/열 누락/);
});

test('research XLSX round trip uses DEV and immutable release directory',async t=>{
  const release='research-test-'+Date.now();
  const directory=path.join(projectRoot,'outputs/research-charts',release);
  t.after(()=>fs.rmSync(directory,{recursive:true,force:true}));
  const exported=await exportResearch(source,release);
  assert.equal(exported.targetChartName,'DEV_ResearchBalance');
  assert.equal(exported.remoteApplied,false);
  const rows=readResearch(path.join(directory,'ResearchBalance.xlsx'));
  assert.equal(rows.length,61); assert.equal(rows[0].ReleaseKey,release);
  await assert.rejects(exportResearch(source,release),/EEXIST/);
  await assert.rejects(exportResearch(source,'../escape'));
});
