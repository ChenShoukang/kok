'use strict';

const LibraryCategories=(()=>{
 const symbols={
  film:{label:'胶片盘',icon:'video',match:/电影|影片|影视|film|movie/i,tone:'sand'},
  series:{label:'复古电视',icon:'monitor',match:/剧集|电视剧|连续剧|series|television/i,tone:'sage'},
  camera:{label:'相机',icon:'photo',match:/视频|摄影|相册|video|photo/i,tone:'slate'},
  book:{label:'布面书',icon:'book',match:/书|阅读|学习|文献|book|study|reading/i,tone:'rose'},
  folder:{label:'档案夹',icon:'folder',tone:'sage'},
  record:{label:'黑胶唱片',icon:'music',match:/音乐|唱片|专辑|music|album/i,tone:'sand'},
  microphone:{label:'麦克风',icon:'mic',match:/歌手|录音|播客|singer|recording|podcast/i,tone:'slate'},
  postcard:{label:'明信片',icon:'bookmark',match:/旅行|旅途|旅游|travel|trip/i,tone:'sand'}
 };
 const tones={sand:'暖砂',sage:'鼠尾草绿',rose:'陶粉',slate:'蓝灰'};
 let directoryReturn=null;

 function appearance(name,chosen=state.collectionLogos?.get(name)){
  const symbol=Object.hasOwn(symbols,chosen?.symbol)?chosen.symbol:
   Object.keys(symbols).find(key=>symbols[key].match?.test(name))||'folder';
  return {symbol,tone:Object.hasOwn(tones,chosen?.tone)?chosen.tone:symbols[symbol].tone};
 }
 function logo(name,{compact=false,chosen}={}){
  const {symbol,tone}=appearance(name,chosen);
  const art=compact?svg(symbols[symbol].icon):
   '<span class="category-figure"><i class="part-a"></i><i class="part-b"></i><i class="part-c"></i></span>';
  return `<span class="category-logo${compact?' category-logo-small':''}" data-symbol="${symbol}" data-tone="${tone}" aria-hidden="true">${art}</span>`;
 }
 function directory(){
  const names=state.collections||[];
  const cards=names.slice(0,state.limit).map(name=>
   `<button class="library-category-card" type="button" data-collection="${esc(name)}" title="${esc(name)}" data-user-content>${logo(name)}<span class="library-category-name">${esc(name)}</span></button>`).join('');
  return `<section class="library-category-shelf">
   <div class="collection-heading library-category-top">${heading('我的分类')}<button type="button" id="add-collection" class="button secondary">${svg('plus')}新建分类</button></div>
   <div class="library-toolbar">${libraryFilters()}</div>
   ${cards?`<div class="library-category-grid">${cards}</div>`:'<p class="library-category-empty">新建分类后，可以在文件菜单里将资料加入分类。</p>'}
   ${names.length>state.limit?'<button type="button" class="button secondary load-more" data-action="loadMore">显示更多</button>':''}
  </section>`;
 }
 function hasReturn(){return !state.query&&state.route==='collection'&&directoryReturn?.name===state.collection;}
 function cancelReturn(){directoryReturn=null;}
 function moveLogo(from,target,duration){
  if(!from?.width||!target||!CalmMotion.allowed())return;
  const to=target.getBoundingClientRect();
  if(!to.width||!to.height||Math.abs(from.y-to.y)>innerHeight)return;
  CalmMotion.play(target,[{transform:`translate(${from.x-to.x}px,${from.y-to.y}px) scale(${from.width/to.width},${from.height/to.height})`},{transform:'none'}],
   {duration,channel:'collection-logo'});
 }
 function open(button){
  const fromCard=state.route==='collections'&&!state.query&&button.classList.contains('library-category-card');
  const name=button.dataset.collection;
  const from=fromCard?button.querySelector('.category-logo').getBoundingClientRect():null;
  directoryReturn=fromCard?{name,limit:state.limit,scroll:window.scrollY}:null;
  state.collection=name;
  navigate('collection');
  if(fromCard&&state.route==='collection'){
   const title=$('.library-category-title h1');
   title?.setAttribute('tabindex','-1');
   title?.focus({preventScroll:true});
   moveLogo(from,$('.library-category-title .category-logo'),280);
  }
 }
 function back(){
  if(!hasReturn())return;
  const origin=directoryReturn,from=$('.library-category-title .category-logo')?.getBoundingClientRect();
  navigate('collections');
  if(state.limit!==origin.limit){state.limit=origin.limit;render();}
  window.scrollTo(0,origin.scroll);
  const card=$(`.library-category-card[data-collection="${CSS.escape(origin.name)}"]`);
  card?.focus({preventScroll:true});
  moveLogo(from,card?.querySelector('.category-logo'),220);
 }
 function editLogo(){
  if(state.isPrivate||state.route!=='collection'||state.query)return;
  const name=state.collection;
  const chosen=state.collectionLogos?.get(name)||{symbol:'auto',tone:'auto'};
  const options=(values,selected)=>[['auto','自动'],...values].map(([key,label])=>
   `<option value="${key}"${key===selected?' selected':''}>${label}</option>`).join('');
  showSmall(`<h2>设置分类图标</h2><form id="collection-logo-form">
   <div class="collection-logo-preview" aria-hidden="true">${logo(name)}</div>
   <label class="form-label">图案<select name="symbol">${options(Object.entries(symbols).map(([key,value])=>[key,value.label]),chosen.symbol)}</select></label>
   <label class="form-label">配色<select name="tone">${options(Object.entries(tones),chosen.tone)}</select></label>
   <p class="collection-logo-error" role="alert" hidden></p>
   <button class="button primary dialog-submit" type="submit">保存</button>
  </form>`);
  const dialog=$('#small-dialog'),form=$('#collection-logo-form'),submit=form.querySelector('[type=submit]'),error=form.querySelector('[role=alert]');
  const values=()=>({symbol:form.elements.symbol.value,tone:form.elements.tone.value});
  I18n.localize(dialog);
  form.elements.symbol.focus();
  form.addEventListener('change',()=>{form.querySelector('.collection-logo-preview').innerHTML=logo(name,{chosen:values()});});
  form.addEventListener('submit',async event=>{
   event.preventDefault();
   if(submit.disabled)return;
   submit.disabled=true;
   error.hidden=true;
   try{
    await native('saveCollectionLogo',{name,...values()});
    if(form.isConnected)await closeSmall();
    await refresh();
    if(state.route==='collection'&&state.collection===name)$('[data-collection-logo-edit]')?.focus({preventScroll:true});
   }catch(reason){
    error.textContent=I18n.text(reason.message||String(reason));
    error.hidden=false;
   }finally{submit.disabled=false;}
  });
 }
 document.addEventListener('click',event=>{
  const button=event.target.closest('button');
  if(button?.hasAttribute('data-category-back'))back();
  if(button?.hasAttribute('data-collection-logo-edit'))editLogo();
 });
 return {logo,directory,open,hasReturn,cancelReturn};
})();
