'use strict';
const assert=require('node:assert/strict');
module.exports=async({run,window,atomic,resultPath,checks})=>{
 const click=selector=>run(`document.querySelector('iframe').contentDocument.querySelector(${JSON.stringify(selector)}).click()`);
 const set=(selector,value)=>run(`(()=>{const w=document.querySelector('iframe').contentWindow,e=w.document.querySelector(${JSON.stringify(selector)});if(!e)throw Error('Missing '+${JSON.stringify(selector)});e.value=${JSON.stringify(value)};e.dispatchEvent(new w.Event('input',{bubbles:true}));e.dispatchEvent(new w.Event('change',{bubbles:true}));})()`);
 const top='dialog[open]:last-of-type';
 const field=k=>top+' [data-edit-field="'+k+'"]';
 const pick=key=>top+' [data-catalog-pick="'+key+'"]';
 const save=()=>click(top+' [data-edit-save]');
 const before=await run(`document.querySelector('iframe').contentWindow.StudioUI.snapshot()`);
 try{
  await run(`document.getElementById('rewardsWorkspaceButton').click()`);
  window.showInactive();
  await click('[data-workspace-section="items"]');await click('[data-action="new"]');
  await set(field('Name'),'테스트 선택 상자');await set(field('Description'),'골드 또는 스태미나 선택');await set(field('IconAddress'),'Items/TestChoice');
  await click(top+' [data-edit-ref="FunctionId"]');await set(top+' [data-catalog-search]','테스트 선택 기능');await click(top+' [data-catalog-create]');
  await set(field('FunctionType'),'ChooseReward');await click(top+' [data-edit-ref="SelectionListId"]');await set(top+' [data-catalog-search]','공유 재화 목록');await click(top+' [data-catalog-create]');
  for(const [name,id,amount]of [['골드500','Gold',500],['스태미나30','Stamina',30]]){
   await click(top+' [data-edit-add-option]');await set(field('Name'),name);await click(top+' [data-edit-reward]');await click(pick(id));await set(top+' [data-edit-amount="0"]',String(amount));await save();
  }
  await save();await save();await save();
  let p=await run(`document.querySelector('iframe').contentWindow.StudioUI.snapshot()`);const item=p.tables.Item.find(i=>i.Name==='테스트 선택 상자');assert(item);assert.equal(item.IconAddress,'Items/TestChoice');const fn=p.tables.ItemFunction.find(f=>f.FunctionId===item.FunctionId);assert.equal(fn.FunctionType,'ChooseReward');assert.equal(p.tables.SelectionOption.filter(o=>o.SelectionListId===fn.SelectionListId).length,2);
  checks.push('item -> newly created function -> newly created selection list -> two reward options, saved together from one editor');
  await click('[data-action="new"]');await set(field('Name'),'공유 기능 상자');await click(top+' [data-edit-ref="FunctionId"]');await set(top+' [data-catalog-search]','선택 기능');await click(pick(fn.FunctionId));await save();
  p=await run(`document.querySelector('iframe').contentWindow.StudioUI.snapshot()`);assert.equal(p.tables.Item.find(i=>i.Name==='공유 기능 상자').FunctionId,fn.FunctionId);assert.equal(p.tables.ItemFunction.length,before.tables.ItemFunction.length+1);
  await click('[data-action="new"]');await set(field('Name'),'취소할 아이템');await click(top+' [data-edit-ref="FunctionId"]');await click(top+' [data-catalog-create]');await set(field('Name'),'취소할 기능');await save();await click(top+' [data-edit-cancel]');
  p=await run(`document.querySelector('iframe').contentWindow.StudioUI.snapshot()`);assert(!p.tables.Item.some(i=>i.Name==='취소할 아이템'));assert(!p.tables.ItemFunction.some(f=>f.Name==='취소할 기능'));checks.push('shared function reuse and cancellation discard nested new records');
  await click('[data-table="SelectionList"]');await click('[data-action="new"]');await set(field('Name'),'다른 이름의 같은 구성');
  for(const [name,id,amount]of [['골드500','Gold',500],['스태미나30','Stamina',30]]){await click(top+' [data-edit-add-option]');await set(field('Name'),name);await click(top+' [data-edit-reward]');await click(pick(id));await set(top+' [data-edit-amount="0"]',String(amount));await save();}
  await click(top+' [data-edit-reuse="'+fn.SelectionListId+'"]');p=await run(`document.querySelector('iframe').contentWindow.StudioUI.snapshot()`);assert.equal(p.tables.SelectionList.length,before.tables.SelectionList.length+1);assert.equal(p.tables.SelectionOption.length,before.tables.SelectionOption.length+2);checks.push('duplicate option lists detected during creation and reused without leaving draft rows');
  await set('#search','골드');assert(await run(`Array.from(document.querySelector('iframe').contentDocument.querySelectorAll('[data-row]')).some(b=>b.textContent.includes('공유 재화 목록'))`));
  await new Promise(resolve=>setTimeout(resolve,150));
  atomic(resultPath.replace('.json','-item-catalog.png'),(await window.webContents.capturePage()).toPNG());
  const result=await run(`window.studio.contentWorkbookExport({project:document.querySelector('iframe').contentWindow.StudioUI.snapshot(),bundle:'items'})`);assert(result.ok,result.error);assert(result.data.file.endsWith('.xlsx'));assert.equal(result.data.tableFiles.length,4);const unsupported=JSON.parse(JSON.stringify(p));unsupported.tables.Missions[0].Rewards=[{itemId:item.ItemId,amount:1}];const deploy=await run(`window.studio.contentBackendPrepare({kind:'missions',project:${JSON.stringify(unsupported)}})`);assert(!deploy.ok&&deploy.error.includes('지급 처리가 연결되지 않은 아이템'));checks.push('selection content search and 4-sheet item catalog export');
  await click('[data-workspace-section="missions"]');await set('[data-reward-search="Rewards"]','선택 상자');assert(await run(`document.querySelector('iframe').contentDocument.querySelector('[data-add="Rewards:Gold"]').hidden`));
  await click('[data-workspace-section="attendance"]');await click('[data-open-palette="0"]');await set('#grid-item-search','선택 상자');assert(await run(`document.querySelector('iframe').contentDocument.querySelector('[data-palette-item="Gold"]').hidden`));
  checks.push('mission and attendance rewards share item name/ID/description search with type filters');
 }finally{await run(`document.querySelector('iframe').contentWindow.StudioUI.loadProject(${JSON.stringify(before)},['Missions'])`);}
};
