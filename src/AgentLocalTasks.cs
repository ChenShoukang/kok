using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace kok {
    // Deterministic, read-only library answers. The planner never calls a model, never uses
    // the network and never writes: it only reads entries that are already inside the scope.
    public sealed class AgentLocalTask {
        public bool handled;
        public string kind="",title="",summary="",reason="",scopeLabel="";
        public List<Dictionary<string,object>> stats=new List<Dictionary<string,object>>();
        public List<Dictionary<string,object>> items=new List<Dictionary<string,object>>();
        public int total;
        public bool truncated;
        public List<string> followups=new List<string>();
    }

    public static class AgentLocalTasks {
        public const int MaxItems=20;
        const int MaxRows=20;
        static readonly string[] KindOrder={"video","document","photo","music","book"};
        static readonly string[] CollectionWords={"这个分类","当前分类","该分类","此分类","本分类","这个类目","当前类目","this collection","current collection"};
        static readonly string[] CountWords={"统计","数一下","数数","算一下","数量","多少","几份","几个","几篇","几首","几张","几段","几本","几部","一共","总共","合计","共计","计数","count","how many","number of","tally"};
        static readonly string[] ListWords={"找出","列出","列举","罗列","显示","查看","筛选","过滤","找一下","列出来","清单","列表","有哪些","哪些","哪个","哪几","给我看","看看","list","show","find","display","filter","which","what are"};
        static readonly string[] ImportWords={"导入","添加","加入","收录","入库","上传","import","imported","add","added","upload","uploaded"};
        static readonly string[] CollectionIntent={"分类列表","有哪些分类","分类有哪些","哪些分类","每个分类","各分类","分类分布","分类统计","分类数量","分类总数","多少个分类","几个分类","全部分类","所有分类","category list","list categories","which categories","what categories","all categories","categories are there","each category"};
        static readonly string[] TagIntent={"有哪些标签","标签列表","标签有哪些","哪些标签","每个标签","各标签","标签分布","标签统计","标签数量","多少个标签","几个标签","所有标签","全部标签","标签云","tag list","list tags","which tags","all tags","tags are there","each tag"};
        static readonly string[] KindIntent={"各类","各类型","类型分布","文件类型","文件种类","类型统计","类型数量","按类型","每种","每一种","多少种","哪些类型","几种类型","资料库构成","库构成","kind distribution","file types","type breakdown","types of files","what kinds","which kinds","kinds of files","file type","library breakdown","library composition"};
        static readonly string[] RejectZh={"总结","概括","归纳","摘要","提炼","提取","分析","观点","看法","建议","推荐","规划","计划","整理","生成","制作","做一份","做一个","做成","写一份","写一个","撰写","编写","创建","新建","修改","改成","改为","重命名","删除","删掉","移除","移动","合并","拆分","翻译","润色","改写","续写","比较","对比","区别","差异","共同","异同","为什么","怎么","如何","讲什么","讲了什么","说了什么","主要内容","什么意思","主题","思路","教程","方案","步骤","分类建议","标签建议","帮我写","帮我做"};
        static readonly string[] RejectEn={"summar","analys","analyze","analysis","suggest","recommend","organiz","organis","clean up","tidy","rename","delete","remove","modify","translate","compar","difference","differences","common","insight","opinion","how to","explain","extract","outline","theme","idea","write","create","generate","draft","prepare","produce","compose","make a","make an","turn "};

        static readonly Regex ZhDay=new Regex(@"(?:最近|近|过去|前)\s*([0-9]{1,3}|[一二两三四五六七八九十]{1,3})\s*[天日]",RegexOptions.CultureInvariant);
        static readonly Regex ZhDayWithin=new Regex(@"([0-9]{1,3}|[一二两三四五六七八九十]{1,3})\s*[天日]\s*(?:内|以内|之内)",RegexOptions.CultureInvariant);
        static readonly Regex EnDay=new Regex(@"\b(?:last|past|recent)\s+(\d{1,3})\s+days?\b",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
        static readonly Regex ZhWeek=new Regex(@"(?:最近|近|过去|这|本|上)\s*(?:一|1)?\s*个?\s*(?:周|星期|礼拜)",RegexOptions.CultureInvariant);
        static readonly Regex EnWeek=new Regex(@"\b(?:last|past|this)\s+week\b",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
        static readonly Regex ZhMonth=new Regex(@"(?:最近|近|过去|这|本|上)\s*(?:一|1)?\s*个?\s*月",RegexOptions.CultureInvariant);
        static readonly Regex EnMonth=new Regex(@"\b(?:last|past|this)\s+month\b",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
        static readonly Regex SizeZh=new Regex(@"(大于|超过|多于|至少|不小于|不低于|>=|≥|>)\s*([0-9]+(?:\.[0-9]+)?)\s*(TB|GB|MB|KB|兆字节|吉字节|千字节|字节|兆|吉|千|B|G|M|K)",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
        static readonly Regex SizeZhSuffix=new Regex(@"([0-9]+(?:\.[0-9]+)?)\s*(TB|GB|MB|KB|兆|吉|千|G|M|K|B)\s*(以上|或以上|及以上)",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
        static readonly Regex SizeEnAtLeast=new Regex(@"\b(at\s+least|no\s+less\s+than)\s+([0-9]+(?:\.[0-9]+)?)\s*(tb|gb|mb|kb|b)\b",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
        static readonly Regex SizeEn=new Regex(@"\b(larger|bigger|greater|more)\s+than\s+([0-9]+(?:\.[0-9]+)?)\s*(tb|gb|mb|kb|b)\b",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
        static readonly Regex SizeEnOver=new Regex(@"\b(over|above)\s+([0-9]+(?:\.[0-9]+)?)\s*(tb|gb|mb|kb|b)\b",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
        static readonly Regex TagHash=new Regex(@"[#＃]\s*([^\s#＃，,。;；、|「」“”""'（）()\[\]【】]{1,40})",RegexOptions.CultureInvariant);
        static readonly Regex TagQuote=new Regex(@"标签\s*[:：]?\s*[「“""']([^」”""']{1,40})[」”""']",RegexOptions.CultureInvariant);
        static readonly Regex TagMarked=new Regex(@"(?:标了|标记为|打了|带有|带)\s*[#＃]?\s*([^\s#＃，,。;；、]{1,40}?)\s*标签",RegexOptions.CultureInvariant);
        static readonly Regex TagAfter=new Regex(@"标签\s*[:：]?\s*[「“""']?([^的\s，,。;；、」”""']{1,40})",RegexOptions.CultureInvariant);
        static readonly Regex TagEn=new Regex(@"\b(?:tagged|tag)\s+[""“]?([A-Za-z0-9_\-]{1,40})",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);

        sealed class LocalConstraints {
            internal readonly HashSet<string> kinds=new HashSet<string>(StringComparer.Ordinal);
            internal readonly HashSet<string> exts=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            internal readonly List<string> targets=new List<string>();
            internal string collection="",tag="";
            internal bool scopeCollection,favorite;
            internal DateTime? since,until;
            internal string timeLabel="";
            internal long sizeMin=-1;
            internal bool sizeInclusive;
            internal string sizeLabel="";
            internal bool HasConstraint {get{return kinds.Count>0||exts.Count>0||collection.Length>0||scopeCollection||tag.Length>0||favorite||since.HasValue||sizeMin>=0;}}
        }

        // Entry point: pure, read-only and deterministic for a given library snapshot and scope.
        public static AgentLocalTask Plan(Library library,List<string> allowed,string scopeLabel,string goal) {
            var result=new AgentLocalTask();
            if(goal==null||goal.Trim().Length==0){result.reason="empty_goal";return result;}
            string text=goal.Trim();
            bool en=!HasCjk(text);
            result.scopeLabel=DisplayScope(scopeLabel,en);
            if(library==null||library.Db==null){result.reason="unavailable";return result;}
            var scope=new HashSet<string>(allowed??new List<string>(),StringComparer.Ordinal);
            List<Entry> rows=(library.Db.items??new List<Entry>()).Where(e=>e!=null&&scope.Contains(e.id)).ToList();
            string shownScope=result.scopeLabel;
            if(Rejected(text)){result.reason="generative_or_write";return result;}
            if(HasAnyWordList(text,CollectionIntent))return CollectionsPlan(result,rows,shownScope,en,text);
            if(HasAnyWordList(text,TagIntent))return TagsPlan(result,rows,shownScope,en,text);
            if(HasAnyWordList(text,KindIntent))return KindsPlan(result,rows,shownScope,en,text);
            bool scopeCollection=scopeLabel!=null&&scopeLabel.StartsWith("分类",StringComparison.Ordinal);
            bool vagueCollection=HasAnyWordList(text,CollectionWords);
            if(FindCollection(library.Db,text).Length==0&&vagueCollection&&!scopeCollection){result.reason="ambiguous_scope";return result;}
            var c=ParseConstraints(text,library.Db,scopeCollection,en);
            if(!c.HasConstraint){result.reason="no_explicit_constraint";return result;}
            List<Entry> matched=rows.Where(e=>Matches(e,c)).ToList();
            bool count=HasAnyWordList(text,CountWords);
            bool list=HasAnyWordList(text,ListWords);
            if(count)return CountPlan(result,matched,c,shownScope,en,text);
            if(list||c.since.HasValue&&HasAnyWordList(text,ImportWords))return ListPlan(result,matched,c,shownScope,en,text);
            result.reason="no_read_intent";
            return result;
        }

        static LocalConstraints ParseConstraints(string text,Database db,bool scopeCollection,bool en) {
            var c=new LocalConstraints();
            var buffer=new StringBuilder(text);
            string collection=FindCollection(db,text);
            if(collection.Length>0){c.collection=collection;MaskAll(buffer,collection);}
            else if(scopeCollection&&HasAnyWordList(text,CollectionWords))c.scopeCollection=true;
            string tag;Match tagMatch;if(ParseTag(text,out tag,out tagMatch)){c.tag=tag;if(tagMatch!=null)MaskRange(buffer,tagMatch.Index,tagMatch.Length);}
            DateTime? since,until;string timeLabel;Match timeMatch;
            if(ParseTime(text,en,out since,out until,out timeLabel,out timeMatch)){c.since=since;c.until=until;c.timeLabel=timeLabel;if(timeMatch!=null)MaskRange(buffer,timeMatch.Index,timeMatch.Length);else MaskAll(buffer,timeLabel);}
            long sizeMin;bool inclusive;string sizeLabel;Match sizeMatch;
            if(ParseSize(en,text,out sizeMin,out inclusive,out sizeLabel,out sizeMatch)){c.sizeMin=sizeMin;c.sizeInclusive=inclusive;c.sizeLabel=sizeLabel;MaskRange(buffer,sizeMatch.Index,sizeMatch.Length);}
            if(HasAnyWordList(text,new[]{"收藏","favorite","favourite","starred","bookmarked"}))c.favorite=true;
            ParseTargets(buffer.ToString(),c,en);
            return c;
        }

        static bool Matches(Entry e,LocalConstraints c) {
            if(e==null)return false;
            if(c.kinds.Count>0&&!c.kinds.Contains(e.kind??""))return false;
            if(c.exts.Count>0&&!c.exts.Contains(e.ext??""))return false;
            if(c.collection.Length>0&&!String.Equals(e.collection??"",c.collection,StringComparison.Ordinal))return false;
            if(c.tag.Length>0&&!(e.tags??new List<string>()).Any(t=>String.Equals(t,c.tag,StringComparison.OrdinalIgnoreCase)))return false;
            if(c.favorite&&!e.favorite)return false;
            if(c.since.HasValue||c.until.HasValue){DateTime added;if(!TryAdded(e.added,out added))return false;if(c.since.HasValue&&added<c.since.Value)return false;if(c.until.HasValue&&added>=c.until.Value)return false;}
            if(c.sizeMin>=0&&(c.sizeInclusive?e.size<c.sizeMin:e.size<=c.sizeMin))return false;
            return true;
        }

        static AgentLocalTask CountPlan(AgentLocalTask r,List<Entry> matched,LocalConstraints c,string scope,bool en,string goal) {
            r.handled=true;r.kind="count";r.total=matched.Count;
            string noun=TargetNoun(c,en),cond=CondPhrase(c,noun,en,scope);
            r.title=en?"Local count":"本地统计";
            r.summary=matched.Count==0?(en?"No matching "+cond+" in "+scope+".":scope+"中没有"+PadBefore(cond)+"。"):(en?"Found "+matched.Count+" "+cond+" in "+scope+".":scope+"中，"+PadAfter(cond)+"共 "+matched.Count+" 份。");
            r.stats.Add(Stat(en?"Scope":"范围",scope));
            r.stats.Add(Stat(en?"Filter":"条件",cond));
            r.stats.Add(Stat(en?"Matches":"匹配",matched.Count));
            r.followups.Add((en?"List ":"列出")+PadBefore(cond));
            r.followups.Add((en?"Analyze with AI: ":"用 AI 深入分析：")+goal);
            return r;
        }

        static AgentLocalTask ListPlan(AgentLocalTask r,List<Entry> matched,LocalConstraints c,string scope,bool en,string goal) {
            r.handled=true;r.kind="list";r.total=matched.Count;
            List<Entry> sorted=matched.OrderByDescending(AddedValue).ThenBy(e=>e.name??"",StringComparer.Ordinal).ToList();
            foreach(var e in sorted.Take(MaxItems))r.items.Add(ItemRow(e));
            r.truncated=r.total>r.items.Count;
            string noun=TargetNoun(c,en),cond=CondPhrase(c,noun,en,scope);
            r.title=en?"Local filter":"本地筛选";
            r.summary=matched.Count==0?(en?"No matching "+cond+" in "+scope+".":scope+"中没有找到"+PadBefore(cond)+"。"):(en?"Found "+r.total+" "+cond+" in "+scope+"."+(r.truncated?" Showing the "+r.items.Count+" most recent.":""):scope+"中找到 "+r.total+" 份"+PadBefore(cond)+"。"+(r.truncated?"已显示最近添加的 "+r.items.Count+" 份。":""));
            r.stats.Add(Stat(en?"Scope":"范围",scope));
            r.stats.Add(Stat(en?"Filter":"条件",cond));
            r.stats.Add(Stat(en?"Matches":"匹配",r.total));
            if(r.truncated)r.stats.Add(Stat(en?"Shown":"显示",r.items.Count));
            r.followups.Add((en?"Count ":"统计")+PadBefore(cond));
            r.followups.Add((en?"Analyze with AI: ":"用 AI 深入分析：")+goal);
            return r;
        }

        static AgentLocalTask CollectionsPlan(AgentLocalTask r,List<Entry> rows,string scope,bool en,string goal) {
            r.handled=true;r.kind="collections";r.title=en?"Collections":"分类概览";
            var groups=rows.Where(e=>e!=null&&!String.IsNullOrEmpty(e.collection)).GroupBy(e=>e.collection).Select(g=>new{name=g.Key,count=g.Count()}).OrderByDescending(x=>x.count).ThenBy(x=>x.name,StringComparer.Ordinal).ToList();
            int uncategorized=rows.Count(e=>e!=null&&String.IsNullOrEmpty(e.collection));
            r.total=groups.Count;
            foreach(var g in groups.Take(MaxRows))r.stats.Add(Stat(g.name,g.count));
            if(uncategorized>0)r.stats.Add(Stat(en?"Uncategorized":"未分类",uncategorized));
            if(groups.Count==0&&uncategorized==0)r.summary=en?scope+" has no collections yet.":scope+"中还没有分类。";
            else {
                var top=groups.FirstOrDefault();
                r.summary=en?scope+" has "+groups.Count+" collections"+(top==null?"":"; \""+top.name+"\" is the largest with "+top.count+(top.count==1?" item":" items"))+"."+(uncategorized>0?" "+uncategorized+" more not categorized.":""):scope+"共有 "+groups.Count+" 个分类"+(top==null?"":"，「"+top.name+"」最多，共 "+top.count+" 份")+"。"+(uncategorized>0?"另有 "+uncategorized+" 份未分类。":"");
            }
            r.followups.Add(en?"Count file kinds":"统计各类文件数量");
            r.followups.Add((en?"Analyze with AI: ":"用 AI 深入分析：")+goal);
            return r;
        }

        static AgentLocalTask TagsPlan(AgentLocalTask r,List<Entry> rows,string scope,bool en,string goal) {
            r.handled=true;r.kind="tags";r.title=en?"Tags":"标签概览";
            var groups=rows.Where(e=>e!=null&&e.tags!=null).SelectMany(e=>e.tags.Where(t=>!String.IsNullOrWhiteSpace(t))).GroupBy(t=>t).Select(g=>new{name=g.Key,count=g.Count()}).OrderByDescending(x=>x.count).ThenBy(x=>x.name,StringComparer.Ordinal).ToList();
            r.total=groups.Count;
            foreach(var g in groups.Take(MaxRows))r.stats.Add(Stat("#"+g.name,g.count));
            if(groups.Count==0)r.summary=en?scope+" has no tags yet.":scope+"中还没有标签。";
            else {var top=groups[0];r.summary=en?scope+" has "+groups.Count+" tags; #"+top.name+" is the most used ("+top.count+").":scope+"共有 "+groups.Count+" 个标签，「#"+top.name+"」最多，共 "+top.count+" 份。";}
            r.followups.Add(en?"Count file kinds":"统计各类文件数量");
            r.followups.Add((en?"Analyze with AI: ":"用 AI 深入分析：")+goal);
            return r;
        }

        static AgentLocalTask KindsPlan(AgentLocalTask r,List<Entry> rows,string scope,bool en,string goal) {
            r.handled=true;r.kind="kinds";r.title=en?"File kinds":"类型分布";r.total=rows.Count;
            var parts=new List<string>();
            foreach(var kind in KindOrder){
                int n=rows.Count(e=>e!=null&&e.kind==kind);if(n==0)continue;
                string label=KindLabel(kind,en);r.stats.Add(Stat(label,n));parts.Add(label+" "+n);
            }
            int other=rows.Count(e=>e!=null&&!KindOrder.Contains(e.kind??""));
            if(other>0){r.stats.Add(Stat(en?"Other":"其他",other));parts.Add((en?"other ":"其他 ")+other);}
            r.summary=rows.Count==0?(en?scope+" has no items yet.":scope+"中还没有资料。"):(en?scope+" has "+rows.Count+(rows.Count==1?" item: ":" items: ")+String.Join(", ",parts)+".":scope+"共 "+rows.Count+" 份资料："+String.Join("、",parts)+"。");
            r.followups.Add(en?"List collections with counts":"有哪些分类，各自多少份资料");
            r.followups.Add((en?"Analyze with AI: ":"用 AI 深入分析：")+goal);
            return r;
        }

        // Handles both "这个分类" wording (scope derived) and an explicitly quoted collection name.
        static string FindCollection(Database db,string text) {
            if(db==null||db.collections==null)return "";
            foreach(var name in db.collections.Where(x=>!String.IsNullOrWhiteSpace(x)).OrderByDescending(x=>x.Length)){
                int start=0;
                while(start<=text.Length-name.Length){
                    int at=text.IndexOf(name,start,StringComparison.OrdinalIgnoreCase);
                    if(at<0)break;
                    string before=text.Substring(0,at).TrimEnd(),after=text.Substring(at+name.Length).TrimStart();
                    bool quoted=before.Length>0&&@"「“""'《".IndexOf(before[before.Length-1])>=0||after.Length>0&&@"」”""'》".IndexOf(after[0])>=0;
                    bool adjacent=before.EndsWith("分类",StringComparison.Ordinal)||before.EndsWith("类目",StringComparison.Ordinal)||after.StartsWith("分类",StringComparison.Ordinal)||after.StartsWith("类目",StringComparison.Ordinal);
                    if(quoted||adjacent)return name;
                    start=at+1;
                }
            }
            return "";
        }

        static bool ParseTag(string text,out string tag,out Match match) {
            tag="";match=null;
            foreach(var pattern in new[]{TagQuote,TagHash,TagMarked,TagEn,TagAfter}){
                var found=pattern.Match(text);if(!found.Success)continue;
                string candidate=CleanTag(found.Groups[1].Value);
                if(candidate.Length==0)continue;
                tag=candidate;match=found;return true;
            }
            return false;
        }

        static string CleanTag(string value) {
            if(value==null)return "";
            string s=value.Trim().TrimStart('#','＃','「','“','"','\'').TrimEnd('」','”','"','\'','。','，',',');
            foreach(var suffix in new[]{"标签","资料","文件","相关","们"})if(s.EndsWith(suffix,StringComparison.Ordinal))s=s.Substring(0,s.Length-suffix.Length);
            s=s.Trim();
            if(s.Length==0||"有哪些列表如下资料文件统计数量".Contains(s)||s.StartsWith("有",StringComparison.Ordinal))return "";
            return s;
        }

        static bool ParseTime(string text,bool en,out DateTime? since,out DateTime? until,out string label,out Match match) {
            since=null;until=null;label="";match=null;
            DateTime now=DateTime.Now,today=now.Date;
            if(HasAnyWordList(text,new[]{"今天","today"})){since=today;label=en?"today":"今天";return true;}
            if(HasAnyWordList(text,new[]{"昨天","yesterday"})){since=today.AddDays(-1);until=today;label=en?"yesterday":"昨天";return true;}
            int days;var found=ZhDay.Match(text);
            if(!found.Success)found=ZhDayWithin.Match(text);
            if(found.Success&&(days=ParseNumber(found.Groups[1].Value))>0&&days<=365){since=now.AddDays(-days);label=DayLabel(days,en);match=found;return true;}
            found=EnDay.Match(text);
            if(found.Success&&Int32.TryParse(found.Groups[1].Value,out days)&&days>0&&days<=365){since=now.AddDays(-days);label=DayLabel(days,en);match=found;return true;}
            if(ZhWeek.IsMatch(text)||EnWeek.IsMatch(text)){since=now.AddDays(-7);label=DayLabel(7,en);return true;}
            if(ZhMonth.IsMatch(text)||EnMonth.IsMatch(text)){since=now.AddDays(-30);label=DayLabel(30,en);return true;}
            return false;
        }

        static bool ParseSize(bool en,string text,out long min,out bool inclusive,out string label,out Match match) {
            min=-1;inclusive=false;label="";match=null;
            var found=SizeZh.Match(text);
            if(found.Success){
                string word=found.Groups[1].Value;inclusive=word=="至少"||word=="不小于"||word=="不低于";
                min=Bytes(found.Groups[2].Value,found.Groups[3].Value);
                if(min>=0){label=SizeLabel(found.Groups[2].Value,found.Groups[3].Value,inclusive,en);match=found;return true;}
            }
            found=SizeZhSuffix.Match(text);
            if(found.Success){min=Bytes(found.Groups[1].Value,found.Groups[2].Value);if(min>=0){inclusive=true;label=SizeLabel(found.Groups[1].Value,found.Groups[2].Value,true,en);match=found;return true;}}
            found=SizeEnAtLeast.Match(text);
            if(found.Success){min=Bytes(found.Groups[2].Value,found.Groups[3].Value);if(min>=0){inclusive=true;label=SizeLabel(found.Groups[2].Value,found.Groups[3].Value,true,en);match=found;return true;}}
            found=SizeEn.Match(text);
            if(found.Success){min=Bytes(found.Groups[2].Value,found.Groups[3].Value);if(min>=0){label=SizeLabel(found.Groups[2].Value,found.Groups[3].Value,false,en);match=found;return true;}}
            found=SizeEnOver.Match(text);
            if(found.Success){min=Bytes(found.Groups[2].Value,found.Groups[3].Value);if(min>=0){label=SizeLabel(found.Groups[2].Value,found.Groups[3].Value,false,en);match=found;return true;}}
            return false;
        }

        static string SizeLabel(string number,string unit,bool inclusive,bool en) {
            string size=number+" "+unit.ToUpperInvariant();
            if(en)return (inclusive?"at least ":"larger than ")+size;
            return (inclusive?"至少 ":"大于 ")+size;
        }

        static long Bytes(string number,string unit) {
            double n;if(!Double.TryParse(number,NumberStyles.Float,CultureInfo.InvariantCulture,out n)||n<=0||n>1e9)return -1;
            long factor;
            switch((unit??"").ToUpperInvariant()){
                case "TB":factor=1024L*1024*1024*1024;break;
                case "GB":factor=1024L*1024*1024;break;
                case "MB":factor=1024L*1024;break;
                case "KB":factor=1024L;break;
                case "B":factor=1;break;
                case "兆":case "兆字节":factor=1024L*1024;break;
                case "吉":case "吉字节":factor=1024L*1024*1024;break;
                case "千":case "千字节":factor=1024L;break;
                case "G":factor=1024L*1024*1024;break;
                case "M":factor=1024L*1024;break;
                case "K":factor=1024L;break;
                default:return -1;
            }
            double value=n*factor;if(value>9e18)return -1;return (long)value;
        }

        static void ParseTargets(string text,LocalConstraints c,bool en) {
            AddKindIf(text,c,"video","视频","video",en,"视频","影片","电影","录像","video","videos","movie","movies");
            AddKindIf(text,c,"document","文档","document",en,"文档","文稿","document","documents");
            AddKindIf(text,c,"photo","图片","photo",en,"图片","照片","相片","图像","photo","photos","picture","pictures","image","images");
            AddKindIf(text,c,"music","音乐","music track",en,"音乐","音频","歌曲","乐曲","music","audio","song","songs");
            AddKindIf(text,c,"book","电子书","book",en,"电子书","图书","书籍","小说","book","books","ebook","ebooks");
            if(HasAnyWordList(text,new[]{"pdf","pdfs"}))AddExts(c,new[]{"PDF"},en,"PDF","PDF");
            if(HasAnyWordList(text,new[]{"pptx","ppt","powerpoint"})||HasAnyWordList(text,new[]{"演示文稿","幻灯片"}))AddExts(c,new[]{"PPTX","PPT"},en,"演示文稿","presentation");
            if(HasAnyWordList(text,new[]{"docx","doc","word"})||HasAnyWordList(text,new[]{"word文档"}))AddExts(c,new[]{"DOCX","DOC"},en,"Word 文档","Word document");
            if(HasAnyWordList(text,new[]{"xlsx","xls","excel"})||HasAnyWordList(text,new[]{"表格"}))AddExts(c,new[]{"XLSX","XLS","CSV"},en,"表格","spreadsheet");
            if(HasAnyWordList(text,new[]{"csv"}))AddExts(c,new[]{"CSV"},en,"CSV","CSV");
            if(HasAnyWordList(text,new[]{"txt"}))AddExts(c,new[]{"TXT"},en,"TXT","TXT");
            if(HasAnyWordList(text,new[]{"epub"}))AddExts(c,new[]{"EPUB"},en,"EPUB","EPUB");
            if(HasAnyWordList(text,new[]{"markdown"}))AddExts(c,new[]{"MD","MARKDOWN"},en,"Markdown","Markdown");
            if(HasAnyWordList(text,new[]{"html","htm"}))AddExts(c,new[]{"HTML","HTM"},en,"HTML","HTML");
            if(HasAnyWordList(text,new[]{"json"}))AddExts(c,new[]{"JSON"},en,"JSON","JSON");
        }

        static void AddKindIf(string text,LocalConstraints c,string kind,string zh,string enLabel,bool en,params string[] words) {
            foreach(var word in words)if(IsAscii(word)?HasWord(text,word):HasAny(text,word)){
                c.kinds.Add(kind);string label=en?enLabel:zh;if(!c.targets.Contains(label))c.targets.Add(label);return;
            }
        }

        static void AddExts(LocalConstraints c,string[] exts,bool en,string zh,string enLabel) {
            foreach(var ext in exts)c.exts.Add(ext);
            string label=en?enLabel:zh;if(!c.targets.Contains(label))c.targets.Add(label);
        }

        static string TargetNoun(LocalConstraints c,bool en) {
            if(c.targets.Count==0)return en?"items":"资料";
            return en?String.Join(" or ",c.targets):String.Join("、",c.targets);
        }

        static string CondPhrase(LocalConstraints c,string noun,bool en,string scope) {
            if(en){
                var parts=new List<string>();
                if(c.collection.Length>0&&scope!="collection \""+c.collection+"\"")parts.Add("in collection \""+c.collection+"\"");
                if(c.since.HasValue)parts.Add("imported "+c.timeLabel);
                if(c.tag.Length>0)parts.Add("tagged #"+c.tag);
                if(c.favorite)parts.Add("favorited");
                if(c.sizeMin>=0)parts.Add(c.sizeLabel);
                return noun+(parts.Count>0?" "+String.Join(", ",parts):"");
            }
            var pieces=new List<string>();
            if(c.collection.Length>0&&scope!="分类「"+c.collection+"」")pieces.Add("分类「"+c.collection+"」中的");
            if(c.since.HasValue)pieces.Add(c.timeLabel+"导入的");
            if(c.tag.Length>0)pieces.Add("标了 #"+c.tag+" 标签的");
            if(c.favorite)pieces.Add("已收藏的");
            if(c.sizeMin>=0)pieces.Add(c.sizeLabel+"的");
            pieces.Add(noun);
            return String.Concat(pieces);
        }

        static string KindLabel(string kind,bool en) {
            switch(kind){
                case "video":return en?"video":"视频";
                case "document":return en?"document":"文档";
                case "photo":return en?"photo":"图片";
                case "music":return en?"music track":"音乐";
                case "book":return en?"book":"电子书";
                default:return kind;
            }
        }

        static string DisplayScope(string scopeLabel,bool en) {
            string label=(scopeLabel??"").Trim();
            if(!en)return label.Length==0?"整个资料库":label;
            if(label.StartsWith("分类「",StringComparison.Ordinal)&&label.EndsWith("」",StringComparison.Ordinal))return "collection \""+label.Substring(3,Math.Max(0,label.Length-4))+"\"";
            if(label.StartsWith("所选",StringComparison.Ordinal)){string digits=new string(label.Where(Char.IsDigit).ToArray());return digits.Length>0?"the "+digits+" selected items":"the selected items";}
            return "the entire library";
        }

        static string DayLabel(int days,bool en) {
            if(en)return days==7?"in the last 7 days":"in the last "+days+" days";
            return "最近 "+days+" 天";
        }

        static int ParseNumber(string value) {
            int parsed;if(Int32.TryParse(value,out parsed))return parsed;
            int result=0;
            foreach(char c in value){
                if(c=='十'){result=(result==0?1:result)*10;continue;}
                int digit=Digit(c);if(digit<0)return -1;result+=digit;
            }
            return result>0?result:-1;
        }

        static int Digit(char c) {
            switch(c){case '一':return 1;case '二':case '两':return 2;case '三':return 3;case '四':return 4;case '五':return 5;case '六':return 6;case '七':return 7;case '八':return 8;case '九':return 9;default:return -1;}
        }

        static Dictionary<string,object> Stat(string label,object value) {
            return new Dictionary<string,object>{{"label",label},{"value",value}};
        }

        static Dictionary<string,object> ItemRow(Entry e) {
            return new Dictionary<string,object>{{"id",e.id??""},{"name",e.name??""},{"kind",e.kind??""},{"ext",e.ext??""},{"collection",e.collection??""},{"added",e.added??""},{"size",e.size},{"tags",e.tags==null?new List<string>():e.tags.ToList()},{"favorite",e.favorite}};
        }

        static bool TryAdded(string text,out DateTime added) {
            added=DateTime.MinValue;
            if(String.IsNullOrEmpty(text))return false;
            DateTime parsed;
            if(!DateTime.TryParse(text,CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind,out parsed))return false;
            added=parsed.Kind==DateTimeKind.Utc?parsed.ToLocalTime():parsed;
            return true;
        }

        static DateTime AddedValue(Entry e) {
            DateTime added;return TryAdded(e.added,out added)?added:DateTime.MinValue;
        }

        static bool Rejected(string text) {
            if(HasAnyWordList(text,RejectZh))return true;
            // English stems such as "summar" are prefixes, so match them as substrings.
            foreach(var word in RejectEn)if(text.IndexOf(word,StringComparison.OrdinalIgnoreCase)>=0)return true;
            return false;
        }

        static bool HasCjk(string text) {
            foreach(char c in text)if(c>=0x4E00&&c<=0x9FFF)return true;
            return false;
        }

        static bool IsAscii(string word) {
            foreach(char c in word)if(c>127)return false;
            return true;
        }

        static bool IsAsciiAlnum(char c) {
            return c>='a'&&c<='z'||c>='A'&&c<='Z'||c>='0'&&c<='9';
        }

        // Chinese sentences need a space when a Latin token (PDF, Word, Markdown) touches prose.
        static string PadBefore(string value) {
            return value.Length>0&&IsAsciiAlnum(value[0])?" "+value:value;
        }

        static string PadAfter(string value) {
            return value.Length>0&&IsAsciiAlnum(value[value.Length-1])?value+" ":value;
        }

        static bool HasAny(string text,params string[] words) {
            foreach(var word in words)if(word.Length>0&&text.IndexOf(word,StringComparison.OrdinalIgnoreCase)>=0)return true;
            return false;
        }

        static bool HasWord(string text,string word) {
            int start=0;
            while(start<=text.Length-word.Length){
                int at=text.IndexOf(word,start,StringComparison.OrdinalIgnoreCase);
                if(at<0)return false;
                bool left=at==0||!Char.IsLetterOrDigit(text[at-1]);
                int end=at+word.Length;bool right=end>=text.Length||!Char.IsLetterOrDigit(text[end]);
                if(left&&right)return true;
                start=at+1;
            }
            return false;
        }

        static bool HasAnyWordList(string text,IEnumerable<string> words) {
            foreach(var word in words)if(word.Length>0&&(IsAscii(word)?HasWord(text,word):HasAny(text,word)))return true;
            return false;
        }

        static void MaskAll(StringBuilder buffer,string value) {
            if(value==null||value.Length==0)return;
            string current=buffer.ToString();int start=0;
            while(start<=current.Length-value.Length){
                int at=current.IndexOf(value,start,StringComparison.OrdinalIgnoreCase);
                if(at<0)break;
                MaskRange(buffer,at,value.Length);
                start=at+value.Length;
            }
        }

        static void MaskRange(StringBuilder buffer,int start,int length) {
            for(int i=Math.Max(0,start);i<Math.Min(buffer.Length,start+length);i++)buffer[i]=' ';
        }
    }
}
