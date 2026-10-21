using System.Runtime.InteropServices;
using Avalonia.Platform;
using LibVLCSharp.Avalonia;

namespace BingeX;

public sealed class VideoSurfaceView : VideoView
{
    protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
    {
        var handle = base.CreateNativeControlCore(parent);
        if (OperatingSystem.IsWindows())
        {
            // Avalonia's default HWND has no background brush. Before VLC
            // paints a frame, it can retain pixels from the preceding page;
            // the transparent controls then appear over that stale picture.
            // Each default child has its own registered window class.
            var black = GetStockObject(4); // BLACK_BRUSH, owned by Windows.
            if (IntPtr.Size == 8) SetClassLongPtr(handle.Handle, -10, black); // GCLP_HBRBACKGROUND
            else SetClassLong(handle.Handle, -10, black.ToInt32());
            InvalidateRect(handle.Handle, IntPtr.Zero, true);
        }
        return handle;
    }

    [DllImport("gdi32.dll")]
    private static extern IntPtr GetStockObject(int index);
    [DllImport("user32.dll", EntryPoint = "SetClassLongPtrW")]
    private static extern IntPtr SetClassLongPtr(IntPtr window, int index, IntPtr value);
    [DllImport("user32.dll", EntryPoint = "SetClassLongW")]
    private static extern uint SetClassLong(IntPtr window, int index, int value);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool InvalidateRect(IntPtr window, IntPtr rect, [MarshalAs(UnmanagedType.Bool)] bool erase);
}
