# BingeX

A modern desktop media player with local playback, a visual media library, and Stremio add-on support.

## Status

Early development preview 0.13.8. The desktop app includes a visual library, searchable catalogs, movie and series pages, and an upgraded local player with track selection, subtitle controls, and fullscreen overlays. No production release is available yet.

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

Use **← Back** or **Alt+Left** in the player to return to the page you opened it from. Playback now continues in the mini-player while movie details, selected season, episode/source list and scroll position remain in place. Restore, or use the Player sidebar entry, to return to the full player. Fullscreen controls include their own Back button. On a details page, Back returns to Home. Leaving during initial torrent startup cancels that attempt while retaining already downloaded data.

## Mini-player, external audio and add-on diagnostics (0.11 preview)

- **Mini-player:** choose Mini-player in the video controls, use Back, or navigate to another app page to keep watching in the bottom-right corner. The existing video session, selected audio, subtitles and timing remain intact. Mini controls provide Pause/Play, Restore and Close. Close stops playback and saves progress; it does not remove the video or browsing page. This is an in-app player, not a separate desktop always-on-top window. During initial torrent startup, leaving still cancels preparation; once playback starts it can continue in miniature.
- **External audio:** open playback settings → Add audio file, then select the added audio track if necessary. Common audio formats and MKA are supported by the bundled VLC engine. Adjust Audio delay in seconds (positive plays later). Use a file from the same cut of the video; offsets cannot correct different edits or ongoing drift. The attachment stays through mini/full transitions, and can be reapplied when replaying the same local/direct path in the current session. Changing media or restarting the app requires loading the external audio again. Audio delay resets when opening media.
- **Installed add-ons:** Check connection requests the configured manifest and distinguishes access errors, rate limits, timeouts, invalid manifests and configuration requirements. Existing stream lookups also record whether the last title returned no streams, unsupported sources, or a request failure. Results are local observations with timestamps, not global uptime or playback guarantees. Manual manifest checks have a short cooldown.
- **Community reports:** known provider hosts link directly to their public stremio-addons.net page; unknown hosts open the directory so you can locate the correct listing. The link never contains the installed URL's private path or query tokens. Community charts stay on that site because its documented API does not expose report history. Reports may refer to another hosted instance of the same add-on. No reports are submitted automatically.

## Playback startup fix (0.11.1)

The main video surface is shown and arranged before starting VLC. Native stop/start operations run in a serialized background queue; UI polling and playback gestures wait while a native transition is active. Newer source selections supersede pending starts, preventing an old stop from interrupting a new stream. Closing the mini-player or app waits for native playback to stop before hiding or disposing its video surface. Video snapshots also run outside the UI dispatcher. Regression checks cover first HLS startup, repeated mini-to-full source changes, rapid selections, stalled HTTP recovery and closing while buffering using local fixtures; they do not certify any third-party provider's availability.

## Player layout compatibility update (0.11.2)

Mini/full dimensions are applied before showing the native overlay; controls receive the exact video-surface dimensions on resize. Playback waits for layout and two frame callbacks instead of assuming a nonzero window handle means the surface is ready. The player has an opaque black background while opening.

Windows interface rendering uses Avalonia software rendering with a redirection surface as a compatibility measure for stale owner-window drawing around the native VLC child and transparent controls. This affects the interface, with a possible CPU cost while drawing it; VLC hardware decoding and video output remain independently configured. This is a candidate mitigation for the reported misplaced black video box, stale details page and detached controls. The exact third-party-stream failure has not been reproduced locally.

The interactive Windows regression in `tests/BingeX.PlayerLayoutTests` checks native Win32 video dimensions against the controls, DPI scaling, page visibility and full/mini controls across startup, source changes, resizing and restoration. Run it with an isolated data directory and a local playable fixture.

## Responsive stream transitions (0.11.3)

Selecting a stream commits the Player page and an opaque opening state before waiting for the previous decoder to stop. Playback-state, timing and track discovery queries run on the serialized native worker; interface timers, navigation, subtitle drawing and history saves read a cached snapshot. Automatic resume seeking and saved-audio restoration also run on that worker. Poll results and queued commands from an obsolete media generation are discarded. Buffering retains the last known seek capability, and navigation during opening is preserved.

The native UI regression now renders 100 source cards and injects a two-second native-operation delay. The previous 0.11.2 build fails immediate navigation under this delay; the updated build keeps the Player page visible and the dispatcher responsive. Checks also cover normal/maximized mini transitions, pause/resume, seek-to-end, replay, saved alternate audio when available, and a failed HTTP source falling back to a local fixture. These controlled checks do not certify a third-party stream's startup time.

Slow startup stages and UI-dispatch delays (250 ms or more) are recorded locally in `playback-performance.log` inside the BingeX data directory. Logging runs in the background, rotates at 64 KiB with one previous file, and records fixed stage names and durations only, without media names, URLs or credentials. These best-effort timings help distinguish native decoder waits from actual interface stalls.

## Native video background fix (0.11.4)

The Windows video host now has a black native background brush. Avalonia's default child window had no brush, so visiting Player before loading any video retained the previous page's pixels underneath the transparent controls, even though that page was logically hidden. This exact empty-player case was reproduced locally and visually verified after the fix. Native host bounds are also updated immediately after page layout, instead of waiting for a queued resize.

