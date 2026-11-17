'use strict';
// Shop products for the two BACKND charts the game reads (2026-11-17):
// ShopDailyList (Daily rows) and ShopCatalog (Package, Gold, Essence, InApp rows).
// Rules mirror Assets/WorkSpace/USW/Shop/ShopCatalogTable.Parse; keep both in step.
globalThis.ShopCatalogFeature=({studio:C})=>{
  const SECTIONS=['Daily','Package','Gold','Essence','InApp'];
  const SECTION_LABELS={Daily:'일일',Package:'패키지',Gold:'골드 교환',Essence:'정수 교환',InApp:'인앱 (다이아)'};
  const REWARDS=['Gold','Diamond','Essence'];
  // Slots in the lobby shop layout (ShopCatalogTable.Capacity).
  const CAPACITY={Daily:3,Package:2,Gold:3,Essence:3,InApp:3};
  const fields=[
    {key:'ShopID',label:'상품 ID (고유 정수)',type:'int',min:1,max:2147483647},
    {key:'Name',label:'상품명',type:'text',required:true},
    {key:'Section',label:'상점 구역',type:'enum',values:SECTIONS,labels:SECTION_LABELS},
    {key:'SortOrder',label:'표시 순서',type:'int',min:0,max:2147483647},
    {key:'RewardCurrency',label:'지급 재화',type:'enum',values:REWARDS,labels:{Gold:'골드',Diamond:'다이아',Essence:'정수'}},
    {key:'Amount',label:'지급 수량',type:'int',min:1,max:2147483647},
    {key:'CurrencyType',label:'결제 방식',type:'enum',values:['Free','Ad','Gold','Diamond','Cash'],labels:{Free:'무료',Ad:'광고 시청',Gold:'골드',Diamond:'다이아',Cash:'인앱 결제'}},
    {key:'Price',label:'가격 (무료·광고·인앱 다이아는 0, 패키지는 원화)',type:'int',min:0,max:2147483647},
    {key:'StoreID',label:'스토어 상품 ID (인앱 다이아·패키지)',type:'text'},
    {key:'Limit',label:'구매 횟수 제한 (일일: 하루, 패키지: 기간마다, 0은 제한 없음)',type:'int',min:0,max:2147483647},
    {key:'Tag',label:'표시 문구 (패키지 필수, 예: 계정당 1회)',type:'text'},
    {key:'LimitPeriod',label:'패키지 구매 기간',type:'enum',values:['','Account','Weekly'],labels:{'':'없음 (패키지 외)',Account:'계정당 1회',Weekly:'주마다'}},
    {key:'ExtraRewards',label:'패키지 추가 보상 (예: Essence:60;Gold:20000)',type:'text'},
    {key:'Enabled',label:'판매 활성',type:'int',min:0,max:1},
    {key:'Source',label:'기획 메모',type:'text',export:false}
  ];
  // Column order of each game workbook (Data/Balance/ShopDailyList.xlsx, ShopCatalog.xlsx).
  const DAILY_HEADER=['ShopID','Name','Section','SortOrder','RewardCurrency','Amount','CurrencyType','Price','StoreID','Limit','Tag','ExtraRewards','Enabled'];
  const CATALOG_HEADER=['ShopID','Name','Section','SortOrder','RewardCurrency','Amount','CurrencyType','Price','StoreID','Limit','Tag','LimitPeriod','ExtraRewards','Enabled'];
  STUDIO_SCHEMAS.ShopCatalog={label:'상점 상품 목록',keys:['ShopID'],fields,note:'일일 상점은 고정 3칸(무료, 광고)이고 추첨하지 않습니다. 패키지 2칸, 골드/정수 교환과 인앱 다이아는 각 3칸입니다. 내보내면 ShopDailyList(일일)와 ShopCatalog(나머지) 두 차트 파일이 나옵니다.'};
  const src='현재 차트 원본 (Data/Balance, 2026-11-16)';
  const row=(ShopID,Name,Section,SortOrder,RewardCurrency,Amount,CurrencyType,Price,extra={})=>({ShopID,Name,Section,SortOrder,RewardCurrency,Amount,CurrencyType,Price,StoreID:'',Limit:0,Tag:'',LimitPeriod:'',ExtraRewards:'',Enabled:1,Source:src,...extra});
  STUDIO_SEED.tables.ShopCatalog=[
    row(1001,'무료 다이아','Daily',0,'Diamond',10,'Free',0,{Limit:1}),
    row(1002,'광고 골드','Daily',1,'Gold',1000,'Ad',0,{Limit:2}),
    row(1003,'광고 정수','Daily',2,'Essence',150,'Ad',0,{Limit:2}),
    row(5001,'첫 모험 패키지','Package',0,'Diamond',500,'Cash',1100,{StoreID:'package_first_adventure',Limit:1,Tag:'계정당 1회',LimitPeriod:'Account',ExtraRewards:'Essence:60;Gold:20000'}),
    row(5002,'주간 성장 보급','Package',1,'Diamond',300,'Cash',15000,{StoreID:'package_weekly_growth',Limit:1,Tag:'주마다 1회',LimitPeriod:'Weekly',ExtraRewards:'Essence:30;Gold:100000'}),
    ...[[1000,20],[5000,90],[12000,180]].map(([a,p],i)=>row(3001+i,'골드 교환 '+(i+1),'Gold',i,'Gold',a,'Diamond',p)),
    ...[[2000,10],[10000,50],[50000,250]].map(([a,p],i)=>row(6001+i,'정수 교환 '+(i+1),'Essence',i,'Essence',a,'Diamond',p)),
    ...[100,550,1200,6500].map((a,i)=>row(4001+i,'다이아 '+a,'InApp',i,'Diamond',a,'Cash',0,{StoreID:'diamond_'+a,Enabled:i<3?1:0}))
  ];
  PlannerScope.active.add('ShopCatalog');
  STUDIO_NAV.splice(4,0,{id:'shops',label:'상점',icon:'◇',tables:['ShopCatalog']});
  const nextId=p=>1+Math.max(1000,...p.tables.ShopCatalog.map(r=>r.ShopID));
  /** "Essence:60;Gold:20000" -> [{currency,amount}], or an error message (ShopCatalogTable.ParseExtras). */
  function parseExtras(text){
    text=String(text??'').trim();if(!text)return [];
    const result=[];
    for(const part of text.split(';')){
      const pair=part.split(':');
      if(pair.length!==2)return '추가 보상은 재화:수량;재화:수량 형식입니다.';
      const currency=pair[0].trim(),amount=pair[1].trim();
      if(!REWARDS.includes(currency))return '추가 보상 재화는 Gold, Diamond, Essence만 됩니다.';
      if(!/^[0-9]+$/.test(amount)||Number(amount)<=0)return '추가 보상 수량은 양의 정수입니다.';
      result.push({currency,amount:Number(amount)});
    }
    return result;
  }
  function validate(p){
    const issues=[],stores=new Set(),add=(index,field,message)=>issues.push({table:'ShopCatalog',index,field,message,severity:'error'});
    p.tables.ShopCatalog.forEach((r,i)=>{
      const store=String(r.StoreID??'').trim(),tag=String(r.Tag??'').trim(),period=String(r.LimitPeriod??'');
      const freeLike=r.CurrencyType==='Free'||r.CurrencyType==='Ad';
      if(store){if(stores.has(store))add(i,'StoreID','스토어 ID가 중복되었습니다.');stores.add(store);}
      if(r.Section==='Daily'){
        if(store||r.CurrencyType==='Cash'||(freeLike?r.Price!==0:r.Price<=0)||r.Limit<1)add(i,'CurrencyType','일일 상품은 무료/광고(가격 0) 또는 골드/다이아 결제이고, 구매 횟수 제한 1 이상, 스토어 ID 없음입니다.');
      }else if(r.Section==='Gold'||r.Section==='Essence'){
        if(r.RewardCurrency!==r.Section||r.CurrencyType!=='Diamond'||r.Price<=0||store||r.Limit!==0)add(i,'RewardCurrency',SECTION_LABELS[r.Section]+'은 다이아로 '+(r.Section==='Gold'?'골드':'정수')+'를 교환합니다. 가격 양수, 제한 0, 스토어 ID 없음입니다.');
      }else if(r.Section==='InApp'){
        if(r.RewardCurrency!=='Diamond'||r.CurrencyType!=='Cash'||r.Price!==0||!store||r.Limit!==0)add(i,'StoreID','인앱 다이아는 다이아 지급, 인앱 결제, 가격 0, 제한 0, 스토어 ID 필수입니다.');
      }else if(r.Section==='Package'){
        if(r.CurrencyType!=='Cash'||r.Price<=0||r.Limit<1||!tag||!['Account','Weekly'].includes(period))add(i,'LimitPeriod','패키지는 인앱 결제, 원화 가격, 구매 횟수 제한 1 이상, 표시 문구, 구매 기간(계정당 1회 또는 주마다)이 필요합니다.');
      }
      const extras=parseExtras(r.ExtraRewards);
      if(typeof extras==='string')add(i,'ExtraRewards',extras);
      else{
        if(r.Section!=='Package'&&(extras.length||period))add(i,'ExtraRewards','추가 보상과 구매 기간은 패키지에만 씁니다.');
        if(1+extras.length>3)add(i,'ExtraRewards','패키지 보상은 기본 보상 포함 최대 3개입니다.');
      }
    });
    for(const section of SECTIONS){const n=p.tables.ShopCatalog.filter(r=>r.Section===section&&r.Enabled===1).length;if(n>CAPACITY[section])add(0,'Enabled',SECTION_LABELS[section]+' 판매 활성 상품은 최대 '+CAPACITY[section]+'개입니다 (지금 '+n+'개).');}
    return issues;
  }
  const exportRows=(p,sections,header)=>[header,...p.tables.ShopCatalog.filter(r=>sections.includes(r.Section)).map(r=>header.map(k=>r[k]??''))];
  /** Old planner rows used the lottery layout (FixedFree/Weight, Weekly); the game no longer reads it. */
  const isLegacy=rows=>rows.some(r=>!Object.hasOwn(r,'Limit')||Object.hasOwn(r,'FixedFree')||Object.hasOwn(r,'Weight')||r.Section==='Weekly');
  globalThis.ShopCatalogTools={
    DAILY_HEADER,CATALOG_HEADER,SECTIONS,SECTION_LABELS,CAPACITY,parseExtras,isLegacy,
    /** Chart files for upload: ShopDailyList (Daily) and ShopCatalog (the other sections), in game column order. */
    listTables:p=>({ShopDailyList:exportRows(p,['Daily'],DAILY_HEADER),ShopCatalog:exportRows(p,['Package','Gold','Essence','InApp'],CATALOG_HEADER)}),
    sectionSummary(p,row){
      const n=p.tables.ShopCatalog.filter(r=>r.Section===row.Section&&r.Enabled===1).length;
      return `${SECTION_LABELS[row.Section]||row.Section}: 판매 활성 ${n} / 최대 ${CAPACITY[row.Section]??0}칸`;
    }
  };
  return {
    prepare(p){
      if(!p?.tables)return p;p=C.clone(p);
      // Old lottery rows cannot satisfy the game rules, so a saved project falls back to the chart source.
      if(!Object.hasOwn(p.tables,'ShopCatalog')||isLegacy(p.tables.ShopCatalog)){
        p.tables.ShopCatalog=C.clone(STUDIO_SEED.tables.ShopCatalog);
      }
      for(const r of p.tables.ShopCatalog)for(const f of fields)if(!Object.hasOwn(r,f.key))r[f.key]=f.type==='int'?0:'';
      return p;
    },
    decorate(p){p.meta.statuses.ShopCatalog=STUDIO_SCHEMAS.ShopCatalog.note;return p;},
    updated(p,name,index,key,value){
      if(name!=='ShopCatalog')return;const r=p.tables[name][index];
      if(key==='Section'){
        if(value!=='Package'){r.LimitPeriod='';r.ExtraRewards='';}
        if(value==='Gold'||value==='Essence'){r.RewardCurrency=value;r.CurrencyType='Diamond';r.Limit=0;r.StoreID='';}
        if(value==='InApp'){r.RewardCurrency='Diamond';r.CurrencyType='Cash';r.Price=0;r.Limit=0;}
        if(value==='Package'){r.CurrencyType='Cash';if(r.Limit<1)r.Limit=1;if(!r.LimitPeriod)r.LimitPeriod='Account';}
        if(value==='Daily'){if(r.Limit<1)r.Limit=1;r.StoreID='';}
      }
      if(key==='CurrencyType'&&(value==='Free'||value==='Ad'))r.Price=0;
    },
    newRecord(row,name,p){return name==='ShopCatalog'?{ShopID:nextId(p),Name:'새 상품',Section:'Daily',SortOrder:0,RewardCurrency:'Gold',Amount:1,CurrencyType:'Free',Price:0,StoreID:'',Limit:1,Tag:'',LimitPeriod:'',ExtraRewards:'',Enabled:0,Source:'신규 기획'}:row;},
    filterIssues:issues=>issues.filter(i=>i.table!=='ShopCatalog'),
    validate(p){
      // Scope filtering hides legacy shop tables; validate this runtime contract explicitly.
      const issues=validate(p),seen=new Set();
      p.tables.ShopCatalog.forEach((r,i)=>{
        for(const f of fields){const v=r[f.key];if(f.type==='int'&&(!Number.isInteger(v)||v<f.min||v>f.max)||f.type==='enum'&&!f.values.includes(v)||f.required&&!String(v??'').trim())issues.push({table:'ShopCatalog',index:i,field:f.key,message:'값의 형식 또는 허용 범위를 확인하세요.',severity:'error'});}
        if(seen.has(r.ShopID))issues.push({table:'ShopCatalog',index:i,field:'ShopID',message:'중복 상품 ID입니다.',severity:'error'});seen.add(r.ShopID);
      });return issues;
    },
    tables(tables,p){const f=fields.filter(f=>f.export!==false);tables.ShopCatalog=[f.map(f=>f.key),...p.tables.ShopCatalog.map(r=>f.map(f=>r[f.key]??''))];return tables;}
  };
};
