using System;
using System.IO;
using System.Linq;
using System.Text;
using kok;

class KnowledgeTests {
    public static void Run(string root,Action<bool,string> check){
        var parsed=KnowledgeText.Parse("---\ntags: [学习, design]\naliases: [别名, 'Alias']\n---\n# 目录\n[[目标#章节|显示]] #本地\n- [ ] 待办\n- [X] 完成\n`[[不要]] #不是标签`\n```md\n[[也不要]] #假标签\n```\n[local](note.md#section) [url](https://example.com/a.md) ![image](ignore.md)\n");
        check(parsed.references.Count==2&&parsed.references[0].title=="目标"&&parsed.references[0].section=="章节"&&parsed.references[1].title=="note.md","wiki and local Markdown references parse while code, images and URLs are excluded");
        check(parsed.tags.Count==3&&parsed.tags.Contains("本地")&&!parsed.tags.Contains("章节")&&parsed.aliases.Contains("Alias")&&parsed.tasks==2&&parsed.completed==1,"frontmatter aliases, Unicode tags and tasks index without mistaking heading anchors for tags");
        check(KnowledgeText.Parse("```\n[[hidden]]\n").references.Count==0&&KnowledgeText.Parse("[[%invalid]]\n---\nnot frontmatter").references.Count==1,"unfinished fences and malformed percent escapes do not break indexing");
        var library=new Library(root);var target=library.CreateDocument("目标","md","学习","---\naliases: [别名]\n---\n# 章节\n正文");
        var other=library.CreateDocument("目标","docx","其他","Word 正文");
        // Managed files have globally unique physical names, but library names may be shared between collections.
        other.name="目标";
        var source=library.CreateDocument("起点","md","学习","# 索引\n\n[[目标#章节]] [[别名]] [[未创建]] #学习\n- [ ] 继续\n");
        var ambiguous=library.CreateDocument("独立","md","","[[目标]]");
        var exact=library.CreateDocument("格式链接","md","","[[目标.md]]");
        var index=library.BuildKnowledgeIndex();
        check(index.links.Any(e=>e.source==source.id&&e.target==target.id&&e.count==2)&&!index.links.Any(e=>e.source==source.id&&e.target==other.id),"same-collection and alias references resolve to the real document with repeated links counted");
        check(index.links.Any(e=>e.source==exact.id&&e.target==target.id),"explicit file extensions distinguish Markdown from Word references");
        check(index.nodes.Any(n=>n.ambiguous&&n.candidates.Contains(target.id)&&n.candidates.Contains(other.id))&&index.links.Any(e=>e.source==ambiguous.id&&e.target.StartsWith("missing:")),"ambiguous references retain choices instead of opening an arbitrary document");
        check(index.nodes.Any(n=>n.missing&&n.name=="未创建")&&index.nodes.Any(n=>n.kind=="tag"&&n.key=="学习")&&index.nodes.First(n=>n.id==source.id).tasks==1,"unresolved references, tag nodes and task counts are available to graph and context panels");
        var missing=library.CreateDocument("未创建","md","学习");index=library.BuildKnowledgeIndex();check(index.links.Any(e=>e.source==source.id&&e.target==missing.id)&&!index.nodes.Any(n=>n.missing&&n.name=="未创建"),"creating a linked note replaces its placeholder on the next index");
        library.SaveDocument(source.id,"# 新内容\n[[目标]] #更新",library.DocumentSource(source.id).signature,null);index=library.BuildKnowledgeIndex();check(!index.nodes.Any(n=>n.kind=="tag"&&n.key=="学习")&&index.nodes.Any(n=>n.kind=="tag"&&n.key=="更新"),"saving a note invalidates cached content and removes orphaned tags");
        library.Remove(target.id);index=library.BuildKnowledgeIndex();check(!index.nodes.Any(n=>n.id==target.id)&&!index.links.Any(e=>e.target==target.id),"removing a document clears its indexed metadata and edges");
        var large=library.CreateDocument("Large","md","");File.WriteAllText(large.path,new string('a',1100000),new UTF8Encoding(false));index=library.BuildKnowledgeIndex();check(index.skipped>0&&index.nodes.Any(n=>n.id==large.id),"oversized bodies skip bounded text indexing while retaining library metadata");
        string initial="# 模板\n\n- [ ] 待办\n";var template=library.CreateDocument("模板","docx","",initial);check(library.DocumentSource(template.id).text==initial&&Library.ReadBook(template).chapters.Any(c=>c.text.Contains("待办")),"templates create real Word content in the initial atomic document operation");
    }
}
