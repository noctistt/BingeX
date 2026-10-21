using BingeX;
internal static class EpisodeNavigationChecks
{
    public static void Run()
    {
        EpisodeInfo[] entries=[new("s2e1","Next season",2,1,""),new("s1e2","Second",1,2,""),new("s1e1","First",1,1,"")];
        var title=new TitleInfo("show","series","Show","","","","","","","provider",entries);
        MediaContext Context(string id)=>new("show","series","Show","","provider",id);
        void Check(bool value,string message) { if(!value)throw new Exception(message); }
        Check(EpisodeNavigation.Next(title,Context("s1e1"))?.Id=="s1e2","Episodes use verified numeric order.");
        Check(EpisodeNavigation.Next(title,Context("s1e2"))?.Id=="s2e1","Next episode crosses season boundaries.");
        Check(EpisodeNavigation.Next(title,Context("s2e1"))==null,"Final episode must not loop.");
        Check(EpisodeNavigation.Next(title,Context("unknown"))==null && EpisodeNavigation.Next(title,Context("s1e1") with{Id="other"})==null,"Unknown episodes and browsed titles cannot alter current playback.");
        var channel=title with{Type="movie",Episodes=[new("a","Video A",0,0,"",false),new("b","Video B",0,0,"",false)]};
        Check(EpisodeNavigation.Next(channel,Context("a") with{Type="movie"})?.Id=="b","Unnumbered channel entries retain provider order.");
        var duplicated = title with{ Episodes=[entries[2],entries[2] with{Id="first-dub"},entries[1],entries[1] with{Id="second-dub"},entries[0]] };
        Check(EpisodeNavigation.Entries(duplicated).Count==3,"Alternate source IDs must not create duplicate episode cards.");
        Check(EpisodeNavigation.Next(duplicated,Context("s1e1"))?.Id=="s1e2" && EpisodeNavigation.Next(duplicated,Context("first-dub"))?.Id=="s1e2","Autoplay skips variants from either original or alternate history IDs.");
        Check(EpisodeNavigation.Variants(duplicated,"s1e2").Select(x=>x.Id).SequenceEqual(new[]{"s1e2","second-dub"}),"Episode grouping preserves every alternate source ID.");
        Check(EpisodeNavigation.Entries(channel with{Episodes=[new("a","Same",0,0,"",false),new("b","Same",0,0,"",false)]}).Count==2,"Unknown episode numbers and repeated channel titles must not be merged.");
        var described = title with { Episodes=[new("sub-1","Cruelty",1,1,"Same complete synopsis",false),new("dub-1","Cruelty",1,1,"Same complete synopsis",false),new("sub-2","Second",1,2,"Next synopsis",false)] };
        Check(EpisodeNavigation.Entries(described).Count==2 && EpisodeNavigation.Next(described,Context("dub-1"))?.Id=="sub-2","Providers missing reliable numbering still group exact duplicate descriptions at the same displayed episode position.");
        Check(EpisodeNavigation.Entries(described with{Episodes=[described.Episodes[0],described.Episodes[1] with{Overview="Different video"}]}).Count==2,"Unverified entries with different descriptions remain separate.");
        Check(!new PlayerPreferences().AutoplayEpisodes,"Autoplay is opt-in.");
        Console.WriteLine("PASS: next episode numeric order, season transitions, last/unknown episode, title isolation and unnumbered channel order.");
    }
}
