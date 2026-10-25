using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Windows.Forms;

namespace kok {
    public partial class MainWindow {
        object ExportReadingNotes(Dictionary<string,object> args){
            string text=StringArg(args,"text");if(text.Length>2*1024*1024)throw new InvalidDataException("导出内容过大。");
            string name=StringArg(args,"name");foreach(char c in Path.GetInvalidFileNameChars())name=name.Replace(c,'_');
            using(var dialog=new SaveFileDialog{Title=L("导出笔记与书签"),Filter=L("Markdown 笔记|*.md"),FileName=name+"-notes.md"}){
                if(dialog.ShowDialog(this)!=DialogResult.OK)return false;
                EnsureExportDestination(dialog.FileName);File.WriteAllText(dialog.FileName,text,new UTF8Encoding(false));return true;
            }
        }
    }
}
