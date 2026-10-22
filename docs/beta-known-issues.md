# BingeX beta: known issues and troubleshooting

Applies to preview **0.13.24**. These are current limitations and troubleshooting notes, not a promise that every provider or media file has been tested.

## Current limits

| Area | Limit or expected behaviour |
| --- | --- |
| Platform | The packaged build targets Windows x64. Other platforms are not ready for this beta. |
| Installation | Development installers are unsigned. The public download channel is not configured yet. |
| Local library | Folder scans are manual. Moved files are not automatically relinked; cached missing entries can remain. Local next-episode/autoplay is still planned. |
| Series grouping | Depends on recognisable filename/folder patterns; it does not fetch or verify show metadata. |
| Catalogs | Some catalog pagination and required provider filters are not implemented. Providers may return incomplete metadata. |
| Provider availability | Providers can be slow, rate-limited, require configuration, or return expired/nonworking links. Early results can appear while others are pending. |
| Cross-provider matching | Shared IDs and reliable episode numbers are needed. Movie-labelled channels, anime specials and site-specific IDs may not map safely. |
| Stream labels | Resolution, language and audio type depend on provider evidence. Unknown is intentional. A filter can legitimately return no results; use Reset filters. |
| Headers and protection | Valid custom request headers are supported. Malformed headers and custom response-header overrides remain unsupported. DRM is unsupported by the Cloudstream bridge. |
| Extensions | Cloudstream compatibility is experimental. Android/browser/UI-dependent extensions may fail. The helper is not a security sandbox. |
| Subtitles/audio | Sources may return no captions or omit spoken-language labels. Separate releases/cuts can need manual timing adjustment. |
| Seeking/recovery | A replacement source must support seeking to resume. The same timestamp can differ between edits; provider metadata cannot verify the actual video contents. |
| Next episode | Automatic selection is conservative. Different provider/known quality/audio, reused stream identities or conflicting episode labels require manual choice. |
| Torrent playback | Requires available peers and disk space. Downloads are cached; torrent speed and seeking depend on the swarm and available pieces. It is not anonymous. V2-only torrents and some tracker/archive flows are unsupported. |

Torrent cache budget is 32 GiB and reserves space for the whole torrent, so large season packs can be rejected. **My Library → Clear unused video cache** removes inactive torrent downloads. It does not delete your personal media files.

## Quick troubleshooting

- **No streams:** check whether the installed provider actually supplies streams and whether it needs configuration. For an extension, check the selected episode and Search all providers scope. Reset source filters; torrents remain hidden unless explicitly enabled.
- **Unexpected empty audio results:** choose All or Reset filters. A source with no advertised language may appear under Unknown.
- **Buffering or failure:** read the player status, wait briefly, or choose another source. Torrent download percentage is not the amount buffered ahead of playback.
- **Wrong episode:** stop it and choose manually. Report the provider and displayed season/episode labels; do not assume a matching title proves the video is correct.
- **No subtitles:** check CC, load your own matching file, or try online subtitle search with online mode enabled. Not every extension returns captions.
- **Local video unavailable:** reconnect its drive, check its location and rescan. Removing a folder from the library does not remove its files.
- **Old Windows icon:** close and reopen BingeX after upgrading. Windows may cache shortcut icons.

## Reporting a problem

Copy this into the project's issue tracker or its announced Discord bug-report channel:

```text
BingeX version:
Windows version:
Local file / Stremio add-on / Cloudstream extension / torrent:
Provider or extension name (no private install URL):
Title and season/episode, if relevant:
Steps to reproduce:
1.
2.
Expected behaviour:
Actual behaviour and exact message:
Does it happen with another file/source?:
Screenshot, with private details hidden:
```

App data is stored in `%LOCALAPPDATA%\BingeX`. The database can contain configured URLs with credentials. Keep it private. Review logs before sharing them and provide only the relevant redacted excerpt if requested.

## Before publishing the first beta

- Choose the project's own licence: the README currently says it has not been selected. Review bundled third-party notices and applicable source-distribution requirements.
- Test a clean Windows installation, upgrade, uninstall and Open with registration on a separate profile or machine. Existing developer-profile checks do not prove a clean install.
- Verify a local video, folder indexing, subtitles, audio selection, fullscreen/mini-player and source-switch resume in the packaged build.
- Verify online mode starts off, torrent sources start off, and private provider credentials are absent from release files and screenshots.
- Publish the exact version, download location, file checksum and these known issues. Discord/supporter URLs still need to be supplied; none are invented here.
- Keep stable builds free. Optional supporters can receive early previews and development updates. Do not advertise third-party content access as a membership benefit.

This documentation change does not publish a build, create a Discord server or activate memberships.
