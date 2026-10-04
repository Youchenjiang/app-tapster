# Tapster 踩坑歷史與故障排除日誌 (Troubleshooting & Resolutions)

本文檔詳細記錄 Tapster 在原生 Win32 P/Invoke 輸入模擬、全域鍵盤鉤子、WinUI 3 (Windows App SDK) 開發以及 CI/CD 代碼掃描過程中遭遇之關鍵問題與修復方案。

---

## 1. 執行時期與 Win32 子系統踩坑

### 1.1 UIPI (User Interface Privilege Isolation) 阻擋 `SendInput` 模擬輸入
- **現象**：Tapster 在一般應用程式（記事本、瀏覽器）正常按鍵，但切換至系統管理員權限視窗（如「工作管理員」、管理員終端機、某些 Anti-Cheat 遊戲）時，模擬按鍵無效或無回應。
- **原因**：Windows UIPI 安全機制嚴格禁止較低 Integrity Level（中完整性，一般使用者）的行程向較高 Integrity Level（高完整性，系統管理員）的視窗發送 Win32 視窗訊息或注入硬體模擬輸入。
- **解決方案**：
  1. 若目標視窗以管理者權限執行，Tapster 必須以「以系統管理員身分執行」啟動。
  2. 在使用指南中清楚標註 UIPI 權限邊界，避免誤判為程式 Bug。

---

### 1.2 全域 LowLevel 鍵盤鉤子 (`WH_KEYBOARD_LL`) 被 Windows 系統強制卸載
- **現象**：錄製巨集一段時間後，鍵盤動作突然無法被偵測或錄製中斷。
- **原因**：Windows 核心設有 `LowLevelHooksTimeout`（通常為 200ms ~ 1000ms）。若鉤子回呼函式（Hook Callback）中執行了耗時操作（如磁碟 I/O、字串重度格式化或同步鎖等待），Windows 會直接將該 Hook 從系統鉤子鏈中強制移除。
- **解決方案**：
  1. 回呼函式必須維持絕對輕量（O(1)），僅提取虛擬鍵碼（VK）與微秒時間戳記，放入非同步記憶體集合後立即呼叫 `CallNextHookEx` 回傳。
  2. 所有磁碟讀寫與 UI 刷新移至獨立背景工作執行緒。

---

### 1.3 `MacroRecorder.StopRecording` 拋出 `ThreadInterruptedException` 導致狀態遺失
- **現象**：在呼叫端執行緒被插斷時呼叫 `StopRecording()`，`Thread.Join(300)` 拋出 `ThreadInterruptedException`，導致 `_recordThread` 與 `_stopwatch` 未正常釋放重置。
- **解決方案**：
  在 `MacroRecorder.StopRecording()` 中明確捕獲 `ThreadInterruptedException`，並將關鍵狀態重置移至 `finally` 區塊保證執行：
  ```csharp
  try
  {
      _recordThread?.Join(300);
  }
  catch (ThreadStateException ex) { /* log */ }
  catch (ThreadInterruptedException ex) { /* log */ }
  finally
  {
      _recordThread = null;
      _stopwatch.Stop();
  }
  ```

---

## 2. 靜態分析與代碼品質規範踩坑

### 2.1 SonarCloud S2589 / S2583 冗餘 null 檢查與無法到達之分支
- **現象**：背景 Worker 迴圈內撰寫 `if (_cts?.Token.IsCancellationRequested ?? true) break;` 被 SonarCloud 判定為 `S2589: Remove this unnecessary check for null` 與 `S2583: Some code paths are unreachable`。
- **原因**：外部 `while` 或前置條件已保證 `_cts` 非 null，但在迴圈內部重複進行可空性驗證，導致資料流分析器認為 `?? true` 分支永遠不可達。
- **解決方案**：
  在啟動 `Task.Run` 前預先提取結構型態 `CancellationToken`：
  ```csharp
  var token = _cts?.Token ?? CancellationToken.None;
  Task.Run(() =>
  {
      while (!token.IsCancellationRequested)
      {
          // 業務邏輯，乾淨無 CS8602 警告與 S2589 異味
      }
  });
  ```

---

### 2.2 MSBuild 背景背景行程鎖定二進位輸出檔案
- **現象**：本地端編譯時出現 `MSB3026: Could not copy file because it is being used by another process`。
- **原因**：Roslyn 編譯器快取（`VBCSCompiler.exe`）與 MSBuild 背景 Worker 在編譯完成後未釋放產物控制代碼。
- **解決方案**：每次本地執行 `dotnet build` 後，務必執行 `dotnet build-server shutdown` 徹底關閉後台快取處理序。

---

## 3. WinUI 3 (Windows App SDK) Fluent 視窗拖曳卡頓與高更新率 (144Hz+) 踩坑歷程

### 3.1 `ExtendsContentIntoTitleBar = true` 引發 DirectComposition 拖曳掉幀與游標遲滯
- **現象**：在 120Hz/144Hz/240Hz 高更新率螢幕與高回報率（1000Hz）滑鼠環境下，拖曳視窗時游標與視窗不同步，產生嚴重頓挫感與掉幀（體感 30~45 FPS）。
- **原因**：
  1. `ExtendsContentIntoTitleBar = true` 將非客戶區拖曳訊息轉交由 Windows App SDK XAML 執行緒與 DirectComposition 接管。
  2. 每一幀位移皆觸發 XAML Visual Tree 重新 Hit-test 與排版運算，導致訊息佇列阻塞。
  3. 搭配 `<MicaBackdrop />` 時，DWM 在視窗位移期間會強制將背景高斯模糊取樣節流至 30~45 FPS。
- **錯誤嘗試**：
  - *嘗試 1*：呼叫 `AppWindow.TitleBar.ExtendsContentIntoTitleBar = false`，但未自訂標題列顏色，導致系統亮色邊框與暗色主題產生強烈違和（「順了但是變醜了」）。
  - *嘗試 2*：使用 `SetDragRectangles` 手動動態計算拖曳矩形，依舊走 Windows App SDK 接管路徑，卡頓依舊。
  - *嘗試 3*：在程式碼中存取 `AppWindowTitleBar` / `AppWindow.TitleBar` 屬性，即使設定背景色，仍會觸發 Windows App SDK 的非客戶區攔截鈎子。
- **終極解決方案**：
  1. **完全不呼叫 `AppWindowTitleBar` / `AppWindow.TitleBar`**：不觸發 App SDK 攔截，讓視窗非客戶區 100% 由 Windows 內核 DWM（`user32.dll` / `dwm.exe`）處理，解鎖滿血 144Hz~360Hz 零延遲拖曳。
  2. **注入 Windows 11 DWM 核心層級色彩屬性**：
     ```csharp
     int darkMode = 1;
     NativeMethods.DwmSetWindowAttribute(_hWnd, NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));
     uint captionColor = 0x00202020; // #202020 (BGR)
     NativeMethods.DwmSetWindowAttribute(_hWnd, NativeMethods.DWMWA_CAPTION_COLOR, ref captionColor, sizeof(uint));
     uint textColor = 0x00FFFFFF;
     NativeMethods.DwmSetWindowAttribute(_hWnd, NativeMethods.DWMWA_TEXT_COLOR, ref textColor, sizeof(uint));
     ```
  3. **將非必要按鈕移出標題列**：如「Always on top」核取方塊移至 `NavigationView.PaneFooter`，保持頂部拖曳區域乾淨完整。
- **詳細指南**：請參閱完整開發規範文件 [`docs/development/fluent_window_drag_performance_guide.md`](development/fluent_window_drag_performance_guide.md)。

