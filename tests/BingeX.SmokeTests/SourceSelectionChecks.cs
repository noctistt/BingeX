using BingeX;
using System.Text.Json;

internal static class SourceSelectionChecks
{
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    public static void Run()
    {
        using var provider = JsonDocument.Parse("""{"infoHash":"abcdef0123456789abcdef0123456789abcdef01","name":"Torrentio 1080p","description":"👤 123 💾 2.5 GB","behaviorHints":{"videoSize":2500000000}}""");
        var parsed = MediaLibrary.ParseStream("Fixture", provider.RootElement);
        Check(parsed.Evidence == new SourceEvidence(2500000000, 123, 1080), "Standard file size and provider seed/quality hints must be extracted without inventing missing values.");
        using var unknown = JsonDocument.Parse("""{"url":"https://example.com/video"}""");
        Check(MediaLibrary.ParseStream("Fixture", unknown.RootElement).Evidence == new SourceEvidence(null, null, null), "Missing evidence must stay unknown.");
        foreach(var quality in new[]{360,480,720,1080,2160})
        {
            var source=MediaLibrary.ParseStream("Fixture",JsonSerializer.SerializeToElement(new{url="https://fixture/video",name=quality+"p"}));
            Check(source.Evidence?.Resolution==quality,"Resolution filters must recognize explicit SD, HD and 4K labels.");
        }
        var large = parsed with { Name = "Large 4K", Torrent = parsed.Torrent! with { InfoHash = new string('b', 40) }, Evidence = new(26L * 1024 * 1024 * 1024, 1, 2160) };
        var direct = new StreamChoice("Fixture", "Direct", "", "https://example.com/video", null);
        var health = new Dictionary<string, SourcePerformance>();
        var ranked = SourceSelection.Rank([large, parsed, direct, parsed], health);
        Check(ranked.Count == 3 && ranked[0] == direct && ranked[1] == parsed, "Ranking should favor supported direct links and manageable seeded videos, and deduplicate identical torrent files.");
        health[SourceSelection.Key(direct)] = new(true, 0);
        Check(SourceSelection.Rank(ranked, health)[0] == parsed, "An observed failure must lower the same source in this session.");
        var previous=new StreamChoice("Preferred","English Dubbed S01E01","","https://fixture/one",null,Evidence:new(null,null,1080));
        var nextEpisode=new EpisodeInfo("two","Second episode",1,2,"");
        var matching=previous with{Name="English Dubbed S01E02",Url="https://fixture/two"};
        var alternatives=new[]{matching with{Provider="Other"},matching with{Evidence=new(null,null,720)},matching with{Name="Hindi Dubbed S01E02"},matching with{Name="English Dubbed S01E01"},matching with{Url=previous.Url},matching};
        Check(NextEpisodeSourceSelection.Choose(alternatives,previous,nextEpisode,"english")==matching,"Next episode must wait for the same provider, resolution and language, rejecting wrong episodes and reused media.");
        Check(NextEpisodeSourceSelection.Choose(alternatives[..^1],previous,nextEpisode,"english")==null,"No matching next episode must require manual selection instead of switching provider or language.");
        Check(!NextEpisodeSourceSelection.Matches(matching with{Name="Unlabelled S01E02"},previous,nextEpisode,"english"),"Unknown audio must not silently replace a known preferred language.");
        Check(!NextEpisodeSourceSelection.Matches(matching,previous,nextEpisode with{NumberingKnown=false},"english"),"Unverified numbering must not be treated as proof of an episode match.");
        var torrentPrevious=previous with{Torrent=new(new string('a',40),0,[],[])};
        Check(NextEpisodeSourceSelection.Matches(matching with{Torrent=torrentPrevious.Torrent! with{FileIndex=1}},torrentPrevious,nextEpisode,"english"),"Distinct episode files within one torrent pack remain eligible.");
        var plan = new SourceAttemptPlan(ranked);
        plan.Mark(direct); Check(plan.Next == parsed, "A failed source must not be selected again.");
        plan.Mark(parsed); Check(plan.Next == large, "Fallback must advance to another distinct supported source.");
        plan.Mark(large); Check(plan.Next == null, "Automatic attempts must be capped at three.");
        var disabled = new SourceAttemptPlan([direct with { UnsupportedReason = "Needs headers" }]);
        Check(disabled.Next == null, "Unsupported sources must never become automatic alternatives.");
        Check(TorrentBufferPolicy.ContiguousBytes(100, 1000, 50, 256, i => i < 2) == 362, "Buffer accounting must handle files that share boundary pieces.");
        Check(TorrentBufferPolicy.ContiguousBytes(0, 1000, 0, 256, i => i != 1) == 256, "Downloaded pieces after a hole must not count as playable data ahead.");
        Check(TorrentBufferPolicy.ContiguousBytes(0, 1000, 950, 256, _ => true) == 50, "Buffer accounting must stop at the selected file boundary.");
        Check(TorrentBufferPolicy.EstimateSeconds(100, 1000, 60000) == 6 && TorrentBufferPolicy.EstimateSeconds(100, 1000, 0) == null, "Time estimates require an actual duration; bytes must not be presented as invented seconds.");
        Check(TorrentBufferPolicy.StartupTarget(16L * 1024 * 1024 * 1024, 7200) > TorrentBufferPolicy.StartupBytes, "Known high-bitrate videos should receive a larger startup target.");
        Check(TorrentBufferPolicy.StartupTarget(1000, 60) == 1000 && TorrentBufferPolicy.StartupTarget(32L * 1024 * 1024 * 1024, 60) == 32L * 1024 * 1024, "Adaptive startup targets must stay bounded and never exceed the selected file.");
        using var runtime = JsonDocument.Parse("""{"runtime":"145 min"}""");
        Check(MediaLibrary.ParseTitle(runtime.RootElement, "fixture", "movie").DurationSeconds == 8700, "Catalog runtime must be converted into seconds for buffer estimation.");
        var rate = new DeliveryRate();
        Check(rate.Add(0, 0) == 0 && rate.Add(2, 2000) == 1000 && rate.Add(4, 2000) == 500, "Measured delivery rate must reflect bytes over time, including slowdowns.");
        Console.WriteLine("PASS: source evidence, ranking, session failures, deduplication, bounded fallback, contiguous buffering, duration estimates and measured throughput.");
    }
}
