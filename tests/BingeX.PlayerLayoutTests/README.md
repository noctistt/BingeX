# Native player layout regression (Windows)

Runs the actual application renderer and VLC window. Supply a playable video of at least 20 seconds, or a local HTTP/HLS fixture. No add-on credentials are needed. For HTTP tests, use a local fixture server that returns an error for `unavailable-fixture.mp4`. The test never reports to an external service.

```powershell
$env:BINGEX_DATA_DIRECTORY = Join-Path $env:TEMP ([Guid]::NewGuid().ToString())
dotnet run --project tests/BingeX.PlayerLayoutTests -c Release -- C:\fixtures\video.mkv
```

Exit 0 indicates completion. Results are written to `player-layout-test.log` in the isolated data directory. Exit 2 means missing arguments, data directory, or Windows. Do not point this test at your normal BingeX data directory.

Renders 100 source cards, injects a two-second native wait and requires immediate Player navigation with dispatcher gaps below 500 ms during that wait. Checks first playback from Details, continued playback, four mini-to-new-stream transitions with normal/maximized resizing, restoring the existing session, pause/resume, seek-to-end, replay, alternate-audio restoration when available, and failed HTTP source fallback. Assertions compare actual Win32 video client dimensions, DPI scaling, overlay dimensions, page visibility and mutually exclusive controls. A watchdog detects UI stalls. This is an interactive desktop test, not a headless CI test. A pass does not reproduce or certify third-party streams or every GPU/display configuration.

For visual inspection, set `BINGEX_LAYOUT_NATIVE_DELAY_MS=10000` to extend the injected wait (bounded to ten seconds). The application itself has no artificial startup delay.
