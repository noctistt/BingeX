using System.Text.Json;
using BingeX;

internal static class StreamAudioChecks
{
    public static void Run()
    {
        var data = JsonSerializer.SerializeToElement(new[]
        {
            new { url = "https://media.example/a?xtags=acont%3Ddubbed%3Alang%3Dar", headers = new Dictionary<string,string>() },
            new { url = "https://media.example/e?xtags=acont%3Doriginal%3Alang%3Den", headers = new Dictionary<string,string>() },
            new { url = "https://media.example/e2?xtags=acont%3Doriginal%3Alang%3Den", headers = new Dictionary<string,string>() },
            new { url = "https://media.example/h?xtags=acont%3Ddubbed%3Alang%3Dhi", headers = new Dictionary<string,string>() },
            new { url = "file:///C:/private.wav", headers = new Dictionary<string,string>() },
            new { url = "https://media.example/private", headers = new Dictionary<string,string>{{"Authorization","private"}} }
        });
        var options = StreamAudioOption.Parse(data);
        if (options.Count != 3 || !options[0].Original || options[0].Language != "en" || !options[0].Label.Contains("English")) throw new Exception("Original audio must precede dubbed alternatives, with repeated encodings collapsed.");
        if (!options.Any(x => x.Language == "hi") || !options.Any(x => x.Language == "ar")) throw new Exception("Other audio languages must remain available.");
        var parsed = MediaLibrary.ParseStream("test", JsonSerializer.SerializeToElement(new { url = "https://media.example/video", audioUrl = "https://media.example/arabic", audioTracks = data }), true);
        if (parsed.AudioUrl != options[0].Url || parsed.AudioOptions?.Count != 3) throw new Exception("Stream mapping must preserve language options and prefer original audio.");
        Console.WriteLine("PASS: stream audio languages, original default, encoding deduplication, unsafe/unsupported audio filtering and stream mapping.");
    }
}
