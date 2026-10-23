> Music update (0.14.13): Echo integration is archived and removed from the active application. Historical Echo notes below describe past development. See [archive and restoration](archive/echo/README.md) and [MusicFree setup and test scope](docs/musicfree-evaluation.md). Local music, artwork, playback controls and OpenSubsonic connections are retained.

# BingeX

A desktop media player for local files and network streams, powered by LibVLC, with a visual library and optional Stremio add-on and Cloudstream extension compatibility.

BingeX is an independent personal development project. No media catalog, streaming extension, media files, or service credentials are bundled. Online features are disabled by default; users choose and configure third-party providers. See [project scope and distribution](docs/project-scope.md) for built-in endpoints, privacy and licensing limits.

## Status

Early development preview 0.14.7. The desktop app includes a visual library, searchable catalogs, movie and series pages, and an upgraded local player with track selection, subtitle controls, and fullscreen overlays. No production release is available yet.

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

Media support depends on the LibVLC build and included codecs; no player supports every possible file. Stremio add-on compatibility does not automatically provide support for every returned source. Optional BitTorrent playback uses MonoTorrent and is disabled by default in source searches. Provider availability, media rights, authentication and compatibility remain independent of the player.

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

BingeX original work is licensed under **GPL-3.0-or-later**. See [LICENSE](LICENSE), [copyright and scope](COPYRIGHT.md), and [distribution requirements](docs/licensing.md). Third-party components retain their respective licenses. BingeX is an independent project and is not affiliated with or endorsed by VideoLAN, Stremio, Cloudstream or third-party providers. Compatibility names identify the software/protocols supported; their owners retain their trademarks.


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

### Next episode (0.13.9)

Player controls show Next episode when the current title has a following entry. Playback settings include opt-in, persisted Autoplay next episode with an eight-second cancelable countdown after completion. Next episode queries the installed providers and prefers a playable source from the current provider; when none is available, the next episode and stream retry controls remain visible. Verified numbering determines episode/season order; unnumbered channels retain provider order. Browsing a different title does not change the playing title's episode list.

### Duplicate episodes (0.13.10)

Episode cards and Next episode use one shared list grouped by reliable season/episode numbers. Alternate provider IDs are retained for stream lookup and existing history; both copies of E01 now advance to E02. Shared-ID queries are deduplicated per provider while distinct extension payloads remain available as source choices. For unverified numbering, grouping requires the same displayed season/episode, title and nonempty synopsis; matching titles alone are not merged. Starting a stream lookup clears the previous result pool so failed or cancelled lookups cannot reuse old sources.

### Progressive loading and subtitle preferences (0.13.11)

Catalog providers and stream providers report results individually. Early catalog posters and playable sources are shown while slow providers remain pending; source rows append without shifting existing choices. Next-episode playback can begin from an early supported result. Catalog pages are cached in memory for five minutes; Refresh invalidates the cache. Stream URLs are freshly resolved. The Cloudstream helper still serializes its native extension requests, on a separate queue so waiting extensions do not occupy Stremio HTTP slots.

Selecting an extension or online subtitle saves its language per title. Following episodes download their own matching caption without opening settings. Embedded subtitle labels/languages and Off are also remembered. Missing preferred captions leave CC available for manual selection; local subtitle files are not reused as captions for a different episode. No subtitle URL is stored in preferences.

### Player controls and preparation states (0.13.12)

Player controls have consistent button sizes, a compact next-episode icon, track counts and a mini-player action. Audio & playback and Subtitles tabs stay within the same settings panel. Track menus distinguish embedded, downloaded and local-file captions while retaining the original track names. Opening a video names the title and source; next-episode lookup shows a preparation card with Cancel autoplay. Cancel stops pending lookup from starting a late source. Native video layout and decoding behavior are unchanged.

### Subtitle picker and title action visibility (0.13.13)

