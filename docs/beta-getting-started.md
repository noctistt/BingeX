# BingeX beta: getting started

For Windows x64 preview **0.13.24**. This guide prepares the first public beta; it does not announce a release. See [known issues](beta-known-issues.md) before testing.

## Install and open

1. Obtain the Windows x64 installer from the download channel linked by the BingeX project. No public Discord or supporter URL has been announced in this repository yet.
2. Close an older BingeX instance, then run the installer. The .NET runtime and native player are included; a separate .NET installation is not needed.
3. Optionally enable **Open with** registration and a desktop shortcut. Choose BingeX in Windows if you want it as your default player.

The installer is currently unsigned. Check where the file came from before running it. Normal installation is for the current user. Uninstall preserves your library/history data.

The opening logo fades briefly. Click to skip it; opening a video directly bypasses it.

## Watch a local video

Use **Open media**, drag a video into the window, or use Windows **Open with → BingeX** after registration.

Click the video to pause/play; double-click for fullscreen. Hover over buttons for their labels.

| Shortcut | Action |
| --- | --- |
| Space | Pause/play; replay after completion |
| Left / Right | Back/forward 10 seconds |
| F or F11 | Toggle fullscreen |
| M | Mute/unmute |
| C | Toggle selected subtitles |
| S | Playback settings |
| G / H | Subtitle timing earlier/later by 50 ms |
| Alt + Left | Back |
| Escape | Close settings, then leave fullscreen |

**CC** opens the quick subtitle picker; **More subtitle settings** opens timing/appearance controls. Playback settings contain audio-track and speed controls. You can load matching external subtitles and audio files. Available tracks depend on the video and source.

Resume uses saved progress. Completed videos restart; sources that cannot seek may play from the start.

## Build a local library

Open **My Library → Add folder**. BingeX indexes video files in that folder and its subfolders without moving them. Use **Rescan folders** after adding or moving files.

- Search using words from filenames or folder paths; dots and underscores in filenames are handled as separators. **Clear** restores the full list.
- Sort by **Name**, **Recently added**, or **Last watched**.
- Group by **Folder**, **Series and season**, or **All videos**. Click a group heading to collapse it. Sort/view choices are remembered.
- Series grouping recognises patterns such as `Show.S01E02`, `Show 1x02`, and `Show/Season 1/Episode 02`. It does not identify every naming style.
- Removing a library folder stops indexing it; it does not delete your videos.

**Continue Watching** shows unfinished videos, newest first. Its cross removes an entry from history, not the video file.

## Optional online mode

Online mode starts off. Turn it on to show catalogs, add-ons and online subtitle search. Turning it off hides these features and retains saved data; it is not a network-disconnection switch for playback already running.

### Stremio add-ons

In **Add-ons**, install the provider's configured manifest URL. A catalog supplies titles and descriptions; it may not supply playable sources. Some providers require configuration or an account before installation. Treat configured URLs as private because they can contain credentials.

Choose a catalog/provider under Home, open a title, select an episode if applicable, and choose **Find streams**. Filter results by provider, resolution, audio language or audio type. Filters start at **All** for each search; missing labels remain **Unknown**.

**Include torrent sources** is off by default and remembered. Off excludes peer-to-peer torrent results from selection and automatic playback. Direct web streams, including compatible debrid links supplied by configured add-ons, remain eligible. **Try suggested torrent** only appears for matching torrent results. Mixed providers may still be queried for their direct streams.

### Cloudstream extensions

The separate Cloudstream section offers the optional bridge/runtime installer, repository reading and extension installation. Use repositories and extensions you trust: they execute code with your user permissions. No provider extension is bundled. Supported repository links include HTTPS and `cloudstreamrepo://` links.

An extension title searches its own provider by default. **Search all providers** can include compatible Stremio add-ons; a shared title ID or an explicit title match may be needed. This does not search every unrelated Cloudstream extension.

## While watching online

**Back** keeps playback in the mini-player; **Restore** returns to the full player. **Choose another source** opens sources for the playing title/episode and preserves the last known position when a replacement supports seeking.

Next-episode playback attempts the same provider and matches known quality/audio information. If it cannot find a match, choose an alternative manually. Optional episode autoplay can be enabled in playback settings.

Buffering means playback is waiting for data. A prolonged stall or failure shows recovery information. A listing is not a guarantee that its source is available.

Online subtitle search is available through CC when online mode is enabled. Extension captions appear only when the provider returns them. Loading online captions downloads their text into memory; it does not mean you manually saved a subtitle file.

## Report a problem

Use the template in [known issues and troubleshooting](beta-known-issues.md#reporting-a-problem). Include the version and reproduction steps. Do not post private manifest URLs, API keys, cookies, tokens, your library database, or unreviewed logs.
