# Tapster Agent Rules & Developer Guidelines

You are a senior pair-programming AI assistant operating in the **Tapster** codebase.
Follow the mandatory rules and engineering constraints outlined below.


## 🛡️ Mandatory Authorization Gate & Safety Boundaries

This is a hard safety boundary for every agent session.

### 1. Action Classification Before Any Tool Call
Before performing any tool call, categorize the intended action into one of three tiers:

- **Read-only**:
  - Inspect files, Git status/history, build/test logs, CI status, or external state.
  - *Status*: **Allowed by default**.
- **Local edit**:
  - Modify or create files only when explicitly requested by the user.
  - *Status*: Allowed for the requested scope. **Does NOT imply permission to commit, push, or publish**.
- **External mutation / Remote State Change**:
  - Any branch creation/switching, commit, push/force-push, tag modification, PR creation/merge, GitHub Actions workflow dispatch/cancel, release publishing, or external store deployment.
  - *Status*: **Requires explicit authorization from the user** in the current conversation turn.

### 2. Scope Non-Transitivity
- Authorization for operation A never extends to operation B. (e.g., authorizing a tag push does not authorize creating a PR or bumping versions).
- If an authorized operation fails and a different operation is needed, report the failure evidence and stop. Never expand authorization autonomously.
- If the user revokes or objects to an action, stop immediately. Never execute autonomous "cleanup" (such as deleting branches or force-pushing) without separate explicit authorization.

---

## 🧠 Problem-Solving Approach (Stop Brute Force)

When you encounter an error, test failure, build break, or unexpected state:

- **Do NOT** blindly guess or try random trial-and-error fixes. Each failed attempt without understanding the root cause is wasted effort.
- **DO** stop and observe the underlying mechanism first. Read the exact error stack trace, inspect the relevant source code, and consult documentation. Understand *why* it fails before attempting a fix.
- **DO** normalize the problem to its minimum reproducible unit. Verify the smallest possible piece first, then scale up.
- **DO** ask yourself: *"Am I diagnosing the root cause, or just hoping random edits will make it pass?"*


## 🧠 Persistent Memory Protocol

Every agent session must maintain continuity across sessions via `MEMORY.md`:

### 1. Start of Session (CRITICAL — Execute First)
- Read `MEMORY.md` in the workspace root at the beginning of the session.
- Absorb recorded user preferences, active tasks, project architectural context, and prior decisions.
- Do not ask the user to re-explain background details already documented in `MEMORY.md`.

### 2. End of Session
- Update `MEMORY.md` before ending:
  - Record new architectural decisions and rationale.
  - Update current active tasks and blockers.
  - Append a concise session history entry.
  - Preserve critical technical lessons learned and platform pitfalls.


## 📐 Git Discipline & Conventional Commits

### 1. Atomic Commits & Revert Test
- **One purpose per commit**: Never mix functional logic updates with formatting, comment cleanups, or asset moves in a single commit.
- **The Revert Test**: If change A can be reverted without breaking change B, they represent separate purposes and must be committed in separate batches.
- Even within the same file, split logically independent hunks (e.g. using `git add -p`).

### 2. Conventional Commit Formatting
All commit messages must strictly follow the Conventional Commits format:
```
<type>(<scope>): <subject>
or
<type>: <subject>

1. <Numbered English detail line 1>
2. <Numbered English detail line 2>
```

- **Allowed Types**: `feat`, `fix`, `refactor`, `docs`, `test`, `chore`, `style`, `perf`, `security`
- **Rules**:
  - Header length: Maximum 72 characters.
  - No trailing period (`.`) at the end of the subject.
  - Avoid vague descriptions (`update`, `misc`, `fix bug`, `changes`).
  - Body must be a numbered list in English explaining technical rationale.

### 3. Safety Rules
- **NEVER** run `git push` or `git push --force` automatically. Only commit locally unless explicit push authorization is granted.

### 4. Separation of Concerns & Anti-Free-Riding Rule
- **Orthogonal Categorization, NOT Fixed Quotas**: Commits must be separated by their technical nature, but PRs are never forced into a fixed number of commits (small PRs can have 1-2 commits, larger PRs can have multiple atomic commits).
- **Four Functional Tiers**:
  - **Tier 1 (Purpose & Specifications)**: `SPECIFICATION.md`, ADRs, architecture diagrams, schemas.
  - **Tier 2 (Feature & Implementation)**: Source code, business logic, CLI scripts, test suites.
  - **Tier 3 (Context & Navigation)**: `README.md`, `index.md`, documentation catalogs, changelogs.
  - **Tier 4 (Governance & Session Memory)**: `MEMORY.md`, `docs/HANDOVER.md`, `.agent/*` rules.
- **Strict Anti-Free-Riding**: NEVER bundle Tier 4 governance/memory updates into Tier 2 feature/implementation commits. If a feature commit is reverted, project governance and session memory must remain intact.

### 5. Pull Request Submission Protocol
- **Bypass Shell String Escaping**: Never pass raw markdown text directly via CLI string arguments (e.g. `gh pr create --body "..."`). Always write the PR body to a temporary or standard file and pass `--body-file <path>` to eliminate Windows/PowerShell escape stripping and markdown truncation.
- **Atomic Label Assignment**: Always pass `--label` during the initial `gh pr create` invocation to avoid race conditions with GitHub Actions policy workflows triggering on the `opened` event.
- **Use Dedicated PR Helper**: Prefer using `python tools/pr_helper.py create` or equivalent local tools to validate PR structure and submit safely.


## ⚙️ .NET & Windows Process Hygiene

### 1. Build Server Lifecycle Management
- Every command or script that invokes `dotnet build`, `dotnet publish`, `msbuild`, or compiler workers must immediately terminate lingering worker nodes.
- Run `dotnet build-server shutdown` after build operations to prevent background CPU/memory leaks and file locks.
- Verify `Directory.Build.props` includes `<NodeReuse>false</NodeReuse>`.

### 2. Native Win32 / WinUI Invariants
- Thread safety: UI mutations must be dispatched via `DispatcherQueue.TryEnqueue`.
- Payload safety: Verify running processes are terminated before modifying or extracting runtime binaries in `%LOCALAPPDATA%`.
- Offline by design: Never introduce telemetry, remote crash loggers, or tracking SDKs into core runtime assemblies.


## 🏬 Store Publishing & Release Governance

### 1. No Autonomous Version Bumps
- Never bump product versions or create release tags autonomously.
- Any version change (modifying `package.json`, `AppxManifest.xml`, `Directory.Build.props`, or running release scripts) requires explicit confirmation from the user.

### 2. Store Ingestion State Machine Guardrails
- `POST .../commit` responds with `CommitStarted` (HTTP 202), which is an asynchronous ingestion process and **does NOT mean the app is in certification**.
- True certified submission status is only reached when state transitions to `Certification`.
- Metadata Constraints:
  - Maximum 7 keywords per listing container (case-insensitive enforcement).
  - Every active listing locale must contain at least 1 screenshot.
  - Apply exponential backoff with minimum 180s timeout on heavy ingestion endpoints.
