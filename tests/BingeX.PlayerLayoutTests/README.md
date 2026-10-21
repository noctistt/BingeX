# Native player layout regression (Windows)

Runs the actual application renderer and VLC window. Supply a playable video of at least 20 seconds, or a local HTTP/HLS fixture. No add-on credentials are needed.

```powershell
$env:BINGEX_DATA_DIRECTORY = Join-Path $env:TEMP ([Guid]::NewGuid().ToString())
dotnet run --project tests/BingeX.PlayerLayoutTests -c Release -- C:\fixtures\video.mkv
```

Exit 0 indicates completion. Results are written to `player-layout-test.log` in the isolated data directory. Exit 2 means missing arguments, data directory, or Windows. Do not point this test at your normal BingeX data directory.

Checks first playback from Details, continued playback, four mini-to-new-stream transitions with normal/maximized resizing, and restoring the existing session. Assertions compare actual Win32 video client dimensions, DPI scaling, overlay dimensions, page visibility and mutually exclusive controls. A watchdog detects UI stalls. This is an interactive desktop test, not a headless CI test. A pass does not reproduce or certify third-party streams or every GPU/display configuration.
