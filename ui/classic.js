'use strict';
// v0.7.2 skins: "classic" skeuomorphism is the default look; "graphite" remains available in Settings.

Kok.extend('core','classic.js:applyThemeBeforeSkin','applyTheme',applyThemeBeforeSkin=>function(){applyThemeBeforeSkin();document.body.dataset.skin=state.settings.skin==='graphite'?'graphite':'classic';});

Kok.extend('core','classic.js:setupSettingsBeforeSkin','setupTranslationSettings',setupSettingsBeforeSkin=>function(){
 setupSettingsBeforeSkin();
 const theme=$('#theme-select')?.closest('.setting-row');
 if(theme&&!$('#skin-select'))theme.insertAdjacentHTML('beforebegin',`<div class="setting-row"><div>${I18n.text('界面风格')}<small>${I18n.text('经典拟物或石墨')}</small></div><select id="skin-select" aria-label="${esc(I18n.text('界面风格'))}"><option value="classic">${I18n.text('经典拟物')}</option><option value="graphite">${I18n.text('石墨')}</option></select></div>`);
 const select=$('#skin-select');if(select)select.value=state.settings.skin==='graphite'?'graphite':'classic';
});
document.addEventListener('change',event=>{if(event.target.id!=='skin-select')return;setting({skin:event.target.value}).then(applyTheme).catch(fail);});
if(document.body.dataset.theme)applyTheme();
