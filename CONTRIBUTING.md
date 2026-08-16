# Contributing to Tapster

Tapster follows strict commit, branch, and engineering quality policies inspired by the Clickra and MealLedger standards. The goal is to keep Git history clean, easily bisectable, auditable, and resilient for long-term maintenance.

---

## 🌿 Branch Rules

- `main` branch must always remain clean, stable, and buildable (`dotnet build` passes with 0 errors).
- **Direct push to `main` is discouraged** for multi-step features.
- Create topic branches branched from `main`:
  - `feat/<short-name>` for new features
  - `fix/<short-name>` for bug fixes and patches
  - `docs/<short-name>` for documentation updates
  - `chore/<short-name>` for maintenance, CI, and build script adjustments
- Keep branches short-lived and rebase on `main` before submitting a PR.

---

## 📝 Commit Format & Convention

Every commit header and pull request title must follow **Conventional Commits**:

```text
<type>(<scope>): <description>
```

### Allowed Types
`feat`, `fix`, `refactor`, `docs`, `test`, `chore`, `style`, `perf`, `security`

### Allowed Scopes
`core`, `fluent`, `launcher`, `cli`, `msix`, `tray`, `macro`, `docs`, `ci`, `deps`, `store`, `agent`, `packaging`, `build`, `tapster`

### Header Rules
- Lowercase description starting with a letter or number.
- Maximum 72 characters.
- **No trailing period (`.`)**.
- No vague descriptions (e.g. `update`, `misc`, `stuff`, `fix bug`).

### Body Requirement
Commit message bodies must contain a numbered English list explaining key changes:

```text
feat(fluent): add dedicated execution control card to macro panel

1. Embed delay box and variable speed slider into macro action card.
2. Implement live playback step progress bar callback.
3. Support global Esc cancellation across background tasks.
```

---

## ⚛️ Atomic Commit Rules

One commit must represent exactly **one logical unit of change**:
- Separate unrelated features into distinct commits.
- Separate architectural refactoring from behavior changes.
- Keep build/cleanup scripts decoupled from UI changes.
- Never mix real secrets, tokens, or personal logs into commits.

---

## 🛡️ Supply Chain & Privacy Rules

- **Zero Network Telemetry**: Tapster is a 100% offline local utility. No keystroke recording or user activity shall ever be sent over the network.
- **Never Commit Sensitive Data**: Do not commit private keys, certificate passwords (`.pfx` without test passwords), or personal files.
- **Build Process Isolation**: Always ensure build scripts invoke `dotnet build-server shutdown` in `finally` blocks to prevent persistent `.NET Host` background processes.

---

## 🚀 Verification Expectations

Before opening a PR or merging commits:
1. Run `dotnet build Tapster.sln -c Release` to ensure 0 errors.
2. Verify both packaging pipelines produce clean artifacts:
   - `powershell -File scripts/build_portable.ps1` -> `publish/Tapster.exe`
   - `powershell -File scripts/build_msix.ps1` -> `publish/Tapster-v1.1.0.msix`
3. Confirm background process state is completely clean (`Stop-BuildServers`).
