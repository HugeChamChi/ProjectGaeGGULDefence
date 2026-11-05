'use strict';
globalThis.ShopCatalogFeature=({studio:C})=>{
  const fields=[
    {key:'ShopID',label:'상품 ID (고유 정수)',type:'int',min:1,max:2147483647},
    {key:'Name',label:'기획 상품명',type:'text',required:true},
    {key:'Section',label:'상점 구역',type:'enum',values:['Daily','Weekly','Gold','InApp'],labels:{Daily:'일일',Weekly:'주간',Gold:'골드',InApp:'인앱 (다이아)'}},
    {key:'FixedFree',label:'무료 고정 상품',type:'int',min:0,max:1},
    {key:'Weight',label:'추첨 가중치 (무료 고정·골드·인앱은 0)',type:'int',min:0,max:1000000},
    {key:'SortOrder',label:'표시 순서',type:'int',min:0,max:2147483647},
    {key:'RewardCurrency',label:'지급 재화',type:'enum',values:['Gold','Diamond'],labels:{Gold:'골드',Diamond:'다이아'}},
    {key:'Amount',label:'지급 수량',type:'int',min:1,max:2147483647},
    {key:'CurrencyType',label:'결제 재화',type:'enum',values:['Gold','Diamond','Free','Cash'],labels:{Gold:'골드',Diamond:'다이아',Free:'무료',Cash:'인앱 결제'}},
    {key:'Price',label:'가격 (무료·인앱은 0)',type:'int',min:0,max:2147483647},
    {key:'StoreID',label:'스토어 상품 ID (인앱 전용)',type:'text'},
    {key:'Enabled',label:'판매 활성',type:'int',min:0,max:1},
    {key:'Source',label:'기획 메모',type:'text',export:false}
  ];
  STUDIO_SCHEMAS.ShopCatalog={label:'상점 상품 목록',keys:['ShopID'],fields,note:'일일/주간: 무료 고정 1개 + 가중치 추첨 최대 5개. 가중치 합계는 자유이며 툴에서 비율을 계산합니다. 뽑힌 상품은 제외하고 다음 상품을 추첨합니다. Gold/InApp은 고정 목록입니다. 기존 계정은 다음 초기화부터 새 설정을 사용합니다.'};
  // Disabled authoring examples. Existing projects keep their own rows.
  STUDIO_SEED.tables.ShopCatalog=[
    ...['Daily','Weekly'].flatMap((Section,s)=>Array.from({length:9},(_,i)=>({ShopID:1001+s*1000+i,Name:(s?'주간':'일일')+(i===0?' 무료 골드':' 골드 후보 '+i),Section,FixedFree:i===0?1:0,Weight:i===0?0:[60,50,36,24,14,8,6,2][i-1],SortOrder:i,RewardCurrency:'Gold',Amount:i===0?(s?1000:100):[500,1000,1500,2500,4000,6000,9000,12000][i-1]*(s?7:1),CurrencyType:i===0?'Free':'Diamond',Price:i===0?0:Math.ceil([10,18,25,38,55,75,100,120][i-1]*(s?5:1)),StoreID:'',Enabled:1,Source:'임시 밸런스 · 무료/일일 진열 구조만 타 게임 참고, 수량·가격·가중치는 자체 초안'}))),
    ...[1000,5000,12000].map((Amount,i)=>({ShopID:3001+i,Name:'골드 교환 '+(i+1),Section:'Gold',FixedFree:0,Weight:0,SortOrder:i,RewardCurrency:'Gold',Amount,CurrencyType:'Diamond',Price:[20,90,180][i],StoreID:'',Enabled:1,Source:'임시 교환 비율 · 확정 전 수정'})),
    ...[100,550,1200,6500].map((Amount,i)=>({ShopID:4001+i,Name:'다이아 '+Amount,Section:'InApp',FixedFree:0,Weight:0,SortOrder:i,RewardCurrency:'Diamond',Amount,CurrencyType:'Cash',Price:0,StoreID:'diamond_'+Amount,Enabled:1,Source:'기존 IAP 상품 정의 · 스토어 가격 사용'}))
  ];
  PlannerScope.active.add('ShopCatalog');
  STUDIO_NAV.splice(4,0,{id:'shops',label:'상점',icon:'◇',tables:['ShopCatalog']});
  const nextId=p=>1+Math.max(1000,...p.tables.ShopCatalog.map(r=>r.ShopID));
  function validate(p){
    const issues=[],stores=new Set(),add=(index,field,message)=>issues.push({table:'ShopCatalog',index,field,message,severity:'error'});
    p.tables.ShopCatalog.forEach((r,i)=>{
      const candidate=['Daily','Weekly'].includes(r.Section)&&r.FixedFree===0;
      if(candidate?r.Weight<=0:r.Weight!==0)add(i,'Weight','일일/주간 추첨 후보는 양수, 무료 고정·골드·인앱은 가중치 0입니다.');
      if(r.FixedFree===1&&(!['Daily','Weekly'].includes(r.Section)||r.CurrencyType!=='Free'||r.Price!==0))add(i,'FixedFree','무료 고정 상품은 일일/주간에서 결제 무료, 가격 0으로 설정하세요.');
      if(r.Section==='InApp'){
        if(r.RewardCurrency!=='Diamond'||r.CurrencyType!=='Cash'||r.Price!==0||!r.StoreID.trim())add(i,'StoreID','인앱은 다이아 지급 / 인앱 결제 / 가격 0 / 스토어 ID가 필요합니다.');
        if(stores.has(r.StoreID))add(i,'StoreID','스토어 ID가 중복되었습니다.');stores.add(r.StoreID);
      }else{
        if(r.StoreID||r.CurrencyType==='Cash'||(r.CurrencyType==='Free'?r.Price!==0:r.Price<=0))add(i,'Price','무료는 가격 0, 유료는 양수이며 스토어 ID는 비워 둡니다.');
      }
      if(r.Section==='Gold'&&(r.RewardCurrency!=='Gold'||r.CurrencyType!=='Diamond'))add(i,'RewardCurrency','골드 상점은 다이아로 골드를 교환합니다.');
    });
    for(const section of ['Daily','Weekly'])if(p.tables.ShopCatalog.filter(r=>r.Section===section&&r.FixedFree===1).length>1)add(0,'FixedFree',section+' 무료 고정 상품은 하나만 지정할 수 있습니다.');
    for(const section of ['Gold','InApp'])if(p.tables.ShopCatalog.filter(r=>r.Section===section&&r.Enabled===1).length>6)add(0,'Section',section+' 활성 상품은 최대 6개입니다.');
    return issues;
  }
  function probabilityTables(p){
    const result={};
    for(const section of ['Daily','Weekly']){
      const rows=p.tables.ShopCatalog.filter(r=>r.Section===section&&r.Enabled===1&&r.FixedFree===0).sort((a,b)=>a.ShopID-b.ShopID);
      if(!rows.length)continue;
      const total=rows.reduce((s,r)=>s+r.Weight,0),scale=1000000000;
      const parts=rows.map(r=>{const n=r.Weight*scale;return {row:r,units:Math.floor(n/total),remainder:n%total};});
      let missing=scale-parts.reduce((s,r)=>s+r.units,0);
      for(const part of [...parts].sort((a,b)=>b.remainder-a.remainder||a.row.ShopID-b.row.ShopID)){if(missing--<=0)break;part.units++;}
      if(parts.some(p=>p.units<=0))throw Error('가중치 차이가 너무 커서 뒤끝의 소수점 7자리로 표현할 수 없습니다. 가중치 비율을 조정하세요.');
      result[section==='Daily'?'ShopDailyProbability':'ShopWeeklyProbability']=[['ShopID','percent'],...parts.map(p=>[p.row.ShopID,p.units/10000000])];
    }
    return result;
  }
  globalThis.ShopCatalogTools={probabilityTables,listTables(p){
    const fields=STUDIO_SCHEMAS.ShopCatalog.fields.filter(f=>f.export!==false);
    return Object.fromEntries([['ShopDailyList',['Daily']],['ShopWeeklyList',['Weekly']],['ShopCatalog',['Gold','InApp']]].map(([name,sections])=>[name,[fields.map(f=>f.key),...p.tables.ShopCatalog.filter(r=>sections.includes(r.Section)).map(r=>fields.map(f=>r[f.key]))]]));
  },weightSummary(p,row){
    if(!['Daily','Weekly'].includes(row.Section))return '골드·인앱은 고정 목록입니다.';
    if(row.FixedFree===1)return '무료 고정: 추첨 없이 항상 표시합니다. 가중치는 0입니다.';
    if(row.Enabled!==1)return '판매 비활성: 추첨 대상에서 제외됩니다.';
    const total=p.tables.ShopCatalog.filter(r=>r.Section===row.Section&&r.FixedFree===0&&r.Enabled===1).reduce((s,r)=>s+r.Weight,0);
    return `가중치 ${row.Weight} / 합계 ${total} → 첫 추첨 비율 ${total>0?(row.Weight/total*100).toFixed(4):'0'}%. 선택된 상품을 제외하고 다음 상품을 추첨합니다.`;
  }};
  return {
    prepare(p){if(!p?.tables)return p;p=C.clone(p);if(!Object.hasOwn(p.tables,'ShopCatalog'))p.tables.ShopCatalog=C.clone(STUDIO_SEED.tables.ShopCatalog);for(const r of p.tables.ShopCatalog)if(!Object.hasOwn(r,'Weight'))r.Weight=['Daily','Weekly'].includes(r.Section)&&r.FixedFree===0?1:0;return p;},
    decorate(p){p.meta.statuses.ShopCatalog=STUDIO_SCHEMAS.ShopCatalog.note;return p;},
    updated(p,name,index,key,value){if(name!=='ShopCatalog')return;const r=p.tables[name][index];if(key==='FixedFree'){r.Weight=value===1?0:1;if(value===1){r.CurrencyType='Free';r.Price=0;}}if(key==='Section'&&['Gold','InApp'].includes(value)){r.FixedFree=0;r.Weight=0;}},
    newRecord(row,name,p){return name==='ShopCatalog'?{ShopID:nextId(p),Name:'새 상품',Section:'Daily',FixedFree:0,Weight:1,SortOrder:0,RewardCurrency:'Gold',Amount:1,CurrencyType:'Diamond',Price:1,StoreID:'',Enabled:0,Source:'신규 기획'}:row;},
    filterIssues:issues=>issues.filter(i=>i.table!=='ShopCatalog'),
    validate(p){
      // Scope filtering hides legacy shop tables; validate this runtime contract explicitly.
      const issues=validate(p),seen=new Set();
      p.tables.ShopCatalog.forEach((r,i)=>{
        for(const f of fields){const v=r[f.key];if(f.type==='int'&&(!Number.isInteger(v)||v<f.min||v>f.max)||f.type==='enum'&&!f.values.includes(v)||f.required&&!String(v).trim())issues.push({table:'ShopCatalog',index:i,field:f.key,message:'값의 형식 또는 허용 범위를 확인하세요.',severity:'error'});}
        if(seen.has(r.ShopID))issues.push({table:'ShopCatalog',index:i,field:'ShopID',message:'중복 상품 ID입니다.',severity:'error'});seen.add(r.ShopID);
      });return issues;
    },
    tables(tables,p){const f=fields.filter(f=>f.export!==false);tables.ShopCatalog=[f.map(f=>f.key),...p.tables.ShopCatalog.map(r=>f.map(f=>r[f.key]))];return tables;}
  };
};
