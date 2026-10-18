using System.Net.Http;
using System.Text.Json;

namespace BingeX;

public sealed class AddonClient : IDisposable
{
    private readonly HttpClient http;
    public AddonClient(HttpMessageHandler? handler = null)
    {
        http = handler == null ? new HttpClient() : new HttpClient(handler);
        http.Timeout = TimeSpan.FromSeconds(20);
        http.MaxResponseContentBufferSize = 8 * 1024 * 1024;
    }
    public async Task<JsonElement> Get(string url, CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "https" || !string.IsNullOrEmpty(uri.UserInfo))
            throw new InvalidOperationException("Use an HTTPS add-on URL without embedded username/password credentials.");
        var json = await http.GetStringAsync(uri, cancellationToken);
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
    public static string Endpoint(string manifestUrl, string resource, string type, string id, IReadOnlyDictionary<string, string>? extra = null)
    {
        var builder = new UriBuilder(manifestUrl);
        builder.Path = builder.Path[..(builder.Path.LastIndexOf('/') + 1)] + resource + "/" + Uri.EscapeDataString(type) + "/" + Uri.EscapeDataString(id)
            + (extra?.Count > 0 ? "/" + string.Join("&", extra.Select(p => Uri.EscapeDataString(p.Key) + "=" + Uri.EscapeDataString(p.Value))) : "") + ".json";
        builder.Fragment = "";
        return builder.Uri.AbsoluteUri;
    }
    public static string Text(JsonElement item, string key, string fallback = "") =>
        item.ValueKind == JsonValueKind.Object && item.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? fallback : fallback;
    public void Dispose() => http.Dispose();
}
