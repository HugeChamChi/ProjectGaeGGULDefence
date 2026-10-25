(()=>{
  'use strict';
  const S=STUDIO_SCHEMAS,clone=RewardCore.clone;
  const fields=name=>S[name].fields;
  const primary=name=>S[name].keys[0];
  const recordKey=(name,row)=>JSON.stringify(S[name].keys.map(k=>row[k]));
  const label=(name,row)=>row.Name||row.Description||row.Instruction||row[primary(name)]||S[name].label;
  const empty=(type)=>['strings','refs','parents','rewards','costs'].includes(type)?[]:type==='bool'?false:['int','float'].includes(type)?0:'';
  function create(){return clone(STUDIO_SEED);}
  function shape(p){
    if(!p||p.schemaVersion!==2||typeof p.name!=='string'||!p.tables||typeof p.tables!=='object')throw Error('통합 프로젝트 형식(v2)이 아닙니다.');
    const clean=create();clean.name=p.name;
    for(const [name,schema] of Object.entries(S)){
      const rows=p.tables[name]??[];if(!Array.isArray(rows)||rows.length>20000)throw Error(`${schema.label}: 목록 형식/행 수가 올바르지 않습니다.`);
      clean.tables[name]=rows.map((row,index)=>{
        if(!row||typeof row!=='object'||Array.isArray(row))throw Error(`${name} ${index+1}행 형식 오류`);
        const result={};
        for(const f of schema.fields){
          const value=Object.hasOwn(row,f.key)?row[f.key]:empty(f.type);
          if(['rewards','costs'].includes(f.type)){
            if(!Array.isArray(value)||value.length>500||value.some(r=>!r||typeof r.itemId!=='string'||(r.amount!==null&&(typeof r.amount!=='number'||!Number.isFinite(r.amount)))))throw Error(`${name}: 아이템 목록 형식 오류`);
            result[f.key]=value.map(r=>({itemId:r.itemId,amount:r.amount}));
          }else if(['strings','refs','parents'].includes(f.type)){
            if(!Array.isArray(value)||value.some(x=>typeof x!=='string'))throw Error(`${name}: ${f.key} 목록 형식 오류`);
            result[f.key]=[...value];
          }else if(['int','float'].includes(f.type)){
            if(value!==null&&(typeof value!=='number'||!Number.isFinite(value)))throw Error(`${name}: ${f.key} 숫자 형식 오류`);
            result[f.key]=value;
          }else if(f.type==='bool'){
            if(typeof value!=='boolean')throw Error(`${name}: ${f.key} 체크 값 형식 오류`);
            result[f.key]=value;
          }else{
            if(typeof value!=='string')throw Error(`${name}: ${f.key} 문자열 형식 오류`);
            result[f.key]=value;
          }
        }
        return result;
      });
    }
    return clean;
  }
  function migrate(old){
    if(old.schemaVersion===2)return shape(old);
    if(old.schemaVersion!==1||!Array.isArray(old.groups)||!old.attendance)throw Error('지원하지 않는 프로젝트 파일입니다.');
    const p=create();
    p.tables.RewardGroup=old.groups.map(g=>({RewardGroupId:g.id,Name:g.name,Rewards:g.rewards,Source:'v1 프로젝트에서 이관'}));
    p.tables.AttendancePlan=[{AttendanceId:old.attendance.id,Name:'불러온 출석표',ResetHourKst:old.attendance.resetHourKst,CycleDays:old.attendance.days.length,Source:'v1 프로젝트에서 이관'}];
    p.tables.Attendance=old.attendance.days.map(d=>({AttendanceId:old.attendance.id,Day:d.day,RewardGroupId:d.groupId,Source:'v1 프로젝트에서 이관'}));
    return shape(p);
  }
  function newRecord(name,p){
    const row=Object.fromEntries(fields(name).map(f=>[f.key,empty(f.type)]));
    for(const f of fields(name)){
      if(f.type==='enum')row[f.key]=f.values[0];
      if(f.type==='stat')row[f.key]=STUDIO_SEED.meta.stats[0].value;
      if(f.type==='ref'&&f.required)row[f.key]=p.tables[f.table][0]?.[primary(f.table)]||'';
      if(f.type==='int')row[f.key]=Math.max(0,f.min??0);
      if(f.type==='id'){let n=1;while(p.tables[name].some(r=>r[f.key]===name+n))n++;row[f.key]=name+n;}
      if(f.key==='Name')row[f.key]='새 '+S[name].label;
    }
    row.Source='신규 기획';
    if(name==='ResearchNode'){row.NodeId='N'+(p.tables[name].length+1);row.Shape='Square';row.Row=1+Math.max(-1,...p.tables[name].filter(n=>n.TreeKey===row.TreeKey).map(n=>n.Row));row.Column=0;row.MaxLevel=3;}
    if(name==='Attendance')row.Day=1+Math.max(0,...p.tables[name].filter(r=>r.AttendanceId===row.AttendanceId).map(r=>r.Day));
    if(name==='AccountLevel')row.Level=1+Math.max(0,...p.tables[name].map(r=>r.Level));
    return row;
  }
  function references(p,name,index){
    const row=p.tables[name][index],key=primary(name),value=row[key],found=[];
    for(const [other,schema] of Object.entries(S))p.tables[other].forEach((r,i)=>{
      if(other===name&&i===index)return;
      for(const f of schema.fields){
        if(f.table===name&&((f.type==='ref'&&r[f.key]===value)||(f.type==='refs'&&r[f.key].includes(value))))found.push(`${schema.label} ${i+1}행`);
        if(name==='Item'&&['rewards','costs'].includes(f.type)&&r[f.key].some(x=>x.itemId===value))found.push(`${schema.label} ${i+1}행`);
        if(name==='ResearchNode'&&other==='ResearchNode'&&r.TreeKey===row.TreeKey&&r.Parents.includes(row.NodeId))found.push(`선행 참조: ${r.NodeId}`);
      }
    });
    return [...new Set(found)];
  }
  function update(p,name,index,key,value){
    const row=p.tables[name][index],old=row[key];
    if(key===primary(name)&&old!==value){
      for(const [other,schema] of Object.entries(S))for(const r of p.tables[other])for(const f of schema.fields){
        if(f.table===name&&f.type==='ref'&&r[f.key]===old)r[f.key]=value;
        if(f.table===name&&f.type==='refs')r[f.key]=r[f.key].map(v=>v===old?value:v);
        if(name==='Item'&&['rewards','costs'].includes(f.type))for(const a of r[f.key])if(a.itemId===old)a.itemId=value;
      }
    }
    if(name==='ResearchNode'&&key==='NodeId')for(const n of p.tables.ResearchNode)if(n.TreeKey===row.TreeKey)n.Parents=n.Parents.map(v=>v===old?value:v);
    row[key]=value;
  }
  function validate(p){
    const issues=[],add=(table,index,field,message,severity='error')=>issues.push({table,index,field,message,severity});
    for(const [name,schema] of Object.entries(S)){
      const keys=new Set();
      p.tables[name].forEach((r,index)=>{
        const composite=recordKey(name,r);if(keys.has(composite))add(name,index,schema.keys[0],'중복 키입니다.');keys.add(composite);
        if(schema.readonly)return;
        for(const f of schema.fields){const v=r[f.key];
          if(f.required&&(typeof v!=='string'||!v.trim()))add(name,index,f.key,'필수 항목을 입력하세요.');
          if(f.type==='id'&&!/^[A-Za-z][A-Za-z0-9_]*$/.test(v))add(name,index,f.key,'영문 시작, 영문·숫자·밑줄을 사용하세요.');
          if(f.type==='nodeid'&&!/^[A-Za-z0-9][A-Za-z0-9_-]*$/.test(v))add(name,index,f.key,'노드 ID에 사용할 수 없는 문자가 있습니다.');
          if(['int','float'].includes(f.type)&&(!Number.isFinite(v)||(f.type==='int'&&!Number.isInteger(v))||v<(f.min??-Infinity)||v>(f.max??Number.MAX_SAFE_INTEGER)))add(name,index,f.key,'숫자 형식 또는 허용 범위를 확인하세요.');
          if(f.type==='enum'&&!f.values.includes(v))add(name,index,f.key,'목록에 없는 값입니다.');
          if(f.type==='stat'&&!STUDIO_SEED.meta.stats.some(s=>s.value===v))add(name,index,f.key,'알 수 없는 강화 스탯입니다.');
          if(f.type==='ref'&&v&&!p.tables[f.table].some(x=>x[primary(f.table)]===v))add(name,index,f.key,`존재하지 않는 ${S[f.table].label} 참조: ${v}`);
          if(f.type==='refs')for(const x of v)if(!p.tables[f.table].some(r=>r[primary(f.table)]===x))add(name,index,f.key,`없는 참조: ${x}`);
          if(f.type==='icon'&&v&&!STUDIO_ICONS[v])add(name,index,f.key,'사용할 수 없는 아이콘 키입니다.');
          if(f.type==='datetime'&&v&&!Number.isFinite(Date.parse(v)))add(name,index,f.key,'날짜 형식을 확인하세요.');
          if(['rewards','costs'].includes(f.type)){
            if(f.type==='rewards'&&!v.length)add(name,index,f.key,'지급 아이템이 필요합니다.');
            const ids=new Set();for(const a of v){
              if(!p.tables.Item.some(i=>i.ItemId===a.itemId))add(name,index,f.key,`알 수 없는 아이템: ${a.itemId}`);
              if(ids.has(a.itemId))add(name,index,f.key,'같은 아이템이 중복되었습니다.');ids.add(a.itemId);
              if(!Number.isInteger(a.amount)||a.amount<1||a.amount>2147483647)add(name,index,f.key,'수량은 1~2,147,483,647 정수입니다.');
            }
          }
        }
      });
    }
    for(const t of p.tables.ResearchTree){
      const nodes=p.tables.ResearchNode.filter(n=>n.TreeKey===t.TreeKey),map=new Map(nodes.map(n=>[n.NodeId,n])),visited=new Set(),stack=new Set(),slots=new Set();
      const visit=id=>{if(stack.has(id))return true;if(visited.has(id))return false;visited.add(id);stack.add(id);for(const parent of map.get(id)?.Parents||[])if(map.has(parent)&&visit(parent))return true;stack.delete(id);return false;};
      if(nodes.length>500){add('ResearchTree',p.tables.ResearchTree.indexOf(t),'TreeKey','트리당 노드 수는 500개 이하여야 합니다.');continue;}
      for(const n of nodes){const i=p.tables.ResearchNode.indexOf(n);
        if(n.Parents.some(x=>!map.has(x)))add('ResearchNode',i,'Parents','같은 트리에 없는 선행 노드가 있습니다.');
        if(new Set(n.Parents).size!==n.Parents.length)add('ResearchNode',i,'Parents','선행 노드 중복');
        const slot=n.Row+':'+n.Column;if(slots.has(slot))add('ResearchNode',i,'Row','같은 위치의 노드가 겹칩니다.');slots.add(slot);
        if(n.Parents.some(x=>map.get(x)?.Row>n.Row))add('ResearchNode',i,'Parents','선행 노드가 같은 행 또는 위에 있습니다.','warning');
        if(visit(n.NodeId))add('ResearchNode',i,'Parents','선행 연결에 순환이 있습니다.');
        if(!n.CostItemId&&(n.BaseCost||n.CostStep))add('ResearchNode',i,'CostItemId','비용 아이템을 선택하거나 비용을 0으로 설정하세요.');
        const lastCost=n.BaseCost+(n.MaxLevel-1)*n.CostStep;if(lastCost>2147483647)add('ResearchNode',i,'BaseCost','최대 레벨 비용이 정수 범위를 초과합니다.');
      }
    }
    for(const [name,rows]of [['Event',p.tables.Event],['CouponPlan',p.tables.CouponPlan]])rows.forEach((r,i)=>{
      if(Date.parse(r.StartAt)>=Date.parse(r.EndAt))add(name,i,'EndAt','종료는 시작보다 늦어야 합니다.');
      if(name==='Event'&&Date.parse(r.ClaimEndAt)<Date.parse(r.EndAt))add(name,i,'ClaimEndAt','수령 마감은 이벤트 종료 이후여야 합니다.');
    });
    p.tables.AttendancePlan.forEach((plan,i)=>{
      const days=p.tables.Attendance.filter(d=>d.AttendanceId===plan.AttendanceId).map(d=>d.Day).sort((a,b)=>a-b);
      if(days.length!==plan.CycleDays||days.some((v,k)=>v!==k+1))add('AttendancePlan',i,'CycleDays','일차가 1부터 순환 일수까지 빠짐없이 있어야 합니다.');
    });
    p.tables.UnitUpgrade.forEach((r,i)=>{const u=p.tables.Unit.find(u=>u.UnitId===r.UnitId);if(u&&r.TargetLevel>u.MaxLevel)add('UnitUpgrade',i,'TargetLevel','유닛 최대 레벨을 초과합니다.');});
    p.tables.UnitMilestone.forEach((r,i)=>{const u=p.tables.Unit.find(u=>u.UnitId===r.UnitId);if(u&&r.Level>u.MaxLevel)add('UnitMilestone',i,'Level','유닛 최대 레벨을 초과합니다.');});
    p.tables.GachaPool.forEach((r,i)=>{const entries=p.tables.GachaEntry.filter(e=>e.PoolId===r.PoolId);if(!entries.length||Math.abs(entries.reduce((s,e)=>s+e.Percent,0)-100)>.000001)add('GachaPool',i,'PoolId','확률 항목 합계가 100%여야 합니다.');});
    const levels=[...p.tables.AccountLevel].sort((a,b)=>a.Level-b.Level);
    if(levels.length&&levels.some((r,i)=>r.Level!==i+1||r.RequiredExperience<(i===levels.length-1?0:1)||(i===levels.length-1&&r.RequiredExperience!==0)))add('AccountLevel',0,'Level','레벨은 1부터 연속이어야 하며 최대 레벨 EXP는 0, 나머지는 양수여야 합니다.');
    const economy=Object.fromEntries(p.tables.Economy.map(r=>[r.ConfigKey,r.Value]));
    const level=levels.find(r=>r.Level===economy.InitialLevel);
    if(!level||economy.InitialExperience<0||economy.InitialExperience>=(level.RequiredExperience||1))add('Economy',0,'Value','계정 시작 레벨/경험치가 경험치 곡선과 맞지 않습니다.');
    return issues;
  }
  const serialize=RewardCore.serialize;
  function tables(p){
    const errors=validate(p).filter(i=>i.severity==='error');if(errors.length)throw Error(`검사 오류 ${errors.length}개를 먼저 수정하세요.`);
    const out={};
    for(const [name,schema] of Object.entries(S)){
      const exportFields=schema.fields.filter(f=>!f.hidden&&f.export!==false);out[name]=[exportFields.map(f=>f.key),...p.tables[name].map(r=>exportFields.map(f=>{
        const v=r[f.key];if(['rewards','costs'].includes(f.type))return serialize(v);if(Array.isArray(v))return v.join(';');if(typeof v==='boolean')return v?1:0;return v??'';
      }))];
    }
    out.ResearchLevel=[['TreeKey','NodeId','TargetLevel','Stat','TotalValue','Costs'],...p.tables.ResearchNode.flatMap(n=>Array.from({length:n.MaxLevel},(_,i)=>[n.TreeKey,n.NodeId,i+1,n.Stat,Number((n.ValuePerLevel*(i+1)).toPrecision(12)),n.CostItemId&&n.BaseCost+i*n.CostStep>0?`${n.CostItemId}:${n.BaseCost+i*n.CostStep}`:'']))];
    return out;
  }
  function treeState(p,treeKey,levels){
    const nodes=p.tables.ResearchNode.filter(n=>n.TreeKey===treeKey),map=new Map(nodes.map(n=>[n.NodeId,n]));
    const level=n=>n?Math.max(0,Math.min(n.MaxLevel,levels[n.NodeId]||0)):0,total=nodes.reduce((s,n)=>s+level(n),0);
    return Object.fromEntries(nodes.map(n=>{
      const parents=n.Parents.map(id=>map.get(id));
      const both=parents.length===2&&parents.every(Boolean)&&(!parents[0].ExclusiveGroup||parents[0].ExclusiveGroup!==parents[1].ExclusiveGroup);
      const ready=!parents.length||(both?parents.every(p=>level(p)>=1):n.RequireAnyParent?parents.some(p=>p&&level(p)>=p.MaxLevel):parents.every(p=>p&&level(p)>=p.MaxLevel));
      const blocked=n.ExclusiveGroup&&level(n)===0&&nodes.some(o=>o!==n&&o.ExclusiveGroup===n.ExclusiveGroup&&level(o)>0);
      return [n.NodeId,level(n)>=n.MaxLevel?'maxed':blocked?'blocked':ready&&total>=n.RequiredTotalLevel?'available':'locked'];
    }));
  }
  globalThis.Studio={clone,create,shape,migrate,newRecord,references,update,validate,tables,treeState,recordKey,label,primary,serialize};
})();
