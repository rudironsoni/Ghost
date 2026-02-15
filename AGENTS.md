# Agent Instructions

## 1. Policy Hierarchy

- MUST: absolute requirement.
- SHOULD: default requirement unless deviation is documented.
- MAY: optional behavior.
- MUST resolve conflicts in this order: explicit user instruction, this file, reference docs.
- MUST treat verification and truthfulness requirements as non-overridable.

## 2. Scope Baseline

- MUST target .NET 10 (`net10.0`).
- MUST use `dotnet` CLI for restore, build, test, and format.
- MUST treat this file as enforcement policy.

## 3. Agent Execution Contract

- MUST run `bd ready --json` before non-trivial work.
- MUST set issue to in progress before code edits: `bd update <id> --status in_progress --json`.
- MUST implement minimal focused changes.
- MUST run mandatory verification sequence from repository root:
  - `dotnet format Ghost.sln --verify-no-changes`
  - `dotnet restore Ghost.sln`
  - `dotnet build Ghost.sln --no-restore --warnaserror`
  - `dotnet test Ghost.sln --no-build`
  - MUST run `dotnet test Ghost.sln` if `--no-build` artifacts are unavailable.
- MUST produce mandatory final output schema.
- MUST close completed issue: `bd close <id> --reason "Completed" --json`.
- MUST NOT edit code before `in_progress` except explicit user one-off waiver.

## 4. Zero-Tolerance Completion Rules

- MUST NOT claim done, complete, fixed, shipped, or equivalent unless all mandatory checks pass in current session.
- MUST treat any build warning as completion-blocking.
- MUST treat any build error as completion-blocking.
- MUST treat any required test failure as completion-blocking.
- MUST treat skipped or unexecuted required tests as completion-blocking.
- MUST leave issue open when verification is incomplete.
- MUST output `NOT VERIFIED: <reason>` when verification is incomplete.
- MUST NOT retry until green without root-cause fix.
- MUST NOT hide failures via unreported filters.
- MUST NOT suppress analyzers without linked issue, owner, and expiry.

## 5. Stop Conditions

- MUST STOP on format failure.
- MUST STOP on restore failure.
- MUST STOP on build warning or build error.
- MUST STOP on required test failure.
- MUST STOP when expected tests discover zero tests.
- MUST STOP when destructive operations would affect source code outside the task scope.
- MUST STOP when filesystem operations encounter case-sensitivity conflicts.
- MUST STOP when `rm`, `git mv`, or deletion operations would remove existing implementation code.
- MUST fix root cause.
- MUST rerun full mandatory verification sequence after fixes.
- MUST NOT close issue while any mandatory check is unresolved.

## 6. Verification Matrix

- Code changes: MUST run format, restore, build, and test.
- Test-only changes: MUST run format, restore, build, and test.
- Docs-only changes: MAY skip verification, MUST output `NOT VERIFIED (docs-only)` when skipped.

## 7. Test Implementation and Execution

- MUST add or update automated tests for behavior-changing code unless impossible.
- MUST document explicit deviation reason when tests are not added or updated.
- MUST NOT treat "no tests run" as compliant for code or test changes.
- MUST use current-session test results only.
- MUST use `dotnet test` only.
- MUST NOT use wrapper test scripts.
- MUST run all tests:
  - `dotnet test Ghost.sln --no-build`
  - MUST use `dotnet test Ghost.sln` when artifacts are missing.
- MUST fix failing tests before completion claim.
- MUST rerun full mandatory verification sequence after test fixes.
- MUST NOT narrow test scope to bypass failures unless user explicitly approves and final status is `NOT VERIFIED`.

## 8. Forbidden Test Flags

- MUST NOT use `--configuration Release` in test commands.
- MUST NOT use `--maxcpucount:1` in test commands.
- MUST NOT use `-nodereuse:false` in test commands.
- MUST enforce this ban in local commands, scripts, and CI snippets.
- MUST treat PRs introducing these flags as non-compliant.

## 9. Flaky Test Protocol

- MUST reproduce without forced serialization and without node-reuse toggles.
- MUST collect diagnostics when hangs or flakes occur:
  - `dotnet test <target> --blame-hang --logger "trx;LogFileName=test-results.trx"`
- MUST fix root cause.
- MUST rerun full mandatory verification sequence.
- MUST document cause and fix in issue notes and final evidence.

