using System.Text.Json;
using BingeX;
internal static class NuclearChecks
{
    public static int Fixtures()
    {
        byte[] Zip(params (string Name,string Text)[] entries){using var bytes=new MemoryStream();using(var zip=new System.IO.Compression.ZipArchive(bytes,System.IO.Compression.ZipArchiveMode.Create,true)){foreach(var entry in entries){using var writer=new StreamWriter(zip.CreateEntry(entry.Name).Open());writer.Write(entry.Text);}}return bytes.ToArray();}
        const string manifest="{\"main\":\"src/index.ts\",\"version\":\"1\",\"author\":\"Fixture\",\"nuclear\":{\"displayName\":\"Nuclear fixture\"}}";
        const string source="""
            import type { NuclearPluginAPI } from '@nuclearplayer/plugin-sdk';
            export default {onEnable(api: NuclearPluginAPI){
              api.Providers.register({kind:'metadata',searchTracks:async(params:{query:string})=>{
                const url=new URL('https://example.org/search?existing=1');url.searchParams.set('q',params.query);const response=await api.Http.fetch(url.toString());
                if(response.status!==403)throw Error('Status lost');
                return [{source:{id:'123'},title:'Fixture song',artists:[{name:'Fixture artist'}],durationMs:120000,album:{title:'Fixture album'},artwork:{items:[{url:'https://example.org/cover.jpg'}]}}];
              }});
              api.Providers.register({kind:'streaming',getStreamUrl:async(id:string)=>({url:'https://example.org/'+id})});
            }};
            """;
        try{
            var package=NuclearMusicImport.Read(Zip(("package.json",manifest),("src/index.ts",source)));using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(25));
            var compiled=NuclearMusicImport.Compile(package,timeout.Token);string? request=null;
            var searched=MusicFreeEngine.Run(compiled.Source,"search","{\"query\":\"test song\",\"page\":1}",timeout.Token,config=>{request=config;return "{\"status\":403,\"body\":\"{}\"}";});
            var results=JsonSerializer.Deserialize<MusicFreeResults>(searched,new JsonSerializerOptions{PropertyNameCaseInsensitive=true})!;
            if(results.Items.Single().Id!="123"||results.Items[0].Artist!="Fixture artist"||results.Items[0].Duration!=120||request==null||!request.Contains("q=test%20song"))throw new Exception("Mapping or URL parameters failed");
            foreach(var invalid in new[]{Zip(("../escape.ts","x")),Zip(("package.json",manifest),("src/index.ts",source),("src/index.ts",source)),Zip(("package.json",manifest),("src/index.ts",new string('a',300000)))}){
                var rejected=false;try{NuclearMusicImport.Read(invalid);}catch{rejected=true;}if(!rejected)throw new Exception("Invalid archive accepted");
            }
            Console.WriteLine("PASS: TypeScript import, registration, metadata mapping, URL query handling, HTTP error status, traversal/duplicate/oversize archive rejection.");return 0;
        }catch(Exception e){Console.WriteLine("FAIL: Nuclear fixtures / "+e.GetType().Name+" / "+e.Message);return 1;}
    }
    public static int Run(string zip)
    {
        var stage="package";
        try{
            var package=NuclearMusicImport.Read(File.ReadAllBytes(zip));stage="compile";
            using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var result=NuclearMusicImport.Compile(package,timeout.Token);
            Console.WriteLine("PASS: compiled "+result.Provider.Name+" / "+result.Source.Length+" characters");
            stage="isolated-inspect";var provider=MusicFreeWorker.Call<MusicFreeProvider>(result.Source,"inspect",new{},timeout.Token).GetAwaiter().GetResult();
            Console.WriteLine("PASS: isolated provider inspection");
            stage="search";var search=MusicFreeWorker.Call<MusicFreeResults>(result.Source,"search",new{query="Kevin MacLeod",page=1},timeout.Token).GetAwaiter().GetResult();
            if(search.Items.Length<2)throw new Exception("Too few tracks");Console.WriteLine("PASS: "+search.Items.Length+" search results");
            stage="resolve";var media=MusicFreeWorker.Call<MusicFreeSource>(result.Source,"resolve",new{item=search.Items[0].Item},timeout.Token).GetAwaiter().GetResult();MusicFreeHttp.PublicUri(media.Url);
            Console.WriteLine("PASS: resolved HTTPS source without Node");return 0;
        }catch(Exception e){Console.WriteLine("FAIL: "+stage+" / "+e.GetType().Name);return 1;}
    }
}
