using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Tapster;

public enum MacroActionType
{
    ClickLeft,
    ClickRight,
    ClickMiddle,
    KeyPress,
    KeyRelease,
    TypeText
}

public class MacroAction
{
    public MacroActionType Type { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public string Data { get; set; } = "";
    public long DelayMs { get; set; }
}

public class MacroRecorder
{
    private readonly List<MacroAction> _actions = new();
    private volatile bool _isRecording;
    private Thread? _recordThread;
    private readonly Stopwatch _stopwatch = new();
    private long _lastActionTimeMs;

    public IReadOnlyList<MacroAction> Actions
    {
        get
        {
            lock (_actions)
            {
                return _actions.ToArray();
            }
        }
    }

    public void StartRecording(Action<int>? onActionCaptured = null)
    {
        StopRecording();

        lock (_actions)
        {
            _actions.Clear();
        }

        _isRecording = true;
        _stopwatch.Restart();
        _lastActionTimeMs = 0;

        _recordThread = new Thread(() => RecordLoop(onActionCaptured))
        {
            IsBackground = true,
            Name = "Tapster_MacroRecordWorker"
        };
        _recordThread.Start();
    }

    private void RecordLoop(Action<int>? onActionCaptured)
    {
        bool prevLeft = false;
        bool prevRight = false;
        bool prevMiddle = false;
        var prevKeyStates = new bool[256];

        // Read initial key states
        for (int i = 1; i < 256; i++)
        {
            short state = NativeMethods.GetAsyncKeyState(i);
            prevKeyStates[i] = (state & 0x8000) != 0 || state < 0;
        }
        prevLeft = prevKeyStates[NativeMethods.VK_LBUTTON];
        prevRight = prevKeyStates[NativeMethods.VK_RBUTTON];
        prevMiddle = prevKeyStates[NativeMethods.VK_MBUTTON];

        // Give a short buffer (150ms) so clicking the "Start Recording" button isn't recorded
        Thread.Sleep(150);

        while (_isRecording)
        {
            long now = _stopwatch.ElapsedMilliseconds;

            // 1. Mouse Button State Check
            if (NativeMethods.GetCursorPos(out var pt))
            {
                short leftState = NativeMethods.GetAsyncKeyState(NativeMethods.VK_LBUTTON);
                short rightState = NativeMethods.GetAsyncKeyState(NativeMethods.VK_RBUTTON);
                short middleState = NativeMethods.GetAsyncKeyState(NativeMethods.VK_MBUTTON);

                bool left = (leftState & 0x8000) != 0 || leftState < 0;
                bool right = (rightState & 0x8000) != 0 || rightState < 0;
                bool middle = (middleState & 0x8000) != 0 || middleState < 0;

                if (left && !prevLeft)
                {
                    AddAction(MacroActionType.ClickLeft, pt.X, pt.Y, "", now);
                    onActionCaptured?.Invoke(Actions.Count);
                }
                if (right && !prevRight)
                {
                    AddAction(MacroActionType.ClickRight, pt.X, pt.Y, "", now);
                    onActionCaptured?.Invoke(Actions.Count);
                }
                if (middle && !prevMiddle)
                {
                    AddAction(MacroActionType.ClickMiddle, pt.X, pt.Y, "", now);
                    onActionCaptured?.Invoke(Actions.Count);
                }

                prevLeft = left;
                prevRight = right;
                prevMiddle = middle;
            }

            // 2. Keyboard Key State Check (scan valid keys, skip mouse buttons)
            for (int vk = 0x08; vk <= 0xFE; vk++)
            {
                if (vk == NativeMethods.VK_LBUTTON || vk == NativeMethods.VK_RBUTTON || vk == NativeMethods.VK_MBUTTON)
                    continue;

                short keyState = NativeMethods.GetAsyncKeyState(vk);
                bool isDown = (keyState & 0x8000) != 0 || keyState < 0;
                if (isDown != prevKeyStates[vk])
                {
                    prevKeyStates[vk] = isDown;
                    string keyName = Keyboard.GetKeyName(vk);
                    if (!string.IsNullOrEmpty(keyName))
                    {
                        AddAction(isDown ? MacroActionType.KeyPress : MacroActionType.KeyRelease, 0, 0, keyName, now);
                        onActionCaptured?.Invoke(Actions.Count);
                    }
                }
            }

            Thread.Sleep(10);
        }
    }

    private void AddAction(MacroActionType type, int x, int y, string data, long now)
    {
        lock (_actions)
        {
            long delay = now - _lastActionTimeMs;
            _lastActionTimeMs = now;
            _actions.Add(new MacroAction
            {
                Type = type,
                X = x,
                Y = y,
                Data = data,
                DelayMs = delay
            });
        }
    }

    public void StopRecording()
    {
        _isRecording = false;
        try
        {
            _recordThread?.Join(300);
        }
        catch { }
        _recordThread = null;
        _stopwatch.Stop();
    }

    public void Clear()
    {
        StopRecording();
        lock (_actions)
        {
            _actions.Clear();
        }
    }

    public async Task ReplayAsync(int repeatCount, double speedMultiplier, CancellationToken token, Action<int, int>? progressCallback = null)
    {
        List<MacroAction> actionsSnapshot;
        lock (_actions)
        {
            actionsSnapshot = new List<MacroAction>(_actions);
        }
        if (actionsSnapshot.Count == 0) return;
        bool infinite = repeatCount <= 0;

        int currentLoop = 0;
        while (!token.IsCancellationRequested && (infinite || currentLoop < repeatCount))
        {
            currentLoop++;
            for (int i = 0; i < actionsSnapshot.Count; i++)
            {
                token.ThrowIfCancellationRequested();
                if (Keyboard.IsEscPressed()) throw new OperationCanceledException("Esc pressed");

                var action = actionsSnapshot[i];
                progressCallback?.Invoke(currentLoop, i + 1);

                int delay = (int)(action.DelayMs / Math.Max(0.1, speedMultiplier));
                if (delay > 0)
                {
                    await Task.Delay(delay, token);
                }

                switch (action.Type)
                {
                    case MacroActionType.ClickLeft:
                        Mouse.ClickAt(action.X, action.Y, "left");
                        break;
                    case MacroActionType.ClickRight:
                        Mouse.ClickAt(action.X, action.Y, "right");
                        break;
                    case MacroActionType.ClickMiddle:
                        Mouse.ClickAt(action.X, action.Y, "middle");
                        break;
                    case MacroActionType.KeyPress:
                        Keyboard.Press(action.Data);
                        break;
                    case MacroActionType.KeyRelease:
                        Keyboard.Release(action.Data);
                        break;
                    case MacroActionType.TypeText:
                        Keyboard.Type(action.Data);
                        break;
                }
            }
        }
    }
}
