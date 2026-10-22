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

Never log resolve results, login data or request bodies, and never persist authenticated source URLs as history keys. The existing BingeX player relay/skipHistory policy must be used when a future UI starts these streams. The helper is deliberately not connected to the app's Add-ons installer yet.
