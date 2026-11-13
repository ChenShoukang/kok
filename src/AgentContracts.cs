using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace kok {
    public sealed class AgentTaskSpec {
        public int version=1,revision;public string goal="",projectId="";
        public string[] capabilities=new string[0];public AgentSpecScope scope=new AgentSpecScope();
        public List<AgentSpecDeliverable> deliverables=new List<AgentSpecDeliverable>();
        public bool fullRead,targetedEdit;public AgentSpecLimits limits=new AgentSpecLimits();
    }
    public sealed class AgentSpecScope {public string mode="all",collection="";public string[] ids=new string[0];}
    public sealed class AgentSpecLimits {public int requests,contextTokens;}
    public sealed class AgentSpecDeliverable {public string kind="",format="",unit="none";public int pages,minLength,maxLength;}
    public sealed class AgentAssetRef {public string id="",role="source",kind="",format="",version="",originCallId="",projectId="",path="";public bool available;}
    public sealed class AgentCapabilityInfo {public string id="",title="",status="planned",reason="";public string[] tools=new string[0];}

    // Versioned projections of the existing authoritative task requirements and native receipts.
    // No parallel mutable requirement store or second execution state machine is introduced.
    internal static class AgentContracts {
        static readonly Dictionary<string,string> intents=new Dictionary<string,string>{
            {"document",@"文档|报告|文章|写作|DOCX|markdown|report|document|article|essay"},
            {"presentation",@"PPT|幻灯片|演示|课件|slides|presentation"},
            {"image",@"照片|图片|图像|海报|修图|image|photo|poster"},
            {"video",@"视频|短片|剪辑|video|movie|clip"},
            {"music",@"音乐|歌曲|作曲|编曲|混音|配乐|音频|music|song|audio|melody"},
            {"coding",@"代码|程序|仓库|编译|测试|工作区|code|coding|repository|compile|debug|workspace"},
            {"computer",@"浏览器|电脑操作|桌面操作|操作软件|computer use|browser automation|desktop automation"},
            {"spreadsheet",@"表格|电子表|数据图表|XLSX|CSV|spreadsheet|workbook"}
        };
        internal static AgentTaskSpec Task(AgentTaskState task){
            var o=task.options;var current=AgentDeliveryRules.Derive(o,task.adjustments);
            string intent=(o.goal??"")+"\n"+String.Join("\n",task.adjustments);
            var caps=intents.Where(pair=>Regex.IsMatch(intent,pair.Value,RegexOptions.IgnoreCase|RegexOptions.CultureInvariant)).Select(pair=>pair.Key).ToList();
            if(caps.Count==0||current.requiresFullSourceCoverage)caps.Insert(0,"research");
            var spec=new AgentTaskSpec{revision=task.steerVersion,goal=o.goal??"",projectId=o.projectId??"",capabilities=caps.Distinct().ToArray(),
                scope=new AgentSpecScope{mode=o.scope==null?"all":o.scope.mode,collection=o.scope==null?"":o.scope.collection??"",ids=o.scope!=null&&o.scope.mode=="selected"?task.TaskScope.ToArray():task.calls.Where(c=>c.status=="done"&&c.sources!=null).SelectMany(c=>c.sources.Keys).Concat(task.createdArtifactIds).Distinct().Take(200).ToArray()},
                fullRead=current.requiresFullSourceCoverage,targetedEdit=current.requiresTargetedEdit,
                limits=new AgentSpecLimits{requests=o.requestLimit,contextTokens=o.contextTokens}};
            string unit=current.lengthUnit.ToString().ToLowerInvariant();
            if(current.requiresDocumentArtifact)spec.deliverables.Add(new AgentSpecDeliverable{kind="document",format=current.requiredFormat??"",minLength=current.minChars,maxLength=current.maxChars,unit=unit});
            if(current.requiresPresentationArtifact)spec.deliverables.Add(new AgentSpecDeliverable{kind="presentation",format="PPTX",pages=current.requestedPages});
            if(current.chatOnly||spec.deliverables.Count==0)spec.deliverables.Add(new AgentSpecDeliverable{kind=current.requiresTargetedEdit?"edit":"answer",minLength=current.minChars,maxLength=current.maxChars,unit=unit});
            return spec;
        }
        internal static List<AgentAssetRef> Assets(AgentTaskState task,Library library){
            var artifacts=new HashSet<string>(task.createdArtifactIds.Concat(task.calls.Where(c=>c.status=="done").Select(AgentTaskState.ArtifactId).Where(id=>id!=null)));
            var selected=task.options.scope!=null&&task.options.scope.mode=="selected"?task.options.scope.ids??new List<string>():new List<string>();
            var ids=selected.Concat(task.calls.Where(c=>c.status=="done"&&c.sources!=null).SelectMany(c=>c.sources.Keys)).Concat(artifacts).Where(task.InScope).Distinct().ToList();
            var stamps=library.AgentSourceStamps(ids);var rows=new List<AgentAssetRef>();
            foreach(string id in ids){var entry=library.Find(id);string stamp;stamps.TryGetValue(id,out stamp);var creator=task.calls.LastOrDefault(c=>c.status=="done"&&AgentTaskState.ArtifactId(c)==id);
                rows.Add(new AgentAssetRef{id=id,role=artifacts.Contains(id)?"artifact":"source",kind=entry==null?"":entry.kind??"",format=entry==null?"":entry.ext??"",version=stamp??"",available=entry!=null&&File.Exists(entry.path),originCallId=creator==null?"":creator.id,projectId=task.options.projectId??""});
            }
            foreach(var call in task.calls.Where(c=>c.status=="done"&&c.result!=null&&(AgentWorkspaceTools.Names.Contains(c.tool)||c.tool=="read_text_file"||c.tool=="write_text_file"||AgentCommandJobTools.Names.Contains(c.tool)))){
                var result=call.result;
                if(result.ContainsKey("result")&&result["result"]!=null)result=AgentTaskState.Get<Dictionary<string,object>>(result,"result");
                foreach(var saved in AgentTaskState.Get<List<AgentAssetRef>>(result,"assets")??new List<AgentAssetRef>()){
                    if(!saved.id.StartsWith("workspace:",StringComparison.Ordinal)||String.IsNullOrEmpty(saved.path))continue;
                    var asset=saved;try{var current=AgentExecution.WorkspaceAsset(library,saved.path,saved.role);if(current.id==saved.id)asset=current;else asset.available=false;}catch{asset.available=false;}
                    asset.originCallId=String.IsNullOrEmpty(saved.originCallId)?call.id:saved.originCallId;asset.projectId=task.options.projectId??"";var previous=rows.FirstOrDefault(r=>r.id==asset.id);if(previous!=null&&previous.role=="artifact"&&asset.role=="source"){asset.role="artifact";asset.originCallId=previous.originCallId;}rows.RemoveAll(r=>r.id==asset.id);rows.Add(asset);
                }
            }
            return rows;
        }
        internal static List<AgentCapabilityInfo> Capabilities(Library library,string appDirectory){
            var tools=new HashSet<string>(AgentRules.Tools.Where(t=>ModuleRegistry.Builtins.ToolEnabled(t.Name,library)).Select(t=>t.Name));
            bool execution=AgentExecution.Configured(library);var rows=new List<AgentCapabilityInfo>();
            Action<string,string,string,string,string[]> add=(id,title,status,reason,names)=>rows.Add(new AgentCapabilityInfo{id=id,title=title,status=status,reason=reason,tools=names.Where(tools.Contains).ToArray()});
            add("research","资料研究",tools.Contains("read_items")?"available":"disabled","精确读取、检索和来源处理。",new[]{"search_library","read_items","read_document","web_search","fetch_url"});
            add("document","文档写作",tools.Contains("create_document")?"available":"disabled","原生文件创建、精确改稿与回读。",new[]{"create_document","read_document","replace_document_text"});
            add("presentation","PPT 制作",tools.Contains("create_presentation")?"available":"disabled","可编辑 PPTX 基础能力，专业视觉质量仍需逐步验证。",new[]{"create_presentation","read_presentation","edit_presentation","copy_presentation"});
            add("image","照片与图像","partial","已有照片编辑界面与图片读取，专用 Agent 像素编辑和生成尚未接入。",new[]{"read_image"});
            add("video","视频制作","partial","已有媒体剪辑界面、索引与导出基础，尚无完整 Agent 时间线工具。",new[]{"index_video_frames","generate_subtitles"});
            add("music","音乐与音频","partial","已有播放与音频片段编辑，音乐生成、编曲和分轨引擎尚未接入。",new string[0]);
            add("coding","代码与工作区",execution?"partial":"unconfigured",execution?"支持代码搜索、版本保护补丁、Git 差异及绑定文件版本的实际测试。":"需在能力与权限中配置并启用工作区。",execution?new[]{"workspace_info","search_workspace","read_workspace_file","git_workspace","apply_workspace_patch","list_folder","read_text_file","write_text_file","run_command","start_command_job","read_command_job","wait_command_job","cancel_command_job"}:new string[0]);
            add("spreadsheet","表格与数据","planned","已有基础计算，专用工作簿编辑器尚未接入。",new[]{"calculate"});
            add("computer","电脑操作",execution?"partial":"unconfigured","支持独立会话中的本地网页观察、输入和点击验证；通用网页与桌面操作待接入。",execution?AgentBrowser.Names:new string[0]);
            return rows;
        }
    }
}
