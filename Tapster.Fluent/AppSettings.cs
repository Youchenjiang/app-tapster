using System;
using System.IO;
using System.Text.Json;
using Microsoft.Win32;

namespace Tapster_Fluent;

public class AppSettings
{
    private const string REG_RUN_KEY = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string APP_NAME = "Tapster";

    private static readonly string SettingsFolder = Path.Join(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Tapster");

    private static readonly string SettingsFilePath = Path.Join(SettingsFolder, "settings.json");

    private static AppSettings? _instance;
    public static AppSettings Current => _instance ??= Load();

    public bool StartMinimizedToTray { get; set; } = false;
    public bool MinimizeToTrayOnClose { get; set; } = true;
    public double DelaySeconds { get; set; } = 3;

    public string HotkeyClicker { get; set; } = "F6";
    public string HotkeyHolder { get; set; } = "F7";
    public string HotkeyTyper { get; set; } = "F8";
    public string HotkeyMacro { get; set; } = "F9";
    public string HotkeyPanicKill { get; set; } = "F10";
    public bool ClickerHoldMode { get; set; } = false;
    public bool ClickerIsKeySpammer { get; set; } = false;
    public string ClickerSpamKey { get; set; } = "space";
    public bool ClickerJitterEnabled { get; set; } = false;
    public double ClickerTimeJitterPercent { get; set; } = 15;
    public double ClickerLocationJitterPx { get; set; } = 0;

    public bool StartOnBoot
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(REG_RUN_KEY, false);
                return key?.GetValue(APP_NAME) != null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to read autorun registry key: {ex.Message}");
                return false;
            }
        }
        set
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(REG_RUN_KEY, true);
                if (key == null) return;

                if (value)
                {
                    string exePath = Environment.ProcessPath ?? Path.Join(AppContext.BaseDirectory, "Tapster.Fluent.exe");
                    key.SetValue(APP_NAME, $"\"{exePath}\" --tray");
                }
                else
                {
                    key.DeleteValue(APP_NAME, false);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to write autorun registry key: {ex.Message}");
            }
        }
    }

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsFilePath))
            {
                string json = File.ReadAllText(SettingsFilePath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);
                if (settings != null) return settings;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load settings file: {ex.Message}");
        }

        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            if (!Directory.Exists(SettingsFolder))
            {
                Directory.CreateDirectory(SettingsFolder);
            }

            string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsFilePath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save settings file: {ex.Message}");
        }
    }
}
