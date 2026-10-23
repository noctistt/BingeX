using System.Net;
using System.Text;
using System.Text.Json;
using BingeX;
internal static class MusicLyricsChecks
{
    private sealed class Handler:HttpMessageHandler
    {
        public Uri? Requested;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        {
            Requested=request.RequestUri;token.ThrowIfCancellationRequested();
            var result=new[]{new MusicLyricMatch(1,"Test Song","Test Artist","Test Album",120,false,"First\nSecond","[00:01.00]First\n[00:03.00]Second"),new MusicLyricMatch(2,"Test Song (Remix)","Test Artist","",140,false,"Remix",null)};
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(JsonSerializer.Serialize(result),Encoding.UTF8,"application/json")});
        }
    }
    public static int Live()
    {
        try{using var client=new MusicLyricsClient();var matches=client.Search("Never Gonna Give You Up","Rick Astley","",213,CancellationToken.None).GetAwaiter().GetResult();if(!matches.Any(x=>!string.IsNullOrWhiteSpace(x.SyncedLyrics)))return 1;Console.WriteLine(JsonSerializer.Serialize(new{result="pass",service="LRCLIB",matches=matches.Count,synchronizedLyricsAvailable=true,scope="metadata API; no lyric text or playback URLs retained"}));return 0;}catch(Exception e){Console.WriteLine("FAIL live lyrics: "+e.GetType().Name);return 1;}
    }
    public static int Run()
    {
        void Check(bool ok,string message){if(!ok)throw new Exception(message);}
        try{
            var lyrics=MusicLyrics.Parse("[ar:Fixture]\n[offset:100]\n[00:01.20][00:04.000]First\n[00:02.50]Second\n[00:02.50]Translation\n[00:06.000]\n[00:08.000]First");
            Check(lyrics.Lines.Length==5&&lyrics.Lines[0].Time==1100,"Offsets/multiple stamps must parse");
            Check(lyrics.Lines[1].Text=="Second\nTranslation","Simultaneous lines must combine");
            Check(lyrics.Active(1000,0)==-1&&lyrics.Active(1200,0)==0,"Before/after cue boundaries failed");
            Check(lyrics.Active(3000,1000)==0&&lyrics.Active(3000,-1000)==2,"Timing adjustment sign failed");
            Check(lyrics.Active(5000,0)==2&&lyrics.Active(1200,0)==0,"Backward seek must restore earlier line");
            Check(lyrics.Lines[^1].Text=="First"&&lyrics.Lines[^2].Text=="","Repeated lines and timed clearing must survive");
            Check(MusicLyrics.Parse("Plain lyrics\nSecond line").Lines.Length==0,"Plain lyrics must remain unsynced");
            using var handler=new Handler();using var client=new MusicLyricsClient(handler);
            var matches=client.Search("Test Song","Test Artist","Test Album",120,CancellationToken.None).GetAwaiter().GetResult();
            Check(matches.Count==2&&MusicLyricsClient.Exact(matches[0],"Test Song","Test Artist","Test Album",120),"Exact match ordering failed");
            Check(!MusicLyricsClient.Exact(matches[1],"Test Song","Test Artist","",120),"Remix must not auto-match");
            Check(!MusicLyricsClient.Exact(matches[0],"Test Song","Uploader","",120)&&!MusicLyricsClient.Exact(matches[0],"Test Song","Test Artist","",0),"Missing metadata must require manual selection");
            Check(handler.Requested?.Host=="lrclib.net"&&!handler.Requested.Query.Contains("http"),"Lyrics query must only send track metadata");
            using var cancel=new CancellationTokenSource();cancel.Cancel();var cancelled=false;try{client.Search("Test Song","Test Artist","",120,cancel.Token).GetAwaiter().GetResult();}catch(OperationCanceledException){cancelled=true;}Check(cancelled,"Lyrics cancellation failed");
            var bounded=false;try{MusicLyrics.Parse(new string('x',256001));}catch(InvalidDataException){bounded=true;}Check(bounded,"Lyrics file size must be bounded");
            Console.WriteLine("PASS: LRC timestamps/offsets, simultaneous and repeated lines, timed clearing, plain fallback, seek/delay positions, exact metadata matching, remix/uploader rejection, independent metadata-only lookup and cancellation.");return 0;
        }catch(Exception e){Console.WriteLine("FAIL: "+e.GetType().Name+" / "+e.Message);return 1;}
    }
}
