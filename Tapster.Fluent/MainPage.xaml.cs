using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using Tapster;

namespace Tapster_Fluent;

public sealed partial class MainPage : Page
{
    private string _activeTab = "Typer";
    private bool _isRunning = false;
    private CancellationTokenSource? _cts;

    private readonly MacroRecorder _macroRecorder = new();
    private bool _isRecordingMacro = false;
    private bool _isCapturingKey = false;

    public MainPage()
    {
        InitializeComponent();
        Loaded += MainPage_Loaded;
    }

    private void MainPage_Loaded(object sender, RoutedEventArgs e)
    {
        NavView.SelectedItem = NavView.MenuItems[0];

        // Fix IEEE 754 floating-point precision display
        var fmt2 = new Windows.Globalization.NumberFormatting.DecimalFormatter
        {
            IntegerDigits = 1,
            FractionDigits = 2
        };
        var fmtInt = new Windows.Globalization.NumberFormatting.DecimalFormatter
        {
            IntegerDigits = 1,
            FractionDigits = 0
        };

        TypeIntervalBox.NumberFormatter = fmt2;
        ClickIntervalBox.NumberFormatter = fmt2;
        HoldDurationBox.NumberFormatter = fmtInt;
        ClickCountBox.NumberFormatter = fmtInt;
        MacroRepeatBox.NumberFormatter = fmtInt;
        MacroSpeedBox.NumberFormatter = fmt2;

        TyperDelayBox.NumberFormatter = fmtInt;
        HolderDelayBox.NumberFormatter = fmtInt;
        ClickerDelayBox.NumberFormatter = fmtInt;
        MacroDelayBox.NumberFormatter = fmtInt;

        // Force refresh displayed text
        TypeIntervalBox.Value = 0.03;
        ClickIntervalBox.Value = 0.1;
        HoldDurationBox.Value = 10;
        ClickCountBox.Value = 100;
        MacroRepeatBox.Value = 1;
        MacroSpeedBox.Value = 1.0;

        TyperDelayBox.Value = 3;
        HolderDelayBox.Value = 3;
        ClickerDelayBox.Value = 3;
        MacroDelayBox.Value = 3;

        GenerateVirtualKeyboard();

        // Load Settings
        StartOnBootToggle.IsOn = AppSettings.Current.StartOnBoot;
        StartMinimizedToggle.IsOn = AppSettings.Current.StartMinimizedToTray;
        MinimizeOnCloseToggle.IsOn = AppSettings.Current.MinimizeToTrayOnClose;
    }

