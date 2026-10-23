using System.Text.Json;
using Jint;
namespace BingeX;
internal static class NuclearMusicRuntime
{
    internal static void Setup(Engine engine,Func<string,MusicFreeResponse> http)
    {
        var status=0;
        engine.SetValue("__nuclearHttp",new Func<string,string>(config=>{
            var response=http(config);status=response.status;return response.body;
        }));
        engine.SetValue("__nuclearStatus",new Func<int>(()=>status));
        engine.SetValue("__nuclearUrl",new Func<string,string>(value=>{
            var uri=new Uri(value,UriKind.Absolute);return JsonSerializer.Serialize(new{href=uri.AbsoluteUri,origin=uri.GetLeftPart(UriPartial.Authority),pathname=uri.AbsolutePath,search=uri.Query});
        }));
    }
    internal const string Adapter="""
        class URLSearchParams {
          constructor(value={}) { this.values=[]; if(typeof value==='string'){ for(const pair of value.replace(/^\?/,'').split('&').filter(Boolean)){const i=pair.indexOf('=');this.values.push([decodeURIComponent((i<0?pair:pair.slice(0,i)).replace(/\+/g,' ')),decodeURIComponent((i<0?'':pair.slice(i+1)).replace(/\+/g,' '))]);} }else for(const key of Object.keys(value))this.values.push([key,String(value[key])]); }
          set(key,value){this.values=this.values.filter(x=>x[0]!==key);this.values.push([String(key),String(value)]);}
          toString(){return this.values.map(x=>encodeURIComponent(x[0])+'='+encodeURIComponent(x[1])).join('&');}
        }
        class URL {
          constructor(value){this.parts=JSON.parse(__nuclearUrl(String(value)));this.searchParams=new URLSearchParams(this.parts.search);}
          toString(){const query=this.searchParams.toString();return this.parts.origin+this.parts.pathname+(query?'?'+query:'');}
        }
        const __nuclearProviders=new Map(),__nuclearCache={};
        function __nuclearLoad(name){
          if(__nuclearCache[name])return __nuclearCache[name].exports;
          const factory=__nuclearFactories[name];if(!factory)throw Error('Unsupported Nuclear module');
          const module={exports:{}};__nuclearCache[name]=module;
          factory(module,module.exports,spec=>{
            if(!spec.startsWith('./')&&!spec.startsWith('../'))throw Error('Unsupported Nuclear dependency');
            const parts=name.split('/');parts.pop();for(const part of spec.split('/')){if(part==='.'||part==='')continue;if(part==='..'){if(!parts.length)throw Error('Module boundary');parts.pop();}else parts.push(part);}
            const path=parts.join('/');const target=[path,path+'.ts',path+'.js'].find(x=>__nuclearFactories[x]);if(!target)throw Error('Missing module');return __nuclearLoad(target);
          });return module.exports;
        }
        const __nuclearAPI={
          Providers:{register(p){if(p.kind==='metadata'||p.kind==='streaming')__nuclearProviders.set(p.kind,p);},unregister(){}},
          Logger:{debug(){},info(){},warn(){},error(){}},
          Http:{fetch:async(input,init={})=>{
            if(init.method&&init.method.toUpperCase()!=='GET')throw Error('Only GET is supported');
            const body=__nuclearHttp(JSON.stringify({url:String(input),method:'GET',headers:init.headers??{}})),status=__nuclearStatus();
            return {ok:status>=200&&status<300,status,text:async()=>body,json:async()=>JSON.parse(body)};
          }}
        };
        const __nuclearPlugin=__nuclearLoad(__nuclearMain);(__nuclearPlugin.default??__nuclearPlugin).onEnable(__nuclearAPI);
        const __nuclearMetadata=__nuclearProviders.get('metadata'),__nuclearStreaming=__nuclearProviders.get('streaming');
        if(!__nuclearMetadata?.searchTracks||!__nuclearStreaming?.getStreamUrl)throw Error('Search and streaming providers required');
        module.exports={platform:'Nuclear provider',
          search:async(query,page)=>{if(page>1)return {data:[],isEnd:true};const tracks=await __nuclearMetadata.searchTracks({query,limit:20});return {isEnd:true,data:tracks.slice(0,60).map(t=>({id:String(t.source?.id??''),title:t.title,artist:(t.artists??[]).map(x=>x.name).join(', '),album:t.album?.title??'',duration:(t.durationMs??0)/1000,artwork:t.artwork?.items?.[0]?.url??null}))};},
          getMediaSource:async(item)=>{const stream=await __nuclearStreaming.getStreamUrl(String(item.id));return {url:stream.url,headers:stream.headers??{}};}
        };
        """;
}
