# Changelog

## [1.2.0.0] - 2026-10-08

### Added
- **Hardware Panic Kill & Safety Safeguards**: Mouse shake gesture detection (>2500 px/s threshold), rapid triple-Esc cancellation guard (<500ms sliding window), and global F10 panic hotkey.
- **Click Target Marker Overlay**: Zero-lag layered overlay window using native GDI `UpdateLayeredWindow`, sequential numbering tags, dynamic click ripple animations, and coordinate picker.
- **Market Convenience Suite**: Global action hotkeys (F6–F9), Hold-to-Click mode, dedicated Key Spammer, time and position jitter anti-detection.
- **Macro Filtering & Editor**: Live mouse move noise filtering, macro step deletion, and fine-grained macro inspector.
- **Clipboard Typer Mode**: High-speed large text typing via Windows clipboard with trailing Enter/Tab keys and automated clipboard content restoration.

### Changed
- Refactored `MacroRecorder` replay execution to minimize cognitive complexity and improve thread safety.
- Transitioned window drag handling to native DWM caption hit-testing, eliminating mouse drag latency on high-polling rate mice.
- Updated project test tooling (Microsoft.NET.Test.Sdk 18.10.1, xunit.runner.visualstudio 4.0.0, coverlet.collector 10.1.0) and dependencies to their latest compatible versions.

## [1.1.1.0] - 2026-10-04

### Fixed
- Fixed MSIX launch crash caused by duplicate WinUI 3 runtime binaries and unpackaged packaging flags conflicting with `Microsoft.WindowsAppRuntime.2` (`CoreMessagingXP.dll` `0xc0000602`).

### Changed
- Parameterized `WindowsPackageType` and `WindowsAppSDKSelfContained` in `Tapster.Fluent.csproj` to support dynamic overrides across MSIX and Portable builds.
- Updated `build_msix.ps1` to publish with `-p:WindowsPackageType=MSIX -p:WindowsAppSDKSelfContained=false`.
- Explicitly pinned `build_portable.ps1` to `-p:WindowsPackageType=None -p:WindowsAppSDKSelfContained=true`.

All notable changes to **Tapster** will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [1.1.0] - 2026-08-16

### Added
- **True Standalone Single-File Launcher (`Tapster.Launcher`)**: Embedded payload compression, SHA-256 integrity hash verification, automated process termination before extraction to prevent DLL file locks, and single-instance window activation.
- **Decoupled Per-Panel Action Cards**: Embedded delay box, live progress bars (typing characters, hold countdown, click counts, macro playback steps), and Start/Stop controls directly into Typer, Holder, Clicker, and Macro panels.
- **Dedicated About Page**: Architecture specs, version tags, 100% offline privacy statement, and one-click appdata directory launcher.
- **OpenSSF Supply Chain Security Suite**: OpenSSF Scorecard audit, CodeQL SAST scanning, Dependabot weekly updates, and comprehensive security policy.
- **Repository Governance**: Repository Policy CI enforcement, PR & Issue templates, local `commit-msg` git hook, and `AGENTS.md` authorization gate.

### Changed
- Refactored `MacroRecorder` to run on a dedicated high-frequency background worker thread supporting relative millisecond timestamps and signed bitwise key state polling.
- Replaced global bottom footer with self-contained execution cards across all functional views.
- Globally configured `Directory.Build.props` to disable MSBuild `NodeReuse` and `UseSharedCompilation`, ensuring zero lingering background processes.

### Fixed
- Fixed launcher file locking issue (`clrjit.dll used by another process`) when launching consecutive instances during updates.
- Fixed window title bar drag lag by isolating the drag region from interactive controls.
- Fixed missing window and taskbar icons by embedding multi-size ICO resources directly into the PE headers.

---

## [1.0.0] - 2026-08-01

### Added
- **Core Automation Engine (`Tapster.Core`)**: Native C# .NET Win32 `SendInput` keyboard and mouse simulator.
- **Fluent Desktop Interface (`Tapster.Fluent`)**: WinUI 3 modern user interface featuring Windows 11 Mica backdrop and dark/light themes.
- **5-Row Physical Virtual Keyboard**: Realistic keycaps with standard physical spacing and dedicated modifier keys.
- **System Tray Residency**: Minimize to system tray on window close, tray icon menu, and global wake-up hotkey (`Ctrl + Alt + T`).
- **Settings Persistence**: User preferences persisted to `%LOCALAPPDATA%\Tapster\settings.json`.
- **Dual-Track Packaging**: Standalone portable zip and signed Microsoft Store MSIX package.
