using System;
using System.IO;
using System.Text.Json;
using Microsoft.Win32;

namespace Tapster_Fluent;

public class AppSettings
{
    private const string REG_RUN_KEY = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string APP_NAME = "Tapster";

    private static readonly string SettingsFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Tapster");

    private static readonly string SettingsFilePath = Path.Combine(SettingsFolder, "settings.json");

    private static AppSettings? _instance;
    public static AppSettings Current => _instance ??= Load();

    public bool StartMinimizedToTray { get; set; } = false;
    public bool MinimizeToTrayOnClose { get; set; } = true;
    public double DelaySeconds { get; set; } = 3;

    public bool StartOnBoot
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(REG_RUN_KEY, false);
                return key?.GetValue(APP_NAME) != null;
            }
            catch
            {
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
                    string exePath = Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "Tapster.Fluent.exe");
                    key.SetValue(APP_NAME, $"\"{exePath}\" --tray");
                }
                else
                {
                    key.DeleteValue(APP_NAME, false);
                }
            }
            catch
            {
                // Ignored
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
        catch { }

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
        catch { }
    }
}
