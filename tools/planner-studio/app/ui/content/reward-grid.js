(()=>{
  'use strict';
  const esc=s=>String(s??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
  let selected=0,paletteOpen=false,eventId='';
  const image=key=>STUDIO_ICONS[key]?`<img src="${STUDIO_ICONS[key].data}" alt="">`:'<span>◇</span>';
  globalThis.RewardGrid={render(api){
    const {project:p,table,changed,render,toast}=api,attendance=table==='Attendance';
    const groups=p.tables.RewardGroup,plans=p.tables.AttendancePlan;
    if(!plans.some(r=>r.AttendanceId===eventId))eventId=plans[0]?.AttendanceId||'';
    const rows=attendance?p.tables.Attendance.filter(r=>r.AttendanceId===eventId):groups;selected=Math.max(0,Math.min(selected,rows.length-1));
    const group=i=>rows[i];
    function ensure(i){return rows[i];}
    const cell=(g,i)=>`<div class="reward-cell ${selected===i?'selected':''}" data-reward-cell="${i}" tabindex="0" aria-label="보상 셀">${(g?.Rewards||[]).map((r,k)=>{const item=p.tables.Item.find(x=>x.ItemId===r.itemId);return `<div class="reward-token" draggable="true" data-token="${i}:${k}">${image(item?.IconKey)}<span>${esc(item?.Name||r.itemId)}</span><input type="number" min="1" step="1" value="${r.amount??''}" data-grid-amount="${i}:${k}" aria-label="${esc(item?.Name||r.itemId)} 수량"><button data-grid-remove="${i}:${k}" aria-label="보상 제거">×</button></div>`;}).join('')||'<span class="cell-empty">아이콘을 여기에 끌어놓으세요</span>'}</div>`;
    document.getElementById('content').innerHTML=`<div class="reward-workbench"><section class="panel reward-sheet"><div class="panel-head"><div><h2>${attendance?'출석 일차별 보상':'보상 구성표'}</h2><small>한 셀에 여러 아이템 · 셀 선택 후 아이콘 클릭도 가능</small></div><button id="grid-add-row">＋ ${attendance?'일차':'보상'} 추가</button></div><table class="reward-table"><thead><tr><th>${attendance?'일차':'보상 이름'}</th><th>보상 셀</th></tr></thead><tbody>${rows.map((r,i)=>`<tr><td>${attendance?`<strong>${r.Day}일차</strong><small>${esc(plans.find(x=>x.AttendanceId===r.AttendanceId)?.Name||r.AttendanceId)}</small>`:`<input data-grid-name="${i}" value="${esc(r.Name)}" aria-label="보상 이름"><small>${esc(r.RewardGroupId)}</small>`}</td><td>${cell(group(i),i)}</td></tr>`).join('')}</tbody></table></section><section class="panel reward-palette"><div class="panel-head"><h2>우리 아이템</h2></div><div class="panel-body"><input id="grid-item-search" placeholder="아이템 검색" aria-label="아이템 검색"><p class="subtle">아이콘을 보상 셀로 드래그하세요.<br>클릭하면 선택한 셀에 추가됩니다.</p><div id="grid-item-list">${p.tables.Item.map(item=>`<button class="palette-item" draggable="true" data-palette-item="${esc(item.ItemId)}">${image(item.IconKey)}<span>${esc(item.Name)}<small>${esc(item.ItemId)}</small></span></button>`).join('')}</div></div></section></div>`;
    const root=document.querySelector('.reward-workbench'),palette=root.querySelector('.reward-palette');
    root.querySelector('.reward-sheet .panel-head small').textContent='보상 추가 → 아이콘 선택 → 수량 입력';
    root.querySelectorAll('[data-reward-cell]').forEach(cell=>{
      const button=document.createElement('button');button.className='cell-add-reward';button.dataset.openPalette=cell.dataset.rewardCell;button.textContent='＋ 보상 추가';cell.append(button);
    });
    const toggle=document.createElement('button');toggle.id='grid-toggle-palette';toggle.type='button';
    root.querySelector('.reward-sheet .panel-head').append(toggle);
    const close=document.createElement('button');close.type='button';close.dataset.closePalette='';close.textContent='닫기';palette.querySelector('.panel-head').append(close);
    const target=document.createElement('p');target.className='palette-target';palette.querySelector('.panel-body').prepend(target);
    function syncPalette(){
      palette.hidden=!paletteOpen;root.classList.toggle('palette-open',paletteOpen);
      toggle.textContent=paletteOpen?'아이템 목록 닫기':'아이템 목록 열기';toggle.setAttribute('aria-expanded',String(paletteOpen));
      target.textContent=rows[selected]?(attendance?`${rows[selected].Day}일차에 추가`:`${rows[selected].Name}에 추가`):'먼저 보상 행을 추가하세요';
      root.querySelectorAll('[data-reward-cell]').forEach(c=>c.classList.toggle('selected',Number(c.dataset.rewardCell)===selected));
    }
    syncPalette();
    const eventSelect=document.createElement('select');eventSelect.id='attendance-event-select';eventSelect.setAttribute('aria-label','출석 이벤트 선택');
    for(const plan of plans){const option=document.createElement('option');option.value=plan.AttendanceId;option.textContent=plan.Name+' · '+(plan.Enabled?'활성':'비활성');option.selected=plan.AttendanceId===eventId;eventSelect.append(option);}
    root.querySelector('.reward-sheet .panel-head').prepend(eventSelect);
    const plan=plans.find(r=>r.AttendanceId===eventId),summary=document.createElement('p');summary.className='panel-body subtle';summary.style.margin='0';summary.textContent=plan?`${plan.Enabled?'활성':'비활성 예시'} · ${plan.CycleDays}일 · 공통 초기화 시간 적용 · ${plan.RepeatMode==='Cycle'?'완주 후 반복':'기간 중 한 번'} · ${plan.StartAt||'시작 미설정'} ~ ${plan.EndAt||'종료 미설정'}`:'이벤트 설정에서 새 출석 이벤트를 추가하세요.';
    root.querySelector('.reward-sheet .panel-head').after(summary);
    eventSelect.onchange=()=>{eventId=eventSelect.value;selected=0;render();};
    function add(i,itemId,amount=1){if(!rows[i]){toast('먼저 보상 행을 추가하세요.');return;}const g=ensure(i),existing=g.Rewards.find(r=>r.itemId===itemId);if(existing)existing.amount+=amount;else g.Rewards.push({itemId,amount});selected=i;changed();render();}
    root.addEventListener('dragstart',e=>{
      if(e.target.tagName==='INPUT'){e.preventDefault();return;}
      const item=e.target.closest('[data-palette-item]'),token=e.target.closest('[data-token]');if(!item&&!token)return;
      e.dataTransfer.setData('application/x-ggd-reward',JSON.stringify(item?{itemId:item.dataset.paletteItem}:{token:token.dataset.token}));e.dataTransfer.effectAllowed=item?'copy':'move';
    });
    root.addEventListener('dragover',e=>{const c=e.target.closest('[data-reward-cell]');if(c&&[...e.dataTransfer.types].includes('application/x-ggd-reward')){e.preventDefault();c.classList.add('drag-over');}});
    root.addEventListener('dragleave',e=>e.target.closest('[data-reward-cell]')?.classList.remove('drag-over'));
    root.addEventListener('drop',e=>{
      const c=e.target.closest('[data-reward-cell]');if(!c)return;e.preventDefault();
      try{const value=JSON.parse(e.dataTransfer.getData('application/x-ggd-reward')),i=Number(c.dataset.rewardCell);
        if(value.itemId&&p.tables.Item.some(x=>x.ItemId===value.itemId))add(i,value.itemId);
        else if(value.token){const [from,k]=value.token.split(':').map(Number);if(from===i)return;const reward=ensure(from).Rewards.splice(k,1)[0];if(reward)add(i,reward.itemId,reward.amount);}
      }catch{toast('아이템 목록의 아이콘을 끌어놓으세요.');}
    });
    root.addEventListener('click',e=>{
      const open=e.target.closest('[data-open-palette]');
      if(open){selected=Number(open.dataset.openPalette);paletteOpen=true;syncPalette();document.getElementById('grid-item-search').focus();return;}
      if(e.target.closest('#grid-toggle-palette')){paletteOpen=!paletteOpen;syncPalette();return;}
      if(e.target.closest('[data-close-palette]')){paletteOpen=false;syncPalette();return;}
      const item=e.target.closest('[data-palette-item]'),remove=e.target.closest('[data-grid-remove]'),c=e.target.closest('[data-reward-cell]');
      if(item){add(selected,item.dataset.paletteItem);return;}
      if(remove){const [i,k]=remove.dataset.gridRemove.split(':').map(Number);ensure(i).Rewards.splice(k,1);changed();render();return;}
      if(c){selected=Number(c.dataset.rewardCell);syncPalette();}
    });
    root.addEventListener('input',e=>{
      if(e.target.id==='grid-item-search'){const query=e.target.value.toLowerCase();root.querySelectorAll('[data-palette-item]').forEach(b=>b.hidden=!b.textContent.toLowerCase().includes(query));}
      if(e.target.dataset.gridAmount){const[i,k]=e.target.dataset.gridAmount.split(':').map(Number);ensure(i).Rewards[k].amount=e.target.value===''?null:Number(e.target.value);changed();}
      if(e.target.dataset.gridName!==undefined){rows[Number(e.target.dataset.gridName)].Name=e.target.value;changed();}
    });
    document.getElementById('grid-add-row').onclick=()=>{
      if(attendance){if(!plans.length){toast('이벤트 설정을 먼저 추가하세요.');return;}const plan=plans.find(r=>r.AttendanceId===eventId);const day=1+Math.max(0,...rows.map(r=>r.Day));p.tables.Attendance.push({AttendanceId:plan.AttendanceId,Day:day,RewardGroupId:'',Rewards:[],Source:'기획작업실'});plan.CycleDays=day;}
      else groups.push(Studio.newRecord('RewardGroup',p));selected=rows.length-1;changed();render();
    };
  }};
})();
