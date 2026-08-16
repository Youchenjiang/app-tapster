# Tapster Agent Authorization Gate

This file is the mandatory first-read rule for every AI assistant and automation agent working in the Tapster repository.

---

## 🚦 Action Classification Before Any Tool Call

Before performing any tool call, categorize the action into one of three tiers:

1. **Read-only**:
   - Inspect files, Git status/history, test results, or external state.
   - *Status*: **Allowed by default**.
2. **Local edit**:
   - Modify or create files only when explicitly requested by the user.
   - *Status*: Allowed for the requested task. **Does not imply permission to commit, push, or publish**.
3. **External mutation / Git commits**:
   - Any branch operation, commit, push/force-push, tag modification, PR operation, release creation, or workflow dispatch.
   - *Status*: **Requires explicit authorization from the user** in the current conversation turn.

---

## 🛡️ Critical Invariants & Non-Negotiable Rules

1. **Strict Background Process Hygiene**:
   - **NEVER** leave persistent `.NET Host`, `MSBuild.exe`, or compiler worker processes lingering in the user's background.
   - Always verify `Directory.Build.props` has `<NodeReuse>false</NodeReuse>` and invoke `dotnet build-server shutdown` after builds.
2. **Commit Policy Conformance**:
   - All commit headers must strictly follow `type(scope): subject` Conventional Commits.
   - All commit bodies must contain a numbered English list (`1. ...`).
3. **Scope Non-Transitivity**:
   - Authorization for task A never extends to task B. Always report verification evidence and confirm before proceeding to irreversible state changes.
4. **Privacy & Offline Integrity**:
   - Never introduce remote analytics, tracking SDKs, cloud telemetry, or external network requests into `Tapster.Core` or `Tapster.Fluent`.
