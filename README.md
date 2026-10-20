# BingeX

A modern desktop media player with local playback, a visual media library, and Stremio add-on support.

## Status

Early development preview 0.10. The desktop app includes a visual library, searchable catalogs, movie and series pages, and an upgraded local player with track selection, subtitle controls, and fullscreen overlays. No production release is available yet.

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

- The **CC** button opens subtitle selection, external subtitle loading, timing, and appearance. The **settings** button opens playback speed and audio track selection. Press **C** to turn subtitles off/on; turning them back on restores the last selected track when available.
- External **SRT / WebVTT** captions support live size, color, background, and timing changes. Embedded and ASS/SSA/image subtitle tracks retain their native styling. Subtitle delay applies to both native tracks and custom text captions; positive delay shows captions later.
- Fullscreen removes the sidebar and page chrome. Controls and the pointer hide after three idle seconds while playing, and return on mouse movement. Controls stay visible when paused, finished, or interacting with settings.
- Space toggles pause or replays a completed video; Left/Right skip ten seconds; F/F11 toggles fullscreen; S opens playback settings; C toggles subtitles; M toggles mute; G/H shifts subtitles earlier/later by 50 ms. Escape closes settings first, then exits fullscreen. Double-clicking the video also toggles fullscreen.
- Audio track choices persist per video and restore after reopening, once tracks become available. Remote titles use stable episode IDs; if a saved track is missing, playback keeps the available default. A new manual selection overrides pending restoration.
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
- **Find streams** queries compatible installed providers concurrently. Direct HTTP(S) and BitTorrent v1 infoHash sources can play; unsupported sources show an explanation. Cinemeta supplies metadata, not playable movies.
- Navigation cancels stale requests. Provider failures leave healthy results available, and loading, empty, and retry states are shown separately.
- Continue Watching shows local thumbnails or provider posters with saved progress. Streamed items ask for a new source before resuming, so expired playback links are not reused.

Browsing currently displays the first 100 titles per catalog; combined search is capped at 120 results. Pagination, required genre/date filters, custom request headers, and favorites are planned work. Artwork requests use HTTPS and a bounded in-memory cache; missing images have a fallback.

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


## Search online for subtitles

Open **CC → Search online…**. Search a movie or series name (or enter its IMDb ID), choose the matching title, filter by language, and press **Load**. For series, choose the season and episode before searching. Catalog playback also offers **Find for this video**, using its existing movie or episode ID. Local filenames supply an editable title hint; the app never sends the full local path.

Results come from installed Stremio add-ons that declare the `subtitles` resource, plus the official OpenSubtitles v3 add-on. An installed configuration for that provider takes precedence over the included endpoint. Title search uses a compatible connected catalog, with Cinemeta as a fallback when none is available. Provider availability and subtitle synchronization vary; use G/H or the delay control to adjust timing.

Downloads load readable SRT / WebVTT captions into memory, without saving subtitle files or playback URLs to history. The existing caption appearance controls and C toggle work on downloaded subtitles. This preview accepts HTTPS subtitle links, follows up to five redirects, limits files to 8 MB, and cancels stale requests when the video/search changes or the panel closes. Archives, ASS downloads, provider login flows, and custom request headers are not supported by online search yet; local subtitle loading remains available.

Run offline protocol/download checks with the smoke test project. Append `-- --live-subtitles` for an optional live search and download from the official subtitle add-on.


## My Library and Continue Watching

Choose **My Library** in the sidebar, then **Add folder**. BingeX scans common video formats in that folder and its subfolders in the background. Added folders and discovered files are saved locally; startup scans and **Rescan folders** pick up changes. Search by filename/subfolder, filter by an added folder, and select a video to play it. Overlapping folders show each physical path once. Large libraries initially show 100 results; **Show more videos** adds the next batch.

Files stay in their original locations. **Remove folder** removes that folder's index entries while preserving physical files and playback history. Missing drives and missing files retain cached entries and show an unavailable label after scanning; reconnect or restore them and rescan. Junctions and symlinks are skipped to avoid loops and scanning outside the selected tree. New files appear on the next scan; continuous filesystem watching is not included yet.

Episode filenames using S01E02 or 1x02 are grouped by show and season, with numeric episode order. Season folders with Episode 01 filenames are recognized too; other videos stay grouped by subfolder. Choose Name, Recently added (first discovery), or Last watched sorting. Cards show filenames, file sizes, and saved playback progress. While My Library is open and playback is stopped, thumbnails are generated silently in the background for displayed videos, one at a time. Failed or damaged files keep their placeholder. Thumbnail images are cached locally. Automatic movie posters/title matching and dedicated show detail pages remain future work.

On **Home**, the small **×** in a Continue Watching card removes that item's history/progress without deleting the video, subtitle settings, or saved audio-track choice. If the item is still playing, progress saving is suppressed for that playback so the dismissed card does not immediately reappear. Explicitly opening the video again allows a new history entry.

