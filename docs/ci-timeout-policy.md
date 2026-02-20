# CI Lane and Timeout Policy

This repository uses lane-based execution to keep PR signal deterministic while preserving deeper coverage in merge/nightly lanes.

## Lane A (PR Blocking)
Workflow: `.github/workflows/lane-a-pr-blocking.yml`

- Unit tests timeout budget: 15 minutes
- Mocked integration tests timeout budget: 20 minutes
- Intended filters:
  - `Category=Unit`
  - `Category=Integration&Capability=RequiresMockServer`

## Lane B (Merge Gate)
Workflow: `.github/workflows/lane-b-merge-gate.yml`

- System tests timeout budget: 30 minutes
- Synthetic integration timeout budget: 30 minutes
- Intended filters:
  - `Category=System&Capability=RequiresSyntheticServer`
  - `Category=Integration&Capability=RequiresSyntheticServer`

## Lane C (Nightly)
Workflow: `.github/workflows/lane-c-nightly.yml`

- End2End timeout budget: 90 minutes
- Live provider timeout budget: 60 minutes
- Intended filters:
  - `Category=End2End`
  - `Capability=RequiresProviderLive`

## Policy Rules
- PR-blocking lanes MUST avoid public internet dependencies.
- Mock-server integration tests SHOULD use `Capability=RequiresMockServer`.
- Tests SHOULD define explicit timeout ceilings at method/class level.
- Hang diagnostics SHOULD be retained as TRX/artifact outputs for failed runs.