Online caption text is fetched and parsed in memory, including preferred captions restored for following episodes; it is not saved as a subtitle file. The selected track label now says Online rather than Downloaded. CC opens a compact subtitle-track picker with a link to full subtitle settings. The settings icon keeps the full audio/playback and subtitle tabs. Find streams sits in the stream pane rather than inside the height-limited synopsis, so long descriptions cannot clip the action.

### Extension stream scope (0.13.14)

Extension title pages include Search all providers, off by default and saved across restarts. Off resolves streams only through the originating extension, retaining its duplicate-episode source variants. On also queries compatible installed Stremio providers through the existing title-ID mapping. Unrelated Cloudstream extensions are not queried with another site's IDs. Normal catalog titles continue to query Stremio providers. Source scope does not remove providers or disable online subtitle searches.

### Audio-type filters and show language (0.13.15)

Source lists include All, Subbed, Dubbed, Dual audio and Unknown filters with counts. Classification uses explicit source labels or structured audio-language alternatives; quality and subtitle language do not prove spoken audio. Filter selection is saved per title and applies to suggested playback, next episode and source retries. Unlabelled or conflicting single-file labels remain Unknown. Provider labels are descriptions, not verified media analysis.

A selected audio language is saved per show. Manually choosing a source with one explicitly labelled audio language also remembers that language. Following episodes match their own separate audio URLs or native audio track labels/languages; prior episode URLs and native track IDs are not reused across episodes. Existing per-video choices retain priority. Automatic source selection prefers a labelled language match and excludes explicitly different languages; sources without audio-language metadata remain eligible. Missing or ambiguous track-language matches keep available playback rather than guessing a different language.

### 0.13.16
Opening a different title or season clears previous stream choices and audio filters immediately. Retired controls and cancelled lookups cannot restore or play the previous title’s sources.

### 0.13.17
Stremio add-on streams can supply validated custom request headers through the existing local playback relay, including nested HLS requests and byte-range seeking. Malformed headers and unsupported response-header overrides remain explained.

### 0.13.18
Stream searches have a remembered Include torrent sources checkbox, off by default. Hidden torrent sources are excluded from automatic playback and suggestions. Try suggested torrent only selects peer-to-peer torrents; direct streams remain manually selectable. Mixed providers may still be queried to obtain their direct streams.

### 0.13.19
Audio type starts at All for each new title or episode search. Manual filters apply to the current results, including progressively arriving sources, without restoring old restrictive filters. Preferred spoken audio language remains remembered.

### 0.13.20
Stream results have combined provider, resolution and audio-language filters with Unknown options, match counts and Reset filters. Every search starts at All. Source cards label direct versus torrent playback while retaining descriptions.

### 0.13.21
Next-episode playback waits for the previous provider and matches known resolution and audio language. Reused stream identities and conflicting episode labels are rejected. Without a match, sources remain visible for manual selection rather than starting an unrelated alternative. Provider metadata cannot verify the actual video contents.

### 0.13.22
Player source recovery offers a direct route to the current title/episode sources and persistent unavailable/timeout notices. Same-episode source changes preserve the last known position even after buffering or errors, and report sources that cannot seek.

### 0.13.23
My Library offers Folder, Series and season, and All videos views with collapsible groups. View and sort choices are remembered. Search matches words across filenames and folder paths, including dotted/underscored names, with a Clear action and a short typing delay. Files stay where they are.

### 0.13.24
Refined purple-and-mint app icon, matching sidebar branding and a brief non-blocking startup logo fade. Click to skip; opening a video directly bypasses the animation.

### 0.13.25
A dedicated Settings page controls seek interval, resume, click-to-pause, online episode autoplay, source recovery offers, playback speed/volume, text captions, library defaults and startup animation. Changes apply with Save settings; quick player controls remain available. The ? key (Shift+/ on a US keyboard) opens the shortcuts reference outside text-entry controls.

### 0.13.26
Settings includes preferred spoken audio and subtitle languages, source-default and subtitles-Off options. Saved video/show choices and manual track selection take priority. Defaults apply to newly opened media; embedded captions are tried before optional online captions for remote titles. Missing labelled tracks produce a fallback notice.

