'use strict';
const fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto');
const execute=require('node:util').promisify(require('node:child_process').execFile);
const hash=file=>crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex');
const fail=(code,message)=>Object.assign(new Error(message),{code});

function locateProject(workspace){
  for(const name of ['chart-upload-project.json','backend-verification-project.json']){
    const file=path.join(workspace,name);
    if(fs.existsSync(file)){const {project}=JSON.parse(fs.readFileSync(file,'utf8'));if(project&&fs.existsSync(path.join(project,'Tools/backnd-mcp/chart-upload.mjs')))return project;}
  }
  for(const origin of [__dirname,path.dirname(process.execPath),process.cwd()]){
    let folder=path.resolve(origin);
    for(;;){if(fs.existsSync(path.join(folder,'Tools/backnd-mcp/chart-upload.mjs')))return folder;const parent=path.dirname(folder);if(parent===folder)break;folder=parent;}
  }
  throw fail('PROJECT_REQUIRED','업로드 도구가 있는 게임 프로젝트 폴더를 선택하세요.');
}

function createService({workspace,atomic,run=execute,resolveProject=()=>locateProject(workspace)}){
  let busy=false;
  const plans=new Map(),historyFile=path.join(workspace,'chart-upload-history.json');
  const history=()=>fs.existsSync(historyFile)?JSON.parse(fs.readFileSync(historyFile,'utf8')):[];
  async function command(command,args=[],project=resolveProject()){
    const script=path.join(project,'Tools/backnd-mcp/chart-upload.mjs');let output,exitCode=0;
    try{output=await run(process.execPath,[script,command,...args],{cwd:project,windowsHide:true,shell:false,
      env:{...process.env,ELECTRON_RUN_AS_NODE:'1'},timeout:command==='login'?660000:360000,maxBuffer:1024*1024});}
    catch(error){
      if(error.killed||error.signal)throw fail('PROCESS_INTERRUPTED',command==='upload'?'업로드 확인 시간이 초과되거나 프로세스가 중단됐습니다. 반영됐을 수 있으니 다시 올리기 전에 콘솔의 적용 파일을 확인하세요.':'명령 실행이 중단됐습니다. 로그인 상태를 다시 확인하세요.');
      if(!error.stdout)throw fail('CLI_FAILED','업로드 도구를 실행하지 못했습니다. 프로젝트 경로와 도구 설치 상태를 확인하세요.');
      output=error;exitCode=Number(error.code);
    }
    let result;
    try{const lines=output.stdout.trim().split(/\r?\n/);result=JSON.parse(lines[lines.length-1]);if(!result||typeof result.ok!=='boolean')throw Error();}
    catch{throw fail('INVALID_RESPONSE',command==='upload'?'업로드 결과를 해석하지 못했습니다. 다시 올리기 전에 콘솔의 적용 파일을 확인하세요.':'업로드 도구의 응답을 읽지 못했습니다.');}
    if(exitCode!==0||!result.ok)throw fail(exitCode===2?'LOGIN_REQUIRED':result.code||'UPLOAD_FAILED',result.message||'업로드 도구가 실패했습니다.');
    return result;
  }
  async function exclusive(action){
    if(busy)throw fail('BUSY','다른 업로드·로그인·상태 확인이 진행 중입니다. 완료된 뒤 실행하세요.');
    busy=true;try{return await action();}finally{busy=false;}
  }
  function plan({environment,chart,file}){
    const project=resolveProject();
    if(!['DEV','LIVE'].includes(environment))throw fail('BAD_ENV','DEV 또는 LIVE를 선택하세요.');
    if(typeof chart!=='string'||!/^([A-Za-z][A-Za-z0-9]*)$/.test(chart)||/^(DEV|LIVE|COMMON)_/.test(chart))throw fail('BAD_CHART','차트에는 접두어 없는 영문 내용 이름을 입력하세요. 예: SkillBalance');
    if(typeof file!=='string'||!path.isAbsolute(file)||!fs.existsSync(file)||!fs.statSync(file).isFile()||!(/\.(xlsx|xls|csv)$/i.test(file)))throw fail('BAD_FILE','업로드할 Excel 또는 CSV 파일을 선택하세요.');
    const stat=fs.statSync(file);if(!stat.size||stat.size>32*1024*1024)throw fail('BAD_FILE','비어 있지 않은 32MB 이하 파일을 선택하세요.');
    const id=crypto.randomUUID(),folder=path.join(workspace,'chart-upload-files',id),snapshot=path.join(folder,path.basename(file));
    fs.mkdirSync(folder,{recursive:true});atomic(snapshot,fs.readFileSync(file));
    const record={id,project,environment,chart,chartName:environment+'_'+chart,file:path.resolve(file),snapshot,sha256:hash(snapshot),bytes:stat.size,createdAt:new Date().toISOString()};
    plans.set(id,record);return {...record};
  }
  async function upload({id,confirmed,confirmLive}){
    if(confirmed!==true)throw fail('NOT_CONFIRMED','주의 창에서 대상과 파일을 확인한 뒤 실행하세요.');
    const record=plans.get(id);if(!record)throw fail('PLAN_REQUIRED','확인할 업로드 파일을 다시 준비하세요.');
    if(record.environment==='LIVE'&&confirmLive!==true)throw fail('LIVE_NOT_CONFIRMED','LIVE 주의 창에서 출시 반영을 확인해야 합니다.');
    return exclusive(async()=>{
      if(!fs.existsSync(record.file)||hash(record.file)!==record.sha256||hash(record.snapshot)!==record.sha256)throw fail('FILE_CHANGED','확인한 뒤 파일이 바뀌었습니다. 파일과 대상을 다시 확인하세요.');
      plans.delete(id);
      const entry={...record,startedAt:new Date().toISOString()};
      try{
        const args=['--env',record.environment,'--chart',record.chart,'--file',record.snapshot];if(record.environment==='LIVE')args.push('--confirm-live');
        const result=await command('upload',args,record.project);
        if(result.env!==record.environment||result.chartName!==record.chartName||!result.fileId||!Number.isInteger(result.rowCount)||result.rowCount<0)throw fail('INVALID_RESPONSE','응답의 차트·적용 파일 정보가 다릅니다. 다시 올리기 전에 콘솔을 확인하세요.');
        entry.result=result;entry.ok=true;return result;
      }catch(error){entry.ok=false;entry.code=error.code||'ERROR';entry.message=error.message;throw error;}
      finally{entry.finishedAt=new Date().toISOString();try{atomic(historyFile,JSON.stringify([entry,...history()],null,2));}catch{throw fail('HISTORY_FAILED','명령은 종료됐지만 업로드 이력을 저장하지 못했습니다. 다시 올리기 전에 콘솔을 확인하세요.');}}
    });
  }
  return {plan,upload,history,isBusy:()=>busy,status:()=>exclusive(()=>command('status')),login:()=>exclusive(()=>command('login'))};
}

