# Nuclear SoundCloud proof — 2026-10-23

Result: search, artwork, stream resolution and native muted playback passed using the unmodified Nuclear SoundCloud plugin. This is research, not an installed BingeX provider or support for all Nuclear plugins. Existing providers, settings and installer remain unchanged.

## Sources and runtime

- Upstream: https://github.com/NuclearPlayer/nuclear-plugin-soundcloud
- Pinned commit: `b6b5722ab493ee9db697fc1d0a92674a9890af09`, package 0.3.0, AGPL-3.0-only.
- The upstream plugin is a separate research checkout, not copied into BingeX or its installer.
- Node 22.20.0 is used only by this test harness. No npm packages, Node runtime, SDK package, daemon or new application dependency were installed/bundled.
- The original TypeScript is loaded with Node's type stripping and VM modules. Type-only SDK imports disappear; the reviewed source has no runtime SDK imports. The harness supplies provider registration, logging and bounded HTTP interfaces.

VM modules are not an OS security sandbox. This command is only for the reviewed pinned source, not arbitrary plugins. HTTP is restricted to GET, HTTPS and SoundCloud/sndcdn domains plus the exact media hostname returned by SoundCloud's API. Requests, response sizes and deadlines are bounded. Native playback uses the existing LibVLC probe with dummy (muted) output; it does not run inside the JavaScript VM.

## Live result

- Search queries `Kevin MacLeod` and `Arijit Singh` each returned five metadata results with artwork and five streaming candidates.
- One stream from each search resolved to HLS AAC without an account or supplied API key. The plugin obtains its client identifier from public SoundCloud scripts; identifiers are not recorded by the harness.
- The two-track native probe passed first-track decoding and clock advancement, forward seek to 15 seconds, backward seek to two seconds, pause/resume, a second distinct track, an intentionally failed loopback source, and recovery with the first source.
- Decoded audio: AAC (`mp4a`), 44.1 kHz, stereo. The plugin estimated 160 kbps; this is not a measured bitrate or lossless claim.
- Final run used ten plugin HTTP requests. No full audio files, signed playback URLs, headers, account details or client identifiers were retained in the report. Source URLs were passed directly to the native child over stdin.
- Initial research runs resolved links but stopped at the harness's media allowlist: SoundCloud returned `playback.media-streaming.soundcloud.cloud`, outside the initial `.soundcloud.com` / `.sndcdn.com` list. Adding that exact hostname allowed the native test. This was a test boundary, not a plugin playback failure.

See [recorded results](results.json).

Limits: muted decoding does not verify listening quality. Search results do not establish official artist identity, an entire catalogue, region-independent availability or long-term reliability. Provider registration passed for metadata/streaming/playlists, but playlist operations were not tested. No BingeX UI, Nuclear installer, account login, hi-res output or generic plugin bridge was tested.

## Reproduce

From the BingeX repository, after building `tests/BingeX.MusicFreeProbe`:

```text
node --experimental-vm-modules tools/nuclear-soundcloud-probe/probe.mjs <reviewed-plugin-src> <dotnet-executable> <BingeX.MusicFreeProbe.dll> <report.json>
```

The command exits successfully only when the native playback probe passes. It discards native stderr because diagnostic messages can contain signed URLs. It writes stage outcomes, HTTP status codes, public hostnames and audio properties, not stream URLs. A fresh live run is required to renew expired source URLs; saved reports cannot be used for playback.

## Integration decision

This is a viable candidate for a small Nuclear compatibility proof in BingeX. The current MusicFree importer cannot install it: its ES-module loading, registration contract, fetch/Response API and URL utilities differ. A user-facing implementation still needs an isolated host for the required interfaces, normal import/trust and cancellation handling, metadata mapping to the existing Music view/queue, and an actual UI playback test. The proof does not require replacing BingeX's player, artwork or independent lyrics implementation, and does not justify installing a Node runtime in the application.
