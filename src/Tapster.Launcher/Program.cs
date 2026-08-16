using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;

namespace Tapster.Launcher;

internal static class Program
{
    private const string AppVersion = "1.1.0";

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private const int SW_RESTORE = 9;

    [STAThread]
    public static int Main(string[] args)
    {
        try
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string appDir = Path.Combine(localAppData, "Tapster", $"app-v{AppVersion}");
            string targetExe = Path.Combine(appDir, "Tapster.Fluent.exe");
            string hashFile = Path.Combine(appDir, ".payload_hash");

            var assembly = Assembly.GetExecutingAssembly();
            using Stream? stream = assembly.GetManifestResourceStream("payload.zip")
                ?? assembly.GetManifestResourceStream("Tapster.Launcher.payload.zip");

            string currentHash = "";
            if (stream != null)
            {
                using var sha256 = SHA256.Create();
                byte[] hashBytes = sha256.ComputeHash(stream);
                currentHash = Convert.ToHexString(hashBytes);
                stream.Position = 0;
            }

            bool needsExtract = !File.Exists(targetExe) ||
                                !File.Exists(hashFile) ||
                                (File.Exists(hashFile) && File.ReadAllText(hashFile).Trim() != currentHash);

            if (needsExtract)
            {
                // Kill any older running instance before updating files to avoid file lock on DLLs
                KillExistingInstances(appDir);
                ExtractPayload(appDir, stream);

                if (!string.IsNullOrEmpty(currentHash))
                {
                    try { File.WriteAllText(hashFile, currentHash); } catch { }
                }
            }
            else
            {
                // If already running and up to date, bring existing instance to foreground
                if (ActivateExistingInstance(appDir))
                {
                    return 0;
                }
            }

            if (!File.Exists(targetExe))
            {
                MessageBoxW(IntPtr.Zero, "Failed to locate Tapster.Fluent.exe after extraction.", "Tapster Error", 0x10);
                return 1;
            }

            // Launch target executable with all arguments
            var startInfo = new ProcessStartInfo
            {
                FileName = targetExe,
                Arguments = string.Join(" ", args),
                WorkingDirectory = appDir,
                UseShellExecute = true
            };

            Process.Start(startInfo);
            return 0;
        }
        catch (Exception ex)
        {
            MessageBoxW(IntPtr.Zero, $"Tapster Launcher encountered an error:\n{ex.Message}", "Tapster Error", 0x10);
            return 1;
        }
    }

    private static void KillExistingInstances(string appDir)
    {
        try
        {
            var processes = Process.GetProcessesByName("Tapster.Fluent");
            foreach (var p in processes)
            {
                try
                {
                    p.Kill();
                    p.WaitForExit(1000);
                }
                catch { }
            }
            Thread.Sleep(200);
        }
        catch { }
    }

    private static bool ActivateExistingInstance(string appDir)
    {
        try
        {
            var processes = Process.GetProcessesByName("Tapster.Fluent");
            foreach (var p in processes)
            {
                if (p.MainWindowHandle != IntPtr.Zero)
                {
                    ShowWindow(p.MainWindowHandle, SW_RESTORE);
                    SetForegroundWindow(p.MainWindowHandle);
                    return true;
                }
            }
        }
        catch { }
        return false;
    }

    private static void ExtractPayload(string targetDir, Stream? stream)
    {
        for (int retry = 0; retry < 3; retry++)
        {
            try
            {
                if (Directory.Exists(targetDir))
                {
                    Directory.Delete(targetDir, true);
                }
                Directory.CreateDirectory(targetDir);
                break;
            }
            catch
            {
                Thread.Sleep(300);
            }
        }

        if (stream == null)
        {
            string fallbackPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "payload.zip");
            if (File.Exists(fallbackPath))
            {
                ZipFile.ExtractToDirectory(fallbackPath, targetDir, true);
                return;
            }
            throw new FileNotFoundException("Embedded application payload was not found.");
        }

        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        archive.ExtractToDirectory(targetDir, true);
    }
}
