'use strict';
const fs=require('node:fs'),path=require('node:path');
module.exports=async({window,app,store,workspace,resultPath,atomic})=>{
  const checks=[],run=source=>window.webContents.executeJavaScript(source);
  try{
    const folder=path.join(workspace,'excel');fs.mkdirSync(folder,{recursive:true});store.setExcelFolder(folder);
    async function until(source){for(let i=0;i<100;i++){if(await run(source))return;await new Promise(r=>setTimeout(r,50));}throw Error('Timeout: '+source);}
    await until("Boolean(document.getElementById('shopsWorkspaceButton'))");
    await run("document.getElementById('shopsWorkspaceButton').click()");
    await until("Boolean(document.querySelector('iframe').contentWindow.StudioUI)");
    const screenshot=await require('./shop-catalog-smoke.cjs')({run,window,store,checks});
    atomic(resultPath,JSON.stringify({ok:true,checks,screenshot},null,2));app.exit(0);
  }catch(error){atomic(resultPath,JSON.stringify({ok:false,checks,error:error.stack},null,2));app.exit(1);}
};
