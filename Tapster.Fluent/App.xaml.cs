using System;
using System.IO;
using Microsoft.UI.Xaml;

namespace Tapster_Fluent;

public partial class App : Application
{
    private Window? _window;

    public App()
    {
        UnhandledException += (sender, e) =>
        {
            LogCrash("UnhandledException: " + e.Exception);
        };

        try
        {
            InitializeComponent();
        }
        catch (Exception ex)
        {
            LogCrash("InitializeComponent failed: " + ex);
            throw;
        }
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        try
        {
            _window = new MainWindow();
            _window.Activate();
        }
        catch (Exception ex)
        {
            LogCrash("OnLaunched failed: " + ex);
            throw;
        }
    }

    private static void LogCrash(string message)
    {
        try
        {
            string path = Path.Combine(Path.GetTempPath(), "tapster_crash.log");
            File.AppendAllText(path, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
        }
        catch { }
    }
}