## 10. Risk Tier Model

- MUST classify change risk as low, medium, or high.
- Low risk: MUST satisfy standard mandatory verification.
- Medium risk: MUST satisfy standard mandatory verification, blast radius, and rollback notes.
- High risk: MUST satisfy standard mandatory verification, blast radius, rollback plan, migration impact, observability impact, and residual risk.

## 11. Change Control

- Non-trivial changes MUST include blast radius.
- Non-trivial changes MUST include rollback steps.
- Non-trivial changes MUST include migration impact statement when applicable.
- Non-trivial changes MUST include follow-up issue IDs when additional work exists.

## 12. Exception Process

- Temporary policy deviations MUST include all fields:
  - bypassed rule,
  - approving owner,
  - reason and accepted risk,
  - expiry date/time,
  - follow-up issue ID.
- MUST treat missing fields as non-compliant.

## 13. .NET 10 Operational Controls

- MUST validate SDK pinning via `global.json` before major edits.
- MUST respect lockfile and deterministic restore policy where configured.
- MUST use locked restore mode when policy requires it.
- MUST maintain CI parity.
- MUST NOT downgrade analyzers.
- MUST NOT relax warning policy.
- MUST NOT add hidden suppressions.

## 14. CI and Branch Protection Alignment

- MUST match local verification to protected-branch CI gates.
- MUST apply stricter CI-equivalent checks locally when CI is stricter than defaults.

## 15. Security and Supply Chain

- MUST NOT commit plaintext secrets in code, logs, tests, fixtures, or artifacts.
- MUST run required secret scanning checks before completion claim.
- MUST run required dependency audit checks before completion claim.
- MUST use approved package sources only.
- MUST enforce lockfile policy.
- MUST pin CI actions, images, and containers to approved immutable references where required.
- MUST provide SBOM or provenance evidence when release policy requires it.

## 16. Data Governance

- MUST classify touched data as public, internal, confidential, or restricted.
- MUST mask confidential and restricted data in logs, fixtures, and artifacts.
- MUST sanitize data used in tests.
- MUST NOT copy production data into tests without explicit approval.

## 17. Database Migration Safety

- MUST use forward-only migrations when applicable.
- MUST use expand/contract for breaking schema transitions.
- MUST include backfill strategy when applicable.
- MUST include rollback safety steps for partial or failed rollout.

## 18. Observability

- Runtime changes MUST include structured logs.
- Runtime changes MUST preserve or add correlation identifiers.
- Runtime changes MUST validate telemetry coverage for changed endpoints, jobs, or workers.

## 19. Performance and Reliability

- Hot-path changes MUST include before/after performance evidence for latency and allocation.
- MUST NOT ship known regressions without approved exception and follow-up issue.
- Concurrency changes MUST include race, ordering, and failure-path tests.

## 20. Mandatory Verification Commands

- `dotnet format Ghost.sln --verify-no-changes`
- `dotnet restore Ghost.sln`
- `dotnet build Ghost.sln --no-restore --warnaserror`
- `dotnet test Ghost.sln --no-build`

## 21. Mandatory Final Output Schema

- MUST output sections in this exact order:
  1. Verification Evidence
  2. Risk Assessment
  3. Rollback Plan
  4. Follow-up Issues
  5. Final Status
- Verification Evidence MUST include:
  - commands run,
  - PASS or FAIL per command,
  - tests discovered, passed, failed, skipped,
  - deviations and rationale.
- Final Status MUST be `VERIFIED` or `NOT VERIFIED: <reason>`.

## 22. Definition of Done

- MUST satisfy all mandatory verification checks in current session.
- MUST have zero build warnings and zero build errors.
- MUST have required tests passing.
- MUST have behavior-changing code covered by test additions or documented deviation.
- MUST document risk tier, blast radius, rollback, and migration impact when applicable.
- MUST satisfy security and data-governance requirements.
- MUST keep bd workflow accurate.
- MUST follow mandatory final output schema.
- MUST commit and push changes.

## 23. Session Completion Protocol

- MUST execute in order:
  1. `git status`
  2. `git add <files>`
  3. `bd sync`
  4. `dotnet format Ghost.sln --verify-no-changes`
  5. `dotnet restore Ghost.sln`
  6. `dotnet build Ghost.sln --no-restore --warnaserror`
  7. `dotnet test Ghost.sln --no-build`
  8. `git commit -m "..."`
  9. `bd sync`
  10. `git push`
  11. `git status`