### 0.13.27
Settings → Storage measures torrent caches and generated thumbnails separately, with immediate cleanup and refresh actions. Locked torrent caches and the active local thumbnail are retained. Totals sum file lengths, not physical allocation for sparse files; incomplete measurements are labelled. Media files, history and providers are not removed.

### 0.13.28
Appearance settings offer Purple, Blue, Teal, Rose and Amber accents, comfortable/compact catalog and local-library cards, and reduced decorative animation. A sample preview updates before saving. Saved accents update control highlights without changing artwork, video or subtitle colours. Reduced animation skips the startup fade while retaining loading indicators.

### 0.13.29
Settings can export saved preferences to a versioned JSON file, preview an import, or preview a reset before applying it. Backups explicitly exclude provider links, credentials, media paths and watch history. Online mode and saved per-show/track choices are not reset. Normal updates keep preferences; exporting first is an optional precaution.

### 0.13.30
A session-only local queue supports multiple files, recursive video-folder loading, reorder/remove/clear, previous/next actions and separately configurable autoplay. Folder entries use natural numeric ordering; automatic continuation skips missing files. Queue editing never deletes files or stops current playback. Online streams do not advance the local queue.

### 0.13.31
Queue lives in a collapsible panel inside My Library, with a button beside folder actions. The player Queue button opens the same panel. Long queues have bounded scrolling; the extra sidebar entry is removed. Queue editing and autoplay behaviour are unchanged.

### 0.13.32
My Library removes the separate full-path folder list. Registered folder headings in the video area now include Remove folder, with subfolder/series groups nested underneath. Full paths remain in tooltips. The folder filter also provides removal for All videos view and empty folders. Removal only unindexes the registered folder and its children; files and watch history are kept.


### 0.14.0 — Music library and personal music servers

My Library has Videos and Music tabs. Add a music folder to index tags, embedded cover art, album/artist, disc/track order and audio properties without changing files. Local music supports the existing session queue, next/previous controls and optional autoplay. Search is debounced; album and track rendering uses Show more. Embedded artwork is limited to 2 MiB per image and a 256 MiB extracted-art cache. Music folders and the index persist separately from video folders; removal keeps source files. Failed or incomplete scans preserve previously indexed tracks. Rescan is manual.

With Online mode enabled, Music can connect to a user-configured Navidrome/OpenSubsonic server, browse/search albums with pagination and play tracks through the private header relay. Username/password and authenticated URLs remain session-only; no streamed music URLs are saved to history or audio-track preferences, including on replay. HTTPS is required except loopback or explicitly allowed HTTP. API redirects are rejected. Streams request format=raw and no bitrate limit, but the server ultimately determines delivery. Quality values are file metadata or server-reported values, not proof of lossless or bit-perfect output. Gapless output, remote artwork, server playlist editing, music downloads and Echo-extension compatibility are not implemented. Disconnect/Online off closes the browser session and prevents new browsing or track selection; a currently playing track may continue until stopped or changed.

The metadata reader uses TagLibSharp 2.3.0 under LGPL-2.1-only; its source, package manifest and license notices accompany the existing dependency inventory. Native regression covers tagged local WAV indexing, non-destructive scan/removal, persistence/cancellation, album order/search, real local and token-authenticated server audio decoding, next/replay, online-mode cleanup and credential-free history against a local fixture. A real user's Navidrome installation has not been tested.


### 0.14.1 — Separate music and connection management

Music now has a dedicated sidebar page for local albums, tracks and browsing a connected music server. My Library keeps video folders and the shared playback queue. Navidrome/OpenSubsonic address, login and disconnect controls live in Add-ons, alongside the other connection types. Music links to that setup and Add-ons links back to Music. Online mode still gates server features; local music remains available offline. Existing music folders, preferences and playback sessions are preserved.


### 0.14.2 — Video history and provider categories

