# Android music helper investigation

Status: feasibility/design investigation, 2026-10-22. No Android helper APK, emulator, Android system image or production integration has been built or tested. BingeX remains 0.14.7. No Windows features, user devices or BingeX profiles were changed.

## Finding

Running original extension DEX on Android ART would remove our DEX-to-JVM conversion step, which is where the reproduced Deezer, Spotify, Saavn and ASMR One failures occur. That is an architectural inference, not a compatibility result. Echo interface versions, Android context injection, extension dependencies, initialization, service availability and authentication still have to work. YouTube Music's reproduced initialization failure is not established to be a conversion failure.

The Android Emulator supports running without its graphical window via `-no-window`; it is still a complete Android virtual device. Hiding the window does not make it a lightweight Java helper. Current Android documentation also describes the newer Android CLI; choose and pin the supported tooling during an actual experiment rather than depending indefinitely on a legacy command interface. See [emulator command-line documentation](https://developer.android.com/studio/run/emulator-commandline).

## Local readiness

A read-only inspection on this machine found an Intel i5-13450HX, about 15.7 GB physical RAM and about 2.3–2.4 GB free during the checks. A Windows hypervisor was reported active. Firmware virtualization/SLAT flags were reported false; those reports are inconclusive with an active hypervisor, so do not interpret them as a reason to change BIOS settings. Hardware acceleration must be verified using the installed emulator's documented check. No SDK or emulator was found in the configured/usual SDK location or available application path by the preflight tool. An adb executable was found on the application path; it was not started or used to inspect connected devices.

`tools/echo-android/check-environment.ps1` reports these prerequisites without installing anything, accepting terms, starting adb or booting a device. If an emulator executable is present it runs only its read-only acceleration check with a ten-second limit. Tool presence alone does not establish an installed system image, a working virtual device, or compatibility.

A prototype could initially allocate two virtual CPUs and 2 GB guest RAM, then measure actual host memory and CPU usage. These are proposed experiment settings, not measured usage or a minimum guarantee. With current free memory, memory pressure is a material concern. Download size, installed disk usage, cold start, idle CPU, working set and shutdown behavior must be measured on the chosen image before offering this route to users. The RTX 3050 does not establish CPU virtualization or available RAM.

## Proposed boundary

1. A dedicated BingeX-owned Android virtual device, separate from personal emulators or phones. Do not connect to a user's existing device by default, enable network adb, modify existing virtual devices, or enable Windows features silently.
2. An original Android host APK with the real Echo Android SDK and dependencies, an Android application context, main looper and a managed service lifecycle. Load a hash-checked, explicitly selected extension with Android DexClassLoader. Use app-private storage and follow Android's read-only dynamic-code requirements. The service/context design still needs implementation and validation; a command-line app_process shell alone is not equivalent to a normal app environment.
3. Reuse the display/opaque-handle music protocol for login, catalogues, artists, playlists, variants and source resolution, but version the transport separately. Keep package trust explicit, serialize requests, enforce limits/deadlines and invalidate session handles on disconnect.
4. A narrowly scoped adb-forwarded connection to the helper, bound to guest loopback with a per-session unguessable token. Do not expose a LAN listener or public debug port. Use an isolated adb instance/port if possible and record only forwarding rules owned by BingeX. Do not kill a user's adb server during cleanup. Port forwarding and token enforcement need actual host-side tests; their mention here is not a security certification.
5. Keep normal playback in the Windows player. A resolved HTTP source may sometimes be usable directly, with its headers preserved by the existing private relay. If it depends on Android-local cookies, temporary localhost servers, TLS/session state or a provider's native networking stack, proxy audio bytes through the guest instead. Support byte ranges and backpressure; do not assume every Android URL works on Windows. DRM and arbitrary merged/native sources remain outside the current player contract.
6. Activate the Android environment only on explicit use, show preparation/boot status, permit cancellation, and stop owned helper processes when unused. Cold boot and reconnection must be bounded. No automatic fallback that silently downloads gigabytes or boots a VM.

Android documents [DexClassLoader](https://developer.android.com/reference/dalvik/system/DexClassLoader), [dynamic-code loading changes](https://developer.android.com/about/versions/14/behavior-changes-14#safer-dynamic-code-loading), [service lifecycle constraints](https://developer.android.com/develop/background-work/services), [adb forwarding](https://developer.android.com/tools/adb#forwardports) and [emulator networking](https://developer.android.com/studio/run/emulator-networking-address). These establish platform mechanisms, not that our unbuilt helper meets them.

## Proof-of-concept acceptance gates

- Boot one isolated accelerated device and measure cold/warm startup, idle/active memory and CPU, and disk/download sizes. Test clean shutdown and host reboot recovery without touching unrelated adb sessions.
- Run a synthetic OpenSubsonic account/catalogue fixture through the Android host: login, paging, artist/playlist navigation, resolution, range streaming into Windows LibVLC, cancellation, process death, reactivation and credential/history exclusions.
- Test the exact previously failing extension versions on ART. Separate successful class loading, actual initialization, catalogue loading and playback; do not label loading alone as full support. Do not use real account credentials during the first tests.
- Verify guest-only sources, authenticated redirects and range seeking through the byte proxy. Keep tokens out of logs, settings backups and history. Explicitly reject unsupported response/DRM contracts.
- Exercise interactive login separately. A permanently hidden emulator does not provide a usable browser/WebView sign-in flow; some providers may need a temporary visible login surface or remain unsupported.
- Decide whether measured cost is acceptable before implementing an installer or a normal user-facing switch. Preserve the existing lightweight JVM path for providers that already work.

## Distribution and next step

Google's SDK/system-image terms and Echo's custom SDK license require their own review. Android SDK use requires accepting its agreement; a user's request to investigate is not acceptance of those terms. Do not auto-accept agreements or assume emulator/system-image redistribution rights. See [Android SDK terms](https://developer.android.com/studio/terms) and the existing [Echo license findings](echo-bridge.md#sdk-license-review-outcome). AOSP source availability is not permission to redistribute an arbitrary downloaded Google image.

The next concrete experiment is a separately provisioned local Android test environment and a minimal host APK. No upstream SDK/image is bundled, no packages are downloaded by this investigation, and no readiness/compatibility pass is claimed. This is a plausible optional compatibility route with substantial setup cost, not a finished workaround or a promise that every provider will work.
