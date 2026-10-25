'use strict';
(()=>{
  const original=document.querySelector('body > main'),panel=document.createElement('section');
  panel.id='contentWorkspace';panel.hidden=true;
  const frame=document.createElement('iframe');frame.title='기획 데이터 편집';panel.append(frame);original.after(panel);
  const settings=document.getElementById('contentWorkspaceButton');settings.lastChild.textContent='기본 설정';
  for(const [id,label,icon]of [['enhancement','강화 밸런스','⌘'],['rewards','이벤트','▦']]){
    const b=document.createElement('button');b.className='nav-item';b.id=id+'WorkspaceButton';
    const span=document.createElement('span');span.className='nav-icon';span.textContent=icon;b.append(span,document.createTextNode(label));settings.before(b);
  }
  const subnav=document.createElement('div');subnav.id='contentSubnav';subnav.hidden=true;settings.after(subnav);
  let pending='settings',loaded=null;
  function sync(){
    const kind=frame.contentWindow?.PlannerScope?.workspace||pending;
    for(const id of ['enhancement','rewards'])document.getElementById(id+'WorkspaceButton').classList.toggle('active',!panel.hidden&&kind===id);
    settings.classList.toggle('active',!panel.hidden&&kind==='settings');subnav.hidden=panel.hidden||kind!=='settings';
  }
  function show(kind){
    const content=kind!=='balance';original.hidden=content;panel.hidden=!content;
    document.getElementById('balanceWorkspaceButton').classList.toggle('active',!content);
    if(content){pending=kind;if(!frame.hasAttribute('src'))frame.src='content/index.html';else if(frame.contentWindow.StudioUI)frame.contentWindow.StudioUI.openWorkspace(kind);}
    sync();
  }
  frame.addEventListener('load',()=>{
    const ready=()=>{
      const w=frame.contentWindow;if(!w.StudioUI){setTimeout(ready,30);return;}
      subnav.replaceChildren(...w.STUDIO_NAV.filter(n=>['party','economy','account','profile','tutorial'].includes(n.id)).map(n=>{
        const b=document.createElement('button');b.className='content-section';b.textContent=n.label;b.dataset.section=n.id;
        b.onclick=()=>{w.PlannerScope.workspace='settings';frame.contentDocument.querySelector(`[data-nav="${n.id}"]`).click();sync();};return b;
      }));
      w.StudioUI.openWorkspace(pending);
      if(loaded){w.StudioUI.loadProject(loaded.project,loaded.names);loaded=null;}
      new MutationObserver(sync).observe(frame.contentDocument.getElementById('nav'),{childList:true,subtree:true});sync();
    };ready();
  });
  document.getElementById('balanceWorkspaceButton').onclick=()=>show('balance');
  document.getElementById('enhancementWorkspaceButton').onclick=()=>show('enhancement');
  document.getElementById('rewardsWorkspaceButton').onclick=()=>show('rewards');settings.onclick=()=>show('settings');
  window.addEventListener('content-imported',e=>{
    if(frame.contentWindow?.StudioUI){original.hidden=true;panel.hidden=false;frame.contentWindow.StudioUI.loadProject(e.detail.project,e.detail.names);sync();}
    else{loaded=e.detail;show('settings');}
  });
})();
