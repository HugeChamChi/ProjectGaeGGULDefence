'use strict';
const fs=require('node:fs'),path=require('node:path');
const excel=require('./content-workbook.cjs');
function tables(project){
  const e=excel.engine(),p=e.Studio.shape(project),errors=e.Studio.validate(p).filter(i=>i.severity==='error');
  if(errors.length)throw Error(errors[0].message);
  // The exported price tables must match the selected Function parameters exactly.
  const generated=e.Studio.clone(p);e.OutgameEconomy.apply(generated);
  for(const name of ['UnitUpgrade','ResearchNode']){
    const relevant=name==='UnitUpgrade'?r=>p.tables.Unit.some(u=>u.Kind==='Unit'&&u.UnitId===r.UnitId):r=>r.TreeKey==='BASIC';
    const key=name==='UnitUpgrade'?r=>r.UnitId+':'+r.TargetLevel:r=>r.NodeId;
    const cost=name==='UnitUpgrade'?r=>r.Costs:r=>[r.CostItemId,r.BaseCost,r.CostStep];
    const expected=new Map(generated.tables[name].filter(relevant).map(r=>[key(r),JSON.stringify(cost(r))]));
    const rows=p.tables[name].filter(relevant);
    if(rows.length!==expected.size||rows.some(r=>expected.get(key(r))!==JSON.stringify(cost(r))))throw Error('공식과 비용표가 다릅니다. 체력 연동 경제에서 비용표를 다시 생성하세요.');
  }
  const readable=require('./readable-chart.cjs').table;
  return {OutgameEconomy:e.Studio.tables(p).OutgameEconomy,
    UnitBaseStats:readable(require('./unit-runtime.cjs').unitRuntime(p),'unitBaseStats'),ResearchBalance:readable(p,'research')};
}
function write(project,{exportFile,atomic}){
  const e=excel.engine(),charts=tables(project);let result;
  for(const [name,rows]of Object.entries(charts)){
    const bytes=Buffer.from(e.RewardCore.xlsx(name,rows));
    if(!result){result=exportFile(name+'.backend.xlsx',bytes);result.chartFiles=[result.file];}
    else{const file=path.join(path.dirname(result.file),name+'.backend.xlsx');atomic(file,bytes);result.chartFiles.push(file);}
  }
  const folder=path.dirname(result.file),functionFolder=path.join(folder,'Function');fs.mkdirSync(functionFolder,{recursive:true});
  for(const name of ['OutgameEconomy.cs','README.md'])atomic(path.join(functionFolder,name),fs.readFileSync(path.join(__dirname,'Function',name)));
  atomic(path.join(folder,'source-project.json'),JSON.stringify(e.Studio.shape(project),null,2));
  return {...result,functionFiles:['OutgameEconomy.cs','README.md'].map(n=>path.join(functionFolder,n))};
}
module.exports={tables,write};
