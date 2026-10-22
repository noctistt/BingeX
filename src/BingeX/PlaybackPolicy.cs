namespace BingeX;

public static class PlaybackPolicy
{
    public static bool IsIncomplete(long position, long duration) => position > 0 &&
        (duration <= 0 || position < duration - Math.Min(10000, Math.Max(1, duration / 20)));

    public static long ResumeTarget(long savedPosition, long duration) =>
        duration > 0 && IsIncomplete(savedPosition, duration) ? Math.Clamp(savedPosition, 0, duration) : 0;

    public static long SourceSwitchPosition(string? previousKey,string? nextKey,long position,long pending,bool ended)=>
        nextKey!=null && previousKey==nextKey && !ended ? Math.Max(0,Math.Max(position,pending)) : 0;

    public static long SeekTarget(long time, long delta, long duration) =>
        Math.Clamp(time + delta, 0, Math.Max(0, duration));
}

public record PlayerPreferences(int Volume = 80, int SubtitleSize = 28, string SubtitleColor = "White", bool SubtitleBackground = true, bool AutoplayEpisodes = false, bool SearchAllStreamProviders = false, bool IncludeTorrentSources = false, int LocalLibrarySort = 0, int LocalLibraryGrouping = 0, int SeekSeconds = 10, bool ResumePlayback = true, bool ClickToPause = true, bool StartupAnimation = true, int DefaultSpeedIndex = 2, bool AutoSwitchSources = true, string PreferredAudioLanguage = "", string PreferredSubtitleLanguage = "");
