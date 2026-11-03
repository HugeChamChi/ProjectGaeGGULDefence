import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { fileURLToPath } from 'node:url';
import { projectRoot, localFile, readJson } from './balance.mjs';
import { chartTarget } from './deployment.mjs';
import { writeChartXlsx, readChartXlsx } from './xlsx.mjs';

export const researchColumns = ['SchemaVersion','ReleaseKey','TreeKey','TreeName','LegacySaveName','ShowLanes','ColumnSpacing','LaneNames','NodeId','Stat','IconAddress','Shape','MaxLevel','ValuePerLevel','Row','Column','Parents','RequireAnyParent','RequiredTotalLevel','ExclusiveGroup','RecommendOrder'];
const stats = ['AttackPercent','AttackSpeedPercent','CritChance','CritDamage','SkillCooldownReduction','FoodProduction','StartFood','BossDamage','BurnDamage','TotemEffect'];
const hash = bytes => crypto.createHash('sha256').update(bytes).digest('hex');

export function validateResearch(rows) {
  if (!Array.isArray(rows) || !rows.length || rows.length > 20000) throw Error('ResearchBalance는 1~20000행이 필요합니다.');
  const trees = new Map(); let release;
  for (const row of rows) {
    for (const column of researchColumns) if (!Object.hasOwn(row,column)) throw Error('열 누락: '+column);
    for (const key of ['ReleaseKey','TreeKey','TreeName','NodeId','IconAddress'])
      if (typeof row[key] !== 'string' || !row[key].trim()) throw Error('필수 문자열: '+key);
    for (const key of ['LegacySaveName','ExclusiveGroup']) if (typeof row[key] !== 'string') throw Error('문자열 필요: '+key);
    for (const [key,min,max] of [['SchemaVersion',1,1],['MaxLevel',1,1000],['Row',0,200],['Column',-1,2],['RequiredTotalLevel',0,20000000],['RecommendOrder',0,20000000]])
      if (!/^[-+]?\d+$/.test(String(row[key])) || !Number.isInteger(Number(row[key])) || Number(row[key])<min || Number(row[key])>max) throw Error('정수 범위 오류: '+key);
    for (const key of ['ColumnSpacing','ValuePerLevel'])
      if (String(row[key]).trim()==='' || !Number.isFinite(Number(row[key])) || Number(row[key])<0 || Number(row[key])>3.4028234663852886e38) throw Error('숫자 범위 오류: '+key);
    if (Number(row.ValuePerLevel)*Number(row.MaxLevel)>3.4028234663852886e38) throw Error('수치 합계 범위 초과');
    for (const key of ['ShowLanes','RequireAnyParent']) if (!['true','false','True','False','1','0'].includes(String(row[key]))) throw Error('boolean 오류: '+key);
    if (!stats.includes(row.Stat) || !['Square','Circle','Special'].includes(row.Shape)) throw Error('미지원 스탯 또는 모양');
    const arrays = {};
    for (const key of ['LaneNames','Parents']) {
      arrays[key] = JSON.parse(row[key]);
      if (!Array.isArray(arrays[key]) || arrays[key].some(v=>typeof v!=='string'||!v.trim()) || new Set(arrays[key]).size!==arrays[key].length) throw Error('목록 오류: '+key);
    }
    if (['true','True','1'].includes(String(row.ShowLanes)) && arrays.LaneNames.length!==3) throw Error('줄기 이름 3개 필요');
    if (release && row.ReleaseKey!==release) throw Error('ReleaseKey 혼합');
    release=row.ReleaseKey;
    const metadata=JSON.stringify(researchColumns.slice(3,8).map(k=>row[k]));
    if (!trees.has(row.TreeKey)) trees.set(row.TreeKey,{metadata,nodes:new Map(),positions:new Set()});
    const tree=trees.get(row.TreeKey), position=row.Row+':'+row.Column;
    if (tree.metadata!==metadata) throw Error('트리 설정 불일치: '+row.TreeKey);
    if (tree.nodes.has(row.NodeId)||tree.positions.has(position)) throw Error('중복 노드/위치');
    tree.nodes.set(row.NodeId,arrays.Parents); tree.positions.add(position);
    for (const key of researchColumns) if (String(row[key]).includes('\\')) throw Error('뒤끝 차트 역슬래시 금지: '+key);
  }
  for (const tree of trees.values()) {
    const visiting=new Set(),done=new Set();
    function visit(id) {
      if (done.has(id)) return;
      if (!tree.nodes.has(id)) throw Error('선행 노드 누락: '+id);
      if (visiting.has(id)) throw Error('선행 노드 순환');
      visiting.add(id); for (const p of tree.nodes.get(id)) visit(p);
      visiting.delete(id); done.add(id);
    }
    for (const id of tree.nodes.keys()) visit(id);
  }
  return {valid:true,trees:trees.size,nodes:rows.length,releaseKey:release};
}

