using Avalonia.Threading;

namespace BingeX;

public partial class MainWindow
{
    private void SynchronizePlayerOverlay()
    {
        if (!VideoSurface.IsEffectivelyVisible || VideoSurface.Bounds.Width <= 0 || VideoSurface.Bounds.Height <= 0) return;
        // VideoView hosts this content in another window. Explicit dimensions
        // keep its first measure, clipping and later mini/full resizes identical
        // to the native video surface, including on scaled displays.
        PlayerOverlay.Width = VideoSurface.Bounds.Width;
        PlayerOverlay.Height = VideoSurface.Bounds.Height;
    }

    private async Task<bool> WaitForPlayerLayoutAsync(int request)
    {
        // A nonzero HWND only proves creation: hidden hosts can still have their
        // default native size. Let both the owner and overlay complete layout
        // and rendering before asking VLC to create its video output.
        for (var frame = 0; frame < 2; frame++)
        {
            if (localClosing || request != playbackRequest) return false;
            UpdateLayout();
            SynchronizePlayerOverlay();
            RootLayout.InvalidateVisual();
            var rendered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            RequestAnimationFrame(_ => rendered.TrySetResult());
            if (await Task.WhenAny(rendered.Task, Task.Delay(TimeSpan.FromSeconds(3))) != rendered.Task) return false;
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        }
        return !localClosing && request == playbackRequest && (activePage == "Player" || miniPlayer) &&
            VideoSurface.IsEffectivelyVisible && VideoSurface.IsArrangeValid &&
            VideoSurface.Bounds.Width > 0 && VideoSurface.Bounds.Height > 0;
    }
}
