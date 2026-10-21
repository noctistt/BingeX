using BingeX;
using LibVLCSharp.Shared;

if (args.Length < 4)
    throw new ArgumentException("Arguments: java.exe engine-directory isolated-data-directory extension.cs3 [search-query]");
await using var bridge = new CloudstreamBridge(args[0], args[1], args[2]);
var hello = await bridge.CallAsync("hello", new { });
Console.WriteLine($"PASS: Windows helper started; engine {hello.GetProperty("engine").GetString()}.");
if (args[3] == "--lifecycle")
{
    try
    {
        await bridge.CallAsync("nonexistent-method", new { });
        throw new Exception("Engine error was not reported.");
    }
    catch (InvalidOperationException) { Console.WriteLine("PASS: Engine error reported."); }
    try
    {
        await bridge.CallAsync("hello", new { });
        throw new Exception("Failed helper was reused.");
    }
    catch (ObjectDisposedException) { Console.WriteLine("PASS: Failed helper cannot be reused."); }
    await bridge.DisposeAsync();
    await bridge.DisposeAsync();
    Console.WriteLine("PASS: Repeated disposal is safe.");
    return;
}
var plugin = Path.GetFullPath(args[3]);
var loaded = await bridge.CallAsync("loadPlugin", new { path = plugin });
var providers = loaded.GetProperty("providers").EnumerateArray().ToArray();
if (providers.Length == 0) throw new Exception("Extension registered no providers.");
Console.WriteLine($"PASS: Real .cs3 loaded; {providers.Length} provider(s). Conversion: {loaded.GetProperty("notes")}");
foreach (var provider in providers)
{
    var name = provider.GetProperty("name").GetString()!;
    var result = await bridge.CallAsync("search", new { provider = name, plugin, query = args.ElementAtOrDefault(4) ?? "His Girl Friday" });
    var items = result.GetProperty("items").EnumerateArray().ToArray();
    Console.WriteLine($"Search returned {items.Length} item(s).");
    if (items.Length == 0) throw new Exception("No search results: loading succeeded, catalog-to-playback is NOT proven.");
    var detail = await bridge.CallAsync("load", new { provider = name, plugin, url = items[0].GetProperty("url").GetString() });
    Console.WriteLine("PASS: Title details loaded.");
    var links = await bridge.CallAsync("loadLinks", new { provider = name, plugin, data = detail.GetProperty("dataUrl").GetString() });
    var count = links.GetProperty("links").GetArrayLength();
    if (count == 0) throw new Exception("No stream links returned: playback is NOT proven.");
    Console.WriteLine($"PASS: {count} stream link(s) resolved. URLs intentionally omitted.");
    if (args.Contains("--playback"))
    {
        Core.Initialize();
        using var vlc = new LibVLC("--quiet", "--vout=dummy", "--aout=dummy", "--no-video-title-show");
        var played = false;
        foreach (var link in links.GetProperty("links").EnumerateArray().Take(4))
        {
            Console.WriteLine($"Testing stream: {link.GetProperty("name").GetString()}, type {link.GetProperty("type").GetString()}.");
            using var player = new MediaPlayer(vlc);
            using var media = new Media(vlc, link.GetProperty("url").GetString()!, FromType.FromLocation);
            if (link.TryGetProperty("referer", out var referer) && !string.IsNullOrEmpty(referer.GetString()))
                media.AddOption(":http-referrer=" + referer.GetString());
            if (link.TryGetProperty("headers", out var headers))
                foreach (var header in headers.EnumerateObject())
                    if (header.Name.Equals("User-Agent", StringComparison.OrdinalIgnoreCase))
                        media.AddOption(":http-user-agent=" + header.Value.GetString());
            if (!player.Play(media)) continue;
            var until = DateTime.UtcNow.AddSeconds(25);
            while (DateTime.UtcNow < until && player.State != VLCState.Error)
            {
                await Task.Delay(250);
                if (player.Time >= 3000 && media.Statistics.DecodedVideo > 0)
                {
                    played = true;
                    Console.WriteLine("PASS: LibVLC playback advanced beyond 3 seconds with decoded video frames.");
                    break;
                }
            }
            player.Stop();
            if (!played) Console.WriteLine("Stream did not meet the video decoding check within 25 seconds.");
            if (played) break;
        }
        if (!played) throw new Exception("Resolved streams did not produce decoded video in LibVLC.");
    }
}