export function researchFromProject(project, releaseKey) {
  if (project?.schemaVersion!==2 || !Array.isArray(project.tables?.ResearchTree) || !Array.isArray(project.tables?.ResearchNode)) throw Error('기획 프로젝트 v2 필요');
  const trees = new Map();
  for (const tree of project.tables.ResearchTree) {
    if (trees.has(tree.TreeKey)) throw Error('중복 트리');
    trees.set(tree.TreeKey,tree);
  }
  const rows=project.tables.ResearchNode.map(n=>{
    const t=trees.get(n.TreeKey); if (!t) throw Error('트리 누락: '+n.TreeKey);
    if (n.CostItemId || n.BaseCost!==0 || n.CostStep!==0) throw Error('현재 강화는 무료입니다. 비용 기능 구현 전에는 유료 설정을 배포할 수 없습니다.');
    return {SchemaVersion:1,ReleaseKey:releaseKey,TreeKey:t.TreeKey,TreeName:t.Name,LegacySaveName:t.LegacySaveName,
      ShowLanes:String(t.ShowLanes),ColumnSpacing:t.ColumnSpacing,LaneNames:JSON.stringify(t.LaneNames),
      ...Object.fromEntries(['NodeId','Stat','IconAddress','Shape','MaxLevel','ValuePerLevel','Row','Column','RequiredTotalLevel','ExclusiveGroup','RecommendOrder'].map(k=>[k,n[k]])),
      Parents:JSON.stringify(n.Parents),RequireAnyParent:String(n.RequireAnyParent)};
  });
  if (new Set(rows.map(r=>r.TreeKey)).size!==trees.size) throw Error('노드 없는 트리');
  validateResearch(rows); return rows;
}

export function readResearch(file, releaseKey, root=projectRoot) {
  let rows;
  if (/\.xlsx$/i.test(file)) {
    const cells=readChartXlsx(fs.readFileSync(localFile(file,root)),'ResearchBalance');
    if (JSON.stringify(cells[0])!==JSON.stringify(researchColumns)) throw Error('ResearchBalance 열 이름/순서 불일치');
    rows=cells.slice(1).map(values=>Object.fromEntries(cells[0].map((k,i)=>[k,values[i]])));
  } else {
    const input=readJson(file,root);
    rows=Array.isArray(input)?input:researchFromProject(input,releaseKey || 'validation');
  }
  // LitJson escapes Korean inside nested JSON strings; upload uses the same decoded list in UTF-8.
  rows=rows.map(row=>({...row,LaneNames:JSON.stringify(JSON.parse(row.LaneNames)),Parents:JSON.stringify(JSON.parse(row.Parents))}));
  validateResearch(rows);
  return releaseKey ? rows.map(row=>({...row,ReleaseKey:releaseKey})) : rows;
}

export async function exportResearch(file, releaseKey, environment='DEV', root=projectRoot, previews=false) {
  const target=chartTarget(environment);
  if (!/^[A-Za-z0-9][A-Za-z0-9_-]{0,63}$/.test(releaseKey)) throw Error('ReleaseKey 형식 오류');
  const rows=readResearch(file,releaseKey,root), realRoot=fs.realpathSync(root);
  let base=realRoot;
  for (const part of ['outputs','research-charts']) {
    base=path.join(base,part); if (!fs.existsSync(base)) fs.mkdirSync(base);
    const relative=path.relative(realRoot,fs.realpathSync(base));
    if (relative==='..'||relative.startsWith('..'+path.sep)||path.isAbsolute(relative)) throw Error('출력 경로 이탈');
  }
  const directory=path.join(base,releaseKey); fs.mkdirSync(directory);
  const xlsx=path.join(directory,'ResearchBalance.xlsx');
  await writeChartXlsx('ResearchBalance',[researchColumns,...rows.map(r=>researchColumns.map(k=>r[k]))],xlsx,previews?path.join(directory,'preview.png'):undefined);
  const reread=readResearch(xlsx,undefined,root);
  const canonical=data=>JSON.stringify(data.map(r=>researchColumns.map(k=>String(r[k]))));
  if (canonical(reread)!==canonical(rows)) throw Error('Research Excel 왕복 불일치');
  fs.writeFileSync(path.join(directory,'ResearchBalance.json'),JSON.stringify(rows,null,2)+'\n',{flag:'wx'});
  const manifest={schemaVersion:1,...target,releaseKey,...validateResearch(rows),file:'ResearchBalance.xlsx',
    targetChartName:target.chartPrefix+'ResearchBalance',sha256:hash(fs.readFileSync(xlsx)),sourceSha256:hash(fs.readFileSync(localFile(file,root))),
    remoteApplied:false,note:'같은 파일을 DEV에서 검증한 후 LIVE_ResearchBalance에 반영합니다. 아이콘 주소는 빌드에 포함된 Addressables 카탈로그에 있어야 합니다.'};
  fs.writeFileSync(path.join(directory,'manifest.json'),JSON.stringify(manifest,null,2)+'\n',{flag:'wx'});
  return {directory,...manifest};
}

if (process.argv[1] && path.resolve(process.argv[1])===fileURLToPath(import.meta.url)) {
  try { console.log(JSON.stringify(await exportResearch(process.argv[2],process.argv[3],process.argv[4]??'DEV',projectRoot,process.argv.includes('--preview')),null,2)); }
  catch(error) {console.error(error.message);process.exitCode=1;}
}
