# Optional engine audio mapping patch

The pinned SkyStream engine maps ExtractorLink into JSON without its `audioTracks` property. This original Java patch adds the generic audio URL/header contract to that map. No provider is special-cased. The patch checks the exact method signature and fails if it is unavailable. All three compiled classes are included in the patched engine to satisfy JVM verification.

Build with JDK 17 or newer and ASM 9.10.1 from the checksum-pinned engine assets:

```
javac --release 17 -cp <engine>/asm-9.10.1.jar -d <classes> AudioPatch.java
jar --create --file ../../src/BingeX/Assets/cloudstream-patch.jar -C <classes> .
```

The committed JAR contains only these original classes, not ASM or upstream engine code. At runtime BingeX downloads the original engine and Java separately with SHA-256 checks, then runs the patcher with **only** the patch JAR and ASM JAR on its classpath (putting engine-core on the patcher's classpath locks that file on Windows). The resulting engine is kept in the optional runtime folder. Upstream GPL source/license links are written there. No extension is included in the installer.

Original BingeX patch source: GPL-3.0-or-later; see ../../LICENSE and ../../COPYRIGHT.md. The source change preserves audio-track URL/header data in the upstream engine JSON output. Upstream and ASM retain their own licenses.
