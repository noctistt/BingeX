# Experimental Nuclear music import — 0.14.19

Add-ons → Music accepts a direct HTTPS MusicFree `.js` link or Nuclear `plugin.zip` release link. Confirm trust before import. Providers execute code; only import reviewed sources. Existing installed providers and their selection/trust data remain compatible.

## SoundCloud setup

1. Install BingeX 0.14.19 and enable Online mode.
2. Open Add-ons → Music and confirm trust.
3. Paste `https://github.com/NuclearPlayer/nuclear-plugin-soundcloud/releases/download/v0.3.0/plugin.zip` and choose Add provider.
4. Open Music, select SoundCloud and search. Select a result to play it using the existing music bar, queue, Now Playing artwork and provider-independent lyrics options.

No separate runtime setup or service account was needed in the tested run. Search is available; Explore/chart browsing and Nuclear playlists/artist pages are not implemented in this pass. The adapter does not invent catalogue categories. Search results can include uploader names, covers or remixes; a matching artist name does not establish official artist identity. Availability remains source/region dependent. Tested streams were AAC, not lossless.

This is a limited Nuclear search/streaming bridge, not universal plugin compatibility. File-system, process, dependency installation, cookies, logins, arbitrary SDK methods and custom playback headers are not supported. User-selected plugins remain external code; the app contains no bundled SoundCloud provider, client identifier or account key. The compiled imported source is stored in the existing hash-checked provider store. Importing an updated version requires importing its link again; there is no silent provider update.

## Implementation and bounds

The importer downloads a bounded ZIP, rejects traversal/duplicate paths and oversized inputs, and reads source in memory without extraction. `package.json` supplies the entry point and provider metadata. Package scripts are never executed. The embedded Sucrase converter strips TypeScript and transforms static imports inside an isolated worker. Only relative, package-owned runtime modules are resolved. The registered metadata search and streaming interfaces are adapted to BingeX's existing music provider contract, keeping the existing selection, persistence, queue, playback and cancellation behavior.

The converter is about 289 KiB of static JavaScript plus notices. Node/npm/esbuild are build tools only and are not installed or started by BingeX. Import compilation has a 128 MB cumulative-allocation budget, 8 million statements and 25-second script deadline, under the existing parent process watchdog. Search and source resolution retain the normal 32 MB allocation/one-million-statement limits. These are interpreter allocation limits, not OS resident-memory guarantees. Workers exit after each operation; there is no persistent music daemon.

HTTP keeps public HTTPS, DNS/address checks, header filtering, no cookies/default credentials, bounded redirects and request count. Nuclear fetch can read responses up to 8 MiB to scan public site scripts; MusicFree retains its 2 MiB default. Nuclear fetch returns HTTP error status to the plugin for its own retry logic. Large response bodies cross the bridge directly as strings, avoiding an additional JavaScript JSON parse/copy. The process is isolated for responsiveness, not an OS security sandbox.

Online mode off cancels pending work and hides provider browsing without deleting providers. Current playback can continue. Removing/replacing a provider preserves current audio but prevents its stale queue from resolving further tracks. Import or inspection failure preserves previous providers. Media links, search results and per-play queues remain session-only and are excluded from video history.

## Validation

- Original Nuclear SoundCloud v0.3.0 release ZIP imported through the actual UI in an isolated test profile.
- Search, two distinct tracks, muted native playback, dedicated music view, pause/resume, seek to 8 seconds holding beyond 8.5 seconds after settling, Next and offline retention passed.
- Compilation, isolated inspection, 20 search results and HTTPS source resolution passed with the embedded Jint engine and no Node runtime.
- TypeScript/module registration, metadata mapping, URL query handling, HTTP error status and traversal/duplicate/oversize archive fixtures passed.
- Existing host checks for process limits, cancellation, URI rejection, provider trust/persistence and removal passed. Unmodified Audiomack synthetic search/source regression passed after the shared HTTP refactor.

No full catalogue, audible quality, lossless output, live lyric availability, Nuclear playlist functionality or other Nuclear plugin is established by these tests. See [earlier native proof](../tools/nuclear-soundcloud-probe/README.md), [converter build instructions](../tools/nuclear-import-compiler/README.md), and [upstream plugin](https://github.com/NuclearPlayer/nuclear-plugin-soundcloud). Upstream plugin licenses remain separate; user-imported code is not redistributed in BingeX's installer.
