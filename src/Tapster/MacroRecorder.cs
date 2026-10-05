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
    MouseMove,
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

    public bool IgnoreMouseMove { get; set; } = true;

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

    public void StartRecording(Action<int>? onActionCaptured = null, bool ignoreMouseMove = true)
    {
        IgnoreMouseMove = ignoreMouseMove;
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
        int prevMouseX = 0;
        int prevMouseY = 0;
        long lastMoveTimeMs = 0;

        // Read initial key states
        for (int i = 1; i < 256; i++)
        {
            short state = NativeMethods.GetAsyncKeyState(i);
            prevKeyStates[i] = (state & 0x8000) != 0 || state < 0;
        }
        prevLeft = prevKeyStates[NativeMethods.VK_LBUTTON];
        prevRight = prevKeyStates[NativeMethods.VK_RBUTTON];
        prevMiddle = prevKeyStates[NativeMethods.VK_MBUTTON];

        if (NativeMethods.GetCursorPos(out var initPt))
        {
            prevMouseX = initPt.X;
            prevMouseY = initPt.Y;
        }

        // Give a short buffer (150ms) so clicking the "Start Recording" button isn't recorded
        Thread.Sleep(150);

        while (_isRecording)
        {
            long now = _stopwatch.ElapsedMilliseconds;

            if (NativeMethods.GetCursorPos(out var pt))
            {
                CheckMouseMove(pt, now, ref prevMouseX, ref prevMouseY, ref lastMoveTimeMs, onActionCaptured);
                CheckMouseButtons(pt, now, ref prevLeft, ref prevRight, ref prevMiddle, onActionCaptured);
            }

            CheckKeyboardKeys(now, prevKeyStates, onActionCaptured);
            Thread.Sleep(10);
        }
    }

    private void CheckMouseMove(NativeMethods.POINT pt, long now, ref int prevMouseX, ref int prevMouseY, ref long lastMoveTimeMs, Action<int>? onActionCaptured)
    {
        if (IgnoreMouseMove) return;

        bool hasMoved = Math.Abs(pt.X - prevMouseX) > 2 || Math.Abs(pt.Y - prevMouseY) > 2;
        if (hasMoved && now - lastMoveTimeMs >= 30)
        {
            AddAction(MacroActionType.MouseMove, pt.X, pt.Y, string.Empty, now);
            prevMouseX = pt.X;
            prevMouseY = pt.Y;
            lastMoveTimeMs = now;
            onActionCaptured?.Invoke(Actions.Count);
        }
    }

    private void CheckMouseButtons(NativeMethods.POINT pt, long now, ref bool prevLeft, ref bool prevRight, ref bool prevMiddle, Action<int>? onActionCaptured)
    {
        short leftState = NativeMethods.GetAsyncKeyState(NativeMethods.VK_LBUTTON);
        short rightState = NativeMethods.GetAsyncKeyState(NativeMethods.VK_RBUTTON);
        short middleState = NativeMethods.GetAsyncKeyState(NativeMethods.VK_MBUTTON);

        bool left = (leftState & 0x8000) != 0 || leftState < 0;
        bool right = (rightState & 0x8000) != 0 || rightState < 0;
        bool middle = (middleState & 0x8000) != 0 || middleState < 0;

        if (left && !prevLeft)
        {
            AddAction(MacroActionType.ClickLeft, pt.X, pt.Y, string.Empty, now);
            onActionCaptured?.Invoke(Actions.Count);
        }
        if (right && !prevRight)
        {
            AddAction(MacroActionType.ClickRight, pt.X, pt.Y, string.Empty, now);
            onActionCaptured?.Invoke(Actions.Count);
        }
        if (middle && !prevMiddle)
        {
            AddAction(MacroActionType.ClickMiddle, pt.X, pt.Y, string.Empty, now);
            onActionCaptured?.Invoke(Actions.Count);
        }

        prevLeft = left;
        prevRight = right;
        prevMiddle = middle;
    }

    private void CheckKeyboardKeys(long now, bool[] prevKeyStates, Action<int>? onActionCaptured)
    {
        for (int vk = 0x08; vk <= 0xFE; vk++)
        {
            if (vk is NativeMethods.VK_LBUTTON or NativeMethods.VK_RBUTTON or NativeMethods.VK_MBUTTON)
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
        catch (ThreadStateException ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to join recording thread: {ex.Message}");
        }
        catch (ThreadInterruptedException ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to join recording thread: {ex.Message}");
        }
        finally
        {
            _recordThread = null;
            _stopwatch.Stop();
        }
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

                ExecuteAction(action);
            }
        }
    }

    public static void ExecuteAction(MacroAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
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
            case MacroActionType.MouseMove:
                Mouse.MoveTo(action.X, action.Y);
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

    public bool RemoveActionAt(int index)
    {
        lock (_actions)
        {
            if (index >= 0 && index < _actions.Count)
            {
                _actions.RemoveAt(index);
                return true;
            }
            return false;
        }
    }

    public bool UpdateAction(int index, MacroAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        lock (_actions)
        {
            if (index >= 0 && index < _actions.Count)
            {
                _actions[index] = action;
                return true;
            }
            return false;
        }
    }

    public void InsertAction(int index, MacroAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        lock (_actions)
        {
            if (index < 0 || index >= _actions.Count)
            {
                _actions.Add(action);
            }
            else
            {
                _actions.Insert(index, action);
            }
        }
    }
}
