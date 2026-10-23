# MusicFree research probe

Original BingeX harness, not an extension installer or security sandbox. Use reviewed plugins in a disposable process without credentials. No plugins are bundled. Dependencies are development-only and locked.

Copy package.json and package-lock.json to an isolated dependency folder and run `npm ci --ignore-scripts --no-audit --no-fund` there. Clone MusicFreePlugins and check out revision 3fa57cf37c100f32ca96e9199c6b008e79db28c1.

```
node probe.cjs PATH_TO_PLUGIN_REPO PATH_TO_DEPENDENCY_FOLDER inspect
node probe.cjs PATH_TO_PLUGIN_REPO PATH_TO_DEPENDENCY_FOLDER fixture
node probe.cjs PATH_TO_PLUGIN_REPO PATH_TO_DEPENDENCY_FOLDER live audiomack "Kevin MacLeod"
```

Inspect records exports/dependencies with HTTP blocked. Fixture runs the unmodified YouTube plugin against synthetic data. Live searches and resolves the first result, optionally checking response headers without retaining audio. Each invocation has a 55-second watchdog. Source URLs, headers and raw errors are excluded from reports. Node vm is not an untrusted-code sandbox; do not supply login variables.

See ../../docs/musicfree-evaluation.md for actual outcomes and limits.

## Native playback mode

Build tests/BingeX.MusicFreeProbe with the project .NET runtime. Set BINGEX_PROBE_DOTNET to its dotnet executable and BINGEX_PROBE_ASSEMBLY to the absolute built BingeX.MusicFreeProbe.dll path, then run:

```
node probe.cjs PATH_TO_PLUGIN_REPO PATH_TO_DEPENDENCY_FOLDER native audiomack "Kevin MacLeod"
```

This resolves two distinct results and sends sources over stdin to the native probe. It checks decoded audio, seeking, pause/resume, switching tracks, an unavailable loopback source and recovery. Output is muted, not an audible device/bit-perfect test. The native mode has a 120-second outer watchdog and a 100-second child limit; other modes retain 55 seconds. Native stderr is intentionally discarded because decoder diagnostics may contain signed source URLs. Cookie/storage support remains research placeholders; header-dependent playback is explicitly rejected pending relay testing. No BingeX profile is opened.

## Embedded host and real UI checks (0.14.14)

The app now uses Jint in a separate BingeX worker, without Node. The original Node harness remains a comparison tool only. Build tests/BingeX.MusicFreeProbe, then run its DLL with:

- `--host-checks`: worker startup, unsupported code, runaway limits, cancellation, URL boundaries and provider persistence/removal.
- `--embedded PATH_TO_REVIEWED_PLUGIN`: unmodified Audiomack plugin with synthetic HTTP responses.
- `--embedded-live PATH_TO_REVIEWED_PLUGIN`: opt-in public search and source resolution.
- `--ui-live`: opt-in real UI import, search/artwork, muted native playback, seek/next, video-history privacy, navigation, offline retention and cancellation. Requires BINGEX_DATA_DIRECTORY with a fresh folder name beginning musicfree-ui-test-.

Live tests use the pinned author Audiomack plugin and public Kevin MacLeod search, with no account and no saved media URLs. They can fail when the source service does. Run UI checks separately from builds so Windows does not lock the test assembly during compilation.

## Explore/provider checks (0.14.15)

`--embedded` now checks the unmodified Audiomack plugin's catalogue definitions and browse mapping against synthetic responses as well as search/resolve. `--browse-live PATH_TO_PLUGIN` verifies live catalogue definitions and trending content without playing media. `--ui-live` checks automatic Explore, genre/recent content and cover rendering, provider selection with stale results cleared, a search-only alternate fixture, playback preserved across provider switches and Next resolved through the original provider, persisted selection and offline cancellation. The alternate is synthetic, not a second verified service. `--host-checks` also checks multi-provider retention/removal and migration of the 0.14.14 single-provider JSON.

## Now Playing/lyrics checks (0.14.16)

`--lyrics-checks` runs LRC parsing, seek/delay and independent metadata-client fixtures. `--lyrics-live` makes an explicit public LRCLIB request and reports match availability without lyric bodies. `--ui-live` additionally checks Now Playing artwork/metadata, LRC loading via the production file loader, highlighting, seek/offset changes and next-track reset. The UI fixture contains original test labels, not downloaded song lyrics; no lyric corpus is checked in.

Additional provider diagnostics: `--providers-live <reviewed-author-dist-directory> <report.json>` checks six author plugins. `--provider-ui-live <reviewed-https-plugin-link> piano` tests import, search, two-track playback, pause/resume, settled seeking and next in an isolated `musicfree-ui-test-*` profile. These are explicit live diagnostics; individual stage failures are recorded, and a completed baseline run does not mean all providers passed. See ../../docs/musicfree-provider-tests.md.
