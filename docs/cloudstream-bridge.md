# Cloudstream Windows compatibility spike

Status: developer proof, not an installed BingeX feature. No extension installation UI or bundled runtime yet.

## Architecture

`CloudstreamBridge` starts a hidden Java helper using explicit arguments, communicates through JSON lines on private standard input/output, and terminates the helper on request errors, timeout, cancellation, or disposal. Requests are serialized; provider diagnostics are drained without storing private URLs. Java is capped at 512 MiB heap. The helper process is crash isolation, **not a security sandbox**: extensions execute code with the user's permissions. Only explicitly selected, trusted extensions should be tested.

The current protocol is compatible with SkyStream's `cs3_engine`, investigated at commit `f5523ae07d729b579be30976df8783a0b98c4559`:
https://github.com/akashdh11/skystream/tree/f5523ae07d729b579be30976df8783a0b98c4559/packages/cs3_engine

No upstream engine binaries or code are copied into BingeX. That engine is GPL-3.0; bundling it requires preserving its license, notices and corresponding source, and reviewing the resulting distribution's licensing. The current helper uses locally supplied assets only. Java 17+ is required by upstream; tests used existing JDK 22.

## Reproduce

Run `tests/BingeX.CloudstreamProbe` with four arguments:

1. Absolute path to `java.exe`.
2. SkyStream `packages/cs3_engine/assets/engine` directory.
3. A new isolated data directory, never the user's BingeX profile.
4. An explicitly selected `.cs3` file, or `--lifecycle` to test failure cleanup.

Optional fifth argument: search query. Append `--playback` to attempt up to four resolved streams through LibVLC and require both decoded video frames and a playback clock beyond three seconds. Each stream gets 25 seconds. The probe fails when no results or no links are returned; successful extension loading alone is not counted as working playback. It does not print stream URLs. Network tests require outbound connectivity from Java and LibVLC. The headless playback check does not verify audible audio, seeking, subtitles, or the desktop UI.

## Evidence (2026-10-21)

- Official InternetArchiveProvider: loaded 33 DEX classes, 33 JVM classes, zero failed methods. Search failed to connect to archive.org and returned no items. This is not proof of functioning playback.
- Official DailymotionProvider: loaded 14 classes, zero failed methods; `nature` search returned 26 items and first title metadata loaded. Link resolution returned zero links. Catalog/detail interoperability is proven; playback is not.
- Browser and extension UI events are unsupported and cause a clear failure rather than an unanswered interaction.

### Official repository sweep

Repository source inspected at `cf16b3b45cda911a36ddca69f9acbe4763c6a4b1`. All five `.cs3` downloads matched the SHA-256 in the repository's builds/plugins.json at test time. Results are specific to this machine/network and are not claims that a provider is down for everyone.

| Provider | Version | Observed result |
|---|---|---|
| YouTube | 1 | 12 classes converted with no failed methods; `nature` returned 23 items; details loaded; 12 stream links resolved; **LibVLC decoded video and playback advanced beyond 3 seconds**. |
| Dailymotion | 4 | 26 search results and title details; no resolved streams for the first result. |
| Twitch | 2 | Loaded; search requested an interactive browser host, unsupported by this probe. |
| Invidious | 9 | Loaded; zero search results using the provider's configured instance. |
| Internet Archive | 1 | Loaded; zero search results; earlier direct probe recorded a connection timeout. |

YouTube test binary SHA-256: `a246eb1584cec9e69ba8c50136b9e8dc11a2fc0ab225710c31ad66c2ec4f4efe`.
This is the first verified candidate for desktop integration, not a guarantee for every YouTube video. No extension files, Java runtime or upstream engine are bundled in BingeX yet.

## Remaining before user-facing support

Use YouTube as the first integration candidate; validate HTTP headers, audio, subtitles and seeking in the visible player. Implement provider-to-BingeX catalog/details/episode mapping. Add repository parsing, pinned runtime acquisition with integrity checks, explicit installation and removal, compatibility labels, helper restart and app shutdown integration. Ship Stremio and Cloudstream as separate installation choices, with one shared player. Do not advertise all `.cs3` extensions as compatible.
