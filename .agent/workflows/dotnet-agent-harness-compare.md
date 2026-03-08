---
description: $1
trigger: /dotnet-agent-harness-compare
turbo: true
---
# Workflow: /dotnet-agent-harness-compare

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

// turbo
