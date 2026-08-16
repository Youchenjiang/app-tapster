# Tapster (Native & Fluent)

Windows 11 現代化鍵鼠輸入自動化工具，支援 **WinUI 3 Fluent 桌面介面** 與 **NativeAOT 輕量命令列** 雙軌運行。

[Read English Version](README.md) · [查看發展藍圖與歷史演進 (Roadmap)](docs/ROADMAP.md)

---

## 🌟 核心架構與模式

| 模式 | 專案 | 特色 | 適用情境 |
|---|---|---|---|
| **Fluent GUI** | `Tapster.Fluent` | WinUI 3 現代介面、5 排擬真鍵盤、Mica、全域熱鍵喚醒、置頂 | 一般使用者 / Microsoft Store |
| **Native CLI** | `src/Tapster/` | C# .NET NativeAOT 編譯、體積僅 2-3MB、零外部依賴 | 腳本批次自動化 / 伺服器維運 |

---

## 與 Python 舊版差異

| | Python 舊版 (`../tapster/`) | Native / Fluent 現代版 (`本目錄`) |
|---|---|---|
| **底層技術** | Python 3 + `keyboard` + `pyperclip` | C# .NET + Win32 `SendInput` API |
| **圖形介面** | Tkinter 分頁介面 | WinUI 3 (Windows App SDK) Fluent 介面 |
| **記憶體與延遲** | ~45MB / 毫秒級 | ~8-15MB / 微秒級時序 |
| **打包體積** | ~8MB (PyInstaller) | ~2-3MB (AOT CLI) / 原生 MSIX |
| **全域喚醒** | 無 | 支援 `Ctrl + Alt + T` 原生熱鍵呼叫 |

---

## 功能總覽

1. **Auto Typer** — 逐字模擬實體鍵盤輸入（支援 Unicode UTF-16，繞過輸入法與 VNC 限制）
2. **Key Holder** — 擬真 5 排實體鍵盤點選、實體按鍵 Capture 捕捉、長按（定時/無限/輪替）
3. **Auto Clicker** — 極速滑鼠連點（左/右/中鍵、毫秒級頻率、連點時按住修飾鍵、十字準星座標拾取）
4. **Macro Recorder** — 鍵鼠動作錄製與高解析時序回放（支援 0.1x~5.0x 倍速播放）

---

## 環境需求

- .NET 8 SDK ([下載](https://dotnet.microsoft.com/download/dotnet/8.0))
- Windows 10/11

---

## 建置

### 開發模式
```bash
dotnet build
```

### 發佈 NativeAOT (推薦)
```bash
dotnet publish -c Release -r win-x64 --self-contained
```

輸出位置：`src/Tapster/bin/Release/net8.0-windows/win-x64/publish/Tapster.exe`

---

## 使用方式

```bash
# 自動打字
tapster type --text "Hello World" --delay 5

# 長按按鍵 30 秒
tapster hold --key w --duration 30

# 連點滑鼠左鍵 100 次
tapster click --button left --interval 50 --count 100

# 指定坐標連點
tapster click --x 500 --y 300 -c 10

# 無限連點 (Ctrl+C 停止)
tapster click --interval 100
```

### 參數說明

| 參數 | 說明 | 預設值 |
|---|---|---|
| `--delay` | 啟動延遲秒數 | 3 |
| `--count` | 執行次數 (0=無限) | 0 |
| `--interval` | 間隔毫秒數 | 100 |
| `--key` | 要長按的按鍵 | w |
| `--duration` | 長按秒數 (0=直到 Esc) | 10 |
| `--button` | 滑鼠按鍵 (left/right/middle) | left |
| `--x`, `--y` | 目標坐標 | - |

---

## 授權條款

MIT License
