# Agent Instructions v2

This document is the enforcement policy for agents working in this repository.

## 1. Purpose and Non-Negotiables

### 1.1 Purpose

Deliver changes quickly with deterministic local feedback while preserving strict safety and completion guardrails.

### 1.2 Policy Hierarchy

When instructions conflict, resolve in this order:
1. Explicit user instruction
2. This file
3. Referenced repository docs

Verification truthfulness and safety requirements are non-overridable.

### 1.3 Scope Baseline

- MUST target `.NET 10` (`net10.0`).
- MUST use `dotnet` CLI for restore/build/test/format commands.
- MUST keep changes minimal and focused to the assigned issue scope.
- MUST classify risk for each implementation: `low | medium | high`.

### 1.4 Policy Interfaces

The following contracts are required for every implementation task:

- `VerificationProfile`: `two-stage | strict-full | risk-tiered`
- `RiskTier`: `low | medium | high`
- `LiveProviderGate`: `required | optional`
- `VerificationEvidence`: command, PASS/FAIL, tests discovered/passed/failed/skipped, deviations, rationale

### 1.5 Defaults (when epic is silent)

- `VerificationProfile=two-stage`
- `RiskTier=medium`
- `LiveProviderGate=required`

## 2. Hard Guardrails

### 2.1 Safety and Truthfulness

- MUST NOT claim done/fixed/completed unless all required gates for the active profile pass in the current session.
- MUST report incomplete verification as `NOT VERIFIED: <reason>`.
- MUST NOT hide failures via unreported filters, silent retries, or undisclosed bypasses.
- MUST treat build warnings as completion-blocking.
- MUST NOT suppress analyzers without linked issue, owner, reason, and expiry.

### 2.2 Scope Discipline

- MUST NOT expand scope without explicit user approval.
- MUST NOT perform unrelated cosmetic/refactor work.
- MUST file discovered follow-up work as linked `bd` issues.

### 2.3 Source Protection

- MUST NOT use destructive operations that remove existing implementation code or directories.
- MUST STOP and ask user before any operation that would delete or move existing source implementation.
- MUST NOT use force bypasses (`--force`, `--no-verify`) without explicit approval.

### 2.4 Test Integrity

- MUST add or update tests for behavior changes unless impossible; if impossible, MUST document deviation.
- MUST use current-session test results only.
- MUST use `dotnet test` commands directly (no wrapper scripts for compliance evidence).
- MUST NOT narrow required scope to bypass failures unless explicitly approved; final status must then be `NOT VERIFIED`.

## 3. Workflow Contract (bd + Execution Lifecycle)

### 3.1 Required bd Lifecycle

1. Run `bd ready --json` before non-trivial work.
2. Set issue to in progress before edits: `bd update <id> --status in_progress --json`.
3. Perform implementation and required verification.
4. Close completed issue only after required verification passes: `bd close <id> --reason "Completed" --json`.

### 3.2 Session Discipline

- MUST keep issue status accurate.
- MUST leave issue open whenever required verification is incomplete.
- MUST run from repository root.

### 3.3 Command Catalog (Canonical)

Use this catalog as the single source of truth for required command sequences.

- `FORMAT_FULL`: `dotnet format Ghost.sln --verify-no-changes`
- `FORMAT_CHANGED`: `dotnet format Ghost.sln --verify-no-changes --include <changed-files>`
- `RESTORE`: `dotnet restore Ghost.sln`
- `BUILD`: `dotnet build Ghost.sln --no-restore --warnaserror`
- `TEST_FAST`: `dotnet test Ghost.sln --no-build --filter "Category!=Smoke&Category!=End2End&Capability!=RequiresProviderLive"`
- `TEST_FAST_FALLBACK`: `dotnet test Ghost.sln --filter "Category!=Smoke&Category!=End2End&Capability!=RequiresProviderLive"`
- `TEST_FULL`: `GHOST_E2E=1 dotnet test Ghost.sln --no-build`
- `TEST_FULL_FALLBACK`: `GHOST_E2E=1 dotnet test Ghost.sln`
- `TEST_HANG_DIAG`: `dotnet test <target> --blame-hang --logger "trx;LogFileName=test-results.trx"`

Forbidden test flags:
- `--configuration Release`
- `--maxcpucount:1`
- `-nodereuse:false`

