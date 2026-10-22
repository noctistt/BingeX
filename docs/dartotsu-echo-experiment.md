# Dartotsu / Echo isolated compatibility experiment

Date: 2026-10-23. Status: successful feasibility experiment, not integrated into BingeX. Application remains 0.14.10. No installer, installed profile, account or application code changed.

## Result

The tested Dartotsu-derived conversion/repair pipeline enabled a previously failing Saavn package to activate, browse, search, resolve sources and decode audio through BingeX's existing relay/player. Deezer progressed to successful activation and login form discovery. This supports testing a broader compatibility pipeline instead of accumulating individual extension fixes. It does not establish that all Echo packages, updates or service accounts work.

| Exact package | Shipped BingeX helper baseline | Isolated experiment |
|---|---|---|
| Saavn `yt-9427445.eapk` | VerifyError during instantiation | Activated; home returned 8 items; search page contained 10 tracks; 1 variant and 4 sources resolved; native audio decoding advanced beyond 3 seconds with silent output |
| Deezer `deezer-1448ed2.eapk` | VerifyError while injecting settings | Activated; 2 custom login forms with 2 and 1 fields; no credentials supplied, browsing/playback not verified |
| ASMR One `ex-e0f56a9.eapk` | ClassFormatError during instantiation | Still fails: VerifyError / missing stack-map frame in a remaining converted class |
| Spotify `sp-4136d68.eapk` | Failed instantiation/loading; root diagnostic also reproduced bad coroutine bytecode | Progressed to missing `com.google.protobuf.ProtocolMessageEnum`; still cannot activate in this test environment |
| ArtistGrid `artistgrid-251873c.eapk` | Known working provider | Control: activation and first home page passed; not a full playback regression |

The baseline helper class bytes were checked against the embedded helper ZIP shipped in BingeX and matched. The failures/results apply to these package hashes:

```
yt-9427445.eapk          198bf1c26491de088e9aafb1ab4283710383a9f7b16744d17b5455ac330307ff
deezer-1448ed2.eapk      addbc10fd56afcdd9df5c2b801d0da8ec148df1a86d5eee7ea4d174065b8b5ab
ex-e0f56a9.eapk          2f08f6edaeb455511dcf55eab5981463ad9ae6942c9d33c1fb710aa5c28deb4f
sp-4136d68.eapk          94d462603ec5b96d054006a5211cfb7e32ade3192a6224029055e6f5e05480fa
artistgrid-251873c.eapk  2f4f971e1daa04dbfb1cf19b562c966c7e3aec0c6e6a21fd4b0a6affaf49800d
```

## What was tested

Upstream snapshot: [DartotsuExtensionBridge 35db96730c98a1e0d76d69e56097585c8c60bab9](https://github.com/aayush2622/DartotsuExtensionBridge/tree/35db96730c98a1e0d76d69e56097585c8c60bab9).

- Original `BytecodeEditor.kt` and `DexNewInstance.kt`, copied unmodified into a separate experiment workspace. Only the logging adapter and invocation harness were original test code.
- Dartotsu's pinned dex2jar 2.4.38 translator/tools and their dependencies, with the conversion flags from `PackageTools.kt`: register reuse off, topological sorting, skipped debug, synchronized optimization off, code/exceptions preserved, name sanitizing disabled.
- Four upstream Java date/number/calendar/time-zone compatibility sources and ICU4J 78.3, required by the repair's SimpleDateFormat remapping. Their original notices were retained in the isolated checkout; ICU and converter downloads had repository checksums verified and local SHA-256 manifests recorded.
- BingeX's Echo settings/lifecycle/protocol adapter, original-package resource lookup and interface-reference repair, in a separately compiled laboratory helper. No Java verification was disabled.
- Actual public catalogue/source requests, plus silent native playback for a Saavn search result. Audio source URLs/headers were passed to the decoder through memory/stdin, not saved as media/history or command-line arguments. No real account was used.

This is a hybrid experiment inside BingeX's host, not a build or certification of the complete Dartotsu app. The Kotlin tooling and existing BingeX runtime dependencies differ from Dartotsu's complete distribution. Repair-only tests against the old converter were not sufficient; the successful Saavn result used the pinned converter, repair passes and compatibility classes together. On Windows, the harness must not hold the output JAR open while upstream repairs replace it.

## Resource and distribution boundary

No Android SDK, Android image or emulator was used. The experiment reused the existing desktop JVM. Laboratory Java processes had a 1536 MB maximum heap; actual memory, cold/warm performance and a production heap limit have not been benchmarked. The isolated converter dependencies total roughly 2.3 MB and ICU roughly 15.2 MB of JAR downloads; these are not a measured final installer-size change. Copying the complete Dartotsu runtime is not proposed.

No upstream repair code, compatibility classes or new libraries were copied into BingeX source/assets or its installer. The upstream bridge's custom Unabandon license and compatibility files' individual notices (including MPL-2.0) need review before any distribution decision. This report is a test result, not license clearance.

## Next implementation gates

1. Decide a distribution-compatible way to use or implement the required conversion repairs; preserve notices and keep third-party components separate from original helper assets.
2. Resolve inherited Echo capabilities (for example QuickSearchClient extends SearchFeedClient), so successful extension loading is reflected accurately in the UI.
3. Add synthetic constructor/DEX-evidence/frame regressions and the existing OpenSubsonic UI checks before changing the default engine. Keep repaired outputs in versioned isolated caches.
4. Benchmark startup/memory on this machine, test cancellation/process death, and test existing YouTube Music/Groove/TuneIn/ArtistGrid behavior before integration.
5. Investigate Spotify's Protobuf requirement and ASMR One's remaining frame error separately. Authentication/browser login remains its own unverified capability.

Research files, original copied sources, dependency manifests, matrix JSON, diagnostics and native-probe source are retained outside the repository under the task workspace's `work/dartotsu-echo-experiment/` folder. No application update is required or available from this experiment.
