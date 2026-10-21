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
        clock.Reset(); clock.Observe(1000,At(0),true,1);
        var last = clock.Position(At(700));
        foreach (var sample in new[] { (Time: 1000, Position: 990), (Time: 2000, Position: 1010), (Time: 3000, Position: 1020), (Time: 4000, Position: 1030) })
        {
            clock.Observe(sample.Position,At(sample.Time),true,1);
            var next = clock.Position(At(sample.Time+100));
            Require(next >= last, "Small native corrections and slow advancement must never redisplay an ended cue.");
            Require(next <= 1830, "Slow samples must not compound interpolation ahead of the video.");
            last = next;
        }
        var repeats = SubtitleDocument.Parse("1\n00:00:01,000 --> 00:00:02,000\nSame line\n\n2\n00:00:02,080 --> 00:00:03,000\nSame line\n\n3\n00:00:04,000 --> 00:00:05,000\nSame line\n\n4\n00:00:01,500 --> 00:00:02,500\nOther speaker\n\n");
        Require(repeats.Cues.Count==3 && repeats.TextAt(2040,0)=="Same line\nOther speaker", "Repeated segment captions join across tiny gaps without losing different overlapping dialogue.");
        Require(repeats.TextAt(3500,0)=="" && repeats.TextAt(4500,0)=="Same line", "Real pauses and later repeated dialogue stay intact.");
        Console.WriteLine("PASS: corrected/stalled subtitle timestamps and repeated segment cues without losing real pauses.");
        Console.WriteLine("PASS: subtitle clock repeated timestamps, cue-boundary stability, pause, buffering, seek, rate and bounded prediction.");
    }
}
