// Keep large workspace views bounded without hiding records or changing sources.
// Each view owns its page; nothing is persisted as library data.
export const CollectionPages={
 size:120,views:new Map(),
 clear(){this.views.clear();},
 render(key,rows,draw){
  let view=this.views.get(key);if(!view){if(this.views.size>=16)this.views.delete(this.views.keys().next().value);view={page:0};this.views.set(key,view);}
  Object.assign(view,{rows,draw});view.page=Math.max(0,Math.min(view.page,Math.ceil(rows.length/this.size)-1));return this.paint(key,view);
 },
 paint(key,view){
  const start=view.page*this.size,end=Math.min(start+this.size,view.rows.length),english=document.documentElement.lang.startsWith('en');
  return `<section class="collection-page" data-collection-page="${encodeURIComponent(key)}">${view.draw(view.rows.slice(start,end))}${view.rows.length>this.size?`<nav class="collection-pagination" aria-label="${english?'Collection pages':'集合分页'}"><button type="button" class="text-button" data-collection-move="-1" ${view.page===0?'disabled':''}>${english?'Previous':'上一页'}</button><span aria-live="polite">${start+1}–${end} / ${view.rows.length}</span><button type="button" class="text-button" data-collection-move="1" ${end===view.rows.length?'disabled':''}>${english?'Next':'下一页'}</button></nav>`:''}</section>`;
 },
 install(){document.addEventListener('click',event=>{const button=event.target.closest('[data-collection-move]'),root=button?.closest('[data-collection-page]');if(!root)return;const key=decodeURIComponent(root.dataset.collectionPage),view=this.views.get(key);if(!view)return;event.preventDefault();const next=view.page+Number(button.dataset.collectionMove);if(next<0||next*this.size>=view.rows.length)return;view.page=next;const direction=button.dataset.collectionMove;root.innerHTML=this.paint(key,view).replace(/^<section[^>]*>|<\/section>$/g,'');root.querySelector(`[data-collection-move="${direction}"]:not(:disabled)`)?.focus({preventScroll:true});root.scrollIntoView({block:'start',behavior:'instant'});document.dispatchEvent(new CustomEvent('kok-collection-page'));});}
};
