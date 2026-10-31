'use strict';
globalThis.ItemCatalogFeature=({studio:C})=>{
 const S=STUDIO_SCHEMAS,f=(key,label,type='text',extra={})=>({key,label,type,...extra}),ref=(key,label,table)=>f(key,label,'ref',{table});
 const definitions={
  GrantItems:{label:'아이템 지급',rewards:true},
  ChooseReward:{label:'보상 선택 상자',list:true,optionType:'Reward'},
  ChooseDeck:{label:'덱 선택 지급',list:true,optionType:'Deck'}
 };
 const names=['Item','ItemFunction','SelectionList','SelectionOption'];
 S.Item.label='아이템';S.Item.note='아이템 ID는 이름과 분리된 고유 키입니다. 기능이 없으면 기능 ID를 비워 둡니다. 기능의 게임 실행 지원은 별도로 연결합니다.';
 const kind=S.Item.fields.find(f=>f.key==='ItemType');kind.values=['Currency','Consumable','Material','Character','ProfileIcon','ProfileFrame'];kind.labels={Currency:'재화',Consumable:'소비 아이템',Material:'재료',Character:'캐릭터',ProfileIcon:'프로필 아이콘',ProfileFrame:'프로필 테두리'};
 S.Item.fields.splice(2,0,f('Description','설명','textarea'),ref('FunctionId','사용 기능','ItemFunction'),f('IconAddress','아이콘 Addressables 주소'),f('IconPng','PNG 원본','text',{hidden:true,export:false}));
 S.ItemFunction={label:'아이템 기능',keys:['FunctionId'],fields:[f('FunctionId','기능 ID','id',{required:true}),f('Name','기능 이름','text',{required:true}),f('FunctionType','기능 종류','enum',{values:Object.keys(definitions),labels:Object.fromEntries(Object.entries(definitions).map(([k,v])=>[k,v.label]))}),ref('SelectionListId','선택 목록','SelectionList'),f('Rewards','지급 아이템','rewards',{allowEmpty:true}),f('Value','이전 규약 값','text',{hidden:true,export:false}),f('FunctionDrafts','종류별 초안','text',{hidden:true,export:false})],note:'아이템 지급은 지급할 아이템과 수량을 설정하고, 선택 기능은 선택 목록을 연결합니다. 같은 기능을 여러 아이템이 재사용할 수 있습니다.'};
 S.SelectionList={label:'선택 목록',keys:['SelectionListId'],fields:[f('SelectionListId','선택 목록 ID','id',{required:true}),f('Name','목록 이름','text',{required:true}),f('Description','설명','textarea')],note:'여러 아이템 기능이 같은 목록을 재사용할 수 있습니다. 포함 옵션·수량으로 검색해 중복 목록을 확인하세요.'};
 S.SelectionOption={label:'선택 옵션',keys:['OptionId'],fields:[f('OptionId','옵션 ID','id',{required:true}),ref('SelectionListId','소속 선택 목록','SelectionList'),f('Name','옵션 이름','text',{required:true}),f('OptionType','선택 결과','enum',{values:['Reward','Deck'],labels:{Reward:'아이템 지급',Deck:'덱 지급'}}),f('Rewards','지급 아이템','rewards',{allowEmpty:true}),ref('DeckId','지급 덱','Party')],note:'옵션 ID로 각 선택지를 식별합니다. 배열 순서에 의존하지 않습니다.'};
 for(const n of names)PlannerScope.active.add(n);
 STUDIO_NAV.splice(STUDIO_NAV.findIndex(n=>n.id==='attendance'),0,{id:'items',label:'아이템 관리',icon:'◇',tables:names});
 function prepare(p){const q=C.clone(p);for(const n of names)q.tables[n]??=[];for(const row of q.tables.ItemFunction){row.Rewards??=[];row.Value??='';row.FunctionDrafts??='';if(row.FunctionType==='RecoverStamina'){row.FunctionType='GrantItems';row.Rewards=[{itemId:'Stamina',amount:Number(row.Value)}];row.SelectionListId='';row.Value='';}}for(const item of q.tables.Item)for(const key of ['Description','FunctionId','IconAddress','IconPng'])item[key]??='';return q;}
 Object.assign(STUDIO_SEED,prepare(STUDIO_SEED));
 function id(p,table){const key=S[table].keys[0],prefix={Item:'item',ItemFunction:'function',SelectionList:'selection',SelectionOption:'option'}[table];let n=1;while(p.tables[table].some(r=>r[key]===prefix+'_'+String(n).padStart(4,'0')))n++;return prefix+'_'+String(n).padStart(4,'0');}
 function record(p,table){const row=C.newRecord(table,p);if(!S[table].fields.some(f=>f.key==='Source'))delete row.Source;row[S[table].keys[0]]=id(p,table);row.Name='';if(table==='Item')row.ItemType='Consumable';if(table==='ItemFunction')Object.assign(row,{FunctionType:'GrantItems',SelectionListId:'',Rewards:[],Value:'',FunctionDrafts:''});if(table==='SelectionOption')Object.assign(row,{OptionType:'Reward',Rewards:[],DeckId:''});return row;}
 function summary(p,table,row){if(table==='SelectionList')return p.tables.SelectionOption.filter(o=>o.SelectionListId===row.SelectionListId).map(o=>o.OptionType==='Deck'?'덱 '+(p.tables.Party.find(d=>d.PartyId===o.DeckId)?.Name||o.DeckId):o.Rewards.map(r=>(p.tables.Item.find(i=>i.ItemId===r.itemId)?.Name||r.itemId)+' ×'+r.amount).join(' + ')).join(' / ');if(table==='ItemFunction')return (definitions[row.FunctionType]?.label||row.FunctionType)+' · '+(definitions[row.FunctionType]?.rewards?row.Rewards.map(r=>(p.tables.Item.find(i=>i.ItemId===r.itemId)?.Name||r.itemId)+' ×'+r.amount).join(' + '):row.SelectionListId);return row.Description||'';}
 function signature(p,list){return JSON.stringify(p.tables.SelectionOption.filter(o=>o.SelectionListId===list.SelectionListId).map(o=>JSON.stringify([o.OptionType,o.DeckId,o.Rewards.map(r=>[r.itemId,r.amount]).sort()])).sort());}
 function validate(p){const issues=[],add=(table,index,field,message,severity='error')=>issues.push({table,index,field,message,severity});
  p.tables.ItemFunction.forEach((r,i)=>{const d=definitions[r.FunctionType];if(!d)return;if(d.list){const list=p.tables.SelectionList.find(l=>l.SelectionListId===r.SelectionListId);if(!list)add('ItemFunction',i,'SelectionListId','존재하는 선택 목록을 연결하세요.');else if(p.tables.SelectionOption.some(o=>o.SelectionListId===r.SelectionListId&&o.OptionType!==d.optionType))add('ItemFunction',i,'SelectionListId','기능과 선택 옵션의 결과 종류가 다릅니다.');if(r.Rewards.length)add('ItemFunction',i,'Rewards','선택 기능의 지급 아이템은 선택 옵션에서 설정하세요.');}else{if(!r.Rewards.length)add('ItemFunction',i,'Rewards','지급할 아이템을 하나 이상 설정하세요.');if(r.SelectionListId)add('ItemFunction',i,'SelectionListId','아이템 지급 기능은 선택 목록을 연결하지 않습니다.');}});
  const signatures=new Map();p.tables.SelectionList.forEach((r,i)=>{if(!p.tables.SelectionOption.some(o=>o.SelectionListId===r.SelectionListId))add('SelectionList',i,'SelectionListId','선택 옵션을 하나 이상 추가하세요.');const key=signature(p,r);if(signatures.has(key))add('SelectionList',i,'SelectionListId','같은 내용의 목록: '+signatures.get(key),'warning');else signatures.set(key,r.SelectionListId);});
  p.tables.SelectionOption.forEach((r,i)=>{if(!r.SelectionListId)add('SelectionOption',i,'SelectionListId','소속 목록을 선택하세요.');if(r.OptionType==='Reward'&&(!r.Rewards.length||r.DeckId)||r.OptionType==='Deck'&&(!r.DeckId||r.Rewards.length))add('SelectionOption',i,'Rewards','아이템 지급 또는 덱 지급 중 해당 결과만 입력하세요.');});
  const visiting=new Set(),done=new Set();function visit(item){if(visiting.has(item.ItemId))return true;if(done.has(item.ItemId))return false;visiting.add(item.ItemId);const fn=p.tables.ItemFunction.find(f=>f.FunctionId===item.FunctionId);const rewards=fn?.FunctionType==='GrantItems'?fn.Rewards:fn?.FunctionType==='ChooseReward'?p.tables.SelectionOption.filter(o=>o.SelectionListId===fn.SelectionListId).flatMap(o=>o.Rewards):[];for(const reward of rewards){const child=p.tables.Item.find(i=>i.ItemId===reward.itemId);if(child&&visit(child))return true;}visiting.delete(item.ItemId);done.add(item.ItemId);return false;}
  p.tables.Item.forEach((r,i)=>{if(r.IconPng&&!/^ItemIcons\/[A-Za-z0-9_-]+$/.test(r.IconAddress))add('Item',i,'IconAddress','PNG 아이콘 주소는 ItemIcons/영문·숫자·밑줄·하이픈 이름으로 입력하세요.');if(r.ItemType==='Currency'&&r.FunctionId)add('Item',i,'FunctionId','재화는 사용 기능을 연결하지 않습니다. 소비 아이템 종류를 선택하세요.');if(visit(r))add('Item',i,'FunctionId','아이템 선택 보상이 자신을 다시 지급하는 순환입니다.');});return issues;
 }
 function resetFunction(row,previousType){
  if(previousType===row.FunctionType)return;
  let drafts={};try{drafts=JSON.parse(row.FunctionDrafts||'{}');}catch{}
  if(previousType)drafts[previousType]={SelectionListId:row.SelectionListId,Rewards:C.clone(row.Rewards)};
  const restored=drafts[row.FunctionType]||{SelectionListId:'',Rewards:[]};
  row.SelectionListId=restored.SelectionListId;row.Rewards=C.clone(restored.Rewards);row.Value='';row.FunctionDrafts=JSON.stringify(drafts);
 }
 globalThis.ItemCatalog={names,definitions,record,summary,signature,validate,resetFunction};
 return {prepare,updated(p,table,index,key,value,previous){if(table==='ItemFunction'&&key==='FunctionType'){const row=p.tables[table][index];resetFunction(row,previous);}},decorate:p=>{for(const n of names)p.meta.statuses[n]=S[n].note;return p;},validate};
};
