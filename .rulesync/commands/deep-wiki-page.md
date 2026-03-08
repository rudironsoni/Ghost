---
name: deep-wiki-page
description: '$1'
targets: ['*']
portability: claude-opencode
flattening-risk: medium
simulated: true
version: '0.0.1'
author: 'dotnet-agent-harness'
claudecode:
  allowed-tools: ['Read', 'Grep', 'Glob', 'Bash']
copilot:
  description: 'Generate individual wiki page'
codexcli:
  sandbox_mode: 'read-only'
---

# /deep-wiki:page

Generate a single wiki page with dark-mode Mermaid diagrams

## Usage

````bash
/deep-wiki:page <topic>
```text

## Parameters

- `topic`: The specific component or system to document

## Description

Generates a focused documentation page for a specific topic including:

- Dark-mode Mermaid diagrams (3–5 minimum)
- Source-linked citations
- Architecture overview
- Usage examples
- Related components

## Examples

```bash
/deep-wiki:page Authentication System
/deep-wiki:page Database Layer
/deep-wiki:page API Gateway
```text

## Output

Single markdown file in `wiki/pages/<topic-slug>.md`
````
