# Licensing and sharing builds

BingeX original work is GPL-3.0-or-later. LICENSE contains the unmodified GPLv3 text; COPYRIGHT.md grants the version-3-or-later option and defines scope. No permission application or registration is needed to exercise rights already granted by the relevant license. This does not grant content or service-access rights.

## Review performed (2026-10-22)

The resolved NuGet metadata was inspected for all packages in the current project.assets.json. The declared expressions are MIT, Apache-2.0, LGPL-2.1-only and LGPL-2.1-or-later, or package-specific license files. MonoTorrent 3.0.2, Mono.Nat 3.0.0 and ReusableTasks 4.0.0 include MIT texts. The full package/version inventory is dependency-licenses.json. Entries with file-based licenses require retaining the supplied texts; an inventory is evidence of metadata, not a legal certification of every constituent file.

VideoLAN.LibVLC.Windows 3.0.24 and LibVLCSharp 3.10.2 declare LGPL-2.1-or-later. Native VLC modules and codec dependencies may have additional terms. Verify the exact shipped module set, third-party notices, complete corresponding source and build configuration; a link to the VLC repository alone is not proof that these obligations are fulfilled. In particular, do not assume GPL-2.0-only code can form a combined work with GPLv3 code. Remove incompatible components or obtain an appropriate license if that review finds any.

The pinned local SkyStream checkout f5523ae07d729b579be30976df8783a0b98c4559 contains GPLv3 texts at its root and packages/cs3_engine/LICENSE. BingeX's original patch changes its audio-track JSON mapping. Its source and build commands are in tools/cloudstream-patch. The engine has many constituent JARs; a separate per-JAR license/source inventory remains necessary before redistributing a prepared runtime. User-selected extensions require their own review. Temurin's legal directory must remain intact; use the exact source/version corresponding to the download lockfile.

TagLibSharp 2.3.0 was added for read-only music metadata. Its package declares LGPL-2.1-only; source: https://github.com/mono/taglib-sharp/tree/TaglibSharp-2.3.0.0 . Its shared DLL remains replaceable, and packaging includes its COPYING text. BingeX never calls Save on user music files.

## What packaging supplies

packaging/copy-notices.ps1 copies BingeX LICENSE, COPYRIGHT.md, third-party notices, this distribution guide, resolved package license/notice files and package manifests. It also creates BingeX-source.zip from the tracked working-tree files and SOURCE-INFO.txt with revision and build guidance. The source snapshot includes the original Java patch and its build instructions; dependencies are restored separately. It is not a full corresponding-source bundle for native libraries or the optional upstream runtime.

Source repository: https://github.com/noctistt/BingeX . Retain the source snapshot for each binary you distribute so later repository changes do not disconnect recipients from that version. Do not redistribute an old binary as though it was built from a newer snapshot. Upstream source references are in THIRD_PARTY_NOTICES.md and the package manifests.

## Before sharing a build

- Supply the matching source snapshot and required license/notice files with the binary, through the same download or another method allowed by the applicable license.
- Complete the native-library source/build-config check and any optional-runtime dependency review described above. Obtain and preserve the needed source materials; do not substitute an unsupported promise to provide source later.
- Keep native shared libraries replaceable and do not impose terms preventing permitted modification, debugging or redistribution.
- Contributions and paid previews are permitted, but recipients retain GPL rights to source, modification and redistribution. Do not require exclusivity, secrecy or a no-redistribution agreement that conflicts with those rights.
- New contributions must be offered under the project's GPL-3.0-or-later license and must not include code/assets the contributor lacks permission to license.

Licensing the original project is complete; the native and optional-runtime distribution checks above remain open. This document does not certify a particular installer as fully compliant or determine jurisdiction-specific patent, service-contract, copyright or circumvention questions.


## Archived Echo helper research

As of 0.14.13, Echo code/tools/tests are historical sources under archive/echo, excluded from the active application. The original Echo helper is no longer embedded. The paragraphs below record prior implementation and review limits, not current runtime contents.

The music helper is original source with a separate C# process transport. Starting in 0.14.7, the application embeds compiled original BingeX helper classes (GPL-3.0-or-later); source and the rebuild script are under archive/echo/tools/echo-helper. The archive contains no SDK, engine, compiler or provider classes. The external Echo common-jvm SDK uses the custom Unabandon Public License and remains separately supplied, without SDK bundling or automatic downloads. See archive/echo/docs/echo-bridge.md for the license review and remaining combination/distribution questions. The NuGet inventory does not certify this optional Java runtime. Keep upstream SDK/runtime bundling and automatic installation out of the app until that review is resolved.

## Optional enhanced music route (0.14.11)

BingeX ships only the original opt-in host/adapter and local preparation tooling. The prepared compatibility component uses separately supplied Dartotsu bridge sources (custom Unabandon license), compatibility sources with retained MPL-2.0 notices, and external converter/ICU dependencies. The custom-license/GPL combination and runtime redistribution review remains unresolved; do not bundle or automatically download that component. The local builder retains source/notices and fingerprints, but does not certify a redistributable bundle. See [setup, test scope and review outcome](../archive/echo/docs/echo-enhanced-compatibility.md).
