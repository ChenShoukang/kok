using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace kok {
    public partial class AgentService {
        // Current file/receipt facts are collected at the native boundary. TypeScript decides whether they satisfy the task.
        object BusinessArtifactFacts(AgentTaskState task,AgentCall call,AgentDeliveryRequirement requirement,bool presentation){
            if(call==null)return null;
            string id=ResultId(call);var item=normalLibrary.Find(id);
            bool exists=item!=null&&File.Exists(item.path),owned=item!=null&&item.ownedDocument&&ArtifactOwnedByTask(task,id,call);
            int length=0,pages=0;bool readable=false,readBack=false;string detail="";
            var errors=new List<string>();
            if(exists)try{
                int after=ReadBackSequence(task,id,call.sequence,presentation?PresentationEditTools:DocumentEditTools);
                if(presentation){
                    readable=TryDeckPages(item,out pages);int total,covered;
                    readBack=AgentReading.DeckCoverage(normalLibrary,task,id,after,out total,out covered,out detail);
                    if(!readBack)readBack=AgentReading.ReadBackSufficient(normalLibrary,task,id,after,Int32.MaxValue,out detail);
                    var review=AgentArtifactReview.Presentation(normalLibrary,id,task);
                    errors=review.findings.Where(finding=>finding.severity=="error").Select(finding=>"第 "+finding.page+" 页 / "+finding.shapeId+"："+finding.message).ToList();
                }else{
                    var source=normalLibrary.DocumentSource(id);readable=!source.recovery;
                    length=AgentDeliveryRules.MeasuredLength(source.text,requirement.lengthUnit);
                    readBack=AgentReading.ReadBackSufficient(normalLibrary,task,id,after,8000,out detail);
                    if(task.options.artifactReviewVersion>=2){var review=AgentArtifactReview.Document(normalLibrary,id,requirement.requiredSections,requirement,task);errors=review.findings.Where(finding=>finding.severity=="error").Select(finding=>finding.message).ToList();}
                }
            }catch(Exception error){readable=false;detail=AgentWorkspaceTools.Redact(error.Message);}
            return new{id=id,name=item==null?id:item.name,exists=exists,owned=owned,format=item==null?"":item.ext,length=length,pages=pages,readable=readable,readBack=readBack,detail=detail,qualityErrors=errors};
        }

        string BusinessDeliveryProblem(AgentTaskState task){
            var requirement=AgentDeliveryRules.Derive(task.options,task.adjustments);
            var doc=requirement.requiresDocumentArtifact?FinalArtifact(task,DocumentFinalTools):null;
            var deck=requirement.requiresPresentationArtifact?FinalArtifact(task,PresentationFinalTools):null;
            object docFacts=BusinessArtifactFacts(task,doc,requirement,false),deckFacts=BusinessArtifactFacts(task,deck,requirement,true),editFacts=null;
            if(requirement.requiresTargetedEdit&&task.options.workspaceVersion!=1){
                var edit=LastCurrentEdit(task);if(edit!=null){var item=normalLibrary.Find(ResultId(edit));editFacts=BusinessArtifactFacts(task,edit,requirement,item!=null&&item.ext=="PPTX");}
            }
            bool sourcesFull=true;string sourceDetail="";
            if(requirement.requiresFullSourceCoverage&&requirement.requiredSourceIds.Count>0)try{sourcesFull=AgentReading.FullCoverage(normalLibrary,task,requirement.requiredSourceIds,out sourceDetail);}catch(Exception error){sourcesFull=false;sourceDetail=AgentWorkspaceTools.Redact(error.Message);}
            var plan=AgentBusinessCore.Plan(task.options,task.adjustments);bool renderingComplete=false;
            if(deck!=null){
                string id=ResultId(deck),signature=normalLibrary.PresentationCoverageDigest(id);var rendered=new HashSet<int>();
                foreach(var call in task.calls.Where(call=>call.tool=="render_presentation"&&call.status=="done"&&call.result!=null)){
                    var render=AgentTaskState.Get<Dictionary<string,object>>(call.result,"render");if(render==null||ModuleContext.Text(render,"id")!=id||ModuleContext.Text(render,"sourceVersion")!=signature)continue;
                    foreach(var frame in AgentTaskState.Get<List<Dictionary<string,object>>>(render,"frames")??new List<Dictionary<string,object>>())rendered.Add(Convert.ToInt32(frame["page"]));
                }
                int pages=normalLibrary.PresentationPageCount(id);renderingComplete=Enumerable.Range(1,pages).All(rendered.Contains);
            }
            return AgentBusinessCore.Invoke<string>("delivery_problem",new{requirement=requirement,document=docFacts,presentation=deckFacts,edit=editFacts,workspaceVersion=task.options.workspaceVersion,
                codeProblem=AgentCodeDelivery.Problem(normalLibrary,task),chatLength=Math.Max(Measure(LastAssistantText(task),requirement),Measure(PendingFinishSummary(task),requirement)),sourcesFull=sourcesFull,sourceDetail=sourceDetail,renderingRequired=plan.renderingRequired,renderingComplete=renderingComplete});
        }
    }
}
