# Tapster (Native & Fluent 原生鍵鼠自動化助手)

[![OpenSSF Scorecard](https://api.scorecard.dev/projects/github.com/Youchenjiang/Tapster/badge)](https://scorecard.dev/viewer/?url=github.com/Youchenjiang/Tapster)
[![Security Policy](https://img.shields.io/badge/Security-Policy-blue.svg)](.github/SECURITY.md)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![Platform: Windows 11](https://img.shields.io/badge/Platform-Windows%2011%20Fluent-0078D4.svg)](https://microsoft.com/windows)

現代化 Windows 11 鍵盤與滑鼠輸入自動化控制台，結合 **WinUI 3 Fluent 現代桌面應用程式** 與超輕量 **NativeAOT 核心**。

[Read English Version](README.md) · [查看發展藍圖 (ROADMAP)](docs/ROADMAP.md) · [安全性政策](.github/SECURITY.md)

---

## 🌟 核心架構與發行雙軌制

| 發行版本 | 專案 / 產出 | 特色亮點 | 適用情境 |
|---|---|---|---|
| **綠色免安裝單檔** | `publish/Tapster.exe` | 真正純單一 `.exe` 檔案（43.1MB），內嵌 Payload 與 SHA-256 自動同步，零依賴解壓即跑 | 隨身碟攜帶 / 快速啟動 / 免安裝 |
| **微軟商店 MSIX** | `publish/Tapster-v1.1.0.msix` | 標準微軟簽署 `.msix` 安裝包（33.9MB），自動更新管理與完全沙盒隔離 | 一般使用者 / 企業受控環境 |
| **底層 Native 核心** | `src/Tapster/` | C# .NET Native 核心（記憶體 < 35MB，延遲 < 1ms），純 Win32 原生調用 | 高性能自動化 / 模組嵌入 |

---

## 🛡️ OpenSSF 軟體供應鏈安全承諾

Tapster 嚴格遵守 **OpenSSF（開源安全基金會）** 安全標準：
- **100% 離線純本機運行**：絕不發送任何外部網路請求，零遙測資料收集。
- **自動化安全審計**：每週自動執行 OpenSSF Scorecard 供應鏈審查與 CodeQL SAST 靜態代碼掃描。
- **CI/CD 權限最小化**：GitHub Actions 全面導入 SHA-Pinned Action 與最小權限宣告（`permissions: read-all`）。

---

## 功能規格亮點

1. **Auto Typer (自動打字機)** — 以實體鍵擊事件逐字模擬打入文字，支援 Unicode UTF-16 直投（自動繞過中英輸入法與遠端 VNC 剪貼簿限制），獨立字元即時進度反饋。
2. **Key Holder (按鍵長按器)** — 擬真 5 排實體虛擬鍵盤、0x08~0xFE 全鍵盤按鍵捕捉（Capture Key）、支援計時倒數與無限保持。
3. **Auto Clicker (極速連點器)** — 高頻滑鼠連點（左鍵/右鍵/中鍵，毫秒級間隔）、十字準星取點與即時點擊計數。
4. **Macro Recorder (巨集錄製器)** — 背景多執行緒即時捕捉鍵鼠動作，高解析度時間戳與多倍速回放（0.1x 至 10.0x）。
5. **系統匣常駐與熱鍵** — 關閉自動縮小至系統匣、全域喚醒熱鍵（**`Ctrl + Alt + T`**）、視窗永遠置頂。

---

## 建置與打包

### 1. 建置專案方案
```powershell
dotnet build Tapster.sln -c Release
```

### 2. 打包純單一 .exe 綠色可攜版
```powershell
powershell -ExecutionPolicy Bypass -File "scripts/build_portable.ps1"
```
產出：`publish/Tapster.exe` (43.1 MB)

### 3. 打包並簽署 Store MSIX 安裝套件
```powershell
powershell -ExecutionPolicy Bypass -File "scripts/build_msix.ps1"
```
產出：`publish/Tapster-v1.1.0.msix` (33.9 MB)

---

## 授權條款

本專案採用 [MIT License](LICENSE) 授權條款開源發布。
