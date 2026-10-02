# Agent Persistent Memory

> **Every agent session MUST read this file first** (defined in `.agent/rules.md`).
> **Every agent session MUST update this file before ending.**

---

## 🔑 User Preferences
- **Language**: 繁體中文 preferred for casual conversation; code/commits in English.
- **Style**: Direct, no fluff. Get things done with high engineering rigor.
- **Git Commit Protocol**: Strict Conventional Commits (`type(scope): subject` <= 72 chars, no trailing period, numbered English body `1. ...`).
- **Authorization Gate**: Always confirm before any external mutations (`git push`, release tags, PR creation).

---

## 📋 Current Active Tasks
- [x] Initial project setup & scaffolding completed (`project-scaffold` desktop preset).
- [x] Fixed GitHub Actions workflows (`setup-dotnet` commit SHA, `scorecard-action` v2.4.4, dependabot policy exemption).
- [x] Deployed governance scripts & workflows via PR #19 (merged into `main`).
- [ ] Phase 3: Macro JSON schema export/import (`P3-1`).
- [ ] Phase 3: Humanized jitter delay simulation (`P3-5`).
- [ ] Phase 4: Multi-language i18n (`resw` for zh-TW, en-US, zh-CN, ja-JP, ko-KR) (`P4-1`).

---

## 🏗️ Architectural Context
- **Project**: Tapster (Native & Fluent 鍵鼠自動化助手)
- **Preset**: desktop (Windows / .NET 8+ / WinUI 3 / WinAppSDK / NativeAOT)
- **Core Engine (`src/Tapster/`)**: Pure Win32 P/Invoke (`SendInput`, `GetAsyncKeyState`), Unicode UTF-16 input, microsecond timing, zero bloat, offline only.
- **Modern UI (`Tapster.Fluent/`)**: WinUI 3 + Windows App SDK, Mica material, realistic 5-row keyboard layout, per-panel self-contained control cards, Win32 system tray resident, global wake hotkey (`Ctrl + Alt + T`).
- **Single-File Launcher (`src/Tapster.Launcher/`)**: Standalone 43MB executable with embedded payload, SHA-256 integrity auto-sync, graceful instance shutdown.
- **Packaging (`packaging/`, `scripts/`)**: Dual-track release (`scripts/build_portable.ps1` -> `Tapster.exe`, `scripts/build_msix.ps1` -> `Tapster-*.msix`).

---

## ✅ Completed Decisions & Lessons Learned
- **Build Server Cleanup**: Must run `dotnet build-server shutdown` after MSBuild tasks to eliminate persistent worker processes in user background (`NodeReuse=false`).
- **Action Commit SHA Hygiene**: When pinning GitHub Actions to commit SHAs, always verify the exact commit hash via `git ls-remote` (avoid typographical errors like `ddab` vs `cbab`).
- **GCR Deprecation**: `ossf/scorecard-action` v2.4.0 relied on deprecated GCR image requiring billing; v2.4.4+ is required for public repository scanning.
- **Policy Scopes**: Must maintain Tapster-specific scopes (`core`, `fluent`, `launcher`, `tray`, `macro`, `packaging`, `tapster`) across `policy.yml` and `tools/lint_commits.py`.
- **Dependabot Grouping**: Configure grouped updates (`groups: github-actions`, `groups: nuget`) in `.github/dependabot.yml` to consolidate dependency bumps and avoid PR spamming.
- **CodeQL Float Conversion**: For progress or ratios, perform multiplication directly in floating-point (`(double)a * b`) instead of casting integer multiplication `(double)(a * b)` to eliminate CodeQL CWE-190 warnings.
- **CodeQL Path Exclusion**: `github/codeql-action/init` requires `config-file: ./.github/codeql/codeql-config.yml` to specify `paths-ignore` for compiler-generated code (`obj/**`, `bin/**`).
- **PR Iteration Hygiene**: Avoid excessive force-pushing during active PR reviews to preserve review thread context; append atomic fix commits instead and squash at merge.