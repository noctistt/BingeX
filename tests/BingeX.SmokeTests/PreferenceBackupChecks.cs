using BingeX;
using System.Text.Json;
internal static class PreferenceBackupChecks
{
    public static void Run()
    {
        void Check(bool value,string message){if(!value)throw new Exception(message);}
        var preferences=new PlayerPreferences(Volume:65,SubtitleColor:"Mint",SeekSeconds:30,PreferredAudioLanguage:"english",PreferredSubtitleLanguage:"off",AccentColor:"Teal",CompactBrowsing:true,ReducedMotion:true,IncludeTorrentSources:true);
        var text=PreferenceBackup.Export(preferences);
        Check(PreferenceBackup.Import(text)==preferences,"A settings backup must round-trip every supported preference.");
        using(var json=JsonDocument.Parse(text))
        {
            var fields=json.RootElement.GetProperty("Preferences").EnumerateObject().Select(x=>x.Name).ToArray();
            Check(fields.Length==20 && !fields.Any(x=>x.Contains("Url") || x.Contains("Path") || x.Contains("Token") || x.Contains("History")),"Backups contain only the explicit preference allowlist.");
        }
        foreach(var invalid in new[]{"{}",text.Replace("BingeX.settings","Other.settings"),text.Replace("\"Version\": 1","\"Version\": 99"),text.Replace("\"Volume\": 65","\"Volume\": 999"),text.Replace("\"Volume\": 65","\"Volume\": \""+"loud"+"\""),text.Replace("\"Volume\": 65","\"SecretUrl\": \""+"private"+"\""),text.Replace("\"Volume\": 65","\"Volume\": 65, \""+"Volume"+"\": 10"),new string(' ',PreferenceBackup.MaxBytes+1)})
        {
            bool rejected=false;try{PreferenceBackup.Import(invalid);}catch(Exception ex)when(ex is InvalidDataException or JsonException or InvalidOperationException){rejected=true;}
            Check(rejected,"Malformed, unknown-version, duplicate, private-field, oversized and out-of-range imports must be rejected.");
        }
        Check(PreferenceBackup.Changes(new(),preferences).Contains("Volume") && PreferenceBackup.Changes(preferences,preferences)=="","Preview shows actual preference changes only.");
        Console.WriteLine("PASS: versioned preference backups, explicit allowlist, round-trip, preview and invalid/private-field import rejection.");
    }
}
