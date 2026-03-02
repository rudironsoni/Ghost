---
root: true
targets: ['*']
description: 'dotnet-agent-harness: Comprehensive .NET development skills for all AI agents'
globs: ['**/*']
antigravity:
  trigger: always_on
---

# dotnet-agent-harness

Comprehensive .NET development guidance for modern C#, ASP.NET Core, MAUI, Blazor, and cloud-native apps.

## Overview

This toolkit provides:

- 131 skills
- 14 specialist agents/subagents
- shared RuleSync rules, commands, hooks, and MCP config

Compatible targets include Claude Code, GitHub Copilot CLI, OpenCode, Codex CLI, Gemini CLI, and Antigravity.

## Recommended install

For full toolkit installation in a project:

````bash

rulesync fetch rudironsoni/dotnet-agent-harness:.rulesync
rulesync generate --targets "*" --features "*"

```bash

If you use declarative sources:

```jsonc

{
  "sources": [{ "source": "rudironsoni/dotnet-agent-harness", "path": ".rulesync" }],
}

```json

```bash

rulesync install && rulesync generate --targets "*" --features "*"

```bash

## OpenCode behavior

- Tab cycles **primary** agents only.
- `@mention` invokes subagents.
- `dotnet-architect` is configured as a primary OpenCode agent in this toolkit so it can appear in Tab rotation.

## Troubleshooting

If RuleSync reports `Multiple root rulesync rules found`, ensure only one root overview rule exists in
`.rulesync/rules/`.

## Contributing

Edit source files in `.rulesync/` and validate with `npm run ci:rulesync`.

## License

MIT License. See `LICENSE`.
````
