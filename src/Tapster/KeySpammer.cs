using System.Runtime.InteropServices;
using static Tapster.NativeMethods;

namespace Tapster;

/// <summary>
/// Provides keyboard key spamming and key-down state detection for auto-clicking and hold-to-click features.
/// </summary>
public static class KeySpammer
{
    /// <summary>
    /// Simulates a single complete key press and release (tap).
    /// </summary>
    public static void Spam(string keys)
    {
        if (string.IsNullOrWhiteSpace(keys)) return;

        Keyboard.Press(keys);
        Keyboard.Release(keys);
    }

    /// <summary>
    /// Checks whether a specific virtual key is currently pressed down.
    /// </summary>
    /// <param name="vk">Virtual Key code (e.g. 0x75 for F6).</param>
    /// <returns>True if the key is currently held down; otherwise false.</returns>
    public static bool IsKeyDown(int vk)
    {
        return (GetAsyncKeyState(vk) & 0x8000) != 0;
    }
}
