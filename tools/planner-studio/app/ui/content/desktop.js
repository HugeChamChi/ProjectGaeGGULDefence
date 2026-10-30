(() => {
  'use strict';
  let saveQueue = Promise.resolve();
  const api = window.parent.studio;
  async function call(name, payload) {
    const result = await api[name](payload);
    if (!result.ok) throw Error(result.error);
    return result.data;
  }
  window.desktopContent = {
    async importWorkbook(file,project){return call('contentWorkbookImport',{bytes:Array.from(new Uint8Array(await file.arrayBuffer())),project});},
    async exportWorkbook(project,workspace,table){await saveQueue;return call('contentWorkbookExport',{project,bundle:['BonusDefinition','BonusEffect'].includes(table)?'bonuses':MissionEvents.names.includes(table)?'missions':workspace==='enhancement'?'enhancement':workspace==='rewards'?'rewards':null,table});},
    async restore(key) {
      try {
        const project = await call('contentLoad');
        if (project) localStorage.setItem(key, JSON.stringify(Studio.shape(project)));
      } catch (error) {
        alert('저장된 콘텐츠를 불러오지 못했습니다. 자동 복구 사본을 확인하세요.\n' + error.message);
      }
    },
    autosave(project) {
      const snapshot = JSON.parse(JSON.stringify(project));
      saveQueue = saveQueue.catch(() => {}).then(() => call('contentSave', { project: snapshot }));
      saveQueue.catch(error => {
        document.getElementById('save-state').textContent = '파일 자동 저장 실패 · JSON 보관 필요';
        console.error(error);
      });
    },
    async exportFile(name, data, type) {
      await saveQueue;
      const bytes = Array.from(new Uint8Array(await new Blob([data], { type }).arrayBuffer()));
      const result = await call('contentExport', { name, bytes });
      const status = document.getElementById('save-state');
      status.textContent = '파일 저장 완료 · 왼쪽 내보낸 파일에서 확인';
      status.title = result.file;
      return result;
    }
  };
})();
