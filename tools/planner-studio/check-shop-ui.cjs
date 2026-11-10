'use strict';
const {execFileSync}=require('node:child_process');
const fs=require('node:fs'),path=require('node:path'),os=require('node:os'),assert=require('node:assert/strict');
const workspace=fs.mkdtempSync(path.join(os.tmpdir(),'ggd-shop-ui-')),result=path.join(workspace,'result.json');
try{execFileSync(process.env.GGD_TEST_EXE||path.join(__dirname,'GGD-Planner/GGD-Planner.exe'),['--workspace='+workspace,'--shop-smoke='+result],{windowsHide:true,timeout:30000,stdio:'ignore'});}catch(error){if(!fs.existsSync(result))throw error;}
const report=JSON.parse(fs.readFileSync(result,'utf8'));assert(report.ok,report.error);
const output=path.resolve(__dirname,'../../outputs/shop-chart');fs.mkdirSync(output,{recursive:true});
fs.copyFileSync(report.screenshot,path.join(output,'planner-shop.png'));fs.writeFileSync(path.join(output,'planner-checks.json'),JSON.stringify(report,null,2));
console.log(JSON.stringify(report,null,2));
