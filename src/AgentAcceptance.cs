using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace kok {
    // v0.12.2 C-B: final-deliverable-bound acceptance (#5/#7A/#8/#9/#10/#11). The check no longer accepts
    // "some owned document meets the length, some other file has a read-back": for each required kind it
    // picks ONE final deliverable (the last successfully created, non-undone artifact owned by this task,
    // still present in the library) and validates type/format/measured length/real page count and a current
    // read-back on that same ID/version. A targeted edit needs its own post-edit read-back; a chat-only
    // delivery is measured on the final answer; full-source coverage is delegated to AgentReading (which now
    // counts exact presentation page coverage too). Multi-deliverable tasks validate each kind independently.
    // Legacy tasks (started before the delivery requirement was persisted) keep their old finish behavior.
    public partial class AgentService {
        static readonly string[] DocumentFinalTools={"create_document"};
        static readonly string[] PresentationFinalTools={"create_presentation","copy_presentation"};
        static readonly string[] DocumentEditTools={"replace_document_text"};
        static readonly string[] PresentationEditTools={"edit_presentation"};
        static readonly string[] AllCreateTools={"create_document","create_presentation","copy_presentation"};
        static readonly string[] AllEditTools={"replace_document_text","edit_presentation"};

        // Called from finish_task(status=completed) through AgentTaskEngine.DeliveryCheck. Empty string means
        // the host-verifiable delivery requirements are met; otherwise an actionable message names the single
        // failing item (final ID / actual value / requirement) so the model can repair only that item.
        internal string DeliveryProblem(AgentTaskState t){
            // t.status is still the pre-finish running/paused state here, so it is never gated on.
            if(t==null||t.options==null)return "";
            // Compatibility: a task journaled by a pre-0.12 host has no persisted requirement. Deriving one
            // now would suddenly block a resumed legacy task, so keep the old finish behavior.
            if(t.options.delivery==null)return "";
            AgentDeliveryRequirement requirement;
            try{requirement=AgentDeliveryRules.Derive(t.options,t.adjustments??Enumerable.Empty<string>());}
            catch{return "";}
            if(requirement==null)return "";
            string codeProblem=AgentCodeDelivery.Problem(normalLibrary,t);if(codeProblem.Length>0)return codeProblem;
            var operations=t.calls.Where(c=>!AgentCognition.IsControl(c.tool)).ToList();

            // 1) Document delivery: all conditions on the last owned create_document's current version.
            if(!requirement.chatOnly&&requirement.requiresDocumentArtifact){
                AgentCall final=FinalArtifact(t,DocumentFinalTools);
                if(final==null)return MissingArtifactMessage(t,operations,requirement,true);
                string id=ResultId(final);
                Entry item=normalLibrary.Find(id);
                if(item==null||!item.ownedDocument||!File.Exists(item.path))return LostArtifactMessage(t,id,requirement,true);
                string formatProblem=FormatProblem(item,requirement);
                if(formatProblem!="")return formatProblem;
                string lengthProblem=DocumentLengthProblem(id,item,requirement);
                if(lengthProblem!="")return lengthProblem;
                int after=ReadBackSequence(t,id,final.sequence,DocumentEditTools);
                string detail="";bool sufficient=false;
                try{sufficient=AgentReading.ReadBackSufficient(normalLibrary,t,id,after,8000,out detail);}
                catch(Exception error){detail=error.Message;}
                if(!sufficient)return "文档成果「"+item.name+"」("+id+") 在创建/修改后还没有充分回读，不能标记 completed"+(String.IsNullOrWhiteSpace(detail)?"。":"（"+detail+"）。")+"请调用 read_items/read_document 读到正文至少前 8000 字符（或全文），确认内容后再 finish_task(completed)；只需补这次回读，已通过的内容和成果保留。";
            }
            // 2) Presentation delivery: real page count and full-page read-back on the same final deck.
            if(!requirement.chatOnly&&requirement.requiresPresentationArtifact){
                AgentCall final=FinalArtifact(t,PresentationFinalTools);
                if(final==null)return MissingArtifactMessage(t,operations,requirement,false);
                string id=ResultId(final);
                Entry item=normalLibrary.Find(id);
                if(item==null||!item.ownedDocument||!File.Exists(item.path))return LostArtifactMessage(t,id,requirement,false);
                int pages;
                if(!TryDeckPages(item,out pages))return "演示文稿「"+item.name+"」("+id+") 无法重新打开，不能作为可信交付物。请修复或重新用 create_presentation 生成完整 PPTX 并回读，再结束任务；其他已通过内容保留。";
                if(requirement.requestedPages>0&&pages!=requirement.requestedPages)
                    return "演示文稿「"+item.name+"」("+id+") 实际 "+pages+" 页，目标要求 "+requirement.requestedPages+" 页（含封面）。请调整页数（插入/删除页面或重做不足部分）后用 read_presentation 从头读到末页，再重新 finish_task(completed)；已符合要求的页面和内容保留，只需处理页数不符项。";
                int after=ReadBackSequence(t,id,final.sequence,PresentationEditTools);
                int total=0,coveredPages=0;string deckDetail="";bool deckOk=false;
                try{deckOk=AgentReading.DeckCoverage(normalLibrary,t,id,after,out total,out coveredPages,out deckDetail);}
                catch(Exception error){deckDetail=error.Message;}
                if(!deckOk){
                    string textDetail="";bool textOk=false;
                    try{textOk=AgentReading.ReadBackSufficient(normalLibrary,t,id,after,8000,out textDetail);}
                    catch(Exception error){textDetail=error.Message;}
                    if(!textOk)return "演示文稿「"+item.name+"」("+id+") 在创建/修改后还没有覆盖全部页面的当前回读，不能标记 completed（实际已覆盖 "+coveredPages+" / "+(total>0?total.ToString():pages.ToString())+" 页"+(String.IsNullOrWhiteSpace(deckDetail)?"":"；"+deckDetail)+"）。请用 read_presentation 从 start_page=1 读到末页并覆盖全部 "+pages+" 页（或 read_items/read_document 读到全文），再 finish_task(completed)；只需补这次回读，已完成的成果保留。";
                }
            }
            // 3) Targeted revision: the latest still-current edit needs a post-edit read-back. A write receipt
            // alone is not verification (#8).
            if(!requirement.chatOnly&&requirement.requiresTargetedEdit&&t.options.workspaceVersion!=1){
                AgentCall edit=LastCurrentEdit(t);
                if(edit==null)return "这是定点改稿任务，但本任务没有成功且当前有效的改稿记录（replace_document_text / edit_presentation）。请先读取目标文件，再提交精确替换并等待用户审批通过，然后回读并重新 finish_task(completed)。已通过的读取与手动修改保留，只需补未通过的改稿。";
                string target=ResultId(edit);
                Entry item=normalLibrary.Find(target);
                bool deck=item!=null&&String.Equals(item.ext,"PPTX",StringComparison.OrdinalIgnoreCase);
                bool covered=false;string detail="";
                try{
                    if(deck){int totalPages=0,coveredDeckPages=0;covered=AgentReading.DeckCoverage(normalLibrary,t,target,edit.sequence,out totalPages,out coveredDeckPages,out detail);}
                    else covered=AgentReading.ReadBackSufficient(normalLibrary,t,target,edit.sequence,8000,out detail);
                }catch(Exception error){detail=error.Message;}
                if(!covered){
                    string label=item==null?target:item.name+" ("+target+")";
                    return "改稿已完成，但在那次修改之后还没有当前有效的回读，不能标记 completed（目标 "+label+" 的改后版本未核对"+(String.IsNullOrWhiteSpace(detail)?"":"；"+detail)+"）。请用 "+(deck?"read_presentation 读完全部页面":"read_items/read_document 读取修改后的正文")+" 核对后再 finish_task(completed)；只需补这次改后回读，已通过的改动保留。";
                }
            }
            // 4) chat-only delivery (#11): the final chat answer is the deliverable. Measure the longer of the
            // last non-meta assistant reply and the pending finish_task summary so nested content in the
            // summary is also accepted.
            if(requirement.chatOnly&&(requirement.minChars>0||requirement.maxChars>0)){
                int measured=Math.Max(Measure(LastAssistantText(t),requirement),Measure(PendingFinishSummary(t),requirement));
                if(!InRange(measured,requirement)){
                    string direction=requirement.maxChars>0&&measured>requirement.maxChars?"超出":"不足";
                    return "聊天交付正文实测 "+measured+" "+UnitText(requirement)+"，要求 "+RangeText(requirement)+"，字数"+direction+"。请直接在最终回复中补足或精简内容（也可以把完整正文放进 finish_task 的 summary），再重新 finish_task(completed)；已完成的读取与成果保留，只需处理长度不符项。";
                }
            }
            // 5) Full-source coverage (#7): AgentReading merges exact document/readable ranges and complete
            // presentation page reads; a partial read of a required source is not coverage.
            if(requirement.requiresFullSourceCoverage&&requirement.requiredSourceIds.Count>0){
                string detail="";bool full=false;
                try{full=AgentReading.FullCoverage(normalLibrary,t,requirement.requiredSourceIds,out detail);}
                catch(Exception error){return "无法确认资料全文覆盖："+error.Message+"。请重新读取全部来源后重试；已生成的内容保留。";}
                if(!full)return "任务要求读完所有资料，但仍有未覆盖内容："+(String.IsNullOrWhiteSpace(detail)?"部分来源尚未完整读取。":detail)+" 请用 read_items/read_document 按 offset/next_offset 继续读到末尾（PPT 来源用 read_presentation 读完全部页面），然后重新 finish_task(completed)；已读完的来源和已生成内容会保留，只需补读未覆盖部分。";
            }
            return "";
        }

        // The last successful, still-present, task-owned creation of one kind. Undone or deleted artifacts are
        // skipped so a canceled draft never becomes the final deliverable, while an older valid artifact still
        // can. Every later check uses this single ID and its current library version.
        AgentCall FinalArtifact(AgentTaskState t,string[] tools){
            for(int n=t.calls.Count-1;n>=0;n--){
                AgentCall call=t.calls[n];
                if(call==null||call.status!="done"||Array.IndexOf(tools,call.tool)<0)continue;
                string id=ResultId(call);
                if(id==null)continue;
                Entry item=normalLibrary.Find(id);
                if(item==null||!item.ownedDocument||!File.Exists(item.path))continue;
                if(!ArtifactOwnedByTask(t,id,call))continue;
                return call;
            }
            return null;
        }
        static string ResultId(AgentCall call){
            if(call==null||call.result==null)return null;
            object value;
            if(!call.result.TryGetValue("id",out value))return null;
            string id=Convert.ToString(value);
            return AgentJournal.ValidId(id)?id:null;
        }
        // The read-back must be after the creation of the final artifact AND after its latest body edit.
        static int ReadBackSequence(AgentTaskState t,string id,int createSequence,string[] editTools){
            int after=createSequence;
            foreach(AgentCall call in t.calls){
                if(call==null||call.status!="done"||Array.IndexOf(editTools,call.tool)<0||call.sequence<=after)continue;
                if(!String.Equals(ResultId(call),id,StringComparison.Ordinal))continue;
                after=call.sequence;
            }
            return after;
        }
        string MissingArtifactMessage(AgentTaskState t,List<AgentCall> operations,AgentDeliveryRequirement requirement,bool document){
            string wanted=document?(requirement.requiresPresentationArtifact?"文档与演示文稿":"文档（MD/TXT/DOCX）"):(requirement.requiresDocumentArtifact?"文档与演示文稿":"PPTX 演示文稿");
            string hint=document?"create_document":"create_presentation";
            bool oppositeCreated=operations.Any(c=>c.status=="done"&&(document?Array.IndexOf(PresentationFinalTools,c.tool)>=0:Array.IndexOf(DocumentFinalTools,c.tool)>=0)&&ResultId(c)!=null);
            if(oppositeCreated)return "交付物类型不符：目标要求"+wanted+"，但本任务只成功创建了"+(document?"演示文稿":"文档")+"。请用 "+hint+" 生成目标类型的成果并充分回读，再 finish_task(completed)；已通过的内容保留，只需补齐缺失的交付物类型。";
            bool undone=operations.Any(c=>(c.status=="undone"||c.status=="failed")&&AgentJournal.ValidId(ResultId(c)));
            if(undone)return "本任务创建的"+(document?"文档":"演示文稿")+"成果已不在资料库或已被撤销，不能作为交付物。请重新调用 "+hint+" 创建成果并充分回读，再 finish_task(completed)；已通过的其他内容保留。";
            return "目标要求交付"+wanted+"，但本任务没有成功创建成果。请调用 "+hint+" 生成真实文件，等待用户审批通过，再充分回读确认，然后重新 finish_task(completed)；已完成的分析与读取会保留，只需补齐缺失的成果。";
        }
        string LostArtifactMessage(AgentTaskState t,string id,AgentDeliveryRequirement requirement,bool document){
            string hint=document?"create_document":"create_presentation";
            return "最终交付物"+id+" 已不在资料库、被撤销或不再属于本任务，不能作为交付物。请重新调用 "+hint+" 创建成果并充分回读，再 finish_task(completed)；已通过的其他内容保留。";
        }
        // Explicit format requirement (#10): DOCX/Word, MD/Markdown and TXT are distinct deliverables.
        string FormatProblem(Entry item,AgentDeliveryRequirement requirement){
            string wanted=NormalizeFormat(requirement.requiredFormat);
            if(wanted=="")return "";
            string actual=NormalizeFormat(item.ext);
            if(actual==wanted)return "";
            return "交付格式不符：最终文档「"+item.name+"」("+item.id+") 的实际格式是 "+(String.IsNullOrWhiteSpace(item.ext)?"未知":item.ext)+"，目标要求 "+wanted+"。请用 create_document 创建 "+wanted+" 格式的最终文档并充分回读，再 finish_task(completed)；已通过的内容保留，只需修复格式不符项。";
        }
        static string NormalizeFormat(string format){
            if(String.IsNullOrWhiteSpace(format))return "";
            string value=format.Trim().ToUpperInvariant();
            if(value.Contains("DOCX")||value.Contains("WORD"))return "DOCX";
            if(value.Contains("MARKDOWN")||value=="MD")return "MD";
            if(value.Contains("TXT")||value.Contains("TEXT"))return "TXT";
            return value;
        }
        // Measured body length of the FINAL document only (#9): unit-aware and reported with the actual value.
        string DocumentLengthProblem(string id,Entry item,AgentDeliveryRequirement requirement){
            if(requirement.minChars<=0&&requirement.maxChars<=0)return "";
            string text;
            try{text=normalLibrary.DocumentSource(id).text??"";}
            catch(Exception error){return "最终文档「"+item.name+"」("+id+") 的正文当前无法读取（"+error.Message+"），不能验证字数要求。请修复或重新保存该文档并充分回读后再 finish_task(completed)；已通过的内容保留。";}
            int measured=Measure(text,requirement);
            if(InRange(measured,requirement))return "";
            string direction=requirement.maxChars>0&&measured>requirement.maxChars?"超出":"不足";
            return "最终文档「"+item.name+"」("+id+") 正文实测 "+measured+" "+UnitText(requirement)+"，要求 "+RangeText(requirement)+"，字数"+direction+"。请补充或精简该文档正文后重新充分回读，再 finish_task(completed)；已通过的内容保留，只需处理这一份最终交付物的字数不符项。";
        }
        static int Measure(string text,AgentDeliveryRequirement requirement){
            AgentMeasureUnit unit=requirement.lengthUnit;
            if(unit==AgentMeasureUnit.None)unit=AgentMeasureUnit.Hanzi;
            return AgentDeliveryRules.MeasuredLength(text,unit);
        }
        static bool InRange(int measured,AgentDeliveryRequirement requirement){
            return (requirement.minChars<=0||measured>=requirement.minChars)&&(requirement.maxChars<=0||measured<=requirement.maxChars);
        }
        static string UnitText(AgentDeliveryRequirement requirement){
            if(requirement.lengthUnit==AgentMeasureUnit.Characters)return "个字符";
            if(requirement.lengthUnit==AgentMeasureUnit.Words)return "个英文单词";
            return "个汉字";
        }
        static string RangeText(AgentDeliveryRequirement requirement){
            string unit=UnitText(requirement);
            if(requirement.minChars>0&&requirement.maxChars>0)return requirement.minChars+"-"+requirement.maxChars+" "+unit;
            if(requirement.minChars>0)return "不少于 "+requirement.minChars+" "+unit;
            return "不超过 "+requirement.maxChars+" "+unit;
        }
        static string LastAssistantText(AgentTaskState t){
            AgentDialogue last=t.dialogue.LastOrDefault(d=>d!=null&&d.role=="agent"&&!d.meta&&!d.interrupted);
            return last==null?"":(last.text??"");
        }
        // The finish_task call normally exists in state.calls before execution (the engine proposes then reads
        // it); taking its summary lets a long final answer live in the finish payload.
        static string PendingFinishSummary(AgentTaskState t){
            for(int n=t.calls.Count-1;n>=0;n--){
                AgentCall call=t.calls[n];
                if(call==null||call.tool!="finish_task"||call.args==null)continue;
                object value;
                if(call.args.TryGetValue("summary",out value))return Convert.ToString(value)??"";
            }
            return "";
        }
        AgentCall LastCurrentEdit(AgentTaskState t){
            for(int n=t.calls.Count-1;n>=0;n--){
                AgentCall call=t.calls[n];
                if(call==null||call.status!="done"||Array.IndexOf(AllEditTools,call.tool)<0)continue;
                string id=ResultId(call);
                if(id==null||!normalLibrary.AgentHas(id))continue;
                bool current;
                try{current=EvidenceCurrentFor(call,t);}catch{current=false;}
                if(!current)continue;
                return call;
            }
            return null;
        }
        bool ArtifactOwnedByTask(AgentTaskState t,string id,AgentCall createCall){
            try{
                if(t.createdArtifactIds!=null&&t.createdArtifactIds.Contains(id))return true;
                foreach(Operation operation in normalLibrary.OperationList(0,512)){
                    if(operation==null||operation.createdItemId!=id)continue;
                    return operation.source=="agent:"+t.id&&operation.state=="applied";
                }
                Entry item=normalLibrary.Find(id);
                return item!=null&&createCall!=null&&!String.IsNullOrEmpty(item.agentOperationId)&&item.agentOperationId==createCall.id;
            }catch{return false;}
        }
        bool TryDeckPages(Entry deck,out int pages){
            pages=0;
            try{pages=PresentationFiles.Read(deck.path,false).slides.Count;return true;}
            catch{return false;}
        }
    }
}
