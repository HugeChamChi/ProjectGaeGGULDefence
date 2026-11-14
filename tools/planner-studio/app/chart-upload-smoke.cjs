'use strict';
const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict');
function fixture({workspace}){
  const project=path.join(workspace,'mock-project'),file=path.join(workspace,'선택한 SkillBalance.xlsx');
  fs.mkdirSync(path.join(project,'Tools/backnd-mcp'),{recursive:true});
  // Exercise Electron's Node subprocess using only a local fixture, without network access.
  fs.writeFileSync(path.join(project,'Tools/backnd-mcp/chart-upload.mjs'),`console.log(JSON.stringify({ok:true,project:'offline-fixture',chartCount:0,charts:[]}));`);
  fs.writeFileSync(file,'offline mock chart');
  const test={project,file,calls:[],mode:'success',release:null,
    dialog:{showOpenDialog:async()=>({canceled:false,filePaths:[file]})}};
  test.run=async(exe,args,options)=>{
    test.calls.push({args,options});
    if(test.mode==='login-required')throw {code:2,stdout:'{"ok":false,"code":"LOGIN_REQUIRED","message":"login required"}'};
    if(test.mode==='wait')await new Promise(resolve=>test.release=resolve);
    if(args[1]==='status')return {stdout:JSON.stringify({ok:true,project:'GaeGGULDefence',chartCount:3,charts:['DEV_OutgameEconomy','LIVE_OutgameEconomy','DEV_SkillBalance']})};
    if(args[1]==='login')return {stdout:'{"ok":true,"command":"login"}'};
    const env=args[args.indexOf('--env')+1],chart=args[args.indexOf('--chart')+1];
    return {stdout:JSON.stringify({ok:true,env,chartName:env+'_'+chart,fileId:'mock-new-file',previousFileId:'mock-old-file',rowCount:1})};
  };
  return test;
}
async function smoke({window,app,store,workspace,resultPath,atomic,test}){
  const checks=[],run=async s=>{try{return await window.webContents.executeJavaScript(s);}catch(error){throw Error(error.message+' Source: '+s);}};
  async function until(s){for(let i=0;i<120;i++){if(await run(s))return;await new Promise(r=>setTimeout(r,50));}throw Error('Timeout: '+s);}
  try{
    const subprocess=require('./chart-upload.cjs').createService({workspace,atomic,resolveProject:()=>test.project});assert.equal((await subprocess.status()).project,'offline-fixture');checks.push('packaged Electron executes local Node CLI protocol without Node installation or network');
    const excelFolder=path.join(workspace,'excel');fs.mkdirSync(excelFolder,{recursive:true});store.setExcelFolder(excelFolder);
    await until('Boolean(window.__studioReady) && Boolean(window.ChartUploadUI)');
    await run(`document.getElementById('chartUploadButton').click()`);assert.equal(test.calls.length,0);
    await run(`document.getElementById('chartUploadChooseFile').click()`);await until(`document.getElementById('chartUploadFile').value!==''`);
    await run(`document.getElementById('chartUploadChart').value='SkillBalance';document.getElementById('chartUploadReview').click()`);await until(`document.getElementById('chartUploadWarning').open`);
    assert.equal(test.calls.length,0);assert.equal(await run(`document.getElementById('chartUploadWarningChart').textContent`),'DEV_SkillBalance');
    await run(`document.getElementById('chartUploadCancel').click()`);assert.equal(test.calls.length,0);checks.push('file selection and caution cancel never invoke upload');
    await run(`document.getElementById('chartUploadReview').click()`);await until(`document.getElementById('chartUploadWarning').open`);
    test.mode='wait';await run(`document.getElementById('chartUploadConfirm').click()`);await until(`document.getElementById('chartUploadReview').disabled`);
    assert(await run(`document.getElementById('chartUploadStatus').disabled && document.getElementById('chartUploadLogin').disabled && document.getElementById('chartUploadClose').disabled`));
    assert.equal(test.calls.length,1);test.release();await until(`document.getElementById('chartUploadMessage').textContent.includes('업로드·적용 확인 완료')`);
    assert(await run(`document.getElementById('chartUploadResult').textContent.includes('mock-old-file')`));checks.push('confirmed DEV upload locks controls and displays applied/previous file IDs');
    test.mode='success';await run(`document.getElementById('chartUploadEnvironment').value='LIVE';document.getElementById('chartUploadReview').click()`);await until(`document.getElementById('chartUploadWarning').open`);
    assert.equal(await run(`document.getElementById('chartUploadWarningChart').textContent`),'LIVE_SkillBalance');
    assert(await run(`document.getElementById('chartUploadWarningText').textContent.includes('출시 앱')`));
    window.showInactive();await new Promise(r=>setTimeout(r,300));atomic(resultPath.replace('.json','-warning.png'),(await window.webContents.capturePage()).toPNG());window.hide();
    await run(`document.getElementById('chartUploadConfirm').click()`);await until(`document.getElementById('chartUploadMessage').textContent.includes('LIVE_SkillBalance 업로드·적용 확인 완료')`);
    assert(test.calls.at(-1).args.includes('--confirm-live'));checks.push('LIVE caution identifies release impact and confirmation passes explicit flag (mock only)');
    test.mode='login-required';await run(`document.getElementById('chartUploadStatus').click()`);await until(`document.getElementById('chartUploadMessage').textContent.includes('로그인이 필요합니다')`);
    test.mode='success';await run(`document.getElementById('chartUploadLogin').click()`);await until(`document.getElementById('chartUploadMessage').textContent.includes('로그인 완료')`);checks.push('exit code 2 offers dedicated upload login and recovers');
    await run(`document.getElementById('chartUploadHistory').click()`);await until(`document.getElementById('chartUploadHistoryList').textContent.includes('mock-old-file')`);
    await run(`document.getElementById('chartUploadClose').click();document.getElementById('enhancementWorkspaceButton').click()`);
    const w=`document.querySelector('#contentWorkspace iframe').contentWindow`;
    await until(`Boolean(${w}.StudioUI) && Boolean(${w}.document.querySelector('[data-workspace-section="outgameEconomy"]'))`);await run(`${w}.document.querySelector('[data-workspace-section="outgameEconomy"]').click();${w}.document.getElementById('chart-upload-content').click()`);
    await until(`document.getElementById('chartUploadDialog').open && document.getElementById('chartUploadFiles').options.length===3`);
    assert.equal(await run(`document.getElementById('chartUploadEnvironment').value`),'DEV');
    assert.equal(await run(`document.getElementById('chartUploadChart').value`),'OutgameEconomy');
    await run(`document.getElementById('chartUploadFiles').selectedIndex=1;document.getElementById('chartUploadFiles').dispatchEvent(new Event('change'))`);
    assert.equal(await run(`document.getElementById('chartUploadChart').value`),'UnitBaseStats');checks.push('current editor export opens three-chart selector with DEV default');
    await run(`document.getElementById('chartUploadClose').click();${w}.document.querySelector('[data-workspace-section="units"]').click();${w}.document.getElementById('unit-edit').click()`);
    await run(`(()=>{const w=${w},input=w.document.querySelector('[data-level-cost="0:0"]');input.value=456;input.dispatchEvent(new w.Event('input',{bubbles:true}));w.document.getElementById('chart-upload-content').click();})()`);
    assert.equal(await run(`document.getElementById('chartUploadDialog').open`),false);
    assert(await run(`${w}.document.getElementById('toast').textContent.includes('수정 저장 또는 취소')`));
    await run(`${w}.document.getElementById('unit-save').click();${w}.document.getElementById('chart-upload-content').click()`);await until(`document.getElementById('chartUploadDialog').open`);
    const editedFile=await run(`document.getElementById('chartUploadFile').value`),book=require('./workbook.cjs').open(fs.readFileSync(editedFile));
    const sheet=book.sheets.keys().next().value;assert([...book.sheets.get(sheet).cells.keys()].some(address=>book.get(sheet,address).value==='456'));
    checks.push('unsaved unit draft blocks export/upload; saved cost appears in exported chart');
    await run(`document.getElementById('chartUploadClose').click();document.getElementById('backendDeploymentButton').click();document.getElementById('backendKind').value='missions';document.getElementById('backendPrepare').click()`);
    await until(`document.getElementById('backendStatus').textContent.startsWith('파일 준비 완료')`);await run(`document.getElementById('backendUpload').click()`);
    await until(`document.getElementById('chartUploadDialog').open && document.getElementById('chartUploadFiles').options.length===2`);
    assert.equal(await run(`document.getElementById('chartUploadChart').value`),'Events');checks.push('prepared deployment opens Events and Missions individually for review');
    await run(`document.getElementById('chartUploadClose').click();document.getElementById('backendClose').click()`);
    await window.webContents.reload();await until('Boolean(window.__studioReady) && Boolean(window.ChartUploadUI)');
    await run(`document.getElementById('chartUploadButton').click();document.getElementById('chartUploadHistory').click()`);await until(`document.getElementById('chartUploadHistoryList').textContent.includes('mock-old-file')`);checks.push('upload history survives reload');
    atomic(resultPath,JSON.stringify({ok:true,checks,cliCalls:test.calls.length,realServerUploads:0},null,2));app.exit(0);
  }catch(error){atomic(resultPath,JSON.stringify({ok:false,checks,error:error.stack,realServerUploads:0},null,2));app.exit(1);}
}
module.exports={fixture,smoke};
