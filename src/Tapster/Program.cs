namespace Tapster;

/// <summary>
/// Lightweight input automation tool for Windows 11.
/// Supports auto-typing, key holding, and mouse clicking.
/// </summary>
internal static class Program
{
    private static readonly CancellationTokenSource _cts = new();
    private static bool _running;

    static void Main(string[] args)
    {
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            Stop();
        };

        if (args.Length == 0)
        {
            PrintUsage();
            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
            return;
        }

        var mode = args[0].ToLower();
        int delay = GetArg(args, "--delay", 3);
        int count = GetArg(args, "--count", 0);
        int intervalMs = GetArg(args, "--interval", 100);

        Console.WriteLine($"[Tapster] Mode: {mode}, Delay: {delay}s");

        // Countdown
        for (int i = delay; i > 0; i--)
        {
            Console.Write($"\r[Tapster] Starting in {i}s... ");
            Thread.Sleep(1000);
            if (_cts.IsCancellationRequested) return;
        }
        Console.WriteLine("\r[Tapster] Running...            ");

        _running = true;

        switch (mode)
        {
            case "type":
                RunTyper(args, intervalMs, count);
                break;
            case "hold":
                RunHolder(args);
                break;
            case "click":
                RunClicker(args, intervalMs, count);
                break;
            default:
                Console.WriteLine($"[Tapster] Unknown mode: {mode}");
                PrintUsage();
                break;
        }

        _running = false;
        Console.WriteLine("[Tapster] Done.");
    }

    private static void RunTyper(string[] args, int intervalMs, int count)
    {
        string? text = GetArgString(args, "--text");
        string? file = GetArgString(args, "--file");

        if (file != null && File.Exists(file))
            text = File.ReadAllText(file);

        if (string.IsNullOrEmpty(text))
        {
            // Read from clipboard or stdin
            text = Console.ReadLine();
        }

        if (string.IsNullOrEmpty(text))
        {
            Console.WriteLine("[Tapster] No text to type.");
            return;
        }

        Keyboard.ReleaseAllModifiers();

        int typed = 0;
        foreach (var c in text)
        {
            if (!_running || _cts.IsCancellationRequested) break;

            Keyboard.Type(c);
            typed++;

            if (intervalMs > 0)
                Thread.Sleep(intervalMs);

            if (count > 0 && typed >= count) break;
        }
    }

    private static void RunHolder(string[] args)
    {
        string key = GetArgString(args, "--key") ?? "w";
        int duration = GetArg(args, "--duration", 10);

        Keyboard.ReleaseAllModifiers();
        Keyboard.Press(key);

        Console.WriteLine($"[Tapster] Holding '{key}'... (Esc to release)");

        var start = DateTime.UtcNow;
        while (_running && !_cts.IsCancellationRequested)
        {
            if (duration > 0)
            {
                var elapsed = (DateTime.UtcNow - start).TotalSeconds;
                if (elapsed >= duration) break;
            }

            Thread.Sleep(50);
        }

        Keyboard.Release(key);
    }

    private static void RunClicker(string[] args, int intervalMs, int count)
    {
        string button = GetArgString(args, "--button") ?? "left";
        int? x = GetArgNullable(args, "--x");
        int? y = GetArgNullable(args, "--y");

        int clicked = 0;
        while (_running && !_cts.IsCancellationRequested)
        {
            if (x.HasValue && y.HasValue)
                Mouse.ClickAt(x.Value, y.Value, button);
            else
                Mouse.Click(button);

            clicked++;

            if (count > 0)
            {
                Console.Write($"\r[Tapster] Clicked {clicked}/{count}");
                if (clicked >= count) break;
            }

            if (intervalMs > 0)
                Thread.Sleep(intervalMs);
        }

        if (count > 0) Console.WriteLine();
    }

    private static void Stop()
    {
        _running = false;
        _cts.Cancel();
        Keyboard.ReleaseAllModifiers();
        Console.WriteLine("\n[Tapster] Aborted.");
    }

    private static void PrintUsage()
    {
        Console.WriteLine("""
            Tapster - Lightweight Input Automation for Windows 11

            Usage: tapster <mode> [options]

            Modes:
              type    Auto-type text character by character
              hold    Hold down a key (or combo) for a duration
              click   Click mouse button repeatedly

            Options:
              --delay <seconds>      Startup delay (default: 3)
              --count <n>            Number of iterations (0 = infinite)
              --interval <ms>        Interval between actions in ms (default: 100)

            Type mode:
              --text <string>        Text to type
              --file <path>          Read text from file

            Hold mode:
              --key <key>            Key to hold (e.g. w, space, ctrl+shift)
              --duration <seconds>   Hold duration (0 = until Esc)

            Click mode:
              --button <left|right|middle>  Mouse button (default: left)
              --x <pixels>          X coordinate
              --y <pixels>          Y coordinate

            Examples:
              tapster type --text "Hello World" --delay 5
              tapster hold --key w --duration 30
              tapster click --button left --interval 50 --count 100
              tapster click --x 500 --y 300 -c 10
            """);
    }

    // ── Argument parsing helpers ──────────────────────────────────────────────

    private static int GetArg(string[] args, string name, int defaultValue)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i].Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                if (int.TryParse(args[i + 1], out int val))
                    return val;
            }
        }
        return defaultValue;
    }

    private static int? GetArgNullable(string[] args, string name)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i].Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                if (int.TryParse(args[i + 1], out int val))
                    return val;
            }
        }
        return null;
    }

    private static string? GetArgString(string[] args, string name)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i].Equals(name, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        }
        return null;
    }
}
