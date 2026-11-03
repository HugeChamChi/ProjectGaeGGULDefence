'use strict';
const assert=require('node:assert/strict'),fs=require('node:fs'),os=require('node:os'),path=require('node:path');
const deployment=require('./app/backend-deployment.cjs');
const workspace=fs.mkdtempSync(path.join(os.tmpdir(),'ggd-deployment-history-'));
function session(){
  const handlers={};
  deployment.register({handler:(key,fn)=>handlers[key]=fn,workspace,
    atomic:(file,data)=>fs.writeFileSync(file,data),
    exportFile:(name,data)=>{const folder=fs.mkdtempSync(path.join(workspace,'release-'));const file=path.join(folder,name);fs.writeFileSync(file,data);return {file};},
    getCurrentProject:()=>require('./app/content-workbook.cjs').engine().Studio.create()});
  return handlers;
}
const first=session();
const unit=first['content-backend-prepare']({kind:'unitBaseStats'});
const mission=first['content-backend-prepare']({kind:'missions'});
const restarted=session();
assert.equal(restarted['content-backend-history']().length,2);
assert.equal(restarted['content-backend-restore']({releaseKey:unit.releaseKey}).sha256,unit.sha256);
assert.equal(restarted['content-backend-restore']({releaseKey:mission.releaseKey}).targetChartName,'DEV_Missions');
assert.throws(()=>restarted['content-backend-restore']({releaseKey:'missing'}));
fs.appendFileSync(unit.sourceFile,' ');
assert.throws(()=>restarted['content-backend-restore']({releaseKey:unit.releaseKey}));
assert.equal(restarted['content-backend-history']().find(r=>r.releaseKey===unit.releaseKey).available,false);
fs.appendFileSync(mission.file,'changed');
assert.throws(()=>restarted['content-backend-restore']({releaseKey:mission.releaseKey}));
console.log('PASS: separate releases, persisted source, restart restore, unknown release and tampered source/upload rejected');
