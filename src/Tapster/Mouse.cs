using System.Runtime.InteropServices;
using static Tapster.NativeMethods;

namespace Tapster;

/// <summary>
/// Simulates mouse input using Windows SendInput API.
/// </summary>
public static class Mouse
{
    /// <summary>
    /// Click at the current cursor position.
    /// </summary>
    public static void Click(string button = "left")
    {
        var (down, up) = button.ToLower() switch
        {
            "right" => (MOUSEEVENTF_RIGHTDOWN, MOUSEEVENTF_RIGHTUP),
            "middle" or "mid" => (MOUSEEVENTF_MIDDLEDOWN, MOUSEEVENTF_MIDDLEUP),
            _ => (MOUSEEVENTF_LEFTDOWN, MOUSEEVENTF_LEFTUP)
        };

        var inputs = new INPUT[2];
        inputs[0].type = INPUT_MOUSE;
        inputs[0].u.mi.dwFlags = down;
        inputs[1].type = INPUT_MOUSE;
        inputs[1].u.mi.dwFlags = up;

        SendInput(2, inputs, Marshal.SizeOf<INPUT>());
    }

    /// <summary>
    /// Click at specific screen coordinates.
    /// </summary>
    public static void ClickAt(int x, int y, string button = "left")
    {
        MoveTo(x, y);
        Thread.Sleep(10);
        Click(button);
    }

    /// <summary>
    /// Move cursor to absolute screen coordinates.
    /// </summary>
    public static void MoveTo(int x, int y)
    {
        int screenW = GetSystemMetrics(SM_CXSCREEN);
        int screenH = GetSystemMetrics(SM_CYSCREEN);

        int absX = (int)(x * 65535.0 / screenW);
        int absY = (int)(y * 65535.0 / screenH);

        var input = new INPUT
        {
            type = INPUT_MOUSE,
            u = new INPUTUNION
            {
                mi = new MOUSEINPUT
                {
                    dx = absX,
                    dy = absY,
                    dwFlags = MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_MOVE
                }
            }
        };

        SendInput(1, [input], Marshal.SizeOf<INPUT>());
    }

    /// <summary>
    /// Get current cursor position.
    /// </summary>
    public static (int X, int Y) GetPosition()
    {
        GetCursorPos(out var point);
        return (point.X, point.Y);
    }

    /// <summary>
    /// Check if a mouse button is currently pressed.
    /// </summary>
    public static bool IsButtonDown(string button)
    {
        int vk = button.ToLower() switch
        {
            "right" => VK_RBUTTON,
            "middle" or "mid" => VK_MBUTTON,
            _ => VK_LBUTTON
        };

        return (GetAsyncKeyState(vk) & 0x8000) != 0;
    }
}
