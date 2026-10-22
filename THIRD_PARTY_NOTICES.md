# Third-party components

BingeX uses these independently maintained projects. Their licenses apply to their respective components; BingeX original work is GPL-3.0-or-later (see LICENSE and COPYRIGHT.md).

- TagLibSharp 2.3.0: LGPL-2.1-only — https://github.com/mono/taglib-sharp/tree/TaglibSharp-2.3.0.0
- MonoTorrent 3.0.2: MIT — https://github.com/alanmcgovern/monotorrent/tree/release-v3.0.2
- Mono.Nat: MIT — https://github.com/alanmcgovern/Mono.Nat
- ReusableTasks: MIT — https://github.com/alanmcgovern/ReusableTasks
- Avalonia: MIT — https://github.com/AvaloniaUI/Avalonia
- LibVLCSharp: LGPL-2.1-or-later — https://code.videolan.org/videolan/LibVLCSharp
- LibVLC 3.0.24 and its native plugins: upstream component licenses, including LGPL-2.1-or-later — https://code.videolan.org/videolan/vlc/-/tree/3.0.24
- .NET and Microsoft.Data.Sqlite: MIT, with third-party notices — https://github.com/dotnet/runtime and https://github.com/dotnet/efcore
- SQLite: public domain — https://sqlite.org/copyright.html
- SQLitePCL.raw: Apache-2.0 — https://github.com/ericsink/SQLitePCL.raw
- SkiaSharp and HarfBuzzSharp: MIT, with third-party native library notices — https://github.com/mono/SkiaSharp
- ANGLE native assets: upstream notices are included with the package — https://github.com/AvaloniaUI/angle

The Windows packaging script copies license and notice files supplied by resolved NuGet packages into `licenses/`, alongside package manifests. Native libraries remain separate DLLs and may be replaced by compatible builds. Upstream source is available at the links above. See docs/dependency-licenses.json for resolved package metadata and docs/licensing.md for source access and remaining native/optional-runtime review. Package metadata is not a complete audit of every native plugin.


## Optional Cloudstream compatibility runtime

The pinned SkyStream checkout at f5523ae07d729b579be30976df8783a0b98c4559 contains GPL version 3 license texts at its root and packages/cs3_engine/LICENSE. Source and build instructions: https://github.com/akashdh11/skystream/tree/f5523ae07d729b579be30976df8783a0b98c4559/packages/cs3_engine . Its constituent JAR dependencies retain their own licenses; the entire runtime must not be assumed to share one license.

Temurin Java is downloaded separately; retain its java/legal directory and consult the notices and source corresponding to the exact version in src/BingeX/Assets/cloudstream-java.json. The original BingeX audio patch is GPL-3.0-or-later; its source and build instructions are in tools/cloudstream-patch. No third-party extensions are included.
