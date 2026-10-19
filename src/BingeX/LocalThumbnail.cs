using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using LibVLCSharp.Shared;
using System.Runtime.InteropServices;

namespace BingeX;

public static class LocalThumbnail
{
    // One small software-decoded frame; never attach this player to a window or audio output.
    public static bool Create(string path, string destination, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        Core.Initialize();
        using var engine = new LibVLC("--no-audio", "--no-spu", "--no-video-title-show", "--avcodec-hw=none");
        using var media = new Media(engine, new Uri(path));
        media.Parse(MediaParseOptions.ParseLocal, 2000, token).GetAwaiter().GetResult();
        var video = media.Tracks.FirstOrDefault(x => x.TrackType == TrackType.Video).Data.Video;
        var aspect = video.Width > 0 && video.Height > 0 ? (double)video.Width / video.Height : 16d / 9;
        if (video.SarNum > 0 && video.SarDen > 0) aspect *= (double)video.SarNum / video.SarDen;
        var width = Math.Clamp((int)Math.Round(Math.Min(480d, 480 * aspect)), 1, 480);
        var height = Math.Clamp((int)Math.Round(width / aspect), 1, 480);
        var pitch = (width * 4 + 31) & ~31;
        var allocation = Marshal.AllocHGlobal(pitch * height + 31);
        var pixels = new IntPtr((allocation.ToInt64() + 31) & ~31L);
        var gate = new object();
        byte[]? frame = null;
        using var ready = new ManualResetEventSlim();
        try
        {
            media.AddOption(":start-time=2");
            using var decoder = new MediaPlayer(engine);
            decoder.SetVideoFormat("RV32", (uint)width, (uint)height, (uint)pitch);
            decoder.SetVideoCallbacks((_, planes) => { Monitor.Enter(gate); Marshal.WriteIntPtr(planes, pixels); return IntPtr.Zero; },
                (_, _, _) => Monitor.Exit(gate),
                (_, _) =>
                {
                    lock (gate)
                    {
                        if (frame != null) return;
                        frame = new byte[pitch * height];
                        Marshal.Copy(pixels, frame, 0, frame.Length);
                        ready.Set();
                    }
                });
            try
            {
                if (!decoder.Play(media) || !ready.Wait(TimeSpan.FromSeconds(7), token)) return false;
            }
            finally { decoder.Stop(); }
            token.ThrowIfCancellationRequested();
            lock (gate)
            {
                if (frame == null) return false;
                Marshal.Copy(frame, 0, pixels, frame.Length);
                using var bitmap = new Bitmap(PixelFormat.Bgra8888, AlphaFormat.Opaque, pixels, new PixelSize(width, height), new Vector(96, 96), pitch);
                var temporary = destination + ".tmp";
                try { bitmap.Save(temporary); token.ThrowIfCancellationRequested(); File.Move(temporary, destination, true); }
                finally { if (File.Exists(temporary)) File.Delete(temporary); }
            }
            return true;
        }
        finally { Marshal.FreeHGlobal(allocation); }
    }
}
