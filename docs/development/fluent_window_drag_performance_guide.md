# Fluent .NET (WinUI 3 / Windows App SDK) 視窗拖曳性能與高更新率 (144Hz+) 開發指南

> **摘要**：本指南完整記錄在 Windows 11 / .NET 8+ 環境下開發 WinUI 3 (Windows App SDK) 應用程式時，解決「視窗拖曳卡頓、掉幀、游標跟手度延遲」的根本機制與標準開發範式。同時兼顧 144Hz/240Hz 滿血硬體更新率與現代 Fluent 深色設計美學。

---

## 1. 核心問題現象與診斷

在開發現代 Fluent .NET 桌面端應用時，許多開發者會遇到以下反饋：
- **現象 1**：在 120Hz、144Hz 或 240Hz 高更新率螢幕上，拖曳視窗時體感極度不流暢，滑鼠游標明顯與視窗本體脫節（掉幀感嚴重，如同鎖定在 30~45 FPS）。
- **現象 2**：若啟用 `ExtendsContentIntoTitleBar = true` 並自訂 XAML 標題列，點擊標題列文字、空白處或圖示時拖曳判定不靈敏，甚至產生 DirectComposition 殘影與排版重繪延遲。
- **現象 3**：改用預設標題列後拖曳雖然順暢，但外觀退化為預設的 Windows 亮色/灰白系統邊框，失去現代 Fluent 設計的一體化美感（「順了但是變醜了」）。

---

## 2. 根因深度剖析 (Under the Hood)

### 2.1 為什麼 WinUI 3 自訂標題列會卡頓？
在傳統 Win32 應用（如 Clickra 核心架構）中，視窗拖曳是由 Windows OS 內核 `user32.dll` 與 DWM（桌面視窗管理員，`dwm.exe`）直接在驅動層進行非客戶區（Non-Client Area）的硬體加速位移。這個過程完全由作業系統驅動，**保證 100% 滿血螢幕更新率（144Hz / 240Hz / 360Hz）且零延遲跟手**。

然而，當你在 WinUI 3 中啟用：
```csharp
ExtendsContentIntoTitleBar = true; // 或是呼叫 SetTitleBar(element)
```
會觸發以下致命機制：
1. **DirectComposition 組合層節流**：視窗的非客戶區被 WinUI 3 剝除，拖曳訊息（`WM_NCHITTEST`、`WM_MOUSEMOVE`）轉由 Windows App SDK 的 XAML 渲染執行緒與 DirectComposition 組合佇列接管。
2. **高回報率滑鼠引發佇列塞車**：現代電競滑鼠輪詢率通常為 1000Hz。每秒上千個輸入事件湧入 WinUI 3 的訊息佇列，XAML 必須在每一幀重新計算 Composition Visual Tree，造成嚴重掉幀（Frame Drop）。
3. **Mica 雲母動態採樣開銷**：若同時啟用了 `<MicaBackdrop />`，DWM 必須在視窗位移的每一微秒對桌布動態採樣並即時計算高斯模糊。在 Windows 11 上，DWM 會自動將帶有複雜自訂非客戶區的視窗**強制節流至 30~45 FPS** 以節省 GPU 算力。

### 2.2 程式碼層級的隱藏陷阱：`AppWindowTitleBar` 的接管效應
許多開發者嘗試透過 `AppWindowTitleBar` 調整顏色：
```csharp
// ⚠️ 陷阱：只要存取或自訂 AppWindow.TitleBar，App SDK 就會掛接非客戶區！
if (AppWindowTitleBar.IsCustomizationSupported())
{
    var titleBar = AppWindow.TitleBar;
    titleBar.ExtendsContentIntoTitleBar = false; // 即使設為 false
    titleBar.BackgroundColor = ...;             // 只要有任何自訂設定
}
```
**關鍵發現**：只要在程式碼中碰觸了 `AppWindow.TitleBar` 或呼叫了其自訂 API，Windows App SDK 就會註冊非客戶區的攔截鈎子，導致拖曳邏輯無法完全回歸純粹的 DWM 內核處理！

---

## 3. 黃金解法：純內核級 DWM 注入 + 完整 Fluent 融合

