using System;
using System.Linq;
using System.Xml.Linq;
using System.Collections.Generic;
using System.Globalization;

namespace kok {
 // Commercial-grade native OOXML presentation composition engine.
 // Every element is a crisp, editable DrawingML shape (rect/roundRect/ellipse/text)
 // so PowerPoint, WPS, Keynote and kok's built-in preview & editor render 1:1 identically
 // with zero unsupported elements.
 internal static class AgentSlideDesign {
  static readonly XNamespace P=AgentPresentationXml.P,A=AgentPresentationXml.A;
  internal static string Value(Dictionary<string,object> row,string key){object value;return row!=null&&row.TryGetValue(key,out value)?Convert.ToString(value)??"":"";}

  sealed class Palette {
   public string Bg,Ink,Muted,Accent,Accent2,Card,Border,AccentInk,HeaderFill,HeaderInk,Zebra,DisplayFace;
  }

  static Palette ThemeOf(string theme){
   switch((theme??"").ToLowerInvariant()){
    case "midnight":
     return new Palette{Bg="111218",Ink="F5F6FA",Muted="9DA4B8",Accent="8B7BFF",Accent2="34D3B3",Card="1B1E2A",Border="2D3246",AccentInk="111218",HeaderFill="242838",HeaderInk="F5F6FA",Zebra="161923",DisplayFace="Microsoft YaHei"};
    case "violet":
     return new Palette{Bg="F7F5FF",Ink="1E1A2E",Muted="68607C",Accent="6D4AFF",Accent2="0F8A6E",Card="FFFFFF",Border="E2D9F8",AccentInk="FFFFFF",HeaderFill="281F4A",HeaderInk="FFFFFF",Zebra="F2EEFE",DisplayFace="Microsoft YaHei"};
    case "slate":
     return new Palette{Bg="F6F6F3",Ink="171716",Muted="6E6D67",Accent="E0532C",Accent2="181817",Card="FFFFFF",Border="E2E1DA",AccentInk="FFFFFF",HeaderFill="181817",HeaderInk="FFFFFF",Zebra="F0EFEA",DisplayFace="Microsoft YaHei"};
    case "ocean":
     return new Palette{Bg="F8FAFC",Ink="0F172A",Muted="64748B",Accent="0369A1",Accent2="0D9488",Card="FFFFFF",Border="D7E2EE",AccentInk="FFFFFF",HeaderFill="0F172A",HeaderInk="FFFFFF",Zebra="F1F5F9",DisplayFace="Microsoft YaHei"};
    case "paper":
     return new Palette{Bg="FFFFFF",Ink="111827",Muted="64748B",Accent="1D4ED8",Accent2="0F766E",Card="F5F7FA",Border="E2E8F0",AccentInk="FFFFFF",HeaderFill="1E293B",HeaderInk="FFFFFF",Zebra="F8FAFC",DisplayFace="Microsoft YaHei"};
    default: // editorial: preserves FFF9F0 + D95338 + SimSun display contract
     return new Palette{Bg="FFF9F0",Ink="221F1B",Muted="706A5E",Accent="D95338",Accent2="237566",Card="F5EEE2",Border="E5DAC8",AccentInk="FFFFFF",HeaderFill="25231F",HeaderInk="FFF9F0",Zebra="EFE7DA",DisplayFace="SimSun"};
   }
  }

  // Reduce type within its readable range and let the Agent revise copy that still exceeds the box.
  static int Fit(string text,double width,double height,int preferred,int minimum=10){
   if(String.IsNullOrEmpty(text))return preferred;
   minimum=Math.Min(minimum,preferred);
   var paragraphs=text.Replace("\r\n","\n").Split('\n');
   for(int size=preferred;size>=minimum;size--){
    double cols=Math.Max(1,(width*72.0)/(size*1.02));
    double lines=0;
    foreach(string line in paragraphs){double wChars=0;foreach(char c in line)wChars+=c>255?1.0:0.56;lines+=Math.Max(1,Math.Ceiling(Math.Max(1,wChars)/cols));}
    double lineHeight=size>=20?1.18:1.12;
    double paraGap=paragraphs.Length>1?(paragraphs.Length-1)*Math.Max(2,size*0.22):0;
    if(lines*size*lineHeight+paraGap<=height*72.0)return size;
   }
   throw new FormatException("Slide copy is too dense for this layout. Shorten the title/body and put details in speaker notes.");
  }

  static void SplitLead(string raw,out string head,out string body){
   head="";body=raw??"";
   if(String.IsNullOrWhiteSpace(raw))return;
   int idx=-1;
   foreach(char sep in new[]{'｜','|','：',':'}){
    int at=raw.IndexOf(sep);
    if(at>=2&&at<=24&&at<raw.Length-2){idx=at;break;}
   }
   if(idx>0){
    string left=raw.Substring(0,idx).Trim(),right=raw.Substring(idx+1).Trim();
    if(left.Length>=2&&left.Length<=24&&right.Length>=2){head=left;body=right;}
   }
  }

