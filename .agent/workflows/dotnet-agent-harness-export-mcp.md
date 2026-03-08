---
description: $1
trigger: /dotnet-agent-harness-export-mcp
turbo: true
---
# Workflow: /dotnet-agent-harness-export-mcp

# /dotnet-agent-harness:export-mcp

Export MCP-ready prompts and resources from `.rulesync/` instead of hand-curating a second source tree.

## Execution Contract

Run:

```bash
dotnet agent-harness export-mcp [--output directory] [--report-output path] [--platform generic|codexcli|claudecode|opencode|geminicli|copilot|antigravity|factorydroid] [--kind skill|subagent|command|persona|rule|mcp|all]
```

## Outputs

The export writes:

- `manifest.json`
- `prompts/index.json`
- `resources/index.json`
- copied prompt files under `prompts/`
- copied resource files under `resources/`

## Notes

- `.rulesync/` remains the source of truth
- `--output` is the export directory; use `--report-output` if you also want the JSON report written to a file
- `--platform` filters out content unsupported by the target runtime
- `--kind` narrows the export to one content class or keeps `all`
- rules from `.rulesync/rules/` and `.rulesync/mcp.json` are exported as resources when included

## Example

```bash
dotnet agent-harness export-mcp --platform geminicli --output .dotnet-agent-harness/exports/mcp
```

```bash
dotnet agent-harness export-mcp \
  --platform geminicli \
  --output .dotnet-agent-harness/exports/mcp \
  --report-output .dotnet-agent-harness/exports/mcp-report.json \
  --format json
```

// turbo
