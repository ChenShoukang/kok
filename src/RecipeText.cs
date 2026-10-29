using System;
namespace kok {
 internal static class RecipeText {
  internal static string Excerpt(string text){if(text.Length<=40000)return text;const int segment=13000;return text.Substring(0,segment)+"\n[... omitted source text ...]\n"+text.Substring(Math.Max(segment,text.Length/2-segment/2),segment)+"\n[... omitted source text ...]\n"+text.Substring(text.Length-segment);}
 }
}
