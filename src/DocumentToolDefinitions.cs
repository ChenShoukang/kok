using System.Collections.Generic;
namespace kok {internal static class DocumentToolDefinitions {internal static AgentToolSpec[] Tools(){return new[]{
new AgentToolSpec{Requires=Need.NormalLibrary,CapabilityId="documents.createDocument",Name="create_document",Write=true,Signature="create_document{name,format:md|docx|txt,text,collection?}",Description="PROPOSE creating a new document. The user approves it.",Properties=AgentRules.Props("name",AgentRules.Str("File name without extension",100),"format",AgentRules.Str("Format",4,new[]{"md","docx","txt"}),"text",AgentRules.Str("Full document text",20000),"collection",AgentRules.Str("Optional collection",100)),Required=new[]{"name","format","text"}}
};}}}