- MUST NOT claim completion before successful push.

## 24. Critical Guardrails (Zero-Tolerance)

### 24.1 Scope Discipline (NEVER Expand Scope)

- MUST NOT implement cosmetic fixes (naming, casing, formatting) discovered during unrelated work.
- MUST NOT refactor code outside the explicitly assigned issue scope.
- MUST document discovered issues with `bd create` and link as dependencies, NOT fix them immediately.
- MUST focus on minimal, focused changes that satisfy ONLY the acceptance criteria.
- MUST ask user for explicit permission before expanding scope in any way.

### 24.2 Data Loss Prevention (Source Code Protection)

- MUST NEVER use `rm -rf`, `git mv`, or deletion operations on existing source code directories.
- MUST NEVER attempt to "clean up" or "fix" filesystem issues by deleting files.
- MUST verify file existence before any move/delete: `ls <path>` and confirm with user.
- MUST create explicit backups before any filesystem restructuring: `cp -r <src> <src>.backup.$(date +%s)`.
- MUST STOP immediately if filesystem operations encounter errors or unexpected states.

### 24.3 Filesystem Safety (Case Sensitivity)

- MUST NOT attempt case-sensitive renames on case-insensitive filesystems (macOS APFS, Windows NTFS).
- MUST recognize that `Sdk` and `SDK` are identical on case-insensitive filesystems.
- MUST document casing inconsistencies in RFC/docs, NOT attempt to fix them with `git mv`.
- MUST use explicit user confirmation before any directory restructuring operations.

### 24.4 Catastrophic Error Prevention

- MUST STOP and ASK USER when:
  - More than 5 files would be modified outside the task scope
  - Existing implementation code would be deleted or moved
  - Git operations fail or show unexpected output
  - Build fails after filesystem changes
- MUST verify workspace integrity before claiming completion: `git status`, `dotnet build`.
- MUST NOT use `--force` or `--no-verify` flags to bypass safety checks without user approval.

## 25. Project Structure

- Layer 0: `src/Core/Ghost/` - Core engine, stealth, sessions, proxies
- Layer 1: `src/Contracts/` - Interfaces, DTOs, shared contracts
- Layer 2: `src/Plugins/` - Platform-specific plugins (LinkedIn, Indeed, Google, etc.)
- Layer 3: `src/Hosting/` - WebAPI, workers, CLI
- Layer 4: `src/Sdk/` - Spider framework for building scrapers
- Tests: `tests/Plugins/` mirrors plugin layout

**Note:** Architecture migrated from Platforms to Plugins. All platform implementations live in `src/Plugins/`.

## 26. Reference Documents

- `docs/agent-playbook.md`
- `docs/testing-reference.md`
- `docs/dotnet10-ops.md`

<!-- BEGIN BEADS INTEGRATION -->
## 27. Issue Tracking with bd

- MUST use bd for non-trivial task tracking.
- MUST run required workflow:
  - `bd ready --json`
  - `bd show <id> --json`
  - `bd update <id> --status in_progress --json`
- MUST link discovered work with dependency metadata when applicable.
- MUST close completed work with `bd close <id> --reason "Completed" --json`.
- MUST run `bd sync` during completion.
- MUST use `--json` for agent-driven workflows.
- MUST NOT use markdown TODO files as tracking system.

<!-- END BEADS INTEGRATION -->

## Landing the Plane (Session Completion)

**When ending a work session**, you MUST complete ALL steps below. Work is NOT complete until `git push` succeeds.

**MANDATORY WORKFLOW:**

1. **File issues for remaining work** - Create issues for anything that needs follow-up
2. **Run quality gates** (if code changed) - Tests, linters, builds
3. **Update issue status** - Close finished work, update in-progress items
4. **PUSH TO REMOTE** - This is MANDATORY:
   ```bash
   git pull --rebase
   bd sync
   git push
   git status  # MUST show "up to date with origin"
   ```
5. **Clean up** - Clear stashes, prune remote branches
6. **Verify** - All changes committed AND pushed
7. **Hand off** - Provide context for next session

**CRITICAL RULES:**
- Work is NOT complete until `git push` succeeds
- NEVER stop before pushing - that leaves work stranded locally
- NEVER say "ready to push when you are" - YOU must push
- If push fails, resolve and retry until it succeeds
