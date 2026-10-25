/* Shared data contract and dependency-free ZIP / OOXML export. */
(function (root) {
  'use strict';
  const encoder = new TextEncoder();
  const clone = value => JSON.parse(JSON.stringify(value));
  function createProject() {
    const days = [['Stamina',10],['Gold',500],['Diamond',10],['Stamina',15],['Gold',1000],['Diamond',20],['Diamond',50]];
    return {schemaVersion:1, name:'개꿀디펜스 보상 테스트', groups:days.map(([itemId,amount],i)=>({id:`AttendanceDay${String(i+1).padStart(2,'0')}`,name:`출석 ${i+1}일차`,rewards:[{itemId,amount}]})), attendance:{id:'BasicAttendance',resetHourKst:5,days:days.map((_,i)=>({day:i+1,groupId:`AttendanceDay${String(i+1).padStart(2,'0')}`}))}};
  }
  function validate(p, catalog, {draft=false}={}) {
    const errors=[];
    if (!p || p.schemaVersion!==1 || !Array.isArray(p.groups) || !p.attendance || !Array.isArray(p.attendance.days)) return ['지원하지 않는 프로젝트 형식입니다.'];
    const ids=new Set(), itemIds=new Set(catalog.map(i=>i.id));
    if (!p.groups.length) errors.push('보상 묶음이 하나 이상 필요합니다.');
    for (const g of p.groups) {
      if (!g || typeof g.id!=='string' || !/^[A-Za-z][A-Za-z0-9_]*$/.test(g.id)) { errors.push('보상 ID는 영문으로 시작하고 영문·숫자·밑줄만 사용할 수 있습니다.'); continue; }
      if(ids.has(g.id)) errors.push(`중복 보상 ID: ${g.id}`);
      ids.add(g.id);
      if(typeof g.name!=='string' || (!draft && !g.name.trim())) errors.push(`${g.id}: 이름을 입력해 주세요.`);
      if(!Array.isArray(g.rewards) || (!draft && !g.rewards.length)) {errors.push(`${g.id}: 아이템을 추가해 주세요.`);continue;}
      const used=new Set();
      for(const r of g.rewards) {
        if(!r || !itemIds.has(r.itemId)) {errors.push(`${g.id}: 알 수 없는 아이템입니다.`);continue;}
        if(used.has(r.itemId)) errors.push(`${g.id}: 중복 아이템 ${r.itemId}`);
        used.add(r.itemId);
        if(draft ? (r.amount!==null && (typeof r.amount!=='number' || !Number.isFinite(r.amount))) : (!Number.isInteger(r.amount) || r.amount<1 || r.amount>2147483647)) errors.push(`${g.id}: 수량은 1~2,147,483,647의 정수여야 합니다.`);
      }
    }
    if(typeof p.attendance.id!=='string' || (!draft && !/^[A-Za-z][A-Za-z0-9_]*$/.test(p.attendance.id))) errors.push('출석표 ID 형식을 확인해 주세요.');
    if(!Number.isInteger(p.attendance.resetHourKst) || p.attendance.resetHourKst<0 || p.attendance.resetHourKst>23) errors.push('초기화 시각은 0~23시여야 합니다.');
    if(p.attendance.days.length!==7) errors.push('테스트 버전의 출석표는 7일이어야 합니다.');
    p.attendance.days.forEach((d,i)=>{if(!d || d.day!==i+1 || !ids.has(d.groupId)) errors.push(`출석 ${i+1}일차: 일차 또는 보상 연결을 확인해 주세요.`);});
    return [...new Set(errors)];
  }
  function serialize(rewards) {return rewards.map(r=>`${r.itemId}:${r.amount}`).join(';');}
  function tables(p,catalog) {
    const errors=validate(p,catalog); if(errors.length) throw Error(errors.join('\n'));
    return {
      Item:[['ItemId','Name','ItemType','IconKey'],...catalog.map(i=>[i.id,i.name,i.type,i.iconKey])],
      RewardGroup:[['RewardGroupId','Name','Rewards'],...p.groups.map(g=>[g.id,g.name,serialize(g.rewards)])],
      Attendance:[['AttendanceId','Day','RewardGroupId','ResetHourKst'],...p.attendance.days.map(d=>[p.attendance.id,d.day,d.groupId,p.attendance.resetHourKst])]
    };
  }
  function concat(parts) {const out=new Uint8Array(parts.reduce((n,p)=>n+p.length,0));let pos=0;for(const p of parts){out.set(p,pos);pos+=p.length;}return out;}
  function crc32(data) {let c=0xffffffff;for(const b of data){c^=b;for(let i=0;i<8;i++)c=(c>>>1)^((c&1)?0xedb88320:0);}return (c^0xffffffff)>>>0;}
  function header(size,fields) {const bytes=new Uint8Array(size),v=new DataView(bytes.buffer);for(const [offset,value,width] of fields)width===2?v.setUint16(offset,value,true):v.setUint32(offset,value,true);return bytes;}
  function zip(entries) {
    const local=[],central=[];let offset=0;
    for(const [path,content] of Object.entries(entries)){
      const name=encoder.encode(path),data=typeof content==='string'?encoder.encode(content):content,crc=crc32(data);
      const head=header(30,[[0,0x04034b50,4],[4,20,2],[6,0x800,2],[12,33,2],[14,crc,4],[18,data.length,4],[22,data.length,4],[26,name.length,2]]);
      local.push(head,name,data);
      central.push(header(46,[[0,0x02014b50,4],[4,20,2],[6,20,2],[8,0x800,2],[14,33,2],[16,crc,4],[20,data.length,4],[24,data.length,4],[28,name.length,2],[42,offset,4]]),name);
      offset+=head.length+name.length+data.length;
    }
    const directory=concat(central),count=Object.keys(entries).length;
    return concat([...local,directory,header(22,[[0,0x06054b50,4],[8,count,2],[10,count,2],[12,directory.length,4],[16,offset,4]])]);
  }
  const xml=s=>String(s).replace(/[\u0000-\u0008\u000B\u000C\u000E-\u001F]/g,'').replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/"/g,'&quot;').replace(/'/g,'&apos;');
  function xlsx(name,rows) {
    const sheet=rows.map((row,r)=>`<row r="${r+1}">${row.map((value,c)=>{
      let column='',columnIndex=c+1;
      while(columnIndex>0){columnIndex--;column=String.fromCharCode(65+columnIndex%26)+column;columnIndex=Math.floor(columnIndex/26);}
      const ref=column+(r+1);
      return typeof value==='number'?`<c r="${ref}"><v>${value}</v></c>`:`<c r="${ref}" t="inlineStr"><is><t xml:space="preserve">${xml(value)}</t></is></c>`;
    }).join('')}</row>`).join('');
    return zip({
      '[Content_Types].xml':'<?xml version="1.0" encoding="UTF-8"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/></Types>',
      '_rels/.rels':'<?xml version="1.0" encoding="UTF-8"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>',
      'xl/workbook.xml':`<?xml version="1.0" encoding="UTF-8"?><workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="${xml(name)}" sheetId="1" r:id="rId1"/></sheets></workbook>`,
      'xl/_rels/workbook.xml.rels':'<?xml version="1.0" encoding="UTF-8"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/></Relationships>',
      'xl/worksheets/sheet1.xml':`<?xml version="1.0" encoding="UTF-8"?><worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetViews><sheetView workbookViewId="0"><pane ySplit="1" topLeftCell="A2" activePane="bottomLeft" state="frozen"/></sheetView></sheetViews><cols><col min="1" max="4" width="28" customWidth="1"/></cols><sheetData>${sheet}</sheetData></worksheet>`
    });
  }
  const api={clone,createProject,validate,serialize,tables,zip,xlsx};
  root.RewardCore=api;
  if(typeof module!=='undefined') module.exports=api;
})(globalThis);
