# Echo music extension feasibility probe

Status (2026-10-22): BingeX 0.14.3 includes metadata-only local music-extension package import/removal. The persistent helper remains development-only; there is no runtime downloader or extension playback UI. Built-in OpenSubsonic support remains separate.

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


## Persistent helper milestone (2026-10-22)

`tools/echo-helper/EchoMusicHelper.java` now implements a dedicated version-1 JSON-lines protocol. `src/BingeX/EchoMusicBridge.cs` launches and owns that helper through redirected standard streams; it does not search for runtimes, download dependencies or register providers. No Java helper classes, SDK JARs or extensions have been embedded in the application, and there is no installer UI yet. The application remains 0.14.2.

Implemented commands: hello, explicit hash-checked load, session configure, settings schema, custom login forms/login/user selection, home/search feed handles, paged shelves, album track feeds, track variants and HTTP source resolution, plus close. Home is a protocol path but is not exercised by the fixture integration. Unsupported browser/WebView hosts, DRM, merged sources and non-GET/body-bearing playback requests fail rather than being silently treated as ordinary streams. HTTP headers are preserved and validated. Artwork and complex settings widgets are not mapped yet.

Data crosses the process boundary as small display models and opaque session handles; raw Echo objects and account extras stay inside Java. Login credentials, settings and resolved URLs are not saved by the helper or bridge. Provider stdout/stderr is discarded instead of mixed with the protocol or written to logs. Errors expose fixed stage names and exception types, never provider exception messages. Requests and responses are capped at 1 MiB of text, pages at 500 items and object references at 8,192 handles. Changing settings/account invalidates browsing handles. A malformed response, helper error, timeout or in-flight cancellation terminates the helper; callers create a new bridge for a clean session. Waiting-call cancellation does not terminate another active request.

The package and fallback SDK each require expected SHA-256 fingerprints. Conversion cache namespaces include the helper's compiled classes, Java version and every runtime JAR digest, while the converter keys its entries by extension hash. A generic ASM rewrite fixes method-reference flags for known external JVM interfaces; Android DEX default-interface calls otherwise failed with IncompatibleClassChangeError during onInitialize. The extension's own converted SDK classes are preferred when present to preserve its ABI. This is bytecode interoperability work, not a media-service bypass. Incomplete conversions are rejected.

### Integration evidence

The original OpenSubsonic fixture now exercises **actual custom login**, including discovery and authenticated getUser requests, rather than injecting a synthetic User object directly. It then checks session settings, a 50-track first page and one-track continuation, album lookup/track feed, variant selection, source mapping and native decoded audio beyond three seconds. Negative cases require cancellation to kill the active helper, clean startup after cancellation, stale-handle rejection after settings changes, explicit unsupported-command failure, and extension/SDK hash rejection. Login responses must omit password and raw account extras. The fixture remains synthetic, loopback-only and silent; no live server account or public music source was used.

Run `tools/echo-helper/run-integration.ps1` with the same JavaPath, EngineDirectory, SdkJar, CompilerJar, ExtensionPath, WorkDirectory, DotnetPath and PythonPath inputs described above. It compiles only original helper source and runs the C# bridge against the temporary Python fixture. The tested extension and runtime fingerprints remain those listed above. This test is specific to that extension; it is not broad Echo compatibility or a test of the BingeX listening UI. The main app/player UI was not changed by this milestone.

### SDK license review outcome

The pinned SDK POM and Echo's [license text](https://github.com/brahmkshatriya/echo/blob/f210820680cd5e7bdcb383e3daab3fa2c06b71e0/LICENSE.md) identify a custom Unabandon Public License. It incorporates GPLv3 and adds public, unrestricted source availability requirements for distributed derivatives. That is a distinct grant, not ordinary GPL-3.0-or-later, and not the unrelated Universal Permissive License. BingeX's publicly available original source helps with public availability but does not by itself establish compatibility of a combined distribution. GNU's [modified-license guidance](https://www.gnu.org/licenses/gpl-faq.en.html#ModifyGPL) explains why modified GPL terms need separate compatibility consideration; that general guidance is not a ruling about this SDK.

No permission request has been sent, no upstream license has been altered and no combined-runtime distribution has been cleared. Before bundling the SDK/runtime or adding automatic downloads, obtain a compatibility assessment or an appropriate upstream clarification/alternative grant, and inventory the exact runtime JARs and corresponding sources. The separate process is a technical boundary, not a guarantee that licensing obligations disappear. Development with explicitly supplied runtime files remains available for this fixture-tested helper.

## Package importer milestone — 0.14.3

Add-ons > Music now imports local .eapk packages without running them. The original bounded reader handles UTF-8 and UTF-16 Android binary XML, requires Echo music feature metadata plus ID/name/version/entry class, and rejects duplicate or malformed metadata. Packages are copied once to temporary storage, inspected and hashed there, then moved under a SHA-256 filename in echo/packages. No package paths, provider URLs or executable classes are loaded from metadata. Originals are retained on removal. This is package storage, not signature authentication or activation.

Reproduction: run tests/BingeX.EchoPackagesTests with an optional path to the documented OpenSubsonic package. Synthetic tests cover string encodings, metadata, persistence, duplicate import, malformed manifests, non-music packages, traversal rejection and cleanup. The separate Windows tests/BingeX.EchoUiTests requires BINGEX_DATA_DIRECTORY pointing to an isolated echo-ui-test-* directory and the inspected package path as its argument; it checks the actual rendered Add-ons page, category separation, offline retention and removal. No user profile or real account is used.

No SDK/runtime distribution decision has changed. Imported files show playback setup pending; the next step is explicit runtime provisioning and connecting provider browsing and playback.
