'use strict';
// Explicit conversion for build tooling; never uploads or edits Unity assets.
const fs=require('node:fs'),path=require('node:path');
const excel=require('./app/content-workbook.cjs');
const [input,output]=process.argv.slice(2);
if(!input||!output){console.error('Usage: node parse-content.cjs input.xlsx output.json');process.exitCode=1;}
else{
 try{
  const data=excel.read(fs.readFileSync(path.resolve(input)),excel.engine().Studio.create());
  const result=excel.toRuntime(data.project,data.names);
  fs.writeFileSync(path.resolve(output),JSON.stringify(result,null,2),{flag:'wx'});
  console.log('Validated JSON: '+path.resolve(output));
 }catch(error){console.error(error.message);process.exitCode=1;}
}
