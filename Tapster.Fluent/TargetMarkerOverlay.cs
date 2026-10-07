using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Tapster;

namespace Tapster_Fluent;

/// <summary>
/// A marker point on screen with a sequence order index.
/// </summary>
public sealed record MarkerPoint(int Index, int X, int Y);

/// <summary>
/// Manages a native Win32 layered, transparent, click-through overlay window
/// displaying single target crosshairs, multi-point numbered badges, and dynamic click ripples.
/// Optimized for zero-stutter composition with pre-cached GDI memory buffers and asynchronous redraws.
/// </summary>
public sealed class TargetMarkerOverlay : IDisposable
{
    private const string WindowClassName = "Tapster_TargetOverlayWindow";
    private readonly TargetMarkerModel _model = new();
    private readonly NativeMethods.WndProc _wndProc;
    private IntPtr _hWnd = IntPtr.Zero;
    private bool _isDisposed;
    private bool _isWindowVisible;

    // Colorkey: Magenta RGB(255, 0, 255)
    private const uint ColorKey = 0x00FF00FF;
    private const uint CrosshairColor = 0x001010FF; // Vivid Bright Red (BGR: R=0xFF, G=0x10, B=0x10)
    private const uint CrosshairOutlineColor = 0x00FFFFFF; // Crisp White outline (BGR)
    private const uint JitterBoundaryColor = 0x00FFDC00; // Cyan / Aqua (BGR)
    private const uint BadgeFillColor = 0x002020E6; // High-contrast Crimson Red (BGR)
    private const uint RippleColor = 0x00FFDC00; // Cyan / Aqua (BGR)

    private readonly List<MarkerPoint> _markers = new();
    private readonly object _lock = new();
    private MarkerPoint? _ripplePoint;
    private bool _isSingleTargetVisible;
    private int _singleTargetX;
    private int _singleTargetY;
    private double _singleJitterRadius;

    private int _vx;
    private int _vy;
    private int _vWidth;
    private int _vHeight;

    // Cached GDI resources to eliminate allocation overhead during painting
    private IntPtr _memDC = IntPtr.Zero;
    private IntPtr _hBitmap = IntPtr.Zero;
    private IntPtr _oldBitmap = IntPtr.Zero;
    private int _memWidth;
    private int _memHeight;

    private IntPtr _hFont = IntPtr.Zero;
    private IntPtr _hMagentaBrush = IntPtr.Zero;
    private IntPtr _hOutlinePen = IntPtr.Zero;
    private IntPtr _hTickPen = IntPtr.Zero;
    private IntPtr _hBadgeBrush = IntPtr.Zero;
    private IntPtr _hRipplePen = IntPtr.Zero;

    private System.Threading.CancellationTokenSource? _rippleCts;

    public TargetMarkerOverlay()
    {
        _wndProc = WndProcHandler;
        RegisterWindowClass();
        CreateOverlayWindow();
    }

    private void RegisterWindowClass()
    {
        var hModule = NativeMethods.GetModuleHandleW(null);
        var wc = new NativeMethods.WndClassEx
        {
            cbSize = (uint)Marshal.SizeOf<NativeMethods.WndClassEx>(),
            style = 0,
            lpfnWndProc = _wndProc,
            cbClsExtra = 0,
            cbWndExtra = 0,
            hInstance = hModule,
            hIcon = IntPtr.Zero,
            hCursor = IntPtr.Zero,
            hbrBackground = IntPtr.Zero,
            lpszMenuName = null,
            lpszClassName = WindowClassName,
            hIconSm = IntPtr.Zero
        };

        ushort atom = NativeMethods.RegisterClassExW(ref wc);
        if (atom == 0)
        {
            int err = Marshal.GetLastWin32Error();
            if (err != 1410) // 1410 = ERROR_CLASS_ALREADY_EXISTS
            {
                Debug.WriteLine($"RegisterClassExW failed with error {err}");
            }
        }
    }

