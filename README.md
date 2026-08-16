# Tapster (Native & Fluent)

[![OpenSSF Scorecard](https://api.scorecard.dev/projects/github.com/Youchenjiang/Tapster/badge)](https://scorecard.dev/viewer/?url=github.com/Youchenjiang/Tapster)
[![Security Policy](https://img.shields.io/badge/Security-Policy-blue.svg)](.github/SECURITY.md)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![Platform: Windows 11](https://img.shields.io/badge/Platform-Windows%2011%20Fluent-0078D4.svg)](https://microsoft.com/windows)

Modern Windows 11 keyboard and mouse automation utility, featuring both a **WinUI 3 Fluent Desktop GUI** and a lightweight **NativeAOT Core**.

[閱讀繁體中文版](README.zh-TW.md) · [View Roadmap & Specifications](docs/ROADMAP.md) · [Security Policy](.github/SECURITY.md)

---

## 🌟 Core Architecture & Distribution

| Distribution | Project / Format | Highlights | Target Scenario |
|---|---|---|---|
| **Standalone Portable** | `publish/Tapster.exe` | True single-file EXE (43.1MB), zero dependencies, embedded payload with SHA-256 auto-update sync | Portable / USB drive / Quick launch |
| **Microsoft Store MSIX** | `publish/Tapster-v1.1.0.msix` | Signed MSIX package (33.9MB), auto updates, full sandbox compliance | End Users / Corporate Environments |
| **Native Core Engine** | `src/Tapster/` | C# .NET NativeAOT build (< 35MB RAM, < 1ms latency), zero external dependencies | Headless automation / Embedding |

---

## 🛡️ OpenSSF Supply Chain Security Guarantee

Tapster strictly complies with **OpenSSF (Open Source Security Foundation)** security standards:
- **100% Offline & Zero Telemetry**: Operates exclusively on local Win32 native APIs without sending any network requests.
- **Automated Security Audits**: Weekly automated OpenSSF Scorecard supply-chain scans and CodeQL SAST analyses.
- **Hardened CI/CD**: Pinned GitHub Actions commit hashes and least-privilege token permissions (`permissions: read-all`).

---

## Features Overview

1. **Auto Typer** — Keystroke-by-keystroke simulation with Unicode UTF-16 support (bypasses IME and VNC clipboard restrictions), independent live progress feedback.
2. **Key Holder** — Realistic 5-row physical virtual keyboard grid, full 0x08~0xFE key capture, timed/indefinite hold.
3. **Auto Clicker** — Ultra-fast mouse clicking (Left/Right/Middle, ms-level interval, Crosshair coordinate picker, live click count).
4. **Macro Recorder** — Global input capture with relative millisecond timing and variable speed replay (0.1x to 10.0x).
5. **System Tray & Hotkey** — Minimizes to system tray on close, global wake-up hotkey (**`Ctrl + Alt + T`**), Always on Top.

---

## Prerequisites

- .NET 8 / 10 SDK ([Download](https://dotnet.microsoft.com/download))
- Windows 10 (20H2+) / Windows 11

---

## Build

### 1. Build Solution
```powershell
dotnet build Tapster.sln -c Release
```

### 2. Package True Standalone Single-File EXE
```powershell
powershell -ExecutionPolicy Bypass -File "scripts/build_portable.ps1"
```
Output: `publish/Tapster.exe` (43.1 MB)

### 3. Package & Sign Store MSIX
```powershell
powershell -ExecutionPolicy Bypass -File "scripts/build_msix.ps1"
```
Output: `publish/Tapster-v1.1.0.msix` (33.9 MB)

---

## License

This project is licensed under the [MIT License](LICENSE).
