using BingeX;
internal static class SourceAudioChecks
{
    public static void Run()
    {
        StreamChoice Source(string name,string text="")=>new("Fixture",name,text,"https://example.invalid/video",null);
        void Check(bool value,string message){if(!value)throw new Exception(message);}
        Check(SourceAudioInfo.Parse(Source("1080p HEVC","English subtitles available")).Kind==SourceAudioKind.Unknown,"Quality or subtitle language cannot prove spoken audio type.");
        Check(SourceAudioInfo.Parse(Source("[SUB] 1080p")).Kind==SourceAudioKind.Subbed,"Explicit SUB labels are retained.");
        var dub=SourceAudioInfo.Parse(Source("Hindi Dubbed 720p"));
        Check(dub.Kind==SourceAudioKind.Dubbed && dub.Languages.Contains("hindi"),"Explicit dub audio language is displayed.");
        Check(SourceAudioInfo.Parse(Source("[DUB] English")).Languages.Contains("english"),"Dub labels can name the spoken language after the marker.");
        Check(SourceAudioInfo.Parse(Source("Dual-Audio 1080p")).Kind==SourceAudioKind.DualAudio,"Explicit dual audio labels are retained.");
        var multiple=Source("1080p") with{AudioOptions=[new("https://example.invalid/ja","Japanese","ja",true),new("https://example.invalid/en","English","en",false)]};
        Check(SourceAudioInfo.Parse(multiple).Kind==SourceAudioKind.DualAudio,"Structured audio alternatives identify multi-language audio.");
        Check(SourceAudioInfo.Parse(Source("Sub / Dub")).Kind==SourceAudioKind.Unknown,"Conflicting labels do not prove a dual-audio file.");
        var selection=new AudioTrackSelection(-3,"English old label","english");
        Check(selection.Resolve([(8,"Japanese"),(9,"English stereo")])==9,"Show language resolves a new native track ID and label.");
        Check(selection.Resolve([(8,"Japanese")])==null,"A missing preferred language does not select a different language.");
        Check(selection.Resolve([(8,"English stereo"),(9,"English commentary")])==null,"Ambiguous language matches are not guessed.");
        var folder=Path.Combine(Path.GetTempPath(),"BingeX-show-audio-"+Guid.NewGuid());
        var episode=new MediaContext("anime-show","series","Show","","fixture","episode1");
        try
        {
            using(var store=new LibraryStore(folder))store.SaveShowAudioPreference(episode,new("hindi","Hindi","Dubbed"));
            using(var store=new LibraryStore(folder))
            {
                var saved=store.ShowAudioPreference(episode with{EpisodeId="episode2"});
                Check(saved.Language=="hindi" && saved.SourceFilter=="Dubbed","Show audio and source filter persist across episodes and restarts.");
                Check(store.ShowAudioPreference(episode with{Id="other"}).Language=="","Audio preferences are scoped to the show.");
            }
        }
        finally{Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();Directory.Delete(folder,true);}
        Console.WriteLine("PASS: explicit sub/dub/dual labels, language evidence, unknown/conflicting sources, per-show persistence and safe native language matching.");
    }
}
