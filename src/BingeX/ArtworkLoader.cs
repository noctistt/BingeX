using Avalonia.Controls;
using Avalonia.Media.Imaging;
using System.Net.Http;

namespace BingeX;

public sealed class ArtworkLoader : IDisposable
{
    private readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(12), MaxResponseContentBufferSize = 6 * 1024 * 1024 };
    private readonly SemaphoreSlim slots = new(4);
    private readonly Dictionary<string, Bitmap> cache = new();
    private readonly CancellationTokenSource lifetime = new();

    public async Task Fill(Image image, string location, CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(location)) return;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, lifetime.Token);
        try
        {
            if (cache.TryGetValue(location, out var existing)) { image.Source = existing; return; }
            await slots.WaitAsync(linked.Token);
            try
            {
                if (cache.TryGetValue(location, out existing)) { image.Source = existing; return; }
                Bitmap bitmap;
                if (File.Exists(location))
                {
                    using var file = File.OpenRead(location);
                    bitmap = Bitmap.DecodeToWidth(file, 480);
                }
                else
                {
                    if (!Uri.TryCreate(location, UriKind.Absolute, out var uri) || uri.Scheme != "https") return;
                    var bytes = await http.GetByteArrayAsync(uri, linked.Token);
                    linked.Token.ThrowIfCancellationRequested();
                    using var stream = new MemoryStream(bytes);
                    bitmap = Bitmap.DecodeToWidth(stream, 480);
                }
                if (linked.IsCancellationRequested) { bitmap.Dispose(); return; }
                // Images keep their bitmap alive. Eviction drops only the cache reference.
                if (cache.Count >= 150) cache.Remove(cache.Keys.First());
                cache[location] = bitmap;
                image.Source = bitmap;
            }
            finally { slots.Release(); }
        }
        catch (Exception) { /* Missing artwork keeps the card's intentional fallback. */ }
    }
    public void Dispose()
    {
        lifetime.Cancel();
        http.Dispose();
        cache.Clear();
    }
}
