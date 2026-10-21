using BingeX;

internal static class SubtitleClockChecks
{
    public static void Run()
    {
        void Require(bool value, string message) { if (!value) throw new Exception(message); }
        var clock = new SubtitlePlaybackClock();
        TimeSpan At(int ms) => TimeSpan.FromMilliseconds(ms);
        var captions = SubtitleDocument.Parse("1\n00:00:01,200 --> 00:00:01,650\nLine\n\n");
        clock.Observe(1000,At(0),true,1);
        Require(captions.TextAt(clock.Position(At(300)),0)=="Line", "Caption appears between native polls.");
        clock.Observe(1000,At(500),true,1);
        Require(captions.TextAt(clock.Position(At(500)),0)=="Line", "Repeated timestamps must not rewind and hide a visible line.");
        Require(captions.TextAt(clock.Position(At(700)),0)=="", "Caption ends on the interpolated clock.");
        clock.Observe(1500,At(750),true,1);
        Require(captions.TextAt(clock.Position(At(750)),0)=="", "A late native update must not resurrect an ended line.");
        clock.Observe(1900,At(900),false,1);
        Require(clock.Position(At(2000))==1900, "Pause freezes the clock.");
        clock.Observe(1900,At(2100),true,2);
        Require(clock.Position(At(2300))==2300, "Caption interpolation follows playback speed.");
        clock.Observe(2400,At(2400),false,2);
        Require(clock.Position(At(3400))==2400, "Buffering freezes caption advancement.");
        clock.Reset(); clock.Observe(500,At(3500),true,.5);
        Require(clock.Position(At(3700))==600, "Backward seek resets monotonic tracking and supports half speed.");
        Require(clock.Position(At(9000))==875, "A stalled native clock has bounded extrapolation.");
        Console.WriteLine("PASS: subtitle clock repeated timestamps, cue-boundary stability, pause, buffering, seek, rate and bounded prediction.");
    }
}