    private void CreateOverlayWindow()
    {
        int exStyle = NativeMethods.WS_EX_TOPMOST |
                      NativeMethods.WS_EX_TRANSPARENT |
                      NativeMethods.WS_EX_TOOLWINDOW |
                      NativeMethods.WS_EX_LAYERED |
                      NativeMethods.WS_EX_NOACTIVATE;

        var hModule = NativeMethods.GetModuleHandleW(null);
        _hWnd = NativeMethods.CreateWindowExW(
            exStyle,
            WindowClassName,
            "Tapster Target Marker",
            NativeMethods.WS_POPUP,
            -1000, -1000, 10, 10,
            IntPtr.Zero,
            IntPtr.Zero,
            hModule,
            IntPtr.Zero);

        if (_hWnd != IntPtr.Zero)
        {
            NativeMethods.SetLayeredWindowAttributes(
                _hWnd,
                ColorKey,
                255,
                NativeMethods.LWA_COLORKEY);

            // Pre-create standard drawing objects once
            _hFont = NativeMethods.CreateFontW(-13, 0, 0, 0, 700, 0, 0, 0, 1, 0, 0, 5, 0, "Segoe UI");
            _hMagentaBrush = NativeMethods.CreateSolidBrush(ColorKey);
            _hOutlinePen = NativeMethods.CreatePen(NativeMethods.PS_SOLID, 2, CrosshairOutlineColor);
            _hTickPen = NativeMethods.CreatePen(NativeMethods.PS_SOLID, 2, CrosshairOutlineColor);
            _hBadgeBrush = NativeMethods.CreateSolidBrush(BadgeFillColor);
            _hRipplePen = NativeMethods.CreatePen(NativeMethods.PS_SOLID, 3, RippleColor);
        }
    }

    private void EnsureWindowPosition()
    {
        if (_hWnd == IntPtr.Zero || _isDisposed) return;

        int vx = NativeMethods.GetSystemMetrics(NativeMethods.SM_XVIRTUALSCREEN);
        int vy = NativeMethods.GetSystemMetrics(NativeMethods.SM_YVIRTUALSCREEN);
        int vcx = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXVIRTUALSCREEN);
        int vcy = NativeMethods.GetSystemMetrics(NativeMethods.SM_CYVIRTUALSCREEN);

