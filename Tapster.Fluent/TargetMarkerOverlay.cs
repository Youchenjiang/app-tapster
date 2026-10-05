using System;
using System.Runtime.InteropServices;
using Tapster;

namespace Tapster_Fluent;

/// <summary>
/// Manages a native Win32 layered, transparent, click-through overlay window
/// displaying the target crosshair marker and jitter spread boundary.
/// </summary>
public sealed class TargetMarkerOverlay : IDisposable
{
    private const string WindowClassName = "Tapster_TargetOverlayWindow";
    private readonly TargetMarkerModel _model = new();
    private readonly NativeMethods.WndProc _wndProc;
    private IntPtr _hWnd = IntPtr.Zero;
    private bool _isDisposed;

    // Colorkey: Magenta RGB(255, 0, 255)
    private const uint ColorKey = 0x00FF00FF;
    private const uint CrosshairColor = 0x003333FF; // Coral / Crimson (BGR)
    private const uint JitterBoundaryColor = 0x00FFDC00; // Cyan / Aqua (BGR)

    public TargetMarkerOverlay()
    {
        _wndProc = WndProcHandler;
        RegisterWindowClass();
        CreateOverlayWindow();
    }

    private void RegisterWindowClass()
    {
        var wc = new NativeMethods.WndClassEx
        {
            cbSize = (uint)Marshal.SizeOf<NativeMethods.WndClassEx>(),
            style = 0,
            lpfnWndProc = _wndProc,
            cbClsExtra = 0,
            cbWndExtra = 0,
            hInstance = IntPtr.Zero,
            hIcon = IntPtr.Zero,
            hCursor = IntPtr.Zero,
            hbrBackground = IntPtr.Zero,
            lpszMenuName = null,
            lpszClassName = WindowClassName,
            hIconSm = IntPtr.Zero
        };

        NativeMethods.RegisterClassExW(ref wc);
    }

    private void CreateOverlayWindow()
    {
        int exStyle = NativeMethods.WS_EX_TOPMOST |
                      NativeMethods.WS_EX_TRANSPARENT |
                      NativeMethods.WS_EX_TOOLWINDOW |
                      NativeMethods.WS_EX_LAYERED |
                      NativeMethods.WS_EX_NOACTIVATE;

        _hWnd = NativeMethods.CreateWindowExW(
            exStyle,
            WindowClassName,
            "Tapster Target Marker",
            NativeMethods.WS_POPUP,
            -1000, -1000, 10, 10,
            IntPtr.Zero,
            IntPtr.Zero,
            IntPtr.Zero,
            IntPtr.Zero);
    }

    private static IntPtr WndProcHandler(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == NativeMethods.WM_ERASEBKGND)
        {
            return (IntPtr)1;
        }

