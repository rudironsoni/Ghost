---
description: $1
trigger: /dotnet-agent-harness-test
turbo: true
---
# Workflow: /dotnet-agent-harness-test

# /dotnet-agent-harness:test

Run the local skill-test harness instead of interpreting `test-cases/` manually.

## Execution Contract

Run:

```bash
dotnet agent-harness test [skill <skill-name|all>|eval] [--format text|json|junit] [--filter value] [--fail-fast] [--platform codexcli] [--trials 3] [--unloaded-check] [--output path]
```

## Notes

- `test skill all` runs the entire authored skill suite
- `test eval` runs the cross-harness eval matrix from `tests/eval/cases/`
- `--platform` and `--trials` switch the command into eval mode when no explicit `eval` token is present
- `--unloaded-check` filters eval execution to retirement/unloaded cases
- `--format junit` is intended for CI systems
- `--filter` narrows test selection to matching case names
- `--fail-fast` stops on the first failing check

## Example

```bash
dotnet agent-harness test all --format junit --output results.xml
```

```bash
dotnet agent-harness test eval --platform codexcli --trials 3 --unloaded-check --artifact-id codex-retirement
```

// turbo
