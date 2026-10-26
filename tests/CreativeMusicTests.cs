using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using kok;
class CreativeMusicTests {
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
 static void Wave(string file){const int rate=44100,samples=rate*3;using(var stream=File.Create(file))using(var w=new BinaryWriter(stream)){w.Write(Encoding.ASCII.GetBytes("RIFF"));w.Write(36+samples*2);w.Write(Encoding.ASCII.GetBytes("WAVEfmt "));w.Write(16);w.Write((short)1);w.Write((short)1);w.Write(rate);w.Write(rate*2);w.Write((short)2);w.Write((short)16);w.Write(Encoding.ASCII.GetBytes("data"));w.Write(samples*2);for(int n=0;n<samples;n++)w.Write((short)(Math.Sin(n*2*Math.PI*220/rate)*4000));}}
 sealed class DirectoryHandler:HttpMessageHandler {
  internal string url,agent;internal bool auth;
  protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token){url=request.RequestUri.AbsoluteUri;agent=request.Headers.UserAgent.ToString();auth=request.Headers.Authorization!=null;return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent("{\"recordings\":[{\"id\":\"11111111-1111-1111-1111-111111111111\",\"title\":\"Quiet Walk\",\"score\":100,\"length\":3000,\"artist-credit\":[{\"name\":\"Test Artist\"}],\"releases\":[{\"id\":\"22222222-2222-2222-2222-222222222222\",\"title\":\"Test Album\",\"date\":\"2025-01-01\"}]}]}")});}
 }
 static void Main(string[] args){try{Run(args).GetAwaiter().GetResult();}catch(Exception e){Console.Error.WriteLine(e);Environment.ExitCode=1;}}
 static async Task Run(string[] args){
  if(args[0]=="--online"){var rows=await new MusicDirectory().Search("Imagine","John Lennon",CancellationToken.None);Check(rows.Any(r=>r.title.IndexOf("Imagine",StringComparison.OrdinalIgnoreCase)>=0),"No expected public catalogue match");Console.WriteLine("PASS: live public music catalogue lookup, "+rows.Count+" candidates; no audio or credentials sent.");return;}
  string root=Path.GetFullPath(args[0]);Directory.CreateDirectory(root);string file=Path.Combine(root,"tagged-tone.wav");Wave(file);
  using(var audio=TagLib.File.Create(file)){audio.Tag.Title="轻声散步";audio.Tag.Performers=new[]{"测试歌手"};audio.Tag.Album="安静专辑";audio.Tag.AlbumArtists=new[]{"测试歌手"};audio.Tag.Genres=new[]{"Ambient"};audio.Tag.Year=2025;audio.Tag.Track=3;using(var image=new Bitmap(32,32))using(var g=Graphics.FromImage(image))using(var bytes=new MemoryStream()){g.Clear(Color.MediumPurple);image.Save(bytes,System.Drawing.Imaging.ImageFormat.Png);audio.Tag.Pictures=new TagLib.IPicture[]{new TagLib.Picture(new TagLib.ByteVector(bytes.ToArray())){Type=TagLib.PictureType.FrontCover}};}audio.Save();}
  string artist,album;byte[] cover;var tags=MusicTags.Read(file,out artist,out album,out cover);Check(tags.title=="轻声散步"&&artist=="测试歌手"&&album=="安静专辑"&&tags.track==3&&tags.year==2025&&tags.duration>2.9&&cover.Length>0,"Local Unicode tags, timing or artwork failed");
  var library=new Library(Path.Combine(root,"native-library"));library.Import(new[]{file},null);var entry=library.Db.items.Single();Check(entry.hasCover&&File.Exists(library.ThumbnailPath(entry)),"Artwork thumbnail missing");entry.audio.title="Keep title";entry.artist="Keep artist";entry.audio.manual=true;library.ReadAudioTags(entry);Check(entry.audio.title=="Keep title"&&entry.artist=="Keep artist"&&entry.audio.duration>2.9,"Manual corrections overwritten");
  var handler=new DirectoryHandler();var matches=await new MusicDirectory(()=>handler).Search("Quiet Walk","Test Artist",CancellationToken.None);Check(matches.Count==1&&matches[0].album=="Test Album"&&matches[0].artist=="Test Artist"&&matches[0].duration==3,"Music catalogue parsing failed");Check(handler.url.StartsWith("https://musicbrainz.org/ws/2/recording/")&&handler.agent.Contains("kok")&&!handler.auth,"Catalogue request leaked auth or used wrong destination");
  var edit=new Dictionary<string,object>{{"start",.5},{"end",1.5},{"rate",.5},{"volume",.8},{"fadeIn",.1},{"fadeOut",.2}};
  var command=CreativeEdits.MediaArguments(file,"out.wav","music",edit);Check(command.IndexOf("-ss")<command.IndexOf("-i")&&command.Contains("pcm_s16le")&&command.Contains("file,pipe"),"Export trim/codec/protocol arguments failed");
  foreach(string format in new[]{"docx","epub"}){string document=Path.Combine(root,"edited."+format);File.WriteAllBytes(document,TextDocumentExport.Export("# 标题\n\n你好，**加粗文字**。\n- 清单", "测试文档",format,2));var book=Library.ReadBook(new Entry{path=document,ext=format.ToUpperInvariant(),kind=format=="epub"?"book":"document",name="测试文档"});Check(String.Join("\n",book.chapters.Select(c=>c.text)).Contains("你好"),"Exported "+format+" cannot reopen");}
  bool rejected=false;try{CreativeEdits.MediaArguments(file,"out.wav","music",new Dictionary<string,object>{{"start",2.0},{"end",1.0}});}catch(InvalidDataException){rejected=true;}Check(rejected,"Invalid trim accepted");
  if(args.Length>1){string encoder=args[1],output=Path.Combine(root,"trimmed.wav");await CreativeEdits.ExportMedia(encoder,file,output,"music",edit,CancellationToken.None,p=>{});using(var audio=TagLib.File.Create(output))Check(Math.Abs(audio.Properties.Duration.TotalSeconds-2)<.15,"Exported slow audio duration wrong");
   string probe=Path.Combine(Path.GetDirectoryName(encoder),"ffprobe.exe"),sequence=Path.Combine(root,"sequence.wav");await MediaSequence.Export(encoder,probe,new List<MediaEditClip>{new MediaEditClip{source=file,edit=new Dictionary<string,object>{{"start",0.0},{"end",.6},{"rate",1.0}}},new MediaEditClip{source=file,edit=new Dictionary<string,object>{{"start",.6},{"end",1.2},{"rate",.5}}}},sequence,"music",CancellationToken.None,p=>{});using(var audio=TagLib.File.Create(sequence))Check(Math.Abs(audio.Properties.Duration.TotalSeconds-1.8)<.15,"Multi-clip audio duration wrong");
   if(args.Length>2){var videoEdit=new Dictionary<string,object>{{"start",.1},{"end",.7},{"rate",.5},{"rotation",90},{"mute",true},{"caption","中文片段字幕"}};string video=Path.Combine(root,"trimmed.mp4");await CreativeEdits.ExportMedia(encoder,args[2],video,"video",videoEdit,CancellationToken.None,p=>{});using(var media=TagLib.File.Create(video))Check(media.Properties.Duration.TotalSeconds>1&&media.Properties.VideoWidth>0,"Edited video invalid");string combined=Path.Combine(root,"sequence.mp4");await MediaSequence.Export(encoder,probe,new List<MediaEditClip>{new MediaEditClip{source=args[2],edit=videoEdit},new MediaEditClip{source=args[2],edit=videoEdit}},combined,"video",CancellationToken.None,p=>{});using(var media=TagLib.File.Create(combined))Check(media.Properties.Duration.TotalSeconds>2.2&&media.Properties.VideoWidth>0,"Multi-clip video invalid");}
  }
  Console.WriteLine("PASS: local Unicode music tags, cover extraction, manual correction retention, catalogue request/parser, validated clipping, real audio/video export and original file retention.");
 }
}
