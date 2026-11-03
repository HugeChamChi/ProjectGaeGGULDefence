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
  await run(`document.getElementById('itemsWorkspaceButton').click()`);
  await click('#backend-export');assert(await run(`document.getElementById('backendDeploymentDialog').open`));await run(`document.getElementById('backendDeploymentDialog').close()`);
  window.showInactive();
  assert(await run(`document.getElementById('itemsWorkspaceButton').classList.contains('active')`));
  await click('[data-action="new"]');
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
  await click('[data-action="new"]');await set(field('Name'),'취소할 아이템');await click(top+' [data-edit-ref="FunctionId"]');await click(top+' [data-catalog-create]');await set(field('Name'),'취소할 기능');await click(top+' [data-edit-reward]');await click(pick('Stamina'));await save();await click(top+' [data-edit-cancel]');
  p=await run(`document.querySelector('iframe').contentWindow.StudioUI.snapshot()`);assert(!p.tables.Item.some(i=>i.Name==='취소할 아이템'));assert(!p.tables.ItemFunction.some(f=>f.Name==='취소할 기능'));checks.push('shared function reuse and cancellation discard nested new records');
  await click('[data-table="SelectionList"]');await click('[data-action="new"]');await set(field('Name'),'다른 이름의 같은 구성');
  for(const [name,id,amount]of [['골드500','Gold',500],['스태미나30','Stamina',30]]){await click(top+' [data-edit-add-option]');await set(field('Name'),name);await click(top+' [data-edit-reward]');await click(pick(id));await set(top+' [data-edit-amount="0"]',String(amount));await save();}
  await click(top+' [data-edit-reuse="'+fn.SelectionListId+'"]');p=await run(`document.querySelector('iframe').contentWindow.StudioUI.snapshot()`);assert.equal(p.tables.SelectionList.length,before.tables.SelectionList.length+1);assert.equal(p.tables.SelectionOption.length,before.tables.SelectionOption.length+2);checks.push('duplicate option lists detected during creation and reused without leaving draft rows');
  await set('#search','골드');assert(await run(`Array.from(document.querySelector('iframe').contentDocument.querySelectorAll('[data-row]')).some(b=>b.textContent.includes('공유 재화 목록'))`));
  await new Promise(resolve=>setTimeout(resolve,150));
  atomic(resultPath.replace('.json','-item-catalog.png'),(await window.webContents.capturePage()).toPNG());
  const result=await run(`window.studio.contentWorkbookExport({project:document.querySelector('iframe').contentWindow.StudioUI.snapshot(),bundle:'items'})`);assert(result.ok,result.error);assert(result.data.file.endsWith('.xlsx'));assert.equal(result.data.tableFiles.length,4);const unsupported=JSON.parse(JSON.stringify(p));unsupported.tables.Missions[0].Rewards=[{itemId:item.ItemId,amount:1}];const deploy=await run(`window.studio.contentBackendPrepare({kind:'missions',project:${JSON.stringify(unsupported)}})`);assert(!deploy.ok&&deploy.error.includes('지급 처리가 연결되지 않은 아이템'));checks.push('selection content search and 4-sheet item catalog export');
  await click('[data-table="Item"]');await click('[data-action="new"]');await set(field('Name'),'테스트 지급 상자');
  await click(top+' [data-edit-ref="FunctionId"]');await set(top+' [data-catalog-search]','직접 아이템 지급');await click(top+' [data-catalog-create]');
  assert.equal(await run(`document.querySelector('iframe').contentDocument.querySelector(${JSON.stringify(field('FunctionType'))}).value`),'GrantItems');
  assert.equal(await run(`Boolean(document.querySelector('iframe').contentDocument.querySelector(${JSON.stringify(field('Value'))}))`),false);
  for(const [id,amount]of [['Stamina',30],['Gold',500]]){await click(top+' [data-edit-reward]');await click(pick(id));await set(top+' [data-edit-amount="'+(id==='Stamina'?0:1)+'"]',String(amount));}
  await set(field('FunctionType'),'ChooseReward');await set(field('FunctionType'),'GrantItems');
  assert.equal(await run(`document.querySelector('iframe').contentDocument.querySelector(${JSON.stringify(top+' [data-edit-amount="0"]')}).value`),'30');
  assert.equal(await run(`Array.from(document.querySelector('iframe').contentDocument.querySelectorAll('dialog[open]')).filter(d=>getComputedStyle(d).visibility!=='hidden').length`),1);
  await new Promise(resolve=>setTimeout(resolve,200));atomic(resultPath.replace('.json','-grant-flow.png'),(await window.webContents.capturePage()).toPNG());
  await save();
  await run(`(()=>{const w=document.querySelector('iframe').contentWindow,d=w.document,bytes=Uint8Array.from(atob('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR4nGP438HwHwAGmAKHfbWTmQAAAABJRU5ErkJggg=='),c=>c.charCodeAt(0)),transfer=new w.DataTransfer();transfer.items.add(new w.File([bytes],'test_potion.png',{type:'image/png'}));const input=d.querySelector('dialog[open]:last-of-type [data-edit-png]');input.files=transfer.files;input.dispatchEvent(new w.Event('change',{bubbles:true}));})()`);
  await new Promise(resolve=>setTimeout(resolve,300));
  assert(await run(`Boolean(document.querySelector('iframe').contentDocument.querySelector('dialog[open]:last-of-type img[src^="data:image/png;base64,"]'))`));
  await save();p=await run(`document.querySelector('iframe').contentWindow.StudioUI.snapshot()`);assert(p.tables.Item.find(i=>i.Name==='테스트 지급 상자').IconAddress.startsWith('ItemIcons/test_potion_'));
  assert.equal(await run(`Boolean(document.querySelector('iframe').contentDocument.querySelector('[data-icon="IconKey"]'))`),false);
  await set('[data-field="IconAddress"]','ItemIcons/custom_potion');
  p=await run(`document.querySelector('iframe').contentWindow.StudioUI.snapshot()`);assert.equal(p.tables.Item.find(i=>i.Name==='테스트 지급 상자').IconAddress,'ItemIcons/custom_potion');
  assert.equal(await run(`document.querySelector('iframe').contentWindow.Studio.validate(document.querySelector('iframe').contentWindow.StudioUI.snapshot()).filter(i=>i.severity==='error').length`),0);
  const grant=p.tables.ItemFunction.find(f=>f.Name==='직접 아이템 지급');assert.equal(grant.FunctionType,'GrantItems');assert.deepEqual(grant.Rewards,[{itemId:'Stamina',amount:30},{itemId:'Gold',amount:500}]);
  checks.push('generic item grant configures Stamina 30 and Gold 500 through the shared item picker');
  await run(`document.getElementById('rewardsWorkspaceButton').click()`);
  assert.equal(await run(`Boolean(document.querySelector('iframe').contentDocument.querySelector('[data-workspace-section="items"]'))`),false);
  await click('[data-workspace-section="missions"]');await set('[data-reward-search="Rewards"]','선택 상자');assert(await run(`document.querySelector('iframe').contentDocument.querySelector('[data-add="Rewards:Gold"]').hidden`));
  await click('[data-workspace-section="attendance"]');await click('[data-open-palette="0"]');await set('#grid-item-search','선택 상자');assert(await run(`document.querySelector('iframe').contentDocument.querySelector('[data-palette-item="Gold"]').hidden`));
  checks.push('mission and attendance rewards share item name/ID/description search with type filters');
  await set('#grid-item-search','보상 등록 중 새 아이템');await click('#grid-create-item');await save();
  p=await run(`document.querySelector('iframe').contentWindow.StudioUI.snapshot()`);const rewardItem=p.tables.Item.find(i=>i.Name==='보상 등록 중 새 아이템');assert(rewardItem);assert(p.tables.Attendance[0].Rewards.some(r=>r.itemId===rewardItem.ItemId));
  checks.push('dedicated item workspace and inline reward creation attaches the new item to the selected attendance cell');
 }finally{await run(`document.querySelector('iframe').contentWindow.StudioUI.loadProject(${JSON.stringify(before)},['Missions'])`);}
};