function register({handler,workspace,atomic,dialog,parent,service=createService({workspace,atomic})}){
  handler('chart-upload-plan',p=>service.plan(p));
  handler('chart-upload-run',p=>service.upload(p));
  handler('chart-upload-history',()=>service.history());
  handler('chart-upload-status',()=>service.status());
  handler('chart-upload-login',()=>service.login());
  handler('chart-upload-choose-project',async()=>{
    const result=await dialog.showOpenDialog(parent,{title:'Tools/backnd-mcp/chart-upload.mjs가 있는 프로젝트',properties:['openDirectory']});if(result.canceled)return null;
    const project=result.filePaths[0];if(!fs.existsSync(path.join(project,'Tools/backnd-mcp/chart-upload.mjs')))throw Error('이 폴더에 차트 업로드 도구가 없습니다. 게임 프로젝트 루트를 선택하세요.');
    atomic(path.join(workspace,'chart-upload-project.json'),JSON.stringify({project}));return {project};
  });
  handler('chart-upload-choose-file',async()=>{
    const result=await dialog.showOpenDialog(parent,{title:'뒤끝에 업로드할 차트 파일',properties:['openFile'],filters:[{name:'차트 Excel / CSV',extensions:['xlsx','xls','csv']}]});return result.canceled?null:{file:result.filePaths[0]};
  });
}
module.exports={createService,register,locateProject};
