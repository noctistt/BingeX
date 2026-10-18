# BingeX

A modern desktop media player with local playback, a visual media library, and Stremio add-on support.

## Status

Early development preview 0.2. The desktop app includes a poster library, searchable add-on catalogs, movie and series detail pages, season/episode selection, source selection, and local playback. No production release is available yet.

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

Open media using **Open media** or pass a local filename as a command-line argument. In the Player page, Space toggles pause, Left/Right skip ten seconds, F toggles fullscreen, and Escape exits fullscreen. Volume, seeking, speed, and external subtitle files have visible controls.

Local file history and installed add-on manifest URLs are stored in `%LOCALAPPDATA%/BingeX/library.db`. Configured manifest URLs can contain provider tokens: treat this database as private. Remote playback URLs are not saved to watch history. Streamed titles and episodes use stable media IDs so Continue Watching can find fresh sources before resuming. Local video thumbnails are stored in the adjacent `thumbnails` folder.

## Browsing and add-ons

- Use **Connect movie catalog** to install Cinemeta, or install a configured HTTPS manifest in **Add-ons**.
- Pick an installed catalog to browse posters. **Search** queries catalogs that declare search support, across both movies and series. Results are deduplicated by media type and ID.
- Open a poster for the synopsis, release information, genres, and rating when provided. Series have a season picker and episode cards.
- **Find streams** queries compatible installed providers concurrently. Direct HTTP(S) sources can play; unsupported sources show an explanation. Cinemeta supplies metadata, not playable movies.
- Navigation cancels stale requests. Provider failures leave healthy results available, and loading, empty, and retry states are shown separately.
- Continue Watching shows local thumbnails or provider posters with saved progress. Streamed items ask for a new source before resuming, so expired playback links are not reused.

Browsing currently displays the first 100 titles per catalog; combined search is capped at 120 results. Pagination, required genre/date filters, add-on subtitles, torrent streaming, custom request headers, favorites, Windows file associations, and installers are planned work. Artwork requests use HTTPS and a bounded in-memory cache; missing images have a fallback.

## License

An open-source release is intended. The project license has not yet been selected. Third-party components retain their respective licenses. BingeX is an independent project and is not affiliated with VideoLAN or Stremio.
