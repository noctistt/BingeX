using System.Text.Json;
using BingeX;
internal static class EmbeddedChecks
{
    public static int Run(string[] args)
    {
        try{
            if(args.Length!=2||args[0] is not("--embedded" or "--embedded-live" or "--browse-live"))return 2;
            var source=File.ReadAllText(args[1]);using var token=new CancellationTokenSource(TimeSpan.FromSeconds(35));
            var inspect=MusicFreeEngine.Run(source,"inspect","{}",token.Token);
            Func<string,string>? mock=args[0]=="--embedded"?config=>{using var c=JsonDocument.Parse(config);var url=c.RootElement.GetProperty("url").GetString()!;return JsonSerializer.Serialize(new{status=200,body=(url.Contains("/search")||url.Contains("/music/")&&!url.Contains("/play/"))?JsonSerializer.Serialize(new{results=new[]{new{id="fixture",title="Test tone",artist="BingeX",duration=60,image="https://example.org/art.jpg"}}}):JsonSerializer.Serialize(new{signedUrl="https://example.org/audio.mp4"})});}:null;
            var groups=JsonSerializer.Deserialize<MusicFreeCatalogGroup[]>(MusicFreeEngine.Run(source,"catalogs","{}",token.Token,mock),new JsonSerializerOptions{PropertyNameCaseInsensitive=true})!;
            if(groups.Length<2||groups[0].Catalogs.Length<2)throw new InvalidOperationException();
            using var browse=JsonDocument.Parse(MusicFreeEngine.Run(source,"browse",JsonSerializer.Serialize(new{item=groups[0].Catalogs[0].Item,page=1}),token.Token,mock));
            if(browse.RootElement.GetProperty("items").GetArrayLength()==0)throw new InvalidOperationException();
            if(args[0]=="--browse-live"){Console.WriteLine(JsonSerializer.Serialize(new{result="pass",mode=args[0],sections=groups.Select(x=>x.Title),genres=groups[0].Catalogs.Length,tracks=browse.RootElement.GetProperty("items").GetArrayLength()}));return 0;}
            using var search=JsonDocument.Parse(MusicFreeEngine.Run(source,"search","{\"query\":\"Kevin MacLeod\",\"page\":1}",token.Token,mock));
            var items=search.RootElement.GetProperty("items");if(items.GetArrayLength()==0)throw new InvalidOperationException();
            using var media=JsonDocument.Parse(MusicFreeEngine.Run(source,"resolve",JsonSerializer.Serialize(new{item=items[0].GetProperty("item")}),token.Token,mock));
            var url=media.RootElement.GetProperty("url").GetString();MusicFreeHttp.PublicUri(url!);
            Console.WriteLine(JsonSerializer.Serialize(new{result="pass",mode=args[0],count=items.GetArrayLength(),artwork=items[0].GetProperty("artwork").GetString()!=null,sourceResolved=true,engine="embedded Jint; unmodified plugin; no Node"}));return 0;
        }catch(Exception e){Console.WriteLine(JsonSerializer.Serialize(new{result="failed",error=e.GetType().Name,httpStatus=(e as HttpRequestException)?.StatusCode,inner=e.InnerException?.GetType().Name}));return 1;}
    }
}
