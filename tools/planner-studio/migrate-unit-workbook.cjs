'use strict';
const fs=require('node:fs'),path=require('node:path');
const excel=require('./app/content-workbook.cjs'),{atomic}=require('./app/store.cjs');
const root=path.join(__dirname,'GGD-Planner/자료'),backup=path.join(__dirname,'backups','unit-keys-'+new Date().toISOString().replace(/[:.]/g,'-'));
fs.mkdirSync(backup,{recursive:true});let p=excel.engine().Studio.create();
for(const name of ['UnitBalance.xlsx','UnitLevelBonus.xlsx']){
 const file=path.join(root,name);if(fs.existsSync(file)){const data=fs.readFileSync(file);fs.copyFileSync(file,path.join(backup,name));p=excel.read(data,p).project;}
}
const bytes=excel.write(p,['Unit','UnitBaseStat','UnitUpgrade','UnitMilestone']);excel.read(bytes,p);atomic(path.join(root,'UnitBalance.xlsx'),bytes);
for(const relative of ['GGD-Planner/자료/UnitLevelBonus.xlsx','app/seeds/UnitLevelBonus.xlsx']){
 const file=path.resolve(__dirname,relative);if(!file.startsWith(path.resolve(__dirname)+path.sep))throw Error('Unexpected path');
 if(fs.existsSync(file)){const name=relative.startsWith('app/')?'source-UnitLevelBonus.xlsx':'retired-UnitLevelBonus.xlsx';fs.renameSync(file,path.join(backup,name));}
}
console.log(JSON.stringify({ok:true,backup,sheets:excel.inspect(bytes).names},null,2));
