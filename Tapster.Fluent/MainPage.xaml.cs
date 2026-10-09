using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using Tapster;

namespace Tapster_Fluent;

public sealed partial class MainPage : Page
{
    private const string KeyEnter = "enter";
    private const string KeyTab = "tab";
    private const string KeyNone = "none";
    private const string KeyPrintScreen = "printscreen";
    private const string TyperModeClipboard = "clipboard";
    private const string TyperModeKeystroke = "keystroke";
    private const string TabClicker = "Clicker";
    private const string TabHolder = "Holder";
    private const string TabMacro = "Macro";
    private const string TabTyper = "Typer";
    private const string TabSettings = "Settings";
    private const string TabAbout = "About";

    public static MainPage? Instance { get; private set; }
    private string _activeTab = TabTyper;
    private bool _isRunning = false;
    private CancellationTokenSource? _cts;

    private readonly MacroRecorder _macroRecorder = new();
    private bool _isRecordingMacro = false;
    private bool _isCapturingKey = false;
    private readonly TargetMarkerOverlay _targetMarkerOverlay = new();
    private readonly PanicDetector _panicDetector = new();
    private readonly Dictionary<string, List<Button>> _keyboardButtons = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<KeyHolderStage> _holderStages = new();
    private bool _isUpdatingHolderStage = false;

    public MainPage()
    {
        Instance = this;
        InitializeComponent();
        Loaded += MainPage_Loaded;
    }

    private void MainPage_Loaded(object sender, RoutedEventArgs e)
    {
        NavView.SelectedItem = NavView.MenuItems[0];

        // Fix IEEE 754 floating-point precision display
        var fmt1 = new Windows.Globalization.NumberFormatting.DecimalFormatter
        {
            IntegerDigits = 1,
            FractionDigits = 1
        };
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
        HoldDurationBox.NumberFormatter = fmt1;
        HolderRestBox.NumberFormatter = fmt1;
        HolderRepeatBox.NumberFormatter = fmtInt;
        ClickCountBox.NumberFormatter = fmtInt;
        TimeJitterBox.NumberFormatter = fmtInt;
        LocationJitterBox.NumberFormatter = fmtInt;
        MacroRepeatBox.NumberFormatter = fmtInt;
        MacroSpeedBox.NumberFormatter = fmt2;

        TyperDelayBox.NumberFormatter = fmtInt;
        HolderDelayBox.NumberFormatter = fmtInt;
        ClickerDelayBox.NumberFormatter = fmtInt;
        MacroDelayBox.NumberFormatter = fmtInt;

        // Force refresh displayed text
        TypeIntervalBox.Value = 0.03;
        ClickIntervalBox.Value = 0.1;
        HoldDurationBox.Value = 5.0;
        HolderRestBox.Value = 0.5;
        HolderRepeatBox.Value = 1;
        ClickCountBox.Value = 100;
        MacroRepeatBox.Value = 1;
        MacroSpeedBox.Value = 1.0;

        TyperDelayBox.Value = 3;
        HolderDelayBox.Value = 3;
        ClickerDelayBox.Value = 3;
        MacroDelayBox.Value = 3;

        // Restore Clicker, Key Spammer and Jitter Preferences
        ClickTriggerModeCombo.SelectedIndex = AppSettings.Current.ClickerHoldMode ? 1 : 0;
        ClickTargetTypeCombo.SelectedIndex = AppSettings.Current.ClickerIsKeySpammer ? 1 : 0;
        SpamKeyBox.Text = string.IsNullOrEmpty(AppSettings.Current.ClickerSpamKey) ? "space" : AppSettings.Current.ClickerSpamKey;
        SpamKeyBox.TextChanged += (_, _) =>
        {
            AppSettings.Current.ClickerSpamKey = SpamKeyBox.Text;
            AppSettings.Current.Save();
            UpdateClickerActionBtnState();
        };

        TimeJitterCheck.IsChecked = AppSettings.Current.ClickerJitterEnabled;
        TimeJitterBox.IsEnabled = AppSettings.Current.ClickerJitterEnabled;
        TimeJitterBox.Value = AppSettings.Current.ClickerTimeJitterPercent;
        LocationJitterBox.Value = AppSettings.Current.ClickerLocationJitterPx;

        TimeJitterCheck.Checked += (_, _) =>
        {
            TimeJitterBox.IsEnabled = true;
            AppSettings.Current.ClickerJitterEnabled = true;
            AppSettings.Current.Save();
        };
        TimeJitterCheck.Unchecked += (_, _) =>
        {
            TimeJitterBox.IsEnabled = false;
            AppSettings.Current.ClickerJitterEnabled = false;
            AppSettings.Current.Save();
        };
        TimeJitterBox.ValueChanged += (_, _) =>
        {
            AppSettings.Current.ClickerTimeJitterPercent = TimeJitterBox.Value;
            AppSettings.Current.Save();
        };
        LocationJitterBox.ValueChanged += (_, _) =>
        {
            AppSettings.Current.ClickerLocationJitterPx = LocationJitterBox.Value;
            AppSettings.Current.Save();
        };

        ShowTargetMarkerCheck.IsChecked = AppSettings.Current.ClickerShowTargetMarker;
        ShowTargetMarkerCheck.Checked += (_, _) => OnTargetMarkerSettingsChanged();
        ShowTargetMarkerCheck.Unchecked += (_, _) => OnTargetMarkerSettingsChanged();
        ClickXBox.ValueChanged += (_, _) =>
        {
            OnTargetMarkerSettingsChanged();
            UpdateClearCoordsBtnState();
        };
        ClickYBox.ValueChanged += (_, _) =>
        {
            OnTargetMarkerSettingsChanged();
            UpdateClearCoordsBtnState();
        };
        LocationJitterBox.ValueChanged += (_, _) => OnTargetMarkerSettingsChanged();
        Unloaded += (_, _) => _targetMarkerOverlay.Dispose();
        UpdateTargetMarkerOverlay();

        // Restore Typer Preferences
        TypeModeCombo.SelectedIndex = AppSettings.Current.TyperInputMode == TyperModeClipboard ? 1 : 0;
        TypeTrailingKeyCombo.SelectedIndex = AppSettings.Current.TyperTrailingKey switch
        {
            KeyEnter => 1,
            KeyTab => 2,
            _ => 0
        };
        TypeJitterCheck.IsChecked = AppSettings.Current.TyperJitterEnabled;

        TypeModeCombo.SelectionChanged += (_, _) => UpdateTyperMode(this);
        TypeTrailingKeyCombo.SelectionChanged += (_, _) =>
        {
            AppSettings.Current.TyperTrailingKey = TypeTrailingKeyCombo.SelectedIndex switch
            {
                1 => KeyEnter,
                2 => KeyTab,
                _ => KeyNone
            };
            AppSettings.Current.Save();
        };
        TypeJitterCheck.Checked += (_, _) =>
        {
            AppSettings.Current.TyperJitterEnabled = true;
            AppSettings.Current.Save();
        };
        TypeJitterCheck.Unchecked += (_, _) =>
        {
            AppSettings.Current.TyperJitterEnabled = false;
            AppSettings.Current.Save();
        };
        UpdateTyperMode(this);

        ClickTargetTypeCombo.SelectionChanged += (_, _) =>
        {
            UpdateClickerTargetType(this);
            UpdateClickerActionBtnState();
        };
        ClickTriggerModeCombo.SelectionChanged += (_, _) => UpdateClickerTriggerMode(this);
        UpdateClickerTargetType(this);
        UpdateClickerTriggerMode(this);

        TypeTextBox.TextChanged += (_, _) => UpdateTyperActionBtnState();
        HolderKeyBox.TextChanged += (_, _) =>
        {
            SyncSelectedHolderStage();
            UpdateHolderActionBtnState();
        };
        HoldDurationBox.ValueChanged += (_, _) => SyncSelectedHolderStage();
        HolderRestBox.ValueChanged += (_, _) => SyncSelectedHolderStage();
        MacroActionList.SelectionChanged += (_, _) => UpdateMacroItemActionBtns();

        _holderStages.Add(new KeyHolderStage("w", 0, 0));
        RefreshHolderStageList();
        HolderStageList.SelectedIndex = 0;

        UpdateAllActionBtnStates();

        GenerateVirtualKeyboard();
        HolderScrollViewer.SizeChanged += (_, _) => UpdateKeyboardContainerWidth(this);
        UpdateKeyboardContainerWidth(this);

        // Load Settings
        StartOnBootToggle.IsOn = AppSettings.Current.StartOnBoot;
        StartMinimizedToggle.IsOn = AppSettings.Current.StartMinimizedToTray;
        MinimizeOnCloseToggle.IsOn = AppSettings.Current.MinimizeToTrayOnClose;
        if (MainWindow.Instance != null)
        {
            AlwaysOnTopToggle.IsOn = MainWindow.Instance.IsAlwaysOnTop;
            MainWindow.Instance.AlwaysOnTopChanged += OnMainWindowAlwaysOnTopChanged;
        }
    }

