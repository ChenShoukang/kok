using System;
using System.IO;
using System.Globalization;
using System.Collections.Generic;
using System.Threading.Tasks;
namespace kok {
 public static class AgentCalculator {
  sealed class Parser {
   readonly string text;int at,depth;
   internal Parser(string text){this.text=text;}
   void Space(){while(at<text.Length&&Char.IsWhiteSpace(text[at]))at++;}
   decimal Factor(){Space();if(++depth>16)throw new FormatException();try{if(at<text.Length&&(text[at]=='+'||text[at]=='-')){bool minus=text[at++]=='-';return (minus?-1:1)*Factor();}if(at<text.Length&&text[at]=='('){at++;decimal n=Sum();Space();if(at>=text.Length||text[at++]!=')')throw new FormatException();return n;}int start=at;while(at<text.Length&&(Char.IsDigit(text[at])||text[at]=='.'))at++;decimal value;if(start==at||!Decimal.TryParse(text.Substring(start,at-start),NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out value))throw new FormatException();return value;}finally{depth--;}}
   decimal Product(){decimal n=Factor();while(true){Space();if(at>=text.Length||text[at]!='*'&&text[at]!='/')return n;char op=text[at++];decimal next=Factor();n=op=='*'?n*next:n/next;}}
   decimal Sum(){decimal n=Product();while(true){Space();if(at>=text.Length||text[at]!='+'&&text[at]!='-')return n;char op=text[at++];decimal next=Product();n=op=='+'?n+next:n-next;}}
   internal decimal Run(){decimal result=Sum();Space();if(at!=text.Length||Math.Abs(result)>100000000000000000000m)throw new FormatException();return result;}
  }
  public static string Evaluate(string expression){if(String.IsNullOrWhiteSpace(expression)||expression.Length>256)throw new InvalidDataException("Use a bounded arithmetic expression with numbers, +, -, *, / and parentheses.");try{return new Parser(expression).Run().ToString(CultureInfo.InvariantCulture);}catch(Exception e)when(e is FormatException||e is DivideByZeroException||e is OverflowException){throw new InvalidDataException("Invalid arithmetic expression or division by zero.");}}
  internal static AgentToolSpec Tool(){return new AgentToolSpec{Name="calculate",Title="计算数值",Requires=Need.NormalLibrary,Signature="calculate{expression}",Description="Calculate exact decimal arithmetic using numbers, + - * / and parentheses. Read-only and automatic. Use this for totals, differences and percentages instead of mental arithmetic.",Properties=AgentRules.Props("expression",AgentRules.Str("Bounded numeric expression",256)),Required=new[]{"expression"},Validate=(a,exists)=>{CapabilitySchema.Check(CapabilitySchema.Obj(AgentRules.Props("expression",AgentRules.Str("Expression",256)),new[]{"expression"}),a.args);},Read=(lib,state,call,token)=>Task.FromResult(new Dictionary<string,object>{{"text",Evaluate(ModuleContext.Text(call.args,"expression"))},{"calculated",true}})};}
 }
}
