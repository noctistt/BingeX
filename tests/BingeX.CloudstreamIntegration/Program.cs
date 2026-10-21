using BingeX;
using LibVLCSharp.Shared;

if (args.Length != 4) throw new ArgumentException("isolated-data repository-url extension-name query");
var data = Path.GetFullPath(args[0]);
await CloudstreamRuntime.Download(data, new Progress<string>(Console.WriteLine), CancellationToken.None);
await using var service = new CloudstreamService(data);
var extensions = await service.Repository(args[1], CancellationToken.None);
Console.WriteLine($"PASS: repository returned {extensions.Count} extensions.");
var extension = extensions.Single(x => x.Name.Equals(args[2], StringComparison.OrdinalIgnoreCase));
await service.Install(extension, CancellationToken.None);
Console.WriteLine("PASS: checksum-verified install and provider load.");
using var client = new AddonClient { CloudstreamRequest = service.Request };
var library = new MediaLibrary(client);
InstalledAddon[] installed = [CloudstreamService.Addon(extension)];
var catalogs = await library.Catalogs(installed, CancellationToken.None);
if (catalogs.Items.Count == 0) throw new Exception("No catalog exposed.");
var results = await library.Search(catalogs.Items, args[3], CancellationToken.None);
if (results.Items.Count == 0) throw new Exception("No titles through MediaLibrary.");
Console.WriteLine($"PASS: generic Discover search returned {results.Items.Count} titles.");
var title = await library.Details(results.Items[0], installed, CancellationToken.None);
var streams = await library.Streams(title.Type, title.Id, installed, CancellationToken.None);
var playable = streams.Items.Where(x => x.UnsupportedReason == null).ToArray();
if (playable.Length == 0) throw new Exception("No playable streams through MediaLibrary.");
Console.WriteLine($"PASS: generic title and stream mapping returned {playable.Length} supported streams.");
Console.WriteLine($"Stream audio languages: {string.Join(", ", playable[0].AudioOptions?.Select(x => x.Label) ?? [])}");
Core.Initialize();
using var vlc = new LibVLC("--quiet", "--vout=dummy", "--aout=dummy", "--no-video-title-show");
var played = false;
foreach (var choice in playable.Take(4))
{
    Console.WriteLine("Testing stream " + choice.Name);
    using var player = new MediaPlayer(vlc);
    using var media = new Media(vlc, choice.Url, FromType.FromLocation);
    if (choice.AudioUrl != null) media.AddSlave(MediaSlaveType.Audio, 4, new Uri(choice.AudioUrl));
    foreach (var header in choice.RequestHeaders ?? new Dictionary<string, string>())
    {
        if (header.Key.Equals("Referer", StringComparison.OrdinalIgnoreCase)) media.AddOption(":http-referrer=" + header.Value);
        if (header.Key.Equals("User-Agent", StringComparison.OrdinalIgnoreCase)) media.AddOption(":http-user-agent=" + header.Value);
    }
    if (!player.Play(media)) continue;
    var until = DateTime.UtcNow.AddSeconds(25);
    while (DateTime.UtcNow < until && player.State != VLCState.Error)
    {
        await Task.Delay(250);
        if (player.Time > 3000 && media.Statistics.DecodedVideo > 0 && media.Statistics.DecodedAudio > 0)
        {
            played = true; Console.WriteLine("PASS: decoded video AND audio beyond three seconds.");
            if (choice.AudioOptions is { Count: > 1 } options)
            {
                var alternative = options.FirstOrDefault(x => x.Language == "hi") ?? options[1];
                var before = player.Time;
                var oldTrack = player.AudioTrack;
                var decoded = media.Statistics.DecodedAudio;
                if (!player.AddSlave(MediaSlaveType.Audio, alternative.Url, true)) throw new Exception("Could not switch stream audio.");
                var switchedUntil = DateTime.UtcNow.AddSeconds(20);
                while (DateTime.UtcNow < switchedUntil && (player.Time <= before + 1500 || media.Statistics.DecodedAudio <= decoded || player.AudioTrack == oldTrack)) await Task.Delay(250);
                if (player.Time <= before + 1500 || media.Statistics.DecodedAudio <= decoded || player.AudioTrack == oldTrack) throw new Exception("Audio switch did not select another track and continue decoding.");
                Console.WriteLine("PASS: changed to " + alternative.Label + " while video continued.");
            }
            break;
        }
    }
    player.Stop();
    Console.WriteLine($"Attempt finished; combined decoding={played}.");
    if (played) break;
}
if (!played) throw new Exception("No combined audio/video playback verified.");
await service.DisposeAsync();
await using var reopened = new CloudstreamService(data);
if (!reopened.Installed.Any(x => x.Key == extension.Key)) throw new Exception("Install did not persist.");
Console.WriteLine("PASS: installation persists after helper restart.");
await reopened.Remove(extension);
if (reopened.Installed.Any(x => x.Key == extension.Key)) throw new Exception("Removal failed.");
Console.WriteLine("PASS: extension removed.");
