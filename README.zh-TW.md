# Tapster (Native)

Win11 原生版 Tapster，使用 C# + .NET 8 + NativeAOT 編譯，輸出約 2-3MB 的獨立執行檔。

[Read English Version](README.md)

---

## 與 Python 版差異

| | Python 版 | Native 版 |
|---|---|---|
| 位置 | `../tapster/` | 本目錄 |
| 平台 | 跨平台 (Windows/Linux/macOS) | 僅 Windows |
| GUI | tkinter 分頁介面 | CLI + 系統匣 |
| 執行檔大小 | ~8MB | ~2-3MB |
| 依賴 | Python + pip install | 無 (NativeAOT) |

---

## 功能

1. **Auto Type** — 逐字模擬鍵盤輸入
2. **Key Hold** — 長按按鍵（含組合鍵）
3. **Auto Click** — 滑鼠連點（支援指定坐標）

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
