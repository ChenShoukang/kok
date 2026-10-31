'use strict';
// Use the same outline glyphs for navigation and all fallback artwork.
const libraryObjectFiles={grid:'card_file_box_flat.svg',video:'clapper_board_flat.svg',photo:'framed_picture_flat.svg',music:'musical_note_flat.svg',document:'page_facing_up_flat.svg',book:'blue_book_flat.svg'};
function libraryObject(kind){return `<span class="flat-object-icon" aria-hidden="true">${svg(kind)}</span>`;}
function navigationIcon(...args){return Kok.invoke('navigationIcon',this,args);}
Kok.define('navigationIcon',function(kind){return libraryObjectFiles[kind]?`<span class="nav-art nav-art-${kind}" aria-hidden="true">${libraryObject(kind)}</span>`:svg(kind);});
(() => {
 const hint=document.createElement('div');hint.id='navigation-hint';hint.className='navigation-hint';hint.setAttribute('role','tooltip');hint.setAttribute('popover','manual');hint.setAttribute('data-user-content','');document.body.append(hint);
 let target=null,focusHint=false,pointer=null;
 let alignmentFrame=0;
 function alignSidebar(){alignmentFrame=0;}
 function scheduleAlignment(){if(!alignmentFrame)alignmentFrame=requestAnimationFrame(alignSidebar);}
 function hide(){if(target)target.removeAttribute('aria-describedby');target=null;hint.classList.remove('visible');if(hint.matches(':popover-open'))hint.hidePopover();}
 function show(button){
  if(!button||document.querySelector('dialog[open]'))return;
  const visibleLabel=button.querySelector('.nav-label');if(visibleLabel&&getComputedStyle(visibleLabel).display!=='none'&&visibleLabel.scrollWidth<=visibleLabel.clientWidth){hide();return;}
  if(target===button)return;hide();
  const label=button.getAttribute('aria-label')||button.querySelector('span:not(.nav-art):not(.count)')?.textContent?.trim();if(!label)return;
  target=button;const count=button.querySelector('.count')?.textContent.trim();hint.textContent=label+(count?' · '+count:'');button.setAttribute('aria-describedby',hint.id);hint.showPopover();
  const box=button.getBoundingClientRect();hint.style.left=Math.min(innerWidth-hint.offsetWidth-8,box.right+12)+'px';hint.style.top=Math.max(8,Math.min(innerHeight-hint.offsetHeight-8,box.y+(box.height-hint.offsetHeight)/2))+'px';
  requestAnimationFrame(()=>{if(target===button)hint.classList.add('visible');});
 }
 const entry=el=>el instanceof Element?el.closest('.sidebar .nav-item,.sidebar .brand,#add-collection'):null;
 document.addEventListener('pointerover',event=>{if(!focusHint)show(entry(event.target));});
 document.addEventListener('pointermove',event=>{const changed=!pointer||pointer.x!==event.clientX||pointer.y!==event.clientY;pointer={x:event.clientX,y:event.clientY};if(changed){focusHint=false;const button=entry(event.target);if(button)show(button);else hide();}},{passive:true});
 document.addEventListener('pointerout',event=>{if(target?.contains(event.target)&&!target.contains(event.relatedTarget))hide();});
 document.addEventListener('focusin',event=>{const button=entry(event.target);if(button){focusHint=true;show(button);}else hide();});
 document.addEventListener('focusout',event=>{if(target?.contains(event.target))hide();});
 document.addEventListener('pointerdown',()=>{focusHint=false;hide();});document.addEventListener('keydown',event=>{if(event.key==='Escape')hide();});
 document.querySelector('.sidebar-scroll').addEventListener('scroll',hide,{passive:true});window.addEventListener('resize',hide);window.addEventListener('blur',hide);
 new MutationObserver(hide).observe(document.documentElement,{attributes:true,attributeFilter:['lang']});
 new MutationObserver(()=>{if(target&&!target.isConnected)hide();scheduleAlignment();}).observe(document.querySelector('.sidebar'),{childList:true,subtree:true});scheduleAlignment();
})();
