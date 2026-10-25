using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using kok;
using YoutubeExplode;

class SiteAdapterChecks {
    static void Check(bool value,string message){if(!value)throw new Exception(message);Console.WriteLine("PASS: "+message);}
    sealed class Fixture : HttpMessageHandler {
        public readonly string player;
        public Fixture(){player=Library.Json.Serialize(new{playabilityStatus=new{status="OK"},videoDetails=new{videoId="abcdefghijk",title="Original fixture",author="kok",channelId="UCaaaaaaaaaaaaaaaaaaaaaa",lengthSeconds="4",viewCount="1",shortDescription="Local original fixture",isLiveContent=false,thumbnail=new{thumbnails=new[]{new{url="https://unit.test/cover.jpg",width=160,height=90}}}},microformat=new{playerMicroformatRenderer=new{uploadDate="2026-10-20",publishDate="2026-10-20"}},responseContext=new{visitorData="unit-visitor"},streamingData=new{expiresInSeconds="3600",adaptiveFormats=new object[]{new{itag=137,url="https://unit.googlevideo.com/videoplayback?itag=137",mimeType="video/mp4; codecs=\"avc1.42001E\"",bitrate=100000,width=160,height=90,quality="hd1080",qualityLabel="1080p",fps=12,contentLength="2048",approxDurationMs="4000"},new{itag=140,url="https://unit.googlevideo.com/videoplayback?itag=140",mimeType="audio/mp4; codecs=\"mp4a.40.2\"",bitrate=64000,audioSampleRate="48000",audioChannels=1,contentLength="1024",approxDurationMs="4000"}}},captions=new{playerCaptionsTracklistRenderer=new{captionTracks=new object[0]}}});}
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token){
            token.ThrowIfCancellationRequested();var response=new HttpResponseMessage(HttpStatusCode.OK){RequestMessage=request};string url=request.RequestUri.AbsoluteUri;
            if(url.Contains("googlevideo.com")){response.Content=new ByteArrayContent(new byte[]{0,0});return Task.FromResult(response);}
            if(url.Contains("sw.js_data")){var data=new object[14];data[13]="unit-visitor";response.Content=new StringContent(Library.Json.Serialize(new object[]{new object[]{null,null,new object[]{new object[]{data}}}}),Encoding.UTF8,"application/json");return Task.FromResult(response);}
            if(url.Contains("watch?"))response.Content=new StringContent("<html><head><meta property=\"og:url\" content=\"https://www.youtube.com/watch?v=abcdefghijk\"><meta itemprop=\"datePublished\" content=\"2026-10-20\"></head><body><div id=\"player\"></div><script>var ytInitialPlayerResponse = "+player+";</script></body></html>",Encoding.UTF8,"text/html");
            else response.Content=new StringContent(player,Encoding.UTF8,"application/json");return Task.FromResult(response);
        }
    }
    static void Main(string[] args){try{Run(args[0]).GetAwaiter().GetResult();}catch(Exception error){Console.Error.WriteLine(error.ToString());Environment.ExitCode=1;}}
    static async Task Run(string directory){
        Directory.CreateDirectory(directory);var sessions=new DownloadSessions(directory);
        sessions.Import("bilibili","# Netscape HTTP Cookie File\n.bilibili.com\tTRUE\t/\tTRUE\t4102444800\tSESSDATA\tunit-cookie-only\n.youtube.com\tTRUE\t/\tTRUE\t4102444800\tSID\tshould-not-import\n");
        Check(sessions.Jar("bilibili").GetCookieHeader(new Uri("https://www.bilibili.com/")).Contains("unit-cookie-only"),"site cookies restore into their own domain");
        Check(!sessions.Jar("bilibili").GetCookieHeader(new Uri("https://www.youtube.com/")).Contains("should-not-import"),"cookie import discards other sites");
        Check(!Encoding.UTF8.GetString(File.ReadAllBytes(Path.Combine(directory,"download-accounts.sealed"))).Contains("unit-cookie-only")&&!Library.Json.Serialize(sessions.Status()).Contains("unit-cookie-only"),"stored sessions are encrypted and UI snapshots contain no cookie values");
        Check(!DownloadSessions.CookieDomain("bilibili","bilibili.com.evil.test")&&!DownloadSessions.LoginUrl("youtube","https://youtube.com.evil.test/")&&!DownloadSessions.LoginUrl("youtube","file:///C:/private")&&DownloadSessions.LoginUrl("youtube","https://accounts.google.com/"),"site and login host validation rejects suffix tricks and local files");
        var reopened=new DownloadSessions(directory);Check(reopened.Jar("bilibili").GetCookieHeader(new Uri("https://www.bilibili.com/")).Contains("unit-cookie-only"),"encrypted sessions survive reopening");reopened.Clear("bilibili");Check(reopened.Jar("bilibili").Count==0,"clearing removes saved credentials");
        Check(MediaResolver.NormalizeInput("复制打开抖音 https://v.douyin.com/abc123/ ").StartsWith("https://v.douyin.com/abc123/"),"share text normalizes into a validated URL");
        string signed=SiteAdapters.SignBili(new Dictionary<string,string>{{"foo","114"},{"bar","514"},{"baz","1919810"}},"7cd084941338484aae1ad9425b84077c4932c2eec244b83a348a9b18512cc89c",1702204169);Check(signed.StartsWith("bar=514&baz=1919810&foo=114&wts=1702204169&w_rid=")&&signed.Length>80,"WBI signing uses canonical ordered parameters and a digest");
        using(var http=new HttpClient(new Fixture()))using(var youtube=new YoutubeClient(http)){
            var method=typeof(YoutubeSite).GetMethod("ResolveClient",BindingFlags.Static|BindingFlags.NonPublic);var task=(Task<MediaPage>)method.Invoke(null,new object[]{youtube,"https://www.youtube.com/watch?v=abcdefghijk",CancellationToken.None});var timeout=Task.Delay(15000);if(await Task.WhenAny(task,timeout)!=task)throw new Exception("YouTube fixture timed out");var page=await task;
            Check(page.provider=="youtube"&&page.title=="Original fixture"&&page.candidates.Any(c=>c.kind=="video")&&page.candidates.Any(c=>c.kind=="music"),"real YoutubeExplode library resolves split streams against an offline HTTP fixture");
        }
    }
}
