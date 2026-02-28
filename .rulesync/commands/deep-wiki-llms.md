---
description: Generate llms.txt for LLM-friendly project access
targets: ['*']
---

# /deep-wiki:llms

Generate llms.txt and llms-full.txt for LLM-friendly project access

## Usage

````bash
/deep-wiki:llms
```text

## Description

Creates LLM-friendly documentation files following the llms.txt spec:

- `llms.txt`: Project summary with links to key documentation
- `llms-full.txt`: Complete inlined documentation

## Purpose

- Enables coding agents to discover and use project documentation
- Provides MCP-compatible documentation access
- Standard location for agent discovery

## Output

```text
./llms.txt                 # Root level
wiki/
└── llms-full.txt         # Full documentation
```text
````
