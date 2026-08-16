using System;
using System.IO;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using WinRT.Interop;
using Tapster;

namespace Tapster_Fluent;

public sealed partial class MainWindow : Window
{
    private const int HOTKEY_ID = 0x5412;
    private readonly IntPtr _hWnd;
    private SystemTrayManager? _trayManager;
    private bool _isExplicitExit = false;

    public MainWindow()
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(TitleBarDragRegion);

        _hWnd = WindowNative.GetWindowHandle(this);

        // Set window & taskbar icon — must use .ico; .png is not supported by SetIcon()
        string[] iconCandidates =
        [
            Path.Combine(AppContext.BaseDirectory, "app.ico"),
            Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico"),
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
            getAlwaysOnTop: () => AlwaysOnTopCheckBox.IsChecked == true,
            onExit: ExitApplication
        );

        // Close button minimizes to tray instead of exiting
        AppWindow.Closing += AppWindow_Closing;
        Closed += MainWindow_Closed;

        RootFrame.Navigate(typeof(MainPage));
    }

    private void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (!_isExplicitExit)
        {
            args.Cancel = true;
            AppWindow.Hide();
        }
    }

    private void AlwaysOnTopCheckBox_CheckedChanged(object sender, RoutedEventArgs e)
    {
        SetAlwaysOnTop(AlwaysOnTopCheckBox.IsChecked == true);
    }

    public void SetAlwaysOnTop(bool isTop)
    {
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsAlwaysOnTop = isTop;
            AlwaysOnTopCheckBox.IsChecked = isTop;
        }
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
