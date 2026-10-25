'use strict';
paths.crop='M6 3v15h15 M3 6h15v15';
const photoClamp=(n,min,max)=>Math.max(min,Math.min(max,n));
function photoWheelFactor(event){const pixels=event.deltaY*(event.deltaMode===1?16:event.deltaMode===2?240:1);return Math.exp(-photoClamp(pixels,-400,400)*.0018);}
function zoomPhoto(next,point){
 const wrap=$('.photo-image-wrap');if(!wrap||!$('#photo-image')?.naturalWidth)return;
 const previous=Number(state.zoom)||1,zoom=photoClamp(next,.25,8),box=wrap.getBoundingClientRect();
 const x=(point?.x??box.left+box.width/2)-box.left-box.width/2,y=(point?.y??box.top+box.height/2)-box.top-box.height/2;
 state.photoPanX=x-(x-(state.photoPanX||0))*zoom/previous;state.photoPanY=y-(y-(state.photoPanY||0))*zoom/previous;state.zoom=zoom;transformPhoto();
}
function setupPhotoInteraction(){
 const wrap=$('.photo-image-wrap'),image=$('#photo-image');if(!wrap||!image)return;image.draggable=false;
 wrap.title=I18n.text('滚轮缩放，放大后拖动查看；双击切换适应窗口。');
 let drag=null;
 const wheel=event=>{if(!event.deltaY)return;event.preventDefault();zoomPhoto((Number(state.zoom)||1)*photoWheelFactor(event),{x:event.clientX,y:event.clientY});};
 const down=event=>{if(event.button!==0||state.zoom<=1)return;event.preventDefault();drag={id:event.pointerId,x:event.clientX,y:event.clientY,panX:state.photoPanX||0,panY:state.photoPanY||0};wrap.setPointerCapture(event.pointerId);wrap.classList.add('photo-panning');};
 const move=event=>{if(!drag||event.pointerId!==drag.id)return;state.photoPanX=drag.panX+event.clientX-drag.x;state.photoPanY=drag.panY+event.clientY-drag.y;transformPhoto();};
 const end=event=>{if(drag&&event.pointerId===drag.id){drag=null;wrap.classList.remove('photo-panning');if(wrap.hasPointerCapture(event.pointerId))wrap.releasePointerCapture(event.pointerId);}};
 const twice=event=>{event.preventDefault();if(state.zoom>1.01){state.photoPanX=state.photoPanY=0;zoomPhoto(1);}else zoomPhoto(2,{x:event.clientX,y:event.clientY});};
 wrap.addEventListener('wheel',wheel,{passive:false});wrap.addEventListener('pointerdown',down);wrap.addEventListener('pointermove',move);wrap.addEventListener('pointerup',end);wrap.addEventListener('pointercancel',end);wrap.addEventListener('lostpointercapture',end);wrap.addEventListener('dblclick',twice);
 workspaceDisposers.push(()=>{wrap.removeEventListener('wheel',wheel);wrap.removeEventListener('pointerdown',down);wrap.removeEventListener('pointermove',move);wrap.removeEventListener('pointerup',end);wrap.removeEventListener('pointercancel',end);wrap.removeEventListener('lostpointercapture',end);wrap.removeEventListener('dblclick',twice);});
}
function photoSourceCrop(values,image){
 if(values.freeCrop){const r=values.freeCrop,minW=1/image.naturalWidth,minH=1/image.naturalHeight,x=photoClamp(r.x,0,1-minW),y=photoClamp(r.y,0,1-minH);return{x,y,w:photoClamp(r.w,minW,1-x),h:photoClamp(r.h,minH,1-y)};}
 const ratio=Number(values.ratio),aspect=image.naturalWidth/image.naturalHeight;let x=0,y=0,w=1,h=1;
 if(ratio){if(aspect>ratio){w=ratio/aspect;x=(1-w)*values.cropX/100;}else{h=aspect/ratio;y=(1-h)*values.cropY/100;}}return{x,y,w,h};
}
function photoOrientationPoint(point,inverse=false){
 const e=photoEditor,rotation=((e.rotation%360)+360)%360;let{x,y}=point;
 if(inverse){if(rotation===90)[x,y]=[y,1-x];else if(rotation===180)[x,y]=[1-x,1-y];else if(rotation===270)[x,y]=[1-y,x];if(e.flip)x=1-x;if(e.flipY)y=1-y;}
 else{if(e.flip)x=1-x;if(e.flipY)y=1-y;if(rotation===90)[x,y]=[1-y,x];else if(rotation===180)[x,y]=[1-x,1-y];else if(rotation===270)[x,y]=[y,1-x];}return{x,y};
}
function photoOrientationRect(rect,inverse=false){const points=[{x:rect.x,y:rect.y},{x:rect.x+rect.w,y:rect.y},{x:rect.x,y:rect.y+rect.h},{x:rect.x+rect.w,y:rect.y+rect.h}].map(p=>photoOrientationPoint(p,inverse));const x=photoClamp(Math.min(...points.map(p=>p.x)),0,1),y=photoClamp(Math.min(...points.map(p=>p.y)),0,1);return{x,y,w:Math.min(1-x,Math.max(...points.map(p=>p.x))-x),h:Math.min(1-y,Math.max(...points.map(p=>p.y))-y)};}
function fitPhotoEditorViewport(){
 const e=photoEditor,canvas=$('#edit-canvas'),wrap=$('.edit-canvas-wrap'),stage=$('#photo-edit-stage');if(!e||!canvas||!wrap||!stage)return;
 const fit=Math.max(.001,Math.min(Math.max(10,wrap.clientWidth-24)/canvas.width,Math.max(10,wrap.clientHeight-(e.cropMode?104:80))/canvas.height)),zoom=e.viewZoom||1,width=canvas.width*fit*zoom,height=canvas.height*fit*zoom;
 const maxX=Math.max(0,(width-wrap.clientWidth)/2),maxY=Math.max(0,(height-wrap.clientHeight)/2);e.viewPanX=photoClamp(e.viewPanX||0,-maxX,maxX);e.viewPanY=photoClamp(e.viewPanY||0,-maxY,maxY);
 stage.style.width=width+'px';stage.style.height=height+'px';stage.style.transform=`translate(${e.viewPanX}px,${e.viewPanY}px)`;canvas.style.width='100%';canvas.style.height='100%';
 wrap.classList.toggle('editor-zoomed',zoom>1.001);const label=$('#photo-edit-zoom');if(label)label.textContent=Math.round(zoom*100)+'%';
}
function zoomPhotoEditor(next,point){
 const e=photoEditor,wrap=$('.edit-canvas-wrap');if(!e||!wrap||e.cropDragging)return;
 const previous=e.viewZoom||1,zoom=photoClamp(next,.25,6),box=wrap.getBoundingClientRect(),x=(point?.x??box.left+box.width/2)-box.left-box.width/2,y=(point?.y??box.top+box.height/2)-box.top-box.height/2;
 e.viewPanX=x-(x-(e.viewPanX||0))*zoom/previous;e.viewPanY=y-(y-(e.viewPanY||0))*zoom/previous;e.viewZoom=zoom;fitPhotoEditorViewport();
}
function setupPhotoEditorInteraction(){
 const canvas=$('#edit-canvas'),wrap=$('.edit-canvas-wrap'),e=photoEditor;if(!canvas||!wrap||!e)return;
 const stage=document.createElement('div');stage.id='photo-edit-stage';stage.className='photo-edit-stage';canvas.replaceWith(stage);stage.append(canvas);
 wrap.insertAdjacentHTML('beforeend',`<div class="photo-view-controls">${workspaceButton('zoomOut','缩小','data-edit-view="out"')}<span id="photo-edit-zoom" data-user-content>100%</span>${workspaceButton('zoomIn','放大','data-edit-view="in"')}<button class="text-button" data-edit-view="fit">适应窗口</button></div><div class="photo-crop-actions" id="photo-crop-actions" hidden><span id="photo-crop-size" data-user-content></span><button class="button secondary" data-free-crop-cancel>取消裁剪</button><button class="button primary" data-free-crop-apply>应用裁剪</button></div>`);
 $('#crop-ratio').insertAdjacentHTML('beforeend','<option value="free">自由裁剪</option>');$('#crop-ratio').closest('.form-label').insertAdjacentHTML('beforebegin',`<button class="button secondary free-crop-button" data-free-crop-start>${svg('crop')}自由裁剪</button><p class="free-crop-help">拖动画选区，可移动边框或拖动八个控制点；滚轮可放大查看。</p>`);
 let pan=null;
 const wheel=event=>{if(!event.deltaY)return;event.preventDefault();zoomPhotoEditor((e.viewZoom||1)*photoWheelFactor(event),{x:event.clientX,y:event.clientY});};
 const down=event=>{if(photoEditor!==e||e.cropMode||e.viewZoom<=1||event.button!==0||event.target.closest('button'))return;event.preventDefault();pan={id:event.pointerId,x:event.clientX,y:event.clientY,px:e.viewPanX||0,py:e.viewPanY||0};wrap.setPointerCapture(event.pointerId);wrap.classList.add('photo-editor-panning');};
 const move=event=>{if(!pan||pan.id!==event.pointerId)return;e.viewPanX=pan.px+event.clientX-pan.x;e.viewPanY=pan.py+event.clientY-pan.y;fitPhotoEditorViewport();};
 const end=event=>{if(pan&&pan.id===event.pointerId){pan=null;wrap.classList.remove('photo-editor-panning');if(wrap.hasPointerCapture(event.pointerId))wrap.releasePointerCapture(event.pointerId);}};
 wrap.addEventListener('wheel',wheel,{passive:false});wrap.addEventListener('pointerdown',down);wrap.addEventListener('pointermove',move);wrap.addEventListener('pointerup',end);wrap.addEventListener('pointercancel',end);wrap.addEventListener('lostpointercapture',end);
 setupFreeCropPointers(stage,e);I18n.localize(wrap);I18n.localize($('.edit-controls'));
 workspaceDisposers.push(()=>{wrap.removeEventListener('wheel',wheel);wrap.removeEventListener('pointerdown',down);wrap.removeEventListener('pointermove',move);wrap.removeEventListener('pointerup',end);wrap.removeEventListener('pointercancel',end);wrap.removeEventListener('lostpointercapture',end);});
}
function cropMinimum(){const e=photoEditor,swapped=e.rotation%180!==0;return{x:Math.min(.08,Math.max(.002,4/(swapped?e.image.naturalHeight:e.image.naturalWidth))),y:Math.min(.08,Math.max(.002,4/(swapped?e.image.naturalWidth:e.image.naturalHeight)))};}
function cropAspectNormalized(){const e=photoEditor;if(e.cropAspect==='original')return 1;const ratio=Number(e.cropAspect);if(!ratio)return 0;const swapped=e.rotation%180!==0;return ratio/((swapped?e.image.naturalHeight:e.image.naturalWidth)/(swapped?e.image.naturalWidth:e.image.naturalHeight));}
function startFreeCrop(){
 const e=photoEditor;if(!e)return;commitPhotoEdit();e.cropMode=true;e.cropAspect='free';e.compare=false;e.viewZoom=1;e.viewPanX=e.viewPanY=0;
 const source=photoSourceCrop(e,e.image);e.cropDraft=source.w<.999||source.h<.999?photoOrientationRect(source):{x:.1,y:.1,w:.8,h:.8};drawPhotoEdit();syncPhotoControls();
}
function cancelFreeCrop(redraw=true){const e=photoEditor;if(!e)return;e.cropMode=false;e.cropDraft=null;e.cropDragging=false;e.viewZoom=1;e.viewPanX=e.viewPanY=0;if(redraw){drawPhotoEdit();syncPhotoControls();}else syncFreeCropOverlay();}
function applyFreeCrop(){const e=photoEditor;if(!e?.cropMode||!e.cropDraft)return;const crop=photoOrientationRect(e.cropDraft,true);e.cropMode=false;e.cropDraft=null;e.cropDragging=false;e.viewZoom=1;e.viewPanX=e.viewPanY=0;applyPhotoEdit({freeCrop:crop,ratio:'free',cropX:50,cropY:50});}
function setFreeCropAspect(aspect){const e=photoEditor;if(!e?.cropMode)return;e.cropAspect=aspect;const ratio=cropAspectNormalized();if(ratio){const r=e.cropDraft,cx=r.x+r.w/2,cy=r.y+r.h/2;let w=r.w,h=w/ratio;const scale=Math.min(1,1/w,1/h);w*=scale;h*=scale;e.cropDraft={x:photoClamp(cx-w/2,0,1-w),y:photoClamp(cy-h/2,0,1-h),w,h};}syncPhotoControls();syncFreeCropOverlay();}
function syncFreeCropOverlay(){
 const e=photoEditor,stage=$('#photo-edit-stage'),actions=$('#photo-crop-actions');if(!e||!stage||!actions)return;actions.hidden=!e.cropMode;stage.classList.toggle('free-cropping',!!e.cropMode);
 let box=$('#photo-crop-box');if(!e.cropMode){box?.remove();return;}
 if(!box){stage.insertAdjacentHTML('beforeend',`<div class="photo-crop-box" id="photo-crop-box" tabindex="0" role="group" aria-label="裁剪选区"><div class="photo-crop-grid" aria-hidden="true"></div>${[['nw','左上'],['n','上边'],['ne','右上'],['e','右边'],['se','右下'],['s','下边'],['sw','左下'],['w','左边']].map(([dir,label])=>`<button type="button" class="crop-handle handle-${dir}" data-crop-handle="${dir}" aria-label="${label}裁剪控制点"></button>`).join('')}</div>`);box=$('#photo-crop-box');I18n.localize(box);}
 const r=e.cropDraft;Object.assign(box.style,{left:r.x*100+'%',top:r.y*100+'%',width:r.w*100+'%',height:r.h*100+'%'});
 const source=photoOrientationRect(r,true),scale=Math.min(1,4096/Math.max(source.w*e.image.naturalWidth,source.h*e.image.naturalHeight));let w=Math.max(1,Math.round(source.w*e.image.naturalWidth*scale)),h=Math.max(1,Math.round(source.h*e.image.naturalHeight*scale));if(e.rotation%180!==0)[w,h]=[h,w];$('#photo-crop-size').textContent=`${w} × ${h}`;
}
function boundedCropFromAnchor(anchor,point,ratio){
 const min=cropMinimum(),sx=point.x<anchor.x?-1:1,sy=point.y<anchor.y?-1:1,maxW=sx<0?anchor.x:1-anchor.x,maxH=sy<0?anchor.y:1-anchor.y;
 let w=Math.max(min.x,Math.abs(point.x-anchor.x)),h=Math.max(min.y,Math.abs(point.y-anchor.y));if(ratio){if(w/h>ratio)h=w/ratio;else w=h*ratio;}const scale=Math.min(1,maxW/Math.max(.000001,w),maxH/Math.max(.000001,h));w*=scale;h*=scale;return{x:sx<0?anchor.x-w:anchor.x,y:sy<0?anchor.y-h:anchor.y,w,h};
}
function resizeFreeCrop(base,handle,dx,dy){
 const min=cropMinimum(),ratio=cropAspectNormalized();let left=base.x,right=base.x+base.w,top=base.y,bottom=base.y+base.h;
 if(handle.includes('w'))left=photoClamp(left+dx,0,right-min.x);if(handle.includes('e'))right=photoClamp(right+dx,left+min.x,1);if(handle.includes('n'))top=photoClamp(top+dy,0,bottom-min.y);if(handle.includes('s'))bottom=photoClamp(bottom+dy,top+min.y,1);
 if(ratio&&handle.length===2){const anchor={x:handle.includes('w')?base.x+base.w:base.x,y:handle.includes('n')?base.y+base.h:base.y};return boundedCropFromAnchor(anchor,{x:handle.includes('w')?left:right,y:handle.includes('n')?top:bottom},ratio);}
 if(ratio){let w=right-left,h=bottom-top;const cx=base.x+base.w/2,cy=base.y+base.h/2;if(handle==='e'||handle==='w'){h=w/ratio;const scale=Math.min(1,2*Math.min(cy,1-cy)/h);w*=scale;h*=scale;left=handle==='w'?base.x+base.w-w:base.x;top=cy-h/2;}else{w=h*ratio;const scale=Math.min(1,2*Math.min(cx,1-cx)/w);w*=scale;h*=scale;left=cx-w/2;top=handle==='n'?base.y+base.h-h:base.y;}return{x:left,y:top,w,h};}
 return{x:left,y:top,w:right-left,h:bottom-top};
}
function setupFreeCropPointers(stage,e){
 let drag=null;
 const point=event=>{const box=stage.getBoundingClientRect();return{x:photoClamp((event.clientX-box.left)/Math.max(1,box.width),0,1),y:photoClamp((event.clientY-box.top)/Math.max(1,box.height),0,1)};};
 const down=event=>{if(photoEditor!==e||!e.cropMode||event.button!==0)return;event.preventDefault();event.stopPropagation();const at=point(event),handle=event.target.closest('[data-crop-handle]')?.dataset.cropHandle;drag={id:event.pointerId,start:at,base:{...e.cropDraft},mode:handle?'resize':event.target.closest('#photo-crop-box')?'move':'new',handle};e.cropDragging=true;stage.classList.add('crop-dragging');stage.setPointerCapture(event.pointerId);if(drag.mode==='new')e.cropDraft=boundedCropFromAnchor(at,{x:Math.min(1,at.x+.002),y:Math.min(1,at.y+.002)},cropAspectNormalized());syncFreeCropOverlay();};
 const move=event=>{if(!drag||drag.id!==event.pointerId||photoEditor!==e)return;const at=point(event),dx=at.x-drag.start.x,dy=at.y-drag.start.y;if(drag.mode==='new')e.cropDraft=boundedCropFromAnchor(drag.start,at,cropAspectNormalized());else if(drag.mode==='move')e.cropDraft={...drag.base,x:photoClamp(drag.base.x+dx,0,1-drag.base.w),y:photoClamp(drag.base.y+dy,0,1-drag.base.h)};else e.cropDraft=resizeFreeCrop(drag.base,drag.handle,dx,dy);syncFreeCropOverlay();};
 const end=event=>{if(!drag||drag.id!==event.pointerId)return;const min=cropMinimum();if(e.cropDraft&&(e.cropDraft.w<min.x||e.cropDraft.h<min.y)){e.cropDraft={...drag.base};syncFreeCropOverlay();}drag=null;e.cropDragging=false;stage.classList.remove('crop-dragging');if(stage.hasPointerCapture(event.pointerId))stage.releasePointerCapture(event.pointerId);};
 stage.addEventListener('pointerdown',down);stage.addEventListener('pointermove',move);stage.addEventListener('pointerup',end);stage.addEventListener('pointercancel',end);stage.addEventListener('lostpointercapture',end);
 workspaceDisposers.push(()=>{stage.removeEventListener('pointerdown',down);stage.removeEventListener('pointermove',move);stage.removeEventListener('pointerup',end);stage.removeEventListener('pointercancel',end);stage.removeEventListener('lostpointercapture',end);});
}
document.addEventListener('click',event=>{
 const button=event.target.closest('button');if(!button||!photoEditor)return;
 if(button.hasAttribute('data-free-crop-start'))startFreeCrop();if(button.hasAttribute('data-free-crop-cancel'))cancelFreeCrop();if(button.hasAttribute('data-free-crop-apply'))applyFreeCrop();
 if(button.dataset.editView){if(button.dataset.editView==='fit'){photoEditor.viewPanX=photoEditor.viewPanY=0;zoomPhotoEditor(1);}else zoomPhotoEditor((photoEditor.viewZoom||1)*(button.dataset.editView==='in'?1.2:1/1.2));}
});
document.addEventListener('keydown',event=>{
 if(!viewer.open||state.viewer?.kind!=='photo'||$('#small-dialog').open||event.target.closest('input,textarea,select,[contenteditable]'))return;
 if(photoEditor){
   const e=photoEditor;if(e.cropMode&&event.key==='Escape'){event.preventDefault();event.stopImmediatePropagation();cancelFreeCrop();return;}
   if(e.cropMode&&event.key==='Enter'){event.preventDefault();event.stopImmediatePropagation();applyFreeCrop();return;}
   if(event.key.startsWith('Arrow')){event.preventDefault();event.stopImmediatePropagation();const dx=(event.key==='ArrowRight'?1:event.key==='ArrowLeft'?-1:0)*(event.shiftKey?.02:.002),dy=(event.key==='ArrowDown'?1:event.key==='ArrowUp'?-1:0)*(event.shiftKey?.02:.002);if(e.cropMode){const handle=event.target.closest('[data-crop-handle]')?.dataset.cropHandle;e.cropDraft=handle?resizeFreeCrop(e.cropDraft,handle,dx,dy):{...e.cropDraft,x:photoClamp(e.cropDraft.x+dx,0,1-e.cropDraft.w),y:photoClamp(e.cropDraft.y+dy,0,1-e.cropDraft.h)};syncFreeCropOverlay();}else if(e.viewZoom>1){e.viewPanX+=dx*10000;e.viewPanY+=dy*10000;fitPhotoEditorViewport();}return;}
 }
 if(['Equal','NumpadAdd','Minus','NumpadSubtract','Digit0','Numpad0'].includes(event.code)){event.preventDefault();event.stopImmediatePropagation();const reset=event.code.endsWith('0'),factor=['Equal','NumpadAdd'].includes(event.code)?1.2:1/1.2;if(photoEditor){if(reset)photoEditor.viewPanX=photoEditor.viewPanY=0;zoomPhotoEditor(reset?1:photoEditor.viewZoom*factor);}else{if(reset)state.photoPanX=state.photoPanY=0;zoomPhoto(reset?1:state.zoom*factor);}}
},true);
viewer.addEventListener('cancel',event=>{if(photoEditor?.cropMode){event.preventDefault();event.stopImmediatePropagation();cancelFreeCrop();}},{capture:true});
