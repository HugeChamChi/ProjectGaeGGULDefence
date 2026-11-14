'use strict';
const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path'),os=require('node:os');
const {createService,register}=require('./app/chart-upload.cjs');
(async()=>{
  const workspace=fs.mkdtempSync(path.join(os.tmpdir(),'ggd-upload-')),file=path.join(workspace,'한글 차트 & $(literal).xlsx');fs.writeFileSync(file,'test chart bytes');
  const calls=[],atomic=(file,data)=>fs.writeFileSync(file,data),project=path.resolve(__dirname,'../..');
  let mode='success',release;
  const fake=async(exe,args,options)=>{
    calls.push({exe,args,options});
    if(mode==='wait')await new Promise(resolve=>release=resolve);
    if(mode==='login-required')throw {code:2,stdout:'{"ok":false,"code":"LOGIN_REQUIRED","message":"login"}\n'};
    if(mode==='failure')throw {code:1,stdout:'{"ok":false,"code":"STORED_MISMATCH","message":"rows differ"}\n'};
    if(mode==='timeout')throw {killed:true};
    if(mode==='malformed')return {stdout:'invalid json'};
    if(args[1]==='status')return {stdout:JSON.stringify({ok:true,command:'status',project:'GaeGGULDefence',chartCount:1,charts:['DEV_UploadTest']})};
    if(args[1]==='login')return {stdout:'{"ok":true,"command":"login"}'};
    const env=args[args.indexOf('--env')+1],chart=args[args.indexOf('--chart')+1];
    return {stdout:'diagnostic line\n'+JSON.stringify({ok:true,command:'upload',env,chartName:mode==='wrong-chart'?'LIVE_Wrong':env+'_'+chart,fileId:'new-file',previousFileId:'old-file',rowCount:12})+'\n'};
  };
  const service=createService({workspace,atomic,run:fake,resolveProject:()=>project});
  const make=(environment='DEV')=>service.plan({environment,chart:'UploadTest',file});
  assert.throws(()=>service.plan({environment:'COMMON',chart:'UploadTest',file}));
  assert.throws(()=>service.plan({environment:'DEV',chart:'DEV_UploadTest',file}));
  assert.throws(()=>service.plan({environment:'DEV',chart:'X;evil',file}));
  assert.throws(()=>service.plan({environment:'DEV',chart:'X',file:'relative.xlsx'}));
  const dev=make();assert.equal(calls.length,0);assert.equal(dev.chartName,'DEV_UploadTest');
  await assert.rejects(service.upload({id:dev.id}),{code:'NOT_CONFIRMED'});assert.equal(calls.length,0);
  const success=await service.upload({id:dev.id,confirmed:true});assert.equal(success.previousFileId,'old-file');
  const call=calls.at(-1);assert.equal(call.options.shell,false);assert.equal(call.options.windowsHide,true);assert.equal(call.options.env.ELECTRON_RUN_AS_NODE,'1');
  assert.equal(call.args[0],path.join(project,'Tools/backnd-mcp/chart-upload.mjs'));assert(call.args.includes(dev.snapshot));assert(!call.args.includes('--confirm-live'));
  await assert.rejects(service.upload({id:dev.id,confirmed:true}),{code:'PLAN_REQUIRED'});
  const live=make('LIVE');await assert.rejects(service.upload({id:live.id,confirmed:true}),{code:'LIVE_NOT_CONFIRMED'});
  await service.upload({id:live.id,confirmed:true,confirmLive:true});assert(calls.at(-1).args.includes('--confirm-live'));
  const changed=make();fs.appendFileSync(file,'changed');await assert.rejects(service.upload({id:changed.id,confirmed:true}),{code:'FILE_CHANGED'});
  const corrupt=make();fs.appendFileSync(corrupt.snapshot,'changed');await assert.rejects(service.upload({id:corrupt.id,confirmed:true}),{code:'FILE_CHANGED'});
  mode='wait';const pendingPlan=make(),pending=service.upload({id:pendingPlan.id,confirmed:true});
  await assert.rejects(service.status(),{code:'BUSY'});await assert.rejects(service.login(),{code:'BUSY'});await assert.rejects(service.upload({id:make().id,confirmed:true}),{code:'BUSY'});
  assert(service.isBusy());release();await pending;assert(!service.isBusy());
  for(const [m,code]of [['login-required','LOGIN_REQUIRED'],['failure','STORED_MISMATCH'],['timeout','PROCESS_INTERRUPTED'],['malformed','INVALID_RESPONSE'],['wrong-chart','INVALID_RESPONSE']]){
    mode=m;await assert.rejects(service.upload({id:make().id,confirmed:true}),{code});assert(!service.isBusy());
  }
  mode='success';assert.equal((await service.status()).chartCount,1);assert.equal((await service.login()).ok,true);
  const records=service.history();assert.equal(records.filter(r=>r.ok).length,3);assert.equal(records.filter(r=>!r.ok).length,5);
  const handlers={},dialog={showOpenDialog:async()=>({canceled:true})};
  register({handler:(name,fn)=>handlers[name]=fn,workspace,atomic,dialog,parent:{},service});
  assert.equal(await handlers['chart-upload-choose-file'](),null);assert.equal(await handlers['chart-upload-choose-project'](),null);
  const restarted=createService({workspace,atomic,run:fake,resolveProject:()=>project});assert.equal(restarted.history().length,8);
  // The actual CLI runner is only invoked against a local fixture, never the console.
  const fixture=path.join(workspace,'fixture');fs.mkdirSync(path.join(fixture,'Tools/backnd-mcp'),{recursive:true});
  fs.writeFileSync(path.join(fixture,'Tools/backnd-mcp/chart-upload.mjs'),`console.log(JSON.stringify({ok:true,project:'fixture',chartCount:0,charts:[]}))`);
  const subprocess=createService({workspace,atomic,resolveProject:()=>fixture});assert.equal((await subprocess.status()).project,'fixture');
  fs.writeFileSync(path.join(fixture,'Tools/backnd-mcp/chart-upload.mjs'),`console.log(JSON.stringify({ok:false,code:'LOGIN_REQUIRED',message:'fixture login'}));process.exitCode=2;`);
  await assert.rejects(subprocess.status(),{code:'LOGIN_REQUIRED'});
  console.log(JSON.stringify({ok:true,checks:['no upload before warning confirmation','DEV/LIVE command argument binding','immutable source and snapshot checks','single operation lock for status/login/upload','single-use confirmation','success/file IDs and failure history','exit 2 login/failed/malformed/timeout/response mismatch handling','literal Unicode and shell metacharacter filename','real subprocess stdout and exit-code protocol (local fixture only)'],workspace},null,2));
})().catch(error=>{console.error(error);process.exitCode=1;});
