namespace BingeX;

public static class CloudstreamRepositoryUrl
{
    public static string Normalize(string url)
    {
        url = url.Trim();
        const string prefix = "cloudstreamrepo://";
        if (url.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) url = "https://" + url[prefix.Length..];
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "https" ||
            uri.UserInfo.Length > 0 || uri.Host.Length == 0)
            throw new InvalidOperationException("Use an HTTPS or Cloudstream repository link.");
        return uri.AbsoluteUri;
    }
}
