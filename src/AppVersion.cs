using System;
using System.Reflection;
namespace kok {
 public static class AppVersion {
  public static readonly string Current=((AssemblyInformationalVersionAttribute)Attribute.GetCustomAttribute(typeof(AppVersion).Assembly,typeof(AssemblyInformationalVersionAttribute))).InformationalVersion;
 }
}
