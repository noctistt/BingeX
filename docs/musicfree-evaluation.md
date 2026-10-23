# Provider browsing update - 0.14.15

Music now has a provider selector above Explore and Search. Multiple imported providers are retained (up to 12), with the last selected provider and each provider's trust choice remembered. Existing single-provider data is read in its original form and migrated when next saved; no reinstall is needed.

The installed plugin supplies its catalogue sections through getTopLists and getTopListDetail. Audiomack supplies Trending Songs, Recently Added Music and 14 genre choices. These are loaded on first entering Music or selecting a provider, without typing a search. Sections/genres are not hardcoded into BingeX. Search-only providers retain search and hide Explore controls after capability discovery. A provider failure shows retry/search alternatives. Chart pagination is not guessed when the plugin supplies no continuation signal; search pagination remains available.

Switching providers cancels pending work and clears results, genres and the search field. Current playback and its result queue retain the original provider code/items, so browsing another provider cannot route Next to an unrelated service. Next only works while that original provider version remains installed and trusted. Removing or updating that provider leaves current audio alone but prevents further resolutions using obsolete code. Search results and playback queues remain session-only.

Live checks: unmodified author Audiomack plugin returned both sections, 14 genres and 49 trending tracks. Actual UI checks cover automatic Explore, genres, recent tracks, search/artwork, a synthetic search-only alternate provider, clearing stale results, continued native playback and original-provider Next while browsing the alternate, persisted selection, offline retention and cancellation. The alternate is a test fixture, not a second recommended public provider. Audiomack remains the only live provider verified here.

This update adds no runtime dependency or bundled provider. Album/artist/playlist detail pages and HTML-dependent catalogue methods remain outside the tested subset. Quality remains source-dependent; no lossless source is added.

The notes below describe the earlier search/audio implementation and experiments.

---

# MusicFree search/audio preview - 0.14.14

Current status: the app includes a small embedded Jint host for the tested MusicFree search/standard-audio subset. The research-only notes below describe earlier phases. No Node, Java, npm packages or provider plugins are bundled in the app. Echo remains archived.

## Using the preview

1. Enable Online mode and open Add-ons > Music.
2. Confirm trust, paste a direct HTTPS MusicFree JavaScript plugin link and choose Add provider.
3. Open Music, search for a song/artist, then select a track. The music bar provides play/pause, seek, volume and previous/next. The existing autoplay preference applies to the current result queue.