    private void GenerateVirtualKeyboard()
    {
        KeyboardContainer.Children.Clear();

        // ── Row 0: Function Keys ──
        var row0 = CreateKeyboardRow();
        AddKeyBtn(row0, "esc", "Esc", width: 44, isAccent: true);
        AddSpacer(row0, 16);
        AddKeyBtn(row0, "f1", "F1"); AddKeyBtn(row0, "f2", "F2"); AddKeyBtn(row0, "f3", "F3"); AddKeyBtn(row0, "f4", "F4");
        AddSpacer(row0, 12);
        AddKeyBtn(row0, "f5", "F5"); AddKeyBtn(row0, "f6", "F6"); AddKeyBtn(row0, "f7", "F7"); AddKeyBtn(row0, "f8", "F8");
        AddSpacer(row0, 12);
        AddKeyBtn(row0, "f9", "F9"); AddKeyBtn(row0, "f10", "F10"); AddKeyBtn(row0, "f11", "F11"); AddKeyBtn(row0, "f12", "F12");
        KeyboardContainer.Children.Add(row0);

        // ── Row 1: Number Row ──
        var row1 = CreateKeyboardRow();
        string[] r1Keys = { "`", "1", "2", "3", "4", "5", "6", "7", "8", "9", "0", "-", "=" };
        foreach (var k in r1Keys) AddKeyBtn(row1, k, k);
        AddKeyBtn(row1, "backspace", "Backspace", width: 76);
        KeyboardContainer.Children.Add(row1);

        // ── Row 2: QWERTY Row ──
        var row2 = CreateKeyboardRow();
        AddKeyBtn(row2, "tab", "Tab", width: 54);
        string[] r2Keys = { "q", "w", "e", "r", "t", "y", "u", "i", "o", "p", "[", "]", "\\" };
        foreach (var k in r2Keys) AddKeyBtn(row2, k, k.ToUpper());
        KeyboardContainer.Children.Add(row2);

        // ── Row 3: Home Row ──
        var row3 = CreateKeyboardRow();
        AddKeyBtn(row3, "capslock", "Caps Lock", width: 66);
        string[] r3Keys = { "a", "s", "d", "f", "g", "h", "j", "k", "l", ";", "'" };
        foreach (var k in r3Keys) AddKeyBtn(row3, k, k.ToUpper());
        AddKeyBtn(row3, "enter", "Enter", width: 80, isAccent: true);
        KeyboardContainer.Children.Add(row3);

        // ── Row 4: Shift Row ──
        var row4 = CreateKeyboardRow();
        AddKeyBtn(row4, "shift", "Shift", width: 88);
        string[] r4Keys = { "z", "x", "c", "v", "b", "n", "m", ",", ".", "/" };
        foreach (var k in r4Keys) AddKeyBtn(row4, k, k.ToUpper());
        AddKeyBtn(row4, "shift", "Shift", width: 92);
        KeyboardContainer.Children.Add(row4);

        // ── Row 5: Control / Space / Navigation Row ──
        var row5 = CreateKeyboardRow();
        AddKeyBtn(row5, "ctrl", "Ctrl", width: 48);
        AddKeyBtn(row5, "win", "Win", width: 44);
        AddKeyBtn(row5, "alt", "Alt", width: 44);
        AddKeyBtn(row5, "space", "Space", width: 240);
        AddKeyBtn(row5, "alt", "Alt", width: 44);
        AddKeyBtn(row5, "win", "Win", width: 44);
        AddKeyBtn(row5, "ctrl", "Ctrl", width: 48);
        AddSpacer(row5, 12);
        AddKeyBtn(row5, "left", "◄", width: 36);
        AddKeyBtn(row5, "up", "▲", width: 36);
        AddKeyBtn(row5, "down", "▼", width: 36);
        AddKeyBtn(row5, "right", "►", width: 36);
        KeyboardContainer.Children.Add(row5);
    }

