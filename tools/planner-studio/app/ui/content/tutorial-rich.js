(()=>{
  'use strict';
  const esc=s=>String(s??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
  const colour=v=>/^(?:#[0-9a-f]{3,4}|#[0-9a-f]{6}|#[0-9a-f]{8}|red|blue|green|white|black|yellow|orange|purple|cyan|magenta|grey|gray)$/i.test(v)?v:null;
  function html(value){
    const tokens=String(value??'').split(/(<[^>]*>)/g),stack=[];let result='',literal=false;
    for(const token of tokens){
      if(literal){if(/^<\/noparse>$/i.test(token))literal=false;else result+=esc(token).replace(/\r?\n/g,'<br>');continue;}
      if(/^<noparse>$/i.test(token)){literal=true;continue;}
      if(/^<br\s*\/?>$/i.test(token)){result+='<br>';continue;}
      const end=/^<\/(b|i|u|s|color|size|mark|alpha)>$/i.exec(token);
      if(end&&stack.at(-1)?.name===end[1].toLowerCase()){result+=stack.pop().end;continue;}
      const simple=/^<(b|i|u|s)>$/i.exec(token);
      if(simple){const name=simple[1].toLowerCase();result+='<'+name+'>';stack.push({name,end:'</'+name+'>'});continue;}
      const match=/^<(color|size|mark|alpha)=(?:"([^"]+)"|'([^']+)'|([^>\s]+))>$/i.exec(token),hex=/^<(#[0-9a-f]{6}(?:[0-9a-f]{2})?)>$/i.exec(token);
      if(match||hex){
        const name=hex?'color':match[1].toLowerCase(),v=hex?hex[1]:match[2]??match[3]??match[4];let style='';
        if(['color','mark'].includes(name)&&colour(v))style=(name==='color'?'color:':'background-color:')+v;
        if(name==='alpha'&&/^#[0-9a-f]{2}$/i.test(v))style='opacity:'+(parseInt(v.slice(1),16)/255);
        if(name==='size'){
          const size=/^(\d+(?:\.\d+)?)(px|%|em)?$/i.exec(v);
          if(size){const n=Number(size[1]),unit=(size[2]||'px').toLowerCase();if(n>0&&n<=(unit==='%'?400:unit==='em'?8:96))style='font-size:'+n+unit;}
        }
        if(style){result+='<span style="'+style+'">';stack.push({name,end:'</span>'});continue;}
      }
      result+=esc(token).replace(/\r?\n/g,'<br>');
    }
    return result+stack.reverse().map(s=>s.end).join('');
  }
  globalThis.TutorialRich={html,field(value){return `<div class="full-field tutorial-rich"><label for="tutorial-instruction">안내 문구 · 리치 텍스트</label><div class="rich-toolbar"><button type="button" data-rich-tag="b"><b>굵게</b></button><button type="button" data-rich-tag="i"><i>기울임</i></button><button type="button" data-rich-tag="u"><u>밑줄</u></button><label>색상 <input type="color" id="rich-colour" value="#D66A27"></label><button type="button" data-rich-tag="color">색 적용</button><label>크기 <input type="number" id="rich-size" min="8" max="96" value="24"></label><button type="button" data-rich-tag="size">크기 적용</button></div><textarea id="tutorial-instruction" data-field="Instruction" rows="5">${esc(value)}</textarea><div class="rich-preview-label">미리보기</div><div id="tutorial-rich-preview" class="tutorial-rich-preview">${html(value)}</div><p class="subtle">태그를 포함한 원문을 저장합니다. 굵게·기울임·밑줄·색상·크기·줄바꿈 등을 미리 봅니다. 폰트·배치는 게임 화면과 다를 수 있습니다.</p></div>`;},update(value){const preview=document.getElementById('tutorial-rich-preview');if(preview)preview.innerHTML=html(value);}};
  document.addEventListener('pointerdown',e=>{if(e.target.closest('[data-rich-tag]'))e.preventDefault();});
  document.addEventListener('click',e=>{
    const button=e.target.closest('[data-rich-tag]');if(!button)return;
    const input=document.getElementById('tutorial-instruction');if(!input)return;
    const tag=button.dataset.richTag,start=input.selectionStart,end=input.selectionEnd,text=input.value.slice(start,end);
    let open='<'+tag+'>';
    if(tag==='color')open='<color='+document.getElementById('rich-colour').value+'>';
    if(tag==='size'){const size=Number(document.getElementById('rich-size').value);if(!Number.isFinite(size)||size<8||size>96)return;open='<size='+size+'>';}
    input.setRangeText(open+text+'</'+tag+'>',start,end,'select');input.focus();input.setSelectionRange(start+open.length,start+open.length+text.length);
    input.dispatchEvent(new Event('input',{bubbles:true}));
  });
})();
