using BingeX;

internal static class PlaybackWaitChecks
{
    public static void Run()
    {
        var wait = new PlaybackWaitTracker();
        if (wait.Update(TimeSpan.Zero, 1000, true, false)) throw new Exception("Normal playback is not buffering.");
        if (wait.Update(TimeSpan.FromSeconds(1), 1000, true, false)) throw new Exception("Brief gaps must not flash an indicator.");
        if (!wait.Update(TimeSpan.FromSeconds(2), 1000, true, false)) throw new Exception("Frozen playback needs acknowledgement.");
        if (wait.Update(TimeSpan.FromSeconds(3), 1500, true, false)) throw new Exception("Progress must clear the indicator.");
        if (!wait.Update(TimeSpan.FromSeconds(4), 1500, false, true)) throw new Exception("Explicit native buffering must be visible.");
        if (wait.Update(TimeSpan.FromSeconds(5), 1500, false, false)) throw new Exception("User pause must clear buffering.");
        wait.Reset(TimeSpan.FromSeconds(6));
        if (wait.Update(TimeSpan.FromSeconds(7), 9000, true, false)) throw new Exception("A seek/new stream must reset stall timing.");
        Console.WriteLine("PASS: buffering acknowledgement, clock-stall grace, recovery, pause and seek reset.");
    }
}
