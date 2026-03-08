---
name: dotnet-agent-harness-search
description: '$1'
targets: ['*']
portability: claude-opencode
flattening-risk: medium
simulated: true
version: '0.0.1'
author: 'dotnet-agent-harness'
claudecode:
  allowed-tools: ['Read', 'Grep', 'Glob', 'Bash']
copilot:
  description: 'Search skills by keyword'
codexcli:
  sandbox_mode: 'read-only'
---

# /dotnet-agent-harness:search

Use the local runtime instead of manually scanning `.rulesync/`.

## Execution Contract

Run:

```bash
dotnet agent-harness search <query> [--kind skill|subagent|command|persona] [--category value] [--platform value] [--limit N] [--format text|json]
```

Do not reimplement catalog ranking in the prompt. Execute the command first, then summarize the returned results.

## Notes

- `--kind` narrows results to `skill`, `subagent`, `command`, or `persona`
- `--category` matches tags, names, and descriptions
- `--platform` filters to generated target compatibility
- `--limit` defaults to `10`

## Example

```bash
dotnet agent-harness search "xunit testing" --kind skill --platform opencode --limit 5 --format json
```
