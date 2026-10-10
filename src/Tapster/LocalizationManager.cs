using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;

namespace Tapster;

public static class LocalizationManager
{
    public const string LangAuto = "auto";
    public const string LangZhTw = "zh-TW";
    public const string LangEnUs = "en-US";
    public const string LangJaJp = "ja-JP";
    public const string LangZhCn = "zh-CN";

    public static readonly IReadOnlyList<(string Code, string DisplayName)> SupportedLanguages = new[]
    {
        (LangAuto, "跟隨系統"),
        (LangZhTw, "繁體中文"),
        (LangEnUs, "English"),
        (LangJaJp, "日本語"),
        (LangZhCn, "简体中文"),
    };

    public static string GetLanguageDisplayName(string code)
    {
        return code switch
        {
            LangAuto => Get("Settings_LangAuto"),
            LangZhTw => "繁體中文",
            LangEnUs => "English",
            LangJaJp => "日本語",
            LangZhCn => "简体中文",
            _ => code,
        };
    }

    private static string _currentLanguage = LangAuto;
    private static string _effectiveLanguage = LangEnUs;

    public static event Action? LanguageChanged;

    static LocalizationManager()
    {
        SetLanguage(LangAuto);
    }

    public static string CurrentLanguage => _currentLanguage;
    public static string EffectiveLanguage => _effectiveLanguage;

    public static void SetLanguage(string langCode)
    {
        _currentLanguage = string.IsNullOrWhiteSpace(langCode) ? LangAuto : langCode;

        if (string.Equals(_currentLanguage, LangAuto, StringComparison.OrdinalIgnoreCase))
        {
            _effectiveLanguage = ResolveSystemLanguage();
        }
        else
        {
            _effectiveLanguage = NormalizeLanguageCode(_currentLanguage);
        }

        LanguageChanged?.Invoke();
    }

