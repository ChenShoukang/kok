using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace kok {
 // Deliberately bounded JSON-Schema subset. Unsupported assertions reject a tool;
 // they are never silently treated as permissive and no $ref is fetched.
 public static class McpSchema {
  static readonly string[] Keys={"$schema","type","title","description","default","examples","deprecated","readOnly","writeOnly","format","properties","required","additionalProperties","items","minItems","maxItems","uniqueItems","minLength","maxLength","pattern","minimum","maximum","exclusiveMinimum","exclusiveMaximum","multipleOf","enum","const","minProperties","maxProperties"};
  public static bool Supported(Dictionary<string,object> schema){try{int nodes=0;Definition(schema,0,ref nodes);return Convert.ToString(schema["type"])=="object";}catch{return false;}}
  static void Definition(object schema,int depth,ref int nodes){
   if(depth>12||++nodes>400)throw new InvalidDataException();if(schema is bool)return;var s=schema as Dictionary<string,object>;if(s==null||s.Keys.Any(k=>!Keys.Contains(k)))throw new InvalidDataException();
   object value;if(s.TryGetValue("$schema",out value)&&!new[]{"https://json-schema.org/draft/2020-12/schema","http://json-schema.org/draft-07/schema#","https://json-schema.org/draft-07/schema"}.Contains(Convert.ToString(value)))throw new InvalidDataException();
   if(s.TryGetValue("type",out value)){var types=value is string?new List<string>{(string)value}:Library.Json.Deserialize<List<string>>(Library.Json.Serialize(value));if(types.Count==0||types.Any(t=>!new[]{"object","array","string","number","integer","boolean","null"}.Contains(t)))throw new InvalidDataException();}
   if(s.TryGetValue("properties",out value)){var properties=value as Dictionary<string,object>;if(properties==null||properties.Count>100)throw new InvalidDataException();foreach(var p in properties.Values)Definition(p,depth+1,ref nodes);}
   if(s.TryGetValue("items",out value))Definition(value,depth+1,ref nodes);
   if(s.TryGetValue("additionalProperties",out value)&&!(value is bool))Definition(value,depth+1,ref nodes);
   if(s.TryGetValue("required",out value)){var names=Library.Json.Deserialize<List<string>>(Library.Json.Serialize(value));if(names==null||names.Count>100||names.Any(n=>String.IsNullOrEmpty(n)))throw new InvalidDataException();}
   if(s.TryGetValue("pattern",out value)){if(!(value is string)||((string)value).Length>300)throw new InvalidDataException();new Regex((string)value,RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(50));}
   foreach(var key in new[]{"minItems","maxItems","minLength","maxLength","minProperties","maxProperties","minimum","maximum","exclusiveMinimum","exclusiveMaximum","multipleOf"})if(s.TryGetValue(key,out value)){if(value==null||value is string||value is bool)throw new InvalidDataException();double n=Convert.ToDouble(value);if(Double.IsInfinity(n)||Double.IsNaN(n)||key=="multipleOf"&&n<=0)throw new InvalidDataException();}
  }
  public static void Check(Dictionary<string,object> schema,Dictionary<string,object> args){if(!Supported(schema)||Library.Json.Serialize(args).Length>32000)throw new AgentFailure("mcp_schema","MCP 工具参数或结构不受支持。");try{Validate(schema,args,0);}catch{throw new AgentFailure("mcp_schema","MCP 工具参数不符合已批准的结构。");}}
  static void Validate(object schema,object value,int depth){
   if(depth>16)throw new InvalidDataException();if(schema is bool){if(!(bool)schema)throw new InvalidDataException();return;}var s=(Dictionary<string,object>)schema;object type;
   if(s.TryGetValue("type",out type)){var types=type is string?new List<string>{(string)type}:Library.Json.Deserialize<List<string>>(Library.Json.Serialize(type));if(!types.Any(t=>Type(t,value)))throw new InvalidDataException();}
   if(s.ContainsKey("enum")&&!Library.Json.Deserialize<List<object>>(Library.Json.Serialize(s["enum"])).Any(v=>Library.Json.Serialize(v)==Library.Json.Serialize(value)))throw new InvalidDataException();if(s.ContainsKey("const")&&Library.Json.Serialize(s["const"])!=Library.Json.Serialize(value))throw new InvalidDataException();
   if(value is string){string text=(string)value;if(!Range(s,"minLength","maxLength",text.Length)||s.ContainsKey("pattern")&&!Regex.IsMatch(text,Convert.ToString(s["pattern"]),RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(50)))throw new InvalidDataException();}
   else if(value is Dictionary<string,object>){var data=(Dictionary<string,object>)value;if(data.Count>100||!Range(s,"minProperties","maxProperties",data.Count))throw new InvalidDataException();if(s.ContainsKey("required")&&Library.Json.Deserialize<List<string>>(Library.Json.Serialize(s["required"])).Any(k=>!data.ContainsKey(k)))throw new InvalidDataException();var properties=s.ContainsKey("properties")?(Dictionary<string,object>)s["properties"]:new Dictionary<string,object>();foreach(var pair in data){if(properties.ContainsKey(pair.Key))Validate(properties[pair.Key],pair.Value,depth+1);else if(s.ContainsKey("additionalProperties"))Validate(s["additionalProperties"],pair.Value,depth+1);}}
   else if(value!=null&&!(value is bool)&&value is IEnumerable){var list=((IEnumerable)value).Cast<object>().ToList();if(list.Count>128||!Range(s,"minItems","maxItems",list.Count)||s.ContainsKey("uniqueItems")&&Convert.ToBoolean(s["uniqueItems"])&&list.Select(Library.Json.Serialize).Distinct().Count()!=list.Count)throw new InvalidDataException();if(s.ContainsKey("items"))foreach(var item in list)Validate(s["items"],item,depth+1);}
   else if(value!=null&&!(value is bool)){double number=Convert.ToDouble(value);if(Double.IsInfinity(number)||Double.IsNaN(number)||!Range(s,"minimum","maximum",number)||s.ContainsKey("exclusiveMinimum")&&number<=Convert.ToDouble(s["exclusiveMinimum"])||s.ContainsKey("exclusiveMaximum")&&number>=Convert.ToDouble(s["exclusiveMaximum"]))throw new InvalidDataException();if(s.ContainsKey("multipleOf")){double ratio=number/Convert.ToDouble(s["multipleOf"]);if(Math.Abs(ratio-Math.Round(ratio))>1e-9)throw new InvalidDataException();}}
  }
  static bool Range(Dictionary<string,object> s,string min,string max,double value){return (!s.ContainsKey(min)||value>=Convert.ToDouble(s[min]))&&(!s.ContainsKey(max)||value<=Convert.ToDouble(s[max]));}
  static bool Type(string t,object v){if(t=="null")return v==null;if(t=="object")return v is Dictionary<string,object>;if(t=="string")return v is string;if(t=="boolean")return v is bool;if(t=="array")return v!=null&&!(v is string)&&!(v is Dictionary<string,object>)&&v is IEnumerable;if(v==null||v is bool||v is string||v is IEnumerable)return false;try{double n=Convert.ToDouble(v);return !Double.IsNaN(n)&&!Double.IsInfinity(n)&&(t=="number"||t=="integer"&&n==Math.Floor(n));}catch{return false;}}
 }
}
