# Now Playing and provider-independent lyrics - 0.14.16

Click the current song title in the bottom music bar to open Music's Now Playing section. It shows large artwork, title, artist, album and source when supplied by the track, controls and a bounded current queue. MusicFree metadata comes from the playing queue, so browsing a different provider does not change its identity. Indexed local music supplies TagLib metadata/artwork. Server tracks supply available title, artist and album; server artwork and artist biographies are not added in this version. Missing information uses a visible fallback.

## Lyrics

Lyrics use an independent LRCLIB client, not a MusicFree or video add-on. Opening Now Playing starts a lookup when Online mode is enabled and no cached/local lyrics exist. Only title and artist are sent to the service; album/duration are used locally for matching. No account, stream URL, cookies or provider tokens are sent. Opening the view does not upload audio. Network requests are cancelled on track changes, offline mode and app close, with bounded time/body/result sizes.

Automatic selection requires exactly one candidate with normalized title and artist, matching album when supplied, and duration within two seconds. Unknown duration, uploader names, remixes and ambiguous results require manual selection. Edit details under Match another recording and choose Find lyrics. Results show recording durations and the difference from the playing track. Service coverage/timing can vary. Plain lyrics and instrumental results have explicit states; no automatic transcription or word-by-word timing is added.

Load .lrc file also accepts plain .txt lyrics. Local audio automatically checks for a same-name .lrc sidecar. Files are read only, bounded to 256 KB, and never modified. Lyric parsing supports fractional timestamps, repeated/multiple timestamps, simultaneous lines, blank timed clearing and LRC offset metadata. Enhanced word timestamps are displayed as plain line text. Up to 1000 synchronized lines are displayed.

Highlighting follows the native player's clock through a bounded interpolation clock, including pause/buffering, seeking and speed changes. Positive delay values show lines later; negative values show them earlier. Clicking a timed line seeks to its adjusted media position when seeking is available. Matches, local lyrics and per-track delay are remembered for up to 25 tracks in this session only; they are not written to the library/history/settings backup. Cached lyrics can continue to display when Online mode is off.

## Validation

Parser/client fixture checks: timestamps and file offsets, simultaneous/repeated lines, blank clearing, plain fallback, seek/delay boundaries, conservative title/artist/album/duration matching, remix/uploader rejection, metadata-only independent requests, cancellation and size limits.

A live LRCLIB metadata request returned synchronized lyrics; lyric bodies were not printed or retained in the test report. Actual UI test with muted native music: Now Playing artwork and artist details, local LRC import through the same loader used by the file picker, active-line highlighting, seek following, delay adjustment and next-track reset. Existing Explore/provider switching, persistent controls, video-history exclusion and offline cancellation checks also passed. The file-picker dialog itself was not automated. Local/server native playback routes remain existing functionality; no real server account was used for this change.

Public API/project: https://lrclib.net/docs and https://github.com/tranxuanthang/lrclib. The app uses the public API; it does not bundle the service or a lyric corpus.
