using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using static Tapster.NativeMethods;

namespace Tapster;

internal readonly record struct KeyStroke(ushort Vk, bool Extended = false);

/// <summary>
/// Simulates keyboard input using Windows SendInput API.
/// </summary>
public static partial class Keyboard
{
    /// <summary>
    /// Press and hold a key (or combo like "ctrl+shift+a").
    /// </summary>
    public static void Press(string keys)
    {
        foreach (var stroke in ParseKeys(keys))
        {
            SendKey(stroke, down: true);
        }
    }

    /// <summary>
    /// Release a key (or combo).
    /// </summary>
    public static void Release(string keys)
    {
        foreach (var stroke in ParseKeys(keys))
        {
            SendKey(stroke, down: false);
        }
    }

    /// <summary>
    /// Type a single character using Unicode SendInput for reliable VNC/app typing.
    /// </summary>
    public static void Type(char c)
    {
        if (c == '\r') return; // Skip \r in \r\n pairs
        if (c == '\n')
        {
            SendKey(0x0D, down: true);
            SendKey(0x0D, down: false);
            return;
        }

        var inputs = new INPUT[2];

        // Key down
        inputs[0].type = INPUT_KEYBOARD;
        inputs[0].u.ki.wScan = c;
        inputs[0].u.ki.dwFlags = KEYEVENTF_UNICODE;

        // Key up
        inputs[1].type = INPUT_KEYBOARD;
        inputs[1].u.ki.wScan = c;
        inputs[1].u.ki.dwFlags = KEYEVENTF_UNICODE | KEYEVENTF_KEYUP;

        SendInput(2, inputs, Marshal.SizeOf<INPUT>());
    }

    /// <summary>
    /// Type a string character by character.
    /// </summary>
    public static void Type(string text)
    {
        foreach (var c in text)
        {
            Type(c);
        }
    }

    /// <summary>
    /// Tap a key or combo (press and release with a brief delay).
    /// </summary>
    public static void Tap(string key)
    {
        Press(key);
        Thread.Sleep(15);
        Release(key);
    }

    /// <summary>
    /// Simulates pasting content via Ctrl+V key combination.
    /// </summary>
    public static void Paste()
    {
        Press("ctrl+v");
        Thread.Sleep(20);
        Release("ctrl+v");
        ReleaseAllModifiers();
    }

