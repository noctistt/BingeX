using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace BingeX;

public sealed class OnlineSubtitles : IDisposable
{
    public static readonly InstalledAddon DefaultProvider = new("org.stremio.opensubtitlesv3", "OpenSubtitles v3", "https://opensubtitles-v3.strem.io/manifest.json");
    public static readonly InstalledAddon DefaultCatalog = new("com.linvo.cinemeta", "Cinemeta", "https://v3-cinemeta.strem.io/manifest.json");
    private readonly HttpClient http;
    private const int MaximumBytes = 8 * 1024 * 1024;

    public OnlineSubtitles(HttpMessageHandler? handler = null)
    {
        http = new(handler ?? new HttpClientHandler { AllowAutoRedirect = false, AutomaticDecompression = DecompressionMethods.All });
        http.Timeout = TimeSpan.FromSeconds(25);
    }
    public static bool IsDownloadUrl(string url) => Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && uri.Scheme == "https" && string.IsNullOrEmpty(uri.UserInfo);

    // Parse in memory: subtitle results never become executable files or native-player URLs.
    public async Task<SubtitleDocument> Download(string url, CancellationToken token)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromSeconds(25));
        token = deadline.Token;
        for (var hop = 0; hop < 6; hop++)
        {
            if (!IsDownloadUrl(url)) throw new InvalidOperationException("This subtitle needs an HTTPS download link.");
            using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token);
            if ((int)response.StatusCode is 301 or 302 or 303 or 307 or 308)
            {
                var location = response.Headers.Location ?? throw new HttpRequestException("Missing redirect location.");
                url = new Uri(new Uri(url), location).AbsoluteUri;
                continue;
            }
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength > MaximumBytes) throw new FormatException("This subtitle file is too large.");
            await using var source = await response.Content.ReadAsStreamAsync(token);
            using var buffer = new MemoryStream();
            var block = new byte[16384];
            int count;
            while ((count = await source.ReadAsync(block, token)) != 0)
            {
                if (buffer.Length + count > MaximumBytes) throw new FormatException("This subtitle file is too large.");
                await buffer.WriteAsync(block.AsMemory(0, count), token);
            }
            buffer.Position = 0;
            var encoding = new UTF8Encoding(false, true);
            var charset = response.Content.Headers.ContentType?.CharSet?.Trim('"');
            Encoding selected = string.IsNullOrWhiteSpace(charset) ? encoding : Encoding.GetEncoding(charset, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
            using var reader = new StreamReader(buffer, selected, detectEncodingFromByteOrderMarks: true);
            var contents = await reader.ReadToEndAsync(token);
            return await Task.Run(() => SubtitleDocument.Parse(contents), token);
        }
        throw new HttpRequestException("Too many subtitle redirects.");
    }

    public static string LanguageName(string code)
    {
        var alias = code.ToLowerInvariant() switch
        {
            "ger" => "deu", "fre" => "fra", "chi" => "zho", "dut" => "nld", "cze" => "ces",
            "gre" => "ell", "rum" => "ron", "per" => "fas", "slo" => "slk", _ => code.ToLowerInvariant()
        };
        return CultureInfo.GetCultures(CultureTypes.NeutralCultures).FirstOrDefault(x =>
            x.TwoLetterISOLanguageName == alias || x.ThreeLetterISOLanguageName == alias)?.EnglishName ?? code;
    }
    public static string GuessTitle(string filename)
    {
        var title = Path.GetFileNameWithoutExtension(filename);
        title = Regex.Replace(title, @"[._]", " ");
        title = Regex.Replace(title, @"(?i)\b(?:S\d{1,2}E\d{1,3}|\d{3,4}p|2160|WEB[- ]?DL|WEBRip|BluRay|BRRip|HDRip|DVDRip|x26[45]|HEVC)\b.*$", "");
        return title.Trim(' ', '-');
    }
    public void Dispose() => http.Dispose();
}
