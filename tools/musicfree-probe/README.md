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