## 4. Verification Profiles (Default + Overrides)

### 4.1 Profile Selection

- Use epic-defined profile when present.
- Otherwise apply defaults from Section 1.5.

### 4.2 `two-stage` (default)

#### Fast Loop (during implementation)

1. `FORMAT_CHANGED`
2. `BUILD`
3. `TEST_FAST`
4. If `--no-build` artifacts are unavailable, run `TEST_FAST_FALLBACK`.

Fast loop purpose: reduce feedback latency while excluding `Smoke`, `End2End`, and `RequiresProviderLive` capability lanes.

#### Final Blocking Gate (before close/push)

1. `FORMAT_FULL`
2. `RESTORE`
3. `BUILD`
4. `TEST_FULL`
5. If `--no-build` artifacts are unavailable, run `TEST_FULL_FALLBACK`.

Final gate purpose: enforce robust completion validation including live-provider-gated tests (`GHOST_E2E=1`).

### 4.3 `strict-full`

Run full gate sequence for every validation cycle, not only at final gate:
1. `FORMAT_FULL`
2. `RESTORE`
3. `BUILD`
4. `TEST_FULL` (or `TEST_FULL_FALLBACK` if needed)

### 4.4 `risk-tiered`

- `low`: same as `two-stage` fast loop during implementation; full gate required before completion.
- `medium`: same as default `two-stage`.
- `high`: run `strict-full` for each validation cycle and include explicit blast radius, rollback, migration/observability impact, and residual risk notes.

### 4.5 Docs-only Change Rule

If only docs are changed, verification MAY be skipped and final status MUST be:
- `NOT VERIFIED (docs-only)`

### 4.6 Live Provider Capability Rule

If `LiveProviderGate=required` and local environment cannot run the full live-provider gate, do not close the issue. Final status MUST be:
- `NOT VERIFIED: <reason>`

## 5. Stop Conditions and Failure Handling

MUST STOP immediately on:
- format failure
- restore failure
- build warning or build error
- required test failure
- expected test run discovers zero tests
- destructive operation risk outside task scope
- case-sensitivity filesystem conflict

Failure handling protocol:
1. Fix root cause.
2. Rerun from the failed stage.
3. Before completion claim, rerun the full final blocking gate for the active profile.

Flaky/hang protocol:
1. Reproduce without forced serialization or node-reuse toggles.
2. Run `TEST_HANG_DIAG`.
3. Fix root cause (no blind retry loops).
4. Rerun full final blocking gate.

## 6. Evidence and Final Output Schema

Final outputs MUST include sections in this exact order:
1. `Verification Evidence`
2. `Risk Assessment`
3. `Rollback Plan`
4. `Follow-up Issues`
5. `Final Status`

### 6.1 Verification Evidence Requirements

For every required command in the active profile, report:
- exact command
- `PASS` or `FAIL`
- tests discovered/passed/failed/skipped (for test commands)
- deviations and rationale

### 6.2 Final Status Values

Allowed final status values:
- `VERIFIED`
- `NOT VERIFIED: <reason>`
- `NOT VERIFIED (docs-only)`

## 7. Session Completion and Push Requirements

Do not claim completion before successful push.

Required completion sequence:
1. `git status`
2. `git add <files>`
3. `bd sync`
4. run required verification sequence for active profile
5. `git commit -m "..."`
6. `bd sync`
7. `git pull --rebase`
8. `git push`
9. `git status` (must show up to date with origin)

If push fails, resolve and retry until success or report `NOT VERIFIED: <reason>`.

## 8. References and Ownership

### 8.1 Canonical References

- `docs/rfc/testing-lanes.md`
- `docs/rfc/testing-architecture-overhaul.md`
- `docs/flaky-test-policy.md`
- `docs/PRE_COMMIT_SETUP.md`
- `README.md`

### 8.2 Ownership

- Policy owner: repository maintainers for Ghost agent workflow.
- Policy changes MUST be tracked via `bd` issue and reviewed before merge.

### 8.3 Phase 2 Follow-Up Scope (Tracked Separately)

1. `.pre-commit` and AGENTS command parity.
2. Lane/capability trait normalization (canonical `End2End` taxonomy and capability ownership).
3. Stale docs cleanup and canonical policy link updates.
4. Optional policy-lint check to prevent AGENTS drift.
