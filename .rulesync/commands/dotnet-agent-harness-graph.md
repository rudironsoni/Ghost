---
name: dotnet-agent-harness-graph
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
  description: 'Generate skill dependency graph'
codexcli:
  sandbox_mode: 'read-only'
---

# /dotnet-agent-harness:graph

Generate a dependency graph from catalog references through the local runtime.

## Execution Contract

Run:

```bash
dotnet agent-harness graph [--item id|--skill id] [--kind skill|subagent|command|persona] [--category value] [--depth N] [--format mermaid|dot|json] [--output path]
```

## Notes

- `--item` or `--skill` scopes the graph to one catalog item and its reachable references
- `--kind` defaults to `skill`
- `--category` filters whole-catalog graph generation by tags or descriptions
- `--format mermaid` prints a Mermaid graph
- `--format dot` prints Graphviz DOT
- `--format json` returns graph nodes, edges, hubs, and orphan items

## Example

```bash
dotnet agent-harness graph --item dotnet-advisor --depth 2 --format mermaid
```
