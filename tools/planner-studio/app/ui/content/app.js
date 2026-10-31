(async()=>{
  'use strict';
  const C=Studio,S=STUDIO_SCHEMAS,N=STUDIO_NAV,$=id=>document.getElementById(id),storageKey='GaeGGUL.OutgameStudio.v2';
  const esc=s=>String(s??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
  let project=C.create(),section='overview',table='RewardGroup',selected=0,search='',treeKey='BASIC',preview=false,levels={},dirty=false,iconTarget=null;
  let recovery='';
  if(window.desktopContent) await window.desktopContent.restore(storageKey);
  try{const saved=localStorage.getItem(storageKey);if(saved){project=C.migrate(JSON.parse(saved));recovery='통합 프로젝트를 자동 복구했습니다.';}}catch{recovery='자동 복구를 사용할 수 없습니다. JSON으로 저장하세요.';}
  const row=()=>project.tables[table][selected];
  const image=(key,cls='item-icon')=>STUDIO_ICONS[key]?`<img class="${cls}" src="${STUDIO_ICONS[key].data}" alt="">`:`<span class="${cls} placeholder-icon">◇</span>`;
  const item=id=>project.tables.Item.find(i=>i.ItemId===id);
  function toast(text){$('toast').textContent=text;$('toast').style.display='block';clearTimeout(toast.timer);toast.timer=setTimeout(()=>$('toast').style.display='none',4000);}
  function changed(){dirty=true;try{localStorage.setItem(storageKey,JSON.stringify(project));if(window.desktopContent)window.desktopContent.autosave(project);$('save-state').textContent='자동 복구 저장됨 · JSON 보관 권장';}catch{$('save-state').textContent='JSON 파일로 저장하세요';}}
  function chips(list){return list.map(a=>`<div class="reward-chip">${image(item(a.itemId)?.IconKey)}<div><small>${esc(item(a.itemId)?.Name||a.itemId)}</small><strong>× ${Number.isInteger(a.amount)?a.amount.toLocaleString():'확인 필요'}</strong></div></div>`).join('');}
  function selectTable(name,index=0){table=name;selected=Math.max(0,Math.min(index,project.tables[name].length-1));search='';section=N.find(n=>n.tables.includes(name))?.id||section;if(name==='ResearchNode'&&row())treeKey=row().TreeKey;render();}
  function render(){
    $('nav').innerHTML=N.map(n=>`<button data-nav="${n.id}" class="${section===n.id?'active':''}">${n.icon}<span>${n.label}</span></button>`).join('');
    const nav=N.find(n=>n.id===section);$('page-title').textContent=nav.label;
    $('tabs').innerHTML=nav.tables.filter(name=>!(section==='units'&&name==='UnitUpgrade')).map(name=>`<button data-table="${name}" class="${name===table?'active':''}">${S[name].label}<span>${project.tables[name].length}</span></button>`).join('');
    $('context').textContent=section==='overview'?'실제 프로젝트 데이터와 신규 기획을 구분하며 작업하세요.':project.meta.statuses[table]||S[table].note||'공통 기획 데이터 · 원본 출처를 유지하며 편집합니다.';
    if(section==='overview')renderOverview();else if(table==='ResearchNode')renderTree();else renderTable();PlannerScope.renderTabs(section);
  }
  function renderOverview(){ $('content').innerHTML=PlannerScope.overview(project); }
  const titleFor=(name,r)=>String(C.label(name,r));
  function listMarkup(){const matches=ItemCatalog.names.includes(table)?new Set(CatalogPicker.filtered(project,table,search)):null;return project.tables[table].map((r,i)=>({r,i})).filter(({r})=>!search||(matches?matches.has(r):JSON.stringify(r).toLowerCase().includes(search.toLowerCase()))).map(({r,i})=>`<button data-row="${i}" class="record-row ${selected===i?'active':''}">${r.IconKey?image(r.IconKey):''}<div><strong>${esc(titleFor(table,r).slice(0,120))}</strong><small>${S[table].keys.map(k=>esc(r[k])).join(' / ')}</small></div></button>`).join('')||'<div class="empty">표시할 데이터가 없습니다.<br>새 항목을 추가하거나 검색을 바꿔 주세요.</div>';}
  function renderTable(){
    if(table==='BonusDefinition')return BonusDesignUI.render({project,changed,render,toast});
    if(table==='DataBindings')return DataBindingsUI.render({project,changed,render,toast});
    if(['Unit','UnitUpgrade'].includes(table))return UnitOverview.render({project,changed,render,toast});
    if(['RewardGroup','Attendance'].includes(table))return RewardGrid.render({project,table,changed,render,toast});
    const schema=S[table];
    $('content').innerHTML=`${table==='Attendance'?attendancePreview():''}<div class="data-layout"><section class="panel records-panel"><div class="panel-head"><h3>${schema.label}</h3><small>${project.tables[table].length}개</small></div><div class="search-wrap"><input id="search" placeholder="이름 · ID · 내용 검색" value="${esc(search)}" aria-label="목록 검색"></div><div class="record-list" id="record-list">${listMarkup()}</div>${!schema.readonly?'<div class="panel-body"><button class="full" data-action="new">＋ 새 항목</button></div>':''}</section><section class="panel inspector"><div class="panel-head"><h2>${schema.readonly?'저장 구조 참고':'상세 편집'}</h2><small>${schema.readonly?'실제 유저 데이터 없음':'변경 시 자동 복구 저장'}</small></div><div class="panel-body" id="editor">${row()?editorMarkup():`<div class="empty">${esc(project.meta.statuses[table]||'새 기획을 추가해 주세요.')}<br><button data-action="new" class="primary">첫 항목 만들기</button></div>`}</div></section></div>`;
  }
  function attendancePreview(){
    const plan=project.tables.AttendancePlan[0];if(!plan)return '';
    return `<div class="attendance-strip">${project.tables.Attendance.filter(d=>d.AttendanceId===plan.AttendanceId).sort((a,b)=>a.Day-b.Day).map(d=>`<button data-attendance="${project.tables.Attendance.indexOf(d)}"><b>${d.Day}일차</b><div class="preview-items">${chips(project.tables.RewardGroup.find(g=>g.RewardGroupId===d.RewardGroupId)?.Rewards||[])}</div></button>`).join('')}</div>`;
  }
  function options(f,r){
    if(table==='DataBindings'&&f.key==='ValueSource')return f.values.filter(v=>EVENT_CATALOG.hooks.find(h=>h.HookKey===r.HookKey)?.Sources.split(';').includes(v)).map(v=>({value:v,label:f.labels?.[v]||v}));
    if(f.type==='enum')return f.values.map(v=>({value:v,label:f.labels?.[v]||v}));
    if(f.type==='stat')return project.meta.stats.map(s=>({value:s.value,label:s.label+(s.percent?' (%)':' (고정)')}));
    if(f.type==='ref'||f.type==='refs')return project.tables[f.table].map(x=>({value:x[C.primary(f.table)],label:`${titleFor(f.table,x)} · ${x[C.primary(f.table)]}`}));
    if(f.type==='parents')return project.tables.ResearchNode.filter(n=>n.TreeKey===r.TreeKey&&n!==r).map(n=>({value:n.NodeId,label:n.NodeId+' · '+(project.meta.stats.find(s=>s.value===n.Stat)?.label||n.Stat)}));
    return [];
  }
  function fieldMarkup(f,r){
    const v=r[f.key],disabled=S[table].readonly?'disabled':'',attrs=`data-field="${f.key}" ${disabled}`;
    if(['rewards','costs'].includes(f.type))return `<div class="field-block full-field"><h3>${f.label}</h3><div class="composer">${v.map((a,i)=>`<div class="reward-row">${image(item(a.itemId)?.IconKey)}<div class="item-info"><strong>${esc(item(a.itemId)?.Name||a.itemId)}</strong><small>${esc(a.itemId)}</small></div><input type="number" min="1" max="2147483647" step="1" value="${a.amount??''}" data-quantity="${f.key}:${i}" aria-label="${esc(item(a.itemId)?.Name||a.itemId)} 수량"><button data-order="${f.key}:${i}" ${i===0?'disabled':''} aria-label="위로">↑</button><button data-remove="${f.key}:${i}" aria-label="제거">×</button></div>`).join('')}<div><input data-reward-search="${f.key}" placeholder="이름 · ID · 설명 검색"><select data-reward-type="${f.key}"><option value="">전체 종류</option>${STUDIO_SCHEMAS.Item.fields.find(f=>f.key==='ItemType').values.map(t=>`<option value="${esc(t)}">${esc(STUDIO_SCHEMAS.Item.fields.find(f=>f.key==='ItemType').labels?.[t]||t)}</option>`).join('')}</select><button data-reward-create="${f.key}">＋ 새 아이템</button></div><div class="catalog-add">${project.tables.Item.map(i=>`<button data-reward-item="${esc(i.ItemId)}" data-reward-field="${f.key}" data-add="${f.key}:${esc(i.ItemId)}">${image(i.IconKey)}<span>${esc(i.Name)} ＋</span></button>`).join('')}</div><div class="preview-items" data-preview="${f.key}">${chips(v)}</div><code class="serialized" data-code="${f.key}">${esc(C.serialize(v))||'없음 (비용은 무료)'}</code></div></div>`;
    if(f.type==='ref')return `<label>${esc(f.label)}<button type="button" data-ref-field="${esc(f.key)}">${esc(project.tables[f.table].find(r=>r[C.primary(f.table)]===v)?.Name||v||'검색하여 연결 / 새로 추가')}</button>${v&&ItemCatalog.names.includes(f.table)?`<button type="button" data-ref-edit="${esc(f.key)}">연결 항목 편집</button>`:''}<button type="button" data-ref-clear="${esc(f.key)}">연결 해제</button></label>`;
    if(f.type==='icon')return `<label>${f.label}<button type="button" class="icon-picker" data-icon="${f.key}">${image(v)}<span>게임 아이콘 선택 →</span></button></label>`;
    if(f.type==='bool')return `<label class="checkbox"><input type="checkbox" ${attrs} ${v?'checked':''}>${f.label}</label>`;
    if(['ref','enum','stat'].includes(f.type))return `<label>${f.label}<select ${attrs}>${f.type==='ref'?'<option value="">선택 안 함</option>':''}${options(f,r).map(o=>`<option value="${esc(o.value)}" ${o.value===v?'selected':''}>${esc(o.label)}</option>`).join('')}</select></label>`;
    if(['refs','parents'].includes(f.type))return `<div class="full-field"><label>${f.label}</label><div class="check-list">${options(f,r).map(o=>`<label class="checkbox"><input type="checkbox" data-multi="${f.key}" value="${esc(o.value)}" ${v.includes(o.value)?'checked':''}>${esc(o.label)}</label>`).join('')||'<span class="subtle">선택 가능한 항목이 없습니다.</span>'}</div></div>`;
    if(table==='Tutorial'&&f.key==='Instruction')return TutorialRich.field(v);
    if(f.type==='textarea')return `<label class="full-field">${f.label}<textarea ${attrs} rows="3">${esc(v)}</textarea></label>`;
    if(f.type==='strings')return `<label>${f.label} (세미콜론 구분)<input ${attrs} value="${esc(v.join(';'))}"></label>`;
    if(f.type==='datetime'){const time=v&&Number.isFinite(Date.parse(v))?new Date(Date.parse(v)+9*3600000).toISOString().slice(0,16):'';return `<label>${f.label}<input type="datetime-local" ${attrs} value="${esc(time)}"></label>`;}
    return `<label class="${f.key==='Source'?'full-field':''}">${f.label}<input type="${['int','float'].includes(f.type)?'number':'text'}" ${attrs} ${f.type==='int'?'step="1"':f.type==='float'?'step="any"':''} ${f.min!==undefined?`min="${f.min}"`:''} ${f.max!==undefined?`max="${f.max}"`:''} value="${esc(v)}"></label>`;
  }
  function errorsMarkup(errors){return errors.length?`<div class="validation">${errors.map(i=>`${i.severity==='warning'?'△':'!'} ${esc(S[i.table].fields.find(f=>f.key===i.field)?.label||i.field)}: ${esc(i.message)}`).join('<br>')}</div>`:'';}
  function nodeSummary(n){const stat=project.meta.stats.find(s=>s.value===n.Stat),value=n.ValuePerLevel*n.MaxLevel;return `<div class="preview"><div class="preview-title">최대 레벨 보너스 · 비용 전개</div><strong>${esc(stat?.label||n.Stat)} +${Number((value*(stat?.percent?100:1)).toPrecision(8))}${stat?.percent?'%':''}</strong><p class="subtle">${n.CostItemId?`${esc(item(n.CostItemId)?.Name||n.CostItemId)}: 1레벨 ${n.BaseCost}, 마지막 레벨 ${n.BaseCost+(n.MaxLevel-1)*n.CostStep}`:'무료 (현재 게임과 동일)'}</p></div>`;}
  function editorMarkup(){
    const r=row();if(!r)return '<div class="empty">항목을 선택하세요.</div>';
    return `<div class="record-heading">${r.IconKey?image(r.IconKey):''}<div><h2>${esc(titleFor(table,r))}</h2><small>${S[table].keys.map(k=>esc(r[k])).join(' / ')}</small></div></div><div id="record-errors">${errorsMarkup(C.validate(project).filter(i=>i.table===table&&i.index===selected))}</div>${S[table].note?`<p class="editor-note">${esc(S[table].note)}</p>`:''}<div class="form-grid">${S[table].fields.filter(f=>!f.hidden).map(f=>fieldMarkup(f,r)).join('')}</div>${table==='SelectionList'?`<section><h3>포함 선택 옵션</h3>${project.tables.SelectionOption.filter(o=>o.SelectionListId===r.SelectionListId).map(o=>`<button data-option-edit="${esc(o.OptionId)}">${esc(o.Name)} · ${esc(o.Rewards.map(a=>(item(a.itemId)?.Name||a.itemId)+' ×'+a.amount).join(' + ')||o.DeckId)}</button>`).join('')}<button data-option-add>＋ 선택 옵션 추가</button></section>`:''}${table==='ResearchNode'?`<div id="node-summary">${nodeSummary(r)}</div>`:''}${!S[table].readonly?'<div class="editor-bottom"><button data-action="duplicate">항목 복제</button><button data-action="delete" class="danger">항목 삭제</button></div>':''}`;
  }
  function renderTree(){
    if(!project.tables.ResearchTree.some(t=>t.TreeKey===treeKey))treeKey=project.tables.ResearchTree[0]?.TreeKey||'';
    if(!row()||row().TreeKey!==treeKey)selected=project.tables.ResearchNode.findIndex(n=>n.TreeKey===treeKey);
    $('content').innerHTML=`<div class="tree-toolbar"><label>트리 선택<select id="tree-select">${project.tables.ResearchTree.map(t=>`<option value="${esc(t.TreeKey)}" ${t.TreeKey===treeKey?'selected':''}>${esc(t.Name)} · ${esc(t.TreeKey)}</option>`).join('')}</select></label><button data-action="tree-settings">트리 설정</button><button data-action="new">＋ 노드 추가</button><label class="checkbox"><input type="checkbox" id="preview-toggle" ${preview?'checked':''}>해금 미리보기</label>${preview?'<button data-action="preview-upgrade">선택 노드 +1</button><button data-action="preview-reset">진행 초기화</button>':''}</div><div class="tree-workspace"><section class="panel"><div class="panel-head"><h3>${esc(treeKey)} <small>${preview?'검토용 진행 · 계정 저장 없음':'클릭 = 편집 · 드래그 = 배치'}</small></h3><span class="subtle">아래 → 위</span></div><div class="tree-scroll" id="tree-scroll"><div id="tree-canvas"></div></div></section><section class="panel inspector"><div class="panel-head"><h2>노드 편집</h2></div><div class="panel-body" id="editor">${editorMarkup()}</div></section></div>`;
    paintTree();const scroller=$('tree-scroll');scroller.scrollTop=scroller.scrollHeight;
  }
  function paintTree(){
    const canvas=$('tree-canvas');if(!canvas)return;
    const nodes=project.tables.ResearchNode.filter(n=>n.TreeKey===treeKey),height=(Math.max(0,...nodes.map(n=>n.Row))+1)*130+110,width=660;
    const pos=n=>({x:135+(n.Column+1)*135,y:height-75-n.Row*130});
    const states=C.treeState(project,treeKey,levels[treeKey]||{}),map=new Map(nodes.map(n=>[n.NodeId,n]));
    canvas.innerHTML=`<svg id="tree-svg" viewBox="0 0 ${width} ${height}" style="min-width:${width}px;height:${height}px" aria-label="영구강화 트리"><defs><pattern id="dots" width="22" height="22" patternUnits="userSpaceOnUse"><circle cx="1" cy="1" r="1" fill="#d9e2f1"/></pattern></defs><rect width="100%" height="100%" fill="url(#dots)"/>${nodes.flatMap(n=>n.Parents.map(id=>{const p=map.get(id);if(!p)return '';const a=pos(p),b=pos(n);return `<path d="M ${a.x} ${a.y-35} V ${(a.y+b.y)/2} H ${b.x} V ${b.y+35}" fill="none" stroke="${n.ExclusiveGroup?'#c2a56b':'#afc1dc'}" stroke-width="3" ${n.RequireAnyParent?'stroke-dasharray="6 4"':''}/>`;})).join('')}${nodes.map(n=>{const p=pos(n),index=project.tables.ResearchNode.indexOf(n),stat=project.meta.stats.find(s=>s.value===n.Stat);return `<g class="tree-node ${selected===index?'selected':''} ${preview?states[n.NodeId]:''}" data-node="${index}" transform="translate(${p.x},${p.y})" tabindex="0" role="button" aria-label="${esc(n.NodeId)} ${esc(stat?.label)}"><title>${esc(n.NodeId)} · ${esc(stat?.label)}</title><rect x="-48" y="-39" width="96" height="78" rx="${n.Shape==='Square'?15:38}" class="node-box ${n.Shape==='Special'?'special':''}"/>${STUDIO_ICONS[n.IconKey]?`<image href="${STUDIO_ICONS[n.IconKey].data}" x="-20" y="-29" width="40" height="40"/>`:''}<text y="28" text-anchor="middle">${esc(n.NodeId)}${preview?' '+((levels[treeKey]||{})[n.NodeId]||0)+'/'+n.MaxLevel:''}</text><text y="58" text-anchor="middle" class="node-stat">${esc(stat?.label||n.Stat)}</text>${n.ExclusiveGroup?`<text y="73" text-anchor="middle" class="node-exclusive">택1 · ${esc(n.ExclusiveGroup)}</text>`:''}</g>`;}).join('')}</svg>`;
    const svg=$('tree-svg');let drag=null;
    svg.onpointerdown=e=>{const node=e.target.closest('[data-node]');if(!node||preview)return;drag={index:Number(node.dataset.node),x:e.clientX,y:e.clientY};svg.setPointerCapture(e.pointerId);};
    svg.onpointerup=e=>{
      if(!drag)return;const d=drag;drag=null;if(Math.hypot(e.clientX-d.x,e.clientY-d.y)<8){selected=d.index;renderTree();return;}
      const rect=svg.getBoundingClientRect(),n=project.tables.ResearchNode[d.index],x=(e.clientX-rect.left)*width/rect.width,y=(e.clientY-rect.top)*height/rect.height;
      const col=Math.max(-1,Math.min(2,Math.round((x-135)/135)-1)),r=Math.max(0,Math.min(200,Math.round((height-75-y)/130)));
      if(nodes.some(o=>o!==n&&o.Column===col&&o.Row===r)){toast('이미 노드가 있는 위치입니다.');return;}
      n.Column=col;n.Row=r;selected=d.index;changed();const top=$('tree-scroll').scrollTop;renderTree();$('tree-scroll').scrollTop=top;
    };
  }
  function commit(field,value){
    const f=S[table].fields.find(f=>f.key===field);if(!f||S[table].readonly)return;const r=row();
    if(S[table].keys.includes(field)&&project.tables[table].some((o,i)=>i!==selected&&S[table].keys.every(k=>o[k]===(k===field?value:r[k])))){toast('이미 사용 중인 키입니다.');render();return;}
    C.update(project,table,selected,field,value);changed();
    if($('record-list'))$('record-list').innerHTML=listMarkup();
    if($('record-errors'))$('record-errors').innerHTML=errorsMarkup(C.validate(project).filter(i=>i.table===table&&i.index===selected));
    if(table==='ResearchNode'){if(field==='TreeKey'){treeKey=value;renderTree();}else{paintTree();if($('node-summary'))$('node-summary').innerHTML=nodeSummary(r);}}
  }
  document.addEventListener('change',e=>{
    const el=e.target;
    if(el.id==='tree-select'){treeKey=el.value;selected=project.tables.ResearchNode.findIndex(n=>n.TreeKey===treeKey);renderTree();return;}
    if(el.id==='preview-toggle'){preview=el.checked;renderTree();return;}
    if(el.dataset.multi){const current=row()[el.dataset.multi];commit(el.dataset.multi,el.checked?[...current,el.value]:current.filter(x=>x!==el.value));return;}
    if(!el.dataset.field)return;const f=S[table].fields.find(f=>f.key===el.dataset.field);let value=el.value;
    if(f.type==='bool')value=el.checked;if(['int','float'].includes(f.type))value=value===''?null:Number(value);
    if(f.type==='strings')value=value.split(';').map(s=>s.trim());if(f.type==='datetime')value=value?value+':00+09:00':'';commit(f.key,value);
  });
  document.addEventListener('input',e=>{
    const el=e.target;if(table==='Tutorial'&&el.dataset.field==='Instruction'){row().Instruction=el.value;changed();TutorialRich.update(el.value);}if(el.id==='search'){search=el.value;$('record-list').innerHTML=listMarkup();}
    if(el.dataset.quantity){const [field,index]=el.dataset.quantity.split(':');row()[field][Number(index)].amount=el.value===''?null:Number(el.value);changed();document.querySelector(`[data-preview="${field}"]`).innerHTML=chips(row()[field]);document.querySelector(`[data-code="${field}"]`).textContent=C.serialize(row()[field]);}
  });
  function filterRewardItems(field){const query=document.querySelector(`[data-reward-search="${field}"]`)?.value||'',type=document.querySelector(`[data-reward-type="${field}"]`)?.value||'',ids=new Set(CatalogPicker.filtered(project,'Item',query,type).map(r=>r.ItemId));document.querySelectorAll(`[data-reward-field="${field}"]`).forEach(b=>b.hidden=!ids.has(b.dataset.rewardItem));}
  document.addEventListener('input',e=>{if(e.target.dataset.rewardSearch)filterRewardItems(e.target.dataset.rewardSearch);});
  document.addEventListener('change',e=>{if(e.target.dataset.rewardType)filterRewardItems(e.target.dataset.rewardType);});
  document.addEventListener('keydown',e=>{if((e.key==='Enter'||e.key===' ')&&e.target.matches('[data-node]')){selected=Number(e.target.dataset.node);$('editor').innerHTML=editorMarkup();paintTree();e.preventDefault();}});
  document.addEventListener('click',e=>{
    const b=e.target.closest('button,[data-node]');if(!b)return;
    if(b.dataset.refField||b.dataset.refEdit){const field=b.dataset.refField||b.dataset.refEdit,f=S[table].fields.find(f=>f.key===field);if(b.dataset.refEdit)CatalogPicker.edit({project,table:f.table,row:project.tables[f.table].find(r=>r[C.primary(f.table)]===row()[field]),changed,onSave:r=>{commit(field,r[C.primary(f.table)]);renderTable();}});else CatalogPicker.open({project,table:f.table,changed,onPick:r=>{commit(field,r[C.primary(f.table)]);renderTable();}});return;}
    if(b.dataset.refClear){commit(b.dataset.refClear,'');renderTable();return;}
    if(b.dataset.rewardCreate){const field=b.dataset.rewardCreate;CatalogPicker.edit({project,table:'Item',changed,onSave:r=>{row()[field].push({itemId:r.ItemId,amount:1});changed();renderTable();}});return;}
    if(b.dataset.optionEdit||b.hasAttribute('data-option-add')){CatalogPicker.edit({project,table:'SelectionOption',row:b.dataset.optionEdit?project.tables.SelectionOption.find(o=>o.OptionId===b.dataset.optionEdit):null,initial:{SelectionListId:row().SelectionListId},changed,onSave:()=>renderTable()});return;}
    if(b.dataset.action==='new'&&ItemCatalog.names.includes(table)){CatalogPicker.edit({project,table,changed,onSave:r=>{selected=project.tables[table].indexOf(r);renderTable();}});return;}
    if(b.dataset.close!==undefined){b.closest('dialog').close();return;}
    if(b.dataset.nav){section=b.dataset.nav;table=N.find(n=>n.id===section).tables[0]||table;selected=0;search='';render();return;}
    if(b.dataset.table){selectTable(b.dataset.table);return;}if(b.dataset.open){selectTable(b.dataset.open);return;}
    if(b.dataset.row!==undefined){selected=Number(b.dataset.row);renderTable();return;}
    if(b.dataset.attendance!==undefined){selected=Number(b.dataset.attendance);renderTable();return;}
    if(b.dataset.node!==undefined){selected=Number(b.dataset.node);$('editor').innerHTML=editorMarkup();paintTree();return;}
    if(b.dataset.issue){const issue=JSON.parse(b.dataset.issue);selectTable(issue.table,issue.index);$('export-dialog').close();return;}
    if(b.dataset.icon){iconTarget=b.dataset.icon;$('icon-list').innerHTML=Object.entries(STUDIO_ICONS).map(([key,icon])=>`<button data-pick-icon="${key}" title="${esc(icon.source)}">${image(key)}<small>${esc(icon.source.split('/').pop())}</small></button>`).join('');$('icon-dialog').showModal();return;}
    if(b.dataset.pickIcon){commit(iconTarget,b.dataset.pickIcon);$('icon-dialog').close();$('editor').innerHTML=editorMarkup();paintTree();return;}
    if(b.dataset.add){const [field,id]=b.dataset.add.split(':'),list=row()[field],existing=list.find(x=>x.itemId===id);if(existing){if(Number.isInteger(existing.amount)&&existing.amount<2147483647)existing.amount++;else{toast('수량을 먼저 확인하세요.');return;}}else list.push({itemId:id,amount:1});changed();$('editor').innerHTML=editorMarkup();return;}
    if(b.dataset.remove){const [field,index]=b.dataset.remove.split(':');row()[field].splice(Number(index),1);changed();$('editor').innerHTML=editorMarkup();return;}
    if(b.dataset.order){const [field,index]=b.dataset.order.split(':'),i=Number(index),list=row()[field];if(i>0)[list[i-1],list[i]]=[list[i],list[i-1]];changed();$('editor').innerHTML=editorMarkup();return;}
    const action=b.dataset.action;if(!action)return;
    if(action==='tree-settings'){selectTable('ResearchTree',project.tables.ResearchTree.findIndex(t=>t.TreeKey===treeKey));return;}
    if(action==='preview-reset'){levels[treeKey]={};paintTree();return;}
    if(action==='preview-upgrade'){const n=row(),state=C.treeState(project,treeKey,levels[treeKey]||{});if(!n||state[n.NodeId]!=='available'){toast('선행/택1/전체 레벨 조건을 충족하지 않습니다.');return;}levels[treeKey]??={};levels[treeKey][n.NodeId]=(levels[treeKey][n.NodeId]||0)+1;paintTree();return;}
    if(S[table].readonly)return;
    if(action==='new'){const fresh=C.newRecord(table,project);if(table==='ResearchNode'){fresh.TreeKey=treeKey;fresh.Row=1+Math.max(-1,...project.tables.ResearchNode.filter(n=>n.TreeKey===treeKey).map(n=>n.Row));}project.tables[table].push(fresh);selected=project.tables[table].length-1;search='';}
    else if(action==='duplicate'&&row()){
      const copy=C.clone(row()),pk=C.primary(table);
      if(typeof copy[pk]==='string'&&S[table].keys.length===1){let n=1;while(project.tables[table].some(r=>r[pk]===copy[pk]+'_Copy'+n))n++;copy[pk]+='_Copy'+n;}
      else if(table==='ResearchNode'){copy.NodeId+='Copy';while(project.tables.ResearchNode.some(n=>n.TreeKey===copy.TreeKey&&n.NodeId===copy.NodeId))copy.NodeId+='1';copy.Row=1+Math.max(...project.tables.ResearchNode.filter(n=>n.TreeKey===copy.TreeKey).map(n=>n.Row));}
      else if(S[table].keys.length>1&&typeof copy[S[table].keys.at(-1)]==='number')copy[S[table].keys.at(-1)]++;
      if(copy.Name)copy.Name+=' 복사';copy.Source='복제 기획';project.tables[table].push(copy);selected=project.tables[table].length-1;
    }else if(action==='delete'&&row()){
      const refs=C.references(project,table,selected);if(refs.length){toast('먼저 연결을 해제하세요: '+refs.slice(0,3).join(', '));return;}
      if(!confirm(`“${titleFor(table,row())}” 항목을 삭제할까요?`))return;project.tables[table].splice(selected,1);selected=Math.max(0,selected-1);
    }else return;changed();render();
  });
  async function download(name,data,type){if(window.desktopContent)return await window.desktopContent.exportFile(name,data,type);const url=URL.createObjectURL(new Blob([data],{type})),a=document.createElement('a');a.href=url;a.download=name;document.body.append(a);a.click();a.remove();setTimeout(()=>URL.revokeObjectURL(url),30000);}
  async function save(){try{document.activeElement?.blur();await download('outgame-project.json',JSON.stringify(project,null,2),'application/json');dirty=false;toast('전체 프로젝트 JSON을 저장했습니다.');}catch(e){alert('저장 실패: '+e.message);}}
  $('save').onclick=save;$('export-json').onclick=save;$('load').onclick=()=>$('file').click();
  $('file').onchange=async e=>{const file=e.target.files[0];if(!file)return;try{if(file.size>20000000)throw Error('프로젝트는 20MB 이하여야 합니다.');const imported=file.name.toLowerCase().endsWith('.xlsx')?await window.desktopContent.importWorkbook(file,project):null;const loaded=imported?imported.project:C.migrate(JSON.parse(await file.text()));if(!confirm('현재 작업을 불러온 프로젝트로 바꿀까요? 필요한 작업은 먼저 저장해 주세요.'))return;project=loaded;selected=0;section='overview';levels={};changed();render();if(imported)window.StudioUI.loadProject(loaded,imported.names);toast('자료를 불러왔습니다.');}catch(error){alert('불러오기 실패\n'+error.message);}finally{e.target.value='';}};
  $('export').onclick=()=>{
    document.activeElement?.blur();const issues=C.validate(project),errors=issues.filter(i=>i.severity==='error');
    $('export-validation').innerHTML=issues.length?`<div class="validation">오류 ${errors.length}개 · 경고 ${issues.length-errors.length}개</div><div class="issue-list">${issues.map(i=>`<button data-issue="${esc(JSON.stringify(i))}">${i.severity==='error'?'!':'△'} ${S[i.table].label} ${i.index+1}행 / ${esc(i.field)}<small>${esc(i.message)} → 수정</small></button>`).join('')}</div>`:'<div class="valid">✓ ID · 수량 · 참조 · 트리 순환 · 출석 일차 검사를 통과했습니다.</div>';
    for(const id of ['export-current','export-all','export-csv'])$(id).disabled=errors.length>0;$('export-current').textContent=S[table].label+' XLSX';$('export-list').textContent=[...PlannerScope.active,'ResearchLevel'].join(', ');$('export-dialog').showModal();
  };
  function manifest(){return {schemaVersion:2,projectName:project.name,note:'기획 규약 v2. 기존 게임/뒤끝 자동 반영 없음. UserSchema는 계정 원본이 아닌 설계 참고. Research 비용/이벤트 등은 후속 런타임 연결 필요.',sourceHashes:project.meta.sources,statuses:Object.fromEntries(Object.entries(project.meta.statuses).filter(([name])=>PlannerScope.active.has(name)))};}
  $('export-current').onclick=async()=>{try{if(table==='BonusDefinition'){await window.desktopContent.exportWorkbook(project,'bonuses',table);toast('보너스·효과 Excel을 함께 추출했습니다.');return;}const t=C.tables(project),sheet=UnitKeys.sheetFor[table]||AttendanceEvents.sheetFor[table]||table;await download(sheet+'.xlsx',RewardCore.xlsx(sheet,t[sheet]),'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet');toast('현재 표를 추출했습니다.');}catch(e){alert(e.message);}};
  async function allExport(csv){try{const data=C.tables(project),files={};for(const [name,rows]of Object.entries(data))files[name+(csv?'.csv':'.xlsx')]=csv?'\ufeff'+rows.map(r=>r.map(v=>'"'+String(v).replace(/"/g,'""')+'"').join(',')).join('\r\n'):RewardCore.xlsx(name,rows);files['manifest.json']=JSON.stringify(manifest(),null,2);await download(csv?'outgame-csv.zip':'outgame-charts.zip',RewardCore.zip(files),'application/zip');toast('전체 표를 ZIP으로 추출했습니다.');}catch(e){alert(e.message);}}
  $('export-all').onclick=()=>allExport(false);$('export-csv').onclick=()=>allExport(true);$('help').onclick=()=>$('help-dialog').showModal();
  window.StudioUI={snapshot:()=>C.clone(project),openWorkspace(kind){PlannerScope.workspace=kind;section=kind==='enhancement'?'units':kind==='rewards'?'attendance':'overview';table=kind==='enhancement'?'UnitUpgrade':'Attendance';selected=0;render();},loadProject(p,names=[]){project=C.shape(p);const first=names.includes('UnitUpgrade')?'UnitUpgrade':names.includes('Attendance')?'Attendance':names.includes('ResearchNode')?'ResearchNode':names[0];if(first){table=first;section=N.find(n=>n.tables.includes(first))?.id||'overview';PlannerScope.workspace=['ResearchNode','ResearchTree','Unit','UnitUpgrade','UnitMilestone','BonusDefinition','BonusEffect'].includes(first)?'enhancement':[...ItemCatalog.names,'RewardGroup','Attendance','AttendancePlan',...MissionEvents.names].includes(first)?'rewards':'settings';}selected=0;changed();render();}};document.addEventListener('click',e=>{const b=e.target.closest('[data-workspace-section]');if(b){section=b.dataset.workspaceSection;table=section==='items'?'Item':section==='units'?'UnitUpgrade':section==='research'?'ResearchNode':section==='attendance'?'Attendance':section==='missions'?'Missions':section==='dataBindings'?'DataBindings':'RewardGroup';selected=0;render();}});$('workbook-export').onclick=async()=>{try{document.activeElement?.blur();const result=await window.desktopContent.exportWorkbook(project,PlannerScope.workspace,table);toast('Excel 저장 완료 · 왼쪽 내보낸 파일에서 확인');}catch(e){alert(e.message);}};window.addEventListener('beforeunload',e=>{if(dirty){e.preventDefault();e.returnValue='';}});render();if(recovery)toast(recovery);
})();
