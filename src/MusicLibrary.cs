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

namespace kok {
    public class AudioDetails {
        public string title="",albumArtist="",genre="",category="music",source="",recordingId="",releaseId="";
        public int year,track,channels,sampleRate,bitrate;
        public double duration;
        public bool manual;
    }
    public class MusicMatch {public string title,artist,album,recordingId,releaseId,date;public int score;public double duration;}
    public static class MusicTags {
        public static AudioDetails Read(string path,out string artist,out string album,out byte[] cover){
            artist="";album="";cover=null;var result=new AudioDetails();
            using(var file=TagLib.File.Create(path,TagLib.ReadStyle.Average)){
                var tag=file.Tag;result.title=tag.Title??"";artist=String.Join(" / ",tag.Performers??new string[0]);album=tag.Album??"";
                result.albumArtist=String.Join(" / ",tag.AlbumArtists??new string[0]);result.genre=String.Join(" / ",tag.Genres??new string[0]);result.year=(int)Math.Min(9999,tag.Year);result.track=(int)Math.Min(9999,tag.Track);
                result.duration=file.Properties.Duration.TotalSeconds;result.channels=file.Properties.AudioChannels;result.sampleRate=file.Properties.AudioSampleRate;result.bitrate=file.Properties.AudioBitrate;
                result.source=!String.IsNullOrWhiteSpace(result.title+artist+album)?"tags":"filename";
                var picture=(tag.Pictures??new TagLib.IPicture[0]).FirstOrDefault(p=>p.Type==TagLib.PictureType.FrontCover)??(tag.Pictures??new TagLib.IPicture[0]).FirstOrDefault();
                if(picture!=null&&picture.Data.Count<=8*1024*1024)cover=picture.Data.Data;
            }
            return result;
        }
    }
    public partial class Library {
        public bool ReadAudioTags(Entry entry,bool force=false){
            if(entry.kind!="music"||!File.Exists(entry.path))return false;if(entry.audio==null)entry.audio=new AudioDetails();bool preserve=entry.audio.manual&&!force;
            try{
                string artist,album;byte[] picture;var details=MusicTags.Read(entry.path,out artist,out album,out picture);
                if(String.IsNullOrWhiteSpace(details.title))details.title=entry.name;details.title=Limit(details.title,200);details.albumArtist=Limit(details.albumArtist,200);details.genre=Limit(details.genre,200);
                if(String.IsNullOrWhiteSpace(artist)){
                    var parts=entry.name.Split(new[]{" - "," – "},2,StringSplitOptions.None);
                    if(parts.Length==2&&parts[0].Length<100){artist=parts[0].Trim();if(details.source=="filename")details.title=parts[1].Trim();}
                }
                lock(gate){if(preserve){var previous=entry.audio;previous.duration=details.duration;previous.channels=details.channels;previous.sampleRate=details.sampleRate;previous.bitrate=details.bitrate;}else{entry.audio=details;if(!String.IsNullOrWhiteSpace(artist))entry.artist=Limit(artist,200);if(!String.IsNullOrWhiteSpace(album))entry.album=Limit(album,200);}}
                if(picture!=null)try{using(var input=new MemoryStream(picture))using(var image=Image.FromStream(input)){
                    if(image.Width>0&&image.Height>0&&(long)image.Width*image.Height<=80000000){double scale=Math.Min(1,600.0/Math.Max(image.Width,image.Height));using(var bitmap=new Bitmap(Math.Max(1,(int)(image.Width*scale)),Math.Max(1,(int)(image.Height*scale))))using(var g=Graphics.FromImage(bitmap))using(var output=new MemoryStream()){
                        g.Clear(Color.Black);g.DrawImage(image,0,0,bitmap.Width,bitmap.Height);bitmap.Save(output,System.Drawing.Imaging.ImageFormat.Jpeg);lock(gate){WriteThumbnail(ThumbnailPath(entry),entry,output.ToArray());entry.hasCover=true;entry.coverVersion=DateTime.UtcNow.Ticks.ToString();}
                    }}
                }}catch{ /* A malformed embedded picture must not prevent audio import. */ }
                return true;
            }catch{if(String.IsNullOrEmpty(entry.audio.title))entry.audio.title=entry.name;if(!preserve)entry.audio.source="unavailable";return false;}
        }
        public void UpdateAudio(Entry entry,Dictionary<string,object> args){
            lock(gate){
                var audio=entry.audio??new AudioDetails();string category=GetMusicString(args,"category",30);
                if(!new[]{"music","recording","podcast","effect","other"}.Contains(category))throw new InvalidDataException("请选择有效的音频类型。");
                audio.title=GetMusicString(args,"title",200);audio.albumArtist=GetMusicString(args,"albumArtist",200);audio.genre=GetMusicString(args,"genre",200);audio.category=category;audio.manual=true;audio.source=String.Equals(GetMusicString(args,"source",30),"musicbrainz",StringComparison.Ordinal)?"musicbrainz":"manual";
                audio.recordingId=MusicId(GetMusicString(args,"recordingId",36));audio.releaseId=MusicId(GetMusicString(args,"releaseId",36));
                int year=args.ContainsKey("year")?Convert.ToInt32(args["year"]):0,track=args.ContainsKey("track")?Convert.ToInt32(args["track"]):0;if(year<0||year>9999||track<0||track>9999)throw new InvalidDataException("年份或曲目编号无效。");audio.year=year;audio.track=track;
                entry.artist=GetMusicString(args,"artist",200);entry.album=GetMusicString(args,"album",200);entry.audio=audio;Save();
            }
        }
        static string GetMusicString(Dictionary<string,object> args,string name,int length){return Limit(args.ContainsKey(name)?Convert.ToString(args[name]):"",length);}
        static string MusicId(string value){Guid id;return Guid.TryParse(value,out id)?id.ToString():"";}
    }
    public sealed class MusicDirectory {
        static readonly SemaphoreSlim throttle=new SemaphoreSlim(1,1);static DateTime lastRequest=DateTime.MinValue;
        readonly Func<HttpMessageHandler> handlerFactory;
        public MusicDirectory(Func<HttpMessageHandler> factory=null){handlerFactory=factory;}
        static string Quote(string text){return "\""+new string((text??"").Where(c=>!Char.IsControl(c)).Take(200).ToArray()).Replace("\\","\\\\").Replace("\"","\\\"")+"\"";}
        public async Task<List<MusicMatch>> Search(string title,string artist,CancellationToken token){
            ServicePointManager.SecurityProtocol=SecurityProtocolType.SystemDefault;
            if(String.IsNullOrWhiteSpace(title)||title.Length>200||artist.Length>200)throw new InvalidDataException("请输入曲名后再匹配。");
            string query="recording:"+Quote(title);if(!String.IsNullOrWhiteSpace(artist)&&artist!="本地音乐")query+=" AND artist:"+Quote(artist);
            await throttle.WaitAsync(token);try{var delay=TimeSpan.FromSeconds(1.1)-(DateTime.UtcNow-lastRequest);if(delay>TimeSpan.Zero)await Task.Delay(delay,token);lastRequest=DateTime.UtcNow;
                using(var handler=handlerFactory==null?new HttpClientHandler{AllowAutoRedirect=false}:handlerFactory())using(var client=new HttpClient(handler))using(var request=new HttpRequestMessage(HttpMethod.Get,"https://musicbrainz.org/ws/2/recording/?fmt=json&limit=6&query="+Uri.EscapeDataString(query))){
                    client.Timeout=TimeSpan.FromSeconds(20);request.Headers.TryAddWithoutValidation("User-Agent","kok/0.7.2 (https://github.com/ChenShoukang/kok)");
                    using(var reply=await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,token)){
                        if(!reply.IsSuccessStatusCode)throw new InvalidDataException("音乐目录暂时不可用，请稍后重试。",new HttpRequestException("Catalogue HTTP "+(int)reply.StatusCode));using(var stream=await reply.Content.ReadAsStreamAsync())using(var buffer=new MemoryStream()){
                            byte[] bytes=new byte[8192];int count;while((count=await stream.ReadAsync(bytes,0,bytes.Length,token))>0){if(buffer.Length+count>1024*1024)throw new InvalidDataException("音乐目录返回内容过大。");buffer.Write(bytes,0,count);}return Parse(Encoding.UTF8.GetString(buffer.ToArray()));
                        }
                    }
                }
            }catch(HttpRequestException error){throw new InvalidDataException("无法连接音乐目录，请检查网络。",error);}finally{throttle.Release();}
        }
        public static List<MusicMatch> Parse(string json){
            var root=(Dictionary<string,object>)Library.Json.DeserializeObject(json);var result=new List<MusicMatch>();object raw;if(!root.TryGetValue("recordings",out raw))return result;
            foreach(Dictionary<string,object> row in ((object[])raw).Take(6)){
                Func<Dictionary<string,object>,string,string> get=(r,k)=>r.ContainsKey(k)?Convert.ToString(r[k]):"";string artist="";
                object credit;if(row.TryGetValue("artist-credit",out credit))artist=String.Concat(((object[])credit).OfType<Dictionary<string,object>>().Select(c=>get(c,"name")+get(c,"joinphrase")));
                object releases;var albums=row.TryGetValue("releases",out releases)?((object[])releases).OfType<Dictionary<string,object>>().Take(2).ToList():new List<Dictionary<string,object>>();if(albums.Count==0)albums.Add(new Dictionary<string,object>());
                int score;Int32.TryParse(get(row,"score"),out score);double duration;Double.TryParse(get(row,"length"),out duration);
                foreach(var album in albums)result.Add(new MusicMatch{title=get(row,"title"),artist=artist,album=get(album,"title"),date=get(album,"date"),recordingId=get(row,"id"),releaseId=get(album,"id"),score=score,duration=duration/1000});
            }return result.Take(10).ToList();
        }
    }
    public partial class MainWindow {
        static readonly string[] MusicActions={"readMusicTags","saveMusicInfo","lookupMusic","cancelMusicLookup","musicOrganizationInput"};
        readonly MusicDirectory musicDirectory=new MusicDirectory();
        CancellationTokenSource musicLookupCancellation;
        async Task<object> HandleMusicAction(string action,Dictionary<string,object> args){
            if(action=="cancelMusicLookup"){if(musicLookupCancellation!=null)musicLookupCancellation.Cancel();return true;}
            if(action=="lookupMusic"){library.RequireAiMode();if(musicLookupCancellation!=null)throw new InvalidOperationException("音乐信息正在匹配，请稍候。");using(var cancel=ModuleCancellation()){musicLookupCancellation=cancel;try{return await musicDirectory.Search(StringArg(args,"title"),StringArg(args,"artist"),cancel.Token);}finally{musicLookupCancellation=null;}}}
            if(action=="saveMusicInfo"){var item=RequireEntry(args);if(item.kind!="music")throw new InvalidDataException("请选择音频文件。");library.UpdateAudio(item,args);Notify("libraryChanged",null);return item;}
            var ids=Library.Json.Deserialize<List<string>>(Library.Json.Serialize(args["ids"]));if(ids==null||ids.Count<1||ids.Count>50||ids.Distinct().Count()!=ids.Count||ids.Any(id=>library.Find(id)==null||library.Find(id).kind!="music"))throw new InvalidDataException("每次请选择 1 至 50 个音频文件。");
            if(action=="musicOrganizationInput")return library.OrganizeInputs(ids,false);
            var owner=library;backgroundLibraryWrites++;try{int scanned=0;await Task.Run(()=>{foreach(var id in ids)if(owner.ReadAudioTags(owner.Find(id)))scanned++;owner.Save();});if(owner!=library)throw new InvalidOperationException("资料库已切换。");Notify("libraryChanged",null);return new{scanned=scanned};}finally{backgroundLibraryWrites--;}
        }
    }
}
