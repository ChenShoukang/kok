'use strict';
function showVideoContextMenu(event,item,player){
 GlassMenu.context(event,[{label:'视频属性',action:()=>showVideoProperties(item,player).catch(fail)},{label:'在文件夹中显示',action:()=>native('reveal',{id:item.id}).catch(fail)}]);
}
async function showVideoProperties(item,player){
 const duration=Number.isFinite(player.duration)?time(player.duration):'—',resolution=player.videoWidth&&player.videoHeight?`${player.videoWidth} × ${player.videoHeight}`:'—';
 const file=await native('fileProperties',{id:item.id});if(state.viewer?.id!==item.id)return;if(document.fullscreenElement)await document.exitFullscreen();
 showSmall(`<h2>视频属性</h2><dl class="video-properties"><dt>文件名</dt><dd data-user-content>${esc(file.name)}</dd><dt>文件位置</dt><dd class="property-path" data-user-content>${esc(file.path)}</dd><dt>文件类型</dt><dd data-user-content>${esc(file.extension)}</dd><dt>文件大小</dt><dd data-user-content>${size(file.size)} · ${Number(file.size).toLocaleString()} B</dd><dt>视频时长</dt><dd data-user-content>${duration}</dd><dt>画面分辨率</dt><dd data-user-content>${resolution}</dd><dt>修改时间</dt><dd data-user-content>${esc(file.modified)}</dd></dl><p class="dialog-footnote">编码与音轨信息暂不解析；显示的是当前文件与播放内核实际读取的信息。</p>`);
 I18n.localize($('#small-dialog'));
}
