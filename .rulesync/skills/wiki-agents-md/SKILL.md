---
name: wiki-agents-md
description: Generates AGENTS.md files for coding agent context
license: MIT
targets: ['claudecode', 'codexcli']
tags: ['wiki', 'agents', 'documentation']
version: '1.0.0'
author: 'microsoft'
invocable: true
---

# wiki-agents-md

Generates AGENTS.md files for coding agent context

## Trigger

- User asks to generate AGENTS.md files
- User wants coding agent context files

## Capabilities

- Analyzes codebase structure
- Generates AGENTS.md only where missing
- Focused on folders with significant logic

## Criteria

- Generated only if AGENTS.md doesn't exist
- Includes architecture overview
- Documents key patterns and conventions

## Output

````text
src/
├── AGENTS.md
├── services/
│   └── AGENTS.md
└── api/
    └── AGENTS.md
```text
````
