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

        Closed += MainWindow_Closed;
        RootFrame.Navigate(typeof(MainPage));
    }

    private void AlwaysOnTopCheckBox_CheckedChanged(object sender, RoutedEventArgs e)
    {
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsAlwaysOnTop = AlwaysOnTopCheckBox.IsChecked == true;
        }
    }

    public void BringToFront()
    {
        NativeMethods.ShowWindow(_hWnd, NativeMethods.SW_RESTORE);
        AppWindow.Show();
        NativeMethods.SetForegroundWindow(_hWnd);
        Activate();
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        NativeMethods.UnregisterHotKey(_hWnd, HOTKEY_ID);
        Keyboard.ReleaseAllModifiers();
    }
}
