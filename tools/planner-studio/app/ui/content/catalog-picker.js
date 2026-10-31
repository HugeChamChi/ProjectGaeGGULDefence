(()=>{
 'use strict';
 const esc=s=>String(s??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
 function filtered(p,table,query='',type=''){
  const key=STUDIO_SCHEMAS[table].keys[0],words=query.toLowerCase().trim().split(/\s+/).filter(Boolean);
  return p.tables[table].filter(r=>(!type||r.ItemType===type||r.FunctionType===type||r.OptionType===type)&&words.every(w=>JSON.stringify([r,ItemCatalog.summary(p,table,r)]).toLowerCase().includes(w)));
 }
 function open({project:p,table,onPick,changed}){
  const previousDialog=document.querySelector('dialog[open]:last-of-type');if(previousDialog)previousDialog.style.visibility='hidden';
  let limit=200;
 const schema=STUDIO_SCHEMAS[table],key=schema.keys[0],dialog=document.createElement('dialog');dialog.className='wide-dialog catalog-dialog';
  dialog.innerHTML=`<div class="dialog-head"><h2>${esc(schema.label)} 선택</h2><button data-catalog-close>닫기</button></div><div class="catalog-search"><input data-catalog-search placeholder="이름 · ID · 설명 · 포함 보상 검색"><select data-catalog-filter><option value="">전체 종류</option>${(schema.fields.find(f=>['ItemType','FunctionType','OptionType'].includes(f.key))?.values||[]).map(v=>`<option value="${esc(v)}">${esc(schema.fields.find(f=>['ItemType','FunctionType','OptionType'].includes(f.key)).labels?.[v]||v)}</option>`).join('')}</select></div><div data-catalog-count></div><div data-catalog-results class="record-list"></div>${ItemCatalog.names.includes(table)?'<button data-catalog-create>＋ 검색한 이름으로 새로 만들기</button>':''}`;
  document.body.append(dialog);const close=()=>{dialog.close();dialog.remove();if(previousDialog)previousDialog.style.visibility='';};dialog.querySelector('[data-catalog-close]').onclick=close;dialog.addEventListener('cancel',e=>{e.preventDefault();close();});
  function paint(){const list=filtered(p,table,dialog.querySelector('[data-catalog-search]').value,dialog.querySelector('[data-catalog-filter]').value);dialog.querySelector('[data-catalog-count]').textContent=`${list.length}개 / 전체 ${p.tables[table].length}개`;dialog.querySelector('[data-catalog-results]').innerHTML=list.slice(0,limit).map(r=>`<button class="record-row" data-catalog-pick="${esc(r[key])}"><strong>${esc(r.Name||r[key])}</strong><small>${esc(r[key])} · ${esc(ItemCatalog.summary(p,table,r))}</small></button>`).join('')+(list.length>limit?'<button data-catalog-more>다음 200개 더 보기</button>':'')||'<p>일치하는 항목이 없습니다.</p>';}
  dialog.oninput=()=>{limit=200;paint();};dialog.onclick=e=>{if(e.target.closest('[data-catalog-more]')){limit+=200;paint();return;}const pick=e.target.closest('[data-catalog-pick]');if(pick){onPick(p.tables[table].find(r=>r[key]===pick.dataset.catalogPick));close();}if(e.target.closest('[data-catalog-create]'))edit({project:p,table,initialName:dialog.querySelector('[data-catalog-search]').value,changed,onSave:row=>{onPick(row);close();}});};paint();dialog.showModal();dialog.querySelector('input').focus();
 }
 function edit({project:p,table,initialName='',initial={},row=null,changed,onSave}){
  const previousDialog=document.querySelector('dialog[open]:last-of-type');if(previousDialog)previousDialog.style.visibility='hidden';
  const work=Studio.clone(p),schema=STUDIO_SCHEMAS[table],key=schema.keys[0],isNew=!row;
  const index=isNew?work.tables[table].length:p.tables[table].indexOf(row),draft=row?Studio.clone(row):Object.assign(ItemCatalog.record(work,table),initial);
  if(isNew){draft.Name=initialName;work.tables[table].push(draft);}else work.tables[table][index]=draft;
  let previousType=draft.FunctionType;
  const before=new Set(Studio.validate(p).filter(i=>i.severity==='error').map(i=>JSON.stringify(i)));
  const dialog=document.createElement('dialog');dialog.className='wide-dialog catalog-dialog';document.body.append(dialog);
  const close=()=>{dialog.close();dialog.remove();if(previousDialog)previousDialog.style.visibility='';};dialog.addEventListener('cancel',e=>{e.preventDefault();close();});
  function paint(){work.tables[table][index]=draft;const same=table==='SelectionList'&&work.tables.SelectionOption.some(o=>o.SelectionListId===draft.SelectionListId)?work.tables.SelectionList.filter(l=>l.SelectionListId!==draft.SelectionListId&&ItemCatalog.signature(work,l)===ItemCatalog.signature(work,draft)):[];
   dialog.innerHTML=`<div class="dialog-head"><h2>${esc(schema.label)} ${isNew?'추가':'편집'}</h2><span class="catalog-step">${previousDialog?'항목을 저장하면 이전 설정으로 돌아가 자동 연결됩니다.':'관리 항목 편집'}</span><button data-edit-cancel>취소</button></div><div class="form-grid">${schema.fields.filter(f=>!f.hidden&&!['Source','BackendId','Note','IconKey'].includes(f.key)&&!(table==='ItemFunction'&&(f.key==='Rewards'&&!ItemCatalog.definitions[draft.FunctionType]?.rewards||f.key==='SelectionListId'&&!ItemCatalog.definitions[draft.FunctionType]?.list))&&!(table==='SelectionOption'&&(f.key==='Rewards'&&draft.OptionType==='Deck'||f.key==='DeckId'&&draft.OptionType==='Reward'))).map(f=>{
    if(table==='Missions'&&f.key==='EventKey'&&draft.MissionType==='Normal')return '';
    const value=draft[f.key];
    if(table==='Item'&&f.key==='IconAddress')return `<div class="full-field"><h3>아이콘 PNG</h3>${draft.IconPng?`<img alt="아이콘 미리보기" width="64" height="64" src="data:image/png;base64,${esc(draft.IconPng)}">`:''}<input type="file" accept=".png,image/png" data-edit-png><small>1MB 이하 · 주소 자동 설정 · Excel 저장 시 PNG 함께 출력</small><input data-edit-field="IconAddress" value="${esc(value)}" ${draft.IconPng?'readonly':''}></div>`;
    if(f.type==='ref')return `<label>${esc(f.label)}<button data-edit-ref="${esc(f.key)}">${esc(work.tables[f.table].find(r=>r[STUDIO_SCHEMAS[f.table].keys[0]]===value)?.Name||value||'검색하여 연결 / 새로 추가')}</button><button data-edit-clear="${esc(f.key)}">연결 해제</button></label>`;
    if(f.type==='rewards')return `<div class="full-field"><h3>${esc(f.label)}</h3>${value.map((r,i)=>`<div>${esc(work.tables.Item.find(x=>x.ItemId===r.itemId)?.Name||r.itemId)} <input type="number" min="1" step="1" data-edit-amount="${i}" value="${r.amount??''}"><button data-edit-remove="${i}">제거</button></div>`).join('')}<button data-edit-reward>＋ 아이템 검색 / 추가</button></div>`;
    if(f.type==='enum')return `<label>${esc(f.label)}<select data-edit-field="${esc(f.key)}">${f.values.map(v=>`<option value="${esc(v)}" ${v===value?'selected':''}>${esc(f.labels?.[v]||v)}</option>`).join('')}</select></label>`;
    return `<label>${esc(f.label)}${f.type==='textarea'?`<textarea data-edit-field="${esc(f.key)}">${esc(value)}</textarea>`:`<input data-edit-field="${esc(f.key)}" value="${esc(value)}">`}</label>`;
   }).join('')}</div>${table==='SelectionList'?`<section><h3>선택 옵션</h3>${work.tables.SelectionOption.filter(o=>o.SelectionListId===draft.SelectionListId).map(o=>`<button data-edit-option="${esc(o.OptionId)}">${esc(o.Name)} · ${esc(o.Rewards.map(r=>(work.tables.Item.find(i=>i.ItemId===r.itemId)?.Name||r.itemId)+' ×'+r.amount).join(' + ')||o.DeckId)}</button>`).join('')}<button data-edit-add-option>＋ 선택 옵션 추가</button></section>`:''}${same.length?`<div class="preview"><strong>동일한 선택 구성이 이미 있습니다.</strong>${same.map(l=>`<button data-edit-reuse="${esc(l.SelectionListId)}">${esc(l.Name)} · ${esc(l.SelectionListId)} 재사용</button>`).join('')}</div>`:''}<div data-edit-errors class="validation"></div><button data-edit-save class="primary">${previousDialog?'저장하고 이전 설정에 연결':'저장'}</button>`;
  }
  dialog.oninput=e=>{if(e.target.dataset.editField){const field=e.target.dataset.editField;if(field===key){work.tables[table][index]=draft;Studio.update(work,table,index,field,e.target.value);}else {const f=schema.fields.find(f=>f.key===field);draft[field]=['int','float'].includes(f.type)?(e.target.value===''?null:Number(e.target.value)):e.target.value;}}if(e.target.dataset.editAmount!==undefined)draft.Rewards[Number(e.target.dataset.editAmount)].amount=e.target.value===''?null:Number(e.target.value);};
  dialog.onchange=async e=>{if(e.target.hasAttribute('data-edit-png')){const file=e.target.files[0];if(!file)return;try{if(file.size>1024*1024)throw Error('1MB 이하 PNG를 선택하세요.');const result=await window.parent.studio.contentIconPrepare({name:file.name,bytes:Array.from(new Uint8Array(await file.arrayBuffer()))});if(!result.ok)throw Error(result.error);draft.IconAddress=result.data.address;draft.IconPng=result.data.png;paint();}catch(error){dialog.querySelector('[data-edit-errors]').textContent=error.message;}return;}if(e.target.dataset.editField==='MissionType'){if(draft.MissionType==='Normal')draft.EventKey='';paint();}if(e.target.dataset.editField==='FunctionType'){ItemCatalog.resetFunction(draft,previousType);previousType=draft.FunctionType;paint();}if(e.target.dataset.editField==='OptionType'){draft.Rewards=[];draft.DeckId='';paint();}};
  dialog.onclick=e=>{
   if(e.target.closest('[data-edit-cancel]'))close();
   const reuse=e.target.closest('[data-edit-reuse]');if(reuse){onSave(p.tables.SelectionList.find(l=>l.SelectionListId===reuse.dataset.editReuse));close();return;}
   const ref=e.target.closest('[data-edit-ref]'),clear=e.target.closest('[data-edit-clear]'),remove=e.target.closest('[data-edit-remove]'),option=e.target.closest('[data-edit-option]');
   if(ref){const f=schema.fields.find(f=>f.key===ref.dataset.editRef);open({project:work,table:f.table,changed:()=>{},onPick:r=>{draft[f.key]=r[STUDIO_SCHEMAS[f.table].keys[0]];paint();}});}
   if(clear){draft[clear.dataset.editClear]='';paint();}
   if(remove){draft.Rewards.splice(Number(remove.dataset.editRemove),1);paint();}
   if(e.target.closest('[data-edit-reward]'))open({project:work,table:'Item',changed:()=>{},onPick:r=>{const old=draft.Rewards.find(a=>a.itemId===r.ItemId);if(old)old.amount++;else draft.Rewards.push({itemId:r.ItemId,amount:1});paint();}});
   if(option||e.target.closest('[data-edit-add-option]'))edit({project:work,table:'SelectionOption',row:option?work.tables.SelectionOption.find(o=>o.OptionId===option.dataset.editOption):null,initial:{SelectionListId:draft.SelectionListId},changed:()=>{},onSave:()=>paint()});
   if(e.target.closest('[data-edit-save]')){
    work.tables[table][index]=draft;
    const errors=Studio.validate(work).filter(i=>i.severity==='error'&&(i.table===table&&i.index===index||!before.has(JSON.stringify(i))));
    if(errors.length){dialog.querySelector('[data-edit-errors]').textContent=errors.map(i=>i.message).join(' / ');return;}
    Object.assign(p,work);changed();onSave(p.tables[table][index]);close();
   }
  };paint();dialog.showModal();
 }
 globalThis.CatalogPicker={open,edit,filtered};
})();
