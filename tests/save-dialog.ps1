param([Parameter(Mandatory=$true)][int]$ProcessId,[string]$Destination)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type @'
using System;
using System.Text;
using System.Collections.Generic;
using System.Runtime.InteropServices;
public static class FixtureSaveDialog {
 public delegate bool Visitor(IntPtr window,IntPtr data);
 [DllImport("user32.dll")] public static extern bool EnumChildWindows(IntPtr window,Visitor visitor,IntPtr data);
 [DllImport("user32.dll")] public static extern int GetDlgCtrlID(IntPtr window);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern int GetClassName(IntPtr window,StringBuilder name,int length);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern IntPtr SendMessage(IntPtr window,int message,IntPtr param,string text);
 [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr window,int message,IntPtr param,IntPtr data);
 public static bool Save(IntPtr dialog,string destination) {
  IntPtr field=IntPtr.Zero,save=IntPtr.Zero;
  EnumChildWindows(dialog,(window,data)=>{var name=new StringBuilder(256);GetClassName(window,name,256);int id=GetDlgCtrlID(window);
   if(name.ToString()=="Edit"&&(id==1152||id==1001))field=window;
   if(name.ToString()=="Button"&&id==1)save=window;
   return true;
  },IntPtr.Zero);
  if(field==IntPtr.Zero||save==IntPtr.Zero)return false;
  SendMessage(field,12,IntPtr.Zero,destination);
  PostMessage(save,245,IntPtr.Zero,IntPtr.Zero);
  return true;
 }
}
'@
$root=[System.Windows.Automation.AutomationElement]::RootElement
$condition=New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty,$ProcessId)
$deadline=[DateTime]::UtcNow.AddSeconds(20)
do {
    $windows=$root.FindAll([System.Windows.Automation.TreeScope]::Children,$condition)
    foreach($window in $windows){
        if($window.Current.ClassName -ne '#32770'){continue}
        if(!$Destination){
            Write-Output 'Cancelled through the isolated native dialog.'
            $window.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).Close()
            exit 0
        }
        if([FixtureSaveDialog]::Save([IntPtr]$window.Current.NativeWindowHandle,$Destination)){Write-Output 'Saved through the isolated native dialog.';exit 0}
        $edits=$window.FindAll([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.Condition]::TrueCondition)
        foreach($edit in $edits){
            if($edit.Current.AutomationId -notin @('1001','1148','1152','FileNameControlHost')){continue}
            $value=$null
            if(!$edit.TryGetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern,[ref]$value)){continue}
            $value.SetValue($Destination)
            $save=$window.FindFirst([System.Windows.Automation.TreeScope]::Descendants,(New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::AutomationIdProperty,'1')))
            if($save){Write-Output ('Save dialog: '+$window.Current.Name);$save.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke();exit 0}
        }
    }
    Start-Sleep -Milliseconds 100
} while([DateTime]::UtcNow -lt $deadline)
throw 'Could not find the isolated app save dialog and filename field.'
