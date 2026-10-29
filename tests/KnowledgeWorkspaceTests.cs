using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using kok;

class KnowledgeWorkspaceTests {
    public static void Run(string root,Action<bool,string> check){
        var library=new Library(root);library.SetAppMode("smart",true);
        var items=Enumerable.Range(0,24).Select(n=>library.CreateDocument("Scope "+n,"md","Scope","# Scope "+n+"\n\n"+new string((char)('a'+n),2500)+"\nLocal evidence "+n)).ToList();
        var ids=items.Select(i=>i.id).ToList();var scope=new KnowledgeScope{id=Guid.NewGuid().ToString("N"),name="Reading",scope="manual",kind="",ids=ids.Take(2).ToList(),limit=100,from="2026-01-01",to="2026-12-31"};
        library.Settings(new Dictionary<string,object>{{"knowledgeViews",new[]{scope}}});var reopened=new Library(root);var views=Library.ValidateKnowledgeScopes(reopened.Db.settings["knowledgeViews"]);
        check(views.Count==1&&views[0].ids.SequenceEqual(scope.ids),"saved graph scopes retain explicit IDs and date boundaries across restart");
        scope.to="2025-01-01";bool invalid=false;try{library.Settings(new Dictionary<string,object>{{"knowledgeViews",new[]{scope}}});}catch(InvalidDataException){invalid=true;}
        check(invalid&&Library.ValidateKnowledgeScopes(library.Db.settings["knowledgeViews"])[0].to=="2026-12-31","invalid date ranges cannot replace a saved graph scope");
        var context=library.BuildKnowledgeContext(ids,"connect","",6000);
        check(context.ids.Count==24&&context.characters<=6000&&context.outputTokens==900&&context.sampled,"economy recipe bounds all 24 selected source excerpts and the output budget");
        check(!context.context.Contains(root)&&context.references.All(r=>ids.Contains(r.fileId)),"AI preparation excludes file paths and restricts references to selected files");
        var repeat=library.BuildKnowledgeContext(ids.AsEnumerable().Reverse().ToList(),"connect","",6000);
        check(context.fingerprint==repeat.fingerprint,"source selection order does not invalidate an identical analysis cache");
        var partial=library.BuildKnowledgeContext(ids.Take(2).ToList(),"connect","",6000);
        var parsed=KnowledgeRecipes.Parse("{\"summary\":\"Evidence [S1] and [S2]\",\"connections\":[{\"source\":\"S1\",\"target\":\"S2\",\"reason\":\"Shared evidence\"}]}",partial);
        check(parsed.connections.Count==1,"a recipe accepts source-bound suggested connections");
        foreach(string answer in new[]{"{\"summary\":\"Unknown [S99]\",\"connections\":[]}","{\"summary\":\"Test\",\"connections\":[{\"source\":\"S1\",\"target\":\"S1\",\"reason\":\"Self\"}]}","{\"summary\":\"Test\",\"connections\":[{\"source\":\"S1\",\"target\":\"S99\",\"reason\":\"Outside\"}]}"}){
            bool rejected=false;try{KnowledgeRecipes.Parse(answer,partial);}catch(InvalidDataException){rejected=true;}check(rejected,"AI results reject invented citations, self connections or out-of-scope endpoints");
        }
        var report=new KnowledgeReport{id=Guid.NewGuid().ToString("N"),fingerprint="test-fingerprint",created=DateTime.UtcNow.ToString("o"),recipe="connect",summary=parsed.summary,connections=parsed.connections,references=partial.references,ids=partial.ids};library.SaveKnowledgeReport(report);
        check(new Library(root).CachedKnowledgeReport("test-fingerprint").id==report.id,"bounded analysis results persist locally for reuse");
        library.SaveDocument(items[0].id,"Changed evidence",library.DocumentSource(items[0].id).signature,null);
        check(library.BuildKnowledgeContext(ids,"connect","",6000).fingerprint!=context.fingerprint,"editing a source invalidates the prepared context fingerprint");
        library.SetAppMode("pure",false);bool pure=false;try{library.BuildKnowledgeContext(ids,"connect","",6000);}catch(InvalidOperationException){pure=true;}check(pure,"pure mode rejects AI context preparation before any model call");
    }
}