  static XElement Shape(int id,string name,string geom,double x,double y,double w,double h,string fill,string line=null,int linePt=100){
   var spPr=new XElement(P+"spPr",
    new XElement(A+"xfrm",
     new XElement(A+"off",new XAttribute("x",(long)Math.Round(x*914400)),new XAttribute("y",(long)Math.Round(y*914400))),
     new XElement(A+"ext",new XAttribute("cx",Math.Max(9144,(long)Math.Round(w*914400))),new XAttribute("cy",Math.Max(9144,(long)Math.Round(h*914400))))),
    new XElement(A+"prstGeom",new XAttribute("prst",geom),new XElement(A+"avLst")),
    String.IsNullOrEmpty(fill)?new XElement(A+"noFill"):new XElement(A+"solidFill",new XElement(A+"srgbClr",new XAttribute("val",fill))),
    String.IsNullOrEmpty(line)?new XElement(A+"ln",new XElement(A+"noFill")):new XElement(A+"ln",new XAttribute("w",linePt*127),new XElement(A+"solidFill",new XElement(A+"srgbClr",new XAttribute("val",line)))));
   return new XElement(P+"sp",
    new XElement(P+"nvSpPr",
     new XElement(P+"cNvPr",new XAttribute("id",id),new XAttribute("name",name)),
     new XElement(P+"cNvSpPr"),
     new XElement(P+"nvPr")),
    spPr);
  }

  static XElement TextShape(int id,string name,string text,double x,double y,double w,double h,int preferredSize,string color,bool bold=false,bool isTitle=false,string face="Microsoft YaHei",string align="l",string valign="t",string fill=null,string line=null,string geom="rect",int minSize=11,double padX=0,double padY=0){
   double innerW=Math.Max(0.2,w-padX*2),innerH=Math.Max(0.15,h-padY*2);
   int size=Fit(text,innerW,innerH,preferredSize,isTitle?Math.Min(preferredSize,20):minSize);
   int spcPct=size>=20?116000:112000;
   int spcAft=size>=20?450:300;
   var paragraphs=(text??"").Replace("\r\n","\n").Split('\n').Select(ln=>
    new XElement(A+"p",
     new XElement(A+"pPr",
      align!="l"?new XAttribute("algn",align):null,
      new XElement(A+"lnSpc",new XElement(A+"spcPct",new XAttribute("val",spcPct))),
      new XElement(A+"spcAft",new XElement(A+"spcPts",new XAttribute("val",spcAft)))),
     new XElement(A+"r",
      new XElement(A+"rPr",
       new XAttribute("lang","zh-CN"),
       new XAttribute("sz",size*100),
       new XAttribute("b",bold?"1":"0"),
       new XElement(A+"solidFill",new XElement(A+"srgbClr",new XAttribute("val",color))),
       new XElement(A+"latin",new XAttribute("typeface",face)),
       new XElement(A+"ea",new XAttribute("typeface",face))),
      new XElement(A+"t",new XAttribute(XNamespace.Xml+"space","preserve"),ln)),
     new XElement(A+"endParaRPr",new XAttribute("sz",size*100))));

   long insX=(long)Math.Round(padX*914400),insY=(long)Math.Round(padY*914400);
   var spPr=new XElement(P+"spPr",
    new XElement(A+"xfrm",
     new XElement(A+"off",new XAttribute("x",(long)Math.Round(x*914400)),new XAttribute("y",(long)Math.Round(y*914400))),
     new XElement(A+"ext",new XAttribute("cx",Math.Max(9144,(long)Math.Round(w*914400))),new XAttribute("cy",Math.Max(9144,(long)Math.Round(h*914400))))),
    new XElement(A+"prstGeom",new XAttribute("prst",geom),new XElement(A+"avLst")),
    String.IsNullOrEmpty(fill)?new XElement(A+"noFill"):new XElement(A+"solidFill",new XElement(A+"srgbClr",new XAttribute("val",fill))),
    String.IsNullOrEmpty(line)?new XElement(A+"ln",new XElement(A+"noFill")):new XElement(A+"ln",new XAttribute("w",12700),new XElement(A+"solidFill",new XElement(A+"srgbClr",new XAttribute("val",line)))));

   return new XElement(P+"sp",
    new XElement(P+"nvSpPr",
     new XElement(P+"cNvPr",new XAttribute("id",id),new XAttribute("name",name)),
     new XElement(P+"cNvSpPr",new XAttribute("txBox","1")),
     new XElement(P+"nvPr",isTitle?new XElement(P+"ph",new XAttribute("type","title")):null)),
    spPr,
    new XElement(P+"txBody",
     new XElement(A+"bodyPr",
      new XAttribute("wrap","square"),
      new XAttribute("anchor",valign),
      new XAttribute("lIns",insX),new XAttribute("rIns",insX),
      new XAttribute("tIns",insY),new XAttribute("bIns",insY),
      new XElement(A+"normAutofit")),
     new XElement(A+"lstStyle"),
     paragraphs));
  }

  delegate void BoxFn(string name,string geom,double x,double y,double w,double h,string fill,string border);
  delegate void TxtFn(string name,string val,double x,double y,double w,double h,int sz,string col,bool bold,bool isTitle,string align,string valign,string fill,string border,string geom,double px,double py);

