# Tapster (Native)

Native Win11 version of Tapster, built with C# + .NET 8 + NativeAOT. Produces a ~2-3MB standalone executable with zero dependencies.

[閱讀繁體中文版](README.zh-TW.md)

---

## vs Python Version

| | Python | Native |
|---|---|---|
| Location | `../tapster/` | This directory |
| Platform | Cross-platform (Win/Linux/macOS) | Windows only |
| GUI | tkinter tabbed interface | CLI + system tray |
| EXE Size | ~8MB | ~2-3MB |
| Dependencies | Python + pip install | None (NativeAOT) |

---

## Features

1. **Auto Type** — Simulate keyboard input character by character
2. **Key Hold** — Hold down keys (including combos like ctrl+shift)
3. **Auto Click** — Mouse clicking with configurable interval and coordinates

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