    private static string ResolveSystemLanguage()
    {
        var currentCulture = CultureInfo.CurrentUICulture.Name;
        if (currentCulture.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
        {
            if (currentCulture.Contains("TW", StringComparison.OrdinalIgnoreCase) ||
                currentCulture.Contains("HK", StringComparison.OrdinalIgnoreCase) ||
                currentCulture.Contains("MO", StringComparison.OrdinalIgnoreCase) ||
                currentCulture.Contains("Hant", StringComparison.OrdinalIgnoreCase))
            {
                return LangZhTw;
            }
            return LangZhCn;
        }

        if (currentCulture.StartsWith("ja", StringComparison.OrdinalIgnoreCase))
        {
            return LangJaJp;
        }

        return LangEnUs;
    }

    private static string NormalizeLanguageCode(string code)
    {
        if (code.StartsWith("zh-TW", StringComparison.OrdinalIgnoreCase) ||
            code.StartsWith("zh-HK", StringComparison.OrdinalIgnoreCase) ||
            code.StartsWith("zh-Hant", StringComparison.OrdinalIgnoreCase))
        {
            return LangZhTw;
        }

        if (code.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
        {
            return LangZhCn;
        }

        if (code.StartsWith("ja", StringComparison.OrdinalIgnoreCase))
        {
            return LangJaJp;
        }

        return LangEnUs;
    }

    public static string Get(string key)
    {
        if (_rawTranslations.TryGetValue(_effectiveLanguage, out var dict) && dict.TryGetValue(key, out var val))
        {
            return val;
        }

        if (_rawTranslations.TryGetValue(LangEnUs, out var fallbackDict) && fallbackDict.TryGetValue(key, out var fallbackVal))
        {
            return fallbackVal;
        }

        return key;
    }

    private static readonly Dictionary<string, Dictionary<string, string>> _rawTranslations = InitializeTranslations();

    public static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Translations { get; } =
        new ReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>(
            _rawTranslations.ToDictionary(
                kvp => kvp.Key,
                kvp => (IReadOnlyDictionary<string, string>)new ReadOnlyDictionary<string, string>(kvp.Value),
                StringComparer.OrdinalIgnoreCase));

    private static Dictionary<string, Dictionary<string, string>> InitializeTranslations()
    {
        var zhTw = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var enUs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var jaJp = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var zhCn = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        void Add(string key, string tw, string en, string ja, string cn)
        {
            zhTw[key] = tw;
            enUs[key] = en;
            jaJp[key] = ja;
            zhCn[key] = cn;
        }

        RegisterNavigation(Add);
        RegisterTray(Add);
        RegisterCommon(Add);
        RegisterTyper(Add);
        RegisterKeyHolder(Add);
        RegisterClicker(Add);
        RegisterMacro(Add);
        RegisterSettings(Add);
        RegisterAbout(Add);

        return new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
        {
            [LangZhTw] = zhTw,
            [LangEnUs] = enUs,
            [LangJaJp] = jaJp,
            [LangZhCn] = zhCn,
        };
    }

    private static void RegisterNavigation(Action<string, string, string, string, string> add)
    {
        add("Nav_Typer", "打字", "Typer", "タイピング", "打字");
        add("Nav_Holder", "長按", "Holder", "長押し", "长按");
        add("Nav_Clicker", "連點", "Clicker", "連打", "连点");
        add("Nav_Macro", "重放", "Replay", "再生", "重放");
        add("Nav_About", "關於", "About", "情報", "关于");
        add("Nav_Settings", "設定", "Settings", "設定", "设置");
    }

    private static void RegisterTray(Action<string, string, string, string, string> add)
    {
        add("Tray_ShowHide", "顯示 / 隱藏 Tapster", "Show / Hide Tapster", "Tapsterの表示 / 非表示", "显示 / 隐藏 Tapster");
        add("Tray_AlwaysOnTop", "視窗永遠置頂", "Always on Top", "常に最前面に表示", "窗口置顶");
        add("Tray_Exit", "結束 Tapster", "Exit Tapster", "Tapsterを終了", "退出 Tapster");
        add("Tray_Tooltip", "Tapster (Ctrl+Alt+T 喚醒)", "Tapster (Ctrl+Alt+T to Wake)", "Tapster (Ctrl+Alt+T で起動)", "Tapster (Ctrl+Alt+T 唤醒)");
    }

    private static void RegisterCommon(Action<string, string, string, string, string> add)
    {
        add("Common_Ready", "就緒", "Ready", "待機中", "就绪");
        add("Common_Running", "執行中...", "Running...", "実行中...", "运行中...");
        add("Common_StartingIn", "{0} 秒後啟動... 請切換至目標應用程式！", "Starting in {0}s... Switch to target app!", "{0} 秒後に開始... 対象アプリへ切り替えてください！", "{0} 秒后启动... 请切换至目标程序！");
        add("Common_Stop", "停止", "Stop", "停止", "停止");
        add("Common_Start", "開始", "Start", "開始", "开始");
        add("Common_Delay", "延遲 (秒)：", "Delay (s):", "遅延 (秒)：", "延迟 (秒)：");
        add("Common_Finished", "執行完成", "Finished successfully", "正常に終了しました", "运行完成");
        add("Common_StoppedByUser", "使用者已手動停止", "Stopped by user", "ユーザーにより停止されました", "用户已手动停止");
        add("Common_TaskCanceled", "工作已取消", "Task canceled", "タスクがキャンセルされました", "任务已取消");
        add("Common_EmergencyStop", "已透過 F10 緊急熱鍵終止所有工作", "Emergency stop triggered via F10 panic key", "F10パニックキーにより緊急停止しました", "已通过 F10 紧急热键终止所有任务");
        add("Common_SwitchedTask", "已從 {0} 切換工作", "Switched task from {0}", "{0} からタスクを切り替えました", "已从 {0} 切换任务");
        add("Common_Error", "錯誤：{0}", "Error: {0}", "エラー：{0}", "错误：{0}");
        add("Common_Save", "儲存", "Save", "保存", "保存");
        add("Common_Cancel", "取消", "Cancel", "キャンセル", "取消");
    }

    private static void RegisterTyper(Action<string, string, string, string, string> add)
    {
        add("Typer_Title", "自動打字機", "Auto Typer", "自動タイパー", "自动打字机");
        add("Typer_Desc", "自動模擬鍵盤輸入指定文字或程式碼，支援遠端桌面與主機終端", "Simulates keyboard input for VNC, VM consoles, or automated text entry", "リモートデスクトップやコンソール向けにテキストを自動キーボード入力", "自动模拟键盘输入预设文本或代码，支持远程桌面与终端");
        add("Typer_TargetText", "目標文字：", "Target Text:", "対象テキスト：", "目标文本：");
        add("Typer_PasteBtn", "貼上", "Paste", "貼り付け", "粘贴");
        add("Typer_ClearBtn", "清除", "Clear", "クリア", "清空");
        add("Typer_TextPlaceholder", "在此輸入要自動輸入的文字內容...", "Enter text to automatically type...", "自動入力するテキストを入力してください...", "在此输入要自动输入的文本内容...");
        add("Typer_ModeLabel", "輸入模式：", "Typing Mode:", "入力モード：", "输入模式：");
        add("Typer_ModeKeystroke", "⌨️ 逐字鍵盤模擬", "⌨️ Keystroke Emulation", "⌨️ キーストローク模倣", "⌨️ 逐字键盘模拟");
        add("Typer_ModeClipboard", "📋 剪貼簿貼上 (瞬間)", "📋 Clipboard Paste (Instant)", "📋 クリップボード貼り付け (即時)", "📋 剪贴板粘贴 (瞬间)");
        add("Typer_ClipboardHint", "⚡ 瞬間貼上 (Ctrl+V) 並自動還原剪貼簿原始內容", "⚡ Instant paste (Ctrl+V) with clipboard auto-restore", "⚡ Ctrl+Vによる即時貼り付け＆クリップボード自動復元", "⚡ 瞬间粘贴 (Ctrl+V) 并自动恢复剪贴板原始内容");
        add("Typer_TrailingKeyLabel", "結尾按鍵：", "Trailing Key:", "末尾キー：", "尾随按键：");
        add("Typer_TrailingNone", "無", "None", "なし", "无");
        add("Typer_TrailingEnter", "Enter (送出 / 換行)", "Enter (Send / Newline)", "Enter (送信 / 改行)", "Enter (发送 / 换行)");
        add("Typer_TrailingTab", "Tab (切換欄位)", "Tab (Switch Field)", "Tab (フィールド移動)", "Tab (切换栏位)");
        add("Typer_IntervalLabel", "按鍵間隔 (秒/字)：", "Interval (sec/char):", "キー間隔 (秒/文字)：", "按键间隔 (秒/字)：");
        add("Typer_JitterCheck", "自然打字微幅抖動 (±15%)", "Natural Typing Jitter (±15%)", "自然な打鍵ゆらぎ (±15%)", "自然打字微幅抖动 (±15%)");
        add("Typer_HistoryTitle", "輸入紀錄", "Execution History", "実行履歴", "输入记录");
        add("Typer_ActionStart", "開始打字 (F8)", "Start Typer (F8)", "タイピング開始 (F8)", "开始打字 (F8)");
        add("Typer_ValidationEmpty", "請輸入要打字的文字內容", "Please enter text to type", "入力するテキストを入力してください", "请输入要打字的文本内容");
        add("Typer_PastingClipboard", "正在透過剪貼簿貼上 {0} 個字元...", "Pasting {0} chars via clipboard...", "クリップボード経由で {0} 文字を貼り付け中...", "正在通过剪贴板粘贴 {0} 个字符...");
        add("Typer_PastedClipboard", "已貼上 {0} 個字元", "Pasted {0} chars", "{0} 文字を貼り付けました", "已粘贴 {0} 个字符");
        add("Typer_TypingChar", "正在輸入第 {0}/{1} 個字元...", "Typing character {0}/{1}...", "{0}/{1} 文字目を入力中...", "正在输入第 {0}/{1} 个字符...");
        add("Typer_PasteError", "貼上錯誤：{0}", "Paste error: {0}", "貼り付けエラー：{0}", "粘贴错误：{0}");
    }

    private static void RegisterKeyHolder(Action<string, string, string, string, string> add)
    {
        add("KeyHolder_Title", "按鍵長按", "Key Holder", "キーホールド", "按键长按");
        add("KeyHolder_Desc", "連續按住指定按鍵或組合鍵，支援奔跑、蓄力與按鍵模擬", "Holds down specified key or key combination continuously", "指定したキーまたはキー組み合わせを継続して長押し", "连续长按指定按键或组合键，支持奔跑、蓄力与按键模拟");
        add("KeyHolder_PipelineTitle", "按鍵序列管線", "Key Pipeline Sequence", "キーパイプラインシーケンス", "按键流水线序列");
        add("KeyHolder_PipelineHint", "• 點擊任意步驟，透過下方鍵盤自訂按鍵組合", "• Click any step to edit its combo using the keyboard below", "• 下のキーボードから任意のステップのキー設定を編集", "• 点击任意步骤，通过下方键盘自定义按键组合");
        add("KeyHolder_RepeatTitle", "循環次數：", "Repeat Loops:", "繰り返し回数：", "循环次数：");
        add("KeyHolder_AddStepBtn", "新增步驟至序列", "Add Step to Sequence", "シーケンスにステップを追加", "添加步骤至序列");
        add("KeyHolder_VkTitle", "虛擬鍵盤控制器", "Virtual Keyboard Controller", "仮想キーボードコントローラー", "虚拟键盘控制器");
        add("KeyHolder_CaptureBtn", "擷取實體按鍵", "Capture Physical Key", "実キー取得", "捕获实体按键");
        add("KeyHolder_CaptureListening", "聆聽中...", "Listening...", "待機中...", "监听中...");
        add("KeyHolder_CaptureHint", "請按下鍵盤上的任意按鍵...", "Press any key on your keyboard...", "キーボードの任意のキーを押してください...", "请按下键盘上的任意按键...");
        add("KeyHolder_CaptureCanceled", "按鍵擷取已取消", "Key capture canceled", "キー取得がキャンセルされました", "按键捕获已取消");
        add("KeyHolder_CaptureSuccess", "已擷取按鍵：{0} (步驟 #{1})", "Captured key: {0} for Step #{1}", "キーを取得しました：{0} (ステップ #{1})", "已捕获按键：{0} (步骤 #{1})");
        add("KeyHolder_ClearKeysBtn", "清除按鍵", "Clear Keys", "キー消去", "清空按键");
        add("KeyHolder_Step", "步驟 #{0}", "Step #{0}", "ステップ #{0}", "步骤 #{0}");
        add("KeyHolder_TapKeysBelow", "(點擊下方按鍵)", "(Tap keys below)", "(下のキーを押す)", "(点击下方按键)");
        add("KeyHolder_HoldUntilStopped", "長按至停止", "Hold until stopped", "停止するまでホールド", "长按至停止");
        add("KeyHolder_HoldForSeconds", "長按 (秒)：", "Hold for (s):", "長押し時間 (秒)：", "长按 (秒)：");
        add("KeyHolder_RestSeconds", "休息 (秒)：", "Rest (s):", "待機時間 (秒)：", "休息 (秒)：");
        add("KeyHolder_MoveUp", "上移步驟", "Move step up", "ステップを上に移動", "上移步骤");
        add("KeyHolder_MoveDown", "下移步驟", "Move step down", "ステップを下に移動", "下移步骤");
        add("KeyHolder_DeleteStep", "刪除步驟", "Delete step", "ステップを削除", "删除步骤");
        add("KeyHolder_ActionStart", "開始長按 (F7)", "Start Holder (F7)", "ホールド開始 (F7)", "开始长按 (F7)");
        add("KeyHolder_ValidationNoKeys", "請為序列中的步驟設定至少一個按鍵組合", "Please configure a key combo for the sequence", "シーケンスのキーの組み合わせを設定してください", "请为序列中的步骤配置至少一个按键组合");
        add("KeyHolder_EditingStep", "(編輯步驟 #{0}: [{1}])", "(Editing Step #{0}: [{1}])", "(編集中 ステップ #{0}: [{1}])", "(正在编辑步骤 #{0}: [{1}])");
        add("KeyHolder_NoKeysSet", "未設定按鍵", "No keys set", "キー未設定", "未设置按键");
        add("KeyHolder_KeysClearedForStep", "已清除步驟 #{0} 的按鍵", "Cleared keys for Step #{0}", "ステップ #{0} のキーをクリアしました", "已清除步骤 #{0} 的按键");
        add("KeyHolder_PreflightNoKey", "請設定要長按的按鍵", "Please enter a key to hold", "ホールドするキーを入力してください", "请设置要长按的按键");
        add("KeyHolder_HoldingDuration", "{0}長按 [{1}] ({2:F1}s / {3:F1}s)...", "{0}Holding [{1}] ({2:F1}s / {3:F1}s)...", "{0}[{1}] をホールド中 ({2:F1}s / {3:F1}s)...", "{0}长按 [{1}] ({2:F1}s / {3:F1}s)...");
        add("KeyHolder_HoldingContinuously", "{0}持續長按 [{1}] (按停止或 F10 放開)...", "{0}Holding [{1}] continuously (Press Stop or F10 to release)...", "{0}[{1}] を継続ホールド中 (停止またはF10で解除)...", "{0}持续长按 [{1}] (按停止或 F10 释放)...");
        add("KeyHolder_RestingDuration", "{0}休息中 ({1:F1}s / {2:F1}s)...", "{0}Resting ({1:F1}s / {2:F1}s)...", "{0}待機中 ({1:F1}s / {2:F1}s)...", "{0}休息中 ({1:F1}s / {2:F1}s)...");
        add("KeyHolder_LoopPrefix", "[第 {0}/{1} 輪] ", "[Loop {0}/{1}] ", "[ループ {0}/{1}] ", "[第 {0}/{1} 轮] ");
        add("KeyHolder_LoopInfinitePrefix", "[第 {0}/∞ 輪] ", "[Loop {0}/∞] ", "[ループ {0}/∞] ", "[第 {0}/∞ 轮] ");
        add("KeyHolder_StepPrefix", "步驟 {0}/{1}：", "Step {0}/{1}: ", "ステップ {0}/{1}：", "步骤 {0}/{1}：");
    }

    private static void RegisterClicker(Action<string, string, string, string, string> add)
    {
        add("Clicker_Title", "滑鼠連點與按鍵連打", "Auto Clicker", "連打ツール", "鼠标连点与按键连打");
        add("Clicker_Desc", "高精度定時滑鼠連點、指定座標鎖定與鍵盤連打工具", "Automates mouse clicks with precise interval and coordinate targeting", "高精度なマウス自動クリックおよびキーボード連打ツール", "高精度定时鼠标连点、指定坐标锁定与键盘连打工具");
        add("Clicker_TargetTypeLabel", "連打目標：", "Target Type:", "対象タイプ：", "连打目标：");
        add("Clicker_TargetMouse", "🖱️ 滑鼠連點", "🖱️ Mouse Click", "🖱️ マウスクリック", "🖱️ 鼠标连点");
        add("Clicker_TargetKeyboard", "⌨️ 鍵盤連打", "⌨️ Keyboard Spammer", "⌨️ キーボード連打", "⌨️ 键盘连打");
        add("Clicker_TriggerModeLabel", "觸發行為：", "Trigger Mode:", "トリガー挙動：", "触发行为：");
        add("Clicker_TriggerToggle", "開關切換 (按一下開始/停止)", "Toggle (Press to Start/Stop)", "トグル (押して開始/停止)", "开关切换 (按一下开始/停止)");
        add("Clicker_TriggerHold", "按住連打 (按住 F6 連打)", "Hold to Click (Hold F6)", "押しっぱなし連打 (F6 長押し)", "按住连打 (按住 F6 连打)");
        add("Clicker_HoldModeHint", "💡 按住熱鍵持續連打，放開即刻停止", "💡 Hold hotkey to spam, release to stop", "💡 ホットキーを押している間連打し、離すと停止", "💡 按住热键持续连打，松开即刻停止");
        add("Clicker_MouseButtonLabel", "點擊按鈕：", "Mouse Button:", "マウスボタン：", "点击按钮：");
        add("Clicker_MouseLeft", "左鍵", "Left Click", "左クリック", "左键");
        add("Clicker_MouseRight", "右鍵", "Right Click", "右クリック", "右键");
        add("Clicker_MouseMiddle", "中鍵", "Middle Click", "中クリック", "中键");
        add("Clicker_SpamKeyLabel", "連打按鍵：", "Spam Key / Combo:", "連打するキー：", "连打按键：");
        add("Clicker_SpamKeyPlaceholder", "輸入要連打的按鍵 (例：space, f, 1)", "Key to spam (e.g. space, f, 1, e)", "連打するキー (例：space, f, 1, e)", "输入要连打的按键 (例：space, f, 1)");
        add("Clicker_IntervalLabel", "間隔 (秒)：", "Interval (sec):", "間隔 (秒)：", "间隔 (秒)：");
        add("Clicker_CountLabel", "連擊次數 (0 = 無限)：", "Repeat Count (0 = Infinite):", "クリック回数 (0 = 無限)：", "连击次数 (0 = 无限)：");
        add("Clicker_TargetCoordsLabel", "目標座標：", "Target Coordinates:", "クリック座標：", "目标坐标：");
        add("Clicker_PickCoordBtn", "鎖定座標", "Pick Location", "座標取得", "锁定坐标");
        add("Clicker_ClearCoordBtn", "清除", "Clear", "クリア", "清空");
        add("Clicker_ShowMarkerCheck", "於螢幕顯示準心標記", "Show Target Marker on Screen", "画面上に十字ターゲットを表示", "在屏幕上显示准星标记");
        add("Clicker_TimeJitterLabel", "時間隨機抖動 (±%)：", "Time Jitter (±%):", "時間ゆらぎ (±%)：", "时间随机抖动 (±%)：");
        add("Clicker_TimeJitterCheck", "隨機間隔", "Randomize interval", "間隔をランダム化", "随机间隔");
        add("Clicker_LocationJitterLabel", "位置隨機抖動 (±px)：", "Location Jitter (±px):", "座標ゆらぎ (±px)：", "位置随机抖动 (±px)：");
        add("Clicker_LocationJitterHint", "像素半徑 (0 = 鎖定精確點)", "px radius (0 = exact point)", "半径ピクセル (0 = 正確な位置)", "像素半径 (0 = 锁定精确点)");
        add("Clicker_ActionStart", "開始點擊 (F6)", "Start Clicker (F6)", "クリック開始 (F6)", "开始点击 (F6)");
        add("Clicker_ValidationSpamKey", "請輸入要連打的按鍵名稱", "Please enter a key to spam", "連打するキーを入力してください", "请输入要连打的按键名称");
        add("Clicker_PickingCoordsStatus", "移動滑鼠至目標位置。按空格/Enter或點擊以鎖定 (Esc 取消)...", "Move cursor to target. Press Space/Enter or click to lock (Esc to cancel)...", "マウスを対象へ移動。Space/Enterまたはクリックで決定 (Esc でキャンセル)...", "移动鼠标至目标位置。按空格/Enter或点击以锁定 (Esc 取消)...");
        add("Clicker_PickCoordsCanceled", "座標選取已取消", "Coordinate picking canceled", "座標取得がキャンセルされました", "坐标选取已取消");
        add("Clicker_PickCoordsLocked", "已鎖定目標座標：({0}, {1})", "Locked target coordinates: ({0}, {1})", "ターゲット座標を固定しました：({0}, {1})", "已锁定目标坐标：({0}, {1})");
        add("Clicker_CoordsCleared", "已清除座標 (於目前游標位置點擊)", "Coordinates cleared (clicking at current cursor location)", "座標をクリアしました (現在のカーソル位置でクリック)", "已清除坐标 (在当前光标位置点击)");
        add("Clicker_ClickingCount", "點擊中 {0}/{1}...", "Clicking {0}/{1}...", "クリック中 {0}/{1}...", "点击中 {0}/{1}...");
        add("Clicker_ClickingInfinite", "點擊次數：{0} (無限)...", "Clicking count: {0} (Infinite)...", "クリック回数：{0} (無制限)...", "点击次数：{0} (无限)...");
        add("Clicker_SpammingKey", "連打中 [{0}] {1}/{2}...", "Spamming [{0}] {1}/{2}...", "[{0}] を連打中 {1}/{2}...", "连打中 [{0}] {1}/{2}...");
        add("Clicker_SpammingKeyInfinite", "連打中 [{0}] 次數：{1} (無限)...", "Spamming [{0}] count: {1} (Infinite)...", "[{0}] を連打中 回数：{1} (無制限)...", "连打中 [{0}] 次数：{1} (无限)...");
    }

    private static void RegisterMacro(Action<string, string, string, string, string> add)
    {
        add("Macro_Title", "動作錄製與重放", "Action Record & Replay", "動作記録と再生", "动作录制与重放");
        add("Macro_Desc", "錄製滑鼠點擊與鍵盤按鍵動作，並自訂速度重放", "Record user mouse clicks and keystrokes and replay them back", "キーボードとマウス操作を記録し、指定した速度で忠実に再生", "录制鼠标点击与键盘按键动作，并自定义速度重放");
        add("Macro_RecordBtn", "開始錄製 (F9)", "Start Recording (F9)", "記録開始 (F9)", "开始录制 (F9)");
        add("Macro_StopRecordBtn", "停止錄製", "Stop Recording", "記録停止", "停止录制");
        add("Macro_ClearBtn", "清除動作", "Clear Actions", "動作クリア", "清除动作");
        add("Macro_RepeatLabel", "重放次數 (0 = 無限)：", "Replay Loop Count (0 = Infinite):", "再生回数 (0 = 無限)：", "重放次数 (0 = 无限)：");
        add("Macro_SpeedLabel", "重放速度倍率：", "Replay Speed Multiplier:", "再生速度倍率：", "重放速度倍率：");
        add("Macro_RecordedActionsTitle", "已錄製動作 (按兩下步驟可編輯)：", "Recorded Actions (Double-click step to edit):", "記録された動作 (ダブルクリックで編集)：", "已录制动作 (双击步骤可编辑)：");
        add("Macro_EditStepBtn", "編輯步驟", "Edit Step", "ステップ編集", "编辑步骤");
        add("Macro_DeleteStepBtn", "刪除步驟", "Delete Step", "ステップ削除", "删除步骤");
        add("Macro_ActionsCount", "已錄製 {0} 個動作", "{0} actions recorded", "{0} 件の動作を記録済み", "已录制 {0} 个动作");
        add("Macro_NoActions", "尚未錄製任何動作", "No actions recorded yet", "まだ動作が記録されていません", "尚未录制任何动作");
        add("Macro_RecordingStatus", "正在錄製動作... 點擊或輸入任何內容以進行記錄", "Recording actions... Click or type anywhere to record actions!", "動作を記録中... 操作を行うと自動で記録されます", "正在录制动作... 点击或输入任何内容以进行记录");
        add("Macro_RecordingCountStatus", "正在錄製動作... 已擷取 {0} 個動作 (按停止完成)", "Recording actions... {0} actions captured (Click Stop to finish)", "動作を記録中... {0} 件の動作を記録 (停止をクリックで完了)", "正在录制动作... 已捕获 {0} 个动作 (点击停止以完成)");
        add("Macro_ClearedStatus", "動作已清除", "Actions cleared", "動作をクリアしました", "动作已清除");
        add("Macro_ActionReplay", "重放動作 (F9)", "Replay Actions (F9)", "動作再生 (F9)", "重放动作 (F9)");
        add("Macro_ValidationNoActions", "尚未錄製任何動作！請先錄製動作。", "No recorded actions to replay! Please record first.", "再生する動作がありません。先に記録を行ってください。", "尚未录制任何动作！请先录制动作。");
        add("Macro_SelectStepToEdit", "請先選擇要編輯的步驟。", "Please select a step to edit first.", "編集するステップを選択してください。", "请先选择要编辑的步骤。");
        add("Macro_SelectStepToDelete", "請先選擇要刪除的步驟。", "Please select a step to delete first.", "削除するステップを選択してください。", "请先选择要删除的步骤。");
        add("Macro_StepDeleted", "已刪除步驟 #{0}", "Deleted step #{0}", "ステップ #{0} を削除しました", "已删除步骤 #{0}");
        add("Macro_StepUpdated", "已更新步驟 #{0}", "Updated step #{0}", "ステップ #{0} を更新しました", "已更新步骤 #{0}");
        add("Macro_ReplayingProgress", "重放動作中：第 {0} 輪，動作 {1}/{2}", "Replaying Macro: Loop {0}, Action {1}/{2}", "動作再生中：ループ {0}、アクション {1}/{2}", "重放动作中：第 {0} 轮，动作 {1}/{2}");
        add("Macro_DetailClickLeft", "🖱️ 左鍵點擊 ({0}, {1})", "🖱️ Left Click at ({0}, {1})", "🖱️ 左クリック ({0}, {1})", "🖱️ 左键点击 ({0}, {1})");
        add("Macro_DetailClickRight", "🖱️ 右鍵點擊 ({0}, {1})", "🖱️ Right Click at ({0}, {1})", "🖱️ 右クリック ({0}, {1})", "🖱️ 右键点击 ({0}, {1})");
        add("Macro_DetailClickMiddle", "🖱️ 中鍵點擊 ({0}, {1})", "🖱️ Middle Click at ({0}, {1})", "🖱️ 中クリック ({0}, {1})", "🖱️ 中键点击 ({0}, {1})");
        add("Macro_DetailMouseMove", "↗️ 移動游標至 ({0}, {1})", "↗️ Move Cursor to ({0}, {1})", "↗️ カーソル移動 ({0}, {1})", "↗️ 移动光标至 ({0}, {1})");
        add("Macro_DetailKeyDown", "⌨️ 按下按鍵 [{0}]", "⌨️ Key Down [{0}]", "⌨️ キー押下 [{0}]", "⌨️ 按下按键 [{0}]");
        add("Macro_DetailKeyUp", "⌨️ 放開按鍵 [{0}]", "⌨️ Key Up [{0}]", "⌨️ キー解放 [{0}]", "⌨️ 松开按键 [{0}]");
        add("Macro_DetailTypeText", "🔤 輸入文字 [{0}]", "🔤 Type [{0}]", "🔤 文字入力 [{0}]", "🔤 输入文本 [{0}]");
        add("Macro_DialogTitle", "編輯步驟 #{0}", "Edit Step #{0}", "ステップ #{0} の編集", "编辑步骤 #{0}");
        add("Macro_ActionTypeHeader", "動作類型：", "Action Type:", "アクション種類：", "动作类型：");
        add("Macro_DelayHeader", "執行前延遲 (毫秒)：", "Delay Before Action (ms):", "実行前遅延 (ミリ秒)：", "执行前延迟 (毫秒)：");
        add("Macro_XCoordHeader", "X 座標：", "X Coordinate:", "X 座標：", "X 坐标：");
        add("Macro_YCoordHeader", "Y 座標：", "Y Coordinate:", "Y 座標：", "Y 坐标：");
        add("Macro_DataHeader", "按鍵名稱 / 文字內容：", "Key Name / Text Data:", "キー名 / テキスト内容：", "按键名称 / 文本内容：");
        add("Macro_ActionTypeLeftClick", "左鍵點擊", "Left Click", "左クリック", "左键点击");
        add("Macro_ActionTypeRightClick", "右鍵點擊", "Right Click", "右クリック", "右键点击");
        add("Macro_ActionTypeMiddleClick", "中鍵點擊", "Middle Click", "中クリック", "中键点击");
        add("Macro_ActionTypeMouseMove", "移動游標", "Move Cursor", "カーソル移動", "移动光标");
        add("Macro_ActionTypeKeyDown", "按下按鍵", "Key Down", "キー押下", "按下按键");
        add("Macro_ActionTypeKeyUp", "放開按鍵", "Key Up", "キー解放", "松开按键");
        add("Macro_ActionTypeTypeText", "輸入文字", "Type Text", "文字入力", "输入文本");
    }

    private static void RegisterSettings(Action<string, string, string, string, string> add)
    {
        add("Settings_Title", "偏好設定", "Settings", "設定", "设置");
        add("Settings_Desc", "自訂 Tapster 系統常駐、開機自動啟動與視窗行為", "Preferences, global hotkeys, system tray integration, and appearance", "環境設定、グローバルホットキー、タスクトレイ設定および外観", "首选项、全局热键、系统托盘与外观设置");
        add("Settings_LangTitle", "介面語言", "Interface Language", "表示言語", "界面语言");
        add("Settings_LangDesc", "選擇 Tapster 介面顯示語言，支援即時套用", "Choose the display language for Tapster with instant UI updates", "Tapsterのインターフェース表示言語を変更します（即時反映）", "选择 Tapster 界面显示语言，支持即时生效");
        add("Settings_LangAuto", "跟隨系統", "System Default", "システムに従う", "跟随系统");
        add("Settings_BootTitle", "開機自動啟動", "Start on Windows Boot", "Windows 起動時に自動実行", "开机自启动");
        add("Settings_BootDesc", "於 Windows 登入時自動在背景啟動 Tapster 並常駐於系統匣", "Automatically launch Tapster in the background when logging into Windows", "Windows ログイン時にバックグラウンドで自動起動します", "登录 Windows 时在后台自动启动 Tapster 并常驻于系统托盘");
        add("Settings_StartMinimizedTitle", "啟動時最小化至系統匣", "Start Minimized to Tray", "起動時にトレイへ最小化", "启动时最小化至托盘");
        add("Settings_StartMinimizedDesc", "開啟應用程式時直接隱藏至右下角系統匣，不彈出主視窗干擾", "Launch directly into the system tray without showing the main window", "メインウィンドウを表示せず、タスクトレイに格納した状態で起動します", "程序启动时不显示主窗口，直接隐藏至托盘");
        add("Settings_CloseToTrayTitle", "點擊關閉時縮小至系統匣", "Minimize to Tray on Close", "閉じるボタンでトレイへ最小化", "关闭时最小化至托盘");
        add("Settings_CloseToTrayDesc", "點擊右上角關閉按鈕時縮排至系統匣，保持全域快捷鍵隨叫隨到", "Clicking the close button (X) minimizes to tray instead of quitting", "右上の [×] を押した際にアプリを終了せずトレイへ格納します", "点击右上角关闭按钮时最小化至托盘，保持全局快捷键随时可用");
        add("Settings_AlwaysOnTopTitle", "視窗永遠置頂", "Always on Top", "常に最前面に表示", "窗口置顶");
        add("Settings_AlwaysOnTopDesc", "將 Tapster 視窗固定於其他視窗最上層 (全域喚醒熱鍵 Ctrl+Alt+T)", "Keep the Tapster window pinned above all other windows (Hotkey: Ctrl+Alt+T)", "Tapsterのウィンドウを他のウィンドウより前面に固定します (ホットキー: Ctrl+Alt+T)", "将 Tapster 窗口固定在其他应用程序顶层 (全局热键: Ctrl+Alt+T)");
        add("Settings_HotkeysTitle", "全域功能熱鍵", "Global Action Hotkeys", "グローバルホットキー", "全局功能热键");
        add("Settings_HotkeysDesc", "即使在遊戲全螢幕或背景視窗，按下熱鍵亦可直接遙控所有功能", "Control features seamlessly even while running full-screen games or apps", "フルスクリーンゲームや作業中でもホットキーから即座に操作できます", "在任何全屏游戏或程序中均可直接通过热键操作");
        add("Settings_HkWake", "喚醒 / 隱藏主視窗", "Wake / Hide Main Window", "ウィンドウの表示 / 非表示", "唤醒 / 隐藏主窗口");
        add("Settings_HkClicker", "切換連點", "Toggle Clicker", "連打の切り替え", "切换连点");
        add("Settings_HkHolder", "切換長按", "Toggle Holder", "長押しの切り替え", "切换长按");
        add("Settings_HkTyper", "啟動打字", "Toggle Typer", "タイピング開始", "启动打字");
        add("Settings_HkMacro", "重放動作", "Replay Actions", "動作再生", "重放动作");
        add("Settings_HkPanicKill", "緊急全部終止", "Panic Kill All", "緊急停止 (すべての動作を終了)", "紧急全部终止");
        add("Settings_HkPanicEscape", "緊急中斷跳出", "Panic Escape", "緊急脱出 (Esc)", "紧急中断跳出");
        add("Settings_AboutNavTitle", "版本與專案資訊", "About & Version Information", "バージョンと詳細情報", "版本与项目信息");
        add("Settings_AboutNavBtn", "查看詳情", "View Details", "詳細を表示", "查看详情");
    }

    private static void RegisterAbout(Action<string, string, string, string, string> add)
    {
        add("About_Title", "關於 Tapster", "About Tapster", "Tapster について", "关于 Tapster");
        add("About_Desc", "應用程式版本、技術規格、專案連結與隱私聲明", "Application version, technical architecture, and open source links", "アプリケーションバージョン、技術仕様、オープンソース情報", "应用程序版本、技术规格、开源协议与项目链接");
        add("About_HeroSub", "Windows 11 原生超輕量自動化助手", "Windows 11 Native Ultra-Lightweight Automation Utility", "Windows 11 ネイティブ超軽量自動化ユーティリティ", "Windows 11 原生超轻量自动化助手");
        add("About_HeroSub2", "專為遠端桌面 (VNC/RDP)、虛擬機、遊戲掛機與高頻點擊量身打造", "Tailored for remote desktop (VNC/RDP), virtual machines, and high-frequency clicking", "リモートデスクトップ、仮想マシン、高速連打、ゲーム放置に最適化", "专为远程桌面 (VNC/RDP)、虚拟机、游戏挂机与高频点击量身打造");
        add("About_SpecsTitle", "技術規格", "Technical Specifications", "技術仕様", "技术规格");
        add("About_SpecsUiLabel", "UI 框架", "UI Framework", "UI フレームワーク", "UI 框架");
        add("About_SpecsCoreLabel", "底層自動化核心", "Automation Core", "自動化コア", "底层自动化核心");
        add("About_SpecsCoreValue", "Win32 SendInput 核心 / 微秒級高精調度", "Win32 SendInput Core / Microsecond Precision Dispatch", "Win32 SendInput コア / マイクロ秒精度ディスパッチ", "Win32 SendInput 核心 / 微秒级高精调度");
        add("About_SpecsPublishLabel", "發布架構", "Distribution", "配布形式", "分发形式");
        add("About_SpecsPublishValue", "雙軌制：Microsoft Store MSIX 封裝 + Standalone 綠色純單檔 EXE", "Dual: Microsoft Store MSIX + Standalone portable single EXE", "二重形式：Microsoft Store MSIX + ポータブル単一EXE", "双轨制：Microsoft Store MSIX 封装 + Standalone 绿色纯单文件 EXE");
        add("About_SpecsLicenseLabel", "授權條款", "License", "ライセンス", "开源协议");
        add("About_SpecsLicenseValue", "MIT License (100% 免費且開源)", "MIT License (100% Free & Open Source)", "MIT License (完全無料＆オープンソース)", "MIT License (100% 免费开源)");
        add("About_PrivacyTitle", "隱私承諾與本地設定", "Privacy & Local Data", "プライバシーとローカル設定", "隐私承诺与本地设置");
        add("About_PrivacyDesc", "100% 離線純本機運行，零網路遙測傳輸，不收集任何按鍵或個人資料", "100% offline local execution, zero network telemetry, no keylogging or data collection", "100% オフライン実行、テレメトリ送信なし、キーや個人情報を一切収集しません", "100% 离线纯本地运行，零网络遥测传输，不收集任何按键或个人资料");
        add("About_OpenFolderBtn", "開啟設定目錄", "Open Settings Folder", "設定フォルダーを開く", "打开设置目录");
    }

}
