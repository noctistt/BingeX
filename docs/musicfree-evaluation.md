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
