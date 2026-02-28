Please also reference the following rules as needed. The list below is provided in TOON format, and `@` stands for the project root directory.

rules[2]:
  - path: @.agents/memories/10-conventions.md
    description: "dotnet-harness authoring conventions for skills, subagents, commands, and hooks"
    applyTo[1]: .rulesync/**/*
  - path: @.agents/memories/20-workflow.md
    description: Workflow for RuleSync-based multi-agent generation
    applyTo[1]: **/*

# Additional Conventions Beyond the Built-in Functions

As this project's AI coding tool, you must follow the additional conventions below, in addition to the built-in functions.

# dotnet-harness

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

rulesync fetch rudironsoni/dotnet-harness:.rulesync
rulesync generate --targets "*" --features "*"

```bash

If you use declarative sources:

```jsonc

{
  "sources": [{ "source": "rudironsoni/dotnet-harness", "path": ".rulesync" }],
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
