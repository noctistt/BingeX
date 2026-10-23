// Research only: run reviewed plugins in a separate disposable process.
// Node vm is NOT a security sandbox. Never use this with private credentials.
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const {createRequire} = require('node:module');
const crypto = require('node:crypto');
const [root, dependencyRoot, mode = 'inspect', name = 'youtube', query = 'Kevin MacLeod'] = process.argv.slice(2);
if (!root || !dependencyRoot || !['inspect','fixture','live'].includes(mode)) throw Error('Usage: node probe.cjs PLUGIN_REPO DEPENDENCY_DIRECTORY inspect|fixture|live [plugin] [query]');
const dependency = createRequire(path.resolve(dependencyRoot, 'package.json'));
const allowed = new Set(['axios','cheerio','crypto-js','dayjs','big-integer','qs','he','webdav']);
const quiet = {log(){},warn(){},error(){},debug(){},info(){}};
let requests = 0;
const startupErrors=[];
process.on('unhandledRejection',e=>{startupErrors.push(e?.code??e?.name??'Error')});
function load(name, mockAxios) {
  if(!/^[a-z0-9_-]+$/i.test(name)) throw Error('Invalid plugin name');
  const file = path.resolve(root, 'dist', name, 'index.js');
  const source = fs.readFileSync(file,'utf8');
  const imports = new Set();
  const memory = new Map();
  function pluginRequire(key) {
    imports.add(key);
    let value;
    if(key === 'axios' && mockAxios) value = mockAxios;
    else if(allowed.has(key)) value = dependency(key);
    else if(key === 'musicfree/storage') value = {getItem:async k=>memory.get(k),setItem:async(k,v)=>memory.set(k,v),removeItem:async k=>memory.delete(k)};
    else if(key === '@react-native-cookies/cookies') value = {get:async()=>({}),set:async()=>{},clearAll:async()=>{}};
    else throw Error('Unsupported dependency: '+key);
    if(value && !value.default) value.default=value;
    return value;
  }
  const module={exports:{}};
  const env={getUserVariables:()=>({}),os:'win32',appVersion:'0.0.8',lang:'en'};
  const context=vm.createContext({module,exports:module.exports,require:pluginRequire,__musicfree_require:pluginRequire,console:quiet,env,process:{platform:'win32',version:'0.0.8',env,ensurePluginInitialized:Promise.resolve()},setTimeout,clearTimeout,URL,URLSearchParams,TextEncoder,TextDecoder,Buffer});
  new vm.Script(source,{filename:name+'.js'}).runInContext(context,{timeout:3000});
  const plugin=module.exports.default??module.exports;
  return {plugin, metadata:{name:plugin.platform,version:plugin.version,sha256:crypto.createHash('sha256').update(source).digest('hex'),dependencies:[...imports],methods:Object.keys(plugin).filter(k=>typeof plugin[k]==='function')}};
}
const watchdog=setTimeout(()=>{console.log(JSON.stringify({mode,name,result:'timeout'}));process.exit(2)},55000);
(async()=>{
  const axios=dependency('axios');axios.defaults.timeout=12000;axios.defaults.maxContentLength=4*1024*1024;
  axios.interceptors.request.use(c=>{requests++;if(mode==='inspect')throw Object.assign(Error('Inspection blocks network'),{code:'INSPECTION_NETWORK_BLOCKED'});return c});
  if(mode==='inspect') {
    const results=[];
    for(const entry of fs.readdirSync(path.resolve(root,'dist'),{withFileTypes:true}).filter(x=>x.isDirectory()&&!x.name.startsWith('_'))){
      try{results.push({...load(entry.name).metadata,loaded:true})}catch(e){results.push({name:entry.name,loaded:false,error:e.code??e.name})}
    }
    await new Promise(resolve=>setTimeout(resolve,100));
    console.log(JSON.stringify({mode,results,blockedStartupRequests:requests,startupErrors},null,2));return;
  }
  if(mode==='fixture') {
    const fake=async config=>{
      requests++;
      if(config.url.includes('/search')) return {data:{contents:{twoColumnSearchResultsRenderer:{primaryContents:{sectionListRenderer:{contents:[{itemSectionRenderer:{contents:[{videoRenderer:{videoId:'fixture',title:{runs:[{text:'Test tone'}]},ownerText:{runs:[{text:'BingeX fixture'}]},thumbnail:{thumbnails:[{url:'https://example.invalid/cover.jpg'}]}}}]}}]}}}}}};
      return {data:{streamingData:{formats:[{quality:'small',url:'https://example.invalid/tone.mp4'}],adaptiveFormats:[]}}};
    };
    const {plugin,metadata}=load('youtube',fake);
    const found=await plugin.search('fixture',1,'music');
    if(found.data?.[0]?.title!=='Test tone') throw Error('Search mapping failed');
    const media=await plugin.getMediaSource(found.data[0],'standard');
    if(media.url!=='https://example.invalid/tone.mp4'||requests!==2) throw Error('Source mapping failed');
    console.log(JSON.stringify({mode,result:'pass',metadata,requests,searchAndSource:true}));return;
  }
  const {plugin,metadata}=load(name);let stage='search';let searchCount=0;
  try {
    if(typeof plugin.search!=='function') throw Error('No search');
    const found=await plugin.search(query,1,'music');const items=found?.data??[];searchCount=items.length;
    const report={mode,metadata,query,result:items.length?'search_ok':'empty',count:items.length,artwork:items.some(x=>!!x.artwork),requests};
    if(items.length&&typeof plugin.getMediaSource==='function') {
      stage='source';const media=await plugin.getMediaSource(items[0],'standard');report.sourceResolved=!!media?.url;
      if(media?.url){
        stage='source_headers';const response=await fetch(media.url,{headers:{...(media.headers??{}),Range:'bytes=0-4095'},signal:AbortSignal.timeout(12000)});
        report.httpStatus=response.status;report.contentType=response.headers.get('content-type');await response.body?.cancel();
      }
    }
    report.requests=requests;console.log(JSON.stringify(report,null,2));
  }catch(e){console.log(JSON.stringify({mode,metadata,result:'failed',stage,searchCount,httpStatus:e.response?.status??null,errorCode:e.code??e.name,requests},null,2));}
})().catch(e=>{console.log(JSON.stringify({mode,name,result:'failed',errorCode:e.code??e.name}));process.exitCode=1}).finally(()=>clearTimeout(watchdog));
