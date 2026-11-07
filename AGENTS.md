# Permanent contributor / agent execution-safety policy

> Owner directive, 7 November 2026. Applies to ChatGPT, Codex and other unattended developer agents when they operate on a contributor's actively used Windows workstation. Do not reveal device identifiers or private environment details in this public repository. Project-specific product and release requirements remain in force.

## Non-disruptive workstation operation
- Never open, switch to, or focus visible consoles, terminals, browsers, installers, dialogs, game windows, settings screens or other GUI applications from unattended development work. Do not steal focus or switch virtual desktops.
- Launch permitted tasks only through an already-approved **invisible native Windows background executor**, with no console window and with redirected stdin/stdout/stderr. Native Windows only on the protected workstation: no WSL. If invisible execution cannot be verified, **fail closed** rather than risk interrupting the owner.
- Do not assume a hidden parent process prevents child GUI windows. Keep child commands non-GUI and noninteractive; do not ask for or bypass interactive approvals.

## Ownership, isolation and release
- Inspect the current Git branch/worktree and concurrent writers before any write. Never terminate or disturb other workers; preserve uncommitted files, running applications, private assets, user data and backups.
- Read-only is the default. Source changes require an explicitly approved **separate isolated Git worktree/clone**, appropriate sandbox, ownership safeguards, local logs, timeouts and tests. Do not silently work in a live owner's workspace.
- No force-push, protected/default-branch updates, merges, production deployments, scheduled-task changes, service/driver changes or public releases without owner approval. Prepare reviewable draft pull requests and report accurate CI/acceptance status.
- Keep credentials, machine addresses, private logs, proprietary artifacts and account data out of this repository.
- For every development continuation, finish with a complete self-contained copy-paste-ready plain-text recovery handover: exact source authority, work performed, actual tests, remaining blockers, rollback and next steps.

**Important:** This document instructs agents; it does not itself provide OS process isolation. Actual Windows launching must use the approved background mechanism. Existing product, licensing and domain rules are not replaced.