Click the video picture to pause or resume; double-click toggles fullscreen without changing playback. Clicks on the controls/settings and pointer drags do not toggle playback. A single click waits briefly for the system double-click interval.

## Torrent playback (0.8 preview)

Choose **Stream torrent** on a compatible add-on result, or open/drop a local `.torrent` file. BingeX uses MonoTorrent 3.0.2 to fetch metadata and verified video pieces, and serves verified bytes to LibVLC through a private loopback HTTP range endpoint. Seeking cancels the obsolete read without replacing the torrent session; completed files are served directly from disk. Provider `fileIdx` is preserved; when absent, the largest supported video is selected. Tracker and IPv4 peer hints are used immediately for metadata discovery. The same peer connections continue into streaming. Hash-only add-on results without trackers receive a small built-in public tracker fallback list; .torrent files keep their own tracker configuration. Torrent metadata, verified-piece resume information and DHT nodes are cached across sessions for each torrent. Subtitle and audio controls continue to use the same player.

During metadata lookup, the player shows elapsed time, connected peers, successful tracker replies and DHT state/node count. File download percentages appear only once metadata is available. The player then shows torrent state, selected-file download percentage, download rate, connected peers, and **Stop torrent**. The percentage is the file download progress, not a guarantee of playable buffer duration. Startup and stalled reads time out after two minutes. Stop, switching sources, reaching the end, and closing release playback while preserving downloaded data. Reopening the same torrent reuses its metadata and verified pieces; incomplete data may need a local hash check after a crash. Watch history keeps the stable show/episode identity and audio choice; resuming an add-on title still looks up sources, and selecting the same torrent reuses its cache.

Saved video caches are stored under `%LOCALAPPDATA%/BingeX/torrent-cache-v1`. One session is active at a time. Caches are keyed by torrent hash, with selected file indexes preserved by playback. The total cache budget is 32 GiB, reserving space for the **entire torrent**, including unselected files; bigger season packs are rejected rather than silently overfilling the cache. Older inactive caches are removed when space is needed; the active cache is protected. **My Library → Clear unused video cache** clears inactive downloads. Only the selected file is prioritized. Uploads are limited to 256 KiB/s while the session is active, and stop when the session ends. Peer-to-peer connections share your IP address with peers; using an add-on does not make this traffic anonymous. Port forwarding is disabled. V2-only hashes, magnet-URL input, private-tracker login flows, archive extraction, and custom stream headers are not supported in this preview. An available listing does not guarantee healthy peers or a valid video.

Version 0.8.1 keeps a single torrent session from metadata discovery through playback. Payload writes wait behind a disk-budget/path validation gate until metadata has been checked. Startup buffering explicitly prioritizes the selected file, first fetching its tail piece and then a small starting buffer.

## Suggested sources and buffering (0.9 preview)

**Try suggested source** starts the first supported result in a balanced order: direct links, reported quality/size, advertised seed counts, and measured successes/failures from this app session influence ranking. Matching torrent hash/file-index results are deduplicated. Provider hints can be missing or stale; this order does not claim that untested links work. Source lists label reported seed counts and keep unknown values unknown. Measured results stay in memory only.

**Try another source if this one stalls** is enabled by default and can be turned off on the source list. On a startup failure or 35 seconds without data progress, BingeX offers another distinct source with an eight-second countdown, **Try now**, and **Cancel switch**. It makes at most three total attempts for the same title/episode, never retries a source within that sequence, and cancels a pending switch when you leave the player, stop, or open other media. A stalled decoder read can also offer a new source. Different sources may have different audio languages/edits; the next provider/source name is shown before switching. Resume and saved audio choices continue to use the same title/episode history identity.

Torrent startup builds an 8 MiB verified starting buffer when runtime is unknown. With catalog runtime, the target adapts to roughly twelve seconds at average bitrate, bounded to 4–32 MiB and capped at file size. It also fetches the tail piece required for container information. It advances requests in small chunks to prioritize video in sequence. Buffer progress is displayed separately from whole-file download percentage. During playback the app reports contiguous verified bytes ahead of the decoder, an estimated duration once the player knows the video length, a rolling measured delivery rate, and the approximate average rate the file requires. Time estimates assume average bitrate and are not exact playable time for variable-bitrate video; data already buffered inside the player is not counted. A completed file is labeled fully downloaded. These changes do not increase an unavailable swarm's upload capacity or guarantee smooth playback on a slow connection.

## Back navigation (0.10 preview)

Use **← Back** or **Alt+Left** in the player to return to the page you opened it from. Playback pauses, while movie details, selected season, episode/source list and scroll position remain in place. The Player sidebar entry returns to the paused video. Fullscreen controls include their own Back button. On a details page, Back returns to Home. Leaving during initial torrent startup cancels that attempt while retaining already downloaded data.