要達到 **「100% 原生 144Hz+ 滿血零延遲拖曳」** 且 **「外觀深色一體化不變醜」**，唯一標準解法是：
1. **完全不要碰 `AppWindowTitleBar` / `AppWindow.TitleBar`**。
2. **直接利用 Windows 11 DWM 內核屬性（`DwmSetWindowAttribute`）注入深色風格與自訂色彩**。
3. **視窗內部維持乾淨俐落的 Fluent XAML 結構**。

### 3.1 C# P/Invoke 宣告
在底層 `NativeMethods.cs` 中宣告 Windows 11 原生 DWM 屬性：

```csharp
namespace Tapster;

public static partial class NativeMethods
{
    // Windows 11 DWM 核心屬性常數
    public const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20; // 沉浸式深色模式（按鈕、外框暗色化）
    public const int DWMWA_CAPTION_COLOR = 35;           // 原生標題列背景色 (COLORREF 0x00BBGGRR)
    public const int DWMWA_TEXT_COLOR = 36;              // 原生標題列文字顏色 (COLORREF 0x00BBGGRR)

    [DllImport("dwmapi.dll", PreserveSig = true)]
    public static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

    [DllImport("dwmapi.dll", PreserveSig = true)]
    public static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref uint pvAttribute, int cbAttribute);
}
```

### 3.2 MainWindow.xaml.cs 實作標準
在 `MainWindow` 建構函式中，於 `InitializeComponent()` 之後直接注入 DWM 屬性：

```csharp
using Microsoft.UI.Xaml;
using WinRT.Interop;
using Tapster;

namespace Tapster_Fluent;

public sealed partial class MainWindow : Window
{
    private readonly IntPtr _hWnd;

    public MainWindow()
    {
        InitializeComponent();

        _hWnd = WindowNative.GetWindowHandle(this);

        // ─────────────────────────────────────────────────────────────
        // 核心：純內核級 DWM 原生深色標題列（144Hz+ 零延遲位移保證）
        // ─────────────────────────────────────────────────────────────
        // 1. 啟用 Windows 11 系統級沉浸式深色邊框與控制按鈕
        int darkMode = 1;
        NativeMethods.DwmSetWindowAttribute(_hWnd, NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));

        // 2. 將原生標題列背景色無縫對齊深色 Fluent 主題 (#202020 -> 0x00202020 BGR)
        uint captionColor = 0x00202020;
        NativeMethods.DwmSetWindowAttribute(_hWnd, NativeMethods.DWMWA_CAPTION_COLOR, ref captionColor, sizeof(uint));

        // 3. 設定標題文字為純白色 (0x00FFFFFF BGR)
        uint textColor = 0x00FFFFFF;
        NativeMethods.DwmSetWindowAttribute(_hWnd, NativeMethods.DWMWA_TEXT_COLOR, ref textColor, sizeof(uint));

        // ⚠️ 注意：切勿在此呼叫 AppWindow.TitleBar 或 ExtendsContentIntoTitleBar！
        // 讓視窗拖曳完全保留在 Windows 系統內核 user32.dll / dwm.exe 中。

        RootFrame.Navigate(typeof(MainPage));
    }
}
```

### 3.3 MainWindow.xaml 視窗範本
XAML 結構極簡化，移除所有在視窗頂部自訂的假標題列 Grid：

```xml
<?xml version="1.0" encoding="utf-8" ?>
<Window
    x:Class="Tapster_Fluent.MainWindow"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
    xmlns:local="using:Tapster_Fluent"
    xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
    Title="Tapster"
    mc:Ignorable="d">

    <!-- 視窗內部客戶區可安心使用 MicaBackdrop 享受極致質感，不影響原生標題列拖曳 -->
    <Window.SystemBackdrop>
        <MicaBackdrop />
    </Window.SystemBackdrop>

    <!-- 內部直接由 Frame 乘載 MainPage，無多餘重複疊層 -->
    <Frame x:Name="RootFrame" />
</Window>
```

---

## 4. UI/UX 排版最佳實踐：互動控制項的分離

