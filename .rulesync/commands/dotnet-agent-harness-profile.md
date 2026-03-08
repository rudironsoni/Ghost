---
name: dotnet-agent-harness-profile
description: '$1'
targets: ['*']
portability: copilot-gemini
flattening-risk: low
simulated: true
version: '0.0.1'
author: 'dotnet-agent-harness'
claudecode:
  allowed-tools: ['Read', 'Glob', 'Bash']
copilot:
  description: 'Generate agent profile'
codexcli:
  sandbox_mode: 'read-only'
---

# /dotnet-agent-harness:profile

Use the runtime catalog profiler instead of hand-counting content.

## Execution Contract

Run one of:

```bash
dotnet agent-harness profile [--format text|json]
dotnet agent-harness profile <catalog-item-id> [--format text|json]
```

## Output

- without an id: catalog totals, line counts, and load-time summary
- with an id: item metadata, file path, tags, references, and approximate token estimate

## Example

```bash
dotnet agent-harness profile reviewer --format json
```
