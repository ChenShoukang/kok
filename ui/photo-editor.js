'use strict';
const photoDefaults={brightness:0,contrast:0,saturation:0,temperature:0,grayscale:0,sepia:0,vignette:0,rotation:0,flip:false,flipY:false,ratio:'original',cropX:50,cropY:50,freeCrop:null,preset:'original'};
const photoPresets={original:{},soft:{brightness:6,contrast:-12,saturation:-8},vivid:{contrast:12,saturation:25},mono:{grayscale:100,contrast:12},vintage:{sepia:38,contrast:-8,saturation:-20,temperature:15}};
function photoSnapshot(){return JSON.parse(JSON.stringify(Object.fromEntries(Object.keys(photoDefaults).map(key=>[key,photoEditor[key]]))));}
function commitPhotoEdit(){
 const e=photoEditor;if(!e)return;const current=photoSnapshot();
 if(JSON.stringify(current)===JSON.stringify(e.history[e.historyAt]))return;
 e.history.splice(e.historyAt+1);e.history.push(current);if(e.history.length>40)e.history.shift();e.historyAt=e.history.length-1;
 syncPhotoControls();
}
function applyPhotoEdit(patch){commitPhotoEdit();Object.assign(photoEditor,patch);photoEditor.compare=false;commitPhotoEdit();syncPhotoControls();drawPhotoEdit();}
function syncPhotoControls(){
 const e=photoEditor;if(!e)return;
 document.querySelectorAll('[data-edit]').forEach(input=>{const key=input.dataset.edit;input.value=e[key];const label=$('#'+key+'-value');if(label)label.textContent=e[key];});
 if($('#crop-ratio'))$('#crop-ratio').value=e.cropMode?e.cropAspect:e.ratio;const crop=Number(e.ratio),aspect=e.image.naturalWidth/e.image.naturalHeight;$('[data-edit="cropX"]').disabled=!!e.cropMode||!!e.freeCrop||!crop||aspect<=crop;$('[data-edit="cropY"]').disabled=!!e.cropMode||!!e.freeCrop||!crop||aspect>=crop;
 document.querySelectorAll('[data-photo-preset]').forEach(button=>button.setAttribute('aria-pressed',String(button.dataset.photoPreset===e.preset)));
 $('[data-photo-undo]').disabled=!!e.cropMode||e.historyAt<=0;$('[data-photo-redo]').disabled=!!e.cropMode||e.historyAt>=e.history.length-1;
 $('[data-photo-compare]').setAttribute('aria-pressed',String(e.compare));
 $('[data-photo-compare]').disabled=!!e.cropMode;$('[data-save-photo]').disabled=!!e.cropMode;
 document.querySelectorAll('[data-edit-rotate],[data-edit-flip],[data-edit-flip-y]').forEach(button=>button.disabled=!!e.cropMode);
}
async function startPhotoEditor(...args){return Kok.invoke('startPhotoEditor',this,args);}
Kok.define('startPhotoEditor',async function(){
 if(!state.viewer||state.viewer.kind!=='photo')return;if(state.slide){clearInterval(state.slide);state.slide=null;}
 const item=state.viewer,image=new Image();image.crossOrigin='anonymous';image.src=media(item);await image.decode();if(state.viewer?.id!==item.id)return;
 cleanupWorkspace();viewer.classList.add('photo-editing');photoEditor={image,item,...photoDefaults,compare:false,history:[{...photoDefaults}],historyAt:0,frame:0,viewZoom:1,viewPanX:0,viewPanY:0,cropMode:false};
 const slider=(key,label,min,max,value=0)=>`<label class="edit-slider"><span>${label}<b id="${key}-value">${value}</b></span><input type="range" data-edit="${key}" min="${min}" max="${max}" value="${value}" aria-label="${label}"></label>`;
 $('#viewer-content').innerHTML=`<section class="photo-editor"><div class="edit-canvas-wrap"><canvas id="edit-canvas" aria-label="照片编辑预览"></canvas></div><aside class="edit-controls"><header class="edit-heading"><h2>编辑照片</h2><div>${workspaceButton('skipBack','撤销','data-photo-undo')}${workspaceButton('skipForward','重做','data-photo-redo')}</div></header><div class="edit-scroll"><div class="photo-presets">${[['original','原图'],['soft','柔和'],['vivid','鲜明'],['mono','黑白'],['vintage','复古']].map(([key,label])=>`<button class="photo-preset" data-photo-preset="${key}" aria-pressed="${key==='original'}"><span class="preset-swatch preset-${key}" aria-hidden="true"></span>${label}</button>`).join('')}</div><details class="edit-group" open><summary>光线与色彩</summary><div class="edit-group-body">${slider('brightness','亮度',-50,50)}${slider('contrast','对比度',-50,50)}${slider('saturation','饱和度',-100,100)}${slider('temperature','色温',-50,50)}${slider('grayscale','黑白程度',0,100)}${slider('sepia','复古程度',0,100)}${slider('vignette','暗角',0,100)}</div></details><details class="edit-group" open><summary>裁剪与方向</summary><div class="edit-group-body"><label class="form-label">裁剪比例<select id="crop-ratio"><option value="original">原始比例</option><option value="1">正方形 · 1:1</option><option value="1.333333">照片 · 4:3</option><option value="1.5" data-user-content>3:2</option><option value="1.777778">宽屏 · 16:9</option><option value="0.75">竖幅 · 3:4</option><option value="0.5625" data-user-content>9:16</option></select></label>${slider('cropX','裁剪水平位置',0,100,50)}${slider('cropY','裁剪垂直位置',0,100,50)}<div class="edit-buttons"><button class="button secondary" data-edit-rotate>${svg('rotate')}旋转</button><button class="button secondary" data-edit-flip>水平翻转</button><button class="button secondary" data-edit-flip-y>垂直翻转</button></div></div></details></div><footer class="edit-actions"><div><button class="text-button" data-photo-compare aria-pressed="false">对比原图</button><button class="text-button" data-photo-reset>重置调整</button></div><button class="button primary" data-save-photo>${svg('download')}另存为新照片</button><button class="text-button" data-cancel-photo>返回原片预览</button><label class="photo-output-size"><span>导出尺寸</span><select id="photo-export-size"><option value="2048">2048 px</option><option value="4096" selected>4096 px</option><option value="original">原始尺寸（最多 8192 px）</option></select></label></footer></aside></section>`;
 setupPhotoEditorInteraction();const wrap=$('.edit-canvas-wrap'),e=photoEditor;const resize=new ResizeObserver(()=>fitPhotoCanvas());resize.observe(wrap);
 workspaceDisposers.push(()=>{resize.disconnect();cancelAnimationFrame(e.frame);});drawPhotoEdit();syncPhotoControls();animateIn($('.edit-controls'));
});
function renderPhotoCanvas(canvas,full=false,compare=false){
 const e=photoEditor;if(!e)return;const image=e.image,values=compare?photoDefaults:!full&&e.cropMode?{...e,ratio:'original',freeCrop:null}:e;
 const rectangle=photoSourceCrop(values,image);let sx=rectangle.x*image.naturalWidth,sy=rectangle.y*image.naturalHeight,sw=rectangle.w*image.naturalWidth,sh=rectangle.h*image.naturalHeight;
 const scale=Math.min(1,(full?(values.exportSize==='original'?8192:Number(values.exportSize)||4096):1600)/Math.max(sw,sh));const w=Math.max(1,Math.round(sw*scale)),h=Math.max(1,Math.round(sh*scale)),swapped=values.rotation%180!==0;
 canvas.width=swapped?h:w;canvas.height=swapped?w:h;const ctx=canvas.getContext('2d');
 ctx.save();ctx.filter=`brightness(${1+values.brightness/100}) contrast(${1+values.contrast/100}) saturate(${1+values.saturation/100}) grayscale(${values.grayscale/100}) sepia(${values.sepia/100})`;
 ctx.translate(canvas.width/2,canvas.height/2);ctx.rotate(values.rotation*Math.PI/180);ctx.scale(values.flip?-1:1,values.flipY?-1:1);ctx.drawImage(image,sx,sy,sw,sh,-w/2,-h/2,w,h);ctx.restore();
 if(values.temperature){ctx.globalCompositeOperation='source-atop';ctx.fillStyle=values.temperature>0?`rgba(255,150,40,${values.temperature/500})`:`rgba(50,125,255,${-values.temperature/500})`;ctx.fillRect(0,0,canvas.width,canvas.height);ctx.globalCompositeOperation='source-over';}
 if(values.vignette){const gradient=ctx.createRadialGradient(canvas.width/2,canvas.height/2,Math.min(canvas.width,canvas.height)*.2,canvas.width/2,canvas.height/2,Math.hypot(canvas.width,canvas.height)*.5);gradient.addColorStop(0,'#0000');gradient.addColorStop(1,`rgba(0,0,0,${values.vignette/130})`);ctx.globalCompositeOperation='source-atop';ctx.fillStyle=gradient;ctx.fillRect(0,0,canvas.width,canvas.height);ctx.globalCompositeOperation='source-over';}
 if(!compare&&typeof drawCreativeObjects==='function')drawCreativeObjects(ctx,canvas.width,canvas.height,values.objects,!full?e.overlaySelected:null);
}
function fitPhotoCanvas(){
 fitPhotoEditorViewport();
}
function drawPhotoEdit(...args){return Kok.invoke('drawPhotoEdit',this,args);}
Kok.define('drawPhotoEdit',function(){const canvas=$('#edit-canvas');if(!photoEditor||!canvas)return;renderPhotoCanvas(canvas,false,photoEditor.compare);fitPhotoCanvas();syncFreeCropOverlay();});
document.addEventListener('input',event=>{
 const input=event.target;if(!photoEditor||!input.dataset.edit)return;
 photoEditor[input.dataset.edit]=Number(input.value);photoEditor.preset='custom';photoEditor.compare=false;const value=$('#'+input.dataset.edit+'-value');if(value)value.textContent=input.value;
 cancelAnimationFrame(photoEditor.frame);photoEditor.frame=requestAnimationFrame(()=>{drawPhotoEdit();syncPhotoControls();});
});
document.addEventListener('change',event=>{
 if(!photoEditor)return;if(event.target.id==='crop-ratio'){if(event.target.value==='free'){startFreeCrop();return;}if(photoEditor.cropMode){setFreeCropAspect(event.target.value);return;}photoEditor.ratio=event.target.value;photoEditor.freeCrop=null;photoEditor.compare=false;drawPhotoEdit();syncPhotoControls();}
 if(event.target.dataset.edit||event.target.id==='crop-ratio')commitPhotoEdit();
});
document.addEventListener('click',async event=>{
 const button=event.target.closest('button');if(!button||!photoEditor)return;
 try{
  if(button.dataset.photoPreset){const geometric=Object.fromEntries(['rotation','flip','flipY','ratio','cropX','cropY','freeCrop','objects','exportSize'].map(key=>[key,photoEditor[key]]));applyPhotoEdit({...photoDefaults,...geometric,...photoPresets[button.dataset.photoPreset],preset:button.dataset.photoPreset});}
  if(button.hasAttribute('data-edit-rotate'))applyPhotoEdit({rotation:(photoEditor.rotation+90)%360});
  if(button.hasAttribute('data-edit-flip'))applyPhotoEdit({flip:!photoEditor.flip});
  if(button.hasAttribute('data-edit-flip-y'))applyPhotoEdit({flipY:!photoEditor.flipY});
  if(button.hasAttribute('data-photo-reset')){cancelFreeCrop(false);photoEditor.viewZoom=1;photoEditor.viewPanX=photoEditor.viewPanY=0;applyPhotoEdit({...photoDefaults});}
  if(button.hasAttribute('data-photo-undo')||button.hasAttribute('data-photo-redo')){commitPhotoEdit();const e=photoEditor;e.historyAt=Math.max(0,Math.min(e.history.length-1,e.historyAt+(button.hasAttribute('data-photo-undo')?-1:1)));Object.assign(e,e.history[e.historyAt]);e.compare=false;syncPhotoControls();drawPhotoEdit();}
  if(button.hasAttribute('data-photo-compare')){photoEditor.compare=!photoEditor.compare;syncPhotoControls();drawPhotoEdit();}
  if(button.hasAttribute('data-cancel-photo')){photoEditor=null;renderPhoto();}
  if(button.hasAttribute('data-save-photo')){const output=document.createElement('canvas');renderPhotoCanvas(output,true);const item=photoEditor.item;const result=await native('savePhoto',{id:item.id,name:item.name,image:output.toDataURL('image/png')});if(result){await refresh();toast('编辑后的照片已另存并加入资料库。');}}
 }catch(error){fail(error);}
});
document.addEventListener('keydown',event=>{
 if(!photoEditor||$('#small-dialog').open||event.target.closest('textarea,input:not([type=range]),select,[contenteditable]')||!(event.ctrlKey||event.metaKey))return;
 if(event.code==='KeyZ'||event.code==='KeyY'){event.preventDefault();$(event.code==='KeyY'||event.shiftKey?'[data-photo-redo]':'[data-photo-undo]')?.click();}
});
