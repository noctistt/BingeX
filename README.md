# BingeX

A modern desktop media player with local playback, a visual media library, and Stremio add-on support.

## Status

Early development preview. The initial desktop application includes local playback controls, local watch history, and an experimental Stremio add-on client. No production release is available yet.

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

The first playback prototype will validate Avalonia video embedding, fullscreen behavior, and controls before the architecture is finalized.

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

Local file history and installed add-on manifest URLs are stored in `%LOCALAPPDATA%/BingeX/library.db`. Configured manifest URLs can contain provider tokens: treat this database as private. Remote playback URLs are not saved to watch history.

The add-on preview supports HTTPS manifests, basic catalogs, series episode metadata, and direct HTTP(S) streams. Poster artwork, search/pagination, add-on subtitles, manifest resource filtering, concurrent stream lookup, torrent streaming, custom request headers, and remote watch history are not implemented yet. Windows file associations and installers are also planned work.

## License

An open-source release is intended. The project license has not yet been selected. Third-party components retain their respective licenses. BingeX is an independent project and is not affiliated with VideoLAN or Stremio.
