---
name: dotnet-agent-harness-incident
description: '$1'
targets: ['*']
portability: claude-opencode
flattening-risk: medium
simulated: true
version: '0.0.1'
author: 'dotnet-agent-harness'
claudecode:
  allowed-tools: ['Read', 'Write', 'Bash']
copilot:
  description: 'Create incident report'
codexcli:
  sandbox_mode: 'read-only'
---

# /dotnet-agent-harness:incident

Create incident records that point to existing prompt evidence artifacts.

## Execution Contract

```bash
dotnet agent-harness incident add <title> --prompt-evidence <evidence-id> [options]
dotnet agent-harness incident list [options]
dotnet agent-harness incident show <incident-id> [options]
dotnet agent-harness incident from-eval <artifact-path-or-id> [options]
dotnet agent-harness incident resolve <incident-id> --owner <name> --rationale <text> --regression-case <id> [options]
dotnet agent-harness incident close <incident-id> --owner <name> --rationale <text> --regression-case <id> [options]
```

## Options

- `--prompt-evidence <id>`: Required prepared-message evidence id
- `--incident-id <id>`: Stable incident identifier override
- `--severity <low|medium|high|critical>`: Incident severity
- `--owner <name>`: Responsible owner
- `--notes <text>`: Additional incident context
- `--format <text|json>`: Output format

For `from-eval`:

- `<artifact-path-or-id>`: Eval artifact JSON path or id
- `--prompt-evidence <id>`: Optional override if the eval artifact did not store prompt evidence
- Incidents inherit gate/profile failure context from the eval artifact

For `resolve` and `close`:

- `--owner <name>`: Required resolver/closer identity
- `--rationale <text>`: Required closure rationale
- `--regression-case <id>`: Required permanent regression case id
- `--regression-path <path>`: Optional linked eval file or testcase path
- `resolve` marks the incident as `resolved`
- `close` marks the incident as `closed`

## Output

Incident records are written to `.dotnet-agent-harness/incidents/<incident-id>.json` and include:

1. incident metadata
2. lifecycle status and optional resolution details
3. linked prompt evidence id and artifact paths
4. persona, platform, and target
5. raw request and enhanced request snapshot
6. optional eval gate/profile and failed-case context when created via `from-eval`

## Notes

- Use this after a production or evaluation failure so the exact prompt bundle is preserved with the incident.
- Pair with `compare-prompts` to diff the failing prompt against a later fixed version.
- Use `incident list` and `incident show` to inspect saved incidents without opening JSON manually.
- Use `incident resolve` or `incident close` only after the failure has a linked permanent regression case.
- Always call the runtime command instead of writing incident JSON by hand.
