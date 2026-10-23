// SPDX-License-Identifier: GPL-3.0-or-later
// Original optional adapter. Third-party conversion/repair libraries are supplied separately.
import java.nio.file.*;
import com.googlecode.d2j.dex.Dex2jar;
import com.googlecode.d2j.reader.MultiDexFileReader;
import com.googlecode.dex2jar.tools.BaksmaliBaseDexExceptionHandler;
public final class BingeXEchoCompatibility {
    public static Path convert(Path apk,Path cache)throws Exception {
        Path result=cache.resolve("converted-v1.jar");
        if(Files.isRegularFile(result))return result;
        Files.createDirectories(cache);Path staging=Files.createTempDirectory(cache,"build-");
        try {
            Path jar=staging.resolve("classes.jar");
            var diagnostics=new BaksmaliBaseDexExceptionHandler();
            Dex2jar.from(MultiDexFileReader.open(Files.readAllBytes(apk))).withExceptionHandler(diagnostics)
                .reUseReg(false).topoLogicalSort().skipDebug(true).optimizeSynchronized(false)
                .printIR(false).noCode(false).skipExceptions(false).dontSanitizeNames(true).to(jar);
            Class.forName("com.aayush262.dartotsu_extension_bridge.util.BytecodeEditor")
                .getMethod("fixAndroidClasses",Path.class,Path.class)
                .invoke(Class.forName("com.aayush262.dartotsu_extension_bridge.util.BytecodeEditor").getField("INSTANCE").get(null),jar,apk);
            // A complete artifact is published only after conversion/repair returns.
            // Actual class verification and provider initialization still happen in the host.
            try{Files.move(jar,result,StandardCopyOption.ATOMIC_MOVE);}catch(AtomicMoveNotSupportedException absent){Files.move(jar,result);}catch(FileAlreadyExistsException raced){}
            return result;
        }finally{
            try(var paths=Files.walk(staging)){for(Path p:paths.sorted(java.util.Comparator.reverseOrder()).toList())Files.deleteIfExists(p);}
        }
    }
}
