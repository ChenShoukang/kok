import './mcp.mjs';
import {AgentUi} from './agent-runtime.mjs';
Kok.register({
 id:'agent',routes:{agent:()=>AgentUi.render()},
 nativeActions:['agentTaskPresence','agentRuntimeInfo','agentMemoryList','agentMemorySave','agentMemoryDelete','agentMemoryUndo','agentSandboxResult','agentArtifactSave','agentTaskStart','agentTaskRead','agentTaskList','agentTaskResume','agentTaskDecide','agentTaskStop','agentTaskAdjust','agentTaskUndo','agentTaskEvents','agentTaskTrace','exportAgentTrace','agentTaskDelete','agentHandoffPreview','agentHandoffPrepare','agentHandoffRead','agentSessions','saveAgentSessions','agentPatch','agentCreatedStamp','agentUndoCreated','commandAgent'],
 beforeNavigate(route){if(route!=='agent')AgentUi.presence(false).catch(()=>{});if(route==='organize-legacy')return {route:'organize'};if(route==='organize'&&AgentUi.available()){AgentUi.open({prompt:I18n.text('整理最近导入的资料，按主题建议分类和标签')});return {handled:true};}},
 nav:[{id:'agent',route:'agent',label:'Agent',icon:'agent',order:-100,className:'agent-nav',badge:()=>!!AgentUi.state.job,when:()=>AgentUi.available()}],
 start:AgentUi.start,
 flush:AgentUi.save,
 cardActions:[{id:'agent',label:'Agent',icon:'agent',className:'agent-launch',command:'agent.open',title:'把选中的资料交给 Agent',when:context=>context==='library'&&AgentUi.available()}],
 settings:AgentUi.settings,
 handlers:{'agent.open':()=>AgentUi.open({ids:typeof Warehouse!=='undefined'?Warehouse.selected.map(id=>byId(id)?id:Warehouse.data.clips.find(c=>c.id===id)?.source.itemId).filter(Boolean):typeof libraryDrag!=='undefined'?[...libraryDrag.selected]:[]})},
 dispose:async()=>{await AgentUi.stop();if(state.route==='agent')navigate('overview');}
});
