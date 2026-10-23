using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace kok {
    public static class Localization {
        static Dictionary<string,string> messages=new Dictionary<string,string>();
        static List<Rule> rules=new List<Rule>();
        class Rule { public Regex regex;public string replacement;public int[] translate; }
        static Localization(){
            try{
                string path=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"ui","locales","en.json");
                var root=(Dictionary<string,object>)Library.Json.DeserializeObject(File.ReadAllText(path,Encoding.UTF8));
                messages=((Dictionary<string,object>)root["messages"]).ToDictionary(pair=>pair.Key,pair=>Convert.ToString(pair.Value));
                foreach(Dictionary<string,object> item in (object[])root["patterns"])rules.Add(new Rule{regex=new Regex(Convert.ToString(item["pattern"]),RegexOptions.Singleline|RegexOptions.CultureInvariant),replacement=Convert.ToString(item["replacement"]),translate=item.ContainsKey("translate")?((object[])item["translate"]).Select(Convert.ToInt32).ToArray():new int[0]});
            }catch{messages.Clear();rules.Clear();}
        }
        public static string Translate(string value,bool english){
            if(!english||String.IsNullOrEmpty(value))return value;string translated;if(messages.TryGetValue(value,out translated))return translated;
            foreach(var rule in rules){var match=rule.regex.Match(value);if(!match.Success)continue;return Regex.Replace(rule.replacement,@"\$(\d+)",placeholder=>{int index=Int32.Parse(placeholder.Groups[1].Value);string group=match.Groups[index].Value;return rule.translate.Contains(index)&&messages.TryGetValue(group,out translated)?translated:group;});}
            return value;
        }
    }
}
