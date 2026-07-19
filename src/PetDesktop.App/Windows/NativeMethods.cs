using System.Runtime.InteropServices;

namespace PetDesktop.App.Windows;

internal static class NativeMethods
{
    internal const int WsPopup = unchecked((int)0x80000000);
    internal const int WsExTopmost = 0x00000008;
    internal const int WsExToolWindow = 0x00000080;
    internal const int WsExLayered = 0x00080000;
    internal const int WsExNoActivate = 0x08000000;

    internal const int WmDestroy = 0x0002;
    internal const int WmCancelMode = 0x001F;
    internal const int WmLeftButtonDown = 0x0201;
    internal const int WmLeftButtonUp = 0x0202;
    internal const int WmRightButtonUp = 0x0205;
    internal const int WmMouseMove = 0x0200;
    internal const int WmCaptureChanged = 0x0215;

    internal const int SwHide = 0;
    internal const int SwShowNoActivate = 4;

    internal const uint SwpNoSize = 0x0001;
    internal const uint SwpNoMove = 0x0002;
    internal const uint SwpNoZOrder = 0x0004;
    internal const uint SwpNoActivate = 0x0010;

    internal const uint UlwAlpha = 0x00000002;
    internal const byte AcSrcOver = 0x00;
    internal const byte AcSrcAlpha = 0x01;
    internal const uint BiRgb = 0;
    internal const uint DibRgbColors = 0;
    internal const int RgnOr = 2;
    internal const int RegionError = 0;

    internal static readonly nint DpiAwarenessContextPerMonitorAwareV2 = new(-4);
    internal static readonly nint HgdiError = new(-1);
    internal static readonly nint HwndTopmost = new(-1);
    internal static readonly nint HwndNotopmost = new(-2);

    [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool UpdateLayeredWindow(
        nint window,
        nint destinationDc,
        in Point destination,
        in Size size,
        nint sourceDc,
        in Point source,
        uint colorKey,
        in BlendFunction blend,
        uint flags);

    [DllImport("gdi32.dll", ExactSpelling = true)]
    internal static extern nint CreateRectRgn(int left, int top, int right, int bottom);

    // CombineRgn reports failure with RegionError. It does not define a useful GetLastError value.
    [DllImport("gdi32.dll", ExactSpelling = true)]
    internal static extern int CombineRgn(nint destination, nint source1, nint source2, int mode);

    [DllImport("user32.dll", ExactSpelling = true)]
    internal static extern int SetWindowRgn(
        nint window,
        nint region,
        [MarshalAs(UnmanagedType.Bool)] bool redraw);

    [DllImport("gdi32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool DeleteObject(nint graphicsObject);

    // SetCapture returns the previous capture owner, not a success flag. Verify with GetCapture.
    [DllImport("user32.dll", ExactSpelling = true)]
    internal static extern nint SetCapture(nint window);

    [DllImport("user32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ReleaseCapture();

    [DllImport("user32.dll", ExactSpelling = true)]
    internal static extern nint GetCapture();

    [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetCursorPos(out Point point);

    [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetLastInputInfo(ref LastInputInfo info);

    internal static TimeSpan GetInputIdleTime()
    {
        var info = new LastInputInfo { Size = (uint)Marshal.SizeOf<LastInputInfo>() };
        if (!GetLastInputInfo(ref info))
        {
            return TimeSpan.Zero;
        }

        var elapsedMilliseconds = unchecked((uint)Environment.TickCount - info.LastInputTick);
        return TimeSpan.FromMilliseconds(elapsedMilliseconds);
    }

    [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetWindowRect(nint window, out Rect rect);

    [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetWindowPos(
        nint window,
        nint insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);

    [DllImport("user32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ShowWindow(nint window, int command);

    [DllImport("user32.dll", ExactSpelling = true)]
    internal static extern nint GetDC(nint window);

    [DllImport("user32.dll", ExactSpelling = true)]
    internal static extern int ReleaseDC(nint window, nint deviceContext);

    [DllImport("gdi32.dll", ExactSpelling = true)]
    internal static extern nint CreateCompatibleDC(nint deviceContext);

    [DllImport("gdi32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool DeleteDC(nint deviceContext);

    [DllImport("gdi32.dll", ExactSpelling = true, SetLastError = true)]
    internal static extern nint CreateDIBSection(
        nint deviceContext,
        in BitmapInfo bitmapInfo,
        uint usage,
        out nint bits,
        nint section,
        uint offset);

    [DllImport("gdi32.dll", ExactSpelling = true)]
    internal static extern nint SelectObject(nint deviceContext, nint graphicsObject);

    [DllImport("user32.dll", ExactSpelling = true)]
    internal static extern nint SetThreadDpiAwarenessContext(nint dpiContext);

    // Raised only while a pet is actively being dragged. The matching end call
    // is required so the process returns to normal idle power usage.
    [DllImport("winmm.dll", ExactSpelling = true)]
    internal static extern uint timeBeginPeriod(uint milliseconds);

    [DllImport("winmm.dll", ExactSpelling = true)]
    internal static extern uint timeEndPeriod(uint milliseconds);

    [StructLayout(LayoutKind.Sequential)]
    internal readonly struct Point(int x, int y)
    {
        internal readonly int X = x;
        internal readonly int Y = y;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal readonly struct Size(int width, int height)
    {
        internal readonly int Width = width;
        internal readonly int Height = height;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Rect
    {
        internal int Left;
        internal int Top;
        internal int Right;
        internal int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LastInputInfo
    {
        internal uint Size;
        internal uint LastInputTick;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal readonly struct BlendFunction(
        byte operation,
        byte flags,
        byte sourceConstantAlpha,
        byte alphaFormat)
    {
        internal readonly byte Operation = operation;
        internal readonly byte Flags = flags;
        internal readonly byte SourceConstantAlpha = sourceConstantAlpha;
        internal readonly byte AlphaFormat = alphaFormat;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct BitmapInfoHeader
    {
        internal uint Size;
        internal int Width;
        internal int Height;
        internal ushort Planes;
        internal ushort BitCount;
        internal uint Compression;
        internal uint SizeImage;
        internal int XPelsPerMeter;
        internal int YPelsPerMeter;
        internal uint ColorsUsed;
        internal uint ColorsImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct BitmapInfo
    {
        internal BitmapInfoHeader Header;
        internal uint Colors;
    }
}