    private static void UpdateKeyboardContainerWidth(MainPage page)
    {
        if (page.KeyboardContainer == null || page.HolderScrollViewer == null) return;
        double availableWidth = page.HolderScrollViewer.ActualWidth - 36;
        page.KeyboardContainer.Width = Math.Max(804, availableWidth);
    }

    private void GenerateVirtualKeyboard()
    {
        KeyboardContainer.Children.Clear();
        _keyboardButtons.Clear();

        // ── Main Keyboard Panel (Rows 0 to 5) ──
        var mainKeyboardPanel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 4,
            HorizontalAlignment = HorizontalAlignment.Left
        };

        // Row 0: Function Keys
        var row0 = CreateKeyboardRow();
        AddKeyBtn(row0, "esc", "Esc", width: 44);
        AddSpacer(row0, 8);
        AddKeyBtn(row0, "f1", "F1"); AddKeyBtn(row0, "f2", "F2"); AddKeyBtn(row0, "f3", "F3"); AddKeyBtn(row0, "f4", "F4");
        AddSpacer(row0, 10);
        AddKeyBtn(row0, "f5", "F5"); AddKeyBtn(row0, "f6", "F6"); AddKeyBtn(row0, "f7", "F7"); AddKeyBtn(row0, "f8", "F8");
        AddSpacer(row0, 10);
        AddKeyBtn(row0, "f9", "F9"); AddKeyBtn(row0, "f10", "F10"); AddKeyBtn(row0, "f11", "F11"); AddKeyBtn(row0, "f12", "F12");
        AddSpacer(row0, 28);
        mainKeyboardPanel.Children.Add(row0);

        // Row 1: Number Row & Backspace
        var row1 = CreateKeyboardRow();
        string[] r1Keys = { "`", "1", "2", "3", "4", "5", "6", "7", "8", "9", "0", "-", "=" };
        foreach (var k in r1Keys) AddKeyBtn(row1, k, k);
        AddKeyBtn(row1, "backspace", "Backspace", width: 76);
        mainKeyboardPanel.Children.Add(row1);

        // Row 2: QWERTY Row & \
        var row2 = CreateKeyboardRow();
        AddKeyBtn(row2, KeyTab, "Tab", width: 54);
        string[] r2Keys = { "q", "w", "e", "r", "t", "y", "u", "i", "o", "p", "[", "]" };
        foreach (var k in r2Keys) AddKeyBtn(row2, k, k.ToUpper());
        AddKeyBtn(row2, "\\", "\\", width: 58);
        mainKeyboardPanel.Children.Add(row2);

        // Row 3: Home Row & Enter
        var row3 = CreateKeyboardRow();
        AddKeyBtn(row3, "capslock", "Caps Lock", width: 66);
        string[] r3Keys = { "a", "s", "d", "f", "g", "h", "j", "k", "l", ";", "'" };
        foreach (var k in r3Keys) AddKeyBtn(row3, k, k.ToUpper());
        AddKeyBtn(row3, KeyEnter, "Enter", width: 86);
        mainKeyboardPanel.Children.Add(row3);

        // Row 4: Shift Row & Up Arrow
        var row4 = CreateKeyboardRow();
        AddKeyBtn(row4, "shift", "Shift", width: 88);
        string[] r4Keys = { "z", "x", "c", "v", "b", "n", "m", ",", ".", "/" };
        foreach (var k in r4Keys) AddKeyBtn(row4, k, k.ToUpper());
        AddKeyBtn(row4, "shift", "Shift", width: 64);
        AddKeyBtn(row4, "up", "▲"); // Starts at 560, ends at 596px, aligning with Backspace / \ / Enter
        mainKeyboardPanel.Children.Add(row4);

        // Row 5: Bottom Row (Ctrl, Win, Alt, Space, Alt, Win, Ctrl, Left Arrow, Down Arrow)
        var row5 = CreateKeyboardRow();
        AddKeyBtn(row5, "ctrl", "Ctrl", width: 48);
        AddKeyBtn(row5, "win", "Win", width: 44);
        AddKeyBtn(row5, "alt", "Alt", width: 44);
        AddKeyBtn(row5, "space", "Space", width: 242);
        AddKeyBtn(row5, "alt", "Alt", width: 38);
        AddKeyBtn(row5, "win", "Win", width: 38);
        AddKeyBtn(row5, "ctrl", "Ctrl", width: 38);
        AddKeyBtn(row5, "left", "◄"); // Starts at 520, ends at 556px, aligning with Shift
        AddKeyBtn(row5, "down", "▼"); // Starts at 560, ends at 596px, aligning with ▲
        mainKeyboardPanel.Children.Add(row5);

