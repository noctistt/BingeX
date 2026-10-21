using BingeX;
internal static class StreamScopeChecks
{
    public static void Run()
    {
        InstalledAddon[] addons=[new("cloudstream:one","Origin","cloudstream://local/one/manifest.json"),new("cloudstream:two","Other extension","cloudstream://local/two/manifest.json"),new("stremio","Stremio","https://addon.example/manifest.json")];
        var context=new MediaContext("cs:one:title","series","Show","","cloudstream:one","cs:one:episode");
        void Check(bool value,string message){if(!value)throw new Exception(message);}
        Check(StreamProviderScope.Select(addons,context,false).Select(x=>x.Id).SequenceEqual(new[]{"cloudstream:one"}),"Extension-only mode must query just the originating extension.");
        Check(StreamProviderScope.Select(addons,context,true).Select(x=>x.Id).SequenceEqual(new[]{"cloudstream:one","stremio"}),"All-provider mode includes compatible Stremio sources without unrelated site-specific extensions.");
        Check(StreamProviderScope.Select(addons,context with{Id="tt1234567",AddonId="catalog"},false).Single().Id=="stremio","Normal catalog titles retain Stremio source discovery.");
        Check(!new PlayerPreferences().SearchAllStreamProviders,"Extension stream lookup defaults to origin only.");
        var folder=Path.Combine(Path.GetTempPath(),"BingeX-scope-"+Guid.NewGuid());
        try
        {
            using(var store=new LibraryStore(folder))store.SavePreferences(new PlayerPreferences(SearchAllStreamProviders:true));
            using(var store=new LibraryStore(folder))Check(store.Preferences().SearchAllStreamProviders,"Search scope survives restarting the app.");
        }
        finally{Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();Directory.Delete(folder,true);}
        Console.WriteLine("PASS: originating extension only, optional shared-ID providers, unrelated-extension exclusion and saved scope.");
    }
}
