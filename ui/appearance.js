'use strict';
const appearanceDefaults={customColorsEnabled:false,customLightBg:'#ffffff',customLightGlass:'#fafafa',customDarkBg:'#212121',customDarkGlass:'#2b2b2b',glassOpacity:78,glassBlur:28};
const appearanceVariables=['--kok-bg-ink','--bg','--surface','--ink','--muted','--line','--green','--green-dark','--green-soft','--glass','--glass-line','--ambient','--kok-panel','--kok-panel-ink','--kok-panel-muted','--kok-hover','--kok-border','--kok-blur','--kok-solid','--kok-accent-ink'];
const colorHex=value=>/^#[\da-f]{6}$/i.test(value||'')?value.toLowerCase():null;
function colorChannels(hex){return [1,3,5].map(at=>parseInt(hex.slice(at,at+2),16));}
function blendColor(foreground,background,alpha){const a=colorChannels(foreground),b=colorChannels(background);return '#'+a.map((n,i)=>Math.round(n*alpha+b[i]*(1-alpha)).toString(16).padStart(2,'0')).join('');}
function colorIsDark(hex){const rgb=colorChannels(hex).map(v=>{v/=255;return v<=.04045?v/12.92:((v+.055)/1.055)**2.4;});return .2126*rgb[0]+.7152*rgb[1]+.0722*rgb[2]<.18;}
function applyCustomColors(){
 const body=document.body;for(const name of appearanceVariables)body.style.removeProperty(name);
 if(!state.settings.customColorsEnabled){delete body.dataset.customColors;return;}
 const dark=body.dataset.theme==='dark',prefix=dark?'customDark':'customLight';
 const background=colorHex(state.settings[prefix+'Bg'])||appearanceDefaults[prefix+'Bg'];
 const tint=colorHex(state.settings[prefix+'Glass'])||appearanceDefaults[prefix+'Glass'];
 const alpha=Math.max(.15,Math.min(.95,Number(state.settings.glassOpacity??78)/100));
 const solid=blendColor(tint,background,alpha),ink=colorIsDark(background)?'#f1f1f1':'#202124',panelInk=colorIsDark(solid)?'#f5f5f5':'#202124';
 const muted=blendColor(ink,background,.62),panelMuted=blendColor(panelInk,solid,.66),accent=panelInk;
 const values={'--kok-bg-ink':ink,'--bg':background,'--surface':solid,'--ink':ink,'--muted':muted,'--line':blendColor(ink,background,.14),'--green':accent,'--green-dark':blendColor(accent,solid,.85),'--green-soft':blendColor(accent,solid,.08),'--glass':`rgba(${colorChannels(tint).join(',')},${alpha})`,'--glass-line':blendColor(panelInk,solid,.12),'--ambient':'none','--kok-panel':`rgba(${colorChannels(tint).join(',')},${alpha})`,'--kok-panel-ink':panelInk,'--kok-panel-muted':panelMuted,'--kok-hover':blendColor(panelInk,solid,.08),'--kok-border':blendColor(panelInk,solid,.15),'--kok-blur':Math.max(0,Math.min(48,Number(state.settings.glassBlur??28)))+'px','--kok-solid':solid,'--kok-accent-ink':colorIsDark(accent)?'#ffffff':'#191919'};
 for(const [key,value]of Object.entries(values))body.style.setProperty(key,value);body.dataset.customColors='on';
}
const applyThemeBeforeCustomColors=applyTheme;
applyTheme=function(){applyThemeBeforeCustomColors();applyCustomColors();};

