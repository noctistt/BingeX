# Echo music helper (development)

Original BingeX code, GPL-3.0-or-later. No upstream SDK, engine, compiler, converted extension or runtime is included. This helper executes explicitly selected trusted extension code with the user's permissions; it is not a security sandbox.

Use run-integration.ps1 to compile and test with the documented OpenSubsonic .eapk and synthetic local server. Supply the trusted Java executable, cs3_engine runtime directory, Echo common-jvm 1.0.0 SDK JAR, ECJ 3.38.0 compiler, extension file and a scratch WorkDirectory. Optional DotnetPath/PythonPath select local tools. See ../../docs/echo-bridge.md for exact upstream revisions, hashes and the license review. The runner does not download anything except ordinary .NET restore dependencies.

## Wire contract

The helper starts as `java -Xmx1024m -cp <original-classes>;<sdk.jar>;<engine-jars> EchoMusicHelper <cache-directory>` on Windows. The C# EchoMusicBridge builds explicit arguments and redirects pipes; no shell is involved. Commands are serialized. Every line is a JSON request containing positive integer id, method and params object. Every reply has the same id and either result or a fatal error object. hello returns protocol=1; an incompatible version must not be loaded.

- load: absolute path, sha256, sdkSha256, entryClass; one extension per helper lifetime.
- configure: values object with strings, booleans, integers, string sets or null. Values remain in memory. Browsing handles are invalidated.
- settings: structural key/title/kind schema; callbacks and complex controls are not hosted.
- loginForms: custom text-input forms only. login takes key/data and returns opaque user handles and display names; selectUser takes handle. Private account extras never cross the pipe.
- home / search(query): return a feed handle and tabs. page takes feed/tab initially, or the opaque next handle for continuation. Results contain display title, kind and opaque handles for tracks/albums/nested feeds.
- album(handle): returns its track feed. variants(track handle): returns quality labels and opaque variant handles. resolve(variant handle): returns validated ordinary HTTP(S) GET sources and headers in memory. No arbitrary body, DRM, raw native stream or merged-source contract is accepted.
- close: exits the process. All errors are fatal; do not reuse handles or pipes after cancellation, timeout or a failed command.

Never log resolve results, login data or request bodies, and never persist authenticated source URLs as history keys. BingeX 0.14.4 connects this helper to the Music UI using the existing private player relay and skipHistory policy. Dependencies remain separately supplied.

## Local app setup

`prepare-runtime.ps1` takes the same explicit JavaPath, EngineDirectory, SdkJar and CompilerJar inputs plus a new OutputDirectory. It compiles original helper classes and creates a machine-specific echo-runtime.json referencing those files and SHA-256 fingerprints. It does not download or copy upstream dependencies. Select the profile in Add-ons > Music, import an .eapk, confirm trust and activate. Login, when required, is session-only. Keep all referenced folders in place; do not distribute this profile as a self-contained runtime.
