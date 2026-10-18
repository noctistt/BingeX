# BingeX

A modern desktop media player with local playback, a visual media library, and Stremio add-on support.

## Status

Early development preview 0.3. The desktop app includes a visual library, searchable catalogs, movie and series pages, and an upgraded local player with track selection, subtitle controls, and fullscreen overlays. No production release is available yet.

## What we are building

- A modern interface with dark colors, poster browsing, and accessible playback controls.
- Local video and audio playback powered by LibVLC.
- A library with favorites, watch history, and Continue Watching.
- Existing Stremio add-ons for catalogs, metadata, streams, and subtitles.
- Subtitle timing, audio-track selection, playback speed, and keyboard shortcuts.
- Windows file associations and an installer when the app is ready.

## Planned stack

- **C# / .NET** for application logic.
- **Avalonia** for the desktop interface, starting with Windows.
- **LibVLCSharp / LibVLC** for playback.
- **SQLite** for local library and playback state.
- **HTTP / JSON** for the Stremio add-on protocol.

The playback prototype has verified native video rendering, pause, duration, and seeking. The visual library builds on that foundation.

## First milestones

1. Set up the .NET SDK and create the desktop application.
2. Open a local file, play/pause, seek, change volume, and enter fullscreen.
3. Save playback position and resume after restarting.
4. Install a Stremio add-on from its manifest URL and browse its catalog.
5. Open a title, choose a supported stream, and play it.
6. Add library browsing, subtitles, and Windows packaging.

## Compatibility

Media support depends on the LibVLC build and included codecs; no player supports every possible file. Stremio add-on compatibility does not automatically provide support for every returned source. Torrent-based streams require a separate streaming component and are outside the initial playback milestone.

## Development

A .NET 10 SDK is required; installing only the .NET runtime is insufficient. The bundled native playback dependency currently targets Windows x64.

```powershell
dotnet build src/BingeX/BingeX.csproj -c Release
dotnet run --project src/BingeX/BingeX.csproj
```

You can also use `./run.ps1`, or pass a workspace SDK with `./run.ps1 -DotnetPath 'path/to/dotnet.exe'`.

Run persistence and protocol smoke checks with `dotnet run --project tests/BingeX.SmokeTests`. Optionally append `-- --live-addon` to check the public Cinemeta catalog, or append `-- 'path/to/test-video.avi'` to test native decoding, pause, and seeking with a known seekable clip.

Open media using **Open media**, drag a local file onto the app, or pass a filename as a command-line argument. Dropping a subtitle file loads it into the current video; dropping media and subtitles together opens the first media file with the first subtitle file.

### Player controls

- The **CC** button opens audio and subtitle settings. The **settings** button also exposes playback speed. These settings select embedded audio and subtitle tracks, switches subtitles off, and loads external files.
- External **SRT / WebVTT** captions support live size, color, background, and timing changes. Embedded and ASS/SSA/image subtitle tracks retain their native styling. Subtitle delay applies to both native tracks and custom text captions; positive delay shows captions later.
- Fullscreen removes the sidebar and page chrome. Controls and the pointer hide after three idle seconds while playing, and return on mouse movement. Controls stay visible when paused, finished, or interacting with settings.
- Space toggles pause or replays a completed video; Left/Right skip ten seconds; F/F11 toggles fullscreen; S opens playback settings; M toggles mute; G/H shifts subtitles earlier/later by 50 ms. Escape closes settings first, then exits fullscreen. Double-clicking the video also toggles fullscreen.
- Volume and text subtitle appearance persist between sessions. Per-video subtitle delay resets for a new file.
- Resume uses stable local paths or catalog/episode IDs. Completed videos restart from the beginning, short clips use a proportional completion margin, and non-seekable streams play from the start.

To open a video with an external subtitle file from the command line:

```powershell
dotnet run --project src/BingeX/BingeX.csproj -- 'C:/media/video.mkv' --subtitle 'C:/media/subtitles.srt'
```

For isolated development checks, set `BINGEX_DATA_DIRECTORY` to a separate writable data folder before launching.

Local file history and installed add-on manifest URLs are stored in `%LOCALAPPDATA%/BingeX/library.db`. Configured manifest URLs can contain provider tokens: treat this database as private. Remote playback URLs are not saved to watch history. Streamed titles and episodes use stable media IDs so Continue Watching can find fresh sources before resuming. Local video thumbnails are stored in the adjacent `thumbnails` folder.

## Browsing and add-ons

- Use **Connect movie catalog** to install Cinemeta, or install a configured HTTPS manifest in **Add-ons**.
- Pick an installed catalog to browse posters. **Search** queries catalogs that declare search support, across both movies and series. Results are deduplicated by media type and ID.
- Open a poster for the synopsis, release information, genres, and rating when provided. Series have a season picker and episode cards.
- **Find streams** queries compatible installed providers concurrently. Direct HTTP(S) sources can play; unsupported sources show an explanation. Cinemeta supplies metadata, not playable movies.
- Navigation cancels stale requests. Provider failures leave healthy results available, and loading, empty, and retry states are shown separately.
- Continue Watching shows local thumbnails or provider posters with saved progress. Streamed items ask for a new source before resuming, so expired playback links are not reused.

Browsing currently displays the first 100 titles per catalog; combined search is capped at 120 results. Pagination, required genre/date filters, add-on subtitles, torrent streaming, custom request headers, and favorites are planned work. Artwork requests use HTTPS and a bounded in-memory cache; missing images have a fallback.

## License

An open-source release is intended. The project license has not yet been selected. Third-party components retain their respective licenses. BingeX is an independent project and is not affiliated with VideoLAN or Stremio.


## Windows preview package

The compact player toolbar has tooltips, accessible button labels, a violet timeline, and icon controls that update for pause/replay, mute, and fullscreen. Speed lives in playback settings. The main window retains Open media; fullscreen keeps the playback toolbar compact.

Build a self-contained Windows x64 package and installer with PowerShell 7, .NET 10 SDK, and [Inno Setup 6](https://jrsoftware.org/isdl.php):

```powershell
./packaging/build-windows.ps1
# Or provide explicit tool/output paths:
./packaging/build-windows.ps1 -DotnetPath 'path/to/dotnet.exe' -IsccPath 'path/to/ISCC.exe' -OutputDirectory 'path/to/output'
```

`-PortableOnly` skips installer compilation. Run `win-x64/BingeX.exe` from the published folder; keep its supporting files together. The runtime is bundled. Installer builds are currently unsigned development previews.

Setup installs for the current user, adds a Start menu shortcut, and offers an optional desktop shortcut and Open with registration for common video/audio extensions. Registration adds BingeX as a candidate in Open with and Default apps; choose it in Windows to make it the default. Setup does not overwrite existing default-player choices. Uninstall removes its program files, shortcuts and registrations, while preserving the library/history in `%LOCALAPPDATA%/BingeX`.

A second launch forwards its file and optional `--subtitle` to the running window instead of opening another player. A launch without a filename activates the existing window. Separate `BINGEX_DATA_DIRECTORY` values keep development instances independent. The secondary process returns a nonzero exit code if the existing instance cannot be reached within six seconds.
