# Tapster (Native & Fluent)

Modern Windows 11 keyboard and mouse automation utility, featuring both a **WinUI 3 Fluent Desktop GUI** and a lightweight **NativeAOT CLI**.

[閱讀繁體中文版](README.zh-TW.md) · [View Roadmap & History](docs/ROADMAP.md)

---

## 🌟 Core Architecture & Modes

| Mode | Project | Highlights | Target Scenario |
|---|---|---|---|
| **Fluent GUI** | `Tapster.Fluent` | WinUI 3 modern UI, 5-row physical virtual keyboard, Mica, global hotkey wake-up, Always on Top | Standard Users / Microsoft Store |
| **Native CLI** | `src/Tapster/` | C# .NET NativeAOT build, ~2-3MB size, zero external dependencies | Scripting batch automation / Server maintenance |

---

## vs Legacy Python Version

| | Legacy Python (`../tapster/`) | Modern Native / Fluent (`This directory`) |
|---|---|---|
| **Backend Engine** | Python 3 + `keyboard` + `pyperclip` | C# .NET + Win32 `SendInput` API |
| **GUI Framework** | Tkinter tabbed interface | WinUI 3 (Windows App SDK) Fluent Design |
| **Memory & Latency** | ~45MB / Millisecond | ~8-15MB / Microsecond timing |
| **Package Size** | ~8MB (PyInstaller) | ~2-3MB (AOT CLI) / Native MSIX |
| **Global Wake-up** | None | `Ctrl + Alt + T` Native Hotkey |

---

## Features Overview

1. **Auto Typer** — Keystroke-by-keystroke simulation with Unicode UTF-16 support (bypasses IME and VNC clipboard restrictions)
2. **Key Holder** — 5-row physical virtual keyboard grid, physical Key Capture, timed/indefinite/rotation hold
3. **Auto Clicker** — Ultra-fast mouse clicking (Left/Right/Middle, ms-level interval, Hold-Key during click, Crosshair Coordinate Picker)
4. **Macro Recorder** — Mouse & keyboard event recording and high-resolution timing replay (0.1x to 5.0x speed multiplier)

---

## Prerequisites

- .NET 8 SDK ([Download](https://dotnet.microsoft.com/download/dotnet/8.0))
- Windows 10/11

---

## Build

### Development
```bash
dotnet build
```

### Release (NativeAOT - Recommended)
```bash
dotnet publish -c Release -r win-x64 --self-contained
```

Output: `src/Tapster/bin/Release/net8.0-windows/win-x64/publish/Tapster.exe`

---

## Usage

```bash
# Auto-type text
tapster type --text "Hello World" --delay 5

# Hold 'w' key for 30 seconds
tapster hold --key w --duration 30

# Click left mouse button 100 times
tapster click --button left --interval 50 --count 100

# Click at specific coordinates
tapster click --x 500 --y 300 -c 10

# Infinite clicking (Ctrl+C to stop)
tapster click --interval 100
```

### Options

| Option | Description | Default |
|---|---|---|
| `--delay` | Startup delay in seconds | 3 |
| `--count` | Number of iterations (0=infinite) | 0 |
| `--interval` | Interval in milliseconds | 100 |
| `--key` | Key to hold | w |
| `--duration` | Hold duration in seconds (0=until Esc) | 10 |
| `--button` | Mouse button (left/right/middle) | left |
| `--x`, `--y` | Target coordinates | - |

---

## License

MIT License