        // ── Right Numpad Grid (Rows 0 to 5 x Columns 0 to 4) ──
        var numpadGrid = new Grid
        {
            RowSpacing = 4,
            ColumnSpacing = 4,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        for (int r = 0; r < 6; r++)
        {
            numpadGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(34) });
        }
        for (int c = 0; c < 5; c++)
        {
            numpadGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
        }

        // Row 0: Top control & navigation keys
        AddGridKeyBtn(numpadGrid, 0, 0, KeyPrintScreen, "PrtSc");
        AddGridKeyBtn(numpadGrid, 0, 1, "scrolllock", "ScrLk");
        AddGridKeyBtn(numpadGrid, 0, 2, "pause", "Pause");
        AddGridKeyBtn(numpadGrid, 0, 3, "insert", "Ins");
        AddGridKeyBtn(numpadGrid, 0, 4, "delete", "Del");

        // Row 1: Numpad operators & Home
        AddGridKeyBtn(numpadGrid, 1, 0, "numlock", "NumLk");
        AddGridKeyBtn(numpadGrid, 1, 1, "divide", "/");
        AddGridKeyBtn(numpadGrid, 1, 2, "multiply", "*");
        AddGridKeyBtn(numpadGrid, 1, 3, "subtract", "-");
        AddGridKeyBtn(numpadGrid, 1, 4, "home", "Home");

        // Row 2: 7 8 9 + PgUp (+ spans Row 2 & 3: 72px)
        AddGridKeyBtn(numpadGrid, 2, 0, "num7", "7");
        AddGridKeyBtn(numpadGrid, 2, 1, "num8", "8");
        AddGridKeyBtn(numpadGrid, 2, 2, "num9", "9");
        AddSpannedGridKeyBtn(numpadGrid, 2, 3, "add", "+");
        AddGridKeyBtn(numpadGrid, 2, 4, "pageup", "PgUp");

        // Row 3: 4 5 6 PgDn (+ spans from row 2)
        AddGridKeyBtn(numpadGrid, 3, 0, "num4", "4");
        AddGridKeyBtn(numpadGrid, 3, 1, "num5", "5");
        AddGridKeyBtn(numpadGrid, 3, 2, "num6", "6");
        AddGridKeyBtn(numpadGrid, 3, 4, "pagedown", "PgDn");

        // Row 4: 1 2 3 NumpadEnter End (Enter and End span Row 4 & 5: 72px)
        AddGridKeyBtn(numpadGrid, 4, 0, "num1", "1");
        AddGridKeyBtn(numpadGrid, 4, 1, "num2", "2");
        AddGridKeyBtn(numpadGrid, 4, 2, "num3", "3");
        AddSpannedGridKeyBtn(numpadGrid, 4, 3, "numpadenter", "Enter");
        AddSpannedGridKeyBtn(numpadGrid, 4, 4, "end", "End");

        // Row 5: ► 0 . (Enter and End span from row 4)
        AddGridKeyBtn(numpadGrid, 5, 0, "right", "►");
        AddGridKeyBtn(numpadGrid, 5, 1, "num0", "0");
        AddGridKeyBtn(numpadGrid, 5, 2, "decimal", ".");

        // ── Master Dual-Column Grid ──
        var masterGrid = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        masterGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        masterGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 12 });
        masterGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        Grid.SetColumn(mainKeyboardPanel, 0);
        Grid.SetColumn(numpadGrid, 2);

        masterGrid.Children.Add(mainKeyboardPanel);
        masterGrid.Children.Add(numpadGrid);

        KeyboardContainer.Children.Add(masterGrid);

        UpdateVirtualKeyboardHighlights();
    }

    private static StackPanel CreateKeyboardRow()
    {
        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            HorizontalAlignment = HorizontalAlignment.Left
        };
    }

    private static void AddSpacer(StackPanel row, double width)
    {
        row.Children.Add(new Border { Width = width });
    }

    private Button CreateKeyBtn(string keyId, string label, double width = 36, double height = 34)
    {
        var btn = new Button
        {
            Content = label,
            Width = width,
            Height = height,
            Padding = new Thickness(2),
            FontSize = width > 50 || height > 50 ? 10 : 11,
            FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Cascadia Code, Consolas"),
            CornerRadius = new CornerRadius(4),
            Style = Application.Current.Resources["DefaultButtonStyle"] as Style
        };

        btn.Click += (s, e) =>
        {
            string current = HolderKeyBox.Text.Trim();
            if (string.IsNullOrEmpty(current) || string.Equals(current, "w", StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(current, keyId, StringComparison.OrdinalIgnoreCase))
                {
                    HolderKeyBox.Text = string.Empty;
                }
                else
                {
                    HolderKeyBox.Text = keyId;
                }
            }
            else
            {
                var keys = Keyboard.SplitCombo(current).ToList();
                if (keys.Any(k => IsKeyMatch(k, keyId)))
                {
                    keys.RemoveAll(k => IsKeyMatch(k, keyId));
                    HolderKeyBox.Text = string.Join("+", keys);
                }
                else
                {
                    keys.Add(keyId);
                    HolderKeyBox.Text = string.Join("+", keys);
                }
            }
        };

        if (!_keyboardButtons.TryGetValue(keyId, out var btnList))
        {
            btnList = [];
            _keyboardButtons[keyId] = btnList;
        }
        btnList.Add(btn);
        return btn;
    }

    private void AddKeyBtn(StackPanel row, string keyId, string label, double width = 36)
    {
        var btn = CreateKeyBtn(keyId, label, width, 34);
        row.Children.Add(btn);
    }

    private void AddGridKeyBtn(Grid grid, int row, int col, string keyId, string label)
    {
        var btn = CreateKeyBtn(keyId, label, 36, 34);
        Grid.SetRow(btn, row);
        Grid.SetColumn(btn, col);
        grid.Children.Add(btn);
    }

    private void AddSpannedGridKeyBtn(Grid grid, int row, int col, string keyId, string label, int rowSpan = 2)
    {
        var btn = CreateKeyBtn(keyId, label, 36, 72);
        Grid.SetRow(btn, row);
        Grid.SetColumn(btn, col);
        Grid.SetRowSpan(btn, rowSpan);
        grid.Children.Add(btn);
    }

    private static readonly Dictionary<string, HashSet<string>> KeyAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["delete"] = new(StringComparer.OrdinalIgnoreCase) { "del" },
        ["del"] = new(StringComparer.OrdinalIgnoreCase) { "delete" },
        ["insert"] = new(StringComparer.OrdinalIgnoreCase) { "ins" },
        ["ins"] = new(StringComparer.OrdinalIgnoreCase) { "insert" },
        ["pageup"] = new(StringComparer.OrdinalIgnoreCase) { "pgup" },
        ["pgup"] = new(StringComparer.OrdinalIgnoreCase) { "pageup" },
        ["pagedown"] = new(StringComparer.OrdinalIgnoreCase) { "pgdn" },
        ["pgdn"] = new(StringComparer.OrdinalIgnoreCase) { "pagedown" },
        [KeyPrintScreen] = new(StringComparer.OrdinalIgnoreCase) { "prtsc", "prtscr" },
        ["prtsc"] = new(StringComparer.OrdinalIgnoreCase) { KeyPrintScreen, "prtscr" },
        ["prtscr"] = new(StringComparer.OrdinalIgnoreCase) { KeyPrintScreen, "prtsc" },
        ["scrolllock"] = new(StringComparer.OrdinalIgnoreCase) { "scrlk" },
        ["scrlk"] = new(StringComparer.OrdinalIgnoreCase) { "scrolllock" },
        ["numlock"] = new(StringComparer.OrdinalIgnoreCase) { "numlk" },
        ["numlk"] = new(StringComparer.OrdinalIgnoreCase) { "numlock" },
        ["ctrl"] = new(StringComparer.OrdinalIgnoreCase) { "control" },
        ["control"] = new(StringComparer.OrdinalIgnoreCase) { "ctrl" },
        ["win"] = new(StringComparer.OrdinalIgnoreCase) { "windows" },
        ["windows"] = new(StringComparer.OrdinalIgnoreCase) { "win" },
        ["esc"] = new(StringComparer.OrdinalIgnoreCase) { "escape" },
        ["escape"] = new(StringComparer.OrdinalIgnoreCase) { "esc" },
        ["numpadenter"] = new(StringComparer.OrdinalIgnoreCase) { "numenter" },
        ["numenter"] = new(StringComparer.OrdinalIgnoreCase) { "numpadenter" },
        ["add"] = new(StringComparer.OrdinalIgnoreCase) { "num+" },
        ["num+"] = new(StringComparer.OrdinalIgnoreCase) { "add" },
    };

    private static bool IsKeyMatch(string a, string b)
    {
        if (string.Equals(a, b, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return (KeyAliases.TryGetValue(a, out var aAliases) && aAliases.Contains(b))
            || (KeyAliases.TryGetValue(b, out var bAliases) && bAliases.Contains(a));
    }

    private static HashSet<string> GetActiveKeysWithAliases(string rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText))
        {
            return [];
        }

        var tokens = Keyboard.SplitCombo(rawText);
        var activeKeys = new HashSet<string>(tokens, StringComparer.OrdinalIgnoreCase);
        foreach (var token in tokens)
        {
            if (KeyAliases.TryGetValue(token, out var aliases))
            {
                activeKeys.UnionWith(aliases);
            }
        }

        return activeKeys;
    }

    private void ResetVirtualKeyboardButtons(Style defaultStyle)
    {
        foreach (var list in _keyboardButtons.Values)
        {
            foreach (var b in list)
            {
                b.Style = defaultStyle;
            }
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Major Bug", "S2583:Conditionally executed code should be reachable", Justification = "HolderKeyBox is initialized by XAML InitializeComponent at runtime")]
    private void UpdateVirtualKeyboardHighlights()
    {
        if (Application.Current.Resources["AccentButtonStyle"] is not Style accentStyle ||
            Application.Current.Resources["DefaultButtonStyle"] is not Style defaultStyle)
        {
            return;
        }

        string rawText = HolderKeyBox != null ? HolderKeyBox.Text : string.Empty;
        var activeKeys = GetActiveKeysWithAliases(rawText);
        if (activeKeys.Count == 0)
        {
            ResetVirtualKeyboardButtons(defaultStyle);
            return;
        }

        foreach (var (keyId, list) in _keyboardButtons)
        {
            var targetStyle = activeKeys.Contains(keyId) ? accentStyle : defaultStyle;
            foreach (var b in list)
            {
                b.Style = targetStyle;
            }
        }
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.IsSettingsSelected)
        {
            _activeTab = TabSettings;
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
            TyperPanel.Visibility = tag == TabTyper ? Visibility.Visible : Visibility.Collapsed;
            HolderPanel.Visibility = tag == TabHolder ? Visibility.Visible : Visibility.Collapsed;
            ClickerPanel.Visibility = tag == TabClicker ? Visibility.Visible : Visibility.Collapsed;
            MacroPanel.Visibility = tag == TabMacro ? Visibility.Visible : Visibility.Collapsed;
            SettingsPanel.Visibility = Visibility.Collapsed;
            AboutPanel.Visibility = tag == TabAbout ? Visibility.Visible : Visibility.Collapsed;

            UpdateTargetMarkerOverlay();
        }
    }

    private void NavigateToAbout_Click(object sender, RoutedEventArgs e)
    {
        _activeTab = TabAbout;
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
            string folder = System.IO.Path.Join(
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
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to open settings folder: {ex.Message}");
        }
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
                UpdateTyperActionBtnState();
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
        UpdateTyperActionBtnState();
    }

    private void ClearHolderKey_Click(object sender, RoutedEventArgs e)
    {
        HolderKeyBox.Text = "";
        SyncSelectedHolderStage();
        UpdateHolderActionBtnState();
    }

    private void HolderDurationTypeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (HoldDurationBox == null) return;
        bool isTimed = HolderDurationTypeCombo.SelectedIndex == 1;
        HoldDurationBox.IsEnabled = isTimed;
        SyncSelectedHolderStage();
    }

    private void HolderRestCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (HolderRestBox == null) return;
        bool hasRest = HolderRestCheck.IsChecked == true;
        HolderRestBox.IsEnabled = hasRest;
        SyncSelectedHolderStage();
    }

    private void HolderStageList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateStageButtonsState();
        if (_isUpdatingHolderStage) return;

        int idx = HolderStageList.SelectedIndex;
        if (idx >= 0 && idx < _holderStages.Count)
        {
            var stage = _holderStages[idx];
            _isUpdatingHolderStage = true;
            if (HolderStepInspectorHeader != null)
            {
                HolderStepInspectorHeader.Text = $"Selected Step #{idx + 1} Key Combo (Click keyboard below):";
            }
            HolderKeyBox.Text = stage.KeyCombo;

            if (stage.HoldDurationSec > 0)
            {
                HolderDurationTypeCombo.SelectedIndex = 1;
                HoldDurationBox.IsEnabled = true;
                HoldDurationBox.Value = stage.HoldDurationSec;
            }
            else
            {
                HolderDurationTypeCombo.SelectedIndex = 0;
                HoldDurationBox.IsEnabled = false;
            }

            if (stage.RestDurationSec > 0)
            {
                HolderRestCheck.IsChecked = true;
                HolderRestBox.IsEnabled = true;
                HolderRestBox.Value = stage.RestDurationSec;
            }
            else
            {
                HolderRestCheck.IsChecked = false;
                HolderRestBox.IsEnabled = false;
            }

            _isUpdatingHolderStage = false;
            UpdateHolderActionBtnState();
            UpdateHolderRepeatPanelVisibility();
        }
    }

    private void RefreshHolderStageList()
    {
        int prevIndex = HolderStageList?.SelectedIndex ?? -1;
        HolderStageList?.Items.Clear();
        for (int i = 0; i < _holderStages.Count; i++)
        {
            HolderStageList?.Items.Add(FormatStageItemText(i + 1, _holderStages[i]));
        }
        if (HolderStageList != null && prevIndex >= 0 && prevIndex < _holderStages.Count)
        {
            HolderStageList.SelectedIndex = prevIndex;
        }
        UpdateStageButtonsState();
        UpdateHolderRepeatPanelVisibility();
    }

    private static string FormatStageItemText(int stageNum, KeyHolderStage stage, bool isRunningNow = false)
    {
        string statusPrefix = isRunningNow ? "▶ " : "";
        string holdStr = stage.HoldDurationSec <= 0 ? "Until stopped" : $"{stage.HoldDurationSec:F1}s";
        string restStr = stage.RestDurationSec > 0 ? $", Rest: {stage.RestDurationSec:F1}s" : "";
        return $"{statusPrefix}Step {stageNum}: [{stage.KeyCombo}] {holdStr}{restStr}";
    }

    private void UpdateStageButtonsState()
    {
        int idx = HolderStageList?.SelectedIndex ?? -1;
        if (DeleteStageBtn != null) DeleteStageBtn.IsEnabled = idx >= 0 && _holderStages.Count > 1;
        if (MoveUpStageBtn != null) MoveUpStageBtn.IsEnabled = idx > 0;
        if (MoveDownStageBtn != null) MoveDownStageBtn.IsEnabled = idx >= 0 && idx < _holderStages.Count - 1;
    }

    private void UpdateHolderRepeatPanelVisibility()
    {
        if (HolderRepeatPanel == null) return;
        bool show = _holderStages.Count > 1 || _holderStages.Any(s => s.HoldDurationSec > 0 && s.RestDurationSec > 0);
        HolderRepeatPanel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SyncSelectedHolderStage()
    {
        if (_isUpdatingHolderStage) return;
        int idx = HolderStageList?.SelectedIndex ?? -1;
        if (idx >= 0 && idx < _holderStages.Count)
        {
            var stage = _holderStages[idx];
            stage.KeyCombo = HolderKeyBox.Text.Trim();
            bool isTimed = HolderDurationTypeCombo?.SelectedIndex == 1;
            stage.HoldDurationSec = isTimed ? Math.Max(0.1, HoldDurationBox.Value) : 0;
            bool hasRest = HolderRestCheck?.IsChecked == true;
            stage.RestDurationSec = hasRest ? Math.Max(0, HolderRestBox.Value) : 0;

            int cur = HolderStageList!.SelectedIndex;
            _isUpdatingHolderStage = true;
            HolderStageList.Items[idx] = FormatStageItemText(idx + 1, stage);
            HolderStageList.SelectedIndex = cur;
            _isUpdatingHolderStage = false;
            UpdateStageButtonsState();
            UpdateHolderRepeatPanelVisibility();
        }
    }

    private void AddStageBtn_Click(object sender, RoutedEventArgs e)
    {
        double hold = _holderStages.Count == 0 ? 0 : 3.0;
        string key = "w";
        var stage = new KeyHolderStage(key, hold, 0);
        _holderStages.Add(stage);
        RefreshHolderStageList();
        HolderStageList.SelectedIndex = _holderStages.Count - 1;
        UpdateHolderActionBtnState();
        UpdateHolderRepeatPanelVisibility();
        HolderStatusText.Text = $"Added Step #{_holderStages.Count}: [{stage.KeyCombo}]";
    }

    private void DeleteStageBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_holderStages.Count <= 1) return;
        int idx = HolderStageList.SelectedIndex;
        if (idx >= 0 && idx < _holderStages.Count)
        {
            _holderStages.RemoveAt(idx);
            RefreshHolderStageList();
            HolderStageList.SelectedIndex = Math.Clamp(idx, 0, _holderStages.Count - 1);
            UpdateHolderActionBtnState();
            UpdateHolderRepeatPanelVisibility();
            HolderStatusText.Text = $"Deleted step #{idx + 1}";
        }
    }

    private void MoveUpStageBtn_Click(object sender, RoutedEventArgs e)
    {
        int idx = HolderStageList.SelectedIndex;
        if (idx > 0 && idx < _holderStages.Count)
        {
            var stage = _holderStages[idx];
            _holderStages.RemoveAt(idx);
            _holderStages.Insert(idx - 1, stage);
            RefreshHolderStageList();
            HolderStageList.SelectedIndex = idx - 1;
            HolderStatusText.Text = $"Moved step to #{idx}";
        }
    }

    private void MoveDownStageBtn_Click(object sender, RoutedEventArgs e)
    {
        int idx = HolderStageList.SelectedIndex;
        if (idx >= 0 && idx < _holderStages.Count - 1)
        {
            var stage = _holderStages[idx];
            _holderStages.RemoveAt(idx);
            _holderStages.Insert(idx + 1, stage);
            RefreshHolderStageList();
            HolderStageList.SelectedIndex = idx + 1;
            HolderStatusText.Text = $"Moved step to #{idx + 2}";
        }
    }

    private void HighlightRunningStage(int activeIndex)
    {
        for (int idx = 0; idx < _holderStages.Count; idx++)
        {
            if (idx < HolderStageList.Items.Count)
            {
                HolderStageList.Items[idx] = FormatStageItemText(idx + 1, _holderStages[idx], isRunningNow: (idx == activeIndex));
            }
        }
        HolderStageList.SelectedIndex = activeIndex;
    }

    private void ClearRunningStageHighlight()
    {
        for (int idx = 0; idx < _holderStages.Count; idx++)
        {
            if (idx < HolderStageList.Items.Count)
            {
                HolderStageList.Items[idx] = FormatStageItemText(idx + 1, _holderStages[idx], isRunningNow: false);
            }
        }
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
        PickCoordText.Text = "Hover & Press Space (Esc=Cancel)...";
        ClickerStatusText.Text = "Move cursor to target. Press Space/Enter or click to lock (Esc to cancel)...";

        var (picked, canceled, x, y) = await Task.Run(PollTargetCoordinatesAsync, CancellationToken.None);

        PickCoordText.Text = "Pick Location";
        if (canceled || !picked)
        {
            ClickerStatusText.Text = "Coordinate picking canceled";
            return;
        }

        ClickXBox.Value = x;
        ClickYBox.Value = y;
        ShowTargetMarkerCheck.IsChecked = true;
        OnTargetMarkerSettingsChanged();
        ClickerStatusText.Text = $"Locked target coordinates: ({x}, {y})";
    }

    private void ClearCoordsBtn_Click(object sender, RoutedEventArgs e)
    {
        ClickXBox.Value = double.NaN;
        ClickYBox.Value = double.NaN;
        OnTargetMarkerSettingsChanged();
        UpdateClearCoordsBtnState();
        ClickerStatusText.Text = "Coordinates cleared (clicking at current cursor location)";
    }

    private static async Task<(bool Picked, bool Canceled, int X, int Y)> PollTargetCoordinatesAsync()
    {
        const int VK_SPACE = 0x20;
        const int VK_RETURN = 0x0D;
        const int VK_LBUTTON = 0x01;
        const int VK_ESCAPE = 0x1B;

        await Task.Delay(250);
        var start = DateTime.UtcNow;

        while ((DateTime.UtcNow - start).TotalSeconds < 4.0)
        {
            if ((NativeMethods.GetAsyncKeyState(VK_ESCAPE) & 0x8000) != 0)
            {
                return (false, true, 0, 0);
            }

            bool triggered = (NativeMethods.GetAsyncKeyState(VK_SPACE) & 0x8000) != 0 ||
                             (NativeMethods.GetAsyncKeyState(VK_RETURN) & 0x8000) != 0 ||
                             (NativeMethods.GetAsyncKeyState(VK_LBUTTON) & 0x8000) != 0;

            if (triggered)
            {
                var (x, y) = Mouse.GetPosition();
                return (true, false, x, y);
            }

            await Task.Delay(20);
        }

        var (fx, fy) = Mouse.GetPosition();
        return (true, false, fx, fy);
    }

    private void RecordMacroBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_isRecordingMacro)
        {
            StopMacroRecording();
        }
        else
        {
            StartMacroRecording();
        }
    }

    private void StopMacroRecording()
    {
        _isRecordingMacro = false;
        _macroRecorder.StopRecording();
        _targetMarkerOverlay.ClearAndHide();
        RecordIcon.Glyph = "\uE7C8";
        RecordMacroText.Text = "Start Recording";
        MacroStatusText.Text = $"Macro recorded: {_macroRecorder.Actions.Count} actions";
        RefreshMacroActionList();
        UpdateMacroActionBtnState();
    }

    private void StartMacroRecording()
    {
        _isRecordingMacro = true;
        UpdateMacroActionBtnState();
        MacroActionList.Items.Clear();
        _targetMarkerOverlay.ClearAndHide();
        int clickOrder = 0;
        _macroRecorder.StartRecording(
            count =>
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    MacroStatusText.Text = $"Recording macro... {count} actions captured (Click Stop to finish)";
                });
            },
            true,
            (action, count) =>
            {
                if (!_isRecordingMacro) return;

                DispatcherQueue.TryEnqueue(() =>
                {
                    if (!_isRecordingMacro) return;

                    if (action.Type is MacroActionType.ClickLeft or MacroActionType.ClickRight or MacroActionType.ClickMiddle)
                    {
                        clickOrder++;
                        _targetMarkerOverlay.AddMarkerWithRipple(clickOrder, action.X, action.Y);
                    }

                    string detail = FormatMacroActionDetail(action);
                    MacroActionList.Items.Add($"#{count} (+{action.DelayMs}ms) — {detail}");
                    if (MacroActionList.Items.Count > 0)
                    {
                        MacroActionList.ScrollIntoView(MacroActionList.Items[^1]);
                    }
                });
            });
        RecordIcon.Glyph = "\uE71A";
        RecordMacroText.Text = "Stop Recording";
        MacroStatusText.Text = "Recording macro... Click or type anywhere to record actions!";
    }

    private void ClearMacroBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_isRecordingMacro)
        {
            _isRecordingMacro = false;
            _macroRecorder.StopRecording();
        }
        RecordIcon.Glyph = "\uE7C8";
        RecordMacroText.Text = "Start Recording";
        _macroRecorder.Clear();
        MacroActionList.Items.Clear();
        _targetMarkerOverlay.ClearAndHide();
        MacroStatusText.Text = "Macro cleared";
        UpdateMacroActionBtnState();
    }

    private void RefreshMacroActionList()
    {
        MacroActionList.Items.Clear();
        var actions = _macroRecorder.Actions;
        for (int i = 0; i < actions.Count; i++)
        {
            var act = actions[i];
            string detail = FormatMacroActionDetail(act);
            MacroActionList.Items.Add($"#{i + 1} (+{act.DelayMs}ms) — {detail}");
        }
    }

    private static string FormatMacroActionDetail(MacroAction act) => act.Type switch
    {
        MacroActionType.ClickLeft => $"🖱️ Left Click at ({act.X}, {act.Y})",
        MacroActionType.ClickRight => $"🖱️ Right Click at ({act.X}, {act.Y})",
        MacroActionType.ClickMiddle => $"🖱️ Middle Click at ({act.X}, {act.Y})",
        MacroActionType.MouseMove => $"↗️ Move Cursor to ({act.X}, {act.Y})",
        MacroActionType.KeyPress => $"⌨️ Key Down [{act.Data}]",
        MacroActionType.KeyRelease => $"⌨️ Key Up [{act.Data}]",
        _ => $"🔤 Type [{act.Data}]"
    };

    private void DeleteActionBtn_Click(object sender, RoutedEventArgs e)
    {
        int index = MacroActionList.SelectedIndex;
        if (index >= 0 && _macroRecorder.RemoveActionAt(index))
        {
            _targetMarkerOverlay.ClearAndHide();
            RefreshMacroActionList();
            UpdateMacroActionBtnState();
            MacroStatusText.Text = $"Deleted step #{index + 1}";
        }
        else
        {
            MacroStatusText.Text = "Please select a step to delete first.";
        }
    }

    private async void EditActionBtn_Click(object sender, RoutedEventArgs e)
    {
        int index = MacroActionList.SelectedIndex;
        if (index >= 0)
        {
            await ShowEditActionDialogAsync(index);
        }
        else
        {
            MacroStatusText.Text = "Please select a step to edit first.";
        }
    }

    private async void MacroActionList_DoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
    {
        int index = MacroActionList.SelectedIndex;
        if (index >= 0)
        {
            await ShowEditActionDialogAsync(index);
        }
    }

    private async Task ShowEditActionDialogAsync(int index)
    {
        var actions = _macroRecorder.Actions;
        if (index < 0 || index >= actions.Count) return;
        var act = actions[index];

        var typeCombo = new ComboBox
        {
            Header = "Action Type:",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ItemsSource = new[]
            {
                "Left Click",
                "Right Click",
                "Middle Click",
                "Move Cursor",
                "Key Down",
                "Key Up",
                "Type Text"
            },
            SelectedIndex = act.Type switch
            {
                MacroActionType.ClickLeft => 0,
                MacroActionType.ClickRight => 1,
                MacroActionType.ClickMiddle => 2,
                MacroActionType.MouseMove => 3,
                MacroActionType.KeyPress => 4,
                MacroActionType.KeyRelease => 5,
                _ => 6
            }
        };

        var delayBox = new NumberBox
        {
            Header = "Delay Before Action (ms):",
            Value = act.DelayMs,
            Minimum = 0,
            Maximum = 60000,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        var xBox = new NumberBox { Header = "X Coordinate:", Value = act.X, Minimum = 0, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact };
        var yBox = new NumberBox { Header = "Y Coordinate:", Value = act.Y, Minimum = 0, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact };

        var coordPanel = new Grid { ColumnSpacing = 12 };
        coordPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        coordPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(xBox, 0);
        Grid.SetColumn(yBox, 1);
        coordPanel.Children.Add(xBox);
        coordPanel.Children.Add(yBox);

        var dataBox = new TextBox
        {
            Header = "Key Name / Text Data:",
            Text = act.Data ?? string.Empty,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        void UpdateVisibility()
        {
            int sel = typeCombo.SelectedIndex;
            bool isMouse = sel is 0 or 1 or 2 or 3;
            coordPanel.Visibility = isMouse ? Visibility.Visible : Visibility.Collapsed;
            dataBox.Visibility = isMouse ? Visibility.Collapsed : Visibility.Visible;
        }

        typeCombo.SelectionChanged += (_, _) => UpdateVisibility();
        UpdateVisibility();

        var contentPanel = new StackPanel { Spacing = 12, Width = 320 };
        contentPanel.Children.Add(typeCombo);
        contentPanel.Children.Add(coordPanel);
        contentPanel.Children.Add(dataBox);
        contentPanel.Children.Add(delayBox);

        var dialog = new ContentDialog
        {
            Title = $"Edit Step #{index + 1}",
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            Content = contentPanel,
            XamlRoot = Content.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            var newType = typeCombo.SelectedIndex switch
            {
                0 => MacroActionType.ClickLeft,
                1 => MacroActionType.ClickRight,
                2 => MacroActionType.ClickMiddle,
                3 => MacroActionType.MouseMove,
                4 => MacroActionType.KeyPress,
                5 => MacroActionType.KeyRelease,
                _ => MacroActionType.TypeText
            };

            var updated = new MacroAction
            {
                Type = newType,
                X = (int)xBox.Value,
                Y = (int)yBox.Value,
                Data = dataBox.Text.Trim(),
                DelayMs = (long)Math.Max(0, delayBox.Value)
            };

            if (_macroRecorder.UpdateAction(index, updated))
            {
                RefreshMacroActionList();
                MacroStatusText.Text = $"Updated step #{index + 1}";
            }
        }
    }

    // ══════════════════════════════════════════════════════════
    // Per-Panel Action Handlers (Plan A: Self-Contained Execution)
    // ══════════════════════════════════════════════════════════

    private static void UpdateClickerTargetType(MainPage page)
    {
        if (page.ClickTargetTypeCombo == null || page.MouseButtonLabel == null || page.SpamKeyLabel == null) return;
        bool isSpammer = page.ClickTargetTypeCombo.SelectedIndex == 1;
        var mouseVis = isSpammer ? Visibility.Collapsed : Visibility.Visible;
        var spamVis = isSpammer ? Visibility.Visible : Visibility.Collapsed;

        page.MouseButtonLabel.Visibility = mouseVis;
        page.MouseButtonCombo.Visibility = mouseVis;
        page.ClickCoordsLabel.Visibility = mouseVis;
        page.ClickCoordsPanel.Visibility = mouseVis;
        page.LocationJitterLabel.Visibility = mouseVis;
        page.LocationJitterPanel.Visibility = mouseVis;

        page.SpamKeyLabel.Visibility = spamVis;
        page.SpamKeyBox.Visibility = spamVis;

        AppSettings.Current.ClickerIsKeySpammer = isSpammer;
        AppSettings.Current.Save();

        if (isSpammer)
        {
            page._targetMarkerOverlay.Hide();
        }
        else
        {
            page.UpdateTargetMarkerOverlay();
        }
    }

    private void OnTargetMarkerSettingsChanged()
    {
        AppSettings.Current.ClickerShowTargetMarker = ShowTargetMarkerCheck.IsChecked.GetValueOrDefault(false);
        AppSettings.Current.Save();
        UpdateTargetMarkerOverlay();
    }

    private void UpdateTargetMarkerOverlay()
    {
        if (_activeTab != TabClicker)
        {
            _targetMarkerOverlay.Update(0, 0, 0, false);
            return;
        }

        bool isSpammer = ClickTargetTypeCombo.SelectedIndex == 1;
        bool isEnabled = ShowTargetMarkerCheck.IsChecked.GetValueOrDefault(false);

        if (isSpammer || !isEnabled)
        {
            _targetMarkerOverlay.Update(0, 0, 0, false);
            return;
        }

        int? targetX = double.IsNaN(ClickXBox.Value) ? null : (int)ClickXBox.Value;
        int? targetY = double.IsNaN(ClickYBox.Value) ? null : (int)ClickYBox.Value;
        double locJitterPx = LocationJitterBox.Value;

        if (targetX.HasValue && targetY.HasValue)
        {
            _targetMarkerOverlay.Update(targetX.Value, targetY.Value, locJitterPx, true);
        }
        else
        {
            // If fixed coordinates are not locked (e.g. Current cursor mode), hide static target marker
            _targetMarkerOverlay.Update(0, 0, 0, false);
        }
    }

    private static void UpdateClickerTriggerMode(MainPage page)
    {
        if (page.ClickTriggerModeCombo == null || page.HoldModeHint == null) return;
        bool isHold = page.ClickTriggerModeCombo.SelectedIndex == 1;
        page.HoldModeHint.Visibility = isHold ? Visibility.Visible : Visibility.Collapsed;

        AppSettings.Current.ClickerHoldMode = isHold;
        AppSettings.Current.Save();
    }

    private static void UpdateTyperMode(MainPage page)
    {
        if (page.TypeModeCombo == null || page.TypeModeHint == null || page.TypeIntervalLabel == null || page.TypeIntervalPanel == null) return;
        bool isClipboard = page.TypeModeCombo.SelectedIndex == 1;
        page.TypeModeHint.Visibility = isClipboard ? Visibility.Visible : Visibility.Collapsed;
        page.TypeIntervalLabel.Visibility = isClipboard ? Visibility.Collapsed : Visibility.Visible;
        page.TypeIntervalPanel.Visibility = isClipboard ? Visibility.Collapsed : Visibility.Visible;

        AppSettings.Current.TyperInputMode = isClipboard ? TyperModeClipboard : TyperModeKeystroke;
        AppSettings.Current.Save();
    }

    public void OnGuiHidden()
    {
        _targetMarkerOverlay.ClearAndHide();
    }

    public void OnGuiRestored()
    {
        UpdateTargetMarkerOverlay();
    }

    // ══════════════════════════════════════════════════════════
    // Global Hotkey Remote Triggers
    // ══════════════════════════════════════════════════════════

    public void ToggleClickerFromHotkey()
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            NavView.SelectedItem = NavView.MenuItems[2];
            ClickerActionBtn_Click(this, new RoutedEventArgs());
        });
    }

    public void ToggleHolderFromHotkey()
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            NavView.SelectedItem = NavView.MenuItems[1];
            bool hasTarget = _holderStages.Count > 0 && _holderStages.Any(s => !string.IsNullOrWhiteSpace(s.KeyCombo));

            if (!_isRunning && !hasTarget)
            {
                HolderStatusText.Text = "Please enter a key to hold";
                UpdateAllActionBtnStates();
                return;
            }
            HolderActionBtn_Click(this, new RoutedEventArgs());
        });
    }

    public void ToggleTyperFromHotkey()
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            NavView.SelectedItem = NavView.MenuItems[0];
            if (!_isRunning && string.IsNullOrWhiteSpace(TypeTextBox.Text))
            {
                TyperStatusText.Text = "Please enter text to type";
                UpdateAllActionBtnStates();
                return;
            }
            TyperActionBtn_Click(this, new RoutedEventArgs());
        });
    }

    public void ToggleMacroFromHotkey()
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            NavView.SelectedItem = NavView.MenuItems[3];
            if (!_isRunning && _macroRecorder.Actions.Count == 0)
            {
                MacroStatusText.Text = "No recorded macro actions to replay! Please record first.";
                UpdateAllActionBtnStates();
                return;
            }
            MacroActionBtn_Click(this, new RoutedEventArgs());
        });
    }

    public void PanicKillAll()
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (_isRunning)
            {
                StopTask("Emergency stop triggered via F10 panic key");
            }
        });
    }

    // ══════════════════════════════════════════════════════════
    // Per-Panel Action Handlers
    // ══════════════════════════════════════════════════════════

    private string? _runningTaskName = null;

    private void UpdateAllActionBtnStates()
    {
        UpdateTyperActionBtnState();
        UpdateHolderActionBtnState();
        UpdateClickerActionBtnState();
        UpdateMacroActionBtnState();
    }

    private void UpdateTyperActionBtnState()
    {
        if (_isRunning && _runningTaskName == TabTyper)
        {
            TyperActionBtn.IsEnabled = true;
            if (ClearTextBtn != null) ClearTextBtn.IsEnabled = false;
            return;
        }

        bool hasText = !string.IsNullOrWhiteSpace(TypeTextBox?.Text);
        TyperActionBtn.IsEnabled = hasText;
        if (ClearTextBtn != null) ClearTextBtn.IsEnabled = hasText;
    }

    private void UpdateHolderActionBtnState()
    {
        UpdateVirtualKeyboardHighlights();

        if (_isRunning && _runningTaskName == TabHolder)
        {
            HolderActionBtn.IsEnabled = true;
            if (ClearHolderKeyBtn != null) ClearHolderKeyBtn.IsEnabled = false;
            return;
        }

        bool hasTarget = _holderStages.Count > 0 && _holderStages.Any(s => !string.IsNullOrWhiteSpace(s.KeyCombo));
        HolderActionBtn.IsEnabled = hasTarget;
        if (ClearHolderKeyBtn != null) ClearHolderKeyBtn.IsEnabled = !string.IsNullOrWhiteSpace(HolderKeyBox?.Text);
        UpdateVirtualKeyboardHighlights();
    }

    private void UpdateClickerActionBtnState()
    {
        if (_isRunning && _runningTaskName == TabClicker)
        {
            ClickerActionBtn.IsEnabled = true;
            return;
        }

        bool isSpammer = ClickTargetTypeCombo?.SelectedIndex == 1;
        if (isSpammer)
        {
            ClickerActionBtn.IsEnabled = !string.IsNullOrWhiteSpace(SpamKeyBox?.Text);
        }
        else
        {
            ClickerActionBtn.IsEnabled = true;
        }

        UpdateClearCoordsBtnState();
    }

    private void UpdateClearCoordsBtnState()
    {
        if (ClearCoordsBtn != null && ClickXBox != null && ClickYBox != null)
        {
            bool hasCoords = !double.IsNaN(ClickXBox.Value) || !double.IsNaN(ClickYBox.Value);
            ClearCoordsBtn.IsEnabled = !_isRunning && hasCoords;
        }
    }

    private void UpdateMacroItemActionBtns()
    {
        bool hasSelection = !_isRunning && !_isRecordingMacro && MacroActionList != null && MacroActionList.SelectedIndex >= 0;
        if (EditActionBtn != null) EditActionBtn.IsEnabled = hasSelection;
        if (DeleteActionBtn != null) DeleteActionBtn.IsEnabled = hasSelection;
        if (ClearMacroBtn != null) ClearMacroBtn.IsEnabled = !_isRunning && (_macroRecorder.Actions.Count > 0 || _isRecordingMacro);
    }

    private void UpdateMacroActionBtnState()
    {
        if (_isRunning && _runningTaskName == TabMacro)
        {
            MacroActionBtn.IsEnabled = true;
            UpdateMacroItemActionBtns();
            return;
        }

        if (_isRecordingMacro)
        {
            MacroActionBtn.IsEnabled = false;
            UpdateMacroItemActionBtns();
            return;
        }

        MacroActionBtn.IsEnabled = _macroRecorder.Actions.Count > 0;
        UpdateMacroItemActionBtns();
    }

    private async void TyperActionBtn_Click(object sender, RoutedEventArgs e)
    {
        await RunTaskAsync(
            TabTyper,
            TyperDelayBox,
            TyperStatusText,
            TyperProgressBar,
            TyperActionBtn,
            TyperActionIcon,
            TyperActionText,
            "Start Typer (F8)",
            RunAutoTyperAsync);
    }

    private async void HolderActionBtn_Click(object sender, RoutedEventArgs e)
    {
        await RunTaskAsync(
            TabHolder,
            HolderDelayBox,
            HolderStatusText,
            HolderProgressBar,
            HolderActionBtn,
            HolderActionIcon,
            HolderActionText,
            "Start Holder (F7)",
            RunKeyHolderAsync);
    }

    private async void ClickerActionBtn_Click(object sender, RoutedEventArgs e)
    {
        await RunTaskAsync(
            TabClicker,
            ClickerDelayBox,
            ClickerStatusText,
            ClickerProgressBar,
            ClickerActionBtn,
            ClickerActionIcon,
            ClickerActionText,
            "Start Clicker (F6)",
            RunAutoClickerAsync);
    }

    private async void MacroActionBtn_Click(object sender, RoutedEventArgs e)
    {
        await RunTaskAsync(
            TabMacro,
            MacroDelayBox,
            MacroStatusText,
            MacroProgressBar,
            MacroActionBtn,
            MacroActionIcon,
            MacroActionText,
            "Replay Macro (F9)",
            RunMacroReplayAsync);
    }

    private bool ValidatePreflightTaskInputs(string taskName, TextBlock statusText)
    {
        if (taskName == TabTyper && string.IsNullOrWhiteSpace(TypeTextBox.Text))
        {
            statusText.Text = "Please enter text to type";
            UpdateAllActionBtnStates();
            return false;
        }

        if (taskName == TabHolder && string.IsNullOrWhiteSpace(HolderKeyBox.Text))
        {
            statusText.Text = "Please enter a key to hold";
            UpdateAllActionBtnStates();
            return false;
        }

        if (taskName == TabMacro && _macroRecorder.Actions.Count == 0)
        {
            statusText.Text = "No recorded macro actions to replay! Please record first.";
            UpdateAllActionBtnStates();
            return false;
        }

        if (taskName == TabClicker && ClickTargetTypeCombo.SelectedIndex == 1 && string.IsNullOrWhiteSpace(SpamKeyBox.Text))
        {
            statusText.Text = "Please enter a key to spam";
            UpdateAllActionBtnStates();
            return false;
        }

        return true;
    }

    private async Task RunCountdownAsync(double delay, TextBlock statusText, ProgressBar progressBar, CancellationToken token)
    {
        for (int remaining = (int)delay; remaining > 0; remaining--)
        {
            token.ThrowIfCancellationRequested();
            CheckPanicSafety();
            statusText.Text = $"Starting in {remaining}s... Switch to target app!";
            progressBar.Value = (delay - remaining) / delay * 100;
            await Task.Delay(1000, token);
        }
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
            StopTask(_runningTaskName == taskName ? "Stopped by user" : $"Switched task from {_runningTaskName}");
            return;
        }

        if (!ValidatePreflightTaskInputs(taskName, statusText))
        {
            return;
        }

        _isRunning = true;
        _runningTaskName = taskName;
        UpdateAllActionBtnStates();

        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        _panicDetector.Reset();

        actionText.Text = "Stop";
        actionIcon.Glyph = "\uE71A";
        actionBtn.IsEnabled = true;

        double delay = delayBox.Value;
        if (double.IsNaN(delay) || delay < 0) delay = 0;

        try
        {
            await RunCountdownAsync(delay, statusText, progressBar, token);

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
            string msg = ex.Message.Contains("panic", StringComparison.OrdinalIgnoreCase) || ex.Message.Contains("Esc", StringComparison.OrdinalIgnoreCase)
                ? ex.Message
                : "Task canceled";
            StopTask(msg);
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

        bool isClipboard = TypeModeCombo.SelectedIndex == 1;
        int trailingIndex = TypeTrailingKeyCombo.SelectedIndex;
        string trailingKey = trailingIndex switch
        {
            1 => KeyEnter,
            2 => KeyTab,
            _ => KeyNone
        };

        if (isClipboard)
        {
            await RunClipboardTyperAsync(text, trailingKey, token, reportProgress);
        }
        else
        {
            double interval = TypeIntervalBox.Value;
            int intervalMs = (int)(interval * 1000);
            bool jitter = TypeJitterCheck.IsChecked.GetValueOrDefault();
            await RunKeystrokeTyperAsync(text, intervalMs, jitter, trailingKey, token, reportProgress);
        }

        DispatcherQueue.TryEnqueue(() =>
        {
            string modeDesc = isClipboard ? "Pasted" : "Typed";
            string summary = $"{DateTime.Now:HH:mm:ss} - {modeDesc} {text.Length} chars" + (trailingKey != KeyNone ? $" + [{trailingKey}]" : "");
            HistoryList.Items.Insert(0, summary);
        });
    }

    private async Task RunClipboardTyperAsync(string text, string trailingKey, CancellationToken token, Action<string, double> reportProgress)
    {
        string? previousText = null;
        try
        {
            var currentContent = Clipboard.GetContent();
            if (currentContent.Contains(StandardDataFormats.Text))
            {
                previousText = await currentContent.GetTextAsync();
            }
        }
        catch
        {
            // Ignore failure to read previous clipboard content
        }

        var package = new DataPackage();
        package.SetText(text);
        Clipboard.SetContent(package);

        try
        {
            await Task.Run(() =>
            {
                token.ThrowIfCancellationRequested();
                CheckPanicSafety();
                reportProgress($"Pasting {text.Length} chars via clipboard...", 50);

                Keyboard.Paste();

                token.ThrowIfCancellationRequested();
                CheckPanicSafety();

                ApplyTrailingKey(trailingKey, token);
                reportProgress($"Pasted {text.Length} chars", 100);
            }, token);
        }
        finally
        {
            if (!string.IsNullOrEmpty(previousText))
            {
                await Task.Delay(150, CancellationToken.None);
                try
                {
                    var restorePackage = new DataPackage();
                    restorePackage.SetText(previousText);
                    Clipboard.SetContent(restorePackage);
                    Clipboard.Flush();
                }
                catch
                {
                    // Ignore failure to restore clipboard
                }
            }
        }
    }

    private async Task RunKeystrokeTyperAsync(string text, int intervalMs, bool jitter, string trailingKey, CancellationToken token, Action<string, double> reportProgress)
    {
        await Task.Run(() =>
        {
            for (int i = 0; i < text.Length; i++)
            {
                token.ThrowIfCancellationRequested();
                CheckPanicSafety();
                Keyboard.Type(text[i]);

                int charIndex = i + 1;
                reportProgress($"Typing character {charIndex}/{text.Length}...", (double)charIndex / text.Length * 100);

                int sleepMs = jitter && intervalMs > 0
                    ? JitterHelper.ApplyTimeJitter(intervalMs, 15, minIntervalMs: 1)
                    : intervalMs;

                if (sleepMs > 0)
                {
                    SleepWithPanicCheck(sleepMs, token);
                }
            }

            token.ThrowIfCancellationRequested();
            CheckPanicSafety();
            ApplyTrailingKey(trailingKey, token);
        }, token);
    }

    private static void ApplyTrailingKey(string trailingKey, CancellationToken token)
    {
        if (token.IsCancellationRequested || Keyboard.IsEscPressed())
        {
            return;
        }

        if (trailingKey is KeyEnter or KeyTab)
        {
            Thread.Sleep(50);
            if (!token.IsCancellationRequested && !Keyboard.IsEscPressed())
            {
                Keyboard.Tap(trailingKey);
            }
        }
    }

    private async Task RunKeyHolderAsync(CancellationToken token, Action<string, double> reportProgress)
    {
        var stagesToRun = _holderStages.Where(s => !string.IsNullOrWhiteSpace(s.KeyCombo)).Select(s => s.Clone()).ToList();
        if (stagesToRun.Count == 0)
        {
            throw new InvalidOperationException("No valid steps in pipeline!");
        }

        int repeatLoops = (int)HolderRepeatBox.Value;
        if (repeatLoops < 0) repeatLoops = 0;

        await Task.Run(() =>
        {
            try
            {
                int currentLoop = 0;
                while (!token.IsCancellationRequested && (repeatLoops == 0 || currentLoop < repeatLoops))
                {
                    currentLoop++;
                    string loopPrefix = repeatLoops == 1 ? "" : (repeatLoops == 0 ? $"[Loop {currentLoop}/∞] " : $"[Loop {currentLoop}/{repeatLoops}] ");

                    for (int i = 0; i < stagesToRun.Count; i++)
                    {
                        token.ThrowIfCancellationRequested();
                        CheckPanicSafety();

                        int activeIdx = i;
                        DispatcherQueue.TryEnqueue(() => HighlightRunningStage(activeIdx));

                        var stage = stagesToRun[i];
                        string stepPrefix = stagesToRun.Count > 1 ? $"{loopPrefix}Step {i + 1}/{stagesToRun.Count}: " : loopPrefix;
                        int durationMs = (int)(stage.HoldDurationSec * 1000);

                        // Hold phase
                        try
                        {
                            Keyboard.Press(stage.KeyCombo);
                            int elapsedMs = 0;
                            int checkIntervalMs = 50;

                            while (!token.IsCancellationRequested && (durationMs <= 0 || elapsedMs < durationMs))
                            {
                                CheckPanicSafety();

                                if (durationMs > 0)
                                {
                                    double pct = (double)elapsedMs / durationMs * 100;
                                    reportProgress($"{stepPrefix}Holding [{stage.KeyCombo}] ({(elapsedMs / 1000.0):F1}s / {stage.HoldDurationSec:F1}s)...", pct);
                                }
                                else
                                {
                                    reportProgress($"{stepPrefix}Holding [{stage.KeyCombo}] continuously (Press Stop or F10 to release)...", 100);
                                }

                                SleepWithPanicCheck(checkIntervalMs, token);
                                elapsedMs += checkIntervalMs;
                            }
                        }
                        finally
                        {
                            Keyboard.Release(stage.KeyCombo);
                            Keyboard.ReleaseAllModifiers();
                        }

                        // Rest phase between steps (if configured)
                        if (stage.RestDurationSec > 0 && !token.IsCancellationRequested)
                        {
                            int restMs = (int)(stage.RestDurationSec * 1000);
                            int restElapsedMs = 0;
                            int checkIntervalMs = 50;

                            while (!token.IsCancellationRequested && restElapsedMs < restMs)
                            {
                                CheckPanicSafety();
                                double pct = (double)restElapsedMs / restMs * 100;
                                reportProgress($"{stepPrefix}Resting ({(restElapsedMs / 1000.0):F1}s / {stage.RestDurationSec:F1}s)...", pct);
                                SleepWithPanicCheck(checkIntervalMs, token);
                                restElapsedMs += checkIntervalMs;
                            }
                        }
                    }
                }
            }
            finally
            {
                DispatcherQueue.TryEnqueue(ClearRunningStageHighlight);
            }
        }, token);
    }

    private async Task RunAutoClickerAsync(CancellationToken token, Action<string, double> reportProgress)
    {
        bool isSpammer = ClickTargetTypeCombo.SelectedIndex == 1;
        string spamKey = SpamKeyBox.Text.Trim();
        bool isHoldMode = ClickTriggerModeCombo.SelectedIndex == 1;

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

        bool jitterEnabled = TimeJitterCheck.IsChecked.GetValueOrDefault();
        double timeJitterPct = TimeJitterBox.Value;
        double locJitterPx = LocationJitterBox.Value;

        // Ensure target marker overlay is fresh and visible
        UpdateTargetMarkerOverlay();

        bool markerEnabled = ShowTargetMarkerCheck.IsChecked.GetValueOrDefault(false) && !isSpammer;
        const int HOTKEY_VK = 0x75;

        await Task.Run(() =>
        {
            int count = 0;
            while (!token.IsCancellationRequested && (infinite || count < totalClicks))
            {
                CheckPanicSafety();

                // If Hold-to-Click is enabled, automatically stop when the hotkey (F6) is released
                if (isHoldMode && !KeySpammer.IsKeyDown(HOTKEY_VK))
                {
                    break;
                }

                ExecuteSingleClickIteration(isSpammer, spamKey, targetX, targetY, locJitterPx, markerEnabled, button);
                count++;

                ReportClickProgress(isSpammer, spamKey, count, totalClicks, infinite, reportProgress);

                int sleepMs = jitterEnabled && timeJitterPct > 0
                    ? JitterHelper.ApplyTimeJitter(intervalMs, timeJitterPct)
                    : intervalMs;

                if (sleepMs > 0)
                {
                    SleepWithPanicCheck(sleepMs, token);
                }
            }
        }, token);
    }

    private void ExecuteSingleClickIteration(
        bool isSpammer,
        string spamKey,
        int? targetX,
        int? targetY,
        double locJitterPx,
        bool markerEnabled,
        string button)
    {
        if (isSpammer)
        {
            KeySpammer.Spam(spamKey);
            return;
        }

        if (targetX.HasValue && targetY.HasValue)
        {
            var (clickX, clickY) = JitterHelper.ApplyLocationJitter(targetX.Value, targetY.Value, locJitterPx);
            if (markerEnabled)
            {
                DispatcherQueue.TryEnqueue(() => _targetMarkerOverlay.ShowClickRipple(clickX, clickY));
            }
            _panicDetector.SyncProgrammaticPosition(clickX, clickY);
            Mouse.ClickAt(clickX, clickY, button);
            _panicDetector.SyncProgrammaticPosition(clickX, clickY);
        }
        else
        {
            var (curX, curY) = Mouse.GetPosition();
            if (markerEnabled)
            {
                DispatcherQueue.TryEnqueue(() => _targetMarkerOverlay.ShowClickRipple(curX, curY));
            }
            Mouse.Click(button);
        }
    }

    private static void ReportClickProgress(
        bool isSpammer,
        string spamKey,
        int count,
        int totalClicks,
        bool infinite,
        Action<string, double> reportProgress)
    {
        string actionName = isSpammer ? $"Spamming [{spamKey}]" : "Clicking";
        if (!infinite)
        {
            reportProgress($"{actionName} {count}/{totalClicks}...", (double)count / totalClicks * 100);
        }
        else
        {
            reportProgress($"{actionName} count: {count} (Infinite)...", 100);
        }
    }

    private async Task RunMacroReplayAsync(CancellationToken token, Action<string, double> reportProgress)
    {
        if (_macroRecorder.Actions.Count == 0)
        {
            throw new InvalidOperationException("No recorded macro actions to replay! Please record first.");
        }

        int loops = (int)MacroRepeatBox.Value;
        double speed = MacroSpeedBox.Value;

        // Display all recorded click markers with sequential numbering ①, ②, ③...
        var clickPoints = _macroRecorder.Actions
            .Where(act => act.Type is MacroActionType.ClickLeft or MacroActionType.ClickRight or MacroActionType.ClickMiddle)
            .Select((act, idx) => new MarkerPoint(idx + 1, act.X, act.Y))
            .ToList();

        DispatcherQueue.TryEnqueue(() =>
        {
            _targetMarkerOverlay.SetMarkers(clickPoints);
        });

        int currentClickInLoop = 0;

        try
        {
            await _macroRecorder.ReplayAsync(loops, speed, token, (loop, step) =>
            {
                if (step == 1)
                {
                    currentClickInLoop = 0;
                }
                double pct = (double)step / _macroRecorder.Actions.Count * 100;
                reportProgress($"Replaying Macro: Loop {loop}, Action {step}/{_macroRecorder.Actions.Count}", pct);
            }, action =>
            {
                if (action.Type is MacroActionType.ClickLeft or MacroActionType.ClickRight or MacroActionType.ClickMiddle)
                {
                    currentClickInLoop++;
                    int thisClick = currentClickInLoop;
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        _targetMarkerOverlay.ShowClickRipple(action.X, action.Y, thisClick);
                    });
                    _panicDetector.SyncProgrammaticPosition(action.X, action.Y);
                }
                else if (action.Type is MacroActionType.MouseMove)
                {
                    _panicDetector.SyncProgrammaticPosition(action.X, action.Y);
                }
            },
            checkPanicCallback: CheckPanicSafety);
        }
        finally
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                _targetMarkerOverlay.ClearAndHide();
            });
        }
    }

    private void CheckPanicSafety()
    {
        if (_panicDetector.CheckPanic(out var reason))
        {
            throw new OperationCanceledException(reason ?? PanicDetector.EmergencyPanicMessage);
        }
    }

    private void SleepWithPanicCheck(int totalMs, CancellationToken token)
    {
        const int stepMs = 20;
        int elapsed = 0;
        while (elapsed < totalMs)
        {
            token.ThrowIfCancellationRequested();
            CheckPanicSafety();
            int chunk = Math.Min(stepMs, totalMs - elapsed);
            Thread.Sleep(chunk);
            elapsed += chunk;
        }
    }

    private void StopTask(string message)
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;

        _isRunning = false;

        _targetMarkerOverlay.ClearAndHide();
        UpdateTargetMarkerOverlay();

        // Reset Typer UI
        TyperActionText.Text = "Start Typer (F8)";
        TyperActionIcon.Glyph = "\uE768";
        TyperProgressBar.Value = 0;

        // Reset Holder UI
        HolderActionText.Text = "Start Holder (F7)";
        HolderActionIcon.Glyph = "\uE768";
        HolderProgressBar.Value = 0;

        // Reset Clicker UI
        ClickerActionText.Text = "Start Clicker (F6)";
        ClickerActionIcon.Glyph = "\uE768";
        ClickerProgressBar.Value = 0;

        // Reset Macro UI
        MacroActionText.Text = "Replay Macro (F9)";
        MacroActionIcon.Glyph = "\uE768";
        MacroProgressBar.Value = 0;

        // Set status message on the active/relevant panel
        if (_runningTaskName == TabTyper) TyperStatusText.Text = message;
        else if (_runningTaskName == TabHolder) HolderStatusText.Text = message;
        else if (_runningTaskName == TabClicker) ClickerStatusText.Text = message;
        else if (_runningTaskName == TabMacro) MacroStatusText.Text = message;

        _runningTaskName = null;
        Keyboard.ReleaseAllModifiers();
        UpdateAllActionBtnStates();
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "S2325:Methods and properties that don't access instance data should be static", Justification = "XAML event handler")]
    private void AlwaysOnTopToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (MainWindow.Instance != null && MainWindow.Instance.IsAlwaysOnTop != AlwaysOnTopToggle.IsOn)
        {
            MainWindow.Instance.SetAlwaysOnTop(AlwaysOnTopToggle.IsOn);
        }
    }

    private void OnMainWindowAlwaysOnTopChanged(bool isTop)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            AlwaysOnTopToggle.IsOn = isTop;
        });
    }
}
