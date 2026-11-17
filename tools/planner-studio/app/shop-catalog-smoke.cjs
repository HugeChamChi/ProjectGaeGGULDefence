'use strict';
const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
// Shop editor UI smoke (2026-11-17 columns): five sections, new columns editable, export = ShopDailyList + ShopCatalog only.
module.exports=async({run,window,store,checks})=>{
  const frame="document.querySelector('iframe').contentWindow";
  const click=s=>run(`${frame}.document.querySelector(${JSON.stringify(s)}).click()`);
  const value=key=>run(`${frame}.document.querySelector('[data-field="${key}"]').value`);
  const set=(key,v)=>run(`(()=>{const w=${frame},el=w.document.querySelector('[data-field="${key}"]');el.value=${JSON.stringify(String(v))};el.dispatchEvent(new w.Event('change',{bubbles:true}));})()`);
  const saved=await run(`${frame}.StudioUI.snapshot()`);
  try{
    await run("document.getElementById('shopsWorkspaceButton').click()");
    assert(await run("document.getElementById('shopsWorkspaceButton').classList.contains('active')"));
    assert.equal(await run(`${frame}.document.querySelectorAll('[data-shop-section]').length`),5);
    assert.equal(await run(`${frame}.document.querySelectorAll('[data-row]').length`),3);
    await click('[data-row="0"]');
    assert.equal(await value('Limit'),'1');assert.equal(await value('CurrencyType'),'Free');
    await set('Amount',777);
    await click('[data-shop-section="Package"]');
    assert.equal(await run(`${frame}.document.querySelectorAll('[data-row]').length`),2);
    await click('[data-row="3"]');
    assert.equal(await value('LimitPeriod'),'Account');assert.equal(await value('Tag'),'계정당 1회');
    await set('ExtraRewards','Essence:61;Gold:20000');
    await click('[data-action="new"]');
    const after=await run(`${frame}.StudioUI.snapshot()`);
    const fresh=after.tables.ShopCatalog.at(-1);
    assert.equal(fresh.Section,'Package');assert.equal(fresh.Enabled,0);assert.equal(fresh.LimitPeriod,'Account');
    assert.equal(after.tables.ShopCatalog[0].Amount,777);assert.equal(after.tables.ShopCatalog[3].ExtraRewards,'Essence:61;Gold:20000');
    await click('[data-row="3"]');
    await run(`${frame}.document.getElementById('workbook-export').click()`);
    let exported;
    for(let i=0;i<80;i++){
      const names=fs.existsSync(store.exportFolder())?fs.readdirSync(store.exportFolder()):[];
      const folder=names.filter(n=>n.startsWith('콘텐츠-')).sort().reverse().find(n=>fs.existsSync(path.join(store.exportFolder(),n,'ShopDailyList.backend.xlsx')));
      if(folder){exported=path.join(store.exportFolder(),folder);break;}await new Promise(r=>setTimeout(r,50));
    }
    assert(exported,'shop workbook export completed');
    const {open}=require('./workbook.cjs');
    assert(!fs.readdirSync(exported).some(n=>/Probability|Weekly/.test(n)),'no probability or weekly files');
    const catalog=open(fs.readFileSync(path.join(exported,'ShopCatalog.backend.xlsx')));
    assert.equal(catalog.get('ShopCatalog','L1').value,'LimitPeriod');assert.equal(catalog.get('ShopCatalog','M2').value,'Essence:61;Gold:20000');
    const daily=open(fs.readFileSync(path.join(exported,'ShopDailyList.backend.xlsx')));
    assert.equal(daily.get('ShopDailyList','L1').value,'ExtraRewards');assert.equal(daily.get('ShopDailyList','F2').value,'777');
    const output=path.dirname(store.exportFolder());fs.mkdirSync(output,{recursive:true});
    fs.writeFileSync(path.join(output,'planner-shop.png'),await (await window.webContents.capturePage()).toPNG());
    checks.push('five shop sections; Limit/Tag/LimitPeriod/ExtraRewards editable; new package defaults; export is ShopDailyList + ShopCatalog in game column order');
    return path.join(output,'planner-shop.png');
  }finally{await run(`${frame}.StudioUI.loadProject(${JSON.stringify(saved)},['ShopCatalog'])`);}
};
