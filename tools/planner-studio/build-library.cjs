'use strict';
const fs=require('node:fs'),path=require('node:path');
const crypto=require('node:crypto'),{unzipSync}=require('./app/node_modules/fflate');
const excel=require('./app/content-workbook.cjs'),project=excel.engine().Studio.create();
const definitions={
 'UnitBalance.xlsx':['Unit','UnitBaseStat','UnitUpgrade','UnitMilestone','BonusDefinition','BonusEffect'],
 'PermanentUpgrade.xlsx':['ResearchTree','ResearchNode'],
 'MissionEvents.xlsx':['MissionEvents','Missions'],
 'Party.xlsx':['Party'],
 'AccountSettings.xlsx':['Economy'],
 'Profile.xlsx':['Profile'],
 'Tutorial.xlsx':['Tutorial'],
 'ShopCatalog.xlsx':['ShopCatalog']
};
const manifestFile=path.join(__dirname,'app/seeds/project-library-manifest.json');
const prior=fs.existsSync(manifestFile)?JSON.parse(fs.readFileSync(manifestFile,'utf8')):{files:{}};
function fingerprint(bytes){const parts=unzipSync(bytes),hash=crypto.createHash('sha256');for(const name of Object.keys(parts).sort())hash.update(name+'\0').update(parts[name]);return hash.digest('hex');}
const manifest={version:4,files:{}};
for(const [name,tables]of Object.entries(definitions)){
 const file=path.join(__dirname,'app/seeds',name),history=prior.files[name]?.previousFingerprints||[];
 if(fs.existsSync(file))history.push(fingerprint(fs.readFileSync(file)));
 let data=project;
 const bytes=excel.write(data,tables);excel.read(bytes,data);
 fs.writeFileSync(file,bytes);
 manifest.files[name]={fingerprint:fingerprint(bytes),previousFingerprints:[...new Set(history)].slice(-20)};
}
fs.writeFileSync(manifestFile,JSON.stringify(manifest,null,2));
console.log('Project snapshot workbooks: '+Object.keys(definitions).length);
