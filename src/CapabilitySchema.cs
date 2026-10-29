using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
namespace kok {
 public static class CapabilitySchema {
  public static Dictionary<string,object> Str(int max){return new Dictionary<string,object>{{"type","string"},{"maxLength",max}};}
  public static Dictionary<string,object> Obj(Dictionary<string,object> p=null,string[] required=null,bool additional=false){return new Dictionary<string,object>{{"type","object"},{"properties",p??new Dictionary<string,object>()},{"required",required??new string[0]},{"additionalProperties",additional}};}
  public static Dictionary<string,object> Fields(params object[] pairs){var p=new Dictionary<string,object>();for(int n=0;n<pairs.Length;n+=2)p[(string)pairs[n]]=pairs[n+1];return p;}
  public static void Check(Dictionary<string,object> s,object v){try{Validate(s,v,0);}catch(InvalidDataException){throw;}catch{throw new InvalidDataException("模块参数无效。");}}
  static void Validate(Dictionary<string,object> s,object v,int depth){
   if(v==null&&s!=null&&s.ContainsKey("nullable")&&Convert.ToBoolean(s["nullable"]))return;
   if(depth>20||s==null)throw new InvalidDataException("模块参数无效。");string type=s.ContainsKey("type")?Convert.ToString(s["type"]):"";
   if(type=="object"){var o=v as Dictionary<string,object>;if(o==null||o.Count>100||Library.Json.Serialize(o).Length>64*1024*1024)throw new InvalidDataException("模块参数无效。");var p=s.ContainsKey("properties")?Library.Json.Deserialize<Dictionary<string,Dictionary<string,object>>>(Library.Json.Serialize(s["properties"])):new Dictionary<string,Dictionary<string,object>>();var r=s.ContainsKey("required")?Library.Json.Deserialize<List<string>>(Library.Json.Serialize(s["required"])):new List<string>();if(r.Any(k=>!o.ContainsKey(k)||o[k]==null))throw new InvalidDataException("模块缺少必填参数。");foreach(var x in o){if(p.ContainsKey(x.Key))Validate(p[x.Key],x.Value,depth+1);else if(!s.ContainsKey("additionalProperties")||!Convert.ToBoolean(s["additionalProperties"]))throw new InvalidDataException("模块参数包含未知字段。");}}
   else if(type=="string"){if(!(v is string)||s.ContainsKey("maxLength")&&((string)v).Length>Convert.ToInt32(s["maxLength"]))throw new InvalidDataException("模块参数无效。");}
   else if(type=="boolean"){if(!(v is bool))throw new InvalidDataException("模块参数无效。");}
   else if(type=="integer"||type=="number"){if(v is bool||v is string||v==null)throw new InvalidDataException("模块参数无效。");double n=Convert.ToDouble(v);if(Double.IsNaN(n)||Double.IsInfinity(n)||type=="integer"&&Math.Floor(n)!=n||s.ContainsKey("minimum")&&n<Convert.ToDouble(s["minimum"])||s.ContainsKey("maximum")&&n>Convert.ToDouble(s["maximum"]))throw new InvalidDataException("模块参数无效。");}
   else if(type=="array"){if(v is string||!(v is IEnumerable))throw new InvalidDataException("模块参数无效。");var rows=((IEnumerable)v).Cast<object>().ToList();if(s.ContainsKey("maxItems")&&rows.Count>Convert.ToInt32(s["maxItems"]))throw new InvalidDataException("模块参数无效。");if(s.ContainsKey("items"))foreach(var row in rows)Validate(Library.Json.Deserialize<Dictionary<string,object>>(Library.Json.Serialize(s["items"])),row,depth+1);}
   if(s.ContainsKey("enum")&&!Library.Json.Deserialize<List<object>>(Library.Json.Serialize(s["enum"])).Any(x=>Library.Json.Serialize(x)==Library.Json.Serialize(v)))throw new InvalidDataException("模块参数无效。");
  }
 }
}
