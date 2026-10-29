(()=>{
  'use strict';
  const esc=s=>String(s??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
  let unitId='',editing=false,draft=null,owner=null;
  globalThis.UnitOverview={render({project:p,changed,render,toast}){
    if(owner!==p){owner=p;editing=false;draft=null;}
    const list=[...p.tables.Unit].sort((a,b)=>Number(p.tables.UnitUpgrade.some(r=>r.UnitId===b.UnitId))-Number(p.tables.UnitUpgrade.some(r=>r.UnitId===a.UnitId)));
    if(!list.some(u=>u.UnitId===unitId)){unitId=list[0]?.UnitId||'';editing=false;}
    const source=editing?draft:p,u=source?.tables.Unit.find(u=>u.UnitId===unitId);
    if(!u){document.getElementById('content').innerHTML='<div class="empty">유닛 데이터가 없습니다.</div>';return;}
    const upgrades=source.tables.UnitUpgrade.filter(r=>r.UnitId===unitId).sort((a,b)=>a.TargetLevel-b.TargetLevel),bonuses=source.tables.UnitMilestone.filter(r=>r.UnitId===unitId).sort((a,b)=>a.Level-b.Level);
    const parties=source.tables.Party.filter(r=>r.Units.includes(unitId)||(u.Kind==='Chief'&&u.Source===r.ChiefAsset));
    const icon=STUDIO_ICONS[u.IconKey]?`<img src="${STUDIO_ICONS[u.IconKey].data}" alt="">`:'<span class="unit-placeholder">◇</span>';
    const number=(key,value,label)=>editing?`<input type="number" step="any" data-unit-value="${key}" value="${value??''}" aria-label="${label}">`:`<strong>${esc(value)}</strong>`;
    document.getElementById('content').innerHTML=`<div class="unit-overview"><section class="panel unit-roster"><div class="panel-head"><h2>유닛·족장</h2></div><div class="panel-body"><input id="unit-search" placeholder="이름 검색" aria-label="유닛 검색"></div><div class="unit-list">${list.map(x=>`<button data-unit-select="${esc(x.UnitId)}" class="${unitId===x.UnitId?'active':''}"><strong>${esc(x.Name)}</strong><small>${x.Kind==='Chief'?'족장':'유닛'} · 최대 Lv.${x.MaxLevel}</small></button>`).join('')}</div></section><div class="unit-detail"><section class="panel"><div class="panel-head"><div class="unit-heading">${icon}<div><h2>${esc(u.Name)}</h2><small>${esc(u.UnitId)}</small></div></div><div class="unit-edit-actions">${editing?'<button id="unit-cancel">취소</button><button id="unit-save" class="primary">수정 저장</button>':'<button id="unit-edit" class="primary">수정</button>'}</div></div><div class="panel-body"><div class="unit-summary"><label>이름${editing?`<input data-unit-value="Name" value="${esc(u.Name)}">`:`<strong>${esc(u.Name)}</strong>`}</label><label>구분<strong>${u.Kind==='Chief'?'족장 · 전체 유닛 보너스':'유닛 · 개별 성장'}</strong></label><label>최대 레벨${number('MaxLevel',u.MaxLevel,'최대 레벨')}</label><label>레벨별 수치<strong>${upgrades.length}개 구간</strong></label></div><div class="unit-status">${upgrades.length?'강화 시안의 비용·성장 수치':'강화 수치 미설계 · 현재 파티 기본 데이터'} · 레벨 보너스 ${bonuses.length}개</div><h3>소속 덱 · 기본 해금</h3>${parties.length?parties.map(r=>`<div class="unit-unlock"><span>${esc(r.Name)}</span>${editing?`<label><input type="checkbox" data-party-unlock="${esc(r.PartyId)}" ${r.IsUnlocked?'checked':''}>덱 기본 해금</label>`:`<strong>${r.IsUnlocked?'기본 해금':'잠김'}</strong>`}</div>`).join(''):'<p class="subtle">연결된 덱이 없습니다.</p>'}<p class="subtle">덱 해금은 해당 덱 전체에 적용됩니다. 개별 유닛 해금 조건은 현재 데이터에 없습니다.</p><h3>레벨 해금 보너스 요약</h3><div class="unit-bonus-strip">${bonuses.map(b=>`<div><b>Lv.${b.Level}</b><span>${esc(b.Description)}</span></div>`).join('')||'<p class="subtle">등록된 레벨 보너스가 없습니다.</p>'}</div>${bonuses.length?'<p class="subtle">현재 보너스는 UI 설명입니다. 전체 목록과 설명 수정은 레벨업 보너스 탭에서 확인하세요.</p>':''}</div></section><section class="panel unit-level-panel"><div class="panel-head"><div><h2>레벨별 강화 · 적용 수치</h2><small>${u.Kind==='Chief'?'전체 유닛 보너스':'유닛 배율'}와 필요 재화를 함께 확인합니다.</small></div>${editing?'<button id="unit-add-level">＋ 레벨 추가</button>':''}</div><table class="unit-level-table"><thead><tr><th>레벨</th><th>강화 비용</th><th>유닛 배율</th><th>전체 보너스</th><th>해금 보너스</th></tr></thead><tbody><tr><td>Lv.1</td><td>기본</td><td>×1</td><td>0%</td><td>—</td></tr>${upgrades.map((r,i)=>`<tr><td>Lv.${r.TargetLevel}</td><td>${r.Costs.map((c,k)=>`<span class="unit-cost">${esc(p.tables.Item.find(x=>x.ItemId===c.itemId)?.Name||c.itemId)} ${editing?`<input type="number" min="1" data-level-cost="${i}:${k}" value="${c.amount??''}" aria-label="Lv.${r.TargetLevel} 비용">`:Number(c.amount).toLocaleString()}</span>`).join('')||'무료'}${editing&&!r.Costs.length?`<button data-level-add-cost="${i}">비용 추가</button>`:''}</td><td>${editing?`<input type="number" step="any" min="0" data-level-multiplier="${i}" value="${r.Multiplier}" aria-label="Lv.${r.TargetLevel} 배율">`:'×'+r.Multiplier}</td><td>${editing?`<input type="number" step="any" min="0" data-level-global="${i}" value="${r.GlobalBonus*100}" aria-label="Lv.${r.TargetLevel} 전체 보너스 퍼센트">`:(Number((r.GlobalBonus*100).toFixed(6))+'%')}</td><td>${bonuses.filter(b=>b.Level===r.TargetLevel).map(b=>esc(b.Description)).join('<br>')||'—'}</td></tr>`).join('')}</tbody></table>${!upgrades.length?'<p class="panel-body subtle">아직 레벨별 강화 수치가 없습니다.</p>':''}<div id="unit-errors" class="panel-body" role="alert"></div></section></div></div>`;
    const root=document.querySelector('.unit-overview');
    const keyPanel=document.createElement('div');keyPanel.className='unit-key-links';
    keyPanel.innerHTML=`<h3>연결 키</h3><div class="unit-summary"><label>유닛 키<strong>${esc(u.UnitKey)}</strong></label>${[['BaseStatKey','기본 스탯'],['UpgradeKey','강화 성장'],['LevelBonusKey','레벨 보너스']].map(([key,label])=>{const keys=key==='BaseStatKey'?[...new Set(source.tables.UnitBaseStat.map(r=>r.BaseStatKey))]:[...new Set(source.tables.Unit.map(r=>r[key]).filter(Boolean))];return `<label>${label}${editing?`<select data-unit-ref="${key}"><option value="">없음</option>${keys.map(k=>`<option ${u[key]===k?'selected':''} value="${esc(k)}">${esc(k)}</option>`).join('')}</select>`:`<strong>${esc(u[key]||'없음')}</strong>`}</label>`;}).join('')}</div>`;
    const stats=source.tables.UnitBaseStat.filter(r=>r.BaseStatKey===u.BaseStatKey);
    keyPanel.innerHTML+=`<h3>기본 스탯 · ${esc(u.BaseStatKey||'해당 없음')}</h3>${stats.length?`<table class="unit-level-table"><thead><tr><th>등급</th><th>공격력</th><th>공격 속도(회/초)</th><th>쿨타임(초)</th><th>초당 식량</th><th>드론 수</th></tr></thead><tbody>${stats.map((r,i)=>`<tr><td>${esc(r.Tier)}</td>${['Attack','AttackInterval','SkillCooldown','FoodProduction','MaxDroneCount'].map(k=>`<td>${editing?`<input type="number" min="0" step="${k==='MaxDroneCount'?'1':'any'}" data-base-stat="${i}:${k}" value="${r[k]}">`:r[k]}</td>`).join('')}</tr>`).join('')}</tbody></table>`:'<p class="subtle">족장 SO에는 일반 유닛의 기본 스탯이 없습니다.</p>'}`;
    root.querySelector('.unit-detail > .panel .panel-body').prepend(keyPanel);
    const strip=root.querySelector('.unit-bonus-strip');
    bonuses.forEach((bonus,index)=>{
      const badge=document.createElement('small');badge.style.display='block';badge.style.color='#77859a';badge.textContent=bonus.EffectStatus==='Defined'?'효과 데이터 정의됨':'설명만 있음';strip.children[index]?.append(badge);
    });
    if(bonuses.length)strip.nextElementSibling.textContent='상세·효과 정의 상태는 레벨업 보너스 탭에서 확인하세요. 실제 전투 적용은 게임 연결이 필요합니다.';
    root.addEventListener('click',e=>{
      const b=e.target.closest('[data-unit-select]');
      if(b){if(editing&&!confirm('수정 중인 내용을 취소하고 다른 유닛을 볼까요?'))return;unitId=b.dataset.unitSelect;editing=false;draft=null;render();}
      if(e.target.closest('#unit-edit')){draft=Studio.clone(p);editing=true;render();}
      if(e.target.closest('#unit-cancel')){editing=false;draft=null;render();}
      if(e.target.closest('#unit-save')){
        UnitKeys.propagate(draft,unitId);UnitKeys.propagate(draft,unitId,true);
        const errors=Studio.validate(draft).filter(i=>i.severity==='error');if(errors.length){document.getElementById('unit-errors').textContent=errors.slice(0,4).map(i=>i.message).join(' / ');return;}
        Object.assign(p.tables.Unit.find(x=>x.UnitId===unitId),u);
        p.tables.UnitUpgrade=Studio.clone(draft.tables.UnitUpgrade);p.tables.UnitMilestone=Studio.clone(draft.tables.UnitMilestone);p.tables.UnitBaseStat=Studio.clone(draft.tables.UnitBaseStat);
        for(const party of parties)Object.assign(p.tables.Party.find(x=>x.PartyId===party.PartyId),party);
        editing=false;draft=null;changed();render();toast('유닛 정보를 저장했습니다.');
      }
      if(e.target.closest('#unit-add-level')){const target=1+Math.max(1,...upgrades.map(r=>r.TargetLevel));source.tables.UnitUpgrade.push({UnitId:unitId,TargetLevel:target,Costs:[],Multiplier:1,GlobalBonus:0,Source:'기획작업실'});u.MaxLevel=Math.max(u.MaxLevel,target);render();}
      const cost=e.target.closest('[data-level-add-cost]');if(cost){upgrades[Number(cost.dataset.levelAddCost)].Costs.push({itemId:p.tables.Item[0]?.ItemId||'Gold',amount:1});render();}
    });
    root.addEventListener('input',e=>{
      const el=e.target;
      if(el.id==='unit-search'){root.querySelectorAll('[data-unit-select]').forEach(b=>b.hidden=!b.textContent.toLowerCase().includes(el.value.toLowerCase()));return;}
      if(!editing)return;
      if(el.dataset.baseStat){const[i,key]=el.dataset.baseStat.split(':');stats[Number(i)][key]=el.value===''?null:Number(el.value);}
      if(el.dataset.unitValue){u[el.dataset.unitValue]=el.dataset.unitValue==='Name'?el.value:el.value===''?null:Number(el.value);}
      if(el.dataset.levelCost){const[i,k]=el.dataset.levelCost.split(':').map(Number);upgrades[i].Costs[k].amount=el.value===''?null:Number(el.value);}
      if(el.dataset.levelMultiplier!==undefined)upgrades[Number(el.dataset.levelMultiplier)].Multiplier=el.value===''?null:Number(el.value);
      if(el.dataset.levelGlobal!==undefined)upgrades[Number(el.dataset.levelGlobal)].GlobalBonus=el.value===''?null:Number(el.value)/100;
      if(el.dataset.partyUnlock)parties.find(r=>r.PartyId===el.dataset.partyUnlock).IsUnlocked=el.checked;
    });
    root.addEventListener('change',e=>{const key=e.target.dataset.unitRef;if(!editing||!key)return;const value=e.target.value;u[key]=value;if(key!=='BaseStatKey'){const table=key==='UpgradeKey'?'UnitUpgrade':'UnitMilestone',owner=source.tables.Unit.find(x=>x!==u&&x[key]===value);const linked=owner?source.tables[table].filter(r=>r.UnitId===owner.UnitId).map(r=>({...Studio.clone(r),UnitId:unitId})):[];source.tables[table]=source.tables[table].filter(r=>r.UnitId!==unitId).concat(linked);}render();});
  }};
})();
