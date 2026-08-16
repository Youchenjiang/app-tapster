using System;
using System.IO;
using System.Runtime.InteropServices;
using Tapster;

namespace Tapster_Fluent;

public sealed class SystemTrayManager : IDisposable
{
    private const uint TRAY_ID = 1;
    private const uint CMD_SHOW_HIDE = 1001;
    private const uint CMD_ALWAYS_ON_TOP = 1002;
    private const uint CMD_EXIT = 1003;

    private readonly IntPtr _hWnd;
    private readonly Action _onToggleVisibility;
    private readonly Action<bool> _onSetAlwaysOnTop;
    private readonly Func<bool> _getAlwaysOnTop;
    private readonly Action _onExit;

    private NativeMethods.NOTIFYICONDATAW _nid;
    private NativeMethods.SubclassProc? _subclassProc;
    private IntPtr _hIcon = IntPtr.Zero;
    private bool _isDisposed = false;

    public SystemTrayManager(
        IntPtr hWnd,
        Action onToggleVisibility,
        Action<bool> onSetAlwaysOnTop,
        Func<bool> getAlwaysOnTop,
        Action onExit)
    {
        _hWnd = hWnd;
        _onToggleVisibility = onToggleVisibility;
        _onSetAlwaysOnTop = onSetAlwaysOnTop;
        _getAlwaysOnTop = getAlwaysOnTop;
        _onExit = onExit;

        InitializeTrayIcon();
        InstallWindowSubclass();
    }

    private void InitializeTrayIcon()
    {
        string iconPath = Path.Combine(AppContext.BaseDirectory, "app.ico");
        if (!File.Exists(iconPath))
        {
            iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
        }

        if (File.Exists(iconPath))
        {
            _hIcon = NativeMethods.LoadImageW(
                IntPtr.Zero,
                iconPath,
                NativeMethods.IMAGE_ICON,
                16,
                16,
                NativeMethods.LR_LOADFROMFILE);
        }

        _nid = new NativeMethods.NOTIFYICONDATAW
        {
            cbSize = (uint)Marshal.SizeOf<NativeMethods.NOTIFYICONDATAW>(),
            hWnd = _hWnd,
            uID = TRAY_ID,
            uFlags = NativeMethods.NIF_MESSAGE | NativeMethods.NIF_ICON | NativeMethods.NIF_TIP,
            uCallbackMessage = NativeMethods.WM_TRAYICON,
            hIcon = _hIcon,
            szTip = "Tapster (Ctrl+Alt+T to Wake)"
        };

        NativeMethods.Shell_NotifyIconW(NativeMethods.NIM_ADD, ref _nid);
        NativeMethods.Shell_NotifyIconW(NativeMethods.NIM_SETVERSION, ref _nid);
    }

    private void InstallWindowSubclass()
    {
        _subclassProc = WindowSubclassCallback;
        NativeMethods.SetWindowSubclass(_hWnd, _subclassProc, (UIntPtr)TRAY_ID, IntPtr.Zero);
    }

    private IntPtr WindowSubclassCallback(
        IntPtr hWnd,
        uint uMsg,
        IntPtr wParam,
        IntPtr lParam,
        UIntPtr uIdSubclass,
        IntPtr dwRefData)
    {
        if (uMsg == NativeMethods.WM_TRAYICON)
        {
            uint mouseMsg = (uint)(lParam.ToInt64() & 0xFFFF);

            if (mouseMsg == NativeMethods.WM_LBUTTONDBLCLK || mouseMsg == NativeMethods.WM_LBUTTONUP)
            {
                _onToggleVisibility();
                return IntPtr.Zero;
            }
            else if (mouseMsg == NativeMethods.WM_RBUTTONUP || mouseMsg == NativeMethods.WM_CONTEXTMENU)
            {
                ShowContextMenu();
                return IntPtr.Zero;
            }
        }

        return NativeMethods.DefSubclassProc(hWnd, uMsg, wParam, lParam);
    }

    private void ShowContextMenu()
    {
        IntPtr hMenu = NativeMethods.CreatePopupMenu();
        if (hMenu == IntPtr.Zero) return;

        try
        {
            NativeMethods.AppendMenuW(hMenu, NativeMethods.MF_STRING, (UIntPtr)CMD_SHOW_HIDE, "顯示 / 隱藏 Tapster");
            
            uint topFlags = NativeMethods.MF_STRING | (_getAlwaysOnTop() ? NativeMethods.MF_CHECKED : NativeMethods.MF_UNCHECKED);
            NativeMethods.AppendMenuW(hMenu, topFlags, (UIntPtr)CMD_ALWAYS_ON_TOP, "永遠置頂 (Always on Top)");

            NativeMethods.AppendMenuW(hMenu, NativeMethods.MF_SEPARATOR, UIntPtr.Zero, string.Empty);
            NativeMethods.AppendMenuW(hMenu, NativeMethods.MF_STRING, (UIntPtr)CMD_EXIT, "結束 Tapster (Exit)");

            NativeMethods.GetCursorPos(out NativeMethods.POINT pt);
            NativeMethods.SetForegroundWindow(_hWnd);

            uint cmd = NativeMethods.TrackPopupMenuEx(
                hMenu,
                NativeMethods.TPM_RETURNCMD | NativeMethods.TPM_RIGHTBUTTON,
                pt.X,
                pt.Y,
                _hWnd,
                IntPtr.Zero);

            if (cmd == CMD_SHOW_HIDE)
            {
                _onToggleVisibility();
            }
            else if (cmd == CMD_ALWAYS_ON_TOP)
            {
                bool isTop = !_getAlwaysOnTop();
                _onSetAlwaysOnTop(isTop);
            }
            else if (cmd == CMD_EXIT)
            {
                _onExit();
            }
        }
        finally
        {
            NativeMethods.DestroyMenu(hMenu);
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        NativeMethods.Shell_NotifyIconW(NativeMethods.NIM_DELETE, ref _nid);

        if (_subclassProc != null)
        {
            NativeMethods.RemoveWindowSubclass(_hWnd, _subclassProc, (UIntPtr)TRAY_ID);
            _subclassProc = null;
        }

        if (_hIcon != IntPtr.Zero)
        {
            NativeMethods.DestroyIcon(_hIcon);
            _hIcon = IntPtr.Zero;
        }
    }
}