Continue Watching excludes local audio files and music/audio contexts, including previously saved entries, while retaining their saved progress. Add-ons has Video and Music filter buttons: Stremio and Cloudstream management appear only under Video, while music-server connections appear only under Music. Switching filters preserves installed providers, connection state and playback. The Music page's manage-connections action selects the Music filter automatically.


### Music extension research

A development-only Echo extension probe has verified DEX conversion, desktop JVM loading, search/track/source resolution and native audio decoding against a synthetic local fixture. Add-ons > Music can import local .eapk files and activate them using a separately supplied music runtime. See [evidence, limits and adapter work](archive/echo/docs/echo-bridge.md).


The research now includes a persistent music helper and C# transport with custom login, session settings, paged search/albums, source resolution, hash verification, cancellation and clean restart. The synthetic-server integration also checks real native audio decoding. The app now connects this helper to explicit activation, session login, browsing and playback without bundled Java/SDK files; the custom SDK license needs a separate distribution compatibility review. Reproduction and limits are in [the helper report](archive/echo/docs/echo-bridge.md#persistent-helper-milestone-2026-10-22).

### 0.14.3 — Music extension package management

Add-ons > Music imports user-selected Echo .eapk files, displays their names, versions and authors, and removes stored copies. Import reads bounded Android manifest metadata without executing extension code or following provider links. Reimporting the same file is idempotent; imported files persist across restarts and Online mode changes. Removing one keeps the user's original file. Imported extensions are explicitly marked inactive: runtime setup, login and listening UI are still pending. No Echo SDK, Java runtime or provider package is included.

### 0.14.4 — Echo activation, browsing and playback

