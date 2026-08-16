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
    private bool _isRecording;
    private readonly Stopwatch _stopwatch = new();
    private long _lastActionTimeMs;

    public IReadOnlyList<MacroAction> Actions => _actions;

    public void StartRecording()
    {
        _actions.Clear();
        _isRecording = true;
        _stopwatch.Restart();
        _lastActionTimeMs = 0;
    }

    public void RecordClick(int x, int y, string button = "left")
    {
        if (!_isRecording) return;
        long now = _stopwatch.ElapsedMilliseconds;
        long delay = now - _lastActionTimeMs;
        _lastActionTimeMs = now;

        var type = button.ToLower() switch
        {
            "right" => MacroActionType.ClickRight,
            "middle" => MacroActionType.ClickMiddle,
            _ => MacroActionType.ClickLeft
        };

        _actions.Add(new MacroAction
        {
            Type = type,
            X = x,
            Y = y,
            DelayMs = delay
        });
    }

    public void RecordKey(string key, bool isDown)
    {
        if (!_isRecording) return;
        long now = _stopwatch.ElapsedMilliseconds;
        long delay = now - _lastActionTimeMs;
        _lastActionTimeMs = now;

        _actions.Add(new MacroAction
        {
            Type = isDown ? MacroActionType.KeyPress : MacroActionType.KeyRelease,
            Data = key,
            DelayMs = delay
        });
    }

    public void StopRecording()
    {
        _isRecording = false;
        _stopwatch.Stop();
    }

    public async Task ReplayAsync(int repeatCount, double speedMultiplier, CancellationToken token, Action<int, int>? progressCallback = null)
    {
        if (_actions.Count == 0) return;
        bool infinite = repeatCount <= 0;

        int currentLoop = 0;
        while (!token.IsCancellationRequested && (infinite || currentLoop < repeatCount))
        {
            currentLoop++;
            for (int i = 0; i < _actions.Count; i++)
            {
                token.ThrowIfCancellationRequested();
                if (Keyboard.IsEscPressed()) throw new OperationCanceledException("Esc pressed");

                var action = _actions[i];
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
