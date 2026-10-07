using Xunit;

namespace Tapster.Core.Tests;

public class TargetMarkerTests
{
    [Fact]
    public void TargetMarkerModel_Defaults_AreAccurate()
    {
        var model = new TargetMarkerModel();

        Assert.Equal(48, model.BaseDiameterPx);
        Assert.Equal(0, model.TargetX);
        Assert.Equal(0, model.TargetY);
        Assert.Equal(0, model.JitterRadiusPx);
        Assert.False(model.IsVisible);
    }

    [Theory]
    [InlineData(0, 48)]
    [InlineData(10, 48)] // 10*2 + 16 = 36 <= 48 -> 48
    [InlineData(20, 56)] // 20*2 + 16 = 56 > 48 -> 56
    [InlineData(50, 116)] // 50*2 + 16 = 116
    public void TargetMarkerModel_GetEffectiveDiameter_AccountsForJitter(double jitterRadius, int expectedDiameter)
    {
        var model = new TargetMarkerModel
        {
            JitterRadiusPx = jitterRadius
        };

        int actual = model.GetEffectiveDiameter();
        Assert.Equal(expectedDiameter, actual);
    }

    [Fact]
    public void TargetMarkerModel_CalculateWindowBounds_CentersProperly()
    {
        var model = new TargetMarkerModel
        {
            TargetX = 500,
            TargetY = 300,
            JitterRadiusPx = 0
        };

        var (x, y, w, h) = model.CalculateWindowBounds();

        Assert.Equal(476, x); // 500 - 24
        Assert.Equal(276, y); // 300 - 24
        Assert.Equal(48, w);
        Assert.Equal(48, h);
    }

    [Fact]
    public void TargetMarkerModel_CalculateWindowBounds_HandlesNegativeCoordinates()
    {
        var model = new TargetMarkerModel
        {
            TargetX = -200,
            TargetY = -150,
            JitterRadiusPx = 20 // diameter = 56, radius = 28
        };

        var (x, y, w, h) = model.CalculateWindowBounds();

        Assert.Equal(-228, x); // -200 - 28
        Assert.Equal(-178, y); // -150 - 28
        Assert.Equal(56, w);
        Assert.Equal(56, h);
    }

    [Fact]
    public void TargetMarkerModel_CalculateRelativeGeometry_ClampsCrosshair()
    {
        var model = new TargetMarkerModel
        {
            TargetX = 100,
            TargetY = 100,
            JitterRadiusPx = 30 // diameter = 76, center = 38
        };

        var (cx, cy, crosshairRadius, jitterRadius) = model.CalculateRelativeGeometry();

        Assert.Equal(38, cx);
        Assert.Equal(38, cy);
        Assert.True(crosshairRadius is >= TargetMarkerModel.MinCrosshairRadiusPx and <= TargetMarkerModel.MaxCrosshairRadiusPx);
        Assert.Equal(30, jitterRadius);
    }

    [Fact]
    public void TargetMarkerModel_IsInsideJitterRadius_IdentifiesPoints()
    {
        var model = new TargetMarkerModel
        {
            TargetX = 100,
            TargetY = 200,
            JitterRadiusPx = 10
        };

        Assert.True(model.IsInsideJitterRadius(100, 200));
        Assert.True(model.IsInsideJitterRadius(106, 208)); // dx=6, dy=8 -> 36+64=100 <= 100
        Assert.False(model.IsInsideJitterRadius(108, 208)); // dx=8, dy=8 -> 64+64=128 > 100

        model.JitterRadiusPx = 0;
        Assert.True(model.IsInsideJitterRadius(100, 200));
        Assert.False(model.IsInsideJitterRadius(101, 200));
    }

