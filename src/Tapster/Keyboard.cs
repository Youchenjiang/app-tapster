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
    /// Type a single character.
    /// </summary>
    public static void Type(char c)
    {
        var inputs = new INPUT[2];

        // Key down
        inputs[0].type = INPUT_KEYBOARD;
        inputs[0].u.ki.wVk = (ushort)VkKeyScan(c);

        // Key up
        inputs[1].type = INPUT_KEYBOARD;
        inputs[1].u.ki.wVk = (ushort)VkKeyScan(c);
        inputs[1].u.ki.dwFlags = KEYEVENTF_KEYUP;

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
            .Select(MapKeyName)
            .ToArray();
    }

    private static ushort MapKeyName(string name) => name switch
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
        _ => (ushort)name[0]  // Single character keys (a-z, 0-9, etc.)
    };

    [DllImport("user32.dll")]
    private static extern short VkKeyScan(char ch);
}
