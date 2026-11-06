'use strict';
const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
module.exports=async({run,window,store,checks})=>{
  const frame="document.querySelector('iframe').contentWindow";
  const click=s=>run(`${frame}.document.querySelector(${JSON.stringify(s)}).click()`);
  const set=(key,value)=>run(`(()=>{const w=${frame},el=w.document.querySelector('[data-field="${key}"]');el.value=${JSON.stringify(String(value))};el.dispatchEvent(new w.Event('change',{bubbles:true}));})()`);
  const saved=await run(`${frame}.StudioUI.snapshot()`);
  try{
    await run("document.getElementById('shopsWorkspaceButton').click()");
    assert(await run("document.getElementById('shopsWorkspaceButton').classList.contains('active')"));
    assert.equal(await run(`${frame}.document.querySelectorAll('[data-shop-section]').length`),4);
    assert.equal(await run(`${frame}.document.querySelectorAll('[data-row]').length`),9);
    await click('[data-row="0"]');
    assert.equal(await run(`${frame}.document.querySelector('[data-field="FixedFree"]').value`),'1');
    await set('Amount',777);await set('RewardCurrency','Diamond');await set('Enabled',1);
    await click('[data-shop-section="Weekly"]');
    assert.equal(await run(`${frame}.document.querySelector('[data-field="Amount"]').value`),'1000');
    await set('Amount',888);
    await click('[data-row="10"]');await set('Weight',333);await click('[data-row="9"]');
    await click('[data-action="new"]');
    const after=await run(`${frame}.StudioUI.snapshot()`);
    assert.equal(after.tables.ShopCatalog.at(-1).Section,'Weekly');
    assert.equal(after.tables.ShopCatalog.at(-1).Enabled,0);
    assert.equal(after.tables.ShopCatalog[0].Amount,777);assert.equal(after.tables.ShopCatalog[9].Amount,888);
    assert.equal(new Set(after.tables.ShopCatalog.map(r=>r.ShopID)).size,26);
    await click('[data-row="9"]');
    await run(`${frame}.document.getElementById('workbook-export').click()`);
    let exported;
    for(let i=0;i<80;i++){
      const names=fs.existsSync(store.exportFolder())?fs.readdirSync(store.exportFolder()):[];
      const folder=names.filter(n=>n.startsWith('콘텐츠-')).sort().reverse().find(n=>fs.existsSync(path.join(store.exportFolder(),n,'ShopCatalog_Editing.editor-preview.json')));
      if(folder){exported=path.join(store.exportFolder(),folder);break;}await new Promise(r=>setTimeout(r,50));
    }
    assert(exported,'shop workbook export completed');
    const excel=require('./content-workbook.cjs');
    const parsed=excel.inspect(fs.readFileSync(path.join(exported,'ShopCatalog_Editing.xlsx')));
    assert.equal(parsed.tables.ShopCatalog.length,26);assert.equal(parsed.tables.ShopCatalog[0].Amount,777);
    const preview=JSON.parse(fs.readFileSync(path.join(exported,'ShopCatalog_Editing.editor-preview.json'),'utf8'));
    assert.equal(preview[0].RewardCurrency,'Diamond');assert.equal(preview[9].Amount,888);
    assert.equal(preview[10].Weight,333);
    const {open}=require('./workbook.cjs');
    for(const name of ['ShopDailyProbability','ShopWeeklyProbability']){
      const book=open(fs.readFileSync(path.join(exported,name+'.xlsx')));assert.equal(book.sheets.size,1);
      assert.equal(book.get(name,'B1').value,'percent');
      let sum=0;for(let i=2;i<=9;i++)sum+=Math.round(Number(book.get(name,'B'+i).value)*10000000);
      assert.equal(sum,1000000000);
      if(name==='ShopWeeklyProbability')assert(Number(book.get(name,'B2').value)>60);
    }
    const weeklyList=open(fs.readFileSync(path.join(exported,'ShopWeeklyList.xlsx')));
    assert.equal(weeklyList.get('ShopWeeklyList','A3').value,'2002');
    assert.equal(weeklyList.get('ShopWeeklyList','E3').value,'333');
    const output=path.dirname(store.exportFolder());fs.mkdirSync(output,{recursive:true});
    fs.writeFileSync(path.join(output,'planner-shop.png'),await (await window.webContents.capturePage()).toPNG());
    checks.push('editable weights/free rewards; Excel and preview export; BACKND probability files include edited weight and exactly 100 percent');
    return path.join(output,'planner-shop.png');
  }finally{await run(`${frame}.StudioUI.loadProject(${JSON.stringify(saved)},['ShopCatalog'])`);}
};
