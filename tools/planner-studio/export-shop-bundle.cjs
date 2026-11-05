'use strict';
// Use the same export handler as the planner, with an explicit local destination.
const fs=require('node:fs'),path=require('node:path');
const target=path.resolve(process.argv[2]||'outputs/shop-chart/export');
fs.mkdirSync(target,{recursive:true});
const excel=require('./app/content-workbook.cjs'),project=excel.engine().Studio.create(),handlers={};
const work=path.resolve(__dirname,'../../outputs/shop-chart/generated',String(Date.now()));
require('./app/content-service.cjs').register({handler:(name,action)=>handlers[name]=action,workspace:work,store:{exportFolder:()=>work},atomic:(file,data)=>{fs.mkdirSync(path.dirname(file),{recursive:true});fs.writeFileSync(file,data);}});
const result=handlers['content-workbook-export']({project,table:'ShopCatalog'});
fs.writeFileSync(path.join(path.dirname(result.file),'shop-project.json'),JSON.stringify(project,null,2));
for(const name of fs.readdirSync(path.dirname(result.file))) {
  const source=path.join(path.dirname(result.file),name);
  if(fs.statSync(source).isFile())fs.copyFileSync(source,path.join(target,name));
}
console.log(JSON.stringify({folder:target,files:[result.file,...result.chartFiles,...result.probabilityFiles].map(f=>path.join(target,path.basename(f))),preview:path.join(target,path.basename(result.previewFile))},null,2));
