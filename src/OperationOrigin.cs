using System.Threading;
namespace kok {
 internal static class OperationOrigin {
  static readonly AsyncLocal<string> source=new AsyncLocal<string>(),id=new AsyncLocal<string>();
  public static string Source{get{return source.Value;}}public static string Id{get{return id.Value;}}
  public static System.IDisposable Use(string origin,string operation){string before=source.Value,previous=id.Value;source.Value=origin;id.Value=operation;return new ModuleLease(()=>{source.Value=before;id.Value=previous;});}
 }
}
