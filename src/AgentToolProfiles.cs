using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace kok {
    // P1 tool groups: derive the task's usable tool names from the task intent (goal plus every mid-task
    // adjustment), the delivery requirement and the selected scope. The host stores an automatic list in
    // AgentTaskOptions.allowedTools with allowedToolsAuto=true so later schemas, prompts and validation use the
    // same decision; a caller-provided explicit list (allowedToolsAuto=false) always wins.
    // v0.12.1 C1: the category union is taken from the positive actions only ("不要做PPT" no longer selects the
    // deck group), a later adjustment can add a group (#13), the 14-tool budget only trims the pure report/read
    // BASE profile, and configured capabilities (memory/delegation/sandbox/web/folder/MCP) are appended after
    // the cap so they can never be silently dropped (#19/#20).
    internal static class AgentToolProfiles {
        // Read/search/control core available to every profile. Control tools also bypass the allow-list in
        // ToolAllowed, so they must be included here only to keep the visible schema self-describing.
        static readonly string[] Core={"search_library","read_items","list_collections","recall_result","calculate","update_plan","finish_task"};
        static readonly string[] ReadExtra={"read_document","read_presentation","read_image","read_clips","load_skill"};
        static readonly string[] ReportExtra={"read_document","create_document","replace_document_text","load_skill"};
        static readonly string[] DeckExtra={"read_document","read_presentation","create_presentation","copy_presentation","edit_presentation","load_skill"};
        static readonly string[] EditExtra={"read_document","read_presentation","create_document","replace_document_text","edit_presentation","load_skill"};
        static readonly string[] MediaExtra={"read_document","read_presentation","read_image","index_pdf_pages","index_video_frames","generate_subtitles","load_skill"};
        static readonly string[] OrganizeExtra={"read_document","create_document","set_collection","add_tags","write_note","open_item","load_skill"};
        static readonly string[] GeneralExtra={"read_document","read_presentation","read_image","read_clips","create_document","replace_document_text","write_note","set_collection","add_tags","open_item","load_skill"};
        static readonly string[] FolderReadTools={"list_folder","read_text_file"};
        static readonly string[] FolderWriteTools={"write_text_file","run_command"};
        static readonly string[] JobReadTools={"read_command_job","wait_command_job"};
        static readonly string[] JobWriteTools={"start_command_job","cancel_command_job"};
        static readonly string[] MemReadTools={"search_memory"};
        static readonly string[] MemWriteTools={"remember_memory"};
        static readonly string[] DelegateTools={"delegate_task","delegate_parallel"};
        static readonly string[] WebTools={"web_search","fetch_url"};
        // A chat-only answer must never be turned into a file by exposing creation tools.
        static readonly string[] CreationTools={"create_document","create_presentation","copy_presentation"};
        const string EditPattern=@"(?i)修改|改进|修订|重写|替换|编辑|改成|改为|改一下|改掉|rewrite|revise|edit|replace|update";
        const string ReportPattern=@"(?i)写|起草|撰写|生成|制作|创作|编写|整理成|总结成|报告|文章|文档|讲稿|方案|report|write|draft|compose|generate|create|document|article|essay";
        const string ReadPattern=@"(?i)总结|概括|摘要|解释|分析|比较|问答|翻译|回答|summar|explain|analy|compare|describe|catalog";
        const string MediaPattern=@"(?i)视频|字幕|音频|音乐|图片|照片|截图|影像|帧|video|subtitle|caption|audio|music|image|photo|frame|pdf";
        const string OrganizePattern=@"(?i)整理|分类|标签|收藏|归档|归入|归类|collection|tag|organize|classify";

        internal static List<string> ForTask(AgentTaskOptions options,Library library){return ForTask(options,library,null);}
        internal static List<string> ForTask(AgentTaskOptions options,Library library,IEnumerable<string> adjustments){
            var o=options??new AgentTaskOptions();
            // An explicit caller allow-list wins; only drop names that are no longer registered. An automatic
            // profile is re-derived so a mid-task adjustment can open the newly needed tool group (#13).
            if(!o.allowedToolsAuto&&o.allowedTools!=null)return o.allowedTools.Where(Registered).Distinct().ToList();
            var adjustmentList=(adjustments??Enumerable.Empty<string>()).Where(item=>!String.IsNullOrWhiteSpace(item)).ToList();
            string goal=o.goal??"";
            string actions;
            AgentTaskContract contract=null;
            try{
                actions=AgentTaskIntent.Actions(AgentTaskIntent.Combined(goal,adjustmentList));
                // v0.12.2 C-A: categories and chatOnly come from the ONE current contract, not from an OR over
                // the whole message history (#1). An auto profile (allowedToolsAuto=true) with a stale persisted
                // list is re-derived too, so chat->save restores create_document and save->chat removes it.
                contract=AgentTaskIntent.Current(o,adjustmentList);
            }catch{
                actions=goal;
                contract=null;
            }
            bool discuss=o.intent=="discuss",readOnly=o.approvalPolicy=="read_only";
            bool chatOnly=contract!=null&&contract.ChatOnly;
            bool deck=AgentTaskIntent.IsPresentation(actions)||(contract!=null&&contract.RequiresPresentation)||ScopeHas(o.allowed,"PPTX",library);
            bool edit=(contract!=null&&contract.RequiresTargetedEdit)||Matches(actions,EditPattern);
            bool report=(contract!=null&&contract.RequiresDocument)||Matches(actions,ReportPattern);
            bool read=Matches(actions,ReadPattern)||(contract!=null&&!contract.RequiresDocument&&!contract.RequiresPresentation&&String.IsNullOrWhiteSpace(goal));
            bool media=Matches(actions,MediaPattern)||ScopeOnlyMedia(o.allowed,library);
            bool organize=Matches(actions,OrganizePattern);
            var tools=new List<string>();
            Add(tools,library,Core);
            bool categorized=false;
            if(discuss||readOnly){
                Add(tools,library,ReadExtra);
                if(media)Add(tools,library,new[]{"read_image"});
            }else{
                // Mixed intents take the UNION of every matched group, not the first match: e.g. "tag this
                // note and write a summary" must keep add_tags/write_note AND create_document. Picking a
                // single priority group silently removed the other group's tools and could fail a whole task
                // when the model followed the user's second request, and "生成报告并制作8页PPT" needs both.
                if(deck){Add(tools,library,DeckExtra);categorized=true;}
                if(edit){Add(tools,library,EditExtra);categorized=true;}
                if(report){Add(tools,library,ReportExtra);categorized=true;}
                if(organize){Add(tools,library,OrganizeExtra);categorized=true;}
                if(media){Add(tools,library,MediaExtra);categorized=true;}
                if(!categorized)Add(tools,library,read?ReadExtra:GeneralExtra);
            }
            // The 14-tool budget applies to the pure report/pure read BASE profile only. Trimming happens
            // BEFORE the capability groups below are appended, so a configured capability is never dropped
            // (#19): the old Take(14) silently removed web_search/fetch_url/run_code/delegate_parallel.
            bool mixed=deck||edit||organize||media;
            bool pureReport=report&&!mixed&&!discuss&&!readOnly;
            bool pureRead=!categorized&&read&&!discuss&&!readOnly;
            if((pureReport||pureRead)&&tools.Count>14)tools=tools.Take(14).ToList();
            if(chatOnly)tools=tools.Where(name=>!CreationTools.Contains(name)).ToList();
            if(o.useMemory)Add(tools,library,discuss||readOnly?MemReadTools:MemReadTools.Concat(MemWriteTools).ToArray());
            if(!discuss&&!readOnly&&o.allowDelegation&&String.IsNullOrEmpty(o.parentTaskId))Add(tools,library,DelegateTools);
            if(!discuss&&!readOnly&&o.allowSandbox)Add(tools,library,new[]{"run_code"});
            if(!String.IsNullOrEmpty(o.searchEndpoint))Add(tools,library,WebTools);
            if(library!=null&&AgentExecution.Configured(library)){
                Add(tools,library,FolderReadTools);
                if(!discuss&&!readOnly)Add(tools,library,FolderWriteTools);
                if(Regex.IsMatch(goal+"\n"+String.Join("\n",adjustmentList),@"后台|长任务|作业|命令|编译|构建|测试|运行|代码|工作区|command|build|test|job|process|workspace|coding|code",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant)){
                    Add(tools,library,JobReadTools);if(!discuss&&!readOnly)Add(tools,library,JobWriteTools);
                }
            }
            // #20: already-connected MCP services must be reachable from the automatic profile. A read-only or
            // discussion task may only discover tools (list_mcp_tools), never call one.
            if(library!=null){
                if(ModuleRegistry.Builtins.ToolEnabled("list_mcp_tools",library))Add(tools,library,new[]{"list_mcp_tools"});
                if(!discuss&&!readOnly&&ModuleRegistry.Builtins.ToolEnabled("mcp_call",library))Add(tools,library,new[]{"mcp_call"});
            }
            return tools;
        }
        static bool Registered(string name){return name!=null&&AgentRules.Tools.Any(tool=>tool.Name==name);}
        static bool Matches(string text,string pattern){return !String.IsNullOrWhiteSpace(text)&&Regex.IsMatch(text,pattern);}
        static void Add(List<string> tools,Library library,params string[] names){
            foreach(var name in names){
                if(tools.Contains(name)||!Registered(name))continue;
                if(library!=null&&!ModuleRegistry.Builtins.ToolEnabled(name,library))continue;
                tools.Add(name);
            }
        }
        static bool ScopeHas(List<string> ids,string ext,Library library){
            if(ids==null||library==null||ids.Count==0)return false;
            foreach(var id in ids){var entry=library.Find(id);if(entry!=null&&String.Equals(entry.ext,ext,StringComparison.OrdinalIgnoreCase))return true;}
            return false;
        }
        static bool ScopeOnlyMedia(List<string> ids,Library library){
            if(ids==null||library==null||ids.Count==0)return false;
            bool any=false;
            foreach(var id in ids){var entry=library.Find(id);if(entry==null)continue;any=true;if(entry.kind!="video"&&entry.kind!="music"&&entry.kind!="photo")return false;}
            return any;
        }
    }
}
