using BingeX;

internal static class NativePlaybackQueueChecks
{
    public static async Task Run()
    {
        var queue = new NativePlaybackQueue();
        using var release = new ManualResetEventSlim();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var order = new List<string>();
        var stopping = queue.Enqueue(() => { order.Add("stop"); started.SetResult(); release.Wait(TimeSpan.FromSeconds(5)); });
        await started.Task.WaitAsync(TimeSpan.FromSeconds(3));
        var playing = queue.Enqueue(() => order.Add("play"));
        if (!queue.IsBusy || playing.IsCompleted) throw new Exception("New playback must wait for native stop without blocking the caller.");
        release.Set();
        await Task.WhenAll(stopping, playing);
        if (queue.IsBusy || !order.SequenceEqual(["stop", "play"])) throw new Exception("Native lifecycle operations must run in order and clear their busy state.");
        var failure = queue.Enqueue(() => throw new InvalidOperationException("fixture"));
        try { await failure; } catch (InvalidOperationException) { }
        var recovered = false;
        await queue.Enqueue(() => recovered = true);
        if (!recovered || queue.IsBusy) throw new Exception("A failed native operation must not prevent cleanup or later playback.");
        Console.WriteLine("PASS: non-blocking serialized native stop/play, ordering, busy-state cleanup and recovery after failure.");
    }
}
