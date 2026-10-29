import {DocumentUi} from './document-runtime.mjs';
import {PresentationUi} from './presentation-runtime.mjs';
Kok.register({id:'presentations',flush:async()=>{await PresentationUi.save();if(viewer.open&&state.viewer?.ext==='PPTX')await closeViewer();},nativeActions:['readPresentation','newPresentation','exportPresentation','commandPresentation'],handlers:{'presentations.new':()=>DocumentUi.open({format:'pptx'})},viewers:[{
 id:'pptx',accepts:item=>item.ext==='PPTX',
 async open(item){try{await PresentationUi.open(item);}catch(error){if(state.viewer?.id===item.id){$('#viewer-content').innerHTML=empty('PPTX 暂时无法预览',error.message);fail(error);}}}
}]});
