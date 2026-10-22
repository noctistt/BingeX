using System.Net;
using System.Text.Json;

namespace BingeX;

public record AddonCheck(string ManifestUrl, string Message, bool Failed, DateTime CheckedAt);

public static class AddonHealth
{
    public static async Task<AddonCheck> CheckManifest(AddonClient client, InstalledAddon addon, CancellationToken token)
    {
        try
        {
            var manifest = await client.Get(addon.ManifestUrl, token);
            var valid = AddonClient.Text(manifest, "id").Length > 0 && AddonClient.Text(manifest, "name").Length > 0 &&
                manifest.TryGetProperty("resources", out var resources) && resources.ValueKind == JsonValueKind.Array;
            var needsSetup = manifest.TryGetProperty("behaviorHints", out var hints) && hints.ValueKind == JsonValueKind.Object &&
                hints.TryGetProperty("configurationRequired", out var required) && required.ValueKind == JsonValueKind.True;
            return new(addon.ManifestUrl, !valid ? "Invalid add-on manifest." : needsSetup ? "Responded; configuration is required." : "Manifest responded. Playback has not been tested.", !valid || needsSetup, DateTime.UtcNow);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception error) { return new(addon.ManifestUrl, Failure(error), true, DateTime.UtcNow); }
    }
    public static string Failure(Exception error) => error switch
    {
        HttpRequestException { StatusCode: HttpStatusCode.TooManyRequests } => "Request limit reached. Wait before retrying.",
        HttpRequestException { StatusCode: HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden } => "Access denied. Check the add-on's login or configuration.",
        HttpRequestException { StatusCode: HttpStatusCode.NotFound } => "Endpoint not found. Check the installed configuration link.",
        OperationCanceledException => "Request timed out.",
        JsonException => "The provider returned an invalid response.",
        HttpRequestException => "Connection failed. The provider or your network may be unavailable.",
        _ => "The provider could not be checked."
    };
    // Use the general directory without transmitting any configured URL or provider identity.
    public static Uri CommunityPage(InstalledAddon addon) => new("https://stremio-addons.net/addons");
}
