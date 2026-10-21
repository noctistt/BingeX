# Cloudstream Windows compatibility spike

Status: experimental user-facing integration in 0.13.0. Add-ons has a separate Cloudstream section with optional bridge download, repository discovery, installation, removal and provider browsing. No extensions or upstream engine/runtime binaries are bundled in the installer.

## Architecture

`CloudstreamBridge` starts a hidden Java helper using explicit arguments, communicates through JSON lines on private standard input/output, and terminates the helper on request errors, timeout, cancellation, or disposal. Requests are serialized; provider diagnostics are drained without storing private URLs. Java is capped at 512 MiB heap. The helper process is crash isolation, **not a security sandbox**: extensions execute code with the user's permissions. Only explicitly selected, trusted extensions should be tested.

The current protocol is compatible with SkyStream's `cs3_engine`, investigated at commit `f5523ae07d729b579be30976df8783a0b98c4559`:
https://github.com/akashdh11/skystream/tree/f5523ae07d729b579be30976df8783a0b98c4559/packages/cs3_engine

No upstream engine binaries or code are copied into BingeX. The optional download fetches pinned engine JARs and a pinned Temurin 21 runtime, verifies SHA-256 for each, and records the upstream GPL-3.0 source/license links. Original patch source under `tools/cloudstream-patch` preserves the generic separate audio-track contract. Only the small original patch JAR and download lockfiles are embedded in BingeX.

Installed extensions live separately from Stremio's database entries. Virtual provider manifests and scoped title/episode IDs connect them to the existing MediaLibrary without a localhost HTTP listener. Helper errors, cancellations and unsupported browser/UI events stop the helper; the next request starts a fresh one. External audio attaches through LibVLC's audio-slave support. Referer and User-Agent are supported; other request headers and DRM are explicitly marked unsupported. Subtitle links returned during stream resolution are exposed to the existing subtitle panel when they need no custom headers.

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

## 0.13.0 verification and limits

`tests/BingeX.CloudstreamIntegration` exercises the actual download, GitHub-repository resolution, hash-verified extension installation, virtual manifests, MediaLibrary search/details/streams, native video AND audio decoding beyond three seconds, persistent installation, helper restart, and removal. It takes isolated-data-directory, repository-URL, extension-name and search-query arguments; no production profile is used. The official YouTube extension passed this flow using the downloaded Temurin runtime and generic audio patch. This is headless decoding evidence, not an assurance for every video or site.

The visible repository UI was checked with a fresh isolated profile and listed all five official extensions. Existing local playback/subtitle/torrent smoke checks passed. Browser verification, extension settings, arbitrary request headers, DRM, pagination and automatic extension updates remain unsupported. The provider's declared home page may be unavailable even when its search works. Each repository entry must supply its SHA-256 fileHash. Extensions execute code with the user's permissions: process isolation is not a security sandbox.
