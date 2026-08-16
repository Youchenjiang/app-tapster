# Tapster AI Agent Engineering Guidelines

This document defines core behavioral, architectural, and quality rules for AI agents operating in the Tapster codebase.

---

## 1. Architectural Principles

- **Zero Unnecessary Dependencies**: `Tapster.Core` must remain 100% dependency-free, relying strictly on Win32 P/Invoke APIs (`user32.dll`, `kernel32.dll`, `shell32.dll`).
- **Thread Safety & UI Responsiveness**: Heavy automation loops, delays, and macro replays must execute on background tasks (`Task.Run`) and communicate with WinUI 3 controls via `DispatcherQueue.TryEnqueue`.
- **100% Offline by Design**: Never introduce HTTP clients, analytics SDKs, cloud crash loggers, or telemetry into runtime assemblies.

---

## 2. Process Hygiene Rules

- **Always Stop Build Servers**: Every script or command that invokes `dotnet build` or `dotnet publish` must be accompanied by `dotnet build-server shutdown` to ensure user systems are not polluted with lingering worker processes.
- **Single-File Runtime Extraction**: `Tapster.Launcher` manages payloads extracted to `%LOCALAPPDATA%\Tapster\app-v<version>\`. Always verify processes are terminated before modifying runtime DLLs.

---

## 3. Commit & PR Standards

- **Conventional Commits**: Every commit header must follow `type(scope): subject` with an allowed scope (`core`, `fluent`, `launcher`, `cli`, `msix`, `tray`, `macro`, `docs`, `ci`, `deps`, `store`, `agent`, `packaging`, `build`, `tapster`).
- **Numbered English Body**: Every commit must provide a numbered English list of key technical changes.
- **Maximum Length**: Header must not exceed 72 characters and must not end with a period.
