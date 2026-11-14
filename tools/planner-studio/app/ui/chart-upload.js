'use strict';
(()=>{
  const $=id=>document.getElementById(id);
  const button=document.createElement('button');button.id='chartUploadButton';button.className='nav-item';button.textContent='뒤끝 차트 업로드';$('backendDeploymentButton').after(button);
  const panel=document.createElement('dialog');panel.id='chartUploadDialog';
  panel.innerHTML=`<div class="dialog-heading"><h2>뒤끝 차트 업로드</h2><button class="icon-button" id="chartUploadClose" aria-label="닫기">×</button></div>
    <p class="settings-description">차트 파일을 선택하고 주의 창에서 확인하면 업로드와 파일 적용을 진행합니다. 처음에는 DEV에서 확인하세요.</p>
    <div class="dialog-actions"><button class="button secondary" id="chartUploadProject">프로젝트 선택</button><button class="button secondary" id="chartUploadStatus">로그인·차트 확인</button><button class="button secondary" id="chartUploadLogin">업로드용 로그인</button></div>
    <label class="guide-edit-label" for="chartUploadEnvironment">적용 환경</label><select id="chartUploadEnvironment"><option value="DEV">DEV · 개발 / 내부 테스트</option><option value="LIVE">LIVE · 출시 앱</option></select>
    <label class="guide-edit-label" for="chartUploadChart">차트 내용 이름 (DEV_ / LIVE_ 제외)</label><input id="chartUploadChart" class="upload-input" list="chartUploadNames" placeholder="예: SkillBalance"><datalist id="chartUploadNames"></datalist>
    <label class="guide-edit-label" for="chartUploadFiles">준비된 파일</label><select id="chartUploadFiles" hidden></select><input id="chartUploadFile" class="upload-input" readonly placeholder="업로드할 Excel 또는 CSV 선택"><button class="button secondary" id="chartUploadChooseFile">파일 선택</button>
    <p class="settings-description">업로드는 게임 규칙 검사를 대신하지 않습니다. 해당 차트의 업로드 전 검사를 먼저 완료하세요. 확률표와 COMMON_ 차트는 이 업로드 대상이 아닙니다.</p>
    <p id="chartUploadMessage" role="status">파일과 대상 차트를 선택하세요.</p><p id="chartUploadResult" role="status"></p>
    <div class="dialog-actions"><button class="button secondary" id="chartUploadHistory">최근 업로드 이력</button><button class="button primary" id="chartUploadReview">내용 확인 후 업로드…</button></div><div id="chartUploadHistoryList"></div>`;
  const warning=document.createElement('dialog');warning.id='chartUploadWarning';
  warning.innerHTML=`<div class="dialog-heading"><h2 id="chartUploadWarningTitle">주의!</h2></div><p id="chartUploadWarningText" class="upload-warning"></p><dl class="guide-properties"><dt>적용 차트</dt><dd id="chartUploadWarningChart"></dd><dt>업로드 파일</dt><dd id="chartUploadWarningFile"></dd><dt>파일 크기</dt><dd id="chartUploadWarningSize"></dd></dl><p>서버 처리가 끝나면 저장된 행을 대조한 뒤 새 파일을 적용합니다. 기존 적용 파일이 교체됩니다. 게임 규칙 검사와 DEV 동작 확인을 먼저 완료하세요.</p><p>여러 차트는 하나씩 적용됩니다. 한 차트가 성공해도 나머지 차트가 함께 반영되지는 않습니다.</p><div class="dialog-actions"><button class="button secondary" id="chartUploadCancel">취소</button><button class="button primary" id="chartUploadConfirm">확인 후 업로드·적용</button></div>`;
  document.body.append(panel,warning);
  let busy=false,plan=null;
  const controls=()=>panel.querySelectorAll('button,input,select');
  function setBusy(value){busy=value;controls().forEach(el=>el.disabled=value);button.disabled=value;}
  async function call(name,payload){const r=await window.studio[name](payload);if(!r.ok)throw Object.assign(new Error(r.error),{code:r.code});return r.data;}
  function error(e){$('chartUploadMessage').textContent=e.code==='LOGIN_REQUIRED'?'로그인이 필요합니다. 「업로드용 로그인」을 누른 후 파일과 대상을 다시 확인하세요.':e.message;}
  async function action(fn){if(busy)return;setBusy(true);try{await fn();}catch(e){error(e);}finally{setBusy(false);}}
  function chartFrom(file){return file.split(/[\\/]/).pop().replace(/\.(xlsx|xls|csv)$/i,'').replace(/\.backend$/i,'').replace(/_\d{4}-\d{2}-\d{2}.*$/,'').replace(/^(DEV|LIVE)_/,'');}
  function selectFile(file,chart){$('chartUploadFile').value=file;$('chartUploadChart').value=chart||chartFrom(file);$('chartUploadResult').textContent='';}
  function refreshNames(names){$('chartUploadNames').replaceChildren(...names.filter(n=>n.startsWith($('chartUploadEnvironment').value+'_')).map(n=>{const option=document.createElement('option');option.value=n.replace(/^(DEV|LIVE)_/,'');return option;}));}
  window.ChartUploadUI={open(files=[]){
    if(busy)return;if(!panel.open){$('chartUploadEnvironment').value='DEV';panel.showModal();}
    if(files.length)$('chartUploadEnvironment').value='DEV';
    if(files.length){$('chartUploadFiles').replaceChildren(...files.map(({file,chart})=>{const option=document.createElement('option');option.value=file;option.dataset.chart=chart||chartFrom(file);option.textContent=option.dataset.chart+' · '+file.split(/[\\/]/).pop();return option;}));$('chartUploadFiles').hidden=files.length<2;selectFile(files[0].file,files[0].chart);}
  }};
  button.onclick=()=>window.ChartUploadUI.open();
  $('chartUploadClose').onclick=()=>{if(!busy)panel.close();};
  panel.addEventListener('cancel',e=>{if(busy)e.preventDefault();});
  warning.addEventListener('close',()=>{if(!warning.open)plan=null;});
  warning.addEventListener('cancel',()=>{plan=null;});
  $('chartUploadCancel').onclick=()=>{plan=null;warning.close();};
  $('chartUploadChooseFile').onclick=()=>action(async()=>{const r=await call('chartUploadChooseFile');if(r){$('chartUploadFiles').hidden=true;selectFile(r.file);}});
  $('chartUploadFiles').onchange=()=>{const option=$('chartUploadFiles').selectedOptions[0];selectFile(option.value,option.dataset.chart);};
  $('chartUploadProject').onclick=()=>action(async()=>{const r=await call('chartUploadChooseProject');if(r)$('chartUploadMessage').textContent='프로젝트 선택: '+r.project;});
  $('chartUploadStatus').onclick=()=>action(async()=>{$('chartUploadMessage').textContent='업로드용 로그인과 차트 목록을 확인하는 중…';const r=await call('chartUploadStatus');refreshNames(r.charts);$('chartUploadMessage').textContent=r.project+' · 로그인 확인 · 차트 '+r.chartCount+'개';});
  $('chartUploadLogin').onclick=()=>action(async()=>{$('chartUploadMessage').textContent='열린 브라우저에서 로그인하세요. 프로젝트 화면이 열리면 자동으로 완료됩니다.';await call('chartUploadLogin');$('chartUploadMessage').textContent='업로드용 로그인 완료. 파일과 대상을 확인하세요.';});
  $('chartUploadReview').onclick=()=>action(async()=>{
    const p=await call('chartUploadPlan',{environment:$('chartUploadEnvironment').value,chart:$('chartUploadChart').value.trim(),file:$('chartUploadFile').value});plan=p;
    $('chartUploadWarningTitle').textContent='주의! '+p.environment+' 차트 반영';
    $('chartUploadWarningText').textContent=p.environment==='LIVE'?'출시 앱이 사용하는 LIVE 차트를 변경합니다. 적용하면 다음 데이터 로드부터 실제 사용자에게 반영될 수 있습니다.':'개발·내부 테스트 앱이 사용하는 DEV 차트를 변경합니다.';
    $('chartUploadWarningChart').textContent=p.chartName;$('chartUploadWarningFile').textContent=p.file;$('chartUploadWarningSize').textContent=p.bytes.toLocaleString()+' bytes';
    $('chartUploadConfirm').textContent=p.environment+' 업로드·적용';$('chartUploadConfirm').classList.toggle('upload-live',p.environment==='LIVE');warning.showModal();
  });
  $('chartUploadConfirm').onclick=()=>{if(!plan||busy)return;const confirmed=plan;plan=null;warning.close();action(async()=>{
    $('chartUploadResult').textContent='';$('chartUploadMessage').textContent=confirmed.chartName+' 업로드 → 서버 처리 → 행 대조 → 적용 확인 중…';
    const r=await call('chartUploadRun',{id:confirmed.id,confirmed:true,confirmLive:confirmed.environment==='LIVE'});
    $('chartUploadMessage').textContent=r.chartName+' 업로드·적용 확인 완료 · '+r.rowCount+'행';
    $('chartUploadResult').textContent='적용 파일 ID: '+r.fileId+' / 이전 파일 ID: '+(r.previousFileId??'없음')+' · 되돌릴 때는 콘솔에서 이전 파일을 다시 적용하세요. 게임 플레이 검증은 별도입니다.';
  });};
  $('chartUploadHistory').onclick=()=>action(async()=>{const rows=await call('chartUploadHistory');$('chartUploadHistoryList').replaceChildren(...rows.slice(0,20).map(r=>{const item=document.createElement('p');item.textContent=r.startedAt+' · '+r.chartName+' · '+(r.ok?'성공 / 적용 '+r.result.fileId+' / 이전 '+(r.result.previousFileId??'없음'):'실패 / '+r.message);return item;}));});
})();
