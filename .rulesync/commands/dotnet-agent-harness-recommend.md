---
name: dotnet-agent-harness-recommend
description: '$1'
targets: ['*']
portability: copilot-gemini
flattening-risk: low
simulated: true
version: '0.0.1'
author: 'dotnet-agent-harness'
claudecode:
  allowed-tools: ['Read', 'Grep', 'Glob', 'Bash']
copilot:
  description: 'Recommend skills for project'
codexcli:
  sandbox_mode: 'read-only'
---

# /dotnet-agent-harness:recommend

Use the local runtime recommender instead of manually inferring the right toolkit content from `.rulesync/`.

## Execution Contract

Run:

```bash
dotnet agent-harness recommend [--format text|json] [--limit N] [--profile path] [--platform generic|codexcli|claudecode|opencode|geminicli|copilot|antigravity|factorydroid] [--category value] [--write-state]
```

## Notes

- `--platform` filters recommendations to surfaces that the selected runtime actually supports
- `--platform factorydroid` is valid, but it intentionally returns no skills, subagents, or commands because Factory
  Droid only consumes generated rules, hooks, and MCP config from RuleSync
- `--category` narrows matches by tags, names, and descriptions
- `--limit` applies per kind (`skill`, `subagent`, `command`)
- `--write-state` persists `.dotnet-agent-harness/recommendations.json` for later prompt assembly

## Example

```bash
dotnet agent-harness recommend --platform codexcli --category data --format json
```
