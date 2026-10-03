# Tapster CI/CD 雙軌發布流程指南 (Dual-Release Strategy)

本文檔說明 Tapster 於 CI/CD (`.github/workflows/release.yml`) 雙軌產出的架構與最佳發布實踐。

---

## 1. 雙軌產出架構

Tapster 採取現代 Windows 桌面軟體的雙軌發布架構，滿足不同使用者族群與發行通路需求：

| 產物名稱 | 發布目標 | 封裝模式 | 預估大小 | 特點與適用對象 |
| :--- | :--- | :--- | :--- | :--- |
| **`Tapster.exe`** | GitHub Releases / 官網免安裝版 | Standalone Single-File (Embedded Payload) | **~46 MB** | **真正純單一檔免安裝**。內嵌執行階段與 Fluent Engine，隨身碟即插即用，適合便攜與進階使用者。 |
| **`Tapster-vX.Y.Z.msix`** | Microsoft Store / 側載安裝 | WinUI 3 Packaged (Windows App SDK) | **~25 MB** | **微軟商店標準封裝**。採用 WindowsAppSDK 打包模式，具備 runFullTrust 存取權限、沙盒隔離與差分增量更新。 |

---

## 2. GitHub Actions (`release.yml`) 步驟規劃

當推送版本標籤（例如 `v1.1.0.0`）至 GitHub 時，發布工作流程將自動執行以下管線：

1. **版本標籤觸發**：
   - 偵測 `tags: ['v*']` 推送事件並啟動 Release Pipeline。
2. **建置單一可攜式執行檔 (Portable Single-File EXE)**：
   - 執行 `powershell -File scripts/build_portable.ps1`。
   - 編譯 `Tapster.Fluent` 核心引擎，裁剪多餘 AI/除錯二進位檔，並壓縮至 `Tapster.Launcher` 原生資源中。
   - 產出純單檔 `publish/Tapster.exe` 並上傳至 GitHub Release 附件。
3. **建置 MSIX 現代安裝套件**：
   - 執行 `powershell -File scripts/build_msix.ps1`。
   - 組裝 `packaging/msix/Layout`，對齊 `packaging/msix/AppxManifest.xml` 依賴版本與中繼資料。
   - 使用自簽憑證（本機/CI 測試）或微軟商店 Trusted Signing 進行簽署，動態產出 `publish/Tapster-vX.Y.Z.msix`。
4. **發布到微軟商店 (Microsoft Store Partner Center)**：
   - 可搭配微軟合作夥伴 API 提交工具自動將 MSIX 套件與多語系中繼資料上傳審核。

---

## 3. 微軟商店下載與增量更新機制 (Delta Updates)

- **首次安裝**：
  - 微軟商店伺服器二次壓縮後，使用者下載體積約僅 20~25 MB。
- **版本更新 (Delta Block Map)**：
  - MSIX 格式原生具備 `AppxBlockMap.xml` 區塊校驗清單。
  - 當發布新版本時，Windows Update 引擎僅比對並下載發生變動的檔案區塊（通常僅數 MB），大幅節省網路頻寬與更新等待時間。
