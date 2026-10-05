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
        var wc = new NativeMethods.WNDCLASSEXW
        {
            cbSize = (uint)Marshal.SizeOf<NativeMethods.WNDCLASSEXW>(),
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

        if (_hWnd != IntPtr.Zero)
        {
            NativeMethods.SetLayeredWindowAttributes(_hWnd, ColorKey, 235, NativeMethods.LWA_COLORKEY | NativeMethods.LWA_ALPHA);
        }
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
        NativeMethods.SetWindowPos(
            _hWnd,
            (IntPtr)NativeMethods.HWND_TOPMOST,
            bounds.X,
            bounds.Y,
            bounds.Width,
            bounds.Height,
            NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);

        RenderMarker(bounds.Width, bounds.Height);
    }

    private void RenderMarker(int width, int height)
    {
        IntPtr hDC = NativeMethods.GetDC(_hWnd);
        if (hDC == IntPtr.Zero) return;

        try
        {
            IntPtr memDC = NativeMethods.CreateCompatibleDC(hDC);
            if (memDC == IntPtr.Zero) return;

            try
            {
                IntPtr hBitmap = NativeMethods.CreateCompatibleBitmap(hDC, width, height);
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

                    NativeMethods.BitBlt(hDC, 0, 0, width, height, memDC, 0, 0, NativeMethods.SRCCOPY);
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
            NativeMethods.ReleaseDC(_hWnd, hDC);
        }
    }

    private static void DrawBackground(IntPtr memDC, int width, int height)
    {
        IntPtr hMagentaBrush = NativeMethods.CreateSolidBrush(ColorKey);
        try
        {
            var rc = new NativeMethods.RECT { Left = 0, Top = 0, Right = width, Bottom = height };
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
}
