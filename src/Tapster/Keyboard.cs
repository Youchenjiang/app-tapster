using System.Runtime.InteropServices;
using static Tapster.NativeMethods;

namespace Tapster;

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
        foreach (var vk in ParseKeys(keys))
        {
            SendKey(vk, down: true);
        }
    }

    /// <summary>
    /// Release a key (or combo).
    /// </summary>
    public static void Release(string keys)
    {
        foreach (var vk in ParseKeys(keys))
        {
            SendKey(vk, down: false);
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
    /// Release all modifier keys to prevent stuck keys.
    /// </summary>
    public static void ReleaseAllModifiers()
    {
        string[] mods = ["shift", "ctrl", "alt", "windows"];
        foreach (var mod in mods)
        {
            try { Release(mod); } catch { }
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
    /// Maps a virtual key code to a friendly key name string.
    /// </summary>
    public static string GetKeyName(int vk)
    {
        if (vk >= 0x41 && vk <= 0x5A) return ((char)('a' + (vk - 0x41))).ToString();
        if (vk >= 0x30 && vk <= 0x39) return ((char)('0' + (vk - 0x30))).ToString();
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
            0x14 => "capslock",
            0x26 => "up",
            0x28 => "down",
            0x25 => "left",
            0x27 => "right",
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

    private static void SendKey(ushort vk, bool down)
    {
        var input = new INPUT
        {
            type = INPUT_KEYBOARD,
            u = new INPUTUNION
            {
                ki = new KEYBDINPUT
                {
                    wVk = vk,
                    dwFlags = down ? 0 : KEYEVENTF_KEYUP
                }
            }
        };

        SendInput(1, [input], Marshal.SizeOf<INPUT>());
    }

    private static ushort[] ParseKeys(string combo)
    {
        return combo.Split('+')
            .Select(k => k.Trim().ToLower())
            .Where(k => !string.IsNullOrEmpty(k))
            .Select(MapKeyName)
            .ToArray();
    }

    private static ushort MapKeyName(string name)
    {
        string k = name.ToLower();
        if (k.Length == 1)
        {
            char ch = k[0];
            if (ch >= 'a' && ch <= 'z') return (ushort)(ch - 'a' + 0x41); // VK_A .. VK_Z
            if (ch >= '0' && ch <= '9') return (ushort)(ch - '0' + 0x30); // VK_0 .. VK_9
        }

        return k switch
        {
            "shift" => 0x10,
            "ctrl" or "control" => 0x11,
            "alt" => 0x12,
            "windows" or "win" => 0x5B,
            "enter" or "return" => 0x0D,
            "space" => 0x20,
            "tab" => 0x09,
            "esc" or "escape" => 0x1B,
            "backspace" => 0x08,
            "delete" or "del" => 0x2E,
            "capslock" => 0x14,
            "up" => 0x26,
            "down" => 0x28,
            "left" => 0x25,
            "right" => 0x27,
            "f1" => 0x70, "f2" => 0x71, "f3" => 0x72, "f4" => 0x73,
            "f5" => 0x74, "f6" => 0x75, "f7" => 0x76, "f8" => 0x77,
            "f9" => 0x78, "f10" => 0x79, "f11" => 0x7A, "f12" => 0x7B,
            _ => (ushort)k[0]
        };
    }

    [DllImport("user32.dll")]
    private static extern short VkKeyScan(char ch);
}
