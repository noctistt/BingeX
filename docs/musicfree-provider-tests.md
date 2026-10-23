# Additional MusicFree providers — 0.14.18

Tested on 2026-10-23 against the author's current HEAD, `3fa57cf37c100f32ca96e9199c6b008e79db28c1`. Plugins were reviewed and tested in isolated profiles, without service accounts or the user's settings. No provider is bundled or installed into the user's profile. No dependency or engine memory limit was added/increased.

| Provider | Result in this test | Practical status |
|---|---|---|
| MaoerFM / 猫耳FM | Search returned 30 items; two sources decoded native audio. Actual UI import, search, two tracks, pause/resume, seeking, Next, music navigation and offline retention passed. A seek to 8 seconds held beyond 8.5 seconds after settling for 2 seconds. | Additional provider worth trying; primarily Chinese audio/music. |
| Udio | Browse and search returned results. Found a host bug: the plugin returns a URL string, whereas BingeX expected an object. Generic normalization fixes that. The resulting media server request returned HTTP 403, and actual UI playback timed out. | Browsable; playback not verified. AI-generated music service. |
| YouTube | Search received HTTP 200 but exceeded the embedded host's 32 MB allocation limit before returning results. | Not compatible with the current lightweight host in this run. |
| Bilibili | Module initialization failed; source requires `he`, cookie state and additional crypto/HTML functionality outside the tested host. | Requires further host compatibility work. |
| Kuaishou | Loaded; public search returned zero tracks for `piano`. | No playable candidate established. |
| Yinyuetai | Loaded; search rejected its promise. | Search failed; no playback established. |

The author Suno example was reviewed but not executed: it performs a request at module load and does not implement the search/source methods required by this preview. Navidrome, Airsonic and WebDAV need a configured server/library; they were not tested without an account. The community subscription list was inspected as a discovery source, not treated as 76 verified providers.

MaoerFM's site includes music, original music, soundtracks, audio dramas and other audio categories. Udio describes itself as an AI music generator. Neither test establishes a replacement for a broad commercial music catalogue. No provider was verified as a hi-res or lossless source. One earlier MaoerFM seek run produced decoder warnings; muted decoding and clock/control checks do not establish audible quality or long-term reliability.

## Import link

Use Add-ons → Music, confirm trust, and paste the author's pinned MaoerFM JavaScript link:

`https://raw.githubusercontent.com/maotoumao/MusicFreePlugins/3fa57cf37c100f32ca96e9199c6b008e79db28c1/dist/maoerfm/index.js`

Select 猫耳FM in Music. It is a user import; BingeX does not ship its code or catalogue. It already passed baseline native playback before the 0.14.18 host change. The 0.14.18 code change only normalizes generic direct-string playback URLs alongside existing object responses, preserving URI validation and the current header restrictions.

## Reproduction

Build `tests/BingeX.MusicFreeProbe` in Release. `--providers-live <reviewed-author-dist-folder> <report.json>` runs the six-provider baseline using bounded HTTP calls and muted native decoding. `--provider-ui-live <reviewed-HTTPS-plugin-link> piano` checks the actual app in a `BINGEX_DATA_DIRECTORY` whose basename starts with `musicfree-ui-test-`. `--host-checks` includes object/string URL normalization and existing process/URI/trust checks.

The baseline probe reports individual stage failures; its exit code indicates completion of the diagnostic run, not that every provider works. Reports contain hashes, status codes and outcomes, excluding playback URLs, headers, accounts and lyric bodies. See [recorded results](../tools/musicfree-probe/results/providers-01418.json).

Sources: [author plugins](https://github.com/maotoumao/MusicFreePlugins), [community list inspected](https://github.com/qwerwhr/musicfree-plugins), [MaoerFM](https://www.missevan.com/), [Udio](https://www.udio.com/).
