// SPDX-License-Identifier: GPL-3.0-or-later
// Original BingeX development probe. No third-party implementation is bundled.
import java.io.*;
import java.net.*;
import java.nio.file.*;
import java.lang.reflect.*;
import java.util.*;
import java.util.concurrent.*;
import kotlin.coroutines.*;
import kotlin.coroutines.intrinsics.IntrinsicsKt;
import kotlin.jvm.functions.Function2;


public final class EchoProbe {
    interface Suspend {Object run(Continuation<Object> continuation) throws Exception;}
    static Object await(Suspend action) throws Exception {
        CompletableFuture<Object> pending=new CompletableFuture<>();
        Continuation<Object> continuation=new Continuation<>() {
            public CoroutineContext getContext(){return EmptyCoroutineContext.INSTANCE;}
            public void resumeWith(Object result){try{kotlin.ResultKt.throwOnFailure(result);pending.complete(result);}catch(Throwable t){pending.completeExceptionally(t);}}
        };
        Object result=action.run(continuation);
        return result==IntrinsicsKt.getCOROUTINE_SUSPENDED()?pending.get(20,TimeUnit.SECONDS):result;
    }
    static Object call(Object target,String name,Object... args)throws Exception {
        Method method=Arrays.stream(target.getClass().getMethods()).filter(x->x.getName().equals(name)&&x.getParameterCount()==args.length).findFirst().orElseThrow();
        try{return method.invoke(target,args);}catch(InvocationTargetException e){throw new RuntimeException(e.getCause());}
    }
    static Object suspend(Object target,String name,Object... args)throws Exception {
        return await(c->{Object[] values=Arrays.copyOf(args,args.length+1);values[args.length]=c;return call(target,name,values);});
    }
    public static void main(String[] args) {
        try {run(args);System.exit(0);} catch(Throwable error) {
            // Provider exceptions may contain private URLs. Report a type only.
            System.err.println("FAIL: "+error.getClass().getSimpleName());System.exit(1);
        }
    }
    @SuppressWarnings("unchecked")
    static void run(String[] args)throws Exception {
        if(args.length<3)throw new IllegalArgumentException("extension.eapk converted.jar entry-class [fixture-base output-file]");
        Path apk=Path.of(args[0]),jar=Path.of(args[1]);
        if(!Files.exists(jar)){
            Class<?> converter=Class.forName("dev.akash.cs3engine.jvm.Dex2Jar");
            Object result=converter.getMethod("convert",File.class,File.class).invoke(converter.getField("INSTANCE").get(null),apk.toFile(),jar.resolveSibling("conversion-cache").toFile());
            System.out.println("CONVERSION: "+call(result,"notes"));
            Files.copy(((File)call(result,"getJar")).toPath(),jar);
        }
        try(URLClassLoader loader=new URLClassLoader(new URL[]{jar.toUri().toURL()},EchoProbe.class.getClassLoader())) {
            Object extension=Class.forName(args[2],true,loader).getConstructor().newInstance();
            System.out.println("PASS: converted extension instantiated on desktop JVM");
            System.out.println("CAPABILITIES: "+String.join(",",Arrays.stream(extension.getClass().getInterfaces()).map(Class::getSimpleName).toList()));
            if(args.length==3)return;
            if(!URI.create(args[3]).getHost().equals("127.0.0.1"))throw new IllegalArgumentException("Only the local fixture is permitted");
            Class<?> userClass=Class.forName("dev.brahmkshatriya.echo.common.models.User");
            Object user=Arrays.stream(userClass.getConstructors()).filter(x->x.getParameterCount()==5).findFirst().orElseThrow().newInstance("fixture","fixture",null,null,Map.of("serverUrl",args[3],"password","probe-password","serverExtensions",""));
            call(extension,"setLoginUser",user);
            Object feed=suspend(extension,"loadSearchFeed","fixture");
            List<?> tabs=(List<?>)call(feed,"getTabs");
            Function2<Object,Continuation<Object>,Object> function=(Function2<Object,Continuation<Object>,Object>)call(feed,"getGetPagedData");
            Object data=await(c->function.invoke(tabs.isEmpty()?null:tabs.get(0),c));
            Object page=suspend(call(data,"getPagedData"),"loadPage",(Object)null);
            List<?> shelves=(List<?>)call(page,"getData");
            if(shelves.isEmpty())throw new IllegalStateException("No search results");
            Object item=call(shelves.get(0),"getMedia");
            Object track=item;
            track=suspend(extension,"loadTrack",track,false);
            List<?> streamables=(List<?>)call(track,"getStreamables");
            if(streamables.isEmpty())throw new IllegalStateException("No streamables");
            Object media=suspend(extension,"loadStreamableMedia",streamables.get(0),false);
            List<?> sources=(List<?>)call(media,"getSources");
            Object request=call(sources.get(0),"getRequest");
            String url=(String)call(request,"getUrl");
            if(!URI.create(url).getHost().equals("127.0.0.1"))throw new IllegalStateException("Resolved outside fixture");
            // Private fixture output only: never log authenticated URLs or add them to BingeX history.
            Files.writeString(Path.of(args[4]),url);
            System.out.println("PASS: search, paginated feed, track and HTTP source resolution (URL withheld)");
        }
    }
}
