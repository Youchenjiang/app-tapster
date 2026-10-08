using System;
using System.IO;
using System.Runtime.InteropServices;
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
    private readonly IntPtr _hWnd;
    private SystemTrayManager? _trayManager;
    private GlobalHotkeyService? _hotkeyService;
    private NativeMethods.SubclassProc? _hotkeySubclassProc;
    private const uint HOTKEY_SUBCLASS_ID = 2;
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

        // Initialize Global Hotkey Dispatcher via Window Subclassing
        _hotkeyService = new GlobalHotkeyService(_hWnd);
        _hotkeySubclassProc = HotkeySubclassCallback;
        NativeMethods.SetWindowSubclass(_hWnd, _hotkeySubclassProc, (UIntPtr)HOTKEY_SUBCLASS_ID, IntPtr.Zero);
        RegisterAllGlobalHotkeys();

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
        AppWindow.Changed += (sender, args) =>
        {
            if (args.DidPresenterChange && AppWindow.Presenter is OverlappedPresenter presenter)
            {
                if (presenter.State == OverlappedPresenterState.Minimized)
                {
                    MainPage.Instance?.OnGuiHidden();
                }
                else if (presenter.State is OverlappedPresenterState.Restored or OverlappedPresenterState.Maximized)
                {
                    MainPage.Instance?.OnGuiRestored();
                }
            }
        };
        // Set responsive default window size and center on active display
        if (AppWindow != null)
        {
            var displayArea = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary);
            int defaultWidth = 980;
            int defaultHeight = 680;
            if (displayArea != null)
            {
                int targetWidth = Math.Min(defaultWidth, (int)(displayArea.WorkArea.Width * 0.9));
                int targetHeight = Math.Min(defaultHeight, (int)(displayArea.WorkArea.Height * 0.9));
                var centeredPosition = new Windows.Graphics.PointInt32
                {
                    X = displayArea.WorkArea.X + (displayArea.WorkArea.Width - targetWidth) / 2,
                    Y = displayArea.WorkArea.Y + (displayArea.WorkArea.Height - targetHeight) / 2
                };
                AppWindow.MoveAndResize(new Windows.Graphics.RectInt32(centeredPosition.X, centeredPosition.Y, targetWidth, targetHeight));
            }
            else
            {
                AppWindow.Resize(new Windows.Graphics.SizeInt32(defaultWidth, defaultHeight));
            }
        }

        Closed += MainWindow_Closed;
        RootFrame.Navigate(typeof(MainPage));
    }

    private void RegisterAllGlobalHotkeys()
    {
        if (_hotkeyService == null) return;

        // Wake / Hide: Ctrl+Alt+T
        _hotkeyService.Register(GlobalHotkeyService.HOTKEY_WAKE, NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT, (uint)'T', ToggleVisibility);

        // Clicker / Spammer: F6
        _hotkeyService.Register(GlobalHotkeyService.HOTKEY_CLICKER, AppSettings.Current.HotkeyClicker, () => MainPage.Instance?.ToggleClickerFromHotkey());

        // Key Holder: F7
        _hotkeyService.Register(GlobalHotkeyService.HOTKEY_HOLDER, AppSettings.Current.HotkeyHolder, () => MainPage.Instance?.ToggleHolderFromHotkey());

        // Auto Typer: F8
        _hotkeyService.Register(GlobalHotkeyService.HOTKEY_TYPER, AppSettings.Current.HotkeyTyper, () => MainPage.Instance?.ToggleTyperFromHotkey());

        // Macro Replay: F9
        _hotkeyService.Register(GlobalHotkeyService.HOTKEY_MACRO, AppSettings.Current.HotkeyMacro, () => MainPage.Instance?.ToggleMacroFromHotkey());

        // Panic Kill All: F10
        _hotkeyService.Register(GlobalHotkeyService.HOTKEY_PANIC_KILL, AppSettings.Current.HotkeyPanicKill, () => MainPage.Instance?.PanicKillAll());
    }

    private IntPtr HotkeySubclassCallback(
        IntPtr hWnd,
        uint uMsg,
        IntPtr wParam,
        IntPtr lParam,
        UIntPtr uIdSubclass,
        IntPtr dwRefData)
    {
        if (uMsg == NativeMethods.WM_GETMINMAXINFO)
        {
            uint dpi = NativeMethods.GetDpiForWindow(hWnd);
            double scale = dpi > 0 ? dpi / 96.0 : 1.0;
            var mmi = Marshal.PtrToStructure<NativeMethods.MINMAXINFO>(lParam);
            mmi.ptMinTrackSize.X = (int)(680 * scale);
            mmi.ptMinTrackSize.Y = (int)(480 * scale);
            Marshal.StructureToPtr(mmi, lParam, true);
            return IntPtr.Zero;
        }

        if (_hotkeyService != null && _hotkeyService.ProcessWindowMessage(uMsg, wParam))
        {
            return IntPtr.Zero;
        }

        return NativeMethods.DefSubclassProc(hWnd, uMsg, wParam, lParam);
    }

    private void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_isExplicitExit) return;

        MainPage.Instance?.OnGuiHidden();

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
            MainPage.Instance?.OnGuiHidden();
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
        MainPage.Instance?.OnGuiRestored();
    }

    private void CleanupHotkeys()
    {
        if (_hotkeySubclassProc != null)
        {
            NativeMethods.RemoveWindowSubclass(_hWnd, _hotkeySubclassProc, (UIntPtr)HOTKEY_SUBCLASS_ID);
            _hotkeySubclassProc = null;
        }
        _hotkeyService?.Dispose();
        _hotkeyService = null;
    }

    public void ExitApplication()
    {
        _isExplicitExit = true;
        MainPage.Instance?.OnGuiHidden();
        _trayManager?.Dispose();
        _trayManager = null;
        CleanupHotkeys();
        Keyboard.ReleaseAllModifiers();
        Close();
        Application.Current.Exit();
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        MainPage.Instance?.OnGuiHidden();
        _trayManager?.Dispose();
        _trayManager = null;
        CleanupHotkeys();
        Keyboard.ReleaseAllModifiers();
    }
}