        if (vcx <= 0 || vcy <= 0)
        {
            vx = 0;
            vy = 0;
            vcx = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXSCREEN);
            vcy = NativeMethods.GetSystemMetrics(NativeMethods.SM_CYSCREEN);
        }

        if (!_isWindowVisible || _vx != vx || _vy != vy || _vWidth != vcx || _vHeight != vcy)
        {
            _vx = vx;
            _vy = vy;
            _vWidth = vcx;
            _vHeight = vcy;
            _isWindowVisible = true;

            NativeMethods.SetWindowPos(
                _hWnd,
                new IntPtr(NativeMethods.HWND_TOPMOST),
                vx,
                vy,
                vcx,
                vcy,
                NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);
        }
        else
        {
            // Re-assert topmost ordering without moving or resizing
            NativeMethods.SetWindowPos(
                _hWnd,
                new IntPtr(NativeMethods.HWND_TOPMOST),
                0,
                0,
                0,
                0,
                NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
        }
    }

    private void EnsureMemoryBuffer(IntPtr hdc, int width, int height)
    {
        if (_memDC != IntPtr.Zero && _memWidth == width && _memHeight == height)
        {
            return;
        }

        CleanupMemoryBuffer();

        _memDC = NativeMethods.CreateCompatibleDC(hdc);
        _hBitmap = NativeMethods.CreateCompatibleBitmap(hdc, width, height);
        _oldBitmap = NativeMethods.SelectObject(_memDC, _hBitmap);
        _memWidth = width;
        _memHeight = height;
    }

    private void CleanupMemoryBuffer()
    {
        if (_memDC != IntPtr.Zero)
        {
            if (_oldBitmap != IntPtr.Zero)
            {
                NativeMethods.SelectObject(_memDC, _oldBitmap);
                _oldBitmap = IntPtr.Zero;
            }
            if (_hBitmap != IntPtr.Zero)
            {
                NativeMethods.DeleteObject(_hBitmap);
                _hBitmap = IntPtr.Zero;
            }
            NativeMethods.DeleteDC(_memDC);
            _memDC = IntPtr.Zero;
        }
        _memWidth = 0;
        _memHeight = 0;
    }

    private IntPtr WndProcHandler(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == NativeMethods.WM_ERASEBKGND)
        {
            return (IntPtr)1;
        }

        if (msg == NativeMethods.WM_PAINT)
        {
            HandlePaintMessage(hWnd);
            return IntPtr.Zero;
        }

        return NativeMethods.DefWindowProcW(hWnd, msg, wParam, lParam);
    }

    private void HandlePaintMessage(IntPtr hWnd)
    {
        IntPtr hdc = NativeMethods.BeginPaint(hWnd, out var ps);
        if (hdc == IntPtr.Zero)
        {
            return;
        }

        try
        {
            int width = _vWidth > 0 ? _vWidth : (ps.rcPaint.Right - ps.rcPaint.Left);
            int height = _vHeight > 0 ? _vHeight : (ps.rcPaint.Bottom - ps.rcPaint.Top);
            if (width <= 0 || height <= 0)
            {
                width = 1920;
                height = 1080;
            }

            EnsureMemoryBuffer(hdc, width, height);
            if (_memDC != IntPtr.Zero)
            {
                RenderOverlayFrame(hdc, width, height);
            }
        }
        finally
        {
            NativeMethods.EndPaint(hWnd, ref ps);
        }
    }

    private void RenderOverlayFrame(IntPtr hdc, int width, int height)
    {
        // 1. Fill entire memory buffer with ColorKey (Magenta -> fully transparent)
        var rc = new NativeMethods.Rect { Left = 0, Top = 0, Right = width, Bottom = height };
        NativeMethods.FillRect(_memDC, ref rc, _hMagentaBrush);

        // 2. Draw single target crosshair & jitter boundary if visible
        if (_isSingleTargetVisible)
        {
            int cx = _singleTargetX - _vx;
            int cy = _singleTargetY - _vy;
            if (_singleJitterRadius > 0)
            {
                DrawJitterBoundary(_memDC, cx, cy, (int)_singleJitterRadius);
            }
            DrawCrosshair(_memDC, cx, cy, 14);
        }

        // 3. Draw numbered markers
        lock (_lock)
        {
            foreach (var marker in _markers)
            {
                int mx = marker.X - _vx;
                int my = marker.Y - _vy;
                DrawNumberedMarker(_memDC, mx, my, marker.Index);
            }
        }

        // 4. Draw ripple if active
        var ripple = _ripplePoint;
        if (ripple != null)
        {
            int rx = ripple.X - _vx;
            int ry = ripple.Y - _vy;
            DrawRippleRing(_memDC, rx, ry);
        }

        // BitBlt pre-buffered frame to layered window
        NativeMethods.BitBlt(hdc, 0, 0, width, height, _memDC, 0, 0, NativeMethods.SRCCOPY);
    }

    /// <summary>
    /// Atomically records a marker and triggers a momentary ripple in a single redraw pass,
    /// avoiding repeated full-screen redraw passes and eliminating input lag.
    /// </summary>
    public void AddMarkerWithRipple(int index, int x, int y, int durationMs = 350)
    {
        if (_hWnd == IntPtr.Zero || _isDisposed) return;

        lock (_lock)
        {
            _markers.Add(new MarkerPoint(index, x, y));
        }

        TriggerMomentaryRipple(index, x, y, durationMs);
    }

    public void AddMarker(int index, int x, int y)
    {
        if (_hWnd == IntPtr.Zero || _isDisposed) return;

        lock (_lock)
        {
            _markers.Add(new MarkerPoint(index, x, y));
        }

        EnsureWindowPosition();
        NativeMethods.InvalidateRect(_hWnd, IntPtr.Zero, false);
    }

    public void SetMarkers(IEnumerable<MarkerPoint> markers)
    {
        if (_hWnd == IntPtr.Zero || _isDisposed) return;

        lock (_lock)
        {
            _markers.Clear();
            _markers.AddRange(markers);
        }

        if (_markers.Count > 0)
        {
            EnsureWindowPosition();
        }
        else if (!_isSingleTargetVisible && _ripplePoint == null)
        {
            Hide();
            return;
        }

        NativeMethods.InvalidateRect(_hWnd, IntPtr.Zero, false);
    }

    public void ClearMarkers()
    {
        if (_hWnd == IntPtr.Zero || _isDisposed) return;

        lock (_lock)
        {
            _markers.Clear();
        }

        if (!_isSingleTargetVisible && _ripplePoint == null)
        {
            Hide();
        }
        else
        {
            NativeMethods.InvalidateRect(_hWnd, IntPtr.Zero, false);
        }
    }

    private void CancelRippleTokenSource()
    {
        if (_rippleCts == null) return;
        try
        {
            _rippleCts.Cancel();
            _rippleCts.Dispose();
        }
        catch (ObjectDisposedException)
        {
            // CTS already disposed during teardown; safe to ignore
        }
        finally
        {
            _rippleCts = null;
        }
    }

    /// <summary>
    /// Unconditionally clears all macro markers and dismisses the overlay window immediately.
    /// </summary>
    public void ClearAndHide()
    {
        CancelRippleTokenSource();
        _ripplePoint = null;

        lock (_lock)
        {
            _markers.Clear();
        }

        Hide();
    }

    public void Update(int targetX, int targetY, double jitterRadiusPx, bool isVisible)
    {
        if (_hWnd == IntPtr.Zero || _isDisposed) return;

        _singleTargetX = targetX;
        _singleTargetY = targetY;
        _singleJitterRadius = jitterRadiusPx;
        _isSingleTargetVisible = isVisible;

        if (!isVisible)
        {
            lock (_lock)
            {
                if (_markers.Count == 0 && _ripplePoint == null)
                {
                    Hide();
                    return;
                }
            }
        }
        else
        {
            EnsureWindowPosition();
        }

        NativeMethods.InvalidateRect(_hWnd, IntPtr.Zero, false);
    }

    public void ShowClickRipple(int x, int y, int index = 0, int durationMs = 350)
    {
        TriggerMomentaryRipple(index, x, y, durationMs);
    }

    private void TriggerMomentaryRipple(int index, int x, int y, int durationMs)
    {
        if (_hWnd == IntPtr.Zero || _isDisposed) return;

        CancelRippleTokenSource();
        _rippleCts = new System.Threading.CancellationTokenSource();
        var token = _rippleCts.Token;

        _ripplePoint = new MarkerPoint(index, x, y);
        EnsureWindowPosition();
        NativeMethods.InvalidateRect(_hWnd, IntPtr.Zero, false);

        System.Threading.Tasks.Task.Delay(durationMs, token).ContinueWith(t =>
        {
            if (!t.IsCanceled && !_isDisposed)
            {
                _ripplePoint = null;
                lock (_lock)
                {
                    if (_markers.Count == 0 && !_isSingleTargetVisible)
                    {
                        Hide();
                        return;
                    }
                }

                if (_hWnd != IntPtr.Zero)
                {
                    NativeMethods.InvalidateRect(_hWnd, IntPtr.Zero, false);
                }
            }
        }, System.Threading.Tasks.TaskScheduler.Default);
    }

    private void DrawNumberedMarker(IntPtr memDC, int cx, int cy, int index)
    {
        const int badgeRadius = 14;

        IntPtr oldPen = NativeMethods.SelectObject(memDC, _hOutlinePen);
        IntPtr oldBrush = NativeMethods.SelectObject(memDC, _hBadgeBrush);

        // Draw solid circle with white border
        NativeMethods.Ellipse(memDC, cx - badgeRadius, cy - badgeRadius, cx + badgeRadius + 1, cy + badgeRadius + 1);

        // 4 crosshair ticks around the circle
        NativeMethods.SelectObject(memDC, _hTickPen);
        NativeMethods.MoveToEx(memDC, cx, cy - badgeRadius, IntPtr.Zero);
        NativeMethods.LineTo(memDC, cx, cy - badgeRadius - 4);
        NativeMethods.MoveToEx(memDC, cx, cy + badgeRadius, IntPtr.Zero);
        NativeMethods.LineTo(memDC, cx, cy + badgeRadius + 4);
        NativeMethods.MoveToEx(memDC, cx - badgeRadius, cy, IntPtr.Zero);
        NativeMethods.LineTo(memDC, cx - badgeRadius - 4, cy);
        NativeMethods.MoveToEx(memDC, cx + badgeRadius, cy, IntPtr.Zero);
        NativeMethods.LineTo(memDC, cx + badgeRadius + 4, cy);

        // Draw sequence index number centered inside badge
        if (_hFont != IntPtr.Zero)
        {
            IntPtr oldFont = NativeMethods.SelectObject(memDC, _hFont);
            NativeMethods.SetBkMode(memDC, NativeMethods.TRANSPARENT);
            NativeMethods.SetTextColor(memDC, CrosshairOutlineColor);

            string text = index.ToString();
            var rc = new NativeMethods.Rect
            {
                Left = cx - badgeRadius,
                Top = cy - badgeRadius + 1,
                Right = cx + badgeRadius + 1,
                Bottom = cy + badgeRadius + 1
            };

            NativeMethods.DrawTextW(memDC, text, text.Length, ref rc,
                NativeMethods.DT_CENTER | NativeMethods.DT_VCENTER | NativeMethods.DT_SINGLELINE);

            NativeMethods.SelectObject(memDC, oldFont);
        }

        NativeMethods.SelectObject(memDC, oldBrush);
        NativeMethods.SelectObject(memDC, oldPen);
    }

    private void DrawRippleRing(IntPtr memDC, int cx, int cy)
    {
        const int rippleRadius = 24;

        IntPtr oldPen = NativeMethods.SelectObject(memDC, _hRipplePen);
        IntPtr oldBrush = NativeMethods.SelectObject(memDC, _hMagentaBrush);

        NativeMethods.Ellipse(memDC, cx - rippleRadius, cy - rippleRadius, cx + rippleRadius + 1, cy + rippleRadius + 1);

        NativeMethods.SelectObject(memDC, oldBrush);
        NativeMethods.SelectObject(memDC, oldPen);
    }

    private void DrawJitterBoundary(IntPtr memDC, int cx, int cy, int jitterRadius)
    {
        IntPtr hJitterPen = NativeMethods.CreatePen(NativeMethods.PS_SOLID, 1, JitterBoundaryColor);
        try
        {
            IntPtr oldPen = NativeMethods.SelectObject(memDC, hJitterPen);
            IntPtr oldBrush = NativeMethods.SelectObject(memDC, _hMagentaBrush);

            NativeMethods.Ellipse(memDC, cx - jitterRadius, cy - jitterRadius, cx + jitterRadius, cy + jitterRadius);

            NativeMethods.SelectObject(memDC, oldBrush);
            NativeMethods.SelectObject(memDC, oldPen);
        }
        finally
        {
            NativeMethods.DeleteObject(hJitterPen);
        }
    }

    private void DrawCrosshair(IntPtr memDC, int cx, int cy, int crosshairRadius)
    {
        IntPtr hCrosshairPen = NativeMethods.CreatePen(NativeMethods.PS_SOLID, 2, CrosshairColor);
        IntPtr hDotBrush = NativeMethods.CreateSolidBrush(CrosshairColor);

        try
        {
            IntPtr oldBrush = NativeMethods.SelectObject(memDC, _hMagentaBrush);

            // Ring (outline then inner)
            IntPtr oldPen = NativeMethods.SelectObject(memDC, _hOutlinePen);
            NativeMethods.Ellipse(memDC, cx - crosshairRadius, cy - crosshairRadius, cx + crosshairRadius, cy + crosshairRadius);

            NativeMethods.SelectObject(memDC, hCrosshairPen);
            NativeMethods.Ellipse(memDC, cx - crosshairRadius, cy - crosshairRadius, cx + crosshairRadius, cy + crosshairRadius);

            // Ticks
            int innerGap = 3;
            int outerTick = crosshairRadius + 5;

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
            NativeMethods.DeleteObject(hCrosshairPen);
        }
    }

    public void Hide()
    {
        _isWindowVisible = false;
        if (_hWnd != IntPtr.Zero && !_isDisposed)
        {
            NativeMethods.ShowWindow(_hWnd, NativeMethods.SW_HIDE);
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        CleanupMemoryBuffer();

        if (_hFont != IntPtr.Zero) { NativeMethods.DeleteObject(_hFont); _hFont = IntPtr.Zero; }
        if (_hMagentaBrush != IntPtr.Zero) { NativeMethods.DeleteObject(_hMagentaBrush); _hMagentaBrush = IntPtr.Zero; }
        if (_hOutlinePen != IntPtr.Zero) { NativeMethods.DeleteObject(_hOutlinePen); _hOutlinePen = IntPtr.Zero; }
        if (_hTickPen != IntPtr.Zero) { NativeMethods.DeleteObject(_hTickPen); _hTickPen = IntPtr.Zero; }
        if (_hBadgeBrush != IntPtr.Zero) { NativeMethods.DeleteObject(_hBadgeBrush); _hBadgeBrush = IntPtr.Zero; }
        if (_hRipplePen != IntPtr.Zero) { NativeMethods.DeleteObject(_hRipplePen); _hRipplePen = IntPtr.Zero; }

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
        internal const int SW_SHOWNOACTIVATE = 4;

        internal const int HWND_TOPMOST = -1;
        internal const uint SWP_NOSIZE = 0x0001;
        internal const uint SWP_NOMOVE = 0x0002;
        internal const uint SWP_NOACTIVATE = 0x0010;
        internal const uint SWP_SHOWWINDOW = 0x0040;

        internal const uint LWA_COLORKEY = 0x00000001;

        internal const int PS_SOLID = 0;
        internal const uint WM_ERASEBKGND = 0x0014;
        internal const uint WM_PAINT = 0x000F;
        internal const uint SRCCOPY = 0x00CC0020;

        internal const int TRANSPARENT = 1;
        internal const uint DT_CENTER = 0x00000001;
        internal const uint DT_VCENTER = 0x00000004;
        internal const uint DT_SINGLELINE = 0x00000020;

        internal const int SM_CXSCREEN = 0;
        internal const int SM_CYSCREEN = 1;
        internal const int SM_XVIRTUALSCREEN = 76;
        internal const int SM_YVIRTUALSCREEN = 77;
        internal const int SM_CXVIRTUALSCREEN = 78;
        internal const int SM_CYVIRTUALSCREEN = 79;

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

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern IntPtr GetModuleHandleW(string? lpModuleName);

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
        internal static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool InvalidateRect(IntPtr hWnd, IntPtr lpRect, [MarshalAs(UnmanagedType.Bool)] bool bErase);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool UpdateWindow(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern IntPtr DefWindowProcW(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern IntPtr BeginPaint(IntPtr hWnd, out Paintstruct lpPaint);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool EndPaint(IntPtr hWnd, ref Paintstruct lpPaint);

        [StructLayout(LayoutKind.Sequential)]
        internal struct Paintstruct
        {
            internal IntPtr hdc;
            [MarshalAs(UnmanagedType.Bool)]
            internal bool fErase;
            internal Rect rcPaint;
            [MarshalAs(UnmanagedType.Bool)]
            internal bool fRestore;
            [MarshalAs(UnmanagedType.Bool)]
            internal bool fIncUpdate;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
            internal byte[] rgbReserved;
        }

        [DllImport("user32.dll")]
        internal static extern int GetSystemMetrics(int nIndex);

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
        internal static extern bool LineTo(IntPtr hdc, int x, int y);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern int FillRect(IntPtr hDC, [In] ref Rect lprc, IntPtr hbr);

        [DllImport("gdi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool BitBlt(IntPtr hdc, int x, int y, int cx, int cy, IntPtr hdcSrc, int x1, int y1, uint rop);

        [DllImport("gdi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern IntPtr CreateFontW(
            int nHeight, int nWidth, int nEscapement, int nOrientation,
            int fnWeight, uint fdwItalic, uint fdwUnderline, uint fdwStrikeOut,
            uint fdwCharSet, uint fdwOutputPrecision, uint fdwClipPrecision,
            uint fdwQuality, uint fdwPitchAndFamily, string lpszFace);

        [DllImport("gdi32.dll")]
        internal static extern int SetBkMode(IntPtr hdc, int iBkMode);

        [DllImport("gdi32.dll")]
        internal static extern uint SetTextColor(IntPtr hdc, uint crColor);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern int DrawTextW(IntPtr hdc, string lpchText, int cchText, ref Rect lprc, uint format);

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
