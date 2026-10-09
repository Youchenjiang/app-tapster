# Agent Persistent Memory

> **Every agent session MUST read this file first** (defined in `.agent/rules.md`).
> **Every agent session MUST update this file before ending.**

---

## 🔑 User Preferences
- **Language**: 繁體中文 preferred for casual conversation; code/commits in English.
- **Style**: Direct, no fluff. Get things done with high engineering rigor.
- **Git Commit Protocol**: Strict Conventional Commits (`type(scope): subject` <= 72 chars, no trailing period, numbered English body `1. ...`).
- **Authorization Gate**: Always confirm before any external mutations (`git push`, release tags, PR creation).
- **Zero GitHub Comments / Chat**: 嚴禁在 GitHub PR / Issue 上留下任何文字評論、回覆或機器人文字。處理 Review Feedback 時只需完成代碼修復、推送到分支、並將 review thread 標記為 resolved，絕對不發布任何文字留言。

---

## 📋 Current Active Tasks
- [x] Initial project setup & scaffolding completed (`project-scaffold` desktop preset).
- [x] Fixed GitHub Actions workflows (`setup-dotnet` commit SHA, `scorecard-action` v2.4.4, dependabot policy exemption).
- [x] Deployed governance scripts & workflows via PR #19 (merged into `main`).
- [x] Resolved CodeQL security alerts & WinUI false positives via PR #23 & #24.
- [x] Refactored C# code quality & eliminated SonarCloud smells via PR #25.
- [x] Merged Dependabot grouped update PRs #21 (NuGet) and #22 (GitHub Actions).
- [x] **Clickra Alignment - Phase 1: Architecture & Developer Documentation (`docs/`)** (PR #26)
  - [x] Add `docs/ARCHITECTURE.md` & `docs/ARCHITECTURE_AND_FRAMEWORK.md`
  - [x] Add `LOCAL_BUILD_NOTES.md` (environment setup, build quirks, developer certs)
  - [x] Add `docs/WINDOWS_COMPATIBILITY_AND_MSIX_SANDBOX.md` & `docs/TROUBLESHOOTING_AND_RESOLUTIONS.md`
  - [x] Add multilingual `docs/StoreListing_*.md` (EN, ZH, ZH-CN, JA, KO)
- [x] **Clickra Alignment - Phase 2: Quality Config & Developer Tooling (`scripts/`)** (PR #27)
  - [x] Add `.deepsource.toml` for static code health analysis
  - [x] Add `scripts/bump_version.ps1` for synchronized SemVer bumps
  - [x] Add `scripts/setup/create_dev_cert.ps1` for local certificate generation
- [x] **Clickra Alignment - Phase 3: Core Unit Tests & CI Automation (`tests/`)** (PR #28)
  - [x] Scaffold `tests/Tapster.Core.Tests` (xUnit test project linked to `Tapster.sln`)
  - [x] Add unit tests for key parsing, action recording/playback models, and speed calculations
  - [x] Integrate `dotnet test` into `.github/workflows/ci.yml`
- [x] **Market Convenience - [P3-8] & [P3-9]**: Global Hotkeys, Hold Mode, and Key Spammer (PR #36)
- [x] **Market Convenience - [P3-10]**: Clipboard Paste Mode, Trailing Keys, Jitter & Auto-Restore (PR #38)
- [x] **Hardware Safety Safeguards**: Panic Kill, Mouse Shake & Triple-Esc (PR #44)
- [x] **Release 1.2.0.0**: Minor version bump across props, manifests and changelog (chore/bump-version-1.2.0.0)
- [x] **UI Scrollability Fix**: Wrap Typer, Clicker & Macro panels in ScrollViewer for small screen accessibility (fix/fluent-scrollable-panels)

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
- **CodeQL Query Suite Standard**: Use `security-extended` rather than `security-and-quality` for CodeQL SAST scanning in desktop apps with Win32 P/Invoke and XAML generators, eliminating stylistic linter noise without suppressing any security rules.
- **CancellationToken in Worker Loops**: Pre-capture `var token = _cts?.Token ?? CancellationToken.None;` to avoid redundant null checks and unreachable branch warnings (SonarCloud S2589, S2583).
- **Macro Recorder Thread Safety**: Catch `ThreadInterruptedException` on `Thread.Join` and ensure state cleanup (`_recordThread = null; _stopwatch.Stop();`) runs inside a `finally` block.
- **PR Iteration Hygiene**: Avoid excessive force-pushing during active PR reviews to preserve review thread context; append atomic fix commits instead and squash at merge.
- **WinUI 3 Window Dragging Performance vs. Native DWM (PR #35)**: Never use `ExtendsContentIntoTitleBar = true`, `SetTitleBar()`, or `AppWindow.TitleBar` in WinUI 3 as they funnel non-client drag events into DirectComposition, dropping framerate to 30~45 FPS under 1000Hz mice. Always maintain native Win32 non-client drag and inject Windows 11 DWM attributes (`DWMWA_USE_IMMERSIVE_DARK_MODE`, `DWMWA_CAPTION_COLOR = 0x00202020`, `DWMWA_TEXT_COLOR = 0x00FFFFFF`) via encapsulated safe wrappers (`NativeMethods.SetWindowAttribute`). Relocate title bar controls (e.g. Always on top) to `NavigationView.PaneFooter`.
- **WinUI 3 XAML Event Handlers vs. Static Method Linters**: Generated code in `*.g.cs` references `this.MethodName`, so making event handlers static causes compiler error `CS0176`. For simple controls, wire events dynamically in code-behind (`Loaded`) via lambdas to eliminate false positive code smells (SonarCloud S2325).
- **PR Creation & Body Validation Protocol (`tools/pr_helper.py`)**: Never run `gh pr create --body "..."` directly via PowerShell strings, which causes quote stripping, backtick escaping to backslashes, and markdown truncation. Always use `python tools/pr_helper.py` (`generate`, `lint`, or `create` with `--body-file`) to enforce the 3 mandatory sections (`## Summary`, `## Key Changes`, `## Verification`) and apply repository labels.
- [x] **Zero GitHub Comment Noise**: 永遠不要在 GitHub PR / Issue 上調用 comment/reply API 發表任何文字回覆。自動化/機器人審查只需要用「代碼修復 + Resolve Thread」回應即可，發送文字評論純屬多餘噪音並冒用使用者發言，已被永久列入禁用操作。
- **WinUI 3 Panel Scrollability & Window Sizing**: Fixed issue where controls below the fold (e.g. Target Coordinates, Jitter in Auto Clicker) or Target Text in Auto Typer were vertically collapsed or unreachable when window height is small. Wrapped all functional panels in `ScrollViewer` with `VerticalScrollBarVisibility="Auto"` and `HorizontalScrollBarVisibility="Auto"`, set `MinHeight="140"` on `TypeTextBox`, intercepted `WM_GETMINMAXINFO` to enforce minimum window dimensions (680x480) with DPI scaling, added text wrapping to settings labels to prevent truncation, and enforced healthy window size (980x680) upon initial window activation.
- **Action Button State & Empty Input Validation**: In Auto Typer, Auto Clicker (Spammer), Key Holder, and Macro Replay, action buttons (`TyperActionBtn`, `MacroActionBtn`, etc.) are now disabled whenever their required inputs are empty (e.g. empty `TypeTextBox`, 0 recorded macro actions, empty holder key). Dynamic state updates are wired via `TextChanged` and recording lifecycle events, with pre-flight checks in `RunTaskAsync` and remote hotkey toggles ensuring task countdowns never trigger when inputs are missing.
- **Nested ScrollViewer Elimination in Key Holder**: Removed inner redundant `ScrollViewer` wrapping `KeyboardContainer` inside Key Holder panel. WinUI 3 child `ScrollViewer` controls capture mouse wheel (`PointerWheelChanged`) events even when vertical scrolling is disabled, which previously prevented the outer panel from scrolling vertically when hovering over the virtual keyboard.
- **Macro Action Item & Clear Button State Disabling**: Disabled `EditActionBtn` and `DeleteActionBtn` when no action is selected in `MacroActionList` or when the list is empty. Synchronized `ClearMacroBtn`, `ClearTextBtn`, and `ClearHolderKeyBtn` to be disabled when their corresponding input targets contain no content or during active task runs.
- **NavigationView PaneFooter & About Screen Label Truncation Fix**: In compact pane mode (`IsPaneOpen == false`), `PaneFooter` is now collapsed to avoid truncated hint text (`(Ctrl+`) and orphan checkbox squares, while adding a dedicated AlwaysOnTop toggle to `SettingsPanel`. In the About screen, wrapped Technical Specifications labels and used `TextOnAccentFillColorPrimaryBrush` for the version badge to ensure readability.
- **Auto Clicker Target Coordinates Clear Button**: Added a dedicated `ClearCoordsBtn` next to `Pick Location` in the Auto Clicker Target Coordinates control group. Clearing sets coordinate boxes to `double.NaN` ("Current" cursor position), immediately hides the on-screen target marker crosshair overlay, and automatically updates button enabled states based on coordinate presence and task lifecycle.
- **Key Holder Inline Action Buttons Layout**: Replaced the separate top-right header layout with an inline Grid containing `HolderKeyBox`, `CaptureKeyBtn`, and `ClearHolderKeyBtn`. This prevents the action buttons from being pushed out of view when the virtual keyboard extends the panel's horizontal scroll width.
- **Dynamic Virtual Keyboard Highlights**: Removed hardcoded static `AccentButtonStyle` from `Esc` and `Enter`. Registered all virtual keyboard buttons in `_keyboardButtons` and dynamically synchronize `AccentButtonStyle` vs `DefaultButtonStyle` to match the exact keys present in `HolderKeyBox.Text`. Clicking a key now toggles it in the combo string, immediately updating the visual selection state.
- **Navigation Pane Spacing Stabilization**: Locked `NavigationView` to `PaneDisplayMode="Left"` with `AlwaysShowHeader="False"` and `Header="{x:Null}"`, removing dynamic template header padding calculations when opening and closing the hamburger pane. Cleaned up redundant `PaneFooter` elements now that Always on Top is cleanly integrated into Settings.
- **Fluent UI Code Quality & Review Fixes (PR #46)**:
  - Guarded `UpdateVirtualKeyboardHighlights` against empty collection analysis (SonarCloud S4158) with an early return resetting buttons when key count is 0.
  - Stored virtual keyboard buttons as `Dictionary<string, List<Button>>` to support multi-button modifier highlights (both Shift/Ctrl/Alt keys).
  - Treated default `"w"` in `HolderKeyBox` as replaceable on initial virtual key clicks to prevent unintended combo appending.
  - Locked input clearing and macro step editing during active tasks by invoking `UpdateAllActionBtnStates()` immediately after setting `_isRunning = true`.
  - Extracted pre-flight validation and countdown loop from `RunTaskAsync` to helper methods, reducing cognitive complexity below SonarCloud S3776 threshold.
  - Replaced repetitive `"Holder"` and `"Macro"` tab literals with constant identifiers (SonarCloud S1192).
  - Suppressed false-positive unreachable code analysis (SonarCloud S2583) on XAML control state in virtual keyboard highlights.
- **Numpad and Navigation Key Aliases in Core**: Added VK mappings in `Keyboard.cs` for numpad numeric keys (`num0`..`num9`), arithmetic operators (`num+`, `num-`, `num*`, `num/`, `num.`), `NumLock`, `ScrollLock`, `PrintScreen`, and `Pause` to power full-sized virtual keyboard configurations.
- **Full 96% Laptop Keyboard Layout with Aligned Arrows and Numpad**: Upgraded virtual keyboard to a full 96% laptop layout. Row 5 `◄` right edge aligns with Row 4 `Shift` right edge; `▲` (Row 4) and `▼` (Row 5) right edges align with `Backspace` / `\` / `Enter`; `►` (Row 5) right edge aligns with `NumLk` / `7` / `4` / `1`; completed with top function navigation keys and full 4-column numpad.
