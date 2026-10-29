'use strict';
function setupCompatibility(){
 const about=$('#small-dialog .about-details');
 about.insertAdjacentHTML('beforebegin','<div class="setting-row"><div>格式兼容性<small>查看内置阅读、媒体解码与系统应用范围</small></div><button class="button secondary" id="formats-button">支持格式</button></div>');
 $('#formats-button').addEventListener('click',()=>showCompatibility().catch(fail));
}
async function showCompatibility(){
 const groups=await native('formats');
 const modes={builtin:'内置打开',codec:'取决于编码',external:'建议系统应用'};
 showSmall(`<div class="formats-heading"><button class="icon-button" id="formats-back" aria-label="返回设置">${svg('skipBack')}</button><h2>支持格式</h2></div><p class="dialog-description">能收录不等于能内置打开。媒体播放同时取决于容器、编码和当前 WebView2 环境。</p><div class="formats-list">${['video','music','photo','document','book'].map(kind=>`<section class="format-category"><h3>${svg(kind)}${labels[kind]}</h3>${groups.filter(g=>g.kind===kind).map(g=>`<article class="format-group"><span class="format-mode ${g.mode}">${modes[g.mode]}</span><div class="format-extensions format-icons" data-user-content>${g.extensions.toUpperCase().split(' ').map(ext=>`<span title="${ext}">${FileTypes.icon(ext,g.kind)}<b>${ext}</b></span>`).join('')}</div><p class="format-description">${esc(g.description)}</p></article>`).join('')}</section>`).join('')}<section class="format-category"><h3>字幕与字体</h3><p class="format-extensions" data-user-content>SRT · VTT · ASS · SSA · SUB · SBV · LRC · TTML · DFXP · SMI</p><p class="format-description">字幕仅支持外挂文字文件；SUB 仅支持 MicroDVD。ASS / SSA 不还原特效，TTML 使用基础时间标记；不提取内嵌字幕或识别语音。</p><p class="format-description">字体可导入 TTF / OTF，单个文件最多 20 MB。照片另存 PNG / JPEG，批注导出 HTML，译文导出 TXT / SRT。</p></section></div><p class="dialog-footnote">系统应用入口需要已安装相应软件。kok 不包含外部播放器、Office、OCR 或 DRM 解密工具。</p>`);
 $('#formats-back').addEventListener('click',()=>settings().catch(fail));
 I18n.localize($('#small-dialog'));
}
