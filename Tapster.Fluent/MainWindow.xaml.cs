using System;
using System.IO;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using WinRT.Interop;
using Tapster;

namespace Tapster_Fluent;

public sealed partial class MainWindow : Window
{
    public static MainWindow? Instance { get; private set; }
    private bool _isAlwaysOnTop = false;
    public bool IsAlwaysOnTop => _isAlwaysOnTop;
    private const int HOTKEY_ID = 0x5412;
    private readonly IntPtr _hWnd;
    private SystemTrayManager? _trayManager;
    private bool _isExplicitExit = false;

    public MainWindow()
    {
        Instance = this;
        InitializeComponent();

        _hWnd = WindowNative.GetWindowHandle(this);

        // Pure Native DWM Dark Mode & Title Bar Styling for 100% native 144Hz+ zero-latency window dragging
        NativeMethods.SetWindowAttribute(_hWnd, NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE, 1);
        NativeMethods.SetWindowAttribute(_hWnd, NativeMethods.DWMWA_CAPTION_COLOR, 0x00202020u); // COLORREF 0x00BBGGRR -> #202020
        NativeMethods.SetWindowAttribute(_hWnd, NativeMethods.DWMWA_TEXT_COLOR, 0x00FFFFFFu);

        // Set window & taskbar icon — must use .ico; .png is not supported by SetIcon()
        string[] iconCandidates =
        [
            Path.Join(AppContext.BaseDirectory, "app.ico"),
            Path.Join(AppContext.BaseDirectory, "Assets", "AppIcon.ico"),
        ];
        foreach (var candidate in iconCandidates)
        {
            if (File.Exists(candidate))
            {
                AppWindow.SetIcon(candidate);
                break;
            }
        }

        // Register Ctrl+Alt+T global wake hotkey
        NativeMethods.RegisterHotKey(_hWnd, HOTKEY_ID, NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT, (uint)'T');

        // Initialize System Tray Manager
        _trayManager = new SystemTrayManager(
            _hWnd,
            onToggleVisibility: ToggleVisibility,
            onSetAlwaysOnTop: SetAlwaysOnTop,
            getAlwaysOnTop: () => _isAlwaysOnTop,
            onExit: ExitApplication
        );

        // Close button minimizes to tray instead of exiting
        AppWindow.Closing += AppWindow_Closing;
        Closed += MainWindow_Closed;

        RootFrame.Navigate(typeof(MainPage));
    }

    private void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_isExplicitExit) return;

        if (AppSettings.Current.MinimizeToTrayOnClose)
        {
            args.Cancel = true;
            AppWindow.Hide();
        }
        else
        {
            ExitApplication();
        }
    }

    public event Action<bool>? AlwaysOnTopChanged;

    public void SetAlwaysOnTop(bool isTop)
    {
        _isAlwaysOnTop = isTop;
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsAlwaysOnTop = isTop;
        }
        AlwaysOnTopChanged?.Invoke(isTop);
    }

    public void ToggleVisibility()
    {
        if (AppWindow.IsVisible)
        {
            AppWindow.Hide();
        }
        else
        {
            BringToFront();
        }
    }

    public void BringToFront()
    {
        NativeMethods.ShowWindow(_hWnd, NativeMethods.SW_RESTORE);
        AppWindow.Show();
        NativeMethods.SetForegroundWindow(_hWnd);
        Activate();
    }

    public void ExitApplication()
    {
        _isExplicitExit = true;
        _trayManager?.Dispose();
        _trayManager = null;
        NativeMethods.UnregisterHotKey(_hWnd, HOTKEY_ID);
        Keyboard.ReleaseAllModifiers();
        Close();
        Application.Current.Exit();
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        _trayManager?.Dispose();
        _trayManager = null;
        NativeMethods.UnregisterHotKey(_hWnd, HOTKEY_ID);
        Keyboard.ReleaseAllModifiers();
    }
}
