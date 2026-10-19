# Third-party components

BingeX uses these independently maintained projects. Their licenses apply to their respective components; this file does not assign a license to BingeX.

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

The Windows packaging script copies license and notice files supplied by resolved NuGet packages into `licenses/`, alongside package manifests. Native libraries remain separate DLLs and may be replaced by compatible builds. Upstream source is available at the links above. This is a development preview; the BingeX project license remains to be selected before a public open-source release.
