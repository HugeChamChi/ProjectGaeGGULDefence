'use strict';
const crypto=require('node:crypto'),fs=require('node:fs'),path=require('node:path');
function prepare(name,bytes){
 const data=Buffer.from(bytes);
 if(data.length<33||data.length>1024*1024||data.subarray(0,8).toString('hex')!=='89504e470d0a1a0a'||data.toString('ascii',12,16)!=='IHDR')throw Error('1MB 이하의 PNG 파일을 선택하세요.');
 const w=data.readUInt32BE(16),h=data.readUInt32BE(20);if(!w||!h||w>4096||h>4096)throw Error('PNG 크기는 1~4096픽셀이어야 합니다.');
 const sha256=crypto.createHash('sha256').update(data).digest('hex');
 const stem=path.basename(String(name),path.extname(String(name))).replace(/[^A-Za-z0-9_-]/g,'_').slice(0,60)||'icon';
 return {address:'ItemIcons/'+stem+'_'+sha256.slice(0,8),png:data.toString('base64'),sha256};
}
function exportIcons(project,folder){
 const icons=new Map();
 for(const item of project.tables.Item){if(!item.IconPng)continue;const data=Buffer.from(item.IconPng,'base64');const check=prepare('icon.png',data);if(!/^ItemIcons\/[A-Za-z0-9_-]+$/.test(item.IconAddress))throw Error('PNG 아이콘 주소가 올바르지 않습니다: '+item.ItemId);const filename=item.IconAddress.split('/')[1]+'.png';if(icons.has(filename)&&icons.get(filename).sha256!==check.sha256)throw Error('같은 주소에 다른 PNG가 연결되어 있습니다.');icons.set(filename,{address:item.IconAddress,file:filename,sha256:check.sha256,data});}
 if(!icons.size)return;
 const root=path.join(folder,'ItemIcons');fs.mkdirSync(root,{recursive:true});
 for(const icon of icons.values())fs.writeFileSync(path.join(root,icon.file),icon.data);
 fs.writeFileSync(path.join(root,'item-icons.json'),JSON.stringify({version:1,icons:[...icons.values()].map(({data,...r})=>r)},null,2));
}
module.exports={prepare,exportIcons};
