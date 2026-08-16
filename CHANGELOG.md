# Changelog

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
