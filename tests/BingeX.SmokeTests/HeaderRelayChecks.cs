using System.Net;
using System.Net.Sockets;
using BingeX;

internal static class HeaderRelayChecks
{
    public static async Task Run()
    {
        using var reservation = new TcpListener(IPAddress.Loopback, 0);
        reservation.Start(); var port = ((IPEndPoint)reservation.LocalEndpoint).Port; reservation.Stop();
        using var fixture = new HttpListener();
        fixture.Prefixes.Add($"http://127.0.0.1:{port}/"); fixture.Start();
        using var cancel = new CancellationTokenSource();
        using var otherPort = new TcpListener(IPAddress.Loopback, 0);
        otherPort.Start(); var redirectPort = ((IPEndPoint)otherPort.LocalEndpoint).Port; otherPort.Stop();
        using var other = new HttpListener(); other.Prefixes.Add($"http://127.0.0.1:{redirectPort}/"); other.Start();
        var redirected = Task.Run(async () =>
        {
            try
            {
                var c = await other.GetContextAsync().WaitAsync(cancel.Token);
                c.Response.StatusCode = c.Request.Headers["Cookie"] == null && c.Request.Headers["Authorization"] == null ? 200 : 403;
                c.Response.Close();
            }
            catch (Exception) when (cancel.IsCancellationRequested) { }
        });
        var served = Task.Run(async () =>
        {
            try
            {
                while (!cancel.IsCancellationRequested)
                {
                    var c = await fixture.GetContextAsync().WaitAsync(cancel.Token);
                    try
                    {
                        if (c.Request.Headers["Origin"] != "https://provider.example" || c.Request.Headers["Cookie"] != "session=fixture") { c.Response.StatusCode = 403; continue; }
                        var path = c.Request.Url!.AbsolutePath;
                        if (path == "/redirect") { c.Response.Redirect($"http://127.0.0.1:{redirectPort}/target"); continue; }
                        var body = path switch
                        {
                            "/master.m3u8" => "#EXTM3U\n#EXT-X-STREAM-INF:BANDWIDTH=1\nchild/list.m3u8\n",
                            "/child/list.m3u8" => "#EXTM3U\n#EXT-X-KEY:METHOD=AES-128,URI=\"../key\"\n#EXTINF:1,\n../segment\n#EXT-X-ENDLIST\n",
                            "/key" => "fixture-key", _ => "0123456789"
                        };
                        c.Response.ContentType = path.EndsWith("m3u8") ? "application/vnd.apple.mpegurl" : "application/octet-stream";
                        if (c.Request.Headers["Range"] == "bytes=3-5") { c.Response.StatusCode = 206; c.Response.Headers["Content-Range"] = "bytes 3-5/10"; body = "345"; }
                        var bytes = System.Text.Encoding.UTF8.GetBytes(body); c.Response.ContentLength64 = bytes.Length;
                        if (c.Request.HttpMethod != "HEAD") await c.Response.OutputStream.WriteAsync(bytes);
                    }
                    finally { c.Response.Close(); }
                }
            }
            catch (Exception) when (cancel.IsCancellationRequested) { }
        });
        try
        {
            await using var relay = new HeaderStreamRelay();
            using var client = new HttpClient();
            var url = relay.Register($"http://127.0.0.1:{port}/master.m3u8", new Dictionary<string,string> { ["Origin"] = "https://provider.example", ["Cookie"] = "session=fixture" });
            var master = await client.GetStringAsync(url);
            var child = master.Split('\n').First(x => x.StartsWith("http"));
            if (child.Contains($":{port}/")) throw new Exception("Nested playlist was not rewritten.");
            var playlist = await client.GetStringAsync(child);
            var key = System.Text.RegularExpressions.Regex.Match(playlist, "URI=\"([^\"]+)\"").Groups[1].Value;
            if (await client.GetStringAsync(key) != "fixture-key") throw new Exception("Key header forwarding failed.");
            var segment = playlist.Split('\n').First(x => x.StartsWith("http"));
            using var range = new HttpRequestMessage(HttpMethod.Get, segment);
            range.Headers.Range = new(3, 5);
            using var reply = await client.SendAsync(range);
            if (reply.StatusCode != HttpStatusCode.PartialContent || reply.Content.Headers.ContentRange?.From != 3 || await reply.Content.ReadAsStringAsync() != "345") throw new Exception("Byte-range seeking failed.");
            using var head = await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, segment));
            if (head.Content.Headers.ContentLength != 10 || (await head.Content.ReadAsByteArrayAsync()).Length != 0) throw new Exception("HEAD handling failed.");
            var wrong = url.Replace(new Uri(url).Segments[1], "wrong/");
            using var rejected = await client.GetAsync(wrong);
            if (rejected.StatusCode != HttpStatusCode.NotFound) throw new Exception("Unknown relay route accepted.");
            if (HeaderStreamRelay.ValidHeaders(new Dictionary<string,string>{{"Origin","bad\r\nInjected: yes"}})) throw new Exception("Header injection accepted.");
            var redirectUrl = relay.Register($"http://127.0.0.1:{port}/redirect", new Dictionary<string,string> { ["Origin"] = "https://provider.example", ["Cookie"] = "session=fixture", ["Authorization"] = "Bearer fixture" });
            using var redirectReply = await client.GetAsync(redirectUrl);
            if (redirectReply.StatusCode != HttpStatusCode.OK) throw new Exception("Cross-origin redirect leaked credentials or did not complete.");
            Console.WriteLine("PASS: relay HTTP, HLS rewriting and cross-origin credential scope.");
            Console.WriteLine("PASS: header relay, nested HLS, encryption-key headers, byte ranges, HEAD, private routes and injection rejection.");
        }
        finally { cancel.Cancel(); fixture.Close(); other.Close(); await served; await redirected; }
    }
}