    private static StackPanel CreateKeyboardRow()
    {
        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4
        };
    }

    private static void AddSpacer(StackPanel row, double width)
    {
        row.Children.Add(new Border { Width = width });
    }

    private void AddKeyBtn(StackPanel row, string keyId, string label, double width = 36, bool isAccent = false)
    {
        var btn = new Button
        {
            Content = label,
            Width = width,
            Height = 34,
            Padding = new Thickness(2),
            FontSize = width > 50 ? 10 : 11,
            FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Cascadia Code, Consolas"),
            CornerRadius = new CornerRadius(4),
            Style = Application.Current.Resources[isAccent ? "AccentButtonStyle" : "DefaultButtonStyle"] as Style
        };

        btn.Click += (s, e) =>
        {
            string current = HolderKeyBox.Text.Trim();
            if (string.IsNullOrEmpty(current) || current == "w")
            {
                HolderKeyBox.Text = keyId;
            }
            else if (!current.Split('+').Contains(keyId))
            {
                HolderKeyBox.Text = $"{current}+{keyId}";
            }
        };

        row.Children.Add(btn);
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.IsSettingsSelected)
        {
            _activeTab = "Settings";
            TyperPanel.Visibility = Visibility.Collapsed;
            HolderPanel.Visibility = Visibility.Collapsed;
            ClickerPanel.Visibility = Visibility.Collapsed;
            MacroPanel.Visibility = Visibility.Collapsed;
            AboutPanel.Visibility = Visibility.Collapsed;
            SettingsPanel.Visibility = Visibility.Visible;
            return;
        }

        if (args.SelectedItem is NavigationViewItem item && item.Tag is string tag)
        {
            _activeTab = tag;
            TyperPanel.Visibility = tag == "Typer" ? Visibility.Visible : Visibility.Collapsed;
            HolderPanel.Visibility = tag == "Holder" ? Visibility.Visible : Visibility.Collapsed;
            ClickerPanel.Visibility = tag == "Clicker" ? Visibility.Visible : Visibility.Collapsed;
            MacroPanel.Visibility = tag == "Macro" ? Visibility.Visible : Visibility.Collapsed;
            SettingsPanel.Visibility = Visibility.Collapsed;
            AboutPanel.Visibility = tag == "About" ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void NavigateToAbout_Click(object sender, RoutedEventArgs e)
    {
        _activeTab = "About";
        TyperPanel.Visibility = Visibility.Collapsed;
        HolderPanel.Visibility = Visibility.Collapsed;
        ClickerPanel.Visibility = Visibility.Collapsed;
        MacroPanel.Visibility = Visibility.Collapsed;
        SettingsPanel.Visibility = Visibility.Collapsed;
        AboutPanel.Visibility = Visibility.Visible;

        if (NavView.FooterMenuItems.Count > 0)
        {
            NavView.SelectedItem = NavView.FooterMenuItems[0];
        }
    }

    private void OpenSettingsFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string folder = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Tapster");
            if (!System.IO.Directory.Exists(folder))
            {
                System.IO.Directory.CreateDirectory(folder);
            }
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = folder,
                UseShellExecute = true
            });
        }
        catch { }
    }

    private void StartOnBootToggle_Toggled(object sender, RoutedEventArgs e)
    {
        AppSettings.Current.StartOnBoot = StartOnBootToggle.IsOn;
    }

    private void StartMinimizedToggle_Toggled(object sender, RoutedEventArgs e)
    {
        AppSettings.Current.StartMinimizedToTray = StartMinimizedToggle.IsOn;
        AppSettings.Current.Save();
    }

    private void MinimizeOnCloseToggle_Toggled(object sender, RoutedEventArgs e)
    {
        AppSettings.Current.MinimizeToTrayOnClose = MinimizeOnCloseToggle.IsOn;
        AppSettings.Current.Save();
    }

    private async void Paste_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dataPackageView = Clipboard.GetContent();
            if (dataPackageView.Contains(StandardDataFormats.Text))
            {
                string text = await dataPackageView.GetTextAsync();
                TypeTextBox.Text = text;
            }
        }
        catch (Exception ex)
        {
            TyperStatusText.Text = $"Paste error: {ex.Message}";
        }
    }

    private void ClearText_Click(object sender, RoutedEventArgs e)
    {
        TypeTextBox.Text = "";
    }

    private void ClearHolderKey_Click(object sender, RoutedEventArgs e)
    {
        HolderKeyBox.Text = "";
    }

    private async void CaptureKeyBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_isCapturingKey)
        {
            _isCapturingKey = false;
            CaptureIcon.Glyph = "\uE7C8";
            CaptureKeyText.Text = "Capture Key";
            HolderStatusText.Text = "Key capture canceled";
            return;
        }

        _isCapturingKey = true;
        CaptureIcon.Glyph = "\uE71A";
        CaptureKeyText.Text = "Listening...";
        HolderStatusText.Text = "Press any key on your keyboard...";

        await Task.Run(async () =>
        {
            // Give 200ms buffer so clicking the button itself is not captured
            await Task.Delay(200);

            while (_isCapturingKey)
            {
                await Task.Delay(15);
                for (int vk = 0x08; vk <= 0xFE; vk++)
                {
                    short state = NativeMethods.GetAsyncKeyState(vk);
                    if ((state & 0x8000) != 0 || state < 0)
                    {
                        string k = Keyboard.GetKeyName(vk);
                        DispatcherQueue.TryEnqueue(() =>
                        {
                            HolderKeyBox.Text = k;
                            _isCapturingKey = false;
                            CaptureIcon.Glyph = "\uE7C8";
                            CaptureKeyText.Text = "Capture Key";
                            HolderStatusText.Text = $"Captured key: {k}";
                        });
                        return;
                    }
                }
            }
        });
    }

    private async void PickCoordBtn_Click(object sender, RoutedEventArgs e)
    {
        ClickerStatusText.Text = "Move cursor to target location in 3s...";
        for (int i = 3; i > 0; i--)
        {
            ClickerStatusText.Text = $"Locking coordinates in {i}s... Move mouse to target!";
            await Task.Delay(1000);
        }

        var (x, y) = Mouse.GetPosition();
        ClickXBox.Value = x;
        ClickYBox.Value = y;
        ClickerStatusText.Text = $"Locked target coordinates: ({x}, {y})";
    }

    private void RecordMacroBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_isRecordingMacro)
        {
            _isRecordingMacro = false;
            _macroRecorder.StopRecording();
            RecordIcon.Glyph = "\uE7C8";
            RecordMacroText.Text = "Start Recording";
            MacroStatusText.Text = $"Macro recorded: {_macroRecorder.Actions.Count} actions";
            RefreshMacroActionList();
        }
        else
        {
            _isRecordingMacro = true;
            _macroRecorder.StartRecording(count =>
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    MacroStatusText.Text = $"Recording macro... {count} actions captured (Click Stop to finish)";
                });
            });
            RecordIcon.Glyph = "\uE71A";
            RecordMacroText.Text = "Stop Recording";
            MacroStatusText.Text = "Recording macro... Click or type anywhere to record actions!";
        }
    }

    private void ClearMacroBtn_Click(object sender, RoutedEventArgs e)
    {
        _isRecordingMacro = false;
        RecordIcon.Glyph = "\uE7C8";
        RecordMacroText.Text = "Start Recording";
        _macroRecorder.Clear();
        MacroActionList.Items.Clear();
        MacroStatusText.Text = "Macro cleared";
    }

    private void RefreshMacroActionList()
    {
        MacroActionList.Items.Clear();
        var actions = _macroRecorder.Actions;
        foreach (var act in actions)
        {
            string detail = act.Type switch
            {
                MacroActionType.ClickLeft => $"🖱️ Left Click at ({act.X}, {act.Y})",
                MacroActionType.ClickRight => $"🖱️ Right Click at ({act.X}, {act.Y})",
                MacroActionType.ClickMiddle => $"🖱️ Middle Click at ({act.X}, {act.Y})",
                MacroActionType.KeyPress => $"⌨️ Key Down [{act.Data}]",
                MacroActionType.KeyRelease => $"⌨️ Key Up [{act.Data}]",
                _ => $"🔤 Type [{act.Data}]"
            };
            MacroActionList.Items.Add($"+{act.DelayMs}ms — {detail}");
        }
    }

    // ══════════════════════════════════════════════════════════
    // Per-Panel Action Handlers (Plan A: Self-Contained Execution)
    // ══════════════════════════════════════════════════════════

    private string? _runningTaskName = null;

    private async void TyperActionBtn_Click(object sender, RoutedEventArgs e)
    {
        await RunTaskAsync(
            "Typer",
            TyperDelayBox,
            TyperStatusText,
            TyperProgressBar,
            TyperActionBtn,
            TyperActionIcon,
            TyperActionText,
            "Start Typer",
            RunAutoTyperAsync);
    }

    private async void HolderActionBtn_Click(object sender, RoutedEventArgs e)
    {
        await RunTaskAsync(
            "Holder",
            HolderDelayBox,
            HolderStatusText,
            HolderProgressBar,
            HolderActionBtn,
            HolderActionIcon,
            HolderActionText,
            "Start Holder",
            RunKeyHolderAsync);
    }

    private async void ClickerActionBtn_Click(object sender, RoutedEventArgs e)
    {
        await RunTaskAsync(
            "Clicker",
            ClickerDelayBox,
            ClickerStatusText,
            ClickerProgressBar,
            ClickerActionBtn,
            ClickerActionIcon,
            ClickerActionText,
            "Start Clicker",
            RunAutoClickerAsync);
    }

    private async void MacroActionBtn_Click(object sender, RoutedEventArgs e)
    {
        await RunTaskAsync(
            "Macro",
            MacroDelayBox,
            MacroStatusText,
            MacroProgressBar,
            MacroActionBtn,
            MacroActionIcon,
            MacroActionText,
            "Replay Macro",
            RunMacroReplayAsync);
    }

    private async Task RunTaskAsync(
        string taskName,
        NumberBox delayBox,
        TextBlock statusText,
        ProgressBar progressBar,
        Button actionBtn,
        FontIcon actionIcon,
        TextBlock actionText,
        string defaultActionTitle,
        Func<CancellationToken, Action<string, double>, Task> taskFunc)
    {
        if (_isRunning)
        {
            if (_runningTaskName == taskName)
            {
                StopTask("Stopped by user");
            }
            else
            {
                StopTask($"Switched task from {_runningTaskName}");
            }
            return;
        }

        _isRunning = true;
        _runningTaskName = taskName;
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        actionText.Text = "Stop";
        actionIcon.Glyph = "\uE71A";

        double delay = delayBox.Value;
        if (double.IsNaN(delay) || delay < 0) delay = 0;

        try
        {
            // Countdown phase
            for (int remaining = (int)delay; remaining > 0; remaining--)
            {
                token.ThrowIfCancellationRequested();
                CheckEmergencyEsc();
                statusText.Text = $"Starting in {remaining}s... Switch to target app!";
                progressBar.Value = (delay - remaining) / delay * 100;
                await Task.Delay(1000, token);
            }

            progressBar.Value = 100;
            statusText.Text = "Running...";

            // Progress callback for live updates
            Action<string, double> reportProgress = (msg, pct) =>
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    statusText.Text = msg;
                    if (pct >= 0) progressBar.Value = Math.Min(100, Math.Max(0, pct));
                });
            };

            await taskFunc(token, reportProgress);

            StopTask("Finished successfully");
        }
        catch (OperationCanceledException ex)
        {
            StopTask(ex.Message.Contains("Esc") ? "Stopped via Esc key" : "Task canceled");
        }
        catch (Exception ex)
        {
            StopTask($"Error: {ex.Message}");
        }
    }

    private async Task RunAutoTyperAsync(CancellationToken token, Action<string, double> reportProgress)
    {
        string text = TypeTextBox.Text;
        if (string.IsNullOrEmpty(text))
        {
            throw new InvalidOperationException("No text to type!");
        }

        double interval = TypeIntervalBox.Value;
        int intervalMs = (int)(interval * 1000);

        await Task.Run(() =>
        {
            for (int i = 0; i < text.Length; i++)
            {
                token.ThrowIfCancellationRequested();
                CheckEmergencyEsc();
                Keyboard.Type(text[i]);

                int charIndex = i + 1;
                reportProgress($"Typing character {charIndex}/{text.Length}...", (double)charIndex / text.Length * 100);

                if (intervalMs > 0)
                {
                    Thread.Sleep(intervalMs);
                }
            }
        }, token);

        DispatcherQueue.TryEnqueue(() =>
        {
            string summary = $"{DateTime.Now:HH:mm:ss} - Typed {text.Length} chars";
            HistoryList.Items.Insert(0, summary);
        });
    }

    private async Task RunKeyHolderAsync(CancellationToken token, Action<string, double> reportProgress)
    {
        string keys = HolderKeyBox.Text.Trim();
        if (string.IsNullOrEmpty(keys))
        {
            throw new InvalidOperationException("No key specified!");
        }

        double durationSec = HoldDurationBox.Value;
        int durationMs = (int)(durationSec * 1000);

        await Task.Run(async () =>
        {
            try
            {
                Keyboard.Press(keys);
                int elapsedMs = 0;
                int checkIntervalMs = 100;

                while (!token.IsCancellationRequested && (durationMs <= 0 || elapsedMs < durationMs))
                {
                    CheckEmergencyEsc();

                    if (durationMs > 0)
                    {
                        double pct = (double)elapsedMs / durationMs * 100;
                        reportProgress($"Holding [{keys}] ({elapsedMs / 1000}s / {durationSec}s)...", pct);
                    }
                    else
                    {
                        reportProgress($"Holding [{keys}] ({elapsedMs / 1000}s / Infinite)...", 100);
                    }

                    await Task.Delay(checkIntervalMs, token);
                    elapsedMs += checkIntervalMs;
                }
            }
            finally
            {
                Keyboard.Release(keys);
                Keyboard.ReleaseAllModifiers();
            }
        }, token);
    }

    private async Task RunAutoClickerAsync(CancellationToken token, Action<string, double> reportProgress)
    {
        int buttonIndex = MouseButtonCombo.SelectedIndex;
        string button = buttonIndex switch
        {
            1 => "right",
            2 => "middle",
            _ => "left"
        };

        double intervalSec = ClickIntervalBox.Value;
        int intervalMs = Math.Max(5, (int)(intervalSec * 1000));
        int totalClicks = (int)ClickCountBox.Value;
        bool infinite = totalClicks <= 0;

        int? targetX = double.IsNaN(ClickXBox.Value) ? null : (int)ClickXBox.Value;
        int? targetY = double.IsNaN(ClickYBox.Value) ? null : (int)ClickYBox.Value;

        await Task.Run(() =>
        {
            int count = 0;
            while (!token.IsCancellationRequested && (infinite || count < totalClicks))
            {
                CheckEmergencyEsc();
                if (targetX.HasValue && targetY.HasValue)
                {
                    Mouse.ClickAt(targetX.Value, targetY.Value, button);
                }
                else
                {
                    Mouse.Click(button);
                }
                count++;

                if (!infinite)
                {
                    reportProgress($"Clicking {count}/{totalClicks}...", (double)count / totalClicks * 100);
                }
                else
                {
                    reportProgress($"Clicking count: {count} (Infinite)...", 100);
                }

                if (intervalMs > 0)
                {
                    Thread.Sleep(intervalMs);
                }
            }
        }, token);
    }

    private async Task RunMacroReplayAsync(CancellationToken token, Action<string, double> reportProgress)
    {
        if (_macroRecorder.Actions.Count == 0)
        {
            throw new InvalidOperationException("No recorded macro actions to replay! Please record first.");
        }

        int loops = (int)MacroRepeatBox.Value;
        double speed = MacroSpeedBox.Value;

        await _macroRecorder.ReplayAsync(loops, speed, token, (loop, step) =>
        {
            double pct = (double)step / _macroRecorder.Actions.Count * 100;
            reportProgress($"Replaying Macro: Loop {loop}, Action {step}/{_macroRecorder.Actions.Count}", pct);
        });
    }

    private static void CheckEmergencyEsc()
    {
        if (Keyboard.IsEscPressed())
        {
            throw new OperationCanceledException("Emergency stop triggered via Esc key");
        }
    }

    private void StopTask(string message)
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;

        _isRunning = false;

        // Reset Typer UI
        TyperActionText.Text = "Start Typer";
        TyperActionIcon.Glyph = "\uE768";
        TyperProgressBar.Value = 0;

        // Reset Holder UI
        HolderActionText.Text = "Start Holder";
        HolderActionIcon.Glyph = "\uE768";
        HolderProgressBar.Value = 0;

        // Reset Clicker UI
        ClickerActionText.Text = "Start Clicker";
        ClickerActionIcon.Glyph = "\uE768";
        ClickerProgressBar.Value = 0;

        // Reset Macro UI
        MacroActionText.Text = "Replay Macro";
        MacroActionIcon.Glyph = "\uE768";
        MacroProgressBar.Value = 0;

        // Set status message on the active/relevant panel
        if (_runningTaskName == "Typer") TyperStatusText.Text = message;
        else if (_runningTaskName == "Holder") HolderStatusText.Text = message;
        else if (_runningTaskName == "Clicker") ClickerStatusText.Text = message;
        else if (_runningTaskName == "Macro") MacroStatusText.Text = message;

        _runningTaskName = null;
        Keyboard.ReleaseAllModifiers();
    }
}
