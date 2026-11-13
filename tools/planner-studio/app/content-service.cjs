'use strict';
const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');

exports.register = ({ handler, workspace, store, atomic }) => {
  handler('content-icon-prepare',p=>{if(!Array.isArray(p.bytes)||p.bytes.length>1024*1024||p.bytes.some(n=>!Number.isInteger(n)||n<0||n>255))throw Error('PNG 파일을 확인하세요.');const result=require('./item-icons.cjs').prepare(p.name,p.bytes);if(require('electron').nativeImage.createFromBuffer(Buffer.from(p.bytes)).isEmpty())throw Error('PNG 이미지를 읽을 수 없습니다.');return result;});
  const draft = path.join(workspace, 'content-project-v2.json');
  const excel=require('./content-workbook.cjs');
  const current=()=>excel.engine().Studio.shape(fs.existsSync(draft)?JSON.parse(fs.readFileSync(draft,'utf8')):excel.engine().Studio.create());
  function exportFile(name,bytes){
    const folder=path.join(store.exportFolder(),'콘텐츠-'+new Date().toISOString().replace(/[:.]/g,'-')+'-'+crypto.randomBytes(3).toString('hex'));
    fs.mkdirSync(folder,{recursive:true});const file=path.join(folder,name);atomic(file,bytes);return {file};
  }
  require('./backend-deployment.cjs').register({handler,workspace,atomic,exportFile,getCurrentProject:current});
  handler('content-workbook-export',p=>{
    const e=excel.engine(), project=e.Studio.shape(p.project);
    const errors=e.Studio.validate(project).filter(i=>i.severity==='error');
    if(errors.length)throw Error(errors[0].message);
    let tables;
    const readable=require('./readable-chart.cjs').table;
    if(p.bundle==='missions'||p.bundle==='rewards')tables=require('./event-tables.cjs').tables(project);
    else if(p.bundle==='bonuses')tables={UnitLevelBonuses:readable(project,'unitBonuses')};
    else if(['Unit','UnitUpgrade','UnitBaseStat','UnitMilestone'].includes(p.table))tables={UnitBaseStats:readable(require('./unit-runtime.cjs').unitRuntime(project),'unitBaseStats')};
    else if(p.table==='ShopCatalog')tables={...e.ShopCatalogTools.listTables(project),...e.ShopCatalogTools.probabilityTables(project)};
    else {
      const all=e.Studio.tables(project);
      const names=p.bundle==='items'?excel.bundles.items:['ResearchTree','ResearchNode'].includes(p.table)?['ResearchTree','ResearchNode','ResearchLevel']:[p.table];
      tables=Object.fromEntries(names.map(name=>[name,all[name]]));
    }
    let result;
    for(const [name,rows] of Object.entries(tables)){
      if(!rows)throw Error('지원하지 않는 백엔드 표: '+name);
      const bytes=Buffer.from(e.RewardCore.xlsx(name,rows));
      if(!result){result=exportFile(name+'.backend.xlsx',bytes);result.chartFiles=[result.file];}
      else {const file=path.join(path.dirname(result.file),name+'.backend.xlsx');atomic(file,bytes);result.chartFiles.push(file);}
    }
    require('./item-icons.cjs').exportIcons(project,path.dirname(result.file));
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
  handler('content-load', () => {
    if(!fs.existsSync(draft))return null;
    const raw=JSON.parse(fs.readFileSync(draft,'utf8'));
    if(raw.tables?.AttendancePlan?.length){const backup=path.join(workspace,'content-project-before-event-missions.json');if(!fs.existsSync(backup))atomic(backup,JSON.stringify(raw,null,2));}
    return raw;
  });
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