        return NativeMethods.DefWindowProcW(hWnd, msg, wParam, lParam);
    }

    public void Update(int targetX, int targetY, double jitterRadiusPx, bool isVisible)
    {
        if (_hWnd == IntPtr.Zero || _isDisposed) return;

        if (!isVisible)
        {
            NativeMethods.ShowWindow(_hWnd, NativeMethods.SW_HIDE);
            return;
        }

        _model.TargetX = targetX;
        _model.TargetY = targetY;
        _model.JitterRadiusPx = jitterRadiusPx;
        _model.IsVisible = true;

        var bounds = _model.CalculateWindowBounds();
        NativeMethods.ShowWindow(_hWnd, NativeMethods.SW_SHOWNOACTIVATE);
        RenderMarker(bounds.X, bounds.Y, bounds.Width, bounds.Height);
    }

    private void RenderMarker(int x, int y, int width, int height)
    {
        IntPtr screenDC = NativeMethods.GetDC(IntPtr.Zero);
        if (screenDC == IntPtr.Zero) return;

        try
        {
            IntPtr memDC = NativeMethods.CreateCompatibleDC(screenDC);
            if (memDC == IntPtr.Zero) return;

            try
            {
                IntPtr hBitmap = NativeMethods.CreateCompatibleBitmap(screenDC, width, height);
                if (hBitmap == IntPtr.Zero) return;

                try
                {
                    IntPtr oldBmp = NativeMethods.SelectObject(memDC, hBitmap);
                    DrawBackground(memDC, width, height);

                    var (cx, cy, crosshairRadius, jitterRadius) = _model.CalculateRelativeGeometry();
                    if (jitterRadius > 0)
                    {
                        DrawJitterBoundary(memDC, cx, cy, jitterRadius);
                    }

                    DrawCrosshair(memDC, cx, cy, crosshairRadius);

                    CommitLayeredWindow(screenDC, memDC, x, y, width, height);
                    NativeMethods.SelectObject(memDC, oldBmp);
                }
                finally
                {
                    NativeMethods.DeleteObject(hBitmap);
                }
            }
            finally
            {
                NativeMethods.DeleteDC(memDC);
            }
        }
        finally
        {
            NativeMethods.ReleaseDC(IntPtr.Zero, screenDC);
        }
    }

    private void CommitLayeredWindow(IntPtr screenDC, IntPtr memDC, int x, int y, int width, int height)
    {
        var ptDst = new NativeMethods.Point { X = x, Y = y };
        var size = new NativeMethods.Size { Cx = width, Cy = height };
        var ptSrc = new NativeMethods.Point { X = 0, Y = 0 };
        var blend = new NativeMethods.BlendFunction
        {
            BlendOp = 0, // AC_SRC_OVER
            BlendFlags = 0,
            SourceConstantAlpha = 240,
            AlphaFormat = 0
        };

        NativeMethods.UpdateLayeredWindow(
            _hWnd,
            screenDC,
            ref ptDst,
            ref size,
            memDC,
            ref ptSrc,
            ColorKey,
            ref blend,
            NativeMethods.ULW_COLORKEY | NativeMethods.ULW_ALPHA);
    }

    private static void DrawBackground(IntPtr memDC, int width, int height)
    {
        IntPtr hMagentaBrush = NativeMethods.CreateSolidBrush(ColorKey);
        try
        {
            var rc = new NativeMethods.Rect { Left = 0, Top = 0, Right = width, Bottom = height };
            NativeMethods.FillRect(memDC, ref rc, hMagentaBrush);
        }
        finally
        {
            NativeMethods.DeleteObject(hMagentaBrush);
        }
    }

    private static void DrawJitterBoundary(IntPtr memDC, int cx, int cy, int jitterRadius)
    {
        IntPtr hJitterPen = NativeMethods.CreatePen(NativeMethods.PS_SOLID, 1, JitterBoundaryColor);
        IntPtr hNullBrush = NativeMethods.CreateSolidBrush(ColorKey);
        try
        {
            IntPtr oldPen = NativeMethods.SelectObject(memDC, hJitterPen);
            IntPtr oldBrush = NativeMethods.SelectObject(memDC, hNullBrush);

            NativeMethods.Ellipse(memDC, cx - jitterRadius, cy - jitterRadius, cx + jitterRadius, cy + jitterRadius);

            NativeMethods.SelectObject(memDC, oldBrush);
            NativeMethods.SelectObject(memDC, oldPen);
        }
        finally
        {
            NativeMethods.DeleteObject(hNullBrush);
            NativeMethods.DeleteObject(hJitterPen);
        }
    }

    private static void DrawCrosshair(IntPtr memDC, int cx, int cy, int crosshairRadius)
    {
        IntPtr hCrosshairPen = NativeMethods.CreatePen(NativeMethods.PS_SOLID, 2, CrosshairColor);
        IntPtr hNullBrush = NativeMethods.CreateSolidBrush(ColorKey);
        IntPtr hDotBrush = NativeMethods.CreateSolidBrush(CrosshairColor);
        try
        {
            IntPtr oldPen = NativeMethods.SelectObject(memDC, hCrosshairPen);
            IntPtr oldBrush = NativeMethods.SelectObject(memDC, hNullBrush);

            // Ring
            NativeMethods.Ellipse(memDC, cx - crosshairRadius, cy - crosshairRadius, cx + crosshairRadius, cy + crosshairRadius);

            // Ticks
            int innerGap = 3;
            int outerTick = crosshairRadius + 4;
            NativeMethods.MoveToEx(memDC, cx, cy - innerGap, IntPtr.Zero);
            NativeMethods.LineTo(memDC, cx, cy - outerTick);
            NativeMethods.MoveToEx(memDC, cx, cy + innerGap, IntPtr.Zero);
            NativeMethods.LineTo(memDC, cx, cy + outerTick);
            NativeMethods.MoveToEx(memDC, cx - innerGap, cy, IntPtr.Zero);
            NativeMethods.LineTo(memDC, cx - outerTick, cy);
            NativeMethods.MoveToEx(memDC, cx + innerGap, cy, IntPtr.Zero);
            NativeMethods.LineTo(memDC, cx + outerTick, cy);

            // Center dot
            NativeMethods.SelectObject(memDC, hDotBrush);
            NativeMethods.Ellipse(memDC, cx - 2, cy - 2, cx + 3, cy + 3);

            NativeMethods.SelectObject(memDC, oldBrush);
            NativeMethods.SelectObject(memDC, oldPen);
        }
        finally
        {
            NativeMethods.DeleteObject(hDotBrush);
            NativeMethods.DeleteObject(hNullBrush);
            NativeMethods.DeleteObject(hCrosshairPen);
        }
    }

    public void Hide()
    {
        if (_hWnd != IntPtr.Zero && !_isDisposed)
        {
            NativeMethods.ShowWindow(_hWnd, NativeMethods.SW_HIDE);
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        if (_hWnd != IntPtr.Zero)
        {
            NativeMethods.DestroyWindow(_hWnd);
            _hWnd = IntPtr.Zero;
        }
    }

    private static class NativeMethods
    {
        internal const int WS_EX_TOPMOST = 0x00000008;
        internal const int WS_EX_TRANSPARENT = 0x00000020;
        internal const int WS_EX_TOOLWINDOW = 0x00000080;
        internal const int WS_EX_LAYERED = 0x00080000;
        internal const int WS_EX_NOACTIVATE = 0x08000000;

        internal const uint WS_POPUP = 0x80000000;
        internal const int SW_HIDE = 0;

        internal const uint ULW_COLORKEY = 0x00000001;
        internal const uint ULW_ALPHA = 0x00000002;
        internal const int SW_SHOWNOACTIVATE = 4;

        internal const int PS_SOLID = 0;
        internal const uint WM_ERASEBKGND = 0x0014;

        [StructLayout(LayoutKind.Sequential)]
        internal struct Point
        {
            internal int X;
            internal int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct Size
        {
            internal int Cx;
            internal int Cy;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct BlendFunction
        {
            internal byte BlendOp;
            internal byte BlendFlags;
            internal byte SourceConstantAlpha;
            internal byte AlphaFormat;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
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

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        internal delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern ushort RegisterClassExW(ref WndClassEx lpwcx);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern IntPtr CreateWindowExW(
            int dwExStyle,
            string lpClassName,
            string lpWindowName,
            uint dwStyle,
            int X,
            int Y,
            int nWidth,
            int nHeight,
            IntPtr hWndParent,
            IntPtr hMenu,
            IntPtr hInstance,
            IntPtr lpParam);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool DestroyWindow(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool UpdateLayeredWindow(
            IntPtr hwnd,
            IntPtr hdcDst,
            ref Point pptDst,
            ref Size psize,
            IntPtr hdcSrc,
            ref Point pptSrc,
            uint crKey,
            ref BlendFunction pblend,
            uint dwFlags);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern IntPtr DefWindowProcW(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll", SetLastError = true)]
        internal static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [DllImport("gdi32.dll", SetLastError = true)]
        internal static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int cx, int cy);

        [DllImport("gdi32.dll", SetLastError = true)]
        internal static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

        [DllImport("gdi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool DeleteObject(IntPtr hObject);

        [DllImport("gdi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll", SetLastError = true)]
        internal static extern IntPtr CreatePen(int fnPenStyle, int nWidth, uint crColor);

        [DllImport("gdi32.dll", SetLastError = true)]
        internal static extern IntPtr CreateSolidBrush(uint crColor);

        [DllImport("gdi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool Ellipse(IntPtr hdc, int left, int top, int right, int bottom);

        [DllImport("gdi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool MoveToEx(IntPtr hdc, int x, int y, IntPtr lpPoint);

        [DllImport("gdi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool LineTo(IntPtr hdc, int x, int y);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern int FillRect(IntPtr hDC, [In] ref Rect lprc, IntPtr hbr);

        [StructLayout(LayoutKind.Sequential)]
        internal struct Rect
        {
            internal int Left;
            internal int Top;
            internal int Right;
            internal int Bottom;
        }
    }
}
