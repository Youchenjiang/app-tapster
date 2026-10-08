using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
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
    private uint _recordThreadId;
    private IntPtr _mouseHook = IntPtr.Zero;
    private IntPtr _keyboardHook = IntPtr.Zero;
    private readonly ManualResetEventSlim _startedEvent = new(false);

    private readonly Stopwatch _stopwatch = new();
    private long _lastActionTimeMs;

    private Action<int>? _countCallback;
    private Action<MacroAction, int>? _detailedCallback;

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

    public void StartRecording(Action<int>? onActionCaptured = null, bool ignoreMouseMove = true, Action<MacroAction, int>? onActionDetailed = null)
    {
        IgnoreMouseMove = ignoreMouseMove;
        StopRecording();

        lock (_actions)
        {
            _actions.Clear();
        }

        _countCallback = onActionCaptured;
        _detailedCallback = onActionDetailed;
        _isRecording = true;
        _startedEvent.Reset();

        _recordThread = new Thread(RecordLoop)
        {
            IsBackground = true,
            Name = "Tapster_MacroRecordWorker"
        };
        _recordThread.Start();

        // Wait until hooks are safely installed so recording starts immediately without missing first input
        _startedEvent.Wait(1000);
    }

    private void RecordLoop()
    {
        _recordThreadId = NativeMethods.GetCurrentThreadId();
        IntPtr hMod = NativeMethods.GetModuleHandleW(null);

        // Keep delegates alive as local variables on the thread stack for the duration of the message loop
        NativeMethods.HookProc mouseProc = MouseHookCallback;
        NativeMethods.HookProc kbdProc = KeyboardHookCallback;

        _mouseHook = NativeMethods.SetWindowsHookExW(NativeMethods.WH_MOUSE_LL, mouseProc, hMod, 0);
        _keyboardHook = NativeMethods.SetWindowsHookExW(NativeMethods.WH_KEYBOARD_LL, kbdProc, hMod, 0);

        _stopwatch.Restart();
        _lastActionTimeMs = 0;

        _startedEvent.Set();

        while (_isRecording && NativeMethods.GetMessageW(out var msg, IntPtr.Zero, 0, 0) > 0)
        {
            NativeMethods.TranslateMessage(ref msg);
            NativeMethods.DispatchMessageW(ref msg);
        }

        CleanupHooks();
        GC.KeepAlive(mouseProc);
        GC.KeepAlive(kbdProc);
    }

    private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && _isRecording)
        {
            try
            {
                var hookStruct = Marshal.PtrToStructure<NativeMethods.Msllhookstruct>(lParam);
                int msg = wParam.ToInt32();
                long now = _stopwatch.ElapsedMilliseconds;

                switch (msg)
                {
                    case (int)NativeMethods.WM_LBUTTONDOWN:
                        RecordCapturedAction(MacroActionType.ClickLeft, hookStruct.pt.X, hookStruct.pt.Y, string.Empty, now);
                        break;
                    case (int)NativeMethods.WM_RBUTTONDOWN:
                        RecordCapturedAction(MacroActionType.ClickRight, hookStruct.pt.X, hookStruct.pt.Y, string.Empty, now);
                        break;
                    case 0x0207: // WM_MBUTTONDOWN
                        RecordCapturedAction(MacroActionType.ClickMiddle, hookStruct.pt.X, hookStruct.pt.Y, string.Empty, now);
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Mouse hook error: {ex.Message}");
            }
        }

        return NativeMethods.CallNextHookEx(_mouseHook, nCode, wParam, lParam);
    }

    private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && _isRecording)
        {
            ProcessKeyboardHookEvent(wParam.ToInt32(), lParam);
        }

        return NativeMethods.CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
    }

    private void ProcessKeyboardHookEvent(int msg, IntPtr lParam)
    {
        bool isKeyDown = msg is NativeMethods.WM_KEYDOWN or NativeMethods.WM_SYSKEYDOWN;
        bool isKeyUp = msg is NativeMethods.WM_KEYUP or NativeMethods.WM_SYSKEYUP;
        if (!isKeyDown && !isKeyUp)
        {
            return;
        }

        try
        {
            var hookStruct = Marshal.PtrToStructure<NativeMethods.Kbdllhookstruct>(lParam);
            int vk = (int)hookStruct.vkCode;
            string keyName = Keyboard.GetKeyName(vk);
            if (!string.IsNullOrEmpty(keyName))
            {
                var type = isKeyDown ? MacroActionType.KeyPress : MacroActionType.KeyRelease;
                RecordCapturedAction(type, 0, 0, keyName, _stopwatch.ElapsedMilliseconds);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Keyboard hook error: {ex.Message}");
        }
    }

    private void RecordCapturedAction(MacroActionType type, int x, int y, string data, long now)
    {
        MacroAction action;
        int totalCount;
        lock (_actions)
        {
            if (!_isRecording) return;

            long delay = _actions.Count == 0 ? 0 : Math.Max(0, now - _lastActionTimeMs);
            _lastActionTimeMs = now;
            action = new MacroAction
            {
                Type = type,
                X = x,
                Y = y,
                Data = data,
                DelayMs = delay
            };
            _actions.Add(action);
            totalCount = _actions.Count;
        }

        _countCallback?.Invoke(totalCount);
        _detailedCallback?.Invoke(action, totalCount);
    }

    private void CleanupHooks()
    {
        if (_mouseHook != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_mouseHook);
            _mouseHook = IntPtr.Zero;
        }
        if (_keyboardHook != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_keyboardHook);
            _keyboardHook = IntPtr.Zero;
        }
    }

    public void StopRecording()
    {
        _isRecording = false;
        _countCallback = null;
        _detailedCallback = null;

        if (_recordThreadId != 0)
        {
            NativeMethods.PostThreadMessageW(_recordThreadId, NativeMethods.WM_QUIT, IntPtr.Zero, IntPtr.Zero);
        }

        try
        {
            _recordThread?.Join(400);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to join recording thread: {ex.Message}");
        }
        finally
        {
            CleanupHooks();
            _recordThread = null;
            _recordThreadId = 0;
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

    public async Task ReplayAsync(
        int repeatCount,
        double speedMultiplier,
        CancellationToken token,
        Action<int, int>? progressCallback = null,
        Action<MacroAction>? actionExecutingCallback = null,
        Action? checkPanicCallback = null)
    {
        List<MacroAction> actionsSnapshot;
        lock (_actions)
        {
            actionsSnapshot = new List<MacroAction>(_actions);
        }
        if (actionsSnapshot.Count == 0) return;
        bool infinite = repeatCount <= 0;

        // Reset previous Esc key buffer state before starting replay
        Keyboard.ResetEscState();

        int currentLoop = 0;
        while (!token.IsCancellationRequested && (infinite || currentLoop < repeatCount))
        {
            currentLoop++;
            for (int i = 0; i < actionsSnapshot.Count; i++)
            {
                var action = actionsSnapshot[i];
                progressCallback?.Invoke(currentLoop, i + 1);

                await ReplaySingleStepAsync(action, speedMultiplier, token, actionExecutingCallback, checkPanicCallback);
            }
        }
    }

    private static async Task ReplaySingleStepAsync(
        MacroAction action,
        double speedMultiplier,
        CancellationToken token,
        Action<MacroAction>? actionExecutingCallback,
        Action? checkPanicCallback)
    {
        CheckReplaySafety(token, checkPanicCallback);

        int delay = (int)(action.DelayMs / Math.Max(0.1, speedMultiplier));
        if (delay > 0)
        {
            await Task.Delay(delay, token);
        }

        CheckReplaySafety(token, checkPanicCallback);

        actionExecutingCallback?.Invoke(action);
        ExecuteAction(action);
    }

    private static void CheckReplaySafety(CancellationToken token, Action? checkPanicCallback)
    {
        token.ThrowIfCancellationRequested();
        if (checkPanicCallback != null)
        {
            checkPanicCallback();
        }
        else if (Keyboard.IsEscPressed())
        {
            throw new OperationCanceledException("Esc pressed during macro replay");
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
