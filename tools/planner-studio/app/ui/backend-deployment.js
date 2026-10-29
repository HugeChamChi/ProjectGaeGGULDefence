'use strict';
(() => {
  const $ = id => document.getElementById(id);
  const dialog = $('backendDeploymentDialog');
  const status = $('backendStatus');
  const verification = $('backendVerification');
  let prepared;

  async function call(name, payload) {
    const result = await window.studio[name](payload);
    if (!result.ok) throw Error(result.error);
    return result.data;
  }

  function setBusy(busy) {
    for (const id of ['backendKind', 'backendPrepare', 'backendVerify', 'backendProject', 'backendHistory', 'backendRestore']) {
      $(id).disabled = busy;
    }
  }

  function clearPrepared(message) {
    prepared = null;
    $('backendPrepared').hidden = true;
    status.textContent = message;
    verification.textContent = '';
  }

  async function prepare() {
    setBusy(true);
    clearPrepared('검사하고 파일을 준비하는 중…');
    try {
      const project = document.querySelector('#contentWorkspace iframe')?.contentWindow?.StudioUI?.snapshot();
      prepared = await call('contentBackendPrepare', { kind: $('backendKind').value, project });
      $('backendChart').textContent = prepared.targetChartName;
      $('backendPath').textContent = prepared.file;
      $('backendAuthoringPath').textContent = prepared.authoringFile;
      $('backendRelease').textContent = prepared.releaseKey;
      $('backendPrepared').hidden = false;
      status.textContent = '파일 준비 완료 · 서버 업로드·적용은 아직 확인하지 않았습니다.';
      await refreshHistory();
    } catch (error) {
      status.textContent = error.message;
    } finally {
      setBusy(false);
    }
  }

  async function verify() {
    setBusy(true);
    verification.textContent = '서버의 현재 적용 파일을 조회하는 중…';
    try {
      const result = await call('contentBackendVerify', { releaseKey: prepared.releaseKey });
      verification.textContent = `서버 적용 파일 일치 · ${result.chartName} · 파일 ID ${result.selectedChartFileId} · 게임 플레이 검증은 별도 진행하세요.`;
    } catch (error) {
      verification.textContent = '확인 실패 · ' + error.message;
    } finally {
      setBusy(false);
    }
  }

  async function report(action, output = status) {
    try { await action(); }
    catch (error) { output.textContent = error.message; }
  }

  function showPrepared(record) {
    prepared = record;
    $('backendKind').value = record.kind;
    $('backendChart').textContent = record.targetChartName;
    $('backendPath').textContent = record.file;
    $('backendAuthoringPath').textContent = record.authoringFile;
    $('backendRelease').textContent = record.releaseKey;
    $('backendPrepared').hidden = false;
    verification.textContent = '';
  }
  async function refreshHistory() {
    const records = await call('contentBackendHistory');
    $('backendHistory').replaceChildren(...records.map(record => {
      const option = document.createElement('option');
      option.value = record.releaseKey;
      option.textContent = `${record.createdAt} · ${record.targetChartName} · ${record.releaseKey}${record.available ? '' : ' · 파일 확인 필요'}`;
      option.disabled = !record.available;
      return option;
    }));
  }
  $('backendDeploymentButton').onclick = () => { dialog.showModal(); report(refreshHistory); };
  $('backendRestore').onclick = () => report(async () => {
    setBusy(true);
    try {
      clearPrepared('이전 배포 원본과 파일을 확인하는 중…');
      showPrepared(await call('contentBackendRestore', {releaseKey:$('backendHistory').value}));
      status.textContent = '이전 버전 복원 완료. 현재 서버 적용 파일을 다시 확인하세요.';
    } finally { setBusy(false); }
  });
  $('backendClose').onclick = () => dialog.close();
  $('backendKind').onchange = () => clearPrepared('대상이 변경되었습니다. 파일을 다시 준비하세요.');
  $('backendPrepare').onclick = prepare;
  $('backendVerify').onclick = verify;
  $('backendReveal').onclick = () => report(() => call('contentBackendFile', { releaseKey: prepared.releaseKey }));
  $('backendConsole').onclick = () => report(() => call('backendConsole'));
  $('backendExternalConsole').onclick = () => report(() => call('openConsole'));
  $('backendProject').onclick = () => report(async () => {
    const result = await call('backendChooseProject');
    if (result) verification.textContent = '조회할 프로젝트: ' + result.project;
  }, verification);
})();