function appearanceSettingsRow(){
 const dark=document.body.dataset.theme==='dark',prefix=dark?'customDark':'customLight';
 return `<div class="setting-row appearance-setting"><div>自定义配色<small>背景、玻璃颜色与透明度</small></div><button class="button secondary" data-open-appearance><span class="palette-mini" style="--swatch-one:${colorHex(state.settings[prefix+'Bg'])||appearanceDefaults[prefix+'Bg']};--swatch-two:${colorHex(state.settings[prefix+'Glass'])||appearanceDefaults[prefix+'Glass']}"></span>调整配色</button></div>`;
}
function showAppearance(){
 const dark=document.body.dataset.theme==='dark',prefix=dark?'customDark':'customLight';
 const presets=dark?[['石墨','#212121','#303030'],['雾紫','#211c2b','#514163'],['深海','#14242b','#2d4b57'],['暖灰','#292521','#554c43']]:[['清透白','#f7f7f8','#ffffff'],['雾紫','#f1edf7','#e6dcf6'],['浅青','#edf5f3','#d7ece7'],['暖灰','#f5f1eb','#ebe1d4']];
 showSmall(`<div class="dialog-topline"><div><small class="eyebrow">${dark?'深色配色':'浅色配色'}</small><h2>让空间成为你的颜色</h2></div></div><p class="dialog-description">轻一点的背景，柔和一点的玻璃。选择会立即预览，并保存在本机。</p><label class="appearance-enable"><span>启用自定义配色</span><input type="checkbox" id="custom-colors-enabled" ${state.settings.customColorsEnabled?'checked':''}></label><div class="appearance-presets">${presets.map(([name,bg,glass],n)=>`<button data-color-preset="${n}" style="--preset-bg:${bg};--preset-glass:${glass}"><i></i><span>${name}</span></button>`).join('')}</div><div class="appearance-colors"><label><span>背景颜色</span><input type="color" data-color-setting="${prefix}Bg" value="${colorHex(state.settings[prefix+'Bg'])||appearanceDefaults[prefix+'Bg']}" aria-label="背景颜色"><code data-color-code="${prefix}Bg" data-user-content>${colorHex(state.settings[prefix+'Bg'])||appearanceDefaults[prefix+'Bg']}</code></label><label><span>玻璃颜色</span><input type="color" data-color-setting="${prefix}Glass" value="${colorHex(state.settings[prefix+'Glass'])||appearanceDefaults[prefix+'Glass']}" aria-label="玻璃颜色"><code data-color-code="${prefix}Glass" data-user-content>${colorHex(state.settings[prefix+'Glass'])||appearanceDefaults[prefix+'Glass']}</code></label></div><label class="appearance-range"><span>玻璃不透明度<output id="glass-opacity-value" data-user-content>${state.settings.glassOpacity??78}%</output></span><input type="range" min="15" max="95" value="${state.settings.glassOpacity??78}" data-color-setting="glassOpacity" aria-label="玻璃不透明度"></label><label class="appearance-range"><span>模糊程度<output id="glass-blur-value" data-user-content>${state.settings.glassBlur??28} px</output></span><input type="range" min="0" max="48" value="${state.settings.glassBlur??28}" data-color-setting="glassBlur" aria-label="模糊程度"></label><div class="appearance-preview"><span>${svg('layers')}</span><div><b>恰到好处的留白</b><small>文字对比会随颜色自动调整</small></div></div><div class="appearance-footer"><small>浅色和深色配色分别保存</small><button class="text-button" data-appearance-back>返回设置</button><button class="text-button" data-appearance-reset>恢复默认配色</button></div>`);
 $('#small-dialog').classList.add('appearance-dialog');
 $('#small-dialog').querySelectorAll('[data-color-preset]').forEach(button=>button.onclick=async()=>{const [,bg,glass]=presets[Number(button.dataset.colorPreset)];await setting({customColorsEnabled:true,[prefix+'Bg']:bg,[prefix+'Glass']:glass});applyCustomColors();showAppearance();});
 I18n.localize($('#small-dialog'));
}
const settingsBeforeCustomColors=settings;
settings=async function(){await settingsBeforeCustomColors();$('#theme-select')?.closest('.setting-row').insertAdjacentHTML('afterend',appearanceSettingsRow());hydrate($('#small-dialog'));I18n.localize($('#small-dialog'));};
document.addEventListener('input',event=>{
 const input=event.target,key=input.dataset.colorSetting;if(!key)return;
 state.settings[key]=input.type==='color'?input.value:Number(input.value);state.settings.customColorsEnabled=true;
 $('#custom-colors-enabled').checked=true;const code=$(`[data-color-code="${key}"]`);if(code)code.textContent=input.value;
 if(key==='glassOpacity')$('#glass-opacity-value').textContent=input.value+'%';if(key==='glassBlur')$('#glass-blur-value').textContent=input.value+' px';applyCustomColors();
});
document.addEventListener('change',async event=>{const input=event.target;try{if(input.dataset.colorSetting)await setting({customColorsEnabled:true,[input.dataset.colorSetting]:input.type==='color'?input.value:Number(input.value)});if(input.id==='custom-colors-enabled'){await setting({customColorsEnabled:input.checked});applyCustomColors();}}catch(error){fail(error);}});
document.addEventListener('click',async event=>{const b=event.target.closest('button');if(!b)return;try{if(b.hasAttribute('data-open-appearance'))showAppearance();if(b.hasAttribute('data-appearance-back'))await settings();if(b.hasAttribute('data-appearance-reset')){await setting({...appearanceDefaults});applyCustomColors();showAppearance();}}catch(error){fail(error);}});
