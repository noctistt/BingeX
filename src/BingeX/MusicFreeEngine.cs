using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Jint;
namespace BingeX;

// A deliberately small, stateless MusicFree host: search, charts + standard audio.
// Execution limits are cooperative; the app also runs this in a killable worker.
public static class MusicFreeEngine
{
    public static string Run(string source,string operation,string arguments,CancellationToken token,Func<string,string>? http=null)
    {
        if(source.Length>512*1024||operation is not("inspect" or "search" or "resolve" or "catalogs" or "browse"))throw new InvalidDataException();
        var requests=0;
        using var engine=new Engine(o=>o.DisableStringCompilation().LimitMemory(32_000_000).MaxStatements(1_000_000).LimitRecursion(128).TimeoutInterval(TimeSpan.FromSeconds(25)).CancellationToken(token));
        engine.SetValue("__http",new Func<string,string>(config=>{if(operation=="inspect"||++requests>12)throw new InvalidOperationException();return (http??(s=>MusicFreeHttp.Send(s,token)))(config);}));
        engine.SetValue("__hmac",new Func<string,string,string>((message,key)=>Convert.ToBase64String(HMACSHA1.HashData(Encoding.UTF8.GetBytes(key),Encoding.UTF8.GetBytes(message)))));
        engine.Execute("""
            var module={exports:{}},exports=module.exports;
            var console={log(){},warn(){},error(){},info(){},debug(){}};
            var env={os:'win32',appVersion:'0.0.8',lang:'en',getUserVariables:()=>({})};
            var process={platform:'win32',env,ensurePluginInitialized:Promise.resolve()};
            function request(config){
                const response=JSON.parse(__http(JSON.stringify(config)));let data=response.body;
                try{data=JSON.parse(data)}catch{}
                return Promise.resolve({data,status:response.status});
            }
            var axios=config=>request(config);
            axios.get=(url,config={})=>request({...config,url,method:'GET'});
            axios.post=(url,data,config={})=>request({...config,url,data,method:'POST'});
            axios.default=axios;
            function require(name){
                if(name==='axios')return axios;
                if(name==='crypto-js')return {enc:{Base64:'base64'},HmacSHA1:(message,key)=>({toString:encoding=>{if(encoding!=='base64')throw Error('Unsupported encoding');return __hmac(message,key)}})};
                if(name==='dayjs')return {unix:seconds=>({format:pattern=>{if(pattern!=='YYYY-MM-DD')throw Error('Unsupported date format');return new Date(seconds*1000).toISOString().slice(0,10)}})};
                if(name==='cheerio')return {load:()=>{throw Error('HTML catalogue methods are not supported in this preview')}};
                throw Error('Unsupported MusicFree module');
            }
            var __musicfree_require=require;
            """);
        engine.Execute(source);
        engine.SetValue("__operation",operation);engine.SetValue("__arguments",arguments);
        var result=engine.Evaluate("""
            (async()=>{
                const plugin=module.exports.default??module.exports,args=JSON.parse(__arguments);
                if(typeof plugin.platform!=='string'||typeof plugin.search!=='function'||typeof plugin.getMediaSource!=='function')throw Error('Search/audio provider required');
                if(__operation==='inspect')return JSON.stringify({name:plugin.platform,version:String(plugin.version??''),author:String(plugin.author??'')});
                const tracks=items=>(items??[]).slice(0,60).map(x=>({id:String(x.id??''),title:String(x.title??''),artist:String(x.artist??''),artwork:typeof x.artwork==='string'?x.artwork:null,duration:Number(x.duration)||0,item:x}));
                if(__operation==='catalogs'){
                    if(typeof plugin.getTopLists!=='function'||typeof plugin.getTopListDetail!=='function')return JSON.stringify([]);
                    const groups=await plugin.getTopLists();
                    return JSON.stringify((groups??[]).slice(0,12).map(g=>({title:String(g.title??''),catalogs:(g.data??[]).slice(0,60).map(c=>({title:String(c.title??c.name??''),item:c}))})));
                }
                if(__operation==='browse'){
                    if(typeof plugin.getTopListDetail!=='function')throw Error('Browse unavailable');
                    const result=await plugin.getTopListDetail(args.item,args.page);
                    return JSON.stringify({isEnd:result?.isEnd!==false,items:tracks(result?.musicList??result?.data)});
                }
                if(__operation==='search'){
                    const found=await plugin.search(args.query,args.page,'music');
                    return JSON.stringify({isEnd:found?.isEnd!==false,items:tracks(found?.data)});
                }
                const media=await plugin.getMediaSource(args.item,'standard');
                return JSON.stringify({url:typeof media==='string'?media:(media?.url??''),headers:typeof media==='string'?{}:(media?.headers??{})});
            })()
            """).UnwrapIfPromise().AsString();
        if(result.Length>512*1024)throw new InvalidDataException();return result;
    }
}
