---
description: $1
trigger: /dotnet-agent-harness-compare-prompts
turbo: true
---
# Workflow: /dotnet-agent-harness-compare-prompts

# /dotnet-agent-harness:compare-prompts

Compare prepared prompt evidence artifacts stored under `.dotnet-agent-harness/evidence/prepared-messages/`.

## Execution Contract

```bash
dotnet agent-harness compare-prompts <left-evidence-id> <right-evidence-id> [--format text|json]
```

## Output

The comparison includes:

1. left and right evidence ids
2. persona, platform, and target metadata
3. per-section comparison for:
   - system
   - tool
   - skill
   - request
4. first differing line and left-only/right-only line samples

## Notes

- Compare prompt evidence ids produced by `prepare-message --write-evidence`.
- Use this to review prompt regressions before changing personas, tool policy, or request shaping.
- Execute the runtime command first, then summarize the diff rather than comparing prompt files manually.

// turbo