    /// <summary>
    /// Release all modifier keys to prevent stuck keys.
    /// </summary>
    public static void ReleaseAllModifiers()
    {
        string[] mods = ["shift", "ctrl", "alt", "windows"];
        foreach (var mod in mods)
        {
            try
            {
                Release(mod);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to release modifier key {mod}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Checks if the Esc key is currently pressed on the keyboard.
    /// </summary>
    public static bool IsEscPressed()
    {
        return (GetAsyncKeyState(0x1B) & 0x8000) != 0;
    }

    /// <summary>
    /// Resets the GetAsyncKeyState buffer for Esc key by reading it once.
    /// </summary>
    public static void ResetEscState()
    {
        _ = GetAsyncKeyState(0x1B);
    }

    /// <summary>
    /// Maps a virtual key code to a friendly key name string.
    /// </summary>
    public static string GetKeyName(int vk)
    {
        if (vk >= 0x41 && vk <= 0x5A) return ((char)('a' + (vk - 0x41))).ToString();
        if (vk >= 0x30 && vk <= 0x39) return ((char)('0' + (vk - 0x30))).ToString();
        if (vk >= 0x60 && vk <= 0x69) return $"num{vk - 0x60}";
        if (vk >= 0x70 && vk <= 0x7B) return $"f{vk - 0x70 + 1}";

        return vk switch
        {
            0x10 or 0xA0 or 0xA1 => "shift",
            0x11 or 0xA2 or 0xA3 => "ctrl",
            0x12 or 0xA4 or 0xA5 => "alt",
            0x5B or 0x5C => "win",
            0x0D => "enter",
            0x20 => "space",
            0x09 => "tab",
            0x1B => "esc",
            0x08 => "backspace",
            0x2E => "delete",
            0x2D => "insert",
            0x24 => "home",
            0x23 => "end",
            0x21 => "pageup",
            0x22 => "pagedown",
            0x14 => "capslock",
            0x26 => "up",
            0x28 => "down",
            0x25 => "left",
            0x27 => "right",
            0x2C => "printscreen",
            0x13 => "pause",
            0x90 => "numlock",
            0x91 => "scrolllock",
            0x6A => "multiply",
            0x6B => "add",
            0x6D => "subtract",
            0x6E => "decimal",
            0x6F => "divide",
            0xC0 => "`",
            0xBD => "-",
            0xBB => "=",
            0xDB => "[",
            0xDD => "]",
            0xDC => "\\",
            0xBA => ";",
            0xDE => "'",
            0xBC => ",",
            0xBE => ".",
            0xBF => "/",
            _ => $"vk_{vk}"
        };
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static void SendKey(KeyStroke stroke, bool down)
    {
        uint flags = down ? 0u : KEYEVENTF_KEYUP;
        if (stroke.Extended)
        {
            flags |= KEYEVENTF_EXTENDEDKEY;
        }

        var input = new INPUT
        {
            type = INPUT_KEYBOARD,
            u = new INPUTUNION
            {
                ki = new KEYBDINPUT
                {
                    wVk = stroke.Vk,
                    dwFlags = flags
                }
            }
        };

        SendInput(1, [input], Marshal.SizeOf<INPUT>());
    }

    private static void SendKey(ushort vk, bool down, bool extended = false)
    {
        SendKey(new KeyStroke(vk, extended), down);
    }

    [GeneratedRegex(@"(?i)\b(numpad|num)\+")]
    private static partial Regex NumpadPlusRegex();

    internal static string[] SplitCombo(string combo)
    {
        if (string.IsNullOrWhiteSpace(combo))
        {
            return [];
        }

        string trimmed = combo.Trim();
        if (trimmed == "+")
        {
            return ["+"];
        }

        string normalized = NumpadPlusRegex().Replace(trimmed, "add");
        if (normalized == "+")
        {
            return ["+"];
        }

        bool endsWithDoublePlus = normalized.EndsWith("++", StringComparison.Ordinal);
        string toSplit = endsWithDoublePlus ? normalized[..^1] : normalized;

        var tokens = toSplit.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        if (endsWithDoublePlus)
        {
            tokens.Add("+");
        }

        return tokens.ToArray();
    }

    internal static KeyStroke[] ParseKeys(string combo)
    {
        return SplitCombo(combo)
            .Select(MapKeyName)
            .Where(k => k.Vk != 0)
            .ToArray();
    }

    private static KeyStroke MapKeyName(string name)
    {
        string k = name.ToLowerInvariant();
        if (k.StartsWith("vk_") && int.TryParse(k[3..], out int parsedVk))
        {
            return new KeyStroke((ushort)parsedVk);
        }

        if (NamedKeyMap.TryGetValue(k, out KeyStroke stroke))
        {
            return stroke;
        }

        if (k.Length == 1)
        {
            char ch = k[0];
            if (ch >= 'a' && ch <= 'z') return new KeyStroke((ushort)(ch - 'a' + 0x41)); // VK_A .. VK_Z
            if (ch >= '0' && ch <= '9') return new KeyStroke((ushort)(ch - '0' + 0x30)); // VK_0 .. VK_9
        }

        return new KeyStroke(ResolveFallbackKey(k));
    }

    private static readonly Dictionary<string, KeyStroke> NamedKeyMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["shift"] = new(0x10),
        ["ctrl"] = new(0x11),
        ["control"] = new(0x11),
        ["alt"] = new(0x12),
        ["windows"] = new(0x5B),
        ["win"] = new(0x5B),
        ["enter"] = new(0x0D),
        ["return"] = new(0x0D),
        ["numpadenter"] = new(0x0D, Extended: true),
        ["numenter"] = new(0x0D, Extended: true),
        ["space"] = new(0x20),
        ["tab"] = new(0x09),
        ["esc"] = new(0x1B),
        ["escape"] = new(0x1B),
        ["backspace"] = new(0x08),
        ["delete"] = new(0x2E, Extended: true),
        ["del"] = new(0x2E, Extended: true),
        ["insert"] = new(0x2D, Extended: true),
        ["ins"] = new(0x2D, Extended: true),
        ["home"] = new(0x24, Extended: true),
        ["end"] = new(0x23, Extended: true),
        ["pageup"] = new(0x21, Extended: true),
        ["pgup"] = new(0x21, Extended: true),
        ["pagedown"] = new(0x22, Extended: true),
        ["pgdn"] = new(0x22, Extended: true),
        ["capslock"] = new(0x14),
        ["up"] = new(0x26, Extended: true),
        ["down"] = new(0x28, Extended: true),
        ["left"] = new(0x25, Extended: true),
        ["right"] = new(0x27, Extended: true),
        ["`"] = new(0xC0),
        ["~"] = new(0xC0),
        ["-"] = new(0xBD),
        ["_"] = new(0xBD),
        ["="] = new(0xBB),
        ["+"] = new(0xBB),
        ["["] = new(0xDB),
        ["{"] = new(0xDB),
        ["]"] = new(0xDD),
        ["}"] = new(0xDD),
        ["\\"] = new(0xDC),
        ["|"] = new(0xDC),
        [";"] = new(0xBA),
        [":"] = new(0xBA),
        ["'"] = new(0xDE),
        ["\""] = new(0xDE),
        [","] = new(0xBC),
        ["<"] = new(0xBC),
        ["."] = new(0xBE),
        [">"] = new(0xBE),
        ["/"] = new(0xBF),
        ["?"] = new(0xBF),
        ["f1"] = new(0x70), ["f2"] = new(0x71), ["f3"] = new(0x72), ["f4"] = new(0x73),
        ["f5"] = new(0x74), ["f6"] = new(0x75), ["f7"] = new(0x76), ["f8"] = new(0x77),
        ["f9"] = new(0x78), ["f10"] = new(0x79), ["f11"] = new(0x7A), ["f12"] = new(0x7B),
        ["numlock"] = new(0x90, Extended: true), ["numlk"] = new(0x90, Extended: true),
        ["scrolllock"] = new(0x91), ["scrlk"] = new(0x91),
        ["printscreen"] = new(0x2C, Extended: true), ["prtsc"] = new(0x2C, Extended: true), ["prtscr"] = new(0x2C, Extended: true),
        ["pause"] = new(0x13),
        ["numpad0"] = new(0x60), ["num0"] = new(0x60),
        ["numpad1"] = new(0x61), ["num1"] = new(0x61),
        ["numpad2"] = new(0x62), ["num2"] = new(0x62),
        ["numpad3"] = new(0x63), ["num3"] = new(0x63),
        ["numpad4"] = new(0x64), ["num4"] = new(0x64),
        ["numpad5"] = new(0x65), ["num5"] = new(0x65),
        ["numpad6"] = new(0x66), ["num6"] = new(0x66),
        ["numpad7"] = new(0x67), ["num7"] = new(0x67),
        ["numpad8"] = new(0x68), ["num8"] = new(0x68),
        ["numpad9"] = new(0x69), ["num9"] = new(0x69),
        ["multiply"] = new(0x6A), ["num*"] = new(0x6A),
        ["add"] = new(0x6B), ["num+"] = new(0x6B), ["numpad+"] = new(0x6B),
        ["subtract"] = new(0x6D), ["num-"] = new(0x6D),
        ["decimal"] = new(0x6E), ["num."] = new(0x6E),
        ["divide"] = new(0x6F, Extended: true), ["num/"] = new(0x6F, Extended: true)
    };

    private static ushort ResolveFallbackKey(string k)
    {
        if (k.Length == 0)
        {
            return 0;
        }

        short scan = VkKeyScan(k[0]);
        if (scan != -1)
        {
            return (ushort)(scan & 0xFF);
        }

        return k[0];
    }

    [DllImport("user32.dll")]
    private static extern short VkKeyScan(char ch);
}
