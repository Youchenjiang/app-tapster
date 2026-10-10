# Changelog

## [1.3.0.0] - 2026-10-10

### Added
- **Multi-Language Localization Engine**: Full runtime dynamic UI localization across Traditional Chinese (繁體中文), Simplified Chinese (简体中文), English, and Japanese (日本語), supporting persistent app settings and automatic system default language matching (PR #49).
- **Multi-Stage Key Holder Pipeline**: Interactive step timeline with live step cards, customizable repeat counts, configurable step durations, and seamless combo-to-pipeline additions (PR #48).
- **96% Compact Virtual Keyboard**: Integrated full 4-key navigation arrow cluster and dedicated 17-key numpad with dual-column responsive grid layout and bidirectional key state highlights (PR #47).

### Changed
- **Unified Navigation Labels**: Streamlined navigation tabs into concise two-character Chinese labels (打字, 長按, 連點, 重放) and eliminated mixed English/Chinese labels throughout all panels (PR #49).
- **Fluent UI Accessibility & Layout**: Added full scrollviewer wrapping across all main functional panels (Typer, Holder, Clicker, Macro) with minimum window height and width safeguards to prevent UI clipping on compact screens (PR #46).
- **Input Validation & Safety Guards**: Disabled execution buttons when necessary inputs are empty to prevent invalid operations (PR #46).

### Fixed
- **Code Quality & Cognitive Complexity**: Decomposed large UI localization and step rendering methods into static null-safe setters, reducing SonarCloud cognitive complexity to 0 and eliminating duplicated string literals (PR #49).
- **Virtual Keyboard Spacing & Layout Alignment**: Fixed numpad alignment gap, eliminated unwanted nested scrollviewers, and centered the virtual keyboard in the step card pipeline (PR #47, PR #48).

## [1.2.0.0] - 2026-10-08

### Added
- **Hardware Panic Kill & Safety Safeguards**: Mouse shake gesture detection (>2500 px/s threshold), rapid triple-Esc cancellation guard (<500ms sliding window), and global F10 panic hotkey (PR #44).
- **Click Target Marker Overlay**: Zero-lag layered overlay window using native GDI `UpdateLayeredWindow`, sequential numbering tags, dynamic click ripple animations, and coordinate picker (PR #40, PR #41).
- **Market Convenience Suite**: Global action hotkeys (F6–F9), Hold-to-Click mode, dedicated Key Spammer, time and position jitter anti-detection (PR #36, PR #37).
- **Macro Filtering & Editor**: Live mouse move noise filtering, macro step deletion, and fine-grained macro inspector (PR #39).
- **Clipboard Typer Mode**: High-speed large text typing via Windows clipboard with trailing Enter/Tab keys and automated clipboard content restoration (PR #38).

### Changed
- Refactored `MacroRecorder` replay execution to minimize cognitive complexity and improve thread safety (PR #44).
- Transitioned window drag handling to native DWM caption hit-testing, eliminating mouse drag latency on high-polling rate mice (PR #35).
- Updated project test tooling (Microsoft.NET.Test.Sdk 18.10.1, xunit.runner.visualstudio 4.0.0, coverlet.collector 10.1.0) and dependencies to their latest compatible versions (PR #42, PR #43).

### Fixed
- Fixed launcher portable extraction path to dynamically derive application version, preventing downgrade extraction collisions.
- Fixed About screen release badge to reflect current 1.2.0.0 release.

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
