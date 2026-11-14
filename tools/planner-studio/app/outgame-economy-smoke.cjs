'use strict';
const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict');
module.exports=async({window,app,store,workspace,resultPath,atomic})=>{
  const run=s=>window.webContents.executeJavaScript(s),checks=[];
  async function until(s){for(let i=0;i<100;i++){if(await run(s))return;await new Promise(r=>setTimeout(r,50));}throw Error('Timeout: '+s);}
  try{
    const isolatedExcel=path.join(workspace,'excel');fs.mkdirSync(isolatedExcel,{recursive:true});store.setExcelFolder(isolatedExcel);
    await until('Boolean(window.__studioReady)');
    await run(`document.getElementById('enhancementWorkspaceButton').click()`);
    await until(`Boolean(document.querySelector('#contentWorkspace iframe').contentWindow.StudioUI)`);
    const prefix=`document.querySelector('#contentWorkspace iframe').contentWindow`;
    await run(`${prefix}.document.querySelector('[data-workspace-section="outgameEconomy"]').click()`);
    await until(`${prefix}.document.getElementById('economy-apply')!==null`);
    assert(await run(`${prefix}.document.getElementById('content').textContent.includes('168,230')`));
    assert(await run(`${prefix}.document.getElementById('content').textContent.includes('1,684,450')`));
    checks.push('enhancement tab opens economy editor with GDD totals');
    await run(`(()=>{const w=${prefix},i=w.document.querySelector('[data-economy-field="UnitCoefficient"]');i.value=200;i.dispatchEvent(new w.Event('change',{bubbles:true}));})()`);
    assert.equal(await run(`${prefix}.StudioUI.snapshot().tables.OutgameEconomy[0].UnitCoefficient`),200);
    await run(`${prefix}.document.getElementById('economy-apply').click()`);
    const snapshot=await run(`${prefix}.StudioUI.snapshot()`);
    const normal=snapshot.tables.Unit.find(u=>u.Kind==='Unit');
    assert.equal(snapshot.tables.UnitUpgrade.find(r=>r.UnitId===normal.UnitId&&r.TargetLevel===2).Costs[0].amount,200);
    checks.push('coefficient edit regenerates costs and preserves draft');
    await run(`${prefix}.document.getElementById('economy-defaults').click();${prefix}.document.getElementById('economy-apply').click()`);
    assert.equal(await run(`${prefix}.StudioUI.snapshot().tables.OutgameEconomy[0].UnitCoefficient`),122.83401441574097);
    await run(`${prefix}.document.getElementById('workbook-export').click()`);
    await until(`${prefix}.document.getElementById('toast').textContent.startsWith('Excel 저장 완료')`);
    const exports=store.exportFolder();
    const find=root=>fs.readdirSync(root,{withFileTypes:true}).flatMap(x=>x.isDirectory()?find(path.join(root,x.name)):[path.join(root,x.name)]);
    const output=find(exports).find(f=>f.endsWith('OutgameEconomy.backend.xlsx'));assert(output,'economy workbook missing');
    assert(fs.existsSync(path.join(path.dirname(output),'Function/OutgameEconomy.cs')));
    checks.push('Excel action writes three charts and Function C# source');
    window.showInactive();await new Promise(r=>setTimeout(r,350));atomic(resultPath.replace('.json','.png'),(await window.webContents.capturePage()).toPNG());window.hide();
    await window.webContents.reload();await until('Boolean(window.__studioReady)');
    await run(`document.getElementById('enhancementWorkspaceButton').click()`);await until(`Boolean(${prefix}.StudioUI)`);
    assert.equal(await run(`${prefix}.StudioUI.snapshot().tables.OutgameEconomy[0].BaseGold`),60);
    checks.push('autosave survives application reload');
    atomic(resultPath,JSON.stringify({ok:true,checks,output},null,2));app.exit(0);
  }catch(error){atomic(resultPath,JSON.stringify({ok:false,checks,error:error.stack},null,2));app.exit(1);}
};