The native UI regression visits the empty Player from Home, My Library, Details and Add-ons before playing any media, with normal/maximized resizing. It checks previous-page visibility, native background brush and native dimensions, then runs the existing playback and mini-player checks.

## Discover by add-on and playback waiting (0.12.0)

Discover now has named add-on tabs and a second row of categories supplied by the selected add-on, such as Movies, Series or Anime. The catalog picker and search are scoped to that provider/category. Search-only categories show a search prompt rather than requesting an unsupported browsing endpoint. Switching tabs cancels old requests so a slow provider cannot replace the current results. Stream-only add-ons do not supply Discover catalogs; their streams remain available on title pages.

A centered, non-blocking waiting indicator appears in the full player and mini-player when VLC reports buffering. It also acknowledges an unchanged playback clock after two seconds while the player reports Playing. That clock-based fallback is a heuristic, not a network-speed measurement; it cannot detect every video-only stall if the playback clock continues. The indicator clears on progress, intentional pause, new media or seeking and never pauses/resumes the decoder itself. Initial startup retains the opening screen.


## Cloudstream extensions (0.13.0 experimental)

Open Add-ons → Cloudstream · Experimental, download the optional bridge, then enter an HTTPS repo.json URL or GitHub repository page and choose Read repository. Install a trusted extension, then use Browse providers or select it under Home → Discover. Extensions are loaded dynamically; none are hardcoded or bundled. Stremio add-ons remain a separate installation flow.

The first bridge download includes a checksum-pinned external SkyStream engine and Temurin Java, stored under the BingeX profile. No system Java installation is needed. Bridge components and extensions are SHA-256 checked. Sources, license links and implementation limits are documented in [docs/cloudstream-bridge.md](docs/cloudstream-bridge.md). Browser verification, extension settings, DRM, arbitrary request headers and automatic updates are not supported yet. Some providers may offer search even if their home feed is unavailable. Install only extensions you trust: they run code with your user permissions.


### Stream audio languages (0.13.1)

Cloudstream streams with separate audio tracks now retain their language alternatives. Open Player settings → Audio → Stream audio language. The original audio is preferred when marked by the provider, repeated encodings of the same language are collapsed, and a selected language is remembered for that video. Languages load on demand through the existing player; no provider is hardcoded. Tracks needing custom headers remain unsupported.


### Provider playback headers (0.13.2)

Cloudstream video sources can now carry additional valid HTTP headers through a private loopback relay. HLS master and media playlists, segment URLs, encryption-key URIs, relative paths and redirects are handled; byte ranges and HEAD remain available. Cookies and Authorization are restricted to the original upstream origin. The relay is cancelled before stopping or changing the player. It runs locally and stores no stream URLs or header values in logs. DRM, browser verification and DASH manifest rewriting remain outside this feature.


### Extension subtitles and shared title IDs (0.13.3)

Subtitles returned with a Cloudstream stream appear in the normal CC track picker as Extension entries. Selecting one downloads and parses the caption with its supplied headers; video and episode changes clear the list. SRT/VTT and the existing subtitle controls are used. Providers that return no subtitle files cannot supply these entries.

Cloudstream metadata IMDb IDs and explicit season/episode numbers now let Stremio stream and subtitle add-ons query the same title while the original extension remains available. If no shared ID is supplied, Match title for other add-ons offers editable catalog search and explicit title selection. Confirmed matches persist locally. Unknown episode numbering is never guessed, and unrelated Cloudstream extensions are not automatically searched for alternative site-specific IDs.

### Compact browsing (0.13.4)

Continue Watching shows recent unfinished videos newest first in one horizontal row. Use the arrows or scrollbar to see older entries. Cloudstream extensions appear in alphabetical card grids, separated into Installed and Available. Paste HTTPS, GitHub repository pages, or cloudstreamrepo:// repository links directly into Read repository.

### Online mode (0.13.5)

The Online mode switch beside Open media is off by default and saved immediately for the next launch. Off hides Discover, Add-ons, online subtitle search, and online Continue Watching entries. Local files, folders, embedded tracks and subtitle files remain available. Switching off cancels catalog browsing but keeps installed providers, history, title matches and settings; existing playback continues. This is a feature visibility preference, not a network firewall.

### Subtitle timing (0.13.6)

Extension and external text subtitles retain a stable interpolation anchor when VLC repeats timestamps, avoiding backwards jumps across cue boundaries. Interpolation follows playback speed, freezes on pause/buffering, resets for seeks and media changes, and stops extrapolating after 750 ms without an advancing timestamp. Caption refresh runs about 30 times per second and text changes only when the active cue changes. Release-specific subtitle offsets may still need the existing delay control.

### Repeated captions (0.13.7)

Small native timestamp corrections and slowly advancing samples no longer rewind the text subtitle clock or compound prediction. Identical SRT/VTT cues that overlap or have a gap of at most 100 ms are merged into one continuous cue. Different overlapping dialogue and repetitions after longer pauses remain separate.

### Episodes beside streams (0.13.8)

Title pages use independent scrolling panes: episode cards on the left, stream details on the right, with the synopsis above. Episode titles, overviews and stream descriptions wrap without line truncation. The selected episode is highlighted; choosing another resets the stream pane to the top while keeping your place in the episode list. Titles with video entries use the episode pane even if their provider labels them as movies. Movies without entries use the full width for streams.
