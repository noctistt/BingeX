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
        Check(!new PlayerPreferences().AutoplayEpisodes,"Autoplay is opt-in.");
        Console.WriteLine("PASS: next episode numeric order, season transitions, last/unknown episode, title isolation and unnumbered channel order.");
    }
}
