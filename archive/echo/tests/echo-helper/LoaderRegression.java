// SPDX-License-Identifier: GPL-3.0-or-later
import java.nio.file.*;
import java.util.*;
import java.util.jar.*;
import java.util.function.Supplier;
import org.objectweb.asm.*;
public class LoaderRegression {
    interface BaseCapability {}
    interface DerivedCapability extends BaseCapability {}
    static class CapabilityFixture implements DerivedCapability {}
    static byte[] type(String name,boolean iface,boolean caller){
        ClassWriter w=new ClassWriter(0);w.visit(Opcodes.V17,Opcodes.ACC_PUBLIC|(iface?Opcodes.ACC_INTERFACE|Opcodes.ACC_ABSTRACT:Opcodes.ACC_SUPER),name,null,"java/lang/Object",null);
        if(!iface){var init=w.visitMethod(Opcodes.ACC_PUBLIC,"<init>","()V",null,null);init.visitCode();init.visitVarInsn(Opcodes.ALOAD,0);init.visitMethodInsn(Opcodes.INVOKESPECIAL,"java/lang/Object","<init>","()V",false);init.visitInsn(Opcodes.RETURN);init.visitMaxs(1,1);init.visitEnd();}
        var m=w.visitMethod(Opcodes.ACC_PUBLIC|Opcodes.ACC_STATIC,"ping","()Ljava/lang/String;",null,null);m.visitCode();
        if(caller)m.visitMethodInsn(Opcodes.INVOKESTATIC,"fixture/Contract","ping","()Ljava/lang/String;",false); // converter's incorrect Methodref
        else m.visitLdcInsn("fixture");
        m.visitInsn(Opcodes.ARETURN);m.visitMaxs(1,0);m.visitEnd();w.visitEnd();return w.toByteArray();
    }
    static byte[] provider(){
        ClassWriter w=new ClassWriter(0);w.visit(Opcodes.V17,Opcodes.ACC_PUBLIC|Opcodes.ACC_SUPER,"fixture/Provider",null,"java/lang/Object",new String[]{"java/util/function/Supplier"});
        var init=w.visitMethod(Opcodes.ACC_PUBLIC,"<init>","()V",null,null);init.visitCode();init.visitVarInsn(Opcodes.ALOAD,0);init.visitMethodInsn(Opcodes.INVOKESPECIAL,"java/lang/Object","<init>","()V",false);init.visitInsn(Opcodes.RETURN);init.visitMaxs(1,1);init.visitEnd();
        var m=w.visitMethod(Opcodes.ACC_PUBLIC,"get","()Ljava/lang/Object;",null,null);m.visitCode();m.visitLdcInsn("registered");m.visitInsn(Opcodes.ARETURN);m.visitMaxs(1,1);m.visitEnd();w.visitEnd();return w.toByteArray();
    }
    static void write(JarOutputStream out,String name,byte[] bytes)throws Exception{out.putNextEntry(new JarEntry(name));out.write(bytes);out.closeEntry();}
    public static void main(String[] args)throws Exception{
        if(!EchoMusicHelper.capabilities(CapabilityFixture.class).containsAll(Set.of("BaseCapability","DerivedCapability")))throw new AssertionError("Inherited capabilities lost");
        Path root=Path.of(args[0]);Files.createDirectory(root);Path classes=root.resolve("classes.jar"),resources=root.resolve("resources.eapk");
        try(var out=new JarOutputStream(Files.newOutputStream(classes))){write(out,"fixture/Contract.class",type("fixture/Contract",true,false));write(out,"fixture/Caller.class",type("fixture/Caller",false,true));write(out,"fixture/Provider.class",provider());}
        try(var out=new JarOutputStream(Files.newOutputStream(resources))){write(out,"META-INF/services/java.util.function.Supplier","fixture.Provider\n".getBytes(java.nio.charset.StandardCharsets.UTF_8));}
        var helper=new EchoMusicHelper(root);Path fixed=helper.fixExternalInterfaces(classes);
        try(var loader=new EchoMusicHelper.ExtensionLoader(fixed.toUri().toURL(),resources.toUri().toURL())){
            Object result=loader.loadClass("fixture.Caller").getMethod("ping").invoke(null);if(!result.equals("fixture"))throw new AssertionError();
            if(!ServiceLoader.load(Supplier.class,loader).iterator().next().get().equals("registered"))throw new AssertionError();
        }
        System.out.println("PASS: package service resources and converted local-interface method references");
    }
}
