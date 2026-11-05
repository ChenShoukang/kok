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
 [DllImport("user32.dll")] public static extern IntPtr GetParent(IntPtr window);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern int GetClassName(IntPtr window,StringBuilder name,int length);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern IntPtr SendMessage(IntPtr window,int message,IntPtr param,string text);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern IntPtr SendMessage(IntPtr window,int message,IntPtr param,StringBuilder text);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern IntPtr SendMessage(IntPtr window,int message,IntPtr param,IntPtr data);
 [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr window,int message,IntPtr param,IntPtr data);
 public static string Describe(IntPtr dialog) {
  var controls=new List<string>();
  EnumChildWindows(dialog,(window,data)=>{
   var name=new StringBuilder(256);GetClassName(window,name,256);
   if(name.ToString()=="Edit"||name.ToString().Contains("ComboBox"))
    controls.Add(name+":"+GetDlgCtrlID(window)+" parent="+GetDlgCtrlID(GetParent(window))+" grandparent="+GetDlgCtrlID(GetParent(GetParent(window))));
   return true;
  },IntPtr.Zero);
  return String.Join(", ",controls);
 }
 public static bool Save(IntPtr dialog,string destination) {
  IntPtr field=IntPtr.Zero,save=IntPtr.Zero;
  EnumChildWindows(dialog,(window,data)=>{var name=new StringBuilder(256);GetClassName(window,name,256);int id=GetDlgCtrlID(window);
   if(name.ToString()=="Edit"&&(id==1152||(id==1001&&field==IntPtr.Zero)))field=window;
   if(name.ToString()=="Button"&&id==1)save=window;
   return true;
  },IntPtr.Zero);
  if(field==IntPtr.Zero||save==IntPtr.Zero)return false;
  const int SelectText=0x00B1,ReplaceSelection=0x00C2,ReadText=0x000D,ClickButton=0x00F5;
  // The shell dialog can keep its original filename when only WM_SETTEXT is sent.
  SendMessage(field,SelectText,IntPtr.Zero,new IntPtr(-1));
  SendMessage(field,ReplaceSelection,new IntPtr(1),destination);
  var current=new StringBuilder(destination.Length+2);
  SendMessage(field,ReadText,(IntPtr)current.Capacity,current);
  return String.Equals(current.ToString(),destination,StringComparison.Ordinal)
   &&PostMessage(save,ClickButton,IntPtr.Zero,IntPtr.Zero);
 }
}
'@
$root=[System.Windows.Automation.AutomationElement]::RootElement
$condition=New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty,$ProcessId)
$fileNameHost=New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::AutomationIdProperty,'FileNameControlHost')
$editControl=New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty,[System.Windows.Automation.ControlType]::Edit)
$saveButton=New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::AutomationIdProperty,'1')
$deadline=[DateTime]::UtcNow.AddSeconds(20)
$observed=''
do {
    $windows=$root.FindAll([System.Windows.Automation.TreeScope]::Children,$condition)
    foreach($window in $windows){
        if($window.Current.ClassName -ne '#32770'){continue}
        if(!$Destination){
            Write-Output 'Cancelled through the isolated native dialog.'
            $window.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).Close()
            exit 0
        }
        # Target the filename control rather than another edit field such as the search box.
        $fileName=$window.FindFirst([System.Windows.Automation.TreeScope]::Descendants,$fileNameHost)
        $save=$window.FindFirst([System.Windows.Automation.TreeScope]::Descendants,$saveButton)
        $invoke=$null
        if($fileName -and $save -and $save.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern,[ref]$invoke)){
            $edits=@($fileName.FindAll([System.Windows.Automation.TreeScope]::Descendants,$editControl))
            foreach($edit in @($edits)+@($fileName)){
                $value=$null
                if(!$edit.TryGetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern,[ref]$value) -or $value.Current.IsReadOnly){continue}
                $value.SetValue($Destination)
                if($value.Current.Value -cne $Destination){continue}
                $invoke.Invoke()
                Write-Output 'Submitted the verified filename through Windows UI Automation.'
                exit 0
            }
        }
        if([FixtureSaveDialog]::Save([IntPtr]$window.Current.NativeWindowHandle,$Destination)){Write-Output 'Submitted the verified filename through the classic Windows dialog.';exit 0}
        if(!$observed){
            $observed=[FixtureSaveDialog]::Describe([IntPtr]$window.Current.NativeWindowHandle)
            $observed+='; UI Automation edits: '+(($window.FindAll([System.Windows.Automation.TreeScope]::Descendants,$editControl)|ForEach-Object { $_.Current.AutomationId }) -join ', ')
        }
    }
    Start-Sleep -Milliseconds 100
} while([DateTime]::UtcNow -lt $deadline)
throw ('Could not submit the isolated app save dialog. Observed controls: '+$observed)
