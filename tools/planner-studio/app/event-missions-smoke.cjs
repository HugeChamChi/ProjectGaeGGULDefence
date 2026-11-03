'use strict';
const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict');
module.exports=async({window,app,resultPath,workspace,store})=>{
 const run=code=>window.webContents.executeJavaScript(code,true);
 try{
  const excelFolder=path.join(workspace,'excel');fs.mkdirSync(excelFolder,{recursive:true});store.setExcelFolder(excelFolder);
  for(let i=0;i<100;i++){if(await run(`!!window.__studioReady`))break;await new Promise(r=>setTimeout(r,100));}
  await run(`document.getElementById('rewardsWorkspaceButton').click()`);
  for(let i=0;i<100;i++){if(await run(`!!document.querySelector('#contentWorkspace iframe')?.contentWindow?.StudioUI`))break;await new Promise(r=>setTimeout(r,100));}
  await run(`document.querySelector('#contentWorkspace iframe').contentWindow.StudioUI.openWorkspace('rewards')`);
  assert(await run(`(()=>{const d=document.querySelector('iframe').contentDocument;return !!d.querySelector('[data-table="MissionEvents"]')&&!!d.querySelector('[data-table="Missions"]')&&!d.querySelector('[data-nav="attendance"]')})()`));
  await run(`(()=>{const d=document.querySelector('iframe').contentDocument;d.querySelector('[data-action="new"]').click();for(const [key,value]of Object.entries({EventKey:'ui_two_table_event',Name:'UI event test',StartAt:'2026-10-31T05:00',EndAt:'2026-12-01T05:00'})){const input=d.querySelector('[data-field="'+key+'"]');input.value=value;input.dispatchEvent(new Event('change',{bubbles:true}));}d.querySelector('[data-mission-add]').click();})()`);
  await run(`(()=>{const d=document.querySelector('iframe').contentDocument;for(const [key,value]of Object.entries({Name:'UI attendance milestone',Target:'1'})){const input=d.querySelector('[data-edit-field="'+key+'"]');input.value=value;input.dispatchEvent(new Event('input',{bubbles:true}));}d.querySelector('[data-edit-ref="DataKey"]').click();d.querySelector('[data-catalog-pick="AttendanceDays"]').click();d.querySelector('[data-edit-reward]').click();d.querySelector('[data-catalog-pick="Gold"]').click();d.querySelector('[data-edit-save]').click();})()`);
  const snapshot=await run(`document.querySelector('iframe').contentWindow.StudioUI.snapshot()`);
  assert(snapshot.tables.Missions.some(m=>m.EventKey==='ui_two_table_event'&&m.DataKey==='AttendanceDays'&&m.Target===1&&m.Rewards.some(r=>r.itemId==='Gold')));
  assert(snapshot.tables.Missions.find(m=>m.EventKey==='ui_two_table_event').MissionType==='Event');
  await run(`(()=>{const d=document.querySelector('iframe').contentDocument;d.querySelector('[data-table="Missions"]').click();d.querySelector('[data-action="new"]').click();})()`);
  assert(await run(`(()=>{const d=document.querySelector('iframe').contentDocument;return d.querySelector('[data-field="MissionType"]').value==='Normal'&&!d.querySelector('[data-ref-field="EventKey"]');})()`));
  await run(`document.querySelector('iframe').contentWindow.StudioUI.loadProject(${JSON.stringify(snapshot)},['Missions'])`);
  await run(`document.querySelector('iframe').contentDocument.getElementById('workbook-export').click()`);
  const exportFolder=store.exportFolder();
  for(let i=0;i<100;i++){if(fs.existsSync(exportFolder)&&fs.readdirSync(exportFolder,{recursive:true}).some(n=>n.endsWith('Missions.backend.xlsx')))break;await new Promise(r=>setTimeout(r,100));}
  const files=fs.readdirSync(exportFolder,{recursive:true});assert(files.some(n=>n.endsWith('Missions.backend.xlsx'))&&files.some(n=>n.endsWith('Events.backend.xlsx')));
  const {open}=require('./workbook.cjs');
  for(const name of ['Events','Missions']){const file=files.find(n=>n.endsWith(name+'.backend.xlsx'));const book=open(fs.readFileSync(path.join(exportFolder,file)));assert.equal(book.get(name,'A1').value,name==='Events'?'EventKey':'MissionKey');}
  let screenshotError;
  try{const screenshot=await window.webContents.capturePage();fs.writeFileSync(path.join(path.dirname(resultPath),'event-missions-ui.png'),screenshot.toPNG());}catch(error){screenshotError=error.message;}
  fs.writeFileSync(resultPath,JSON.stringify({ok:true,screenshotError,checks:['unified navigation','event creation','mission dialog','automatic EventKey','attendance condition picker','reward picker','saved mission','two exported charts without version column']},null,2));
 }catch(error){fs.writeFileSync(resultPath,JSON.stringify({ok:false,error:error.stack},null,2));}
 app.exit(0);
};