Tested provider: [author Audiomack plugin, pinned revision](https://raw.githubusercontent.com/maotoumao/MusicFreePlugins/3fa57cf37c100f32ca96e9199c6b008e79db28c1/dist/audiomack/index.js). This is an optional user import, not a baked-in catalogue or source. Only one provider is retained in this preview. Source code and trust choice are saved in the user's musicfree/provider.json; search results, full result items, queues and signed playback URLs remain session-only and are excluded from video history. Online mode hides the panel and cancels outstanding work without removing the provider. Existing music can continue playing. Removal similarly leaves current playback alone.

This is not full MusicFree compatibility: repository JSON installation, provider accounts/cookies, HTML catalogue methods, nonstandard audio qualities and source headers are unsupported. Other plugins can fail. Search/next-track resolution uses fresh script instances; arbitrary plugin session state is not retained. Lyrics, saved online playlists and lossless sources are not added here. Audiomack's measured output was AAC, 44.1 kHz stereo.

## Runtime and limits

Jint 4.17.0 + Acornima 1.7.0 add roughly 3 MB of managed assemblies before installer compression. Only a small original host API is exposed: axios-style GET/POST, HMAC-SHA1/Base64 and the date format used by the tested plugin. Unsupported modules/methods fail explicitly. String compilation and broad CLR access are not enabled. The engine runs in a separate instance of the BingeX executable, before UI/single-instance initialization; no separate runtime picker or daemon is needed.

Execution is capped by statement/memory/recursion/time limits, at most 12 HTTP requests, and a parent-process deadline which kills the worker. Source/output sizes and result counts are bounded. HTTP uses HTTPS, no cookies/proxy/default credentials, public-address validation and connection-time DNS validation, bounded bodies and manually validated redirects. This is process isolation for responsiveness, not an OS security sandbox; only import trusted code. No provider update is downloaded automatically. Runtime cancellation, startup, trust and import/source failure states are explicit.

## App validation

Actual UI test in an isolated profile: explicit trust before execution, pinned HTTPS import, search cards/artwork, muted LibVLC audio beyond three seconds, seeking, distinct next track, persistent music controls on navigation, no video history entries, offline retention and cancellation. Embedded-host synthetic checks use the unmodified plugin. An additional live engine-only request returned HTTP 406 while the full UI run succeeded; service availability is not guaranteed and the UI permits retry/another track. Earlier Node comparison also hit its script watchdog during the follow-up, so it is not treated as a successful additional run.

The earlier native test remains recorded below. Tests do not establish long-term availability, all-catalogue coverage, gapless/bit-perfect output or a lossless source.

---

# MusicFree evaluation - 2026-10-23

Status: research only. No MusicFree runtime, plugins or dependencies are included in the BingeX app. Echo is archived. Local music, metadata/artwork, playback controls, queues and native OpenSubsonic connections are retained.

## Pinned upstream sources

- [MusicFreeDesktop](https://github.com/maotoumao/MusicFreeDesktop): f3b526a6c1ea9313b277810a8e12003605a98982, latest fetched commit 2026-03-14.
- [Author example plugins](https://github.com/maotoumao/MusicFreePlugins): 3fa57cf37c100f32ca96e9199c6b008e79db28c1, latest fetched commit 2025-11-02.
- [Protocol](https://musicfree.catcat.work/plugin/introduction.html).

These dates do not establish active maintenance of each plugin. Community repositories exist but have not been vetted. No plugin/provider is required by BingeX yet.

## Initial tests

| Test | Result | Limit |
|---|---|---|
| Unmodified YouTube plugin with synthetic responses | Search metadata/artwork and source URL mapping passed | Mocked HTTP |
| YouTube 0.0.1 live search | Found results, failed during source resolution with TypeError | No playable source established |
| Audiomack 0.0.2 live search | 20 results with artwork; URL resolved; HTTP 206, audio/mp4 | Native/audible playback, seeking, reliability and lossless quality not verified |
| Example module loading | Export/dependency inventory recorded | Loading is not functional validation |

Public query: Kevin MacLeod. Reports retain plugin hashes and outcome summaries, not media URLs, headers or cookies. Media response bodies were cancelled after checking headers; audio was not saved. See tools/musicfree-probe/results.

## Plugins and host requirements

Author list: Audiomack, YouTube, Bilibili, Navidrome, WebDAV, Suno, Udio, MaoerFM, Kuaishou, Yinyuetai, Geciwang and Geciqianxun (the last two supply lyrics). Dist also contains Airsonic Advanced. These are candidates, not endorsed working sources. Navidrome/WebDAV/Airsonic require a user's own server/library; lossless originals depend on that server. Audiomack's tested audio/mp4 response is not evidence of high-resolution FLAC.

Plugins are CommonJS JavaScript modules; optional methods include search, getMediaSource, album/artist/playlist browsing, recommendations and getLyric. Repository JSON lists name/version/download links. They are unrelated to Echo .eapk, Cloudstream .cs3 or Stremio manifests.

The desktop host provides axios, cheerio, crypto-js, dayjs, big-integer, qs, he, webdav, cookies, musicfree/storage and env.getUserVariables. A JavaScript interpreter alone is insufficient: these host APIs must match. The isolated research harness uses separately installed Node dependencies, memory-only cookie/storage placeholders and no accounts. It is not full compatibility or a security sandbox. One example made an HTTP request on module load; the final inspection blocks network and records that behavior.

## Next gate

Before implementing a user-facing importer: verify one provider's native playback, seeking, next-track/retry, artwork and real codec/quality; review maintenance and redistribution terms; choose a host requiring no manual runtime selection. Lyrics should be independent of the source. No replacement is claimed ready from load-only tests.

## Native playback follow-up (2026-10-23)

The unmodified pinned Audiomack plugin resolved two distinct public search results, both with artwork. Sources were passed to a standalone C# test through stdin and never written to disk. The test references BingeX's current project and uses the same LibVLC package/backend with video disabled and dummy (muted) output.

Passed in one live run:

- First track decoded audio and advanced beyond three seconds.
- Seek forward to 15 seconds, then backward to two seconds, with clock advancement after both seeks.
- Pause held the playback position; resume advanced again.
- A second distinct source started and decoded audio.
- An intentionally unavailable loopback source failed; opening the first source again recovered playback.

Reported codec FourCC: mp4a (AAC); 44,100 Hz, stereo. This is not FLAC/high-resolution lossless. No bit-perfect output or audible device path was tested. One successful run does not establish service-wide catalogue coverage, long-term reliability or behaviour after provider updates.

The result is recorded in tools/musicfree-probe/results/audiomack-native.json. That earlier probe did not exercise BingeX's UI, queue/history wiring, library persistence or plugin installation. No new runtime/dependency is added to the user app, and no new installer is needed for this research-only change. Next implementation gate: a small end-to-end Audiomack integration with a clear unavailable/error state, artwork and native controls, while keeping generic plugin execution outside the UI process. Provider/runtime distribution and host design still need review before shipping.
