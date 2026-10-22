# Project scope and distribution

BingeX is an independent desktop media player for local files and user-selected network streams. It includes optional Stremio protocol support and an experimental Cloudstream compatibility bridge. It is not affiliated with or endorsed by those projects or third-party services.

## What is included

- LibVLC playback, local library, subtitles and optional MonoTorrent playback.
- Generic add-on and extension interfaces. No streaming extensions, configured provider repositories, media catalog database, media files or service credentials are bundled.
- Online mode is off on a fresh profile. Its switch hides online features and preserves configuration; it is not a firewall or a guarantee of zero network activity.
- Cinemeta's manifest is built into the explicit Connect movie catalog action and subtitle title-search fallback. The OpenSubtitles v3 manifest is a built-in subtitle-search fallback. These are third-party services, not BingeX-operated services.
- Optional torrent sessions can use built-in public tracker endpoints when a source supplies none. BitTorrent can upload as well as download and exposes network information to peers.
- The optional bridge downloads pinned SkyStream engine and Temurin Java files after the user's action. Upstream URLs, hashes, source and license attribution remain documented. Extensions run with the user's permissions; the helper is not a security sandbox.
- The community reports action opens the general external add-on directory without including installed provider names, configuration paths or tokens.

Use content and services you are authorized to access. Compatibility does not establish permission to access, reproduce or distribute media. The player does not implement DRM decryption or promise to bypass subscriptions, ads, authentication or browser challenges. Third-party extensions are separate code and can change behavior independently.

## Privacy in contributions

Do not attach real databases, private manifests, account tokens, signed media URLs, cookies, extension binaries, copyrighted media or screenshots showing private configuration. Use reserved example domains and synthetic media metadata in tests. Sanitize diagnostics before posting them. A gitignore rule does not remove files already tracked or erase Git history.

## Sharing builds and supporting development

This remains a development project; no release schedule or public binary hosting is promised. A chat invite or support link should be added only once its actual URL is supplied. Optional contributions support software development, not access to third-party media or services.

BingeX original work is licensed under GPL-3.0-or-later. See LICENSE and COPYRIGHT.md. Third-party components retain their own licenses. See licensing.md for the current review scope and unresolved distribution requirements.

Before sharing a binary, including privately or with supporters, check the exact dependency versions, packaged license texts and corresponding-source obligations, including native VLC plugins and the optional GPL engine/patch interaction. THIRD_PARTY_NOTICES.md and packaging notices document components but are not a completed legal compliance review. Preserve required notices and provide source in the manner required by each applicable license. Supporter access must not add restrictions that conflict with those licenses.

Documentation and quiet distribution do not determine legality or prevent claims. Review the actual code, dependencies, content access and distribution practices; obtain jurisdiction-specific advice for a claim or commercial distribution. Do not ignore a legal notice or assume it applies to this project merely because it concerns another player.
