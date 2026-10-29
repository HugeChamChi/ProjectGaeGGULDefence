import fs from 'node:fs';
import vm from 'node:vm';

// Update only bundled seed addresses; never overwrite a planner's saved project.
const seedFile='tools/planner-studio/app/ui/content/seed.js';
const code=fs.readFileSync(seedFile,'utf8');
const context=vm.createContext({}); vm.runInContext(code,context);
const rows=JSON.parse(fs.readFileSync('Assets/WorkSpace/USW/Resources/ResearchBalance.json','utf8'));
const byNode=new Map(rows.map(r=>[r.TreeKey+'/'+r.NodeId,r]));
for (const node of context.STUDIO_SEED.tables.ResearchNode) {
  const row=byNode.get(node.TreeKey+'/'+node.NodeId);
  if (!row) throw Error('Missing source research node');
  node.IconAddress=row.IconAddress;
}
context.STUDIO_SEED.meta.statuses.ResearchNode='ResearchBalance 차트·Addressables 아이콘 연결. 현재 포함 차트 사용, 원격 전환은 업로드 후 설정. 무료 강화·로컬 진행 저장 유지.';
const start=code.indexOf('globalThis.STUDIO_SEED = '), end=code.indexOf('\n',start)-1;
if(start!==0||end<0) throw Error('Unexpected seed layout');
fs.writeFileSync(seedFile,'globalThis.STUDIO_SEED = '+JSON.stringify(context.STUDIO_SEED)+';'+code.slice(end+1));
console.log('Updated seed addresses for '+byNode.size+' nodes; user files unchanged.');
