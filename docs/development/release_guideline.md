# Tapster 版本號管理與發布規範 (Release Versioning Guide)

本文件定義 Tapster 的版本號架構設計、微軟商店相容規範，以及發布新版本時的標準作業程序 (SOP)。

---

## 0. 發布檢查清單 (Release Checklist)

> [!WARNING]
> **每次發版前，務必逐項核對以下清單。** 遺漏任何一項都可能導致版本標籤衝突、商店送審退件或文案不一致。

### Step 1：決定版本號
- [ ] 依據 §2 規則決定新版本號（`Major` / `Minor` / `Patch`）
- [ ] 確認第四位 Revision 固定為 `0`

### Step 2：執行 bump_version.ps1（自動同步版本）
- [ ] 執行 `./scripts/bump_version.ps1 -Type minor`（或 `major` / `patch`）
- [ ] 腳本自動更新：`Directory.Build.props`、`packaging/msix/AppxManifest.xml`、`CHANGELOG.md`、`docs/StoreListing_*.md`

### Step 3：手動更新文件（補充變更細節）
- [ ] **CHANGELOG.md**：將 TODO 標記替換為具體的更新內容與修復項目。
- [ ] **docs/ROADMAP.md**：更新里程碑勾選狀態（`[ ]` → `[x]`）。
- [ ] **docs/StoreListing_*.md**（5 國語言）：更新 What's new 與新功能特色說明。

### Step 4：本機驗證
- [ ] 執行單元測試：`dotnet test Tapster.sln -c Release`
- [ ] 執行可攜式單一檔打包：`powershell -File scripts/build_portable.ps1`
- [ ] 執行 MSIX 打包測試：`powershell -File scripts/build_msix.ps1`
- [ ] 清理建置進程：`dotnet build-server shutdown`

### Step 5：提交與推送
- [ ] 原子化提交：版本升級與文件變更遵循 Conventional Commits 提交。
- [ ] 建立 PR 並等待全數 CI Checks 通過後合入 `main`。
- [ ] 推送正式發布標籤（例：`git tag v1.2.0.0; git push origin v1.2.0.0`）。

---

## 1. 版本號結構與微軟商店限制

Tapster 嚴格遵循四位數版本號格式：`Major.Minor.Patch.Revision`（例如 `1.1.0.0`）。

> [!IMPORTANT]
> **微軟商店強制限制**：
> 提交至 Microsoft Store 的 MSIX 套件，其版本號第四位（Revision）**必須強制為 `0`**。
> 若第四位為非 `0`（例如 `1.1.0.1`），合作夥伴中心（Partner Center）將拒絕上傳。

因此，Tapster 的版本號結構定義如下：

$$\text{Version} = \text{Major} . \text{Minor} . \text{Patch} . \mathbf{0}$$

- **Major (主版本號)**：重大架構重組（例如切換核心運行庫或大型重構，目前為 `1`）。
- **Minor (次版本號)**：引入全新模組或子系統（例如新增腳本錄製分析器、進階熱鍵巨集佇列）。
- **Patch (修訂版本號)**：日常 Bug 修復、GUI 細微調整、熱鍵相容性優化與**商店退件緊急修復**。
- **Revision**：**永遠鎖定為 `0`**。

---

## 2. 升級與修補規則

### 2.1 一般版本遞增
- **日常維護與修復**：`1.1.0.0` ➔ `1.1.1.0`
- **重大功能模組新增**：`1.1.0.0` ➔ `1.2.0.0`

### 2.2 商店退件與緊急修補 (Hotfix)
若送審微軟商店被退件，或正式上架後發現重大崩潰，**禁止遞增第四位**，必須遞增第三位（Patch）重新送審：
- `1.1.0.0` ➔ `1.1.1.0` ➔ `1.1.2.0`

---

## 3. 發布時必須更新的檔案清單

當發布新版本時，以下檔案必須同步保持最新狀態：

1. **`Directory.Build.props`**：全域 `<Version>`，影響所有編譯出的 `.dll` 與 `.exe`。
2. **`packaging/msix/AppxManifest.xml` 與 `Tapster.Fluent/Package.appxmanifest`**：MSIX 套件識別版本 `<Identity Version="..." />`（前者為 Store MSIX 打包之權威來源，後者由 `bump_version.ps1` 自動同步對齊供 IDE 打包）。
3. **`CHANGELOG.md`**：記載使用者可見的變更摘要。
4. **`docs/StoreListing_*.md`**：五種語言商店送審文本。

---

## 4. Git 標籤 (Tag) 與自動發布

- **標籤格式**：與版本號完全一致，前綴小寫 `v`，例如 `v1.1.0.0`。
- **標籤推送範例**：
  ```bash
  git tag v1.1.0.0
  git push origin v1.1.0.0
  ```
- **自動觸發**：推送標籤後，GitHub Actions `.github/workflows/release.yml` 將自動建置兩款二進位產物並發布至 GitHub Releases。
