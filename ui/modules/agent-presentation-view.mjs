// Read-only native-deck preview. It uses the same editable shape coordinates as
// the built-in presentation viewer, with no generated HTML or remote resources.
const color=(value,fallback='FFFFFF')=>/^[a-f0-9]{6}$/i.test(value||'')?value:fallback;
const number=(value,fallback=0)=>Number.isFinite(Number(value))?Number(value):fallback;
export function presentationOutline(deck){return (deck?.slides||[]).map((slide,index)=>`${index+1}. ${slide.title||''}\n${slide.shapes.filter(shape=>shape.kind!=='image').map(shape=>shape.text||'').filter(Boolean).join('\n')}${slide.notes?'\n\n'+slide.notes:''}`).join('\n\n');}
export function presentationArtifactView(deck,page=0){
 if(!deck?.slides?.length)return '';
 page=Math.max(0,Math.min(deck.slides.length-1,page));const slide=deck.slides[page],width=number(deck.width,13.333333),height=number(deck.height,7.5);
 const shapes=slide.shapes.map(shape=>{
  const image=shape.kind==='image'&&/^data:image\/(?:png|jpeg|gif|bmp);base64,[a-z0-9+/=]+$/i.test(shape.image||'');
  const align=['left','center','right'].includes(shape.align)?shape.align:'left';
  const valign=shape.valign==='middle'?'center':shape.valign==='bottom'?'flex-end':'flex-start';
  const justify=align==='center'?'center':align==='right'?'flex-end':'flex-start';
  const px=number(shape.padX,0)/width*100,py=number(shape.padY,0)/height*100;
  const radius=shape.kind==='ellipse'?'50%':shape.kind==='roundRect'?'1.1cqw':'0';
  return `<div class="agent-deck-shape" style="box-sizing:border-box;display:flex;align-items:${valign};justify-content:${justify};padding:${py}% ${px}%;left:${number(shape.x)/width*100}%;top:${number(shape.y)/height*100}%;width:${number(shape.w,1)/width*100}%;height:${number(shape.h,1)/height*100}%;font-size:${number(shape.fontSize,24)/width/72*100}cqw;line-height:1.28;font-family:${/^[\w\u3400-\u9fff -]{1,80}$/.test(shape.fontFamily||'')?shape.fontFamily:'Microsoft YaHei'};font-weight:${shape.bold?700:400};font-style:${shape.italic?'italic':'normal'};color:#${color(shape.color,'222222')};background:${shape.fill?'#'+color(shape.fill):'transparent'};text-align:${align};transform:rotate(${number(shape.rotation)}deg);border:${shape.line?'1px solid #'+color(shape.line):'0'};border-radius:${radius}">${image?`<img src="${esc(shape.image)}" alt="" draggable="false">`:`<span style="width:100%">${esc(shape.text||'')}</span>`}</div>`;
 }).join('');
 const miniThumb=row=>{
  const bg=color(row.background,'FFFFFF');
  const accent=row.shapes?.find(s=>s.fill&&s.fill!==bg)?.fill||'888888';
  return `<i class="agent-deck-mini" aria-hidden="true" style="background:#${bg}"><em style="background:#${color(accent,'888888')}"></em></i>`;
 };
 return `<section class="agent-deck-preview" aria-label="${I18n.text('幻灯片预览')}"><div class="agent-deck-navigation"><button type="button" class="icon-button" data-pi-deck-page="${page-1}" ${page===0?'disabled':''} aria-label="${I18n.text('上一页')}">${svg('skipBack')}</button><span data-user-content>${page+1} / ${deck.slides.length}</span><button type="button" class="icon-button" data-pi-deck-page="${page+1}" ${page===deck.slides.length-1?'disabled':''} aria-label="${I18n.text('下一页')}">${svg('skipForward')}</button></div><div class="agent-deck-slide" style="aspect-ratio:${width}/${height};background:#${color(slide.background)}" data-user-content>${shapes}</div><nav class="agent-deck-pages" aria-label="${I18n.text('幻灯片目录')}">${deck.slides.map((row,index)=>`<button type="button" data-pi-deck-page="${index}" aria-current="${page===index}" title="${esc(row.title||'')}" data-user-content><span>${String(index+1).padStart(2,'0')}</span>${miniThumb(row)}<b>${esc(row.title||I18n.text('幻灯片'))}</b></button>`).join('')}</nav>${slide.notes?`<details class="agent-deck-notes"><summary>${I18n.text('演讲备注')}</summary><p data-user-content>${esc(slide.notes)}</p></details>`:''}${deck.unsupported?`<small class="agent-deck-limit">${I18n.text('部分复杂元素未还原')}</small>`:''}</section>`;
}
