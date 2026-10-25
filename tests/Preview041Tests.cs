using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using kok;

static class Preview041Tests {
    [DllImport("shell32.dll",SetLastError=true,CharSet=CharSet.Unicode)]static extern IntPtr CommandLineToArgvW(string command,out int count);
    [DllImport("kernel32.dll")]static extern IntPtr LocalFree(IntPtr pointer);
    public static void Run(string directory,Action<bool,string> check){
        Directory.CreateDirectory(directory);string original=Path.Combine(directory,"reading.txt");File.WriteAllText(original,"Reading data remains local.",Encoding.UTF8);
        var library=new Library(Path.Combine(directory,"data"));library.Import(new[]{original},null);var item=library.Db.items[0];
        library.Settings(new Dictionary<string,object>{{"customColorsEnabled",true},{"customLightBg","#abcDef"},{"customDarkGlass","#202124"},{"glassOpacity",55},{"glassBlur",28},{"readerLineHeight",1.65},{"readerLayout","paged"}});
        var reopened=new Library(library.DataDir);check(Convert.ToString(reopened.Db.settings["customLightBg"])=="#abcdef"&&Convert.ToString(reopened.Db.settings["readerLayout"])=="paged","custom colors and reading preferences persist independently of source files");
        bool invalid=false;try{library.Settings(new Dictionary<string,object>{{"customLightBg","#ffffff"},{"glassOpacity",200}});}catch(InvalidDataException){invalid=true;}
        check(invalid&&Convert.ToString(new Library(library.DataDir).Db.settings["customLightBg"])=="#abcdef","invalid appearance batches do not persist partial changes");
        library.Update(item.id,new Dictionary<string,object>{{"bookmarks",new[]{new ReadingBookmark{id="local-bookmark",name="A thought",chapter=1,position=.375}}}});
        var bookmark=new Library(library.DataDir).Find(item.id).bookmarks.Single();check(bookmark.chapter==1&&bookmark.position==.375&&File.ReadAllText(original)=="Reading data remains local.","bookmarks survive restart without modifying original reading content");
        bool badBookmark=false;try{library.Update(item.id,new Dictionary<string,object>{{"bookmarks",new[]{new ReadingBookmark{id="bad",chapter=-1}}}});}catch(InvalidDataException){badBookmark=true;}
        check(badBookmark&&new Library(library.DataDir).Find(item.id).bookmarks.Count==1,"invalid bookmarks cannot replace saved locations");
        int rejected=0;foreach(var url in new[]{"file:///C:/private.txt","ftp://example.org/file","https://user:password@example.org/video","--exec calc.exe"})try{DownloadManager.ValidateUrl(url);}catch(InvalidDataException){rejected++;}
        check(rejected==4&&DownloadManager.ValidateUrl("https://example.org/video?a=1&b=2").EndsWith("?a=1&b=2"),"download URLs accept HTTP(S) and reject local paths, credentials and command options");
        var values=new[]{"https://example.org/?x=1&y=2","a path ending in \\","embedded \" quotes","中文 folder",""};
        int count;IntPtr argv=CommandLineToArgvW("kok.exe "+String.Join(" ",values.Select(DownloadTools.QuoteArgument)),out count);
        try{check(argv!=IntPtr.Zero&&count==values.Length+1&&values.Select((value,n)=>Marshal.PtrToStringUni(Marshal.ReadIntPtr(argv,(n+1)*IntPtr.Size))==value).All(x=>x),"Windows argument parsing preserves quotes, Unicode, trailing slashes and shell characters as data");}finally{if(argv!=IntPtr.Zero)LocalFree(argv);}
        var job=new VideoDownload();DownloadManager.RecordProgress(job,4294967296L,8589934592L,1048576L);
        check(job.received==4294967296L&&job.total==8589934592L&&job.progress==50,"download progress keeps 64-bit byte counts for large files");
        bool badQuality=false;try{DownloadManager.ValidateQuality("arbitrary-command");}catch(InvalidDataException){badQuality=true;}check(badQuality&&DownloadManager.ValidateQuality("1080")=="1080"&&DownloadManager.ValidateQuality("audio")=="audio","native download settings accept quality and audio options, and reject arbitrary commands");
        check(DownloadManager.IsInside(Path.Combine(directory,"out","file.webm"),Path.Combine(directory,"out"))&&!DownloadManager.IsInside(Path.Combine(directory,"outside","file.webm"),Path.Combine(directory,"out")),"download result paths remain inside the selected job folder");
    }
}
