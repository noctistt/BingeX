package bingex.bridge;

import java.nio.file.*;
import java.util.*;
import java.util.jar.*;
import java.io.*;
import org.objectweb.asm.*;

/** Preserve the generic ExtractorLink audio contract in the optional engine's JSON output. */
public final class AudioPatch {
    @SuppressWarnings({"rawtypes", "unchecked"})
    public static Map enrich(Map result, Object link) {
        try {
            Object tracks = link.getClass().getMethod("getAudioTracks").invoke(link);
            List<Map<String,Object>> audio = new ArrayList<>();
            for (Object track : (Iterable<?>) tracks) {
                Map<String,Object> row = new LinkedHashMap<>();
                row.put("url", track.getClass().getMethod("getUrl").invoke(track));
                row.put("headers", track.getClass().getMethod("getHeaders").invoke(track));
                audio.add(row);
            }
            result.put("audioTracks", audio);
        } catch (ReflectiveOperationException failure) { throw new IllegalStateException("Audio contract unavailable", failure); }
        return result;
    }
    public static void main(String[] args) throws Exception {
        Path input = Path.of(args[0]), output = Path.of(args[0] + ".patched");
        boolean[] patched = {false};
        try (JarFile jar = new JarFile(input.toFile()); JarOutputStream out = new JarOutputStream(Files.newOutputStream(output))) {
            for (JarEntry entry : Collections.list(jar.entries())) {
                if (entry.getName().startsWith("bingex/bridge/AudioPatch")) continue;
                byte[] bytes = jar.getInputStream(entry).readAllBytes();
                if (entry.getName().equals("dev/akash/cs3engine/Mappers.class")) {
                    ClassReader reader = new ClassReader(bytes);
                    ClassWriter writer = new ClassWriter(reader, ClassWriter.COMPUTE_MAXS);
                    reader.accept(new ClassVisitor(Opcodes.ASM9, writer) {
                        public MethodVisitor visitMethod(int access, String name, String descriptor, String signature, String[] exceptions) {
                            MethodVisitor base = super.visitMethod(access, name, descriptor, signature, exceptions);
                            if (!name.equals("link") || !descriptor.equals("(Lcom/lagradost/cloudstream3/utils/ExtractorLink;)Ljava/util/Map;")) return base;
                            patched[0] = true;
                            return new MethodVisitor(Opcodes.ASM9, base) {
                                public void visitInsn(int opcode) {
                                    if (opcode == Opcodes.ARETURN) {
                                        super.visitVarInsn(Opcodes.ALOAD, 1);
                                        super.visitMethodInsn(Opcodes.INVOKESTATIC, "bingex/bridge/AudioPatch", "enrich", "(Ljava/util/Map;Ljava/lang/Object;)Ljava/util/Map;", false);
                                    }
                                    super.visitInsn(opcode);
                                }
                            };
                        }
                    }, 0);
                    bytes = writer.toByteArray();
                }
                out.putNextEntry(new JarEntry(entry.getName())); out.write(bytes); out.closeEntry();
            }
            for (String name : new String[] {"AudioPatch.class", "AudioPatch$1.class", "AudioPatch$1$1.class"}) {
                out.putNextEntry(new JarEntry("bingex/bridge/" + name));
                try (InputStream source = AudioPatch.class.getResourceAsStream(name)) { source.transferTo(out); }
                out.closeEntry();
            }
        }
        if (!patched[0]) { Files.deleteIfExists(output); throw new IOException("Expected engine audio mapping was not found"); }
        Files.move(output, input, StandardCopyOption.REPLACE_EXISTING);
    }
}
