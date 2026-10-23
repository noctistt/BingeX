# Optional enhanced music compatibility (0.14.11)

Enhanced compatibility is an opt-in desktop JVM route. It does not use an Android SDK, image or emulator. It is off by default, remembers the user's choice, and changes the engine on the next extension activation. Turning it off preserves imported packages and returns to the standard engine. Existing version-1 runtime profiles remain supported.

## Local setup

1. Install BingeX 0.14.12 or newer.
2. In Add-ons > Music, choose **Set up music engine** and select the separately prepared `echo-runtime.json`. Supporting files must remain in place.
3. Setup verifies the files and selects enhanced mode automatically when available. Confirm trust, then activate the provider. The Advanced switch is disabled for standard profiles; it remains available for switching a prepared enhanced engine back to standard.

On restart the saved engine is checked again, and an unavailable enhanced mode is cleared. Repository choices open in a bounded, collapsible list and collapse after installation. This simplifies setup; it does not provision an engine or fix every provider.

Nothing is downloaded automatically. The profile lists SHA-256 fingerprints for all referenced components. Extra JARs are verified before process startup, have an explicit ordered classpath, and cannot be enabled by a version-1 profile. Only the original BingeX helper is embedded in the installer. Hash verification checks consistency with the selected profile, not the reputation/safety of its author. The runtime/extensions execute as the current user and are not an untrusted-code sandbox.

## Local preparation tool

`tools/echo-compatibility/prepare.ps1` takes explicitly supplied local dependencies:

- `BaseRuntimeProfile`: the existing verified standard profile.
- `BridgeSourceRoot`: the reviewed DartotsuExtensionBridge checkout, revision `35db96730c98a1e0d76d69e56097585c8c60bab9`.
- `KotlinCompilerClasspath`: an explicitly supplied Kotlin compiler and its dependencies.
- `CompilerJar`: a supplied ECJ compiler JAR.
- `ConverterDirectory`: the ten reviewed dex2jar 2.4.38 dependency JARs.
- `IcuJar`: reviewed ICU4J 78.3 JAR.
- `OutputDirectory`: a new local folder.

The source and dependency lockfile is `components.json`; source text checks normalize CRLF to LF. The tool rejects a different checkout/dependency set, creates the separately supplied repair component, preserves copied source/notices and source hashes, and writes a version-2 profile referring to those components and the base runtime. It does not download, install, accept licenses, alter an existing runtime, or change the user's app data. The generated folder is for local preparation, not a cleared redistributable bundle.

The component combines an original BingeX adapter, original quiet logging adapter, upstream BytecodeEditor/DEX analysis and the upstream date-format compatibility sources. Conversion uses the reviewed settings and reuses a complete published artifact after the first successful conversion/repair. A package hash, helper/runtime fingerprints and an engine-mode subdirectory separate caches. Staging files are cleaned on ordinary completion/failure. Cancelling kills the isolated helper; a killed conversion can leave unreferenced staging files, but these are not treated as completed cache entries.

The host verifies/initializes the resulting classes normally; no verification is disabled. A repaired cache is not proof that every provider operation works. All inherited Echo capability interfaces are now detected, including SearchFeedClient inherited through QuickSearchClient.

## Measured scope on the development machine

One Saavn activation (`yt-9427445.eapk`) at the unchanged Java `-Xmx1024m` limit measured:

| Run | Wall time (Java startup through initialization/exit) | Observed peak Java working set |
|---|---:|---:|
| Cold conversion | 2.08 seconds | 294.3 MiB |
| Cached conversion, fresh Java process | 0.56 seconds | 94.2 MiB |

These are individual measurements, not averages or application-wide memory figures. They exclude BingeX/LibVLC and source streaming, and are not a guarantee for larger extensions. No helper is started until activation. Additional local dependency JARs were roughly 17.5 MB before compression; none are added to the installer.

Validation: standard/enhanced profile verification and opt-in checks, missing/unverified extra components, inherited capability and loader regressions; actual UI Saavn home/search, muted native playback and explicit return to the standard failing path; actual UI YouTube Music activation/home/search/playback and successful return to its standard path; synthetic OpenSubsonic login, paging, artists/playlists, native playback, persistent music controls, cancellation/reactivation, history exclusions, offline retention and removal. Deezer activation/login-form discovery was verified in the prior isolated experiment; account playback is not verified. Spotify and ASMR One remain unresolved. A standalone YouTube decoder probe timed out during native initialization on one attempt; its full app UI playback check subsequently passed. See [experiment scope and package hashes](dartotsu-echo-experiment.md).

## Distribution review

The reviewed upstream bridge root has a custom Unabandon Public License with an additional public-source requirement. The compatibility Java files preserve their Suwayomi/MPL-2.0 notices. Mozilla's [MPL FAQ](https://www.mozilla.org/en-US/MPL/2.0/FAQ/) describes file-level source/notice and combination requirements; it is not clearance of this particular combined runtime. Compatibility of the custom upstream terms with BingeX's GPL distribution remains unresolved. Therefore upstream compiled components, SDKs and new dependencies are not embedded or downloaded by BingeX; only the original opt-in host/adapter/build tooling is in the repository. A process boundary or separately selected runtime is not asserted to settle combined-work licensing. Complete that review and all corresponding-source/dependency checks before distributing a runtime bundle. Existing [optional-runtime distribution limits](licensing.md) continue to apply.
