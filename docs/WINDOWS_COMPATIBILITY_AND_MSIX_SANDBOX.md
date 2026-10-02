# Windows 10/11 相容性與 MSIX 沙盒權限指南

## 1. MSIX 容器權限模型與 FullTrust 宣告

Tapster 核心功能涉及全系統級的鍵盤滑鼠模擬與全域動作監聽，受 Windows 系統安全架構約束，在 MSIX 打包時需採用特定的權限模型：

### 1.1 `runFullTrust` 限制功能宣告
在 `AppxManifest.xml` 中必須宣告受限權限（Restricted Capability）：
```xml
<Capabilities>
  <rescap:Capability Name="runFullTrust" />
</Capabilities>
```
- **必要性**：
  - `SendInput`：向跨行程視窗注入鍵盤與滑鼠事件。
  - `SetWindowsHookEx` (`WH_KEYBOARD_LL`, `WH_MOUSE_LL`)：全域監聽鍵盤與滑鼠輸入用於巨集錄製。
  - `RegisterHotKey`：註冊全域系統熱鍵（`Ctrl + Alt + T`）喚醒主視窗。
- **Store 審查合規**：在提交 Microsoft Store 時，需於 Partner Center 說明本工具為鍵鼠輔助/無障礙/自動化工具，合法使用 `runFullTrust` 權限。

---

## 2. 檔案系統虛擬化與路徑解析

### 2.1 MSIX 容器重定向行為
- 當以 MSIX 打包執行時，應用程式對 `%LocalAppData%\Tapster` 的寫入會被 Windows 核心透明重定向至：
  `%LocalAppData%\Packages\Tapster_<PublisherId>\LocalCache\Local\Tapster\`
- **便攜版 vs MSIX 版一致性**：
  在讀取與儲存設定檔或巨集 JSON 匯出時，程式需優先透過 WinRT API 解析實體路徑：
  ```csharp
  string baseDir;
  try
  {
      baseDir = Windows.Storage.ApplicationData.Current.LocalFolder.Path;
  }
  catch
  {
      baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Tapster");
  }
  ```

---

## 3. 系統匣 (System Tray) 與背景常駐

### 3.1 訊息循環與隱藏視窗
- WinUI 3 (Windows App SDK) 原生不具備傳統 Win32 系統匣事件分派機制。
- Tapster 透過 P/Invoke `CreateWindowEx` 建立一個輕量的隱藏訊息專用視窗（Message-Only Window），以捕獲工作列圖示的 `WM_TRAYICON`、滑鼠點擊、雙擊與右鍵選單快顯事件。
- 當使用者關閉主視窗時，預設最小化至系統匣並釋放 DirectX/XAML 渲染管線記憶體，維持低耗電與極致精簡的待命狀態。
