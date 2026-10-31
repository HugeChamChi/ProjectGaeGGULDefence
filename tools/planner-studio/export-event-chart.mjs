import fs from 'node:fs';
import path from 'node:path';
import {createRequire} from 'node:module';
const require=createRequire(import.meta.url),workbook=require('./app/content-workbook.cjs');
const [input,directory]=process.argv.slice(2);
if(!input||!directory)throw Error('Usage: node export-event-chart.mjs <project.json> <new-directory>');
if(fs.existsSync(directory))throw Error('Output directory exists');
const engine=workbook.engine(),project=engine.Studio.shape(JSON.parse(fs.readFileSync(input,'utf8')));
const errors=engine.Studio.validate(project).filter(i=>i.severity==='error');if(errors.length)throw Error(JSON.stringify(errors));
fs.mkdirSync(directory,{recursive:true});
const tables=require('./app/event-tables.cjs').tables(project);
for(const [name,rows]of Object.entries(tables)){fs.writeFileSync(path.join(directory,name+'.xlsx'),Buffer.from(engine.RewardCore.xlsx(name,rows)));fs.writeFileSync(path.join(directory,name+'.rows.json'),JSON.stringify(rows.slice(1).map(row=>Object.fromEntries(rows[0].map((key,i)=>[key,row[i]]))),null,2));}
fs.writeFileSync(path.join(directory,'EventMissions.authoring.xlsx'),workbook.write(project,workbook.bundles.missions));
fs.writeFileSync(path.join(directory,'project.json'),JSON.stringify(project,null,2));
console.log(directory);
