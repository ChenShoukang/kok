using System;
using System.Linq;
using System.Xml.Linq;
using System.Collections.Generic;

namespace kok {
 // Original native editable compositions. Visual principles are adapted through the
 // bundled Apache-2.0 baoyu frontend-design guidance; no reconstructed editor is used.
 internal static class AgentSlideDesign {
  static readonly XNamespace P=AgentPresentationXml.P,A=AgentPresentationXml.A;
  internal static string Value(Dictionary<string,object> row,string key){object value;return row.TryGetValue(key,out value)?Convert.ToString(value):"";}
  static string Color(string theme,string role){
   string[] colors=theme=="midnight"?new[]{"111218","F5F5F7","B8BAC9","C3B3FF","202330"}:theme=="violet"?new[]{"F7F5FF","242032","686077","7356C8","EDE6FC"}:theme=="editorial"?new[]{"FFF9F0","25231F","767064","D95338","F0E8DB"}:new[]{"FFFFFF","172B29","61716B","237566","EDF3F0"};
   return colors[role=="bg"?0:role=="ink"?1:role=="muted"?2:role=="accent"?3:4];
  }
  static int Fit(string text,double width,double height,int preferred,int minimum=16){
   minimum=Math.Min(minimum,preferred);var paragraphs=(text??"").Split('\n');
   for(int size=preferred;size>=minimum;size--){double columns=Math.Max(1,width*72/size),lines=paragraphs.Sum(line=>Math.Max(1,Math.Ceiling(line.Sum(c=>c>255?1d:.55d)/columns)));if(lines*size*1.12+paragraphs.Length*6<=height*72)return size;}
   throw new FormatException("Slide copy is too dense for this layout. Shorten the title/body and put details in speaker notes.");
  }
  static XElement Text(int id,string name,string text,double x,double y,double w,double h,int size,string color,bool bold=false,bool title=false,string face="Microsoft YaHei"){
   size=Fit(text,w,h,size,title?24:16);
   var paragraphs=(text??"").Split('\n').Select(line=>new XElement(A+"p",new XElement(A+"pPr",new XElement(A+"lnSpc",new XElement(A+"spcPct",new XAttribute("val",112000))),new XElement(A+"spcAft",new XElement(A+"spcPts",new XAttribute("val",600)))),new XElement(A+"r",new XElement(A+"rPr",new XAttribute("lang","zh-CN"),new XAttribute("sz",size*100),new XAttribute("b",bold?"1":"0"),new XElement(A+"solidFill",new XElement(A+"srgbClr",new XAttribute("val",color))),new XElement(A+"latin",new XAttribute("typeface",face)),new XElement(A+"ea",new XAttribute("typeface",face))),new XElement(A+"t",new XAttribute(XNamespace.Xml+"space","preserve"),line)),new XElement(A+"endParaRPr",new XAttribute("sz",size*100))));
   return new XElement(P+"sp",new XElement(P+"nvSpPr",new XElement(P+"cNvPr",new XAttribute("id",id),new XAttribute("name",name)),new XElement(P+"cNvSpPr",new XAttribute("txBox","1")),new XElement(P+"nvPr",title?new XElement(P+"ph",new XAttribute("type","title")):null)),new XElement(P+"spPr",new XElement(A+"xfrm",new XElement(A+"off",new XAttribute("x",(long)(x*914400)),new XAttribute("y",(long)(y*914400))),new XElement(A+"ext",new XAttribute("cx",(long)(w*914400)),new XAttribute("cy",(long)(h*914400)))),new XElement(A+"prstGeom",new XAttribute("prst","rect"),new XElement(A+"avLst")),new XElement(A+"noFill")),new XElement(P+"txBody",new XElement(A+"bodyPr",new XAttribute("wrap","square"),new XAttribute("lIns",0),new XAttribute("rIns",0),new XAttribute("tIns",0),new XAttribute("bIns",0),new XElement(A+"normAutofit")),new XElement(A+"lstStyle"),paragraphs));
  }
  static XElement Box(int id,string name,double x,double y,double w,double h,string fill){
   return new XElement(P+"sp",new XElement(P+"nvSpPr",new XElement(P+"cNvPr",new XAttribute("id",id),new XAttribute("name",name)),new XElement(P+"cNvSpPr"),new XElement(P+"nvPr")),new XElement(P+"spPr",new XElement(A+"xfrm",new XElement(A+"off",new XAttribute("x",(long)(x*914400)),new XAttribute("y",(long)(y*914400))),new XElement(A+"ext",new XAttribute("cx",(long)(w*914400)),new XAttribute("cy",(long)(h*914400)))),new XElement(A+"prstGeom",new XAttribute("prst","rect"),new XElement(A+"avLst")),new XElement(A+"solidFill",new XElement(A+"srgbClr",new XAttribute("val",fill))),new XElement(A+"ln",new XElement(A+"noFill"))));
  }
  internal static XElement Slide(Dictionary<string,object> row,int page,string theme,double width=13.333333,double slideHeight=7.5){
   string layout=Value(row,"layout"),title=Value(row,"title"),ink=Color(theme,"ink"),muted=Color(theme,"muted"),accent=Color(theme,"accent"),panel=Color(theme,"panel"),display=theme=="editorial"?"SimSun":"Microsoft YaHei";
   var bullets=AgentTaskState.Get<List<string>>(row,"bullets")??new List<string>();
   if(layout=="")layout=page==1?"cover":bullets.Count>=4?"cards":"content";
   var tree=new XElement(P+"spTree",new XElement(P+"nvGrpSpPr",new XElement(P+"cNvPr",new XAttribute("id",1),new XAttribute("name","")),new XElement(P+"cNvGrpSpPr"),new XElement(P+"nvPr")),new XElement(P+"grpSpPr"));
   double sx=width/13.333333,sy=slideHeight/7.5;int id=2;
   Action<string,string,double,double,double,double,int,string,bool,bool> text=(name,value,x,y,w,h,size,color,bold,isTitle)=>tree.Add(Text(id++,name,value,x*sx,y*sy,w*sx,h*sy,size,color,bold,isTitle,isTitle?display:"Microsoft YaHei"));
   Action<string,double,double,double,double,string> box=(name,x,y,w,h,color)=>tree.Add(Box(id++,name,x*sx,y*sy,w*sx,h*sy,color));
   if(layout=="cover"){
    box("Accent field",9.9,0,3.433333,7.5,accent);box("Inset field",10.22,.36,2.76,6.78,panel);
    text("Title",title,.8,1.72,8.45,2.45,48,ink,true,true);
    box("Rule",.85,4.49,1.05,.045,accent);
    text("Subtitle",String.Join("\n",bullets),.85,4.8,8.1,1.65,23,muted,false,false);
    text("Cover number",page.ToString("00"),10.52,4.8,2.1,1.25,68,accent,true,false);
   }else if(layout=="section"){
    box("Section panel",0,0,4.15,7.5,accent);text("Title",title,4.85,1.85,7.5,2,40,ink,true,true);
    text("Section number",page.ToString("00"),.7,2.1,3,1.65,80,Color(theme,"bg"),true,false);text("Subtitle",String.Join("\n",bullets),4.9,4.15,7.2,1.9,24,muted,false,false);
   }else if(layout=="quote"){
    text("Title",title,.85,.65,11.65,.95,28,muted,true,true);
    box("Quote rule",.88,2.05,.07,3.7,accent);text("Quote",String.Join("\n",bullets),1.3,2.12,10.7,3.75,36,ink,false,false);
   }else{
    text("Title",title,.85,.65,11.65,1.08,34,ink,true,true);box("Heading rule",.88,1.88,11.55,.02,panel);box("Heading accent",.88,1.88,.72,.04,accent);
    if(layout=="two_columns"||layout=="comparison"){
     int split=(bullets.Count+1)/2;
     box("Left field",.88,2.2,5.55,4.25,panel);box("Right field",6.68,2.2,5.75,4.25,panel);
     box("Left accent",.88,2.2,5.55,.055,accent);box("Right accent",6.68,2.2,5.75,.055,muted);
     text("Left",String.Join("\n",bullets.Take(split)),1.18,2.57,4.95,3.45,25,ink,false,false);
     text("Right",String.Join("\n",bullets.Skip(split)),6.98,2.57,5.15,3.45,25,ink,false,false);
    }else if(layout=="process"){
     int count=Math.Max(1,bullets.Count),columns=count<=4?count:4,rows=(count+columns-1)/columns;double gap=.22,cell=(11.55-(columns-1)*gap)/columns,height=rows==1?3.75:1.83;
     for(int n=0;n<bullets.Count;n++){double x=.88+(n%columns)*(cell+gap),y=2.28+(n/columns)*(height+.25);box("Step field "+n,x,y,cell,height,panel);text("Step number "+n,(n+1).ToString("00"),x+.2,y+.23,cell-.4,.55,24,accent,true,false);text("Step "+n,bullets[n],x+.2,y+1.0,cell-.4,height-1.12,23,ink,false,false);}
    }else if(layout=="cards"||layout=="metrics"){
     int count=Math.Max(1,bullets.Count),columns=count<=3?count:count==4?2:count>6?4:3,rows=(count+columns-1)/columns;double gap=.24,cell=(11.55-(columns-1)*gap)/columns,height=Math.Min(4.2,(4.35-(rows-1)*gap)/rows);
     for(int n=0;n<bullets.Count;n++){double x=.88+(n%columns)*(cell+gap),y=2.22+(n/columns)*(height+gap);box("Card field "+n,x,y,cell,height,panel);box("Card accent "+n,x+.23,y+.24,.45,.04,accent);var parts=bullets[n].Split(new[]{'|','｜'},2);bool metric=layout=="metrics"&&parts.Length==2&&!String.IsNullOrWhiteSpace(parts[0]);if(metric){text("Metric "+n,parts[0].Trim(),x+.25,y+.55,cell-.5,Math.Min(1.25,height*.44),40,accent,true,false);text("Metric label "+n,parts[1].Trim(),x+.25,y+height*.57,cell-.5,height*.34,20,ink,false,false);}else text("Card "+n,bullets[n],x+.25,y+.57,cell-.5,height-.78,25,ink,false,false);}
    }else{
     int count=Math.Max(1,bullets.Count);double gap=.12,height=Math.Min(1.2,(4.38-gap*(count-1))/count);
     for(int n=0;n<bullets.Count;n++){double y=2.22+n*(height+gap);box("Bullet accent "+n,.9,y+.17,.075,Math.Max(.2,height-.28),accent);text("Point "+n,bullets[n],1.22,y+.04,10.9,height-.03,bullets.Count>5?22:27,ink,false,false);}
    }
   }
   if(layout!="cover"){box("Footer rule",.88,6.88,11.55,.014,panel);text("Page",page.ToString("00"),11.84,7.03,.6,.26,10,muted,false,false);}
   return new XElement(P+"sld",new XAttribute(XNamespace.Xmlns+"p",P),new XAttribute(XNamespace.Xmlns+"a",A),new XAttribute(XNamespace.Xmlns+"r",AgentPresentationXml.R),new XElement(P+"cSld",new XElement(P+"bg",new XElement(P+"bgPr",new XElement(A+"solidFill",new XElement(A+"srgbClr",new XAttribute("val",Color(theme,"bg")))),new XElement(A+"effectLst"))),tree),new XElement(P+"clrMapOvr",new XElement(A+"masterClrMapping")));
  }
 }
}