Add-ons > Music can select a separately prepared runtime profile and activate a trusted imported Echo extension. Custom login forms keep credentials only for the session. Music displays provider tabs, paged results and albums with back navigation; tracks play through the existing player and private header relay without adding authenticated URLs to video history. Disconnect, runtime changes, Online mode off and app exit stop the helper and clear session handles. Runtime fingerprints are verified. No Echo SDK/runtime or provider package is bundled or downloaded automatically. One OpenSubsonic extension is verified against a synthetic server; arbitrary extension compatibility, browser login and music-extension autoplay are not guaranteed. See [local setup and limits](archive/echo/docs/echo-bridge.md#app-activation-and-listening--0144).

### 0.14.5 — Paste-link music extension imports

Add-ons > Music accepts Echo repository links, GitHub project/release links, direct .eapk links and Echo-style shortcodes. Choose a repository entry to download and validate its package; activation remains explicit. Local-file import and runtime controls now live under Advanced. Downloads have size limits, HTTPS-only redirects, cancellation and optional release digest checks. The separate music runtime still needs a one-time setup before playback; automatic engine provisioning is not included. See [supported links and limits](archive/echo/docs/echo-bridge.md#link-imports--0145).

### 0.14.6 — Music catalogue and setup feedback

Online Music now keeps a visible catalogue/setup section even before a provider is active. Add-ons shows the music engine status prominently. Successful activation or account login loads the provider's home catalogue automatically (or blank-query search when no home feed exists) and opens Music. Empty sections suggest a song/album/artist search. Repository entries marked lyrics, tracker or misc are excluded from music installation choices. Install buttons show installing/installed/retry states, while missing releases, host errors, rate limits and checksum failures get specific messages. This does not provide a built-in music catalogue or remove the separate engine setup requirement; live provider compatibility still varies.

### 0.14.7 — Download feedback and music compatibility

Music extension downloads show received bytes, can be cancelled and time out when the response body stalls. Artist and playlist catalogue entries now open their feeds/tracks. The app embeds only its original compiled helper code so host updates work with an existing verified runtime profile; the Echo SDK, Java and engine remain separately supplied. Conversion/interface failures have specific safe diagnostics instead of generic retry advice. ArtistGrid live metadata browsing was verified from its artist list to an album feed; that does not establish playback. Current Deezer/Spotify/Saavn/ASMR One packages still have reproduced desktop conversion failures, and YouTube Music initialization is not yet compatible.

### Android helper investigation

A proposed optional Android-hosted extension route has been investigated, with a read-only Windows prerequisite checker. It would run extensions on ART instead of converting DEX to JVM bytecode, but needs a full Android environment and measured resource/compatibility tests. No Android helper or emulator integration is available yet. See [findings and proof-of-concept gates](archive/echo/docs/echo-android-helper.md).

### Music extension status (0.14.8)

Imported music extensions now show session status: installed, starting, login required, ready, disconnected, incompatible, or failed/timed out. Ready means the provider initialized; it does not guarantee every track plays. Failures remain visible beside the extension until retried or the app closes. No Android environment is installed or required; the existing desktop bridge remains in use.

### Music browsing (0.14.9)

Extension songs use compact play rows; albums, artists and playlists use wrapping cards with fallback music icons. Provider titles are preserved. The current bridge does not supply artwork, so no remote cover downloads are added. Local and connected music plus extension audio stay on Music when started, with a persistent bottom bar for pause/resume, position and shared volume while navigating. Audio starts independently of the native video window; video retains its existing mini-player. Extension compatibility is unchanged.

### YouTube Music loader repair (0.14.10)

The desktop helper now preserves extension service/resource lookup and repairs converted local-interface calls. The tested YouTube Music package now activates, browses, searches, resolves sources and decodes audio without Android. Other providers with malformed converted bytecode remain unsupported; see the [tested scope](archive/echo/docs/echo-bridge.md#desktop-loader-repair--01410-2026-10-23).

### Optional enhanced music compatibility (0.14.11)

An off-by-default Advanced option can use a separately prepared, hash-verified compatibility runtime. Saavn was tested through home/search and actual UI playback; the standard engine remains available. No Android emulator, automatic runtime download or new third-party binary is included. See [setup, measured costs and limitations](archive/echo/docs/echo-enhanced-compatibility.md).

### Guided music engine setup (0.14.12)

Add-ons > Music now has a visible Set up music engine button. Selecting a verified prepared profile automatically chooses the available engine mode; enhanced mode cannot be enabled for a standard profile. Saved setup is checked on restart. Repository choices collapse after installation to keep installed providers visible. The runtime is still separately supplied, and provider compatibility remains experimental.

### MusicFree search/audio preview (0.14.14)

Add-ons > Music accepts one trusted MusicFree provider using a direct HTTPS .js link. The tested Audiomack plugin supports search with artwork cards and native music playback, seeking, previous/next and the persistent music bar. The embedded engine needs no external runtime setup. This is a limited experimental host, not full MusicFree compatibility; it adds no provider catalogue or lossless source. See [setup and tested limits](docs/musicfree-evaluation.md).

### Provider Explore and genres (0.14.15)

Music has a provider selector, automatic Explore, plugin-supplied sections and genre filters. Audiomack provides Trending and Recently Added. Multiple imported providers and the selected provider are retained, including the existing 0.14.14 import. Browsing another provider preserves current playback and keeps Next tied to the original provider. Search-only providers remain supported; Audiomack is the only public provider verified live. See [setup and scope](docs/musicfree-evaluation.md).

### Music Now Playing and independent lyrics (0.14.16)

Click the song title in the bottom bar for artwork, title/artist/album details, the current queue and lyrics. LRCLIB matching is independent of the music provider, with synced highlighting, manual recording choices, timing adjustment and local LRC/text import. Existing local LRC sidecars work offline. See [behaviour, privacy and tested limits](docs/music-now-playing.md).

Music player update (0.14.17): dedicated artwork and lyrics screen, one persistent music control bar, music-aware Player navigation, and compact lyric options. See [Now Playing](docs/music-now-playing.md).

Music provider tests (0.14.18): six additional author plugins checked, MaoerFM basic playback verified, and generic direct-string playback URLs supported. See [provider test results and limitations](docs/musicfree-provider-tests.md).

Music import update (0.14.19): experimental Nuclear ZIP support connects SoundCloud search/audio to the existing music player and queue, without a separate Node runtime. See [setup and tested limits](docs/nuclear-music.md).
