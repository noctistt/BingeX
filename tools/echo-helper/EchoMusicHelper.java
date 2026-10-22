// SPDX-License-Identifier: GPL-3.0-or-later
// Original BingeX helper. Runtime libraries and extensions are supplied separately.
import java.io.*;
import java.net.*;
import java.nio.charset.StandardCharsets;
import java.nio.file.*;
import java.lang.reflect.*;
import java.security.MessageDigest;
import java.util.*;
import java.util.concurrent.*;
import com.google.gson.*;
import kotlin.coroutines.*;
import kotlin.coroutines.intrinsics.IntrinsicsKt;
import kotlin.jvm.functions.Function2;
import org.objectweb.asm.*;
import java.util.jar.*;

public final class EchoMusicHelper {
    static final int PROTOCOL=1,MAX_LINE=1048576,MAX_HANDLES=8192;
    static final Gson JSON=new Gson();
    // Android packages can carry desugared SDK default methods. Keep that SDK ABI together.
    static final class ExtensionLoader extends URLClassLoader {
        ExtensionLoader(URL url){super(new URL[]{url},EchoMusicHelper.class.getClassLoader());}
        protected synchronized Class<?> loadClass(String name,boolean resolve)throws ClassNotFoundException {
            if(name.startsWith("dev.brahmkshatriya.echo.common.")){
                Class<?> found=findLoadedClass(name);
                if(found==null)try{found=findClass(name);}catch(ClassNotFoundException missing){found=super.loadClass(name,false);}
                if(resolve)resolveClass(found);return found;
            }
            return super.loadClass(name,resolve);
        }
    }
    record Held(String kind,Object value){}
    record Cursor(Object data,String continuation){}
    interface Suspend {Object run(Continuation<Object> c)throws Exception;}
    final Map<String,Held> handles=new LinkedHashMap<>();
    final Map<String,Object> settings=new ConcurrentHashMap<>();
    final Path cache;
    Object extension;
    URLClassLoader loader;
    boolean exit;
    String phase="dispatch";
    EchoMusicHelper(Path cache){this.cache=cache;}
    static Object await(Suspend action)throws Exception {
        CompletableFuture<Object> pending=new CompletableFuture<>();
        Continuation<Object> c=new Continuation<>() {
            public CoroutineContext getContext(){return EmptyCoroutineContext.INSTANCE;}
            public void resumeWith(Object result){try{kotlin.ResultKt.throwOnFailure(result);pending.complete(result);}catch(Throwable t){pending.completeExceptionally(t);}}
        };
        Object value=action.run(c);
        return value==IntrinsicsKt.getCOROUTINE_SUSPENDED()?pending.get(30,TimeUnit.SECONDS):value;
    }
    static Object call(Object target,String name,Object... args)throws Exception {
        Method method=Arrays.stream(target.getClass().getMethods()).filter(x->x.getName().equals(name)&&x.getParameterCount()==args.length).filter(x->{Class<?>[] types=x.getParameterTypes();for(int i=0;i<args.length;i++)if(args[i]!=null&&!types[i].isPrimitive()&&!types[i].isInstance(args[i]))return false;return true;}).findFirst().orElseThrow(()->new UnsupportedOperationException());
        try{return method.invoke(target,args);}catch(InvocationTargetException e){throw new RuntimeException(e.getCause());}
    }
    static Object suspend(Object target,String name,Object... args)throws Exception {
        return await(c->{Object[] values=Arrays.copyOf(args,args.length+1);values[args.length]=c;return call(target,name,values);});
    }
    static String text(Object item,String key)throws Exception {Object value=call(item,key);return value==null?"":value.toString();}
    static String input(JsonObject p,String key){return p.has(key)&&p.get(key).isJsonPrimitive()?p.get(key).getAsString():"";}
    static List<?> list(Object value){return value instanceof List<?> result?result:List.of();}
    static String digest(Path path)throws Exception {MessageDigest hash=MessageDigest.getInstance("SHA-256");try(InputStream data=Files.newInputStream(path)){byte[] buffer=new byte[65536];for(int n;(n=data.read(buffer))>=0;)hash.update(buffer,0,n);}return HexFormat.of().formatHex(hash.digest());}
    String hold(String kind,Object value){if(handles.size()>=MAX_HANDLES)throw new IllegalStateException();String token=UUID.randomUUID().toString();handles.put(token,new Held(kind,value));return token;}
    Object get(String token,String kind){Held found=handles.get(token);if(found==null||!found.kind.equals(kind))throw new IllegalArgumentException();return found.value;}
    void clearContent(){handles.entrySet().removeIf(x->!x.getValue().kind.equals("user"));}
    static Map<String,String> strings(JsonObject object){Map<String,String> result=new LinkedHashMap<>();if(object.size()>64)throw new IllegalArgumentException();for(var e:object.entrySet()){String value=e.getValue().isJsonNull()?null:e.getValue().getAsString();if(e.getKey().length()>128||(value!=null&&value.length()>16384))throw new IllegalArgumentException();result.put(e.getKey(),value);}return result;}
    void configure(JsonObject values){if(values.size()>128)throw new IllegalArgumentException();for(var row:values.entrySet()){
        if(row.getKey().length()>128)throw new IllegalArgumentException();JsonElement value=row.getValue();Object stored;
        if(value.isJsonNull())stored=null;
        else if(value.isJsonArray()){Set<String> set=new HashSet<>();if(value.getAsJsonArray().size()>128)throw new IllegalArgumentException();for(JsonElement item:value.getAsJsonArray()){String s=item.getAsString();if(s.length()>16384)throw new IllegalArgumentException();set.add(s);}stored=set;}
        else if(value.isJsonPrimitive()){JsonPrimitive primitive=value.getAsJsonPrimitive();stored=primitive.isBoolean()?primitive.getAsBoolean():primitive.isNumber()?primitive.getAsInt():primitive.getAsString();if(stored instanceof String s&&s.length()>16384)throw new IllegalArgumentException();}
        else throw new IllegalArgumentException();
        if(stored==null)settings.remove(row.getKey());else settings.put(row.getKey(),stored);
    }clearContent();}
    String runtimeFingerprint()throws Exception {
        MessageDigest hash=MessageDigest.getInstance("SHA-256");
        Path classes=Path.of(EchoMusicHelper.class.getProtectionDomain().getCodeSource().getLocation().toURI());
        try(var files=Files.list(classes)){for(Path file:files.filter(x->x.getFileName().toString().startsWith("EchoMusicHelper")&&x.toString().endsWith(".class")).sorted().toList())hash.update(digest(file).getBytes(StandardCharsets.US_ASCII));}
        hash.update(System.getProperty("java.version").getBytes(StandardCharsets.UTF_8));
        for(String file:Arrays.stream(System.getProperty("java.class.path").split(File.pathSeparator)).sorted().toList())if(file.endsWith(".jar")){hash.update(Path.of(file).getFileName().toString().getBytes(StandardCharsets.UTF_8));hash.update(digest(Path.of(file)).getBytes(StandardCharsets.US_ASCII));}
        return HexFormat.of().formatHex(hash.digest());
    }
    // DEX invoke-super to an external JVM default interface must use an InterfaceMethodref.
    // The converter cannot infer interfaces omitted by an extension's compileOnly SDK.
    Path fixExternalInterfaces(Path input)throws Exception {
        Path output=input.resolveSibling("echo-contract-v1.jar");if(Files.exists(output))return output;
        Path temporary=input.resolveSibling("echo-contract-"+UUID.randomUUID()+".tmp");
        try(JarFile jar=new JarFile(input.toFile());JarOutputStream out=new JarOutputStream(Files.newOutputStream(temporary))){
            for(JarEntry entry:Collections.list(jar.entries())){
                byte[] bytes=jar.getInputStream(entry).readAllBytes();
                if(entry.getName().endsWith(".class")){
                    ClassReader reader=new ClassReader(bytes);ClassWriter writer=new ClassWriter(reader,0);
                    reader.accept(new ClassVisitor(Opcodes.ASM9,writer){
                        public MethodVisitor visitMethod(int access,String name,String descriptor,String signature,String[] exceptions){
                            return new MethodVisitor(Opcodes.ASM9,super.visitMethod(access,name,descriptor,signature,exceptions)){
                                public void visitMethodInsn(int opcode,String owner,String method,String desc,boolean isInterface){
                                    if(opcode==Opcodes.INVOKESPECIAL||opcode==Opcodes.INVOKESTATIC||opcode==Opcodes.INVOKEINTERFACE)
                                        try{isInterface=Class.forName(owner.replace('/','.'),false,EchoMusicHelper.class.getClassLoader()).isInterface();}catch(ClassNotFoundException absent){}
                                    super.visitMethodInsn(opcode,owner,method,desc,isInterface);
                                }
                            };
                        }
                    },0);bytes=writer.toByteArray();
                }
                out.putNextEntry(new JarEntry(entry.getName()));out.write(bytes);out.closeEntry();
            }
        }catch(Throwable error){Files.deleteIfExists(temporary);throw error;}
        try{Files.move(temporary,output);}catch(FileAlreadyExistsException raced){Files.deleteIfExists(temporary);}return output;
    }
    Object load(JsonObject p)throws Exception {
        phase="validate_package";if(extension!=null)throw new IllegalStateException();
        Path file=Path.of(input(p,"path")).toRealPath();String expected=input(p,"sha256");String sdkHash=input(p,"sdkSha256");String entry=input(p,"entryClass");
        if(!expected.matches("[a-fA-F0-9]{64}")||!sdkHash.matches("[a-fA-F0-9]{64}")||!digest(file).equalsIgnoreCase(expected)||Files.size(file)>33554432||!entry.matches("[A-Za-z_$][A-Za-z0-9_$.]{0,255}"))throw new IllegalArgumentException();
        phase="validate_sdk";Class<?> user=Class.forName("dev.brahmkshatriya.echo.common.models.User");
        if(!digest(Path.of(user.getProtectionDomain().getCodeSource().getLocation().toURI())).equalsIgnoreCase(sdkHash))throw new IllegalArgumentException();
        phase="convert";Path target=cache.resolve(runtimeFingerprint());Files.createDirectories(target);
        Class<?> converter=Class.forName("dev.akash.cs3engine.jvm.Dex2Jar");
        Object converted=converter.getMethod("convert",File.class,File.class).invoke(converter.getField("INSTANCE").get(null),file.toFile(),target.toFile());
        if(Boolean.TRUE.equals(call(converted,"getIncomplete")))throw new UnsupportedOperationException();
        phase="instantiate";loader=new ExtensionLoader(fixExternalInterfaces(((File)call(converted,"getJar")).toPath()).toUri().toURL());
        extension=Class.forName(entry,true,loader).getConstructor().newInstance();
        phase="inject_settings";Class<?> settingsType=Class.forName("dev.brahmkshatriya.echo.common.settings.Settings",true,loader);
        Object proxy=java.lang.reflect.Proxy.newProxyInstance(settingsType.getClassLoader(),new Class<?>[]{settingsType},(obj,method,args)->{
            if(method.getDeclaringClass()==Object.class)return switch(method.getName()){case "hashCode"->System.identityHashCode(obj);case "equals"->obj==args[0];default->"BingeX session settings";};
            String key=(String)args[0];if(method.getName().startsWith("get"))return settings.get(key);
            if(args[1]==null)settings.remove(key);else settings.put(key,args[1]);return null;
        });
        if(p.has("settings"))configure(p.getAsJsonObject("settings"));
        call(extension,"setSettings",proxy);phase="initialize";suspend(extension,"onInitialize");phase="select_extension";suspend(extension,"onExtensionSelected");phase="dispatch";
        return Map.of("capabilities",Arrays.stream(extension.getClass().getInterfaces()).map(Class::getSimpleName).toList(),"conversion",call(converted,"notes"));
    }
    Object feed(Object value)throws Exception {
        if(value==null)return Map.of("feed","","tabs",List.of());
        List<Object> tabs=new ArrayList<>();for(Object tab:list(call(value,"getTabs"))){if(tabs.size()>=64)break;tabs.add(Map.of("id",text(tab,"getId"),"title",text(tab,"getTitle")));}
        return Map.of("feed",hold("feed",value),"tabs",tabs);
    }
    Map<String,Object> item(Object raw)throws Exception {
        Object value=raw;if(raw.getClass().getSimpleName().equals("Item"))value=call(raw,"getMedia");
        String type=value.getClass().getSimpleName();
        if(type.equals("Track")||type.equals("Album"))return Map.of("kind",type.toLowerCase(Locale.ROOT),"handle",hold(type.toLowerCase(Locale.ROOT),value),"title",text(value,"getTitle"));
        if(type.equals("Category")){Object nested=call(value,"getFeed");return Map.of("kind","category","title",text(value,"getTitle"),"feed",nested==null?"":hold("feed",nested));}
        // Lists are expanded into display children; unknown shelves remain labelled instead of executing UI callbacks.
        if(Arrays.stream(value.getClass().getMethods()).anyMatch(x->x.getName().equals("getList"))){List<Object> children=new ArrayList<>();for(Object child:list(call(value,"getList"))){if(children.size()>=100)break;children.add(item(child));}return Map.of("kind","group","title",text(value,"getTitle"),"items",children);}
        return Map.of("kind","unsupported","title",text(value,"getTitle"));
    }
    @SuppressWarnings("unchecked")
    Object page(JsonObject p)throws Exception {
        Object data;String continuation=null;
        if(p.has("next")){Cursor c=(Cursor)get(input(p,"next"),"cursor");data=c.data;continuation=c.continuation;}
        else {
            Object current=get(input(p,"feed"),"feed");List<?> tabs=list(call(current,"getTabs"));Object selected=null;String id=input(p,"tab");
            if(!tabs.isEmpty()){selected=tabs.stream().filter(x->{try{return text(x,"getId").equals(id);}catch(Exception e){return false;}}).findFirst().orElse(null);if(selected==null)throw new IllegalArgumentException();}
            Function2<Object,Continuation<Object>,Object> function=(Function2<Object,Continuation<Object>,Object>)call(current,"getGetPagedData");Object tab=selected;
            data=call(await(c->function.invoke(tab,c)),"getPagedData");
        }
        Object result=suspend(data,"loadPage",continuation);List<?> all=list(call(result,"getData"));if(all.size()>500)throw new UnsupportedOperationException();List<Object> rows=new ArrayList<>();for(Object entry:all)rows.add(item(entry));
        Object next=call(result,"getContinuation");return Map.of("items",rows,"next",next==null?"":hold("cursor",new Cursor(data,next.toString())));
    }
    Object forms()throws Exception {
        List<Object> result=new ArrayList<>();for(Object form:list(call(extension,"getForms"))){if(result.size()>=16)break;List<Object> fields=new ArrayList<>();for(Object field:list(call(form,"getInputFields"))){if(fields.size()>=32)break;fields.add(Map.of("key",text(field,"getKey"),"label",text(field,"getLabel"),"type",text(field,"getType"),"required",call(field,"isRequired")));}result.add(Map.of("key",text(form,"getKey"),"label",text(form,"getLabel"),"fields",fields));}return result;
    }
    Object login(JsonObject p)throws Exception {
        call(extension,"setLoginUser",(Object)null);handles.clear();List<?> users=list(suspend(extension,"onLogin",input(p,"key"),strings(p.getAsJsonObject("data"))));List<Object> result=new ArrayList<>();for(Object user:users){if(result.size()>=32)break;result.add(Map.of("handle",hold("user",user),"name",text(user,"getName")));}return result;
    }
    Object schema(List<?> rows,int depth)throws Exception {
        if(depth>4)throw new UnsupportedOperationException();List<Object> result=new ArrayList<>();for(Object setting:rows){if(result.size()>=128)break;Map<String,Object> row=new LinkedHashMap<>();row.put("key",text(setting,"getKey"));row.put("title",text(setting,"getTitle"));row.put("kind",setting.getClass().getSimpleName());if(Arrays.stream(setting.getClass().getMethods()).anyMatch(x->x.getName().equals("getItems")))row.put("items",schema(list(call(setting,"getItems")),depth+1));result.add(row);}return result;
    }
    Object variants(String handle)throws Exception {
        Object track=suspend(extension,"loadTrack",get(handle,"track"),false);List<Object> rows=new ArrayList<>();for(Object variant:list(call(track,"getStreamables"))){if(rows.size()>=64)break;if(!text(variant,"getType").equals("Server"))continue;rows.add(Map.of("handle",hold("variant",variant),"title",text(variant,"getTitle"),"quality",call(variant,"getQuality")));}return rows;
    }
    Object resolve(String handle)throws Exception {
        Object media=suspend(extension,"loadStreamableMedia",get(handle,"variant"),false);
        if(!media.getClass().getSimpleName().equals("Server")||Boolean.TRUE.equals(call(media,"getMerged")))throw new UnsupportedOperationException();List<Object> rows=new ArrayList<>();
        for(Object source:list(call(media,"getSources"))){if(rows.size()>=32)break;if(!source.getClass().getSimpleName().equals("Http")||call(source,"getDecryption")!=null)throw new UnsupportedOperationException();Object request=call(source,"getRequest");String method=text(request,"getMethod");if(!method.equals("GET")||call(request,"getBodyBase64")!=null)throw new UnsupportedOperationException();String url=text(request,"getUrl");URI uri=URI.create(url);if(!Set.of("http","https").contains(uri.getScheme())||uri.getHost()==null||uri.getUserInfo()!=null||url.length()>65536)throw new UnsupportedOperationException();Map<?,?> headers=(Map<?,?>)call(request,"getHeaders");if(headers.size()>64)throw new UnsupportedOperationException();for(var h:headers.entrySet())if(!h.getKey().toString().matches("[!#$%&'*+.^_`|~0-9A-Za-z-]{1,128}")||h.getValue().toString().length()>16384||h.getValue().toString().matches("(?s).*[\\r\\n\\x00].*"))throw new UnsupportedOperationException();rows.add(Map.of("url",url,"headers",headers,"type",text(source,"getType"),"live",call(source,"isLive")));}
        return rows;
    }
    Object dispatch(String method,JsonObject p)throws Exception {
        if(method.equals("hello"))return Map.of("protocol",PROTOCOL,"sessionOnly",true,"interactiveHost",false);
        if(method.equals("load"))return load(p);
        if(method.equals("close")){exit=true;return Map.of("closed",true);}
        if(extension==null)throw new IllegalStateException();
        return switch(method){
            case "configure"->{configure(p.getAsJsonObject("values"));yield Map.of("configured",true);}
            case "settings"->schema(list(suspend(extension,"getSettingItems")),0);
            case "loginForms"->forms();
            case "login"->login(p);
            case "selectUser"->{Object user=get(input(p,"handle"),"user");clearContent();call(extension,"setLoginUser",user);yield Map.of("selected",true);}
            case "home"->feed(suspend(extension,"loadHomeFeed"));
            case "search"->feed(suspend(extension,"loadSearchFeed",input(p,"query")));
            case "page"->page(p);
            case "album"->{Object album=suspend(extension,"loadAlbum",get(input(p,"handle"),"album"));yield feed(suspend(extension,"loadTracks",album));}
            case "variants"->variants(input(p,"handle"));
            case "resolve"->resolve(input(p,"handle"));
            default->throw new UnsupportedOperationException();
        };
    }
    static String failureType(Throwable error){while(error.getCause()!=null && error.getCause()!=error)error=error.getCause();return error.getClass().getSimpleName();}
    static String readLine(Reader in)throws IOException {StringBuilder result=new StringBuilder();for(int c;(c=in.read())!=-1;){if(c=='\n')return result.toString();if(c!='\r')result.append((char)c);if(result.length()>MAX_LINE)throw new IOException();}return result.length()==0?null:result.toString();}
    public static void main(String[] args) {
        PrintWriter protocol=new PrintWriter(new OutputStreamWriter(System.out,StandardCharsets.UTF_8),true);
        // Extension diagnostics cannot share protocol stdout, and may contain credentials.
        System.setOut(new PrintStream(OutputStream.nullOutputStream()));System.setErr(new PrintStream(OutputStream.nullOutputStream()));
        int code=0;
        try {
            if(args.length!=1)throw new IllegalArgumentException();EchoMusicHelper helper=new EchoMusicHelper(Path.of(args[0]));Reader input=new BufferedReader(new InputStreamReader(System.in,StandardCharsets.UTF_8));String line;
            while((line=readLine(input))!=null){int id=-1;try{JsonObject request=JsonParser.parseString(line).getAsJsonObject();id=request.get("id").getAsInt();if(id<1)throw new IllegalArgumentException();Object result=helper.dispatch(input(request,"method"),request.getAsJsonObject("params"));String response=JSON.toJson(Map.of("id",id,"result",result));if(response.length()>MAX_LINE)throw new UnsupportedOperationException();protocol.println(response);if(helper.exit)break;}
                catch(Throwable error){protocol.println(JSON.toJson(Map.of("id",id,"error",Map.of("code",error instanceof UnsupportedOperationException?"unsupported":"request_failed","fatal",true,"stage",helper.phase,"type",failureType(error)))));code=1;break;}}
            helper.settings.clear();helper.handles.clear();if(helper.loader!=null)helper.loader.close();
        }catch(Throwable error){code=1;}
        System.exit(code);
    }
}
