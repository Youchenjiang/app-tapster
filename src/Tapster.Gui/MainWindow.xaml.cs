using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Tapster.Gui;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer _countdownTimer;
    private int _countdownRemaining;
    private bool _running;
    private CancellationTokenSource? _cts;

    // Key holder state
    private readonly List<string> _selectedKeys = [];
    private bool _capturing;
    private IntPtr _keyboardHook;

    // Recording state
    private bool _recording;
    private readonly List<RecordedAction> _recordedActions = [];
    private DateTime _recordingStartTime;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
        KeyDown += MainWindow_KeyDown;

        _countdownTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _countdownTimer.Tick += CountdownTimer_Tick;

        // Center on screen
        Left = SystemParameters.PrimaryScreenWidth - Width - 20;
        Top = SystemParameters.PrimaryScreenHeight - Height - 60;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        BuildVirtualKeyboard();
    }

    private void MainWindow_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _running)
        {
            StopAction();
            e.Handled = true;
        }
    }

    // ── Virtual Keyboard ──────────────────────────────────────────────────────

    private void BuildVirtualKeyboard()
    {
        KeyboardPanel.Children.Clear();

        // Row 0: F-keys
        var row0 = CreateKeyRow(["Esc", "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9", "F10", "F11", "F12"]);
        KeyboardPanel.Children.Add(row0);

        // Row 1: Number row
        var row1 = CreateKeyRow(["`", "1", "2", "3", "4", "5", "6", "7", "8", "9", "0", "-", "=", "Bksp"]);
        KeyboardPanel.Children.Add(row1);

        // Row 2: QWERTY
        var row2 = CreateKeyRow(["Tab", "Q", "W", "E", "R", "T", "Y", "U", "I", "O", "P", "[", "]", "\\"]);
        KeyboardPanel.Children.Add(row2);

        // Row 3: Home row
        var row3 = CreateKeyRow(["Caps", "A", "S", "D", "F", "G", "H", "J", "K", "L", ";", "'", "Enter"]);
        KeyboardPanel.Children.Add(row3);

        // Row 4: Shift row
        var row4 = CreateKeyRow(["Shift", "Z", "X", "C", "V", "B", "N", "M", ",", ".", "/", "Shift"]);
        KeyboardPanel.Children.Add(row4);

        // Row 5: Bottom row
        var row5 = CreateKeyRow(["Ctrl", "Win", "Alt", "Space", "Alt", "Win", "Menu", "Ctrl"]);
        KeyboardPanel.Children.Add(row5);
    }

    private StackPanel CreateKeyRow(string[] keys)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 1, 0, 1) };

        foreach (var key in keys)
        {
            var vkName = MapKeyToVk(key);
            var isMod = key is "Ctrl" or "Alt" or "Shift" or "Win";

            var btn = new Button
            {
                Content = key,
                Tag = vkName,
                Style = isMod ? (Style)FindResource("ModifierKeyButton") : (Style)FindResource("KeyButton"),
                MinWidth = key == "Space" ? 180 : key is "Tab" or "Caps" or "Enter" or "Bksp" or "Shift" ? 50 : 28
            };
            btn.Click += KeyButton_Click;
            row.Children.Add(btn);
        }

        return row;
    }

    private static string MapKeyToVk(string key) => key.ToLower() switch
    {
        "esc" => "escape",
        "bksp" => "backspace",
        "win" => "lwin",
        "menu" => "apps",
        _ => key.ToLower()
    };

    private void KeyButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string vkName)
        {
            ToggleKey(vkName);
            UpdateKeyButtonStyles();
            UpdateComboDisplay();
        }
    }

    private void ToggleKey(string vkName)
    {
        if (_selectedKeys.Contains(vkName))
            _selectedKeys.Remove(vkName);
        else
            _selectedKeys.Add(vkName);
    }

    private void UpdateKeyButtonStyles()
    {
        foreach (var child in KeyboardPanel.Children)
        {
            if (child is StackPanel row)
            {
                foreach (var btn in row.Children.OfType<Button>())
                {
                    if (btn.Tag is string vkName)
                    {
                        btn.Style = _selectedKeys.Contains(vkName)
                            ? (Style)FindResource("ActiveKeyButton")
                            : vkName is "ctrl" or "alt" or "shift" or "lwin"
                                ? (Style)FindResource("ModifierKeyButton")
                                : (Style)FindResource("KeyButton");
                    }
                }
            }
        }
    }

    private void UpdateComboDisplay()
    {
        ComboDisplay.Text = _selectedKeys.Count > 0
            ? string.Join("+", _selectedKeys)
            : "(none)";

        RotationStepCombo.Text = _selectedKeys.Count > 0
            ? string.Join("+", _selectedKeys)
            : "(select keys above)";
    }

    private void ClearCombo_Click(object sender, RoutedEventArgs e)
    {
        _selectedKeys.Clear();
        UpdateKeyButtonStyles();
        UpdateComboDisplay();
    }

    private void ToggleCapture_Click(object sender, RoutedEventArgs e)
    {
        if (_capturing)
        {
            StopCapture();
        }
        else
        {
            StartCapture();
        }
    }

    private void StartCapture()
    {
        _capturing = true;
        CaptureBtn.Content = "⏹ Stop Capture";
        CaptureBtn.Style = (Style)FindResource("DangerButton");
        StatusText.Text = "⌨️ Capturing... press keys to build combo";
        StatusText.Foreground = FindResource("WarningBrush") as Brush;

        _keyboardHook = SetWindowsHookEx(WH_KEYBOARD_LL, KeyboardHookProc, IntPtr.Zero, 0);
    }

    private void StopCapture()
    {
        _capturing = false;
        CaptureBtn.Content = "⏺ Capture Keys";
        CaptureBtn.Style = (Style)FindResource("SurfaceButton");
        StatusText.Text = "Ready — Select mode and click Start.";
        StatusText.Foreground = FindResource("MutedBrush") as Brush;

        if (_keyboardHook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_keyboardHook);
            _keyboardHook = IntPtr.Zero;
        }
    }

    // ── Hold Mode ─────────────────────────────────────────────────────────────

    private void HoldMode_Changed(object sender, RoutedEventArgs e)
    {
        if (TimedPanel == null || RotationPanel == null) return;

        TimedPanel.Visibility = ModeTimed.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        RotationPanel.Visibility = ModeRotation.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
    }

    private void AddRotationStep_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedKeys.Count == 0)
        {
            MessageBox.Show("Click keys on the keyboard first!", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (double.TryParse(DurationBox.Text, out double dur))
        {
            var combo = string.Join("+", _selectedKeys);
            RotationList.Items.Add($"{combo}  ({dur}s)");
        }
    }

    private void RemoveRotationStep_Click(object sender, RoutedEventArgs e)
    {
        if (RotationList.SelectedIndex >= 0)
        {
            RotationList.Items.RemoveAt(RotationList.SelectedIndex);
        }
    }

    // ── Click Mode ────────────────────────────────────────────────────────────

    private void ClickMode_Changed(object sender, RoutedEventArgs e)
    {
        if (SimpleClickCard == null || RecordCard == null) return;

        SimpleClickCard.Visibility = ModeSimple.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        RecordCard.Visibility = ModeRecord.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ClickCount_Changed(object sender, RoutedEventArgs e)
    {
        if (ClickCountBox != null)
            ClickCountBox.IsEnabled = CountLimited.IsChecked == true;
    }

    // ── Recording ─────────────────────────────────────────────────────────────

    private void ToggleRecord_Click(object sender, RoutedEventArgs e)
    {
        if (_recording)
            StopRecording();
        else
            StartRecording();
    }

    private void StartRecording()
    {
        _recording = true;
        _recordedActions.Clear();
        _recordingStartTime = DateTime.UtcNow;
        RecordBtn.Content = "⏹  Stop Recording";
        RecordBtn.Style = (Style)FindResource("AccentButton");
        RecordStatus.Text = "Recording... perform your clicks now";

        Task.Run(RecordWorker);
    }

    private void StopRecording()
    {
        _recording = false;
        RecordBtn.Content = "⏺  Start Recording";
        RecordBtn.Style = (Style)FindResource("DangerButton");
        RecordStatus.Text = $"{_recordedActions.Count} actions recorded";
    }

    private void ClearRecord_Click(object sender, RoutedEventArgs e)
    {
        _recording = false;
        _recordedActions.Clear();
        RecordBtn.Content = "⏺  Start Recording";
        RecordBtn.Style = (Style)FindResource("DangerButton");
        RecordStatus.Text = "No actions recorded";
    }

    private void RecordWorker()
    {
        while (_recording)
        {
            // Check for mouse clicks
            if ((GetAsyncKeyState(VK_LBUTTON) & 0x8000) != 0)
            {
                GetCursorPos(out var pt);
                _recordedActions.Add(new RecordedAction("mouse_click", "left", pt.X, pt.Y, DateTime.UtcNow - _recordingStartTime));
                Dispatcher.Invoke(() => RecordStatus.Text = $"Recording... {_recordedActions.Count} actions captured");
                Thread.Sleep(100); // Debounce
            }
            else if ((GetAsyncKeyState(VK_RBUTTON) & 0x8000) != 0)
            {
                GetCursorPos(out var pt);
                _recordedActions.Add(new RecordedAction("mouse_click", "right", pt.X, pt.Y, DateTime.UtcNow - _recordingStartTime));
                Dispatcher.Invoke(() => RecordStatus.Text = $"Recording... {_recordedActions.Count} actions captured");
                Thread.Sleep(100);
            }

            Thread.Sleep(20);
        }
    }

    // ── Action Button ─────────────────────────────────────────────────────────

    private void ActionBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_running)
        {
            StopAction();
            return;
        }

        if (_capturing)
            StopCapture();

        int delay = int.TryParse(DelayBox.Text, out int d) ? d : 3;
        _countdownRemaining = delay;
        _running = true;
        _cts = new CancellationTokenSource();

        ActionBtn.Content = "⛔  Abort   (Esc)";
        ActionBtn.Style = (Style)FindResource("DangerButton");

        _countdownTimer.Start();
    }

    private void CountdownTimer_Tick(object? sender, EventArgs e)
    {
        _countdownRemaining--;

        if (_countdownRemaining <= 0)
        {
            _countdownTimer.Stop();
            StatusText.Text = "🚀 Running...";
            StatusText.Foreground = FindResource("SuccessBrush") as Brush;
            ProgressBar.Value = 100;

            // Start the actual task
            var tabIdx = MainTabControl.SelectedIndex;
            switch (tabIdx)
            {
                case 0: StartTyper(); break;
                case 1: StartHolder(); break;
                case 2: StartClicker(); break;
            }
        }
        else
        {
            StatusText.Text = $"⏳ Switch focus to VNC… {_countdownRemaining}s";
            StatusText.Foreground = FindResource("WarningBrush") as Brush;
            ProgressBar.Value = (3 - _countdownRemaining) / 3.0 * 100;
        }
    }

    private void StopAction()
    {
        _running = false;
        _cts?.Cancel();
        _countdownTimer.Stop();

        Keyboard.ReleaseAllModifiers();

        StatusText.Text = "⛔ Aborted";
        StatusText.Foreground = FindResource("DangerBrush") as Brush;
        ProgressBar.Value = 0;

        ActionBtn.Content = "⏎  Start Helper   (Ctrl+Enter)";
        ActionBtn.Style = (Style)FindResource("AccentButton");
    }

    private void ResetUi()
    {
        _running = false;
        StatusText.Text = "✅ Done!";
        StatusText.Foreground = FindResource("SuccessBrush") as Brush;
        ProgressBar.Value = 0;

        Dispatcher.BeginInvoke(() =>
        {
            ActionBtn.Content = "⏎  Start Helper   (Ctrl+Enter)";
            ActionBtn.Style = (Style)FindResource("AccentButton");
        });
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }
        else
        {
            DragMove();
        }
    }

    // ── Workers ───────────────────────────────────────────────────────────────

    private void StartTyper()
    {
        var text = TypeTextBox.Text;
        if (string.IsNullOrWhiteSpace(text))
        {
            MessageBox.Show("Type some text first!", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            ResetUi();
            return;
        }

        double interval = double.TryParse(TypeIntervalBox.Text, out double i) ? i : 0.03;

        Task.Run(() =>
        {
            Keyboard.ReleaseAllModifiers();
            foreach (var c in text)
            {
                if (_cts.Token.IsCancellationRequested) break;
                Keyboard.Type(c);
                if (interval > 0) Thread.Sleep((int)(interval * 1000));
            }
            Dispatcher.Invoke(ResetUi);
        });
    }

    private void StartHolder()
    {
        if (_selectedKeys.Count == 0)
        {
            MessageBox.Show("Click keys on the keyboard first!", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            ResetUi();
            return;
        }

        var key = string.Join("+", _selectedKeys);
        double duration = ModeTimed.IsChecked == true && double.TryParse(DurationBox.Text, out double d) ? d : 0;

        if (ModeRotation.IsChecked == true)
        {
            StartRotation();
            return;
        }

        Task.Run(() =>
        {
            Keyboard.ReleaseAllModifiers();
            Keyboard.Press(key);
            StatusText.Dispatcher.Invoke(() =>
            {
                StatusText.Text = $"🔒 Holding down '{key}'...";
                StatusText.Foreground = FindResource("TextBrush") as Brush;
            });

            var start = DateTime.UtcNow;
            while (!_cts.Token.IsCancellationRequested)
            {
                if (duration > 0)
                {
                    var elapsed = (DateTime.UtcNow - start).TotalSeconds;
                    var progress = Math.Min(100, (elapsed / duration) * 100);
                    ProgressBar.Dispatcher.Invoke(() => ProgressBar.Value = progress);
                    if (elapsed >= duration) break;
                }
                else
                {
                    var pulse = (int)(DateTime.UtcNow.Ticks / 50000000) % 2 * 100;
                    ProgressBar.Dispatcher.Invoke(() => ProgressBar.Value = pulse);
                }
                Thread.Sleep(50);
            }

            Keyboard.Release(key);
            Dispatcher.Invoke(ResetUi);
        });
    }

    private void StartRotation()
    {
        // Parse steps from RotationList
        var steps = new List<(string combo, double duration)>();
        foreach (var item in RotationList.Items)
        {
            if (item is string s)
            {
                var parts = s.Split('(');
                if (parts.Length == 2)
                {
                    var combo = parts[0].Trim();
                    var durStr = parts[1].Replace("s)", "").Trim();
                    if (double.TryParse(durStr, out double dur))
                        steps.Add((combo, dur));
                }
            }
        }

        if (steps.Count == 0)
        {
            MessageBox.Show("Add rotation steps first!", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            ResetUi();
            return;
        }

        bool loop = RotationLoopCheck.IsChecked == true;

        Task.Run(() =>
        {
            Keyboard.ReleaseAllModifiers();
            int round = 0;

            while (!_cts.Token.IsCancellationRequested)
            {
                round++;
                for (int i = 0; i < steps.Count; i++)
                {
                    if (_cts.Token.IsCancellationRequested) break;

                    var (combo, dur) = steps[i];
                    StatusText.Dispatcher.Invoke(() =>
                    {
                        StatusText.Text = $"🔒 Round {round} — Holding '{combo}' ({dur}s) [{i + 1}/{steps.Count}]";
                        StatusText.Foreground = FindResource("TextBrush") as Brush;
                    });

                    Keyboard.Press(combo);
                    var start = DateTime.UtcNow;
                    while (!_cts.Token.IsCancellationRequested)
                    {
                        var elapsed = (DateTime.UtcNow - start).TotalSeconds;
                        if (elapsed >= dur) break;
                        ProgressBar.Dispatcher.Invoke(() => ProgressBar.Value = (elapsed / dur) * 100);
                        Thread.Sleep(50);
                    }
                    Keyboard.Release(combo);

                    if (!loop) break;
                    Thread.Sleep(50);
                }

                if (!loop) break;
            }

            Dispatcher.Invoke(ResetUi);
        });
    }

    private void StartClicker()
    {
        if (ModeRecord.IsChecked == true)
        {
            StartReplay();
            return;
        }

        string button = BtnLeft.IsChecked == true ? "left" : BtnRight.IsChecked == true ? "right" : "middle";
        int intervalMs = int.TryParse(ClickIntervalBox.Text, out int interval) ? interval : 100;
        int count = CountLimited.IsChecked == true && int.TryParse(ClickCountBox.Text, out int c) ? c : 0;
        string? holdKey = string.IsNullOrWhiteSpace(HoldKeyBox.Text) ? null : HoldKeyBox.Text;

        Task.Run(() =>
        {
            if (holdKey != null) Keyboard.Press(holdKey);
            int clicked = 0;

            while (!_cts.Token.IsCancellationRequested)
            {
                Mouse.Click(button);
                clicked++;

                if (count > 0)
                {
                    ProgressBar.Dispatcher.Invoke(() => ProgressBar.Value = (double)clicked / count * 100);
                    StatusText.Dispatcher.Invoke(() => StatusText.Text = $"🖱️  Clicked {clicked}/{count}");
                    if (clicked >= count) break;
                }
                else
                {
                    var pulse = (int)(DateTime.UtcNow.Ticks / 50000000) % 2 * 100;
                    ProgressBar.Dispatcher.Invoke(() => ProgressBar.Value = pulse);
                }

                if (intervalMs > 0) Thread.Sleep(intervalMs);
            }

            if (holdKey != null) Keyboard.Release(holdKey);
            Dispatcher.Invoke(ResetUi);
        });
    }

    private void StartReplay()
    {
        if (_recordedActions.Count == 0)
        {
            MessageBox.Show("Record some actions first!", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            ResetUi();
            return;
        }

        double speed = ReplaySpeedSlider.Value;
        bool loop = LoopRepeat.IsChecked == true;
        int loopCount = loop && int.TryParse(ReplayCountBox.Text, out int lc) ? lc : 1;

        Task.Run(() =>
        {
            var actions = _recordedActions.ToList();
            int total = actions.Count;
            int loops = loop ? loopCount : 1;

            for (int loopIdx = 0; loopIdx < loops; loopIdx++)
            {
                if (_cts.Token.IsCancellationRequested) break;

                for (int i = 0; i < total; i++)
                {
                    if (_cts.Token.IsCancellationRequested) break;

                    var action = actions[i];
                    if (i > 0)
                    {
                        var wait = (action.Timestamp - actions[i - 1].Timestamp).TotalSeconds / speed;
                        Thread.Sleep((int)(wait * 1000));
                    }

                    if (action.Type == "mouse_click")
                    {
                        Mouse.ClickAt(action.X, action.Y, action.Button);
                    }

                    var progress = (double)(loopIdx * total + i + 1) / (loops * total) * 100;
                    ProgressBar.Dispatcher.Invoke(() => ProgressBar.Value = progress);
                }
            }

            Dispatcher.Invoke(ResetUi);
        });
    }

    // ── Clipboard & History ───────────────────────────────────────────────────

    private void Paste_Click(object sender, RoutedEventArgs e)
    {
        if (Clipboard.ContainsText())
        {
            TypeTextBox.Text = Clipboard.GetText();
            StatusText.Text = $"Pasted {TypeTextBox.Text.Length} chars from clipboard";
            StatusText.Foreground = FindResource("SuccessBrush") as Brush;
        }
    }

    private void ClearText_Click(object sender, RoutedEventArgs e)
    {
        TypeTextBox.Clear();
        ProgressBar.Value = 0;
    }

    private void ClearHistory_Click(object sender, RoutedEventArgs e)
    {
        HistoryList.Items.Clear();
    }

    private void HistoryList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (HistoryList.SelectedItem is string text)
        {
            TypeTextBox.Text = text;
        }
    }

    private void Topmost_Changed(object sender, RoutedEventArgs e)
    {
        Topmost = TopmostCheck.IsChecked == true;
    }

    private void MinimizeBtn_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void CloseBtn_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    // ── Win32 Interop ─────────────────────────────────────────────────────────

    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int VK_LBUTTON = 0x01;
    private const int VK_RBUTTON = 0x02;

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll")]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    private IntPtr KeyboardHookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)
        {
            int vkCode = Marshal.ReadInt32(lParam);
            var key = KeyInterop.KeyFromVirtualKey(vkCode);

            Dispatcher.BeginInvoke(() =>
            {
                if (key == Key.Escape)
                {
                    StopCapture();
                }
                else
                {
                    var name = key.ToString().ToLower();
                    ToggleKey(name);
                    UpdateKeyButtonStyles();
                    UpdateComboDisplay();
                }
            });
        }
        return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
    }

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
}

// ── Data Types ───────────────────────────────────────────────────────────────

internal record RecordedAction(string Type, string Button, int X, int Y, TimeSpan Timestamp);
