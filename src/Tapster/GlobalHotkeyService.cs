using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using static Tapster.NativeMethods;

namespace Tapster;

/// <summary>
/// Manages registration and dispatching of Windows global hotkeys using RegisterHotKey / WM_HOTKEY.
/// </summary>
public sealed class GlobalHotkeyService : IDisposable
{
    public const int HOTKEY_WAKE = 0x5412;
    public const int HOTKEY_CLICKER = 0x5414;
    public const int HOTKEY_HOLDER = 0x5415;
    public const int HOTKEY_TYPER = 0x5416;
    public const int HOTKEY_MACRO = 0x5417;
    public const int HOTKEY_PANIC_KILL = 0x5418;

    private readonly IntPtr _hWnd;
    private readonly ConcurrentDictionary<int, Action> _callbacks = new();
    private bool _isDisposed = false;

    public GlobalHotkeyService(IntPtr hWnd)
    {
        _hWnd = hWnd;
    }

    /// <summary>
    /// Registers a hotkey with explicit modifiers and virtual-key code.
    /// </summary>
    public bool Register(int id, uint modifiers, uint vk, Action callback)
    {
        if (_isDisposed || _hWnd == IntPtr.Zero) return false;

        // Unregister existing ID first if already registered
        Unregister(id);

        bool success = RegisterHotKey(_hWnd, id, modifiers, vk);
        if (success)
        {
            _callbacks[id] = callback;
        }
        return success;
    }

    /// <summary>
    /// Registers a hotkey parsed from a combo string such as "F6" or "Ctrl+Alt+T".
    /// </summary>
    public bool Register(int id, string combo, Action callback)
    {
        var (modifiers, vk) = ParseHotkey(combo);
        if (vk == 0) return false;
        return Register(id, modifiers, vk, callback);
    }

    /// <summary>
    /// Unregisters a registered hotkey by ID.
    /// </summary>
    public bool Unregister(int id)
    {
        _callbacks.TryRemove(id, out _);
        if (_hWnd != IntPtr.Zero)
        {
            return UnregisterHotKey(_hWnd, id);
        }
        return false;
    }

    /// <summary>
    /// Unregisters all registered hotkeys.
    /// </summary>
    public void UnregisterAll()
    {
        foreach (var id in _callbacks.Keys)
        {
            Unregister(id);
        }
        _callbacks.Clear();
    }

    /// <summary>
    /// Dispatches a window message. Returns true if the message was handled as a hotkey.
    /// </summary>
    public bool ProcessWindowMessage(uint uMsg, IntPtr wParam)
    {
        if (uMsg == WM_HOTKEY)
        {
            int id = wParam.ToInt32();
            if (_callbacks.TryGetValue(id, out var action))
            {
                action?.Invoke();
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Parses a friendly hotkey string (e.g. "Ctrl+Alt+T", "F6", "Shift+F10") into modifiers and VK code.
    /// </summary>
    public static (uint Modifiers, uint Vk) ParseHotkey(string combo)
    {
        if (string.IsNullOrWhiteSpace(combo)) return (0, 0);

        var tokens = combo.Split('+');
        uint modifiers = 0;
        uint vk = 0;

        foreach (var raw in tokens)
        {
            string t = raw.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(t)) continue;

            switch (t)
            {
                case "ctrl" or "control":
                    modifiers |= MOD_CONTROL;
                    break;
                case "alt":
                    modifiers |= MOD_ALT;
                    break;
                case "shift":
                    modifiers |= MOD_SHIFT;
                    break;
                case "win" or "windows":
                    modifiers |= MOD_WIN;
                    break;
                default:
                    vk = ParseVk(t);
                    break;
            }
        }

        return (modifiers, vk);
    }

    private static uint ParseVk(string key)
    {
        if (key.Length == 1)
        {
            char c = key[0];
            if (c >= 'a' && c <= 'z') return (uint)(c - 'a' + 0x41);
            if (c >= '0' && c <= '9') return (uint)(c - '0' + 0x30);
        }

        if (key.StartsWith('f') && int.TryParse(key.AsSpan(1), out int fNum) && fNum is >= 1 and <= 24)
        {
            return (uint)(0x70 + (fNum - 1)); // VK_F1 is 0x70
        }

        return key switch
        {
            "esc" or "escape" => 0x1B,
            "space" => 0x20,
            "enter" or "return" => 0x0D,
            "tab" => 0x09,
            "backspace" => 0x08,
            "delete" or "del" => 0x2E,
            "insert" or "ins" => 0x2D,
            "home" => 0x24,
            "end" => 0x23,
            "pageup" or "pgup" => 0x21,
            "pagedown" or "pgdn" => 0x22,
            "up" => 0x26,
            "down" => 0x28,
            "left" => 0x25,
            "right" => 0x27,
            _ => 0
        };
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        UnregisterAll();
    }
}