    [Fact]
    public void TargetMarkerOverlay_NativeWindowCreation_Diagnostics()
    {
        var hMod = System.Diagnostics.Process.GetCurrentProcess().Handle;
        var hModule = GetModuleHandleW(null);
        var wc = new WndClassEx
        {
            cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<WndClassEx>(),
            style = 0,
            lpfnWndProc = DefWindowProcW,
            cbClsExtra = 0,
            cbWndExtra = 0,
            hInstance = hModule,
            hIcon = IntPtr.Zero,
            hCursor = IntPtr.Zero,
            hbrBackground = IntPtr.Zero,
            lpszMenuName = null,
            lpszClassName = "TestOverlayClass_" + Guid.NewGuid().ToString("N"),
            hIconSm = IntPtr.Zero
        };

        ushort atom = RegisterClassExW(ref wc);
        int regError = System.Runtime.InteropServices.Marshal.GetLastWin32Error();

        Assert.True(atom != 0 || regError == 1410, $"RegisterClass failed: {regError}");

        int exStyle = 0x00000008 | 0x00000020 | 0x00000080 | 0x00080000 | 0x08000000;
        var hwnd = CreateWindowExW(
            exStyle,
            wc.lpszClassName,
            "Test",
            0x80000000,
            100, 100, 100, 100,
            IntPtr.Zero, IntPtr.Zero, hModule, IntPtr.Zero);
        int createError = System.Runtime.InteropServices.Marshal.GetLastWin32Error();

        Assert.True(hwnd != IntPtr.Zero, $"CreateWindow failed: {createError}");

        bool setLayered = SetLayeredWindowAttributes(hwnd, 0x00FF00FF, 255, 1);
        int setLayeredErr = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
        Assert.True(setLayered, $"SetLayeredWindowAttributes failed: {setLayeredErr}");

        // Test SetWindowPos positioning and Redraw
        bool swpResult = SetWindowPos(hwnd, new IntPtr(-1), 200, 200, 48, 48, 0x0010 | 0x0040); // SWP_NOACTIVATE | SWP_SHOWWINDOW
        Assert.True(swpResult, "SetWindowPos failed");

        bool invResult = InvalidateRect(hwnd, IntPtr.Zero, true);
        Assert.True(invResult, "InvalidateRect failed");

        DestroyWindow(hwnd);
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    internal struct WndClassEx
    {
        internal uint cbSize;
        internal uint style;
        internal WndProc lpfnWndProc;
        internal int cbClsExtra;
        internal int cbWndExtra;
        internal IntPtr hInstance;
        internal IntPtr hIcon;
        internal IntPtr hCursor;
        internal IntPtr hbrBackground;
        internal string? lpszMenuName;
        internal string lpszClassName;
        internal IntPtr hIconSm;
    }

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    internal delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    internal static extern IntPtr GetModuleHandleW(string? lpModuleName);

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    internal static extern ushort RegisterClassExW(ref WndClassEx lpwcx);

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    internal static extern IntPtr CreateWindowExW(
        int dwExStyle, string lpClassName, string lpWindowName, uint dwStyle,
        int X, int Y, int nWidth, int nHeight, IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    internal static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    internal static extern bool DestroyWindow(IntPtr hWnd);

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    internal static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    internal static extern bool InvalidateRect(IntPtr hWnd, IntPtr lpRect, bool bErase);

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    internal static extern IntPtr DefWindowProcW(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    internal static extern IntPtr GetDC(IntPtr hWnd);

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    internal static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [System.Runtime.InteropServices.DllImport("gdi32.dll", SetLastError = true)]
    internal static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [System.Runtime.InteropServices.DllImport("gdi32.dll", SetLastError = true)]
    internal static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int cx, int cy);

    [System.Runtime.InteropServices.DllImport("gdi32.dll", SetLastError = true)]
    internal static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

    [System.Runtime.InteropServices.DllImport("gdi32.dll", SetLastError = true)]
    internal static extern bool DeleteObject(IntPtr hObject);

    [System.Runtime.InteropServices.DllImport("gdi32.dll", SetLastError = true)]
    internal static extern bool DeleteDC(IntPtr hdc);

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    internal static extern bool UpdateLayeredWindow(
        IntPtr hwnd, IntPtr hdcDst, ref Point pptDst, ref Size psize,
        IntPtr hdcSrc, ref Point pptSrc, uint crKey, ref BlendFunction pblend, uint dwFlags);

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    internal struct Point { internal int X; internal int Y; }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    internal struct Size { internal int Cx; internal int Cy; }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    internal struct BlendFunction { internal byte BlendOp; internal byte BlendFlags; internal byte SourceConstantAlpha; internal byte AlphaFormat; }
}
