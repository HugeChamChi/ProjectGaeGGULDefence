'use strict';
const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');

exports.register = ({ handler, workspace, store, atomic }) => {
  const draft = path.join(workspace, 'content-project-v2.json');
  const excel=require('./content-workbook.cjs');
  const current=()=>excel.engine().Studio.shape(fs.existsSync(draft)?JSON.parse(fs.readFileSync(draft,'utf8')):excel.engine().Studio.create());
  function exportFile(name,bytes){
    const folder=path.join(store.exportFolder(),'콘텐츠-'+new Date().toISOString().replace(/[:.]/g,'-')+'-'+crypto.randomBytes(3).toString('hex'));
    fs.mkdirSync(folder,{recursive:true});const file=path.join(folder,name);atomic(file,bytes);return {file};
  }
  require('./backend-deployment.cjs').register({handler,workspace,atomic,exportFile,getCurrentProject:current});
  handler('content-workbook-export',p=>{
    const names=excel.bundles[p.bundle]||[p.table];
    const result=exportFile(p.bundle==='items'?'ItemCatalog_'+new Date().toISOString().slice(0,10)+'.xlsx':p.bundle==='bonuses'?'UnitLevelBonuses_작성용_'+new Date().toISOString().slice(0,10)+'.xlsx':p.bundle==='missions'?'MissionEvents.xlsx':p.bundle==='enhancement'?'EnhancementBalance.xlsx':p.bundle==='rewards'?'AttendanceEvent.xlsx':p.table+'.xlsx',excel.write(p.project,names));
    if(p.bundle==='items')result.tableFiles=names.map(name=>{const file=path.join(path.dirname(result.file),name+'.xlsx');atomic(file,excel.write(p.project,[name]));return file;});
    const parsed=excel.toRuntime(p.project,names);
    result.dataFile=result.file.replace(/\.xlsx$/,'.data.json');atomic(result.dataFile,JSON.stringify(parsed,null,2));
    result.schemaFile=result.file.replace(/\.xlsx$/,'.schema.json');atomic(result.schemaFile,JSON.stringify(excel.contract(names),null,2));
    if(p.bundle==='missions'){
      const release='event-'+crypto.randomBytes(12).toString('hex');
      const rows=require('./chart-payload.cjs').encodeConfigRows(parsed,release);
      result.chartFile=result.file.replace(/\.xlsx$/,'.backend.xlsx');
      atomic(result.chartFile,Buffer.from(excel.engine().RewardCore.xlsx('MissionEvents',rows)));
    }
    return result;
  });
  handler('content-workbook-import',p=>{
    if(!Array.isArray(p.bytes)||p.bytes.length>8*1024*1024)throw Error('Excel은 8MB 이하여야 합니다.');
    return excel.read(Buffer.from(p.bytes),p.project);
  });
  handler('content-library-import',p=>{
    if(typeof p.name!=='string'||!p.name.endsWith('.xlsx'))throw Error('자료 목록의 Excel을 선택하세요.');
    const root=path.resolve(store.excelFolder()),file=path.resolve(root,p.name);
    if(!file.startsWith(root+path.sep))throw Error('자료 폴더 안의 파일만 선택하세요.');
    let part=root;for(const component of path.relative(root,file).split(path.sep)){part=path.join(part,component);if(fs.lstatSync(part).isSymbolicLink())throw Error('바로가기는 지원하지 않습니다.');}
    const stat=fs.lstatSync(file);
    if(!stat.isFile()||stat.isSymbolicLink()||stat.size>8*1024*1024)throw Error('지원하지 않는 Excel입니다.');
    const bytes=fs.readFileSync(file);if(crypto.createHash('sha256').update(bytes).digest('hex')!==p.sha256)throw Error('파일이 바뀌었습니다. 자료 목록을 새로고침하세요.');
    const result=excel.read(bytes,current());atomic(draft,JSON.stringify(result.project,null,2));return result;
  });
  handler('content-load', () => fs.existsSync(draft) ? JSON.parse(fs.readFileSync(draft, 'utf8')) : null);
  handler('content-save', p => {
    if (p.project?.schemaVersion !== 2 || !p.project.tables) throw Error('지원하지 않는 콘텐츠 프로젝트입니다.');
    const data = JSON.stringify(p.project, null, 2);
    if (Buffer.byteLength(data) > 20 * 1024 * 1024) throw Error('프로젝트는 20MB 이하여야 합니다.');
    atomic(draft, data);
    return { saved: true };
  });
  handler('content-export', p => {
    if (typeof p.name !== 'string' || !/^[A-Za-z][A-Za-z0-9_-]*\.(json|xlsx|zip)$/.test(p.name)) throw Error('지원하지 않는 파일 이름입니다.');
    if (!Array.isArray(p.bytes) || p.bytes.length > 32 * 1024 * 1024 || p.bytes.some(n => !Number.isInteger(n) || n < 0 || n > 255)) throw Error('파일 형식 또는 크기 오류입니다.');
    const root = store.exportFolder();
    const folder = path.join(root, '콘텐츠-' + new Date().toISOString().replace(/[:.]/g, '-') + '-' + crypto.randomBytes(3).toString('hex'));
    fs.mkdirSync(folder, { recursive: true });
    const file = path.join(folder, p.name);
    atomic(file, Buffer.from(p.bytes));
    atomic(path.join(folder, '적용안내.txt'), '콘텐츠 기획안입니다. 기존 밸런스 Excel과 별도 규약입니다.\n서버 업로드·게임 적용은 수행하지 않았습니다.\nJSON은 편집 원본, XLSX/ZIP은 공통 데이터 설계입니다.\n');
    return { file };
  });
};
