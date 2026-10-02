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
