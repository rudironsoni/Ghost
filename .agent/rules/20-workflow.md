---
trigger: always_on
---
# Workflow

Use this sequence for repository updates:

1. Edit source content in `.rulesync/` (`rules`, `skills`, `subagents`, `commands`, `hooks`, `mcp`).
2. Validate configuration: `npx rulesync validate`.
3. Install declarative sources if configured: `npx rulesync install`.
4. Generate outputs: `npx rulesync generate`.
5. Ensure generated files are committed with source changes.

When adding or modifying hooks:

- Prefer canonical RuleSync event names in `.rulesync/hooks.json` (camelCase).
- Put shared hooks under `hooks` and tool-specific hooks under override blocks (`claudecode.hooks`, etc.).
- Keep shell hooks portable (`bash`, `jq`, standard POSIX utilities).
