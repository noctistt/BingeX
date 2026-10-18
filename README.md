# BingeX

A modern desktop media player with local playback, a visual media library, and Stremio add-on support.

## Status

Planning and initial setup. No playable application or release is available yet.

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

A .NET SDK is required; installing only the .NET runtime is insufficient. Build instructions will be added with the first application scaffold.

## License

An open-source release is intended. The project license has not yet been selected. Third-party components retain their respective licenses. BingeX is an independent project and is not affiliated with VideoLAN or Stremio.
