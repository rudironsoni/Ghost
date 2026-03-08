---
name: dotnet-agent-harness-compare
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
  description: 'Compare skills across AI agent systems'
codexcli:
  sandbox_mode: 'read-only'
---

# /dotnet-agent-harness:compare

Compare two catalog items without manually diffing source files first.

## Execution Contract

Run:

```bash
dotnet agent-harness compare <left-id> <right-id> [--format text|json]
```

The runtime returns:

1. left and right item metadata
2. shared tags
3. tags unique to the left item
4. tags unique to the right item

## Example

```bash
dotnet agent-harness compare reviewer implementer --format json
```
