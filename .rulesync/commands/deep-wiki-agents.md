---
description: Generate AGENTS.md files for coding agent context
targets: ['*']
---

# /deep-wiki:agents

Generate AGENTS.md files for pertinent folders (only where missing)

## Usage

````bash
/deep-wiki:agents
```text

## Description

Analyzes codebase structure and generates `AGENTS.md` files for key directories where they don't already exist. These files provide context for AI coding agents.

## Criteria

- Generated only if `AGENTS.md` doesn't exist
- Focused on folders with significant logic
- Includes architecture overview
- Documents key patterns and conventions

## Output

```text
src/
├── AGENTS.md              # Root level
├── services/
│   └── AGENTS.md          # Service layer
├── api/
│   └── AGENTS.md          # API layer
└── ...
```text
````
