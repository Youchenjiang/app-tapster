# Tapster 架構與 Target Framework 決策文件

## 1. 核心組件架構

| 組件名稱 | 專案路徑 | 技術棧 | 發布模式 / 特性 |
| :--- | :--- | :--- | :--- |
| **Tapster.Core** | `src/Tapster/Tapster.csproj` | C# (.NET 8 LTS) | 共享核心函式庫：純 Win32 P/Invoke 鍵鼠模擬、全域 LowLevel Hook、高精度 QPC 計時與錄製狀態機 |
| **Tapster.Gui** | `src/Tapster.Gui/Tapster.Gui.csproj` | C# (.NET 8 LTS) + WPF | **輕量原生桌面端**：內嵌 5 排虛擬鍵盤狀態格、低資源佔用 (<30MB) 單視窗控制台 |
| **Tapster.Fluent** | `Tapster.Fluent/Tapster.Fluent.csproj` | C# (.NET 8 LTS) + WinUI 3 (Windows App SDK) | **現代化 Fluent UI**：Mica 材質、系統匣 (System Tray) 常駐、全域熱鍵喚醒 (`Ctrl+Alt+T`) |

---

## 2. Target Framework 策略與 RollForward 設定

### 2.1 基準版本：.NET 8 LTS (`net8.0-windows`)
- **選擇原因**：
  1. **長期支援 (LTS) 穩定性**：企業與開發者電腦普遍具備 .NET 8 執行環境。
  2. **WinUI 3 (Windows App SDK 2.x) 基線需求**：微軟官方 C#/WinRT 投影套件最佳支援版本。
  3. **高效記憶體管理與 Native Interop**：.NET 8 提供頂級的 P/Invoke 效能與原生記憶體操作支援。

### 2.2 RollForward 機制 (`<RollForward>LatestMajor</RollForward>`)
- 各主要入口專案均配置 `LatestMajor` 推進策略：
  - 若系統安裝有 **.NET 8** ➔ 以原生效能執行。
  - 若系統僅安裝 **.NET 9 / .NET 10** ➔ 自動向上向前滾動相容執行，避免彈出缺失執行階段的提示視窗。

---

## 3. 代碼剪裁 (Trimming) 與 AOT 考量說明

- **WinUI 3 剪裁禁忌**：
  `Tapster.Fluent.csproj` **禁止開啟 `<PublishTrimmed>true</PublishTrimmed>`**。WinUI 3 (Windows App SDK) 與 C#/WinRT 投影層依賴反射與 COM 介面啟動。若啟用代碼修剪，Linker 可能會剔除必要的 XAML 型別元資料，導致啟動時發生未捕獲的 `COMException` 或 `AccessViolationException`。
- **純 Win32 結構體記憶體對齊**：
  `Tapster.Core` 定義之 Win32 結構體（如 `INPUT`, `KEYBDINPUT`, `MOUSEINPUT`, `KBDLLHOOKSTRUCT`）均嚴格指定 `[StructLayout(LayoutKind.Sequential)]` 或 `[StructLayout(LayoutKind.Explicit)]`，以確保在 32 位元與 64 位元執行環境下記憶體邊界與 Windows 核心完全對齊。

---

## 4. WinUI 3 視窗標題列與硬體更新率 (144Hz+) 整合決策

- **核心設計原則：完全迴避 `AppWindowTitleBar`**：
  WinUI 3 官方提供的 `ExtendsContentIntoTitleBar = true` 及 `AppWindow.TitleBar` 屬性會強行將非客戶區拖曳邏輯由 Windows 核心轉交由 XAML DirectComposition 組合層。在 120Hz/144Hz/240Hz 顯示器與高回報率（1000Hz）滑鼠下會造成嚴重掉幀與游標拖曳延遲。
- **標準解決方案：純 Windows 11 DWM 屬性注入**：
  視窗拖曳維持由 Windows 核心 `user32.dll` 與 `dwm.exe` 處理（滿血原生更新率）；深色主題與外觀則透過 `DwmSetWindowAttribute` 注入 `DWMWA_USE_IMMERSIVE_DARK_MODE` (20)、`DWMWA_CAPTION_COLOR` (35, `#202020`) 與 `DWMWA_TEXT_COLOR` (36, 純白) 達成視覺無縫融合。
- **互動控制項分離**：
  將全域切換開關（如 Always on top）由標題列移置 `NavigationView.PaneFooter`，保證視窗非客戶區頂部具備 100% 完整之拖曳 Hit-Test 區域。完整開發指南請參閱 [`docs/development/fluent_window_drag_performance_guide.md`](development/fluent_window_drag_performance_guide.md)。

