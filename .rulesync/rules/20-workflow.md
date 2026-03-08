---
root: false
targets: ['claudecode', 'copilot', 'geminicli', 'antigravity']
description: 'Workflow for RuleSync-based multi-agent generation'
globs: ['**/*']
antigravity:
  trigger: always_on
---

# Workflow

Use this sequence for repository updates:

1. Edit source content only in supported RuleSync surfaces (`rules`, `skills`, `subagents`, `commands`, `hooks`, `mcp`) and repo-owned metadata outside `.rulesync/` when needed.
2. Do not hand-edit generated target directories; regenerate them from RuleSync after source changes.
3. Check source and generated consistency with the system `rulesync` binary: `rulesync generate --targets "<csv>" --features "*" --check`.
4. Install declarative sources if configured: `rulesync install`.
5. Regenerate outputs intentionally with the same binary: `rulesync generate --targets "<csv>" --features "*"`.
6. Review source and generated diffs together, then commit them together when the change is intentional.
7. **Verify target configurations** using per-target checklists in `.rulesync/verification/`:
   - Run automated check: `rulesync generate --check`
   - Use target checklists for manual verification (e.g., `verification/claudecode-checklist.md`)
   - Test in target environment where possible

Prefer the system-wide `rulesync` binary for normal validation and generation. The current CLI does not expose a separate
`rulesync validate` command, so use `generate --check` as the verification gate. Do not add a local npm install just to
run routine repository checks.

## Verification Strategy

After generating outputs, verify each target using the corresponding checklist:

| Target | Checklist | Key Verification Points |
|--------|-----------|------------------------|
| claudecode | [claudecode-checklist.md](../verification/claudecode-checklist.md) | All features, hooks fire, MCP connects |
| opencode | [opencode-checklist.md](../verification/opencode-checklist.md) | Primary agents, @mention works |
| copilot | [copilot-checklist.md](../verification/copilot-checklist.md) | Tool names, implicit subagents |
| geminicli | [geminicli-checklist.md](../verification/geminicli-checklist.md) | Portable hooks, no subagents |
| codexcli | [codexcli-checklist.md](../verification/codexcli-checklist.md) | Read-only sandbox, no commands |
| factorydroid | [factorydroid-checklist.md](../verification/factorydroid-checklist.md) | Rules-only, comprehensive root rule |
| antigravity | [antigravity-checklist.md](../verification/antigravity-checklist.md) | Concise rules, no MCP assumptions |

See [Verification README](../verification/README.md) for the complete verification guide.

When adding or modifying hooks:

- Prefer canonical RuleSync event names in `.rulesync/hooks.json` (camelCase).
- Put shared hooks under `hooks` and tool-specific hooks under override blocks (`claudecode.hooks`, etc.).
- Keep shell hooks portable (`bash`, `jq`, standard POSIX utilities).
