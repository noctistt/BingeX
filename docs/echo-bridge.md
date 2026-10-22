# Echo music extension feasibility probe

Status (2026-10-22): development-only proof of a single extension running on Windows. No music-extension installer, runtime downloader or production app bridge has been added. BingeX remains 0.14.2. OpenSubsonic support in the app is separate from this experiment.

## Examined artifacts

- Echo source: https://github.com/brahmkshatriya/echo/tree/f210820680cd5e7bdcb383e3daab3fa2c06b71e0 . Clients expose suspend functions, paged feeds, tracks, streamable media, provider injection and custom login/settings contracts.
- Echo common-jvm 1.0.0 from Maven Central; SHA-256 b1d591ac908d20389f01e8cd8c967b3b6fee9680a408b8fe970f336736278f89. Its POM identifies the custom Unabandon Public License, not an unmodified GPL grant.
- OpenSubsonic extension source: https://github.com/ArjixWasTaken/echo-opensubsonic-extension/tree/0f94a38c1e7659e0d9b1cf75016055576d953d07 . The repository declares GPLv3.
- Release 202609152-0f94a38, opensubsonic-202609152-0f94a38.eapk; SHA-256 fad05499f208bafd5c750e5fbee5c3179f45ace992214bc90b343b21258eaad0. This artifact is an APK-style ZIP with AndroidManifest.xml and 17 DEX files, not a ready-to-load Java JAR.
- Conversion reused the separately downloaded, pinned SkyStream cs3_engine runtime documented in cloudstream-bridge.md. Its frame-aware converter loaded all 3,784 classes with zero reported failed methods, in about 2.8 seconds on this machine. Standard dex2jar output initially failed JVM stack-map verification; the existing engine converter resolved that failure.
- ECJ 3.38.0 compiled the original Java probe for Java 17 using the existing Temurin 21 runtime. No upstream binaries, converted code or downloaded extension files are checked into BingeX or embedded by this probe.

## Observed result

PASS: convert and instantiate the extension on desktop Java; identify search/feed/track/login client interfaces; inject a synthetic local-only account; call the extension's search feed; load its first track page; load the track; resolve its first HTTP source; request original format with no bitrate limit; decode audio through LibVLC with DecodedAudio greater than zero and playback beyond three seconds. The fixture recorded one authenticated search and two stream requests.

The server supplied a generated 20-second mono PCM WAV tone. No real server account, public music service, copyrighted music or subscription was used. The test used dummy audio output, proving decoded frames and clock progress, not audible sound, fidelity, gapless output or hi-res playback. Early non-STA headless launches intermittently exceeded the native initialization watchdog, including one direct-DLL repeat; another decoded successfully. The saved decoder now uses an explicit Windows STA entry point and granular stage reporting. Two clean STA runs passed conversion, resolution and decoding, each recording one authenticated search and two stream requests. This is evidence of a working fixture path, not a claim that the earlier native-startup problem is fully explained. The runner uses direct DLL execution and bounded subprocess timeouts.

This is a compatibility result for one extension and one fixture path. It does not establish public provider availability, arbitrary extension compatibility, actual login UI behavior, album browsing, artwork, lyrics, DRM, browser/WebView authentication, full settings support, or safety of downloaded extensions.

## Reproduce

Original source lives in tools/echo-probe and tests/BingeX.EchoProbe. Use PowerShell 7 and the .NET SDK from global.json. Supply trusted, verified files yourself; the runner does not auto-install a provider or download code.

Run tools/echo-probe/run-probe.ps1 with JavaPath (java.exe), EngineDirectory (pinned cs3_engine JAR folder), SdkJar (Echo common-jvm 1.0.0), CompilerJar (ECJ 3.38.0), ExtensionPath (the inspected .eapk), EntryClass (dev.brahmkshatriya.echo.extension.OpenSubsonicExtension), WorkDirectory (a new isolated scratch directory), and optional DotnetPath/PythonPath. The runtime dependencies are supplied explicitly from the existing bridge and the SDK; those versions must be pinned and audited before product integration. Do not reuse the conversion output after changing an extension or converter: use a new WorkDirectory.

The local fixture suppresses access logs, never prints authenticated addresses and removes its temporary resolved URL in finally. Only a 127.0.0.1 source is accepted by the decoding test. Extension code itself executes with the user's permissions and could make other network requests: process separation and these URL checks are not a sandbox. Never point this probe at an untrusted package or a real profile. This probe does not add history or change BingeX settings.

## Next adapter work

1. Review SDK and runtime distribution terms. Echo's custom license adds unrestricted public source availability requirements; do not assume it is interchangeable with, or automatically compatible with, BingeX's GPL-3.0-or-later grant. The original probe can remain separate while this is reviewed.
2. Define a dedicated JSON-lines music helper protocol for capabilities, initialization, settings/login, paged search/home/albums, track loading and source resolution. Reuse the established process lifecycle patterns but keep Echo and Cloudstream object models distinct.
3. Add extension metadata/version validation, integrity verification, dependency pinning, cache keys tied to extension/converter hashes, cancellation/timeouts and safe diagnostics. Map headers and HTTP bodies explicitly; unsupported WebView or DRM requests must return a clear error.
4. Add user-selected extension management under Add-ons > Music and provider browsing on Music. Preserve the single player and credential-free history policy. No provider presets need to be bundled.
5. Test more explicitly selected, suitably licensed extensions against fixtures and then real reachable services before claiming broad support.
