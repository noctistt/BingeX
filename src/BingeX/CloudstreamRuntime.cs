using System.IO.Compression;
using System.Diagnostics;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

namespace BingeX;

public static class CloudstreamRuntime
{
    public const string Revision = "f5523ae07d729b579be30976df8783a0b98c4559";
    public static string Home(string data) => Path.Combine(data, "cloudstream", "runtime", Revision);
    public static string Java(string data) => Path.Combine(Home(data), "java", "bin", "java.exe");
    private const string PatchVersion = "audio-2";
    public static bool Ready(string data) => File.Exists(Path.Combine(Home(data), "ready")) && File.ReadAllText(Path.Combine(Home(data), "ready")) == Revision + PatchVersion && File.Exists(Java(data));
    private static JsonElement Asset(string name)
    {
        using var source = Assembly.GetExecutingAssembly().GetManifestResourceStream("BingeX.Assets." + name)!;
        using var document = JsonDocument.Parse(source);
        return document.RootElement.Clone();
    }
    public static async Task Download(string data, IProgress<string> progress, CancellationToken token)
    {
        if (Ready(data)) return;
        var home = Home(data);
        Directory.CreateDirectory(home);
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        async Task Fetch(string url, string path, string hash)
        {
            if (File.Exists(path) && Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(path, token))).Equals(hash, StringComparison.OrdinalIgnoreCase)) return;
            using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token);
            response.EnsureSuccessStatusCode();
            var temporary = path + ".download";
            await using (var output = File.Create(temporary)) await response.Content.CopyToAsync(output, token);
            await using var input = File.OpenRead(temporary);
            var actual = Convert.ToHexString(await SHA256.HashDataAsync(input, token));
            if (!actual.Equals(hash, StringComparison.OrdinalIgnoreCase)) throw new IOException("Runtime integrity check failed.");
            input.Close();
            File.Move(temporary, path, true);
        }
        var engine = Path.Combine(home, "engine");
        Directory.CreateDirectory(engine);
        var files = Asset("cloudstream-engine.json").EnumerateArray().ToArray();
        for (var i = 0; i < files.Length; i++)
        {
            var name = files[i].GetProperty("name").GetString()!;
            progress.Report($"Downloading bridge components {i + 1}/{files.Length}…");
            await Fetch($"https://raw.githubusercontent.com/akashdh11/skystream/{Revision}/packages/cs3_engine/assets/engine/{name}", Path.Combine(engine, name), files[i].GetProperty("sha256").GetString()!);
        }
        progress.Report("Downloading optional Java runtime…");
        var java = Asset("cloudstream-java.json");
        var zip = Path.Combine(home, "java.zip");
        await Fetch(java.GetProperty("link").GetString()!, zip, java.GetProperty("checksum").GetString()!);
        progress.Report("Preparing Java runtime…");
        // Extract only within a new staging folder; archive paths must remain below it.
        await Task.Run(() =>
        {
            var staging = Path.Combine(home, "extract-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(staging);
            ZipFile.ExtractToDirectory(zip, staging);
            token.ThrowIfCancellationRequested();
            var extracted = Directory.GetDirectories(staging).Single();
            var target = Path.GetFullPath(Path.Combine(home, "java"));
            if (!target.StartsWith(Path.GetFullPath(home) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new IOException("Invalid runtime path.");
            if (Directory.Exists(target)) Directory.Delete(target, true);
            Directory.Move(extracted, target);
            Directory.Delete(staging);
            File.Delete(zip);
        }, token);
        await File.WriteAllTextAsync(Path.Combine(home, "SOURCE-AND-LICENSES.txt"),
            $"Optional external SkyStream cs3_engine, GPL-3.0. Source and license: https://github.com/akashdh11/skystream/tree/{Revision}/packages/cs3_engine\nTemurin Java: https://adoptium.net/; included legal notices are under java/legal.\nNo extensions are bundled.\n", token);
        progress.Report("Preparing separate audio track support…");
        var patch = Path.Combine(home, "audio-patch.jar");
        await using (var source = Assembly.GetExecutingAssembly().GetManifestResourceStream("BingeX.Assets.cloudstream-patch.jar")!)
        await using (var output = File.Create(patch)) await source.CopyToAsync(output, token);
        var start = new ProcessStartInfo(Java(data)) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true };
        foreach (var arg in new[] { "-cp", patch + Path.PathSeparator + Path.Combine(engine, "asm-9.10.1.jar"), "bingex.bridge.AudioPatch", Path.Combine(engine, "engine-core.jar") }) start.ArgumentList.Add(arg);
        using var process = Process.Start(start)!;
        var errors = process.StandardError.ReadToEndAsync(token);
        var outputText = process.StandardOutput.ReadToEndAsync(token);
        try { await process.WaitForExitAsync(token); }
        catch { if (!process.HasExited) process.Kill(true); throw; }
        await errors; await outputText;
        if (process.ExitCode != 0) throw new IOException("Could not prepare bridge audio support.");
        await File.WriteAllTextAsync(Path.Combine(home, "ready"), Revision + PatchVersion, token);
    }
}