### 4.1 永遠不要把 CheckBox / ToggleButton 塞在標題列
先前的版本在標題列右側放置了「Always on top」勾選框，帶來兩大弊端：
1. **拖曳 Hit-Testing 破碎**：使用者點擊標題列右半部時無法拖曳視窗，點擊左側文字時也無法拖曳，只有極窄的中間區域能拖曳。
2. **違背微軟 Fluent Design 規範**：現代 Windows 11 設計語言中，標題列應保持乾淨（僅顯示 App 名稱、Icon 與標準視窗三鍵）。

### 4.2 標準解法：移至 `NavigationView.PaneFooter`
將「永遠置頂（Always on top）」控制項放置於左側導覽列底部（`NavigationView.PaneFooter`）：

```xml
<NavigationView x:Name="NavView" PaneDisplayMode="Left" ...>
    <NavigationView.MenuItems>
        <!-- 導覽功能項目 -->
    </NavigationView.MenuItems>

    <!-- 左側面板底部常駐控制項 -->
    <NavigationView.PaneFooter>
        <StackPanel Margin="12,4" Spacing="4">
            <CheckBox x:Name="AlwaysOnTopCheckBox"
                      Content="Always on top"
                      FontSize="11"
                      Checked="AlwaysOnTopCheckBox_CheckedChanged"
                      Unchecked="AlwaysOnTopCheckBox_CheckedChanged"/>
        </StackPanel>
    </NavigationView.PaneFooter>
</NavigationView>
```
**優勢**：
- 在任何功能分頁（Auto Typer、Key Holder、Macro 等）切換時，該勾選框始終可見可操作。
- 完全不佔用標題列空間，徹底解放視窗頂部的原生拖曳區域。

---

## 5. 架構對比總結表

| 架構模式 | 拖曳更新率 (144Hz+) | 游標跟手度 | 外觀整合度 | 實作難度 | 適用場景 |
| :--- | :---: | :---: | :---: | :---: | :--- |
| **WinUI 3 自訂標題列** (`ExtendsContentIntoTitleBar = true` + `SetDragRectangles`) | ❌ 30~45 FPS (嚴重掉幀) | ❌ 延遲有殘影 | ⭐️⭐️⭐️⭐️⭐️ (一體化) | 複雜 | 僅適合 60Hz 傳統辦公螢幕且無高回報率滑鼠之應用 |
| **未調色原生標題列** (`ExtendsContentIntoTitleBar = false`，未注入 DWM) | ✅ 144Hz~360Hz (滿血順暢) | ✅ 100% 同步 | ❌ 灰白邊框違和 | 最低 | 不推薦（「順了但是變醜了」） |
| **純內核 DWM 注入標題列** (**★ 本指南推薦標準**) | **✅ 144Hz~360Hz (滿血順暢)** | **✅ 100% 同步** | **⭐️⭐️⭐️⭐️⭐️ (深色沉浸式)** | **低 (3 行 API)** | **所有高效能 .NET 8/9/10 Fluent 桌面應用** |
| **純 Win32 NativeAOT (Clickra CLI 模式)** | ✅ 144Hz~360Hz (滿血順暢) | ✅ 100% 同步 | ⭐️⭐️⭐️⭐️ (自刻 GDI+/DirectX) | 極高 | 追求極致零依賴性、啟動小於 20ms 之輕量級工具 |

---

## 6. 結論與速查清單 (Checklist)

當在 .NET 中開發 Fluent / WinUI 3 應用時，請遵循以下檢查清單：
- [x] **絕對禁止** 在追求高回報率流暢度的視窗中使用 `ExtendsContentIntoTitleBar = true`。
- [x] **絕對禁止** 存取 `AppWindow.TitleBar` 或呼叫 `AppWindowTitleBar.IsCustomizationSupported()`。
- [x] **務必呼叫** `DwmSetWindowAttribute(hwnd, 20, 1)` 開啟沉浸式深色模式。
- [x] **務必呼叫** `DwmSetWindowAttribute(hwnd, 35, 0x00202020)` 統一標題列背景色。
- [x] **務必呼叫** `DwmSetWindowAttribute(hwnd, 36, 0x00FFFFFF)` 統一標題列文字顏色。
- [x] 視窗內容使用 `<Window.SystemBackdrop><MicaBackdrop /></Window.SystemBackdrop>` 保留現代透明材質。
- [x] 將全域性開關（如 Always on top）放置於 `NavigationView.PaneFooter`。
