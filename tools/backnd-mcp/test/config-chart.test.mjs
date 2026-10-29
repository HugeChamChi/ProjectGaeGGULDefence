import test from 'node:test';
import assert from 'node:assert/strict';
import {createRequire} from 'node:module';
import {decodeConfigRows} from '../verify-config-chart.mjs';
const require=createRequire(import.meta.url);
const {encodeConfigRows}=require('../../planner-studio/app/chart-payload.cjs');
const config={SchemaVersion:1,text:'한글/line\n'+'.'.repeat(3000)};
const table=encodeConfigRows(config,'test-config');
const rows=table.slice(1).map(r=>Object.fromEntries(table[0].map((k,i)=>[k,String(r[i])])));
test('configuration verifies reordered parts and exact UTF8 payload',()=>{
  const result=decodeConfigRows([...rows].reverse());
  assert.deepEqual(JSON.parse(result.bytes.toString('utf8')),config);
});
test('configuration rejects mixed releases, missing/duplicate parts, wrong hash/version',()=>{
  for(const [field,value]of [['ReleaseKey','other'],['Sha256','0'.repeat(64)],['SchemaVersion','2'],['PartIndex',rows[1].PartIndex]]){
    const changed=structuredClone(rows);changed[0][field]=value;assert.throws(()=>decodeConfigRows(changed));
  }
  assert.throws(()=>decodeConfigRows(rows.slice(1)));
  assert.throws(()=>decodeConfigRows([]));
});