  static double Clamp(double v,double min,double max){return Math.Max(min,Math.Min(max,v));}

  internal static XElement Slide(Dictionary<string,object> row,int page,string theme,double width=13.333333,double slideHeight=7.5){
   var p=ThemeOf(theme);
   string layout=Value(row,"layout").Trim().ToLowerInvariant();
   string title=Value(row,"title").Trim();
   string kicker=Value(row,"kicker").Trim();
   string subtitle=Value(row,"subtitle").Trim();
   string source=Value(row,"source").Trim();
   var bullets=AgentTaskState.Get<List<string>>(row,"bullets")??new List<string>();
   var headers=AgentTaskState.Get<List<string>>(row,"headers")??new List<string>();
   var tableObj=row.ContainsKey("table")?AgentTaskState.Get<Dictionary<string,object>>(row,"table"):null;
   var chartObj=row.ContainsKey("chart")?AgentTaskState.Get<Dictionary<string,object>>(row,"chart"):null;

   if(layout==""){
    if(page==1)layout="cover";
    else if(tableObj!=null)layout="table";
    else if(chartObj!=null)layout="chart";
    else if(bullets.Count>=4)layout="cards";
    else layout="content";
   }

   var tree=new XElement(P+"spTree",
    new XElement(P+"nvGrpSpPr",new XElement(P+"cNvPr",new XAttribute("id",1),new XAttribute("name","")),new XElement(P+"cNvGrpSpPr"),new XElement(P+"nvPr")),
    new XElement(P+"grpSpPr"));

   double sx=width/13.333333,sy=slideHeight/7.5;
   int id=2;
   BoxFn box=(name,geom,x,y,w,h,fill,border)=>{
    double cx=Clamp(x,0,13.32),cy=Clamp(y,0,7.49),cw=Clamp(w,0.02,13.333333-cx),ch=Clamp(h,0.012,7.5-cy);
    tree.Add(Shape(id++,name,geom,cx*sx,cy*sy,cw*sx,ch*sy,fill,border));
   };
   TxtFn txt=(name,val,x,y,w,h,sz,col,bold,isTitle,align,valign,fill,border,geom,px,py)=>{
    double cx=Clamp(x,0,13.30),cy=Clamp(y,0,7.48),cw=Clamp(w,0.1,13.333333-cx),ch=Clamp(h,0.12,7.5-cy);
    tree.Add(TextShape(id++,name,val,cx*sx,cy*sy,cw*sx,ch*sy,sz,col,bold,isTitle,isTitle?p.DisplayFace:"Microsoft YaHei",align,valign,fill,border,geom,sz>=16?16:10,px*sx,py*sy));
   };

   // Helper for consistent executive header on non-cover/section slides (Title emitted first so roundtrip title detection always finds it)
   Func<double> renderHeader=()=>{
    double y=0.48;
    double kickerY=y;
    if(!String.IsNullOrEmpty(kicker))y+=0.44;
    double titleH=String.IsNullOrEmpty(subtitle)?0.78:0.66;
    txt("Title",title,0.82,y,11.68,titleH,30,p.Ink,true,true,"l","t",null,null,"rect",0,0);
    if(!String.IsNullOrEmpty(kicker)){
     double pillW=Clamp(kicker.Length*0.17+0.55,1.2,4.2);
     txt("Kicker",kicker,0.82,kickerY,pillW,0.34,11,p.Accent,true,false,"l","ctr",p.Card,p.Border,"roundRect",0.12,0.02);
    }
    y+=titleH+0.04;
    if(!String.IsNullOrEmpty(subtitle)){
     txt("Subtitle",subtitle,0.82,y,11.68,0.42,15,p.Muted,false,false,"l","t",null,null,"rect",0,0);
     y+=0.46;
    }
    box("Heading rule","rect",0.82,y,11.68,0.018,p.Border,null);
    box("Heading accent","rect",0.82,y,0.85,0.04,p.Accent,null);
    return y+0.26;
   };

   if(layout=="cover"){
    // Editorial architectural poster cover (emit Title as first text shape)
    box("Accent field","rect",9.65,0,3.683333,7.5,p.Accent,null);
    box("Inset field","roundRect",10.0,0.42,2.98,6.66,p.Card,null);
    box("Cover circle","ellipse",10.72,1.18,1.54,1.54,p.Accent,null);
    box("Cover mini bar1","roundRect",10.38,3.25,2.22,0.1,p.Border,null);
    box("Cover mini bar2","roundRect",10.38,3.52,1.56,0.1,p.Border,null);
    box("Cover mini bar3","roundRect",10.38,3.79,1.88,0.1,p.Border,null);

    double ty=1.45;
    txt("Title",title,0.82,ty,8.35,2.45,46,p.Ink,true,true,"l","ctr",null,null,"rect",0,0);
    if(!String.IsNullOrEmpty(kicker)){
     double pw=Clamp(kicker.Length*0.18+0.6,1.3,4.8);
     txt("Cover kicker",kicker,0.82,ty-0.55,pw,0.38,12,p.Accent,true,false,"ctr","ctr",p.Card,p.Border,"roundRect",0.12,0.02);
    }
    box("Rule","roundRect",0.82,4.15,1.2,0.06,p.Accent,null);
    string subText=!String.IsNullOrEmpty(subtitle)?subtitle:String.Join("\n",bullets);
    if(!String.IsNullOrEmpty(subText))txt("Subtitle",subText,0.82,4.48,8.2,1.85,20,p.Muted,false,false,"l","t",null,null,"rect",0,0);
    txt("Cover number",page.ToString("00"),10.72,1.18,1.54,1.54,38,p.AccentInk,true,false,"ctr","ctr",null,null,"rect",0,0);
    if(!String.IsNullOrEmpty(source))txt("Cover meta",source,10.24,5.95,2.5,0.82,11,p.Muted,false,false,"ctr","b",null,null,"rect",0,0);
   }else if(layout=="section"){
    box("Section panel","rect",0,0,4.25,7.5,p.Accent,null);
    box("Section badge","ellipse",1.12,1.72,2.0,2.0,p.Card,null);
    box("Section bar","roundRect",1.52,4.18,1.2,0.06,p.AccentInk,null);
    txt("Title",title,4.95,2.15,7.45,1.85,40,p.Ink,true,true,"l","ctr",null,null,"rect",0,0);
    txt("Section number",page.ToString("00"),1.12,1.72,2.0,2.0,56,p.Accent,true,false,"ctr","ctr",null,null,"rect",0,0);
    if(!String.IsNullOrEmpty(kicker))txt("Section kicker",kicker,4.95,1.65,7.4,0.4,13,p.Accent,true,false,"l","ctr",null,null,"rect",0,0);
    box("Section rule","roundRect",4.95,4.18,1.0,0.05,p.Accent,null);
    string subText=!String.IsNullOrEmpty(subtitle)?subtitle:String.Join("\n",bullets);
    if(!String.IsNullOrEmpty(subText))txt("Subtitle",subText,4.95,4.46,7.3,1.95,20,p.Muted,false,false,"l","t",null,null,"rect",0,0);
   }else if(layout=="quote"){
    double top=renderHeader();
    box("Quote card","roundRect",0.82,top+0.12,11.68,4.35,p.Card,p.Border);
    box("Quote rule","roundRect",1.16,top+0.55,0.09,3.45,p.Accent,null);
    txt("Quote mark","“",1.48,top+0.28,0.85,0.95,64,p.Accent,true,false,"l","t",null,null,"rect",0,0);
    string qText=bullets.Count>0?String.Join("\n",bullets):subtitle;
    txt("Quote",qText,1.6,top+1.05,10.35,2.65,30,p.Ink,false,false,"l","ctr",null,null,"rect",0,0);
    if(!String.IsNullOrEmpty(source))txt("Quote source","— "+source,1.6,top+3.85,10.35,0.42,14,p.Muted,false,false,"r","ctr",null,null,"rect",0,0);
   }else if(layout=="agenda"){
    double top=renderHeader();
    int count=Math.Max(1,bullets.Count);
    double availH=6.62-top,gap=0.14;
    double rowH=Clamp((availH-gap*(count-1))/count,0.56,1.05);
    for(int n=0;n<bullets.Count;n++){
     double y=top+n*(rowH+gap);
     box("Agenda row "+n,"roundRect",0.82,y,11.68,rowH,p.Card,p.Border);
     txt("Agenda num "+n,(n+1).ToString("00"),1.02,y+(rowH-0.44)/2,0.58,0.44,15,p.AccentInk,true,false,"ctr","ctr",p.Accent,null,"roundRect",0.04,0.02);
     string head,body;SplitLead(bullets[n],out head,out body);
     if(!String.IsNullOrEmpty(head)){
      txt("Agenda head "+n,head,1.8,y+0.08,3.6,rowH-0.16,18,p.Ink,true,false,"l","ctr",null,null,"rect",0,0);
      txt("Agenda body "+n,body,5.55,y+0.08,6.7,rowH-0.16,15,p.Muted,false,false,"l","ctr",null,null,"rect",0,0);
     }else{
      txt("Agenda item "+n,bullets[n],1.8,y+0.08,10.45,rowH-0.16,18,p.Ink,false,false,"l","ctr",null,null,"rect",0,0);
     }
    }
   }else if(layout=="two_columns"||layout=="comparison"){
    double top=renderHeader();
    int split=(bullets.Count+1)/2;
    var leftItems=bullets.Take(split).ToList();
    var rightItems=bullets.Skip(split).ToList();
    double cardH=6.64-top,colW=5.66;
    string leftHead=headers.Count>0&&!String.IsNullOrWhiteSpace(headers[0])?headers[0]:"";
    string rightHead=headers.Count>1&&!String.IsNullOrWhiteSpace(headers[1])?headers[1]:"";
    bool hasHeaders=!String.IsNullOrEmpty(leftHead)||!String.IsNullOrEmpty(rightHead);

    box("Left card","roundRect",0.82,top,colW,cardH,p.Card,p.Border);
    box("Right card","roundRect",6.84,top,colW,cardH,p.Card,p.Border);
    if(hasHeaders){
     txt("Left header",String.IsNullOrEmpty(leftHead)?"核心要点 A":leftHead,0.82,top,colW,0.56,17,p.AccentInk,true,false,"l","ctr",p.Accent,null,"roundRect",0.22,0.04);
     txt("Right header",String.IsNullOrEmpty(rightHead)?"核心要点 B":rightHead,6.84,top,colW,0.56,17,p.HeaderInk,true,false,"l","ctr",p.HeaderFill,null,"roundRect",0.22,0.04);
    }else{
     box("Left accent","roundRect",0.82,top,colW,0.08,p.Accent,null);
     box("Right accent","roundRect",6.84,top,colW,0.08,p.Accent2,null);
    }
    double bodyY=top+(hasHeaders?0.74:0.32),bodyH=Math.Max(1.2,cardH-(hasHeaders?0.94:0.52));
    txt("Left",String.Join("\n",leftItems),1.08,bodyY,colW-0.52,bodyH,19,p.Ink,false,false,"l","t",null,null,"rect",0,0);
    txt("Right",String.Join("\n",rightItems),7.10,bodyY,colW-0.52,bodyH,19,p.Ink,false,false,"l","t",null,null,"rect",0,0);
   }else if(layout=="process"){
    double top=renderHeader();
    int count=Math.Max(1,bullets.Count);
    int cols=count<=4?count:count<=6?3:4;
    int rows=(count+cols-1)/cols;
    double availH=6.62-top,gapX=0.34,gapY=0.26;
    double cellW=(11.68-(cols-1)*gapX)/cols;
    double cellH=(availH-(rows-1)*gapY)/rows;
    for(int n=0;n<bullets.Count;n++){
     int r=n/cols,c=n%cols;
     double x=0.82+c*(cellW+gapX),y=top+r*(cellH+gapY);
     box("Step card "+n,"roundRect",x,y,cellW,cellH,p.Card,p.Border);
     txt("Step badge "+n,(n+1).ToString("00"),x+0.2,y+0.2,0.52,0.52,15,p.AccentInk,true,false,"ctr","ctr",p.Accent,null,"ellipse",0,0);
     if(c<cols-1&&n<bullets.Count-1){
      txt("Step arrow "+n,"→",x+cellW+0.02,y+cellH*0.42,gapX-0.04,0.4,18,p.Accent,true,false,"ctr","ctr",null,null,"rect",0,0);
     }
     string head,body;SplitLead(bullets[n],out head,out body);
     if(!String.IsNullOrEmpty(head)){
      txt("Step title "+n,head,x+0.2,y+0.86,cellW-0.4,0.48,17,p.Ink,true,false,"l","t",null,null,"rect",0,0);
      txt("Step body "+n,body,x+0.2,y+1.36,cellW-0.4,Math.Max(0.4,cellH-1.52),14,p.Muted,false,false,"l","t",null,null,"rect",0,0);
     }else{
      txt("Step "+n,bullets[n],x+0.2,y+0.88,cellW-0.4,Math.Max(0.5,cellH-1.06),17,p.Ink,false,false,"l","t",null,null,"rect",0,0);
     }
    }
   }else if(layout=="timeline"){
    double top=renderHeader();
    int count=bullets.Count;
    if(count==0)throw new FormatException("Timeline layout requires at least one milestone.");
    double trackY=top+0.68,gap=0.2;
    double cellW=(11.68-(count-1)*gap)/count;
    if(count>1)box("Timeline track","roundRect",0.82+cellW/2,trackY-0.025,11.68-cellW,0.05,p.Border,null);
    for(int n=0;n<count;n++){
     double x=0.82+n*(cellW+gap),cx=x+cellW/2;
     box("Node "+n,"ellipse",cx-0.18,trackY-0.18,0.36,0.36,n==count-1?p.Accent:p.Accent2,null);
     string head,body;SplitLead(bullets[n],out head,out body);
     string phase=!String.IsNullOrEmpty(head)?head:("阶段 "+(n+1));
     string desc=!String.IsNullOrEmpty(head)?body:bullets[n];
     txt("Phase "+n,phase,x,top+0.02,cellW,0.4,14,p.Accent,true,false,"ctr","ctr",null,null,"rect",0,0);
     double cardY=trackY+0.42,cardH=Math.Max(1.4,6.56-cardY);
     box("Milestone card "+n,"roundRect",x,cardY,cellW,cardH,p.Card,p.Border);
     box("Milestone top "+n,"roundRect",x,cardY,cellW,0.06,n==count-1?p.Accent:p.Accent2,null);
     txt("Milestone "+n,desc,x+0.18,cardY+0.24,cellW-0.36,cardH-0.4,15,p.Ink,false,false,"l","t",null,null,"rect",0,0);
    }
   }else if(layout=="metrics"){
    double top=renderHeader();
    int count=Math.Max(1,bullets.Count);
    int cols=count<=4?count:count<=6?3:4;
    int rows=(count+cols-1)/cols;
    double availH=6.62-top,gap=0.24;
    double cellW=(11.68-(cols-1)*gap)/cols;
    double cellH=Math.Min(4.15,(availH-(rows-1)*gap)/rows);
    double startY=top+(availH-(rows*cellH+(rows-1)*gap))*0.35;
    for(int n=0;n<bullets.Count;n++){
     double x=0.82+(n%cols)*(cellW+gap),y=startY+(n/cols)*(cellH+gap);
     box("Metric card "+n,"roundRect",x,y,cellW,cellH,p.Card,p.Border);
     box("Metric top "+n,"roundRect",x,y,cellW,0.07,n%2==0?p.Accent:p.Accent2,null);
     var parts=bullets[n].Split(new[]{'|','｜'},3);
     if(parts.Length>=2&&!String.IsNullOrWhiteSpace(parts[0])){
      txt("Metric "+n,parts[0].Trim(),x+0.26,y+0.32,cellW-0.52,Math.Min(1.35,cellH*0.44),44,p.Accent,true,false,"l","ctr",null,null,"rect",0,0);
      txt("Metric label "+n,parts[1].Trim(),x+0.26,y+cellH*0.52,cellW-0.52,Math.Min(0.68,cellH*0.24),18,p.Ink,true,false,"l","t",null,null,"rect",0,0);
      if(parts.Length==3&&!String.IsNullOrWhiteSpace(parts[2])){
       txt("Metric note "+n,parts[2].Trim(),x+0.26,y+cellH*0.76,cellW-0.52,Math.Max(0.32,cellH*0.18),13,p.Muted,false,false,"l","t",null,null,"rect",0,0);
      }
     }else{
      txt("Metric text "+n,bullets[n],x+0.26,y+0.36,cellW-0.52,cellH-0.62,22,p.Ink,false,false,"l","ctr",null,null,"rect",0,0);
     }
    }
   }else if(layout=="table"){
    double top=renderHeader();
    var tHeaders=tableObj!=null?(AgentTaskState.Get<List<string>>(tableObj,"headers")??new List<string>()):new List<string>();
    var tRows=tableObj!=null?(AgentTaskState.Get<List<List<string>>>(tableObj,"rows")??new List<List<string>>()):new List<List<string>>();
    // Fallback if model passed pipe-separated bullets for a table slide
    if(tHeaders.Count==0&&bullets.Count>0){
     var split=bullets.Select(b=>b.Split(new[]{'|','｜'}).Select(c=>c.Trim()).ToList()).ToList();
     tHeaders=split[0];tRows=split.Skip(1).ToList();
    }
    int cols=Math.Max(1,tHeaders.Count);
    int rCount=Math.Max(1,Math.Min(8,tRows.Count));
    double availH=6.60-top-(bullets.Count>0&&tableObj!=null?0.72:0);
    double headH=0.56;
    double rowH=Clamp((availH-headH)/rCount,0.42,0.72);
    double colW=11.68/cols;

    box("Table header bg","roundRect",0.82,top,11.68,headH,p.HeaderFill,null);
    for(int c=0;c<cols;c++){
     txt("TH "+c,tHeaders[c],0.82+c*colW+0.12,top+0.04,colW-0.24,headH-0.08,14,p.HeaderInk,true,false,c==0?"l":"ctr","ctr",null,null,"rect",0,0);
    }
    for(int r=0;r<Math.Min(8,tRows.Count);r++){
     double ry=top+headH+r*rowH;
     box("TR "+r,"rect",0.82,ry,11.68,rowH,r%2==1?p.Zebra:p.Card,p.Border);
     var cells=tRows[r]??new List<string>();
     for(int c=0;c<cols;c++){
      string cell=c<cells.Count?cells[c]:"";
      txt("TD "+r+"_"+c,cell,0.82+c*colW+0.12,ry+0.04,colW-0.24,rowH-0.08,13,c==0?p.Ink:p.Muted,c==0,false,c==0?"l":"ctr","ctr",null,null,"rect",0,0);
     }
    }
    if(tableObj!=null&&bullets.Count>0){
     double noteY=Math.Min(5.98,top+headH+Math.Min(8,tRows.Count)*rowH+0.18);
     txt("Table takeaway",String.Join("  ·  ",bullets),0.82,noteY,11.68,0.54,13,p.Ink,false,false,"l","ctr",p.Card,p.Border,"roundRect",0.18,0.04);
    }
   }else if(layout=="chart"){
    double top=renderHeader();
    string kind=chartObj!=null?Value(chartObj,"kind").ToLowerInvariant():"hbar";
    string unit=chartObj!=null?Value(chartObj,"unit"):"";
    var items=chartObj!=null?(AgentTaskState.Get<List<Dictionary<string,object>>>(chartObj,"items")??new List<Dictionary<string,object>>()):new List<Dictionary<string,object>>();
    bool hasInsights=bullets.Count>0;
    double chartX=0.82,chartW=hasInsights?7.75:11.68,chartH=6.60-top;
    box("Chart panel","roundRect",chartX,top,chartW,chartH,p.Card,p.Border);

    var parsed=new List<Tuple<string,double,string>>();
    foreach(var it in items){
     double v=0;try{v=Convert.ToDouble(it["value"],CultureInfo.InvariantCulture);}catch{}
     string lbl=Value(it,"label"),nt=Value(it,"note");
     parsed.Add(Tuple.Create(lbl,v,String.IsNullOrEmpty(nt)?(v.ToString("0.##",CultureInfo.InvariantCulture)+unit):nt));
    }
    double maxVal=Math.Max(0.0001,parsed.Count>0?parsed.Max(x=>Math.Abs(x.Item2)):1);
    if(parsed.Count>0){
     if(kind=="bar"){
      int n=parsed.Count;
      double innerX=chartX+0.42,innerW=chartW-0.84,baseY=top+chartH-0.78,maxBarH=Math.Max(1.0,chartH-1.52);
      double slotW=innerW/n,barW=Math.Min(0.95,slotW*0.58);
      box("Axis","rect",innerX,baseY,innerW,0.02,p.Border,null);
      for(int i=0;i<n;i++){
       double ratio=Clamp(Math.Abs(parsed[i].Item2)/maxVal,0.06,1.0);
       double bh=maxBarH*ratio,bx=innerX+i*slotW+(slotW-barW)/2,by=baseY-bh;
       bool isPeak=Math.Abs(parsed[i].Item2)>=maxVal*0.999;
       box("Bar "+i,"roundRect",bx,by,barW,bh,isPeak?p.Accent:p.Accent2,null);
       txt("Val "+i,parsed[i].Item3,innerX+i*slotW,Math.Max(top+0.12,by-0.38),slotW,0.34,13,isPeak?p.Accent:p.Ink,true,false,"ctr","b",null,null,"rect",0,0);
       txt("Cat "+i,parsed[i].Item1,innerX+i*slotW,baseY+0.08,slotW,0.58,12,p.Muted,false,false,"ctr","t",null,null,"rect",0,0);
      }
     }else{
      int n=parsed.Count;
      double innerY=top+0.32,innerH=chartH-0.64,gap=0.14;
      double rowH=Clamp((innerH-gap*(n-1))/n,0.36,0.68);
      double labelW=Math.Min(2.45,chartW*0.30),valW=1.25;
      double trackX=chartX+0.28+labelW+0.12,trackW=Math.Max(1.2,chartW-0.56-labelW-valW-0.24);
      for(int i=0;i<n;i++){
       double ry=innerY+i*(rowH+gap),barH=Math.Min(0.32,rowH*0.62),by=ry+(rowH-barH)/2;
       double ratio=Clamp(Math.Abs(parsed[i].Item2)/maxVal,0.05,1.0);
       bool isPeak=Math.Abs(parsed[i].Item2)>=maxVal*0.999;
       txt("Bar label "+i,parsed[i].Item1,chartX+0.26,ry,labelW,rowH,13,p.Ink,isPeak,false,"r","ctr",null,null,"rect",0,0);
       box("Bar track "+i,"roundRect",trackX,by,trackW,barH,p.Border,null);
       box("Bar fill "+i,"roundRect",trackX,by,Math.Max(0.14,trackW*ratio),barH,isPeak?p.Accent:p.Accent2,null);
       txt("Bar val "+i,parsed[i].Item3,trackX+trackW+0.1,ry,valW,rowH,13,isPeak?p.Accent:p.Muted,true,false,"l","ctr",null,null,"rect",0,0);
      }
     }
    }
    if(hasInsights){
     double ix=chartX+chartW+0.26,iw=12.5-ix;
     box("Insight card","roundRect",ix,top,iw,chartH,p.Card,p.Border);
     box("Insight accent","roundRect",ix,top,iw,0.07,p.Accent,null);
     txt("Insight heading","核心洞察",ix+0.22,top+0.22,iw-0.44,0.4,16,p.Accent,true,false,"l","ctr",null,null,"rect",0,0);
     txt("Insight body",String.Join("\n",bullets.Select(b=>"• "+b)),ix+0.22,top+0.72,iw-0.44,chartH-0.92,15,p.Ink,false,false,"l","t",null,null,"rect",0,0);
    }
   }else if(layout=="summary"){
    double top=renderHeader();
    int count=Math.Max(1,bullets.Count);
    var mainPts=count>1?bullets.Take(count-1).ToList():bullets;
    string actionPt=count>1?bullets.Last():"";
    double cardsH=String.IsNullOrEmpty(actionPt)?(6.60-top):(6.60-top-1.16);
    int cols=Math.Min(3,Math.Max(1,mainPts.Count));
    int rows=(mainPts.Count+cols-1)/cols;
    double gap=0.22,cw=(11.68-(cols-1)*gap)/cols,ch=(cardsH-(rows-1)*gap)/rows;
    for(int n=0;n<mainPts.Count;n++){
     double x=0.82+(n%cols)*(cw+gap),y=top+(n/cols)*(ch+gap);
     box("Summary card "+n,"roundRect",x,y,cw,ch,p.Card,p.Border);
     box("Summary bar "+n,"roundRect",x+0.22,y+0.22,0.46,0.06,p.Accent,null);
     string head,body;SplitLead(mainPts[n],out head,out body);
     if(!String.IsNullOrEmpty(head)){
      txt("Sum head "+n,head,x+0.22,y+0.42,cw-0.44,0.48,18,p.Ink,true,false,"l","t",null,null,"rect",0,0);
      txt("Sum body "+n,body,x+0.22,y+0.96,cw-0.44,Math.Max(0.4,ch-1.14),15,p.Muted,false,false,"l","t",null,null,"rect",0,0);
     }else{
      txt("Sum "+n,mainPts[n],x+0.22,y+0.46,cw-0.44,ch-0.66,18,p.Ink,false,false,"l","t",null,null,"rect",0,0);
     }
    }
    if(!String.IsNullOrEmpty(actionPt)){
     double ay=top+cardsH+0.22;
     txt("Next step",actionPt,0.82,ay,11.68,0.88,16,p.HeaderInk,true,false,"l","ctr",p.HeaderFill,null,"roundRect",0.28,0.1);
    }
   }else if(layout=="cards"){
    double top=renderHeader();
    int count=Math.Max(1,bullets.Count);
    int cols=count<=3?count:count==4?2:count>6?4:3;
    int rows=(count+cols-1)/cols;
    double availH=6.62-top,gap=0.24;
    double cellW=(11.68-(cols-1)*gap)/cols;
    double cellH=Math.Min(4.15,(availH-(rows-1)*gap)/rows);
    for(int n=0;n<bullets.Count;n++){
     double x=0.82+(n%cols)*(cellW+gap),y=top+(n/cols)*(cellH+gap);
     box("Card field "+n,"roundRect",x,y,cellW,cellH,p.Card,p.Border);
     txt("Card index "+n,(n+1).ToString("00"),x+0.24,y+0.22,0.46,0.3,11,p.AccentInk,true,false,"ctr","ctr",p.Accent,null,"roundRect",0.04,0.02);
     string head,body;SplitLead(bullets[n],out head,out body);
     if(!String.IsNullOrEmpty(head)){
      txt("Card head "+n,head,x+0.24,y+0.64,cellW-0.48,0.48,18,p.Ink,true,false,"l","t",null,null,"rect",0,0);
      txt("Card body "+n,body,x+0.24,y+1.16,cellW-0.48,Math.Max(0.4,cellH-1.36),15,p.Muted,false,false,"l","t",null,null,"rect",0,0);
     }else{
      txt("Card "+n,bullets[n],x+0.24,y+0.66,cellW-0.48,Math.Max(0.5,cellH-0.86),20,p.Ink,false,false,"l","t",null,null,"rect",0,0);
     }
    }
   }else{ // content: clean structured statement cards
    double top=renderHeader();
    int count=Math.Max(1,bullets.Count);
    double availH=6.62-top,gap=0.18;
    double rowH=Clamp((availH-gap*(count-1))/count,0.62,1.25);
    for(int n=0;n<bullets.Count;n++){
     double y=top+n*(rowH+gap);
     box("Point card "+n,"roundRect",0.82,y,11.68,rowH,p.Card,p.Border);
     box("Bullet accent "+n,"roundRect",0.82,y+0.14,0.08,Math.Max(0.24,rowH-0.28),p.Accent,null);
     string head,body;SplitLead(bullets[n],out head,out body);
     if(!String.IsNullOrEmpty(head)){
      txt("Point head "+n,head,1.14,y+0.12,11.1,Math.Min(0.42,rowH*0.42),17,p.Ink,true,false,"l","ctr",null,null,"rect",0,0);
      txt("Point body "+n,body,1.14,y+Math.Min(0.54,rowH*0.50),11.1,Math.Max(0.32,rowH*0.44),15,p.Muted,false,false,"l","t",null,null,"rect",0,0);
     }else{
      txt("Point "+n,bullets[n],1.14,y+0.08,11.1,rowH-0.16,bullets.Count>5?18:22,p.Ink,false,false,"l","ctr",null,null,"rect",0,0);
     }
    }
   }

   if(layout!="cover"){
    box("Footer rule","rect",0.82,6.88,11.68,0.015,p.Border,null);
    if(!String.IsNullOrEmpty(source)&&layout!="quote"){
     txt("Footer source",source,0.82,6.98,10.5,0.34,10,p.Muted,false,false,"l","ctr",null,null,"rect",0,0);
    }
    txt("Page",page.ToString("00"),11.9,6.98,0.6,0.34,11,p.Muted,true,false,"r","ctr",null,null,"rect",0,0);
   }

   return new XElement(P+"sld",
    new XAttribute(XNamespace.Xmlns+"p",P),
    new XAttribute(XNamespace.Xmlns+"a",A),
    new XAttribute(XNamespace.Xmlns+"r",AgentPresentationXml.R),
    new XElement(P+"cSld",
     new XElement(P+"bg",
      new XElement(P+"bgPr",
       new XElement(A+"solidFill",new XElement(A+"srgbClr",new XAttribute("val",p.Bg))),
       new XElement(A+"effectLst"))),
     tree),
    new XElement(P+"clrMapOvr",new XElement(A+"masterClrMapping")));
  }
 }
}
