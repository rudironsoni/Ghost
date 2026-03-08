---
name: wiki-llms-txt
category: developer-experience
subcategory: cli
description: Generates llms.txt for LLM-friendly project access
license: MIT
targets: ['claudecode', 'codexcli']
tags: [wiki, llm, documentation]
version: '1.0.0'
author: 'microsoft'
invocable: true
claudecode: {}
opencode: {}
codexcli:
  short-description: 'Toolkit guidance for wiki-llms-txt'
copilot: {}
geminicli: {}
antigravity: {}
---

# wiki-llms-txt

Generates llms.txt for LLM-friendly project access

## Trigger

- User asks to generate llms.txt
- User wants LLM-friendly docs

## Files Generated

- `llms.txt`: Project summary with links to key documentation
- `llms-full.txt`: Complete inlined documentation

## Purpose

- Enables coding agents to discover documentation
- Provides MCP-compatible documentation access
- Standard location for agent discovery

## Output

````text
./llms.txt
wiki/llms-full.txt
```text

## Agent Integration

Works with GitHub MCP Server for automatic documentation discovery.
````
